using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// 青スキルの見た目を作る共通の道具（板・マテリアル・パーティクル・グラデーションの焼き込み）。
    /// プレハブや画像は使わず、専用シェーダー3つ（Vortex / Glow / Particle）とコードだけで作る。
    /// 作った物はすべて保存されない一時オブジェクト（HideFlags.DontSave）。
    /// </summary>
    internal static class BlueFx
    {
        public const string VortexShaderName = "MS2026/BlueSkill/Vortex";
        public const string GlowShaderName = "MS2026/BlueSkill/Glow";
        public const string ParticleShaderName = "MS2026/BlueSkill/Particle";

        /// <summary>敵や床より手前（上）に重ねる演出の renderQueue。</summary>
        public const int OverlayRenderQueue = 3100;

        /// <summary>敵（2990）より上、SpriteRenderer（3000）より下に描く光の renderQueue（ボトルの後ろの光）。</summary>
        public const int BehindSpriteRenderQueue = 2995;

        /// <summary>床に落とす影の renderQueue（敵より下）。</summary>
        public const int GroundShadowRenderQueue = 2985;

        public static readonly int ColorId = Shader.PropertyToID("_Color");
        public static readonly int ShapeAId = Shader.PropertyToID("_ShapeA");
        public static readonly int ShapeBId = Shader.PropertyToID("_ShapeB");
        public static readonly int ShapeCId = Shader.PropertyToID("_ShapeC");
        public static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        public static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        public static readonly int ModeId = Shader.PropertyToID("_Mode");

        private static Mesh _sharedQuad;
        private static Sprite _softCircle;

        /// <summary>1×1 m の四角い板（中心が原点、XY 平面）。</summary>
        public static Mesh SharedQuad
        {
            get
            {
                if (_sharedQuad != null)
                {
                    return _sharedQuad;
                }

                _sharedQuad = new Mesh
                {
                    name = "BlueSkillQuad",
                    hideFlags = HideFlags.HideAndDontSave,
                    vertices = new[]
                    {
                        new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                        new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                    },
                    uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) },
                    triangles = new[] { 0, 2, 1, 2, 3, 1 }
                };
                _sharedQuad.RecalculateBounds();
                return _sharedQuad;
            }
        }

        /// <summary>ふちがぼやけた白い丸（飛んでいるボトルの影に使う）。</summary>
        public static Sprite SoftCircle
        {
            get
            {
                if (_softCircle != null)
                {
                    return _softCircle;
                }

                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "BlueSkillSoftCircle",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                {
                    for (var x = 0; x < size; x++)
                    {
                        var dx = (x + 0.5f) / size * 2f - 1f;
                        var dy = (y + 0.5f) / size * 2f - 1f;
                        var r = Mathf.Sqrt(dx * dx + dy * dy);
                        var a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.45f, 1f, r));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                _softCircle = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
                _softCircle.hideFlags = HideFlags.HideAndDontSave;
                return _softCircle;
            }
        }

        public static Material CreateMaterial(Shader shader, string fallbackName, int renderQueue)
        {
            if (shader == null)
            {
                shader = Shader.Find(fallbackName);
            }

            if (shader == null)
            {
                Debug.LogWarning($"[BlueSkill] シェーダー {fallbackName} が見つかりません。スキルの設定ファイルの「シェーダー」欄を確認してください。");
                shader = Shader.Find("Sprites/Default");
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = renderQueue };
        }

        public static MeshRenderer CreateRenderer(Transform parent, string objectName, Mesh mesh, Material material, int sortingOrder)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>
        /// コードから1粒ずつ出すためのパーティクル（自分では湧かない、ワールド座標）。
        /// velocityScale &gt; 0 なら動く向きに伸びる（しぶき）、0 なら丸のまま。
        /// </summary>
        public static ParticleSystem CreateParticles(Transform parent, string objectName, Material material, int sortingOrder,
            float velocityScale, float lengthScale, int maxParticles, float drag, float gravity)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = maxParticles;
            main.startSpeed = 0f;
            main.startLifetime = 0.5f;
            main.startSize = 0.2f;
            main.gravityModifier = gravity;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.08f), new GradientAlphaKey(0.85f, 0.55f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.35f)));

            if (drag > 0f)
            {
                var limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.drag = drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = false;
                limit.limit = 1000f;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (velocityScale > 0f)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = velocityScale;
                renderer.lengthScale = lengthScale;
            }
            else
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
            }

            system.Play();
            return system;
        }

        public static void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, float size, float lifetime, Color color)
        {
            if (system == null)
            {
                return;
            }

            var parameters = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color
            };
            system.Emit(parameters, 1);
        }

        /// <summary>毎秒の数×経過秒を整数の粒数にする（端数は次のフレームへ持ち越す）。</summary>
        public static int Accumulate(ref float carry, float amount)
        {
            carry += Mathf.Max(0f, amount);
            var count = Mathf.FloorToInt(carry);
            carry -= count;
            return Mathf.Min(count, 200);
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            var rad = degrees * Mathf.Deg2Rad;
            var s = Mathf.Sin(rad);
            var c = Mathf.Cos(rad);
            return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
        }

        public static Vector2 RandomDirection()
        {
            var a = Random.Range(0f, Mathf.PI * 2f);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>グラデーションを横1列のテクスチャ（HDR、リニア空間に合わせて変換）に焼く。</summary>
        public static Texture2D BakeGradient(Gradient gradient, int resolution, Texture2D existing)
        {
            resolution = Mathf.Clamp(resolution, 8, 512);
            var texture = existing;
            if (texture == null || texture.width != resolution)
            {
                if (texture != null)
                {
                    Object.Destroy(texture);
                }

                texture = new Texture2D(resolution, 1, TextureFormat.RGBAHalf, false, true)
                {
                    name = "BlueSkillGradient",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }

            var linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            var pixels = new Color[resolution];
            for (var i = 0; i < resolution; i++)
            {
                var c = gradient != null ? gradient.Evaluate(i / (resolution - 1f)) : Color.white;
                pixels[i] = linear ? new Color(c.linear.r, c.linear.g, c.linear.b, c.a) : c;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>カメラを揺らす（各PCが自分の画面だけ揺らす。ownerViewOnly なら投げた人の画面のときだけ）。</summary>
        public static void Shake(SimpleShakeSettings settings, float strength, bool ownerViewOnly, int owner)
        {
            if (settings == null || strength <= 0f)
            {
                return;
            }

            var rig = FortressCameraRig.Active;
            if (rig == null)
            {
                return;
            }

            if (ownerViewOnly && rig.CurrentViewer != owner)
            {
                return;
            }

            rig.AddModifier(new SimpleShakeModifier(settings, strength));
        }

        public static void Destroy(Object obj)
        {
            if (obj == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Object.Destroy(obj);
            }
            else
            {
                Object.DestroyImmediate(obj);
            }
        }
    }

    /// <summary>Glow シェーダーの板1枚（光・輪・トゲ）。</summary>
    internal sealed class BlueGlow
    {
        private readonly MeshRenderer _renderer;
        private readonly Transform _transform;

        public BlueGlow(MeshRenderer renderer)
        {
            _renderer = renderer;
            _transform = renderer.transform;
        }

        public bool Visible => _renderer.enabled;

        public void Show(Vector3 position, float rotationDegrees, Vector2 scale)
        {
            _transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, rotationDegrees));
            _transform.localScale = new Vector3(scale.x, scale.y, 1f);
            _renderer.enabled = scale.x > 0.001f && scale.y > 0.001f;
        }

        public void Hide()
        {
            _renderer.enabled = false;
        }

        /// <param name="color">色（HDR）。アルファは濃さ。</param>
        /// <param name="shapeA">x=芯の大きさ(0〜1), y=芯のしぼり, z=トゲの本数, w=トゲの強さ</param>
        /// <param name="shapeB">x=トゲの細さ, y=回転(ラジアン), z=輪の半径(0〜1), w=輪の太さ</param>
        /// <param name="shapeC">x=輪の強さ, y=点線の数(0=つながった輪), z=トゲの乱数の種, w=トゲの長さのばらつき</param>
        public void Apply(MaterialPropertyBlock block, Color color, Vector4 shapeA, Vector4 shapeB, Vector4 shapeC)
        {
            block.Clear();
            block.SetColor(BlueFx.ColorId, color);
            block.SetVector(BlueFx.ShapeAId, shapeA);
            block.SetVector(BlueFx.ShapeBId, shapeB);
            block.SetVector(BlueFx.ShapeCId, shapeC);
            _renderer.SetPropertyBlock(block);
        }

        /// <summary>ふわっとした丸い光。</summary>
        public void ApplySoft(MaterialPropertyBlock block, Color color, float corePower)
        {
            Apply(block, color, new Vector4(1f, corePower, 0f, 0f), Vector4.zero, Vector4.zero);
        }

        /// <summary>輪だけ（width は 0〜1 の半径に対する太さ）。dashes &gt; 0 なら点線。</summary>
        public void ApplyRing(MaterialPropertyBlock block, Color color, float radius, float width, float dashes, float rotationRadians)
        {
            Apply(block, color, Vector4.zero, new Vector4(0f, rotationRadians, radius, width), new Vector4(1f, dashes, 0f, 0f));
        }
    }

    /// <summary>広がって消える輪（衝撃波・波紋）の使い回し。</summary>
    internal sealed class BlueRingPool
    {
        private sealed class Ring
        {
            public BlueGlow Glow;
            public Vector3 Position;
            public float Delay;
            public float Age;
            public float Life;
            public float From;
            public float To;
            public float Width;
            public Color Color;
            public bool Active;
        }

        private readonly Ring[] _rings;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();

        public BlueRingPool(Transform parent, string prefix, Material material, int sortingOrder, int size)
        {
            _rings = new Ring[size];
            for (var i = 0; i < size; i++)
            {
                _rings[i] = new Ring { Glow = new BlueGlow(BlueFx.CreateRenderer(parent, prefix + i, BlueFx.SharedQuad, material, sortingOrder)) };
            }
        }

        /// <param name="from">始まりの直径（m）</param>
        /// <param name="to">終わりの直径（m）</param>
        /// <param name="width">輪の太さ（0〜1、半径に対する割合）</param>
        public void Spawn(Vector3 position, float from, float to, float life, Color color, float width = 0.08f, float delay = 0f)
        {
            if (to <= 0.001f || life <= 0f)
            {
                return;
            }

            var ring = _rings[0];
            for (var i = 0; i < _rings.Length; i++)
            {
                if (!_rings[i].Active)
                {
                    ring = _rings[i];
                    break;
                }

                if (_rings[i].Age / _rings[i].Life > ring.Age / ring.Life)
                {
                    ring = _rings[i];
                }
            }

            ring.Active = true;
            ring.Position = position;
            ring.Delay = delay;
            ring.Age = 0f;
            ring.Life = life;
            ring.From = from;
            ring.To = to;
            ring.Width = width;
            ring.Color = color;
        }

        public void Tick(float dt)
        {
            for (var i = 0; i < _rings.Length; i++)
            {
                var ring = _rings[i];
                if (!ring.Active)
                {
                    continue;
                }

                if (ring.Delay > 0f)
                {
                    ring.Delay -= dt;
                    ring.Glow.Hide();
                    continue;
                }

                ring.Age += dt;
                var t = ring.Age / ring.Life;
                if (t >= 1f)
                {
                    ring.Active = false;
                    ring.Glow.Hide();
                    continue;
                }

                var ease = 1f - (1f - t) * (1f - t) * (1f - t);
                var size = Mathf.Lerp(ring.From, ring.To, ease);
                var color = ring.Color;
                color.a *= (1f - t) * (1f - t);
                ring.Glow.Show(ring.Position, 0f, new Vector2(size, size));
                ring.Glow.ApplyRing(_block, color, 0.8f, ring.Width * (1f + t * 0.6f), 0f, 0f);
            }
        }

        public void Clear()
        {
            for (var i = 0; i < _rings.Length; i++)
            {
                _rings[i].Active = false;
                _rings[i].Glow.Hide();
            }
        }
    }

    /// <summary>
    /// 中心のまわりをぐるぐる回りながら吸い込まれていく粒（渦に巻き込まれる水しぶき・ボトルに集まる水の粒）。
    /// 位置をコードで毎フレーム計算し、パーティクルにそのまま渡す（ParticleSystem.SetParticles）。
    /// </summary>
    internal sealed class BlueSpiralParticles
    {
        private struct Item
        {
            public float Angle;
            public float Radius;
            public float AngularSpeed;
            public float RadialSpeed;
            public float Age;
            public float Life;
            public float Size;
            public float Lift;
            public Color Color;
        }

        private readonly ParticleSystem _system;
        private readonly List<Item> _items = new List<Item>(128);
        private ParticleSystem.Particle[] _buffer = new ParticleSystem.Particle[128];
        private readonly int _max;

        public BlueSpiralParticles(ParticleSystem system, int max)
        {
            _system = system;
            _max = max;
            if (system != null)
            {
                // 位置・色・大きさはこちらで毎フレーム決めるので、パーティクル側の「寿命による変化」は切る。
                var colorOverLifetime = system.colorOverLifetime;
                colorOverLifetime.enabled = false;
                var sizeOverLifetime = system.sizeOverLifetime;
                sizeOverLifetime.enabled = false;
                var limit = system.limitVelocityOverLifetime;
                limit.enabled = false;
            }
        }

        public int Count => _items.Count;

        /// <param name="angularSpeed">回る速さ（ラジアン/秒、＋で反時計回り）</param>
        /// <param name="radialSpeed">中心へ近づく速さ（m/秒、＋で内側へ）</param>
        /// <param name="lift">床からの高さ（m、カメラの方向）</param>
        public void Add(float angle, float radius, float angularSpeed, float radialSpeed, float life, float size, Color color, float lift = 0f)
        {
            if (_items.Count >= _max)
            {
                return;
            }

            _items.Add(new Item
            {
                Angle = angle,
                Radius = radius,
                AngularSpeed = angularSpeed,
                RadialSpeed = radialSpeed,
                Life = Mathf.Max(0.05f, life),
                Size = size,
                Lift = lift,
                Color = color
            });
        }

        /// <summary>動かして描く。innerRadius より内側へ入った粒は消える。</summary>
        public void Tick(float dt, Vector3 center, float innerRadius, float speedMultiplier)
        {
            if (_system == null)
            {
                return;
            }

            for (var i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                item.Age += dt;
                // 内側ほど速く回る（角運動量を保つ感じ）。
                var spin = item.AngularSpeed * Mathf.Clamp(1f / Mathf.Max(item.Radius, 0.15f), 0.6f, 5f);
                item.Angle += spin * dt * speedMultiplier;
                item.Radius -= item.RadialSpeed * dt * speedMultiplier;
                if (item.Age >= item.Life || item.Radius <= innerRadius)
                {
                    _items.RemoveAt(i);
                    continue;
                }

                _items[i] = item;
            }

            if (_buffer.Length < _items.Count)
            {
                _buffer = new ParticleSystem.Particle[Mathf.NextPowerOfTwo(_items.Count)];
            }

            for (var i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                var t = item.Age / item.Life;
                var fade = Mathf.Clamp01(t * 8f) * (1f - t * t);
                var dir = new Vector2(Mathf.Cos(item.Angle), Mathf.Sin(item.Angle));
                var tangent = new Vector2(-dir.y, dir.x) * Mathf.Sign(item.AngularSpeed);
                var speed = Mathf.Abs(item.AngularSpeed) * Mathf.Clamp(1f / Mathf.Max(item.Radius, 0.15f), 0.6f, 5f) * item.Radius;
                var color = item.Color;
                color.a *= fade;
                var position = center + (Vector3)(dir * item.Radius) + new Vector3(0f, 0f, -item.Lift);
                var particle = new ParticleSystem.Particle
                {
                    position = position,
                    velocity = (Vector3)(tangent * speed - dir * item.RadialSpeed),
                    startSize = item.Size * (0.6f + 0.4f * (1f - t)),
                    startColor = color,
                    remainingLifetime = 10f,
                    startLifetime = 10f
                };
                _buffer[i] = particle;
            }

            _system.SetParticles(_buffer, _items.Count);
        }

        public void Clear()
        {
            _items.Clear();
            if (_system != null)
            {
                _system.Clear();
            }
        }
    }
}
