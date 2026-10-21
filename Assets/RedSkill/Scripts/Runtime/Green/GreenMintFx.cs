using System;
using MS2026.Fortress.Billboards;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Skills.Green
{
    /// <summary>
    /// 緑スキルの演出の道具箱（1人ぶん）。キラキラ・粉・葉っぱ・回復の十字（パーティクル）、光（Radial の板）、広がる波の輪を、
    /// どこからでも「ここに出して」と呼べるようにまとめる。見た目はすべてシーンの一番上の `[GreenSkill] Pn` の下に作る（保存されない）。
    /// 粉・葉っぱ・十字は明るい床の上でも読めるよう、光を足す（加算）のではなく普通に重ねて描く。キラキラと光だけ加算。
    /// </summary>
    internal sealed class GreenMintFx : IDisposable
    {
        public const float ZOffset = -0.08f;

        private const string RadialShaderName = "MS2026/RedSkill/Radial";
        private const string ParticleShaderName = "MS2026/RedSkill/Particle";
        private const string SpriteShaderName = "MS2026/GreenSkill/Sprite";
        private const int GlowPoolSize = 24;
        private const int WavePoolSize = 10;

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
        private readonly Flash[] _flashes = new Flash[GlowPoolSize];
        private readonly Wave[] _waves = new Wave[WavePoolSize];

        public GreenMintFx(GreenMintSkill definition, int playerIndex)
        {
            Definition = definition;
            Art = new GreenMintArt(definition.palette);
            _root = new GameObject("[GreenSkill] P" + (playerIndex + 1)) { hideFlags = HideFlags.DontSave };
            // 地面に寝かせて描くので、自動のビルボード（立たせる処理）の対象から外す。
            _root.AddComponent<BillboardIgnore>();

            RadialMaterial = CreateMaterial(definition.shaders.radial, RadialShaderName, 3100);
            SparkMaterial = CreateMaterial(definition.shaders.particle, ParticleShaderName, 3110);
            SparkMaterial.SetFloat(IntensityId, 2.2f);
            SparkMaterial.SetFloat(SoftnessId, 1.4f);
            TrailMaterial = CreateMaterial(definition.shaders.particle, ParticleShaderName, 3105);
            TrailMaterial.SetFloat(LineModeId, 1f);
            TrailMaterial.SetFloat(SoftnessId, 1.2f);
            TrailMaterial.SetFloat(IntensityId, 1.6f);
            var powderMaterial = CreateSpriteMaterial(definition.shaders.sprite, Art.SoftDot);
            var leafMaterial = CreateSpriteMaterial(definition.shaders.sprite, Art.LeafTexture);
            var crossMaterial = CreateSpriteMaterial(definition.shaders.sprite, Art.Cross);

            var order = definition.satellites.sortingOrder;
            _powder = CreateParticles("Powder", powderMaterial, order + 4, 600, 2.4f,
                new AnimationCurve(new Keyframe(0f, 0.55f), new Keyframe(0.25f, 1f), new Keyframe(1f, 1.35f)));
            _leaves = CreateParticles("Leaves", leafMaterial, order + 5, 200, 1.6f,
                new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.85f)));
            var leafRotation = _leaves.rotationOverLifetime;
            leafRotation.enabled = true;
            leafRotation.z = new ParticleSystem.MinMaxCurve(-3f, 3f);
            _crosses = CreateParticles("Crosses", crossMaterial, order + 6, 120, 0.8f,
                new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.12f, 1.25f), new Keyframe(0.3f, 1f), new Keyframe(1f, 0.7f)));
            _sparkles = CreateParticles("Sparkles", SparkMaterial, order + 7, 1200, 1.8f,
                new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.15f)));

            for (var i = 0; i < _flashes.Length; i++)
            {
                _flashes[i] = new Flash { Renderer = CreateQuad("Flash" + i, RadialMaterial, order + 3) };
            }

            for (var i = 0; i < _waves.Length; i++)
            {
                var renderer = CreateSprite("Wave" + i, Art.WaveRing, order - 2);
                _waves[i] = new Wave { Renderer = renderer };
            }
        }

        public GreenMintSkill Definition { get; }

        public GreenMintArt Art { get; }

        public Transform Root => _root.transform;

        /// <summary>光の板（加算）。</summary>
        public Material RadialMaterial { get; }

        /// <summary>光の粒（加算）。</summary>
        public Material SparkMaterial { get; }

        /// <summary>光の尾（加算、線）。</summary>
        public Material TrailMaterial { get; }

        public MaterialPropertyBlock Block => _block;

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
            }
        }

        public void Tick(float dt)
        {
            TickFlashes(dt);
            TickWaves(dt);
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
                Emit(_sparkles, position, dir * speed * UnityEngine.Random.Range(0.35f, 1.1f), size * UnityEngine.Random.Range(0.6f, 1.25f),
                    lifetime * UnityEngine.Random.Range(0.6f, 1.2f), color, 0f);
            }
        }

        /// <summary>ミントの粉（白〜ミント色のやわらかい粒）をパッと広げる。</summary>
        public void Powder(Vector2 position, int count, float size, float speed)
        {
            var palette = Definition.palette;
            for (var i = 0; i < count; i++)
            {
                var color = Color.Lerp(palette.tablet, palette.mint, UnityEngine.Random.Range(0f, 0.8f));
                color.a = UnityEngine.Random.Range(0.75f, 0.95f);
                var dir = GreenMintUtil.RandomDirection();
                var offset = dir * size * UnityEngine.Random.Range(0f, 0.3f);
                Emit(_powder, position + offset, dir * speed * UnityEngine.Random.Range(0.3f, 1f), size * UnityEngine.Random.Range(0.6f, 1.2f),
                    UnityEngine.Random.Range(0.35f, 0.6f), color, 0f);
            }
        }

        /// <summary>ミントの葉っぱをひらひら舞わせる（外へ流れて、くるくる回りながら消える）。</summary>
        public void Leaves(Vector2 position, int count, float size, float speed)
        {
            var palette = Definition.palette;
            for (var i = 0; i < count; i++)
            {
                var dir = GreenMintUtil.RandomDirection();
                var color = Color.Lerp(Color.white, palette.mint, UnityEngine.Random.Range(0f, 0.25f));
                Emit(_leaves, position + dir * 0.2f, dir * speed * UnityEngine.Random.Range(0.4f, 1f), size * UnityEngine.Random.Range(0.75f, 1.2f),
                    UnityEngine.Random.Range(0.8f, 1.3f), color, UnityEngine.Random.Range(0f, 360f));
            }
        }

        /// <summary>回復の十字（＋）を、画面の上へ浮かばせる。</summary>
        public void Crosses(Vector2 position, int count, float size, float spreadRadius, Vector2 screenUp)
        {
            var palette = Definition.palette;
            for (var i = 0; i < count; i++)
            {
                var offset = UnityEngine.Random.insideUnitCircle * spreadRadius;
                var color = Color.Lerp(palette.green, palette.mint, UnityEngine.Random.Range(0.3f, 0.9f));
                var velocity = screenUp * UnityEngine.Random.Range(1.6f, 3.2f) + UnityEngine.Random.insideUnitCircle * 0.5f;
                Emit(_crosses, position + offset, velocity, size * UnityEngine.Random.Range(0.7f, 1.2f),
                    UnityEngine.Random.Range(0.8f, 1.2f), color, 0f);
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

        /// <summary>広がる波の輪（普通に重ねる白い輪に色を付ける）。</summary>
        public void WaveAt(Vector2 position, float fromSize, float toSize, float seconds, Color color, float z = ZOffset + 0.02f)
        {
            var slot = Oldest(_waves);
            slot.Active = true;
            slot.Elapsed = 0f;
            slot.Duration = Mathf.Max(0.02f, seconds);
            slot.From = fromSize;
            slot.To = toSize;
            slot.Color = color;
            slot.Position = new Vector3(position.x, position.y, z);
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

        public SpriteRenderer CreateSprite(string objectName, Sprite sprite, int sortingOrder, Transform parent = null)
        {
            var go = new GameObject(objectName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent != null ? parent : _root.transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            return renderer;
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

        private static Material CreateSpriteMaterial(Shader shader, Texture texture)
        {
            var material = CreateMaterial(shader, SpriteShaderName, 3108);
            material.SetTexture(MainTexId, texture);
            material.mainTexture = texture;
            return material;
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
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.55f), new GradientAlphaKey(0f, 1f) });
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

        // ───────────── 光と波 ─────────────

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
                var t = wave.Elapsed / wave.Duration;
                if (t >= 1f)
                {
                    wave.Active = false;
                    wave.Renderer.enabled = false;
                    continue;
                }

                var size = Mathf.Lerp(wave.From, wave.To, GreenMintUtil.EaseOutCubic(t));
                var color = wave.Color;
                color.a *= 1f - t * t;
                var transform = wave.Renderer.transform;
                transform.SetPositionAndRotation(wave.Position, Quaternion.identity);
                transform.localScale = new Vector3(size, size, 1f);
                wave.Renderer.color = color;
                wave.Renderer.enabled = true;
            }
        }
    }
}
