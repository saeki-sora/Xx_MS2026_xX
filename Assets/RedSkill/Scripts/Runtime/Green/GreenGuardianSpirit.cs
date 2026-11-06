using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Green
{
    /// <summary>その瞬間の守護霊の姿（ランナーが毎フレーム決めて渡す）。</summary>
    internal struct GreenSpiritState
    {
        /// <summary>false なら隠す。</summary>
        public bool Visible;

        /// <summary>守護霊の中心（コアに重なる点）。</summary>
        public Vector2 Center;

        /// <summary>高さ（m）。</summary>
        public float Height;

        /// <summary>ポーズ: 0 = 両手を上げて降りる、1 = 両手を広げる、2 = コアを抱える（間は混ぜる）。</summary>
        public float Pose;

        /// <summary>濃さ（0〜1）。</summary>
        public float Alpha;

        /// <summary>まわりの光の強さ（0〜1）。</summary>
        public float Glow;

        /// <summary>画面で見た回転（度、左回り）。吸い込まれるときに回す。</summary>
        public float Spin;

        /// <summary>しっぽ: 0 = 出さない、1 = 足元で丸まる（降りている間）、2 = 緑の砲台へつながる。</summary>
        public int TailMode;

        /// <summary>しっぽの見えている範囲（根元 0 〜 先 1）。</summary>
        public float TailFrom;

        public float TailTo;

        /// <summary>しっぽの先（緑の砲台）。</summary>
        public Vector2 TailTarget;
    }

    /// <summary>
    /// 緑の守護霊。うすく透けた体（ポーズごとの1枚絵を重ねて切り替える、またはパーツ分けの両腕を肩で回す）、まわりの光、
    /// 足元から緑の砲台へつながるしっぽの煙（帯のメッシュ）。どのPCの画面でも、画面の上向きに立って見える（地面に寝かせて描く）。
    /// 敵・砲台・コアより下に描く（<see cref="GreenMintFx.UnderEnemiesQueue"/>）。
    /// </summary>
    internal sealed class GreenGuardianSpirit
    {
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int FillTopId = Shader.PropertyToID("_FillTop");
        private static readonly int FillBottomId = Shader.PropertyToID("_FillBottom");
        private static readonly int OutlineId = Shader.PropertyToID("_Outline");
        private static readonly int ParamsId = Shader.PropertyToID("_Params");

        private readonly GreenMintFx _fx;
        private readonly Material _bodyMaterial;
        private readonly SpriteRenderer _haloA;
        private readonly SpriteRenderer _haloB;
        private readonly SpriteRenderer _bodyA;
        private readonly SpriteRenderer _bodyB;
        private readonly SpriteRenderer _armLeft;
        private readonly SpriteRenderer _armRight;
        private readonly Material _tailMaterial;
        private readonly MeshRenderer _tailRenderer;
        private readonly Mesh _tailMesh;

        private Vector3[] _vertices = new Vector3[0];
        private Vector4[] _uvs = new Vector4[0];
        private int[] _triangles = new int[0];
        private readonly System.Collections.Generic.List<Vector4> _uvList = new System.Collections.Generic.List<Vector4>();

        public GreenGuardianSpirit(GreenMintFx fx)
        {
            _fx = fx;
            var queue = GreenMintFx.UnderEnemiesQueue;
            var haloMaterial = fx.CreateSpriteMaterial(null, queue - 3);
            _bodyMaterial = fx.CreateSpriteMaterial(null, queue);
            const int order = -40;
            _haloA = fx.CreateSprite("SpiritHaloA", null, order, haloMaterial);
            _haloB = fx.CreateSprite("SpiritHaloB", null, order + 1, haloMaterial);
            _bodyA = fx.CreateSprite("SpiritBodyA", null, order + 4, _bodyMaterial);
            _bodyB = fx.CreateSprite("SpiritBodyB", null, order + 5, _bodyMaterial);
            _armLeft = fx.CreateSprite("SpiritArmLeft", null, order + 6, _bodyMaterial);
            _armRight = fx.CreateSprite("SpiritArmRight", null, order + 6, _bodyMaterial);

            _tailMaterial = GreenMintFx.CreateMaterial(fx.Definition.shaders.tail, "MS2026/GreenSkill/Tail", queue - 2);
            var tailObject = new GameObject("SpiritTail") { hideFlags = HideFlags.DontSave };
            tailObject.transform.SetParent(fx.Root, false);
            _tailMesh = new Mesh { name = "GreenSpiritTail", hideFlags = HideFlags.HideAndDontSave };
            _tailMesh.MarkDynamic();
            tailObject.AddComponent<MeshFilter>().sharedMesh = _tailMesh;
            _tailRenderer = tailObject.AddComponent<MeshRenderer>();
            _tailRenderer.sharedMaterial = _tailMaterial;
            _tailRenderer.sortingOrder = order + 2;
            _tailRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _tailRenderer.receiveShadows = false;
            _tailRenderer.lightProbeUsage = LightProbeUsage.Off;
            _tailRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _tailRenderer.enabled = false;
        }

        public void Hide()
        {
            _haloA.enabled = false;
            _haloB.enabled = false;
            _bodyA.enabled = false;
            _bodyB.enabled = false;
            _armLeft.enabled = false;
            _armRight.enabled = false;
            _tailRenderer.enabled = false;
        }

        public void Dispose()
        {
            GreenMintUtil.DestroySafe(_tailMesh);
        }

        public void Apply(in GreenSpiritState state, float time)
        {
            if (!state.Visible || state.Alpha <= 0.001f || state.Height <= 0.01f)
            {
                Hide();
                return;
            }

            var definition = _fx.Definition;
            var art = definition.art;
            var palette = definition.palette;
            var up = GreenMintUtil.ScreenUp;
            // ふわりと少し揺れる（大きいので、ほんの少しだけ）。
            var sway = Mathf.Sin(time * 0.9f) * 1.2f;
            var rotation = GreenMintUtil.FacingUp(up) * Quaternion.Euler(0f, 0f, state.Spin + sway);
            var position = new Vector3(state.Center.x, state.Center.y, GreenMintFx.ZOffset + 0.03f);
            var glow = Mathf.Clamp01(state.Glow);
            var bright = 1f + 0.28f * glow;
            _bodyMaterial.SetColor(ColorId, new Color(bright, bright, bright, 1f));

            var pose = Mathf.Clamp(state.Pose, 0f, 2f);
            if (art.spiritMode == GreenSpiritArtMode.Parts && art.partBody != null)
            {
                ApplyParts(state, pose, rotation, position);
            }
            else
            {
                var lower = Mathf.Min(1, Mathf.FloorToInt(pose));
                var blend = Mathf.SmoothStep(0f, 1f, pose - lower);
                ApplyPosePicture(_bodyA, _haloA, lower, state, (1f - blend), glow, rotation, position);
                ApplyPosePicture(_bodyB, _haloB, lower + 1, state, blend, glow, rotation, position);
                _armLeft.enabled = false;
                _armRight.enabled = false;
            }

            ApplyTail(state, rotation, up, time, palette);
        }

        // ───────────── 体 ─────────────

        private Sprite PoseSprite(int pose, out bool procedural)
        {
            var art = _fx.Definition.art;
            var custom = pose == 0 ? art.poseDescend : pose == 1 ? art.poseSpread : art.poseHug;
            procedural = custom == null;
            return custom != null ? custom : _fx.Art.SpiritPoses[pose];
        }

        private void ApplyPosePicture(SpriteRenderer body, SpriteRenderer halo, int pose, in GreenSpiritState state, float weight, float glow,
            Quaternion rotation, Vector3 position)
        {
            if (weight <= 0.003f)
            {
                body.enabled = false;
                halo.enabled = false;
                return;
            }

            var sprite = PoseSprite(pose, out var procedural);
            var scale = procedural ? state.Height : state.Height / Mathf.Max(0.01f, sprite.bounds.size.y);
            body.sprite = sprite;
            body.transform.SetPositionAndRotation(position, rotation);
            body.transform.localScale = new Vector3(scale, scale, 1f);
            body.color = new Color(1f, 1f, 1f, state.Alpha * weight);
            body.enabled = true;

            if (procedural)
            {
                halo.sprite = _fx.Art.SpiritHalos[pose];
                var haloScale = scale * (1f + 0.02f * glow);
                halo.transform.SetPositionAndRotation(position + new Vector3(0f, 0f, 0.002f), rotation);
                halo.transform.localScale = new Vector3(haloScale, haloScale, 1f);
                halo.color = new Color(1f, 1f, 1f, state.Alpha * weight * Mathf.Lerp(0.3f, 1f, glow));
                halo.enabled = true;
            }
            else
            {
                halo.enabled = false;
            }
        }

        private void ApplyParts(in GreenSpiritState state, float pose, Quaternion rotation, Vector3 position)
        {
            var art = _fx.Definition.art;
            var body = art.partBody;
            var scale = state.Height / Mathf.Max(0.01f, body.bounds.size.y);
            _bodyA.sprite = body;
            _bodyA.transform.SetPositionAndRotation(position, rotation);
            _bodyA.transform.localScale = new Vector3(scale, scale, 1f);
            _bodyA.color = new Color(1f, 1f, 1f, state.Alpha);
            _bodyA.enabled = true;
            _bodyB.enabled = false;
            _haloA.enabled = false;
            _haloB.enabled = false;

            var angle = pose < 1f
                ? Mathf.Lerp(art.armDegrees.x, art.armDegrees.y, Mathf.SmoothStep(0f, 1f, pose))
                : Mathf.Lerp(art.armDegrees.y, art.armDegrees.z, Mathf.SmoothStep(0f, 1f, pose - 1f));
            PlaceArm(_armLeft, art.partArmLeft, new Vector2(art.shoulder.x, art.shoulder.y), angle, state, scale, rotation, position);
            PlaceArm(_armRight, art.partArmRight, new Vector2(-art.shoulder.x, art.shoulder.y), -angle, state, scale, rotation, position);
        }

        private static void PlaceArm(SpriteRenderer renderer, Sprite sprite, Vector2 shoulder, float angle, in GreenSpiritState state, float scale,
            Quaternion rotation, Vector3 position)
        {
            if (sprite == null)
            {
                renderer.enabled = false;
                return;
            }

            var offset = rotation * new Vector3(shoulder.x * state.Height, shoulder.y * state.Height, -0.001f);
            renderer.sprite = sprite;
            renderer.transform.SetPositionAndRotation(position + offset, rotation * Quaternion.Euler(0f, 0f, angle));
            renderer.transform.localScale = new Vector3(scale, scale, 1f);
            renderer.color = new Color(1f, 1f, 1f, state.Alpha);
            renderer.enabled = true;
        }

        // ───────────── しっぽの煙 ─────────────

        private void ApplyTail(in GreenSpiritState state, Quaternion rotation, Vector2 up, float time, GreenGuardianPalette palette)
        {
            var settings = _fx.Definition.spirit;
            var from = Mathf.Clamp01(state.TailFrom);
            var to = Mathf.Clamp01(state.TailTo);
            if (state.TailMode == 0 || to - from < 0.01f)
            {
                _tailRenderer.enabled = false;
                return;
            }

            var h = state.Height;
            var right = GreenMintUtil.RightOf(up);
            var root = (Vector2)(rotation * (Vector3)(_fx.Definition.art.tailRoot * h));
            var p0 = state.Center + root;
            Vector2 p1, p2, p3;
            float rootWidth, tipWidth;
            float wave;
            if (state.TailMode == 2)
            {
                p3 = state.TailTarget + up * 0.2f;
                var delta = p3 - p0;
                var length = Mathf.Max(delta.magnitude, 0.01f);
                var normal = new Vector2(-delta.y, delta.x) / length;
                p1 = p0 + right * (Mathf.Sin(time * 1.3f) * 0.05f * h) - up * Mathf.Min(0.2f * h, 0.45f * length);
                p2 = p0 + delta * 0.72f + normal * (Mathf.Sin(time * 1.7f + 1f) * 1.1f);
                rootWidth = settings.tailWidth.x * h;
                tipWidth = settings.tailWidth.y;
                wave = 0.16f;
            }
            else
            {
                // 降りている間は、足元でくるっと丸まった短いしっぽ。
                p1 = p0 + right * (-0.05f * h) - up * (0.15f * h);
                p2 = p0 + right * (0.16f * h) - up * (0.27f * h);
                p3 = p0 + right * ((0.12f + Mathf.Sin(time * 1.4f) * 0.03f) * h) - up * (0.41f * h);
                rootWidth = settings.tailWidth.x * h;
                tipWidth = 0.015f * h;
                wave = 0.15f;
            }

            var segments = Mathf.Clamp(settings.tailSegments, 8, 96);
            var count = segments + 1;
            EnsureBuffers(count);

            // 点と太さ。
            var z = GreenMintFx.ZOffset + 0.035f;
            var total = 0f;
            var previous = Bezier(p0, p1, p2, p3, from);
            for (var i = 0; i < count; i++)
            {
                var s = Mathf.Lerp(from, to, i / (float)segments);
                var p = Bezier(p0, p1, p2, p3, s);
                var tangent = BezierTangent(p0, p1, p2, p3, s);
                if (tangent.sqrMagnitude < 1e-6f)
                {
                    tangent = p - previous;
                }

                tangent = tangent.sqrMagnitude > 1e-8f ? tangent.normalized : Vector2.up;
                var normal = new Vector2(-tangent.y, tangent.x);
                var shape = state.TailMode == 2 ? Mathf.Pow(s, 0.55f) : s;
                var radius = Mathf.Lerp(rootWidth, tipWidth, shape) * (1f + wave * Mathf.Sin(s * 20f - time * 5f));
                radius = Mathf.Max(radius, 0.02f);
                total += i == 0 ? 0f : (p - previous).magnitude;
                previous = p;
                _vertices[i * 2] = new Vector3(p.x + normal.x * radius, p.y + normal.y * radius, z);
                _vertices[i * 2 + 1] = new Vector3(p.x - normal.x * radius, p.y - normal.y * radius, z);
                _uvs[i * 2] = new Vector4(total, 0f, radius, 0f);
                _uvs[i * 2 + 1] = new Vector4(total, 1f, radius, 0f);
            }

            for (var i = 0; i < _uvs.Length; i++)
            {
                _uvs[i].w = total;
            }

            _tailMesh.Clear();
            _tailMesh.vertices = _vertices;
            _uvList.Clear();
            _uvList.AddRange(_uvs);
            _tailMesh.SetUVs(0, _uvList);
            _tailMesh.triangles = _triangles;
            _tailMesh.RecalculateBounds();

            var fill = settings.fillOpacity;
            var top = palette.spiritMiddle;
            top.a = fill;
            var bottom = palette.spiritBottom;
            bottom.a = fill;
            _tailMaterial.SetColor(FillTopId, top);
            _tailMaterial.SetColor(FillBottomId, bottom);
            _tailMaterial.SetColor(OutlineId, palette.spiritOutline);
            _tailMaterial.SetVector(ParamsId, new Vector4(Mathf.Max(0.05f, 0.006f * h), state.Alpha, Mathf.Min(0.6f, 0.06f * h), 0.12f));
            _tailRenderer.enabled = true;
        }

        private void EnsureBuffers(int count)
        {
            if (_vertices.Length == count * 2)
            {
                return;
            }

            _vertices = new Vector3[count * 2];
            _uvs = new Vector4[count * 2];
            _triangles = new int[(count - 1) * 6];
            for (var i = 0; i < count - 1; i++)
            {
                var v = i * 2;
                var t = i * 6;
                _triangles[t] = v;
                _triangles[t + 1] = v + 2;
                _triangles[t + 2] = v + 1;
                _triangles[t + 3] = v + 1;
                _triangles[t + 4] = v + 2;
                _triangles[t + 5] = v + 3;
            }

            _tailMesh.Clear();
        }

        private static Vector2 Bezier(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float s)
        {
            var m = 1f - s;
            return m * m * m * a + 3f * m * m * s * b + 3f * m * s * s * c + s * s * s * d;
        }

        private static Vector2 BezierTangent(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float s)
        {
            var m = 1f - s;
            return 3f * m * m * (b - a) + 6f * m * s * (c - b) + 3f * s * s * (d - c);
        }
    }
}
