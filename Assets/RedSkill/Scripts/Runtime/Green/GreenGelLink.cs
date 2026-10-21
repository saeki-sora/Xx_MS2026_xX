using UnityEngine;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑の砲台からコアへ流れる「緑のジェル」。弓なりにうねる帯（メッシュ）を毎フレーム作り直し、
    /// 中を泡と小さなタブレットが流れ、ところどころ「ごくっ」とふくらみがコアへ運ばれていく（力を送っている見た目）。
    /// 握る強さ（boost 0〜1）で太く・速く・明るくなる。伸びる（砲台→コア）と縮む（根元がコアへ吸い込まれる）にも対応。
    /// </summary>
    internal sealed class GreenGelLink
    {
        private static readonly int CoreColorId = Shader.PropertyToID("_CoreColor");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int FlowId = Shader.PropertyToID("_Flow");

        private const float PulseSpacing = 2.6f;
        private const float PulseWidth = 0.12f;

        private readonly GreenMintFx _fx;
        private readonly Material _material;
        private readonly Mesh _mesh;
        private readonly MeshRenderer _renderer;
        private readonly SpriteRenderer[] _carried;
        private readonly MeshRenderer _inflowGlow;
        private readonly MeshRenderer _outflowGlow;

        private Vector3[] _vertices;
        private Vector4[] _uvs;
        private int[] _triangles;
        private int _segments = -1;
        private float _flow;
        private float _time;
        private float _phase;

        public GreenGelLink(GreenMintFx fx)
        {
            _fx = fx;
            var definition = fx.Definition;
            _material = GreenMintFx.CreateMaterial(definition.shaders.gel, "MS2026/GreenSkill/Gel", 3090);
            _mesh = new Mesh { name = "GreenSkillGel", hideFlags = HideFlags.HideAndDontSave };
            _mesh.MarkDynamic();

            var go = new GameObject("Gel") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(fx.Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;

            var link = definition.link;
            _carried = new SpriteRenderer[Mathf.Clamp(link.carriedTablets, 0, 24)];
            for (var i = 0; i < _carried.Length; i++)
            {
                _carried[i] = fx.CreateSprite("GelTablet" + i, fx.Art.Tablet, link.sortingOrder + 1);
            }

            _inflowGlow = fx.CreateQuad("GelInflow", fx.RadialMaterial, link.sortingOrder + 2);
            _outflowGlow = fx.CreateQuad("GelOutflow", fx.RadialMaterial, link.sortingOrder + 2);
            _phase = Random.Range(0f, 10f);
        }

        /// <summary>
        /// 帯を作り直す。tail〜head（0〜1、0=砲台 1=コア）の間だけを描く（伸びる・縮むの表現）。
        /// boost は握る強さ（0〜1）、strength は全体の濃さ（0〜1、消えるときに下げる）。
        /// </summary>
        public void Tick(Vector2 from, Vector2 to, float tail, float head, float boost, float strength, float dt)
        {
            var definition = _fx.Definition;
            var link = definition.link;
            var boostSettings = definition.boost;
            _time += dt;
            var flowSpeed = link.flowSpeed * Mathf.Lerp(1f, boostSettings.boostedFlowScale, boost);
            _flow += flowSpeed * dt;

            tail = Mathf.Clamp01(tail);
            head = Mathf.Clamp01(head);
            var total = Vector2.Distance(from, to);
            if (head - tail < 0.002f || total < 0.05f || strength <= 0.001f)
            {
                Hide();
                return;
            }

            var segments = Mathf.Clamp(link.segments, 8, 96);
            EnsureBuffers(segments);
            var widthScale = Mathf.Lerp(1f, boostSettings.boostedWidthScale, boost);
            var baseWidth = link.width * widthScale;
            var z = GreenMintFx.ZOffset + 0.03f;

            for (var i = 0; i <= segments; i++)
            {
                var t = Mathf.Lerp(tail, head, (float)i / segments);
                var point = PointAt(from, to, t, out var tangent);
                var normal = new Vector2(-tangent.y, tangent.x);
                var distance = t * total;
                var width = baseWidth * WidthProfile(t, distance) * Mathf.Lerp(0.6f, 1f, strength);
                var half = width * 0.5f;
                var a = point + normal * half;
                var b = point - normal * half;
                _vertices[i * 2] = new Vector3(a.x, a.y, z);
                _vertices[i * 2 + 1] = new Vector3(b.x, b.y, z);
                _uvs[i * 2] = new Vector4(distance, 0f, width, total);
                _uvs[i * 2 + 1] = new Vector4(distance, 1f, width, total);
            }

            _mesh.vertices = _vertices;
            _mesh.SetUVs(0, _uvs);
            _mesh.triangles = _triangles;
            _mesh.RecalculateBounds();

            var palette = definition.palette;
            _material.SetColor(CoreColorId, palette.mint);
            _material.SetColor(EdgeColorId, palette.green);
            _material.SetColor(RimColorId, palette.outline);
            _material.SetColor(GlowColorId, palette.glow);
            _material.SetVector(FlowId, new Vector4(_flow, link.opacity * strength, 1f, boost));
            _renderer.sortingOrder = link.sortingOrder;
            _renderer.enabled = true;

            UpdateCarried(from, to, tail, head, total, baseWidth, strength);
            UpdateGlows(from, to, tail, head, boost, strength, z);
        }

        public void Hide()
        {
            _renderer.enabled = false;
            for (var i = 0; i < _carried.Length; i++)
            {
                _carried[i].enabled = false;
            }

            _inflowGlow.enabled = false;
            _outflowGlow.enabled = false;
        }

        public void Dispose()
        {
            GreenMintUtil.DestroySafe(_mesh);
            GreenMintUtil.DestroySafe(_material);
        }

        /// <summary>帯の上の点（0=砲台 1=コア）。全体は弓なりにゆっくり揺れ、横にうねりが流れる。</summary>
        private Vector2 PointAt(Vector2 from, Vector2 to, float t, out Vector2 tangent)
        {
            var link = _fx.Definition.link;
            var axis = to - from;
            var length = Mathf.Max(axis.magnitude, 0.001f);
            var dir = axis / length;
            var normal = new Vector2(-dir.y, dir.x);

            var bow = link.bow * (0.75f + 0.25f * Mathf.Sin(_time * 0.9f + _phase));
            var envelope = Mathf.Sin(Mathf.PI * t);
            var wave = Mathf.Sin(Mathf.PI * 2f * (link.wobbleWaves * t) - _time * 3.2f + _phase) * link.wobbleAmount;
            var offset = bow * 4f * t * (1f - t) + wave * envelope;
            var point = from + axis * t + normal * offset;

            // 向き（少し先の点との差）。
            var t2 = Mathf.Min(1f, t + 0.01f);
            var envelope2 = Mathf.Sin(Mathf.PI * t2);
            var wave2 = Mathf.Sin(Mathf.PI * 2f * (link.wobbleWaves * t2) - _time * 3.2f + _phase) * link.wobbleAmount;
            var next = from + axis * t2 + normal * (bow * 4f * t2 * (1f - t2) + wave2 * envelope2);
            var delta = t2 > t ? next - point : dir;
            tangent = delta.sqrMagnitude > 1e-8f ? delta.normalized : dir;
            return point;
        }

        /// <summary>場所ごとの太さの倍率: 根元は少し細く、コアの手前で広がって流れ込む。ふくらみ（ごくっ）がコアへ運ばれていく。</summary>
        private float WidthProfile(float t, float distance)
        {
            var start = Mathf.Lerp(0.7f, 1f, Mathf.SmoothStep(0f, 1f, t / 0.12f));
            var end = 1f + 0.3f * Mathf.SmoothStep(0f, 1f, (t - 0.82f) / 0.18f);
            var phase = Mathf.Repeat((distance - _flow) / PulseSpacing, 1f) - 0.5f;
            var pulse = 1f + 0.22f * Mathf.Exp(-(phase * phase) / (PulseWidth * PulseWidth));
            return start * end * pulse;
        }

        private void UpdateCarried(Vector2 from, Vector2 to, float tail, float head, float total, float width, float strength)
        {
            if (_carried.Length == 0)
            {
                return;
            }

            var size = Mathf.Clamp(width * 0.55f, 0.18f, 0.6f);
            var spin = _time * 160f;
            for (var i = 0; i < _carried.Length; i++)
            {
                var renderer = _carried[i];
                var t = Mathf.Repeat((float)i / _carried.Length + _flow / Mathf.Max(total, 0.5f), 1f);
                if (t < tail + 0.02f || t > head - 0.02f)
                {
                    renderer.enabled = false;
                    continue;
                }

                var point = PointAt(from, to, t, out _);
                // コアに近づくと小さくなって吸い込まれる。
                var scale = size * Mathf.SmoothStep(0f, 1f, (1f - t) / 0.15f) * Mathf.SmoothStep(0f, 1f, t / 0.08f);
                var transform = renderer.transform;
                transform.SetPositionAndRotation(new Vector3(point.x, point.y, GreenMintFx.ZOffset + 0.02f), Quaternion.Euler(0f, 0f, spin + i * 47f));
                transform.localScale = new Vector3(scale, scale, 1f);
                var color = Color.white;
                color.a = strength;
                renderer.color = color;
                renderer.sortingOrder = _fx.Definition.link.sortingOrder + 1;
                renderer.enabled = scale > 0.01f;
            }
        }

        private void UpdateGlows(Vector2 from, Vector2 to, float tail, float head, float boost, float strength, float z)
        {
            var palette = _fx.Definition.palette;
            var width = _fx.Definition.link.width;
            var pulse = 0.8f + 0.2f * Mathf.Sin(_time * 9f);
            if (head > 0.98f)
            {
                var end = PointAt(from, to, 1f, out _);
                var size = width * (3.2f + boost * 2.5f) * pulse;
                _inflowGlow.transform.SetPositionAndRotation(new Vector3(end.x, end.y, z - 0.01f), Quaternion.identity);
                _inflowGlow.transform.localScale = new Vector3(size, size, 1f);
                _fx.ApplyRadial(_inflowGlow, palette.glow * (0.7f + boost * 0.6f) * strength,
                    new Vector4(0.5f, 2f, 0f, 0f), new Vector4(4f, 0f, 0.7f, 0.1f), new Vector4(0.6f, 0f, 0f, 0f));
                _inflowGlow.enabled = true;
            }
            else
            {
                _inflowGlow.enabled = false;
            }

            if (tail < 0.02f)
            {
                var start = PointAt(from, to, 0f, out _);
                var size = width * (2.4f + boost * 1.6f) * pulse;
                _outflowGlow.transform.SetPositionAndRotation(new Vector3(start.x, start.y, z - 0.01f), Quaternion.identity);
                _outflowGlow.transform.localScale = new Vector3(size, size, 1f);
                _fx.ApplyRadial(_outflowGlow, palette.glow * (0.45f + boost * 0.7f) * strength,
                    new Vector4(0.55f, 2f, 0f, 0f), new Vector4(4f, 0f, 0f, 0f), new Vector4(0f, 0f, 0f, 0f));
                _outflowGlow.enabled = true;
            }
            else
            {
                _outflowGlow.enabled = false;
            }
        }

        private void EnsureBuffers(int segments)
        {
            if (_segments == segments)
            {
                return;
            }

            _segments = segments;
            _vertices = new Vector3[(segments + 1) * 2];
            _uvs = new Vector4[(segments + 1) * 2];
            _triangles = new int[segments * 6];
            for (var i = 0; i < segments; i++)
            {
                var v = i * 2;
                var k = i * 6;
                _triangles[k] = v;
                _triangles[k + 1] = v + 2;
                _triangles[k + 2] = v + 1;
                _triangles[k + 3] = v + 1;
                _triangles[k + 4] = v + 2;
                _triangles[k + 5] = v + 3;
            }

            _mesh.Clear();
        }
    }
}
