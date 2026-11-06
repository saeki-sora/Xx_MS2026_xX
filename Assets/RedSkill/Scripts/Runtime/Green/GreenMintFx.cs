using System;
using MS2026.Fortress.Billboards;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑スキルの演出の道具箱（1人ぶん）。光の粒・ミントの粉・葉っぱ・回復の十字・煙（パーティクル）、光（Radial の板）、広がる衝撃波の輪、
    /// 当たったときの白い線を、どこからでも「ここに出して」と呼べるようにまとめる。見た目はすべてシーンの一番上の `[GreenSkill] Pn` の下に作る（保存されない）。
    /// 粉・葉っぱ・十字・煙は明るい床の上でも読めるよう、光を足す（加算）のではなく普通に重ねて描く。光の粒と光の板だけ加算。
    /// 描く順番: 守護霊としっぽ（<see cref="UnderEnemiesQueue"/>）は敵より下。葉・粒・光は敵より上。
    /// </summary>
    internal sealed class GreenMintFx : IDisposable
    {
        public const float ZOffset = -0.08f;

        /// <summary>敵（群衆 2990）より先に描く順番。守護霊・しっぽ・地面の光はここから下を使う。</summary>
        public const int UnderEnemiesQueue = 2986;

        /// <summary>敵より上の絵（葉・十字・煙など）の順番。</summary>
        public const int OverQueue = 3108;

        public const int SortingBase = 160;

        private const string RadialShaderName = "MS2026/RedSkill/Radial";
        private const string ParticleShaderName = "MS2026/RedSkill/Particle";
        private const string SpriteShaderName = "MS2026/GreenSkill/Sprite";
        private const int GlowPoolSize = 24;
        private const int WavePoolSize = 10;
        private const int SlashPoolSize = 16;

        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int ShapeAId = Shader.PropertyToID("_ShapeA");
        private static readonly int ShapeBId = Shader.PropertyToID("_ShapeB");
        private static readonly int ShapeCId = Shader.PropertyToID("_ShapeC");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int LineModeId = Shader.PropertyToID("_LineMode");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        private static Mesh _sharedQuad;

        private readonly GameObject _root;
        private readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        private readonly ParticleSystem _sparkles;
        private readonly ParticleSystem _powder;
        private readonly ParticleSystem _leaves;
        private readonly ParticleSystem _crosses;
        private readonly ParticleSystem _smoke;
        private readonly Flash[] _flashes = new Flash[GlowPoolSize];
        private readonly Wave[] _waves = new Wave[WavePoolSize];
        private readonly Slash[] _slashes = new Slash[SlashPoolSize];
        private readonly Material _overSpriteMaterial;

        public GreenMintFx(GreenMintSkill definition, int playerIndex)
        {
            Definition = definition;
            Art = new GreenMintArt(definition.palette, definition.spirit.fillOpacity);
            _root = new GameObject("[GreenSkill] P" + (playerIndex + 1)) { hideFlags = HideFlags.DontSave };
            // 地面に寝かせて描くので、自動のビルボード（立たせる処理）の対象から外す。
            _root.AddComponent<BillboardIgnore>();

            RadialMaterial = CreateMaterial(definition.shaders.radial, RadialShaderName, 3100);
            GroundGlowMaterial = CreateMaterial(definition.shaders.radial, RadialShaderName, UnderEnemiesQueue + 2);
            SparkMaterial = CreateMaterial(definition.shaders.particle, ParticleShaderName, 3110);
            SparkMaterial.SetFloat(IntensityId, 2.2f);
            SparkMaterial.SetFloat(SoftnessId, 1.4f);
            TrailMaterial = CreateMaterial(definition.shaders.particle, ParticleShaderName, 3104);
            TrailMaterial.SetFloat(LineModeId, 1f);
            TrailMaterial.SetFloat(SoftnessId, 1.2f);
            TrailMaterial.SetFloat(IntensityId, 1.3f);
            _overSpriteMaterial = CreateSpriteMaterial(null, OverQueue);

            var smokeTexture = definition.art.smoke != null ? definition.art.smoke : Art.SmokeDot;
            var leafTexture = definition.art.leaf != null ? definition.art.leaf.texture : Art.Leaf.texture;
            const int order = SortingBase;
            _smoke = CreateParticles("Smoke", CreateSpriteMaterial(smokeTexture, OverQueue - 2), order + 2, 500, 1.1f,
                new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.7f)));
            var noise = _smoke.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.45f;
            noise.scrollSpeed = 0.3f;
            noise.damping = false;
            _powder = CreateParticles("Powder", CreateSpriteMaterial(Art.PowderDot, OverQueue), order + 4, 500, 2.4f,
                new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.6f)));
            _leaves = CreateParticles("Leaves", CreateSpriteMaterial(leafTexture, OverQueue), order + 5, 200, 1.5f,
                new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.85f)));
            var leafRotation = _leaves.rotationOverLifetime;
            leafRotation.enabled = true;
            leafRotation.z = new ParticleSystem.MinMaxCurve(-5f, 5f);
            _crosses = CreateParticles("Crosses", CreateSpriteMaterial(Art.Cross, OverQueue), order + 6, 120, 0.8f,
                new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.12f, 1.25f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.7f)));
            _sparkles = CreateParticles("Sparkles", SparkMaterial, order + 7, 1200, 2f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));

            for (var i = 0; i < _flashes.Length; i++)
            {
                _flashes[i] = new Flash { Renderer = CreateQuad("Flash" + i, RadialMaterial, order + 3) };
            }

            for (var i = 0; i < _waves.Length; i++)
            {
                _waves[i] = new Wave { Renderer = CreateSprite("Wave" + i, Art.WaveRing, order - 2, _overSpriteMaterial) };
            }

            var slashMaterial = CreateMaterial(definition.shaders.sprite, SpriteShaderName, 3112);
            for (var i = 0; i < _slashes.Length; i++)
            {
                _slashes[i] = new Slash { Renderer = CreateSprite("Slash" + i, Art.Slash, order + 8, slashMaterial) };
            }
        }

        public GreenMintSkill Definition { get; }

        public GreenMintArt Art { get; }

        public Transform Root => _root.transform;

        /// <summary>光の板（加算・敵より上）。</summary>
        public Material RadialMaterial { get; }

        /// <summary>地面の光（加算・敵より下。コアや砲台の足元を光らせる）。</summary>
        public Material GroundGlowMaterial { get; }

        /// <summary>光の粒（加算）。</summary>
        public Material SparkMaterial { get; }

        /// <summary>光の尾（加算、線）。</summary>
        public Material TrailMaterial { get; }

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
                    name = "GreenSkillQuad",
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

        public void SetVisible(bool visible)
        {
            if (_root == null || _root.activeSelf == visible)
            {
                return;
            }

            _root.SetActive(visible);
            if (visible)
            {
                // 非アクティブにするとパーティクルが止まるので、もう一度動かす（自分では湧かないので何も出ない）。
                _sparkles.Play();
                _powder.Play();
                _leaves.Play();
                _crosses.Play();
                _smoke.Play();
            }
        }

        public void Tick(float dt)
        {
            TickFlashes(dt);
            TickWaves(dt);
            TickSlashes(dt);
        }

        public void Dispose()
        {
            GreenMintUtil.DestroySafe(_root);
            Art.Dispose();
        }

        // ───────────── 出す ─────────────

        /// <summary>光の粒（加算）を、position から全方向（または direction のまわり spread 度）に飛ばす。</summary>
        public void Sparkles(Vector2 position, int count, float speed, float size, Color color, Vector2 direction = default, float spread = 180f, float lifetime = 0.6f)
        {
            for (var i = 0; i < count; i++)
            {
                var dir = direction.sqrMagnitude > 0.0001f
                    ? GreenMintUtil.Rotate(direction.normalized, UnityEngine.Random.Range(-spread, spread))
                    : GreenMintUtil.RandomDirection();
                Emit(_sparkles, position, dir * speed * UnityEngine.Random.Range(0.4f, 1f), size * UnityEngine.Random.Range(0.75f, 1.2f),
                    lifetime * UnityEngine.Random.Range(0.8f, 1.15f), color, 0f);
            }
        }

        /// <summary>光の粒を1つ、決まった速さで出す（光の柱の中を降る粒など）。</summary>
        public void SparkAt(Vector2 position, Vector2 velocity, float size, float lifetime, Color color)
        {
            Emit(_sparkles, position, velocity, size, lifetime, color, 0f);
        }

        /// <summary>ミントの粉（白い中・緑のふち）をパッと広げる。</summary>
        public void Powder(Vector2 position, int count, float size, float speed)
        {
            for (var i = 0; i < count; i++)
            {
                var dir = GreenMintUtil.RandomDirection();
                Emit(_powder, position, dir * speed * UnityEngine.Random.Range(0.4f, 1f), size * UnityEngine.Random.Range(0.75f, 1.2f),
                    UnityEngine.Random.Range(0.36f, 0.52f), Color.white, 0f);
            }
        }

        /// <summary>ミントの葉っぱをひらひら舞わせる（外へ流れて、くるくる回りながら消える）。</summary>
        public void Leaves(Vector2 position, int count, float size, float speed)
        {
            for (var i = 0; i < count; i++)
            {
                var dir = GreenMintUtil.RandomDirection();
                Emit(_leaves, position + dir * 0.2f, dir * speed * UnityEngine.Random.Range(0.4f, 1f), size * UnityEngine.Random.Range(0.75f, 1.2f),
                    UnityEngine.Random.Range(1.1f, 1.6f), Color.white, UnityEngine.Random.Range(0f, 360f));
            }
        }

        /// <summary>回復の十字（＋）を、画面の上へ浮かばせる。</summary>
        public void Crosses(Vector2 position, int count, float size, float spreadRadius, Vector2 screenUp, float rise = 2f)
        {
            for (var i = 0; i < count; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * spreadRadius;
                var velocity = screenUp * rise * UnityEngine.Random.Range(0.8f, 1.6f) + UnityEngine.Random.insideUnitCircle * (count > 1 ? 2.5f : 0.3f);
                Emit(_crosses, position + offset, velocity, size * UnityEngine.Random.Range(0.75f, 1.2f),
                    UnityEngine.Random.Range(0.9f, 1.3f), Color.white, 0f);
            }
        }

        /// <summary>煙の粒（うすい黄緑の丸。ゆらゆら流れながら大きくなって消える）。</summary>
        public void Smoke(Vector2 position, int count, float size, float speed, Vector2 drift, float lifetime)
        {
            for (var i = 0; i < count; i++)
            {
                var dir = GreenMintUtil.RandomDirection();
                Emit(_smoke, position, dir * speed * UnityEngine.Random.Range(0.4f, 1f) + drift, size * 2f * UnityEngine.Random.Range(0.75f, 1.2f),
                    lifetime * UnityEngine.Random.Range(0.8f, 1.15f), Color.white, UnityEngine.Random.Range(0f, 360f));
            }
        }

        /// <summary>光の板をパッと光らせて消す（芯・トゲ・輪は Radial シェーダーの値）。</summary>
        public void FlashAt(Vector2 position, float size, float seconds, Color color, int rays = 0, float ring = 0f, float z = ZOffset)
        {
            var slot = Oldest(_flashes);
            slot.Active = true;
            slot.Elapsed = 0f;
            slot.Duration = Mathf.Max(0.02f, seconds);
            slot.Size = size;
            slot.Color = color;
            slot.Rays = rays;
            slot.Ring = ring;
            slot.Rotation = UnityEngine.Random.Range(0f, 6.283f);
            slot.Position = new Vector3(position.x, position.y, z);
        }

        /// <summary>広がる衝撃波の輪（白い輪に色を付ける）。delay 秒あとに出す。</summary>
        public void WaveAt(Vector2 position, float fromSize, float toSize, float seconds, Color color, float delay = 0f)
        {
            var slot = Oldest(_waves);
            slot.Active = true;
            slot.Elapsed = -Mathf.Max(0f, delay);
            slot.Duration = Mathf.Max(0.02f, seconds);
            slot.From = fromSize;
            slot.To = toSize;
            slot.Color = color;
            slot.Position = new Vector3(position.x, position.y, ZOffset + 0.02f);
        }

        /// <summary>当たったときの白い線（弧）。rotationDegrees は線の向き（地面の上で）。</summary>
        public void SlashAt(Vector2 position, float rotationDegrees, float length, float seconds = 0.22f)
        {
            if (length <= 0f)
            {
                return;
            }

            var slot = Oldest(_slashes);
            slot.Active = true;
            slot.Elapsed = 0f;
            slot.Duration = Mathf.Max(0.02f, seconds);
            slot.Size = length;
            slot.Rotation = rotationDegrees;
            slot.Position = new Vector3(position.x, position.y, ZOffset - 0.07f);
        }

        // ───────────── 作る ─────────────

        public MeshRenderer CreateQuad(string objectName, Material material, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent != null ? parent : _root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = SharedQuad;
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

        /// <summary>絵を1枚出す板。material を省くと、敵より上に描く普通の重ね塗り。</summary>
        public SpriteRenderer CreateSprite(string objectName, Sprite sprite, int sortingOrder, Material material = null, Transform parent = null)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent != null ? parent : _root.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = material != null ? material : _overSpriteMaterial;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
        }

        /// <summary>絵のシェーダー（MS2026/GreenSkill/Sprite）のマテリアル。texture は SpriteRenderer なら自動で入るので空でよい。</summary>
        public Material CreateSpriteMaterial(Texture texture, int renderQueue)
        {
            var material = CreateMaterial(Definition.shaders.sprite, SpriteShaderName, renderQueue);
            if (texture != null)
            {
                material.SetTexture(MainTexId, texture);
                material.mainTexture = texture;
            }

            return material;
        }

        /// <summary>
        /// Radial シェーダーの光の板に値を渡す。
        /// shapeA: x=芯の大きさ, y=芯のしぼり, z=トゲの本数, w=トゲの強さ / shapeB: x=トゲの細さ, y=回転, z=輪の半径, w=輪の太さ / shapeC: x=輪の強さ, z=乱数の種, w=トゲの長さのばらつき
        /// </summary>
        public void ApplyRadial(Renderer renderer, Color color, Vector4 shapeA, Vector4 shapeB, Vector4 shapeC)
        {
            _block.Clear();
            _block.SetColor(ColorId, color);
            _block.SetVector(ShapeAId, shapeA);
            _block.SetVector(ShapeBId, shapeB);
            _block.SetVector(ShapeCId, shapeC);
            renderer.SetPropertyBlock(_block);
        }

        /// <summary>やわらかい丸い光（地面の光・葉の光）。</summary>
        public void ApplySoftGlow(Renderer renderer, Color color)
        {
            ApplyRadial(renderer, color, new Vector4(0.9f, 1.6f, 0f, 0f), new Vector4(4f, 0f, 0f, 0f), Vector4.zero);
        }

        public static Material CreateMaterial(Shader shader, string fallbackName, int renderQueue)
        {
            if (shader == null)
            {
                shader = Shader.Find(fallbackName);
            }

            if (shader == null)
            {
                Debug.LogWarning($"[GreenSkill] シェーダー {fallbackName} が見つかりません。スキルの設定ファイルの「シェーダー」欄を確認してください。");
                shader = Shader.Find("Sprites/Default");
            }

            return new Material(shader) { hideFlags = HideFlags.HideAndDontSave, renderQueue = renderQueue };
        }

        private ParticleSystem CreateParticles(string objectName, Material material, int sortingOrder, int maxParticles, float drag, AnimationCurve sizeCurve)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(_root.transform, false);
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
            main.gravityModifier = 0f;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var colorOverLifetime = system.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.75f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = fade;

            var sizeOverLifetime = system.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

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
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            system.Play();
            return system;
        }

        private static void Emit(ParticleSystem system, Vector2 position, Vector2 velocity, float size, float lifetime, Color color, float rotationDegrees)
        {
            var parameters = new ParticleSystem.EmitParams
            {
                position = new Vector3(position.x, position.y, ZOffset - 0.04f),
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                rotation = rotationDegrees
            };
            system.Emit(parameters, 1);
        }

        // ───────────── 光・波・線 ─────────────

        private abstract class Pooled
        {
            public bool Active;
            public float Elapsed;
            public float Duration;
        }

        private sealed class Flash : Pooled
        {
            public MeshRenderer Renderer;
            public float Size;
            public Color Color;
            public int Rays;
            public float Ring;
            public float Rotation;
            public Vector3 Position;
        }

        private sealed class Wave : Pooled
        {
            public SpriteRenderer Renderer;
            public float From;
            public float To;
            public Color Color;
            public Vector3 Position;
        }

        private sealed class Slash : Pooled
        {
            public SpriteRenderer Renderer;
            public float Size;
            public float Rotation;
            public Vector3 Position;
        }

        private static T Oldest<T>(T[] pool) where T : Pooled
        {
            var slot = pool[0];
            for (var i = 0; i < pool.Length; i++)
            {
                if (!pool[i].Active)
                {
                    return pool[i];
                }

                if (pool[i].Elapsed / pool[i].Duration > slot.Elapsed / slot.Duration)
                {
                    slot = pool[i];
                }
            }

            return slot;
        }

        private void TickFlashes(float dt)
        {
            for (var i = 0; i < _flashes.Length; i++)
            {
                var flash = _flashes[i];
                if (!flash.Active)
                {
                    flash.Renderer.enabled = false;
                    continue;
                }

                flash.Elapsed += dt;
                var t = flash.Elapsed / flash.Duration;
                if (t >= 1f)
                {
                    flash.Active = false;
                    flash.Renderer.enabled = false;
                    continue;
                }

                // ぱっと大きくなって、ゆっくり消える。
                var grow = Mathf.Lerp(0.55f, 1f, GreenMintUtil.EaseOutCubic(t * 2.5f));
                var fade = (1f - t) * (1f - t);
                var size = flash.Size * grow;
                var transform = flash.Renderer.transform;
                transform.SetPositionAndRotation(flash.Position, Quaternion.identity);
                transform.localScale = new Vector3(size, size, 1f);
                flash.Renderer.enabled = size > 0.001f;
                ApplyRadial(flash.Renderer, flash.Color * fade,
                    new Vector4(0.42f, 2.2f, flash.Rays, flash.Rays > 0 ? 0.8f : 0f),
                    new Vector4(5f, flash.Rotation + t * 0.6f, 0.78f, 0.08f),
                    new Vector4(flash.Ring, 0f, flash.Rotation, 0.5f));
            }
        }

        private void TickWaves(float dt)
        {
            for (var i = 0; i < _waves.Length; i++)
            {
                var wave = _waves[i];
                if (!wave.Active)
                {
                    wave.Renderer.enabled = false;
                    continue;
                }

                wave.Elapsed += dt;
                if (wave.Elapsed < 0f)
                {
                    wave.Renderer.enabled = false;
                    continue;
                }

                var t = wave.Elapsed / wave.Duration;
                if (t >= 1f)
                {
                    wave.Active = false;
                    wave.Renderer.enabled = false;
                    continue;
                }

                var size = Mathf.Lerp(wave.From, wave.To, GreenMintUtil.EaseOutCubic(t));
                var color = wave.Color;
                color.a *= 1f - t;
                var transform = wave.Renderer.transform;
                transform.SetPositionAndRotation(wave.Position, Quaternion.identity);
                transform.localScale = new Vector3(size, size, 1f);
                wave.Renderer.color = color;
                wave.Renderer.enabled = true;
            }
        }

        private void TickSlashes(float dt)
        {
            for (var i = 0; i < _slashes.Length; i++)
            {
                var slash = _slashes[i];
                if (!slash.Active)
                {
                    slash.Renderer.enabled = false;
                    continue;
                }

                slash.Elapsed += dt;
                var t = slash.Elapsed / slash.Duration;
                if (t >= 1f)
                {
                    slash.Active = false;
                    slash.Renderer.enabled = false;
                    continue;
                }

                // すっと伸びて消える。
                var length = slash.Size * (0.6f + 0.6f * GreenMintUtil.EaseOutCubic(t));
                var transform = slash.Renderer.transform;
                transform.SetPositionAndRotation(slash.Position, Quaternion.Euler(0f, 0f, slash.Rotation));
                transform.localScale = new Vector3(length, length * Mathf.Lerp(1f, 0.4f, t), 1f);
                slash.Renderer.color = new Color(0.92f, 1f, 0.86f, 1f - t);
                slash.Renderer.enabled = true;
            }
        }
    }
}
