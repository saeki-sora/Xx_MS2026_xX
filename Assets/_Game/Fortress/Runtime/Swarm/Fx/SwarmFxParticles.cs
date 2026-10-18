using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆の演出用の光の粒（倒した敵の粒・燃え尽きる光・爆弾の閃光）。数千個をCPUで動かし、1回の描画でまとめて描く
    /// （Resources/Fortress/SwarmFxParticle.shader）。見た目だけで、各PCが自分で動かす。
    /// </summary>
    internal sealed class SwarmFxParticles : IDisposable
    {
        public const int KindSpark = 0;
        public const int KindFlash = 1;

        private const string ShaderResourcePath = "Fortress/SwarmFxParticle";
        private const float AbsorbDistance = 0.35f;

        private static readonly int ParticlesId = Shader.PropertyToID("_FxParticles");

        [StructLayout(LayoutKind.Sequential)]
        private struct GpuParticle
        {
            public Vector4 a;
            public Vector4 b;
            public Vector4 c;
        }

        private struct Particle
        {
            public Vector2 position;
            public Vector2 velocity;
            public float age;
            public float life;
            public float size;
            public Color color;
            public int kind;
            public int owner;
        }

        private readonly Particle[] _particles;
        private readonly GpuParticle[] _gpu;
        private readonly GraphicsBuffer _buffer;
        private readonly Material _material;
        private readonly Mesh _quad;
        private int _count;

        public int Capacity => _particles.Length;
        public int Count => _count;

        public SwarmFxParticles(int capacity)
        {
            capacity = Mathf.Max(16, capacity);
            _particles = new Particle[capacity];
            _gpu = new GpuParticle[capacity];
            _buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, Marshal.SizeOf<GpuParticle>());
            var shader = Resources.Load<Shader>(ShaderResourcePath);
            if (shader != null)
            {
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            _quad = BuildQuad();
        }

        public bool AddSpark(Vector2 position, Vector2 velocity, float size, Color color, int owner, float life)
        {
            return Add(new Particle
            {
                position = position, velocity = velocity, size = size, color = color, kind = KindSpark, owner = owner, life = life
            });
        }

        public bool AddFlash(Vector2 position, float size, Color color, float life)
        {
            return Add(new Particle { position = position, size = size, color = color, kind = KindFlash, owner = -1, life = life });
        }

        private bool Add(Particle particle)
        {
            if (_count >= _particles.Length || particle.life <= 0f)
            {
                return false;
            }

            _particles[_count++] = particle;
            return true;
        }

        /// <param name="targets">プレイヤー番号ごとの、粒が吸い込まれる位置（砲台）。無ければ null。</param>
        public void Update(float deltaTime, Vector2?[] targets, float homingDelay, float homingSpeed)
        {
            for (var i = _count - 1; i >= 0; i--)
            {
                ref var p = ref _particles[i];
                p.age += deltaTime;
                var dead = p.age >= p.life;
                if (!dead && p.kind == KindSpark)
                {
                    Vector2? target = p.owner >= 0 && p.owner < targets.Length ? targets[p.owner] : null;
                    p.velocity = SwarmFxMath.StepSparkVelocity(p.velocity, p.position, target, p.age, homingDelay, homingSpeed, deltaTime);
                    p.position += p.velocity * deltaTime;
                    if (target.HasValue && p.age >= homingDelay && (target.Value - p.position).sqrMagnitude < AbsorbDistance * AbsorbDistance)
                    {
                        dead = true;
                    }
                }

                if (dead)
                {
                    _particles[i] = _particles[--_count];
                }
            }
        }

        public void Render()
        {
            if (_count == 0 || _material == null)
            {
                return;
            }

            for (var i = 0; i < _count; i++)
            {
                var p = _particles[i];
                float size;
                float alpha;
                if (p.kind == KindFlash)
                {
                    var t = p.age / p.life;
                    size = p.size * SwarmFxMath.FlashScale(t);
                    alpha = SwarmFxMath.FlashAlpha(t);
                }
                else
                {
                    size = p.size;
                    alpha = SwarmFxMath.SparkAlpha(p.age, p.life);
                }

                _gpu[i] = new GpuParticle
                {
                    a = new Vector4(p.position.x, p.position.y, size, alpha),
                    b = new Vector4(p.color.r, p.color.g, p.color.b, p.kind),
                    c = new Vector4(p.velocity.x, p.velocity.y, 0f, 0f)
                };
            }

            _buffer.SetData(_gpu, 0, 0, _count);
            _material.SetBuffer(ParticlesId, _buffer);
            var renderParams = new RenderParams(_material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000f),
                layer = 0,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false
            };
            Graphics.RenderMeshPrimitives(renderParams, _quad, 0, _count);
        }

        public void Clear()
        {
            _count = 0;
        }

        public void Dispose()
        {
            _buffer?.Release();
            if (_material != null)
            {
                UnityEngine.Object.Destroy(_material);
            }

            if (_quad != null)
            {
                UnityEngine.Object.Destroy(_quad);
            }
        }

        private static Mesh BuildQuad()
        {
            var mesh = new Mesh
            {
                name = "Fortress_SwarmFxQuad",
                hideFlags = HideFlags.HideAndDontSave,
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                    new Vector3(-0.5f, 0.5f, 0f), new Vector3(0.5f, 0.5f, 0f)
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 }
            };
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
            return mesh;
        }
    }
}
