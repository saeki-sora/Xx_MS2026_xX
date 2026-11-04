using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵を倒した所に出す、アニメ調の破裂（見た目だけ）。設定は <see cref="Settings"/>（ステージの見た目の設定 StageLookProfile.killBurst を、
    /// ステージが開いたときに入れる）。群衆の「倒された」知らせ（<see cref="SwarmSystem.AgentsDied"/>。Client では Host から届いた分）を聞いて、
    /// Play 中だけ、シーンに保存しない係のオブジェクトが床の少し上に描く。今までの撃破の光・火花には触らない。通信しない。
    /// </summary>
    public static class KillBurstLook
    {
        private static KillBurstSettings _settings;

        /// <summary>今の破裂の設定。null なら出さない（ステージが無いシーン）。</summary>
        public static KillBurstSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                if (value != null && Application.isPlaying)
                {
                    KillBurstRunner.Ensure();
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _settings = null;
        }
    }

    /// <summary><see cref="KillBurstLook"/> の破裂を動かして描く係（Play 中だけ、シーンに保存しない隠しオブジェクト）。</summary>
    [AddComponentMenu("")]
    internal sealed class KillBurstRunner : MonoBehaviour
    {
        private const string ShaderName = "Hidden/MS2026/Fortress/KillBurst";
        private const float KindStar = 0f;
        private const float KindDrop = 1f;
        private const float KindRing = 2f;

        private struct Burst
        {
            public Vector2 Position;
            public Color Color;
            public float Angle;
            public float Age;
            public float Seed;
        }

        private static KillBurstRunner _instance;

        private readonly List<Burst> _bursts = new List<Burst>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly List<Vector2> _uv1 = new List<Vector2>();
        private readonly List<int> _indices = new List<int>();

        private SwarmSystem _subscribed;
        private int _spawnedThisFrame;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _material;
        private bool _missingLogged;

        public static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            // DontSave は付けない（付けると Play を止めても消えずに残り、止まった破裂を描き続けてしまう）。
            var go = new GameObject("[Look] Kill Bursts");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<KillBurstRunner>();
        }

        private void OnDestroy()
        {
            Subscribe(null);
            if (_instance == this)
            {
                _instance = null;
            }

            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        private void Update()
        {
            // 群衆のシステムが入れ替わったら（試合のやり直し・ステージの切り替え）、聞き直す。
            Subscribe(KillBurstLook.Settings != null ? SwarmSystem.Current : null);
        }

        private void Subscribe(SwarmSystem system)
        {
            if (_subscribed == system)
            {
                return;
            }

            if (_subscribed != null)
            {
                _subscribed.AgentsDied -= HandleDeaths;
            }

            _subscribed = system;
            if (_subscribed != null)
            {
                _subscribed.AgentsDied += HandleDeaths;
            }
        }

        private void HandleDeaths(IReadOnlyList<SwarmDeath> deaths)
        {
            var s = KillBurstLook.Settings;
            if (s == null || !s.enabled)
            {
                return;
            }

            for (var i = 0; i < deaths.Count; i++)
            {
                if (_spawnedThisFrame >= s.maxNewPerFrame)
                {
                    return;
                }

                if (_bursts.Count >= s.maxActive)
                {
                    _bursts.RemoveAt(0);
                }

                var death = deaths[i];
                var color = death.owner >= 0 ? FortressColors.PlayerColor(death.owner) : new Color(1f, 0.92f, 0.8f, 1f);
                _bursts.Add(new Burst
                {
                    Position = new Vector2(death.position.x, death.position.y),
                    Color = color,
                    Angle = Random.value * Mathf.PI * 2f,
                    Age = 0f,
                    Seed = Random.value * 100f
                });
                _spawnedThisFrame++;
            }
        }

        private void LateUpdate()
        {
            var s = KillBurstLook.Settings;
            var active = s != null && s.enabled && EnsureResources();
            if (_renderer != null)
            {
                _renderer.enabled = active && _bursts.Count > 0;
            }

            _spawnedThisFrame = 0;
            if (!active)
            {
                _bursts.Clear();
                return;
            }

            var dt = Time.deltaTime;
            for (var i = _bursts.Count - 1; i >= 0; i--)
            {
                var b = _bursts[i];
                b.Age += dt;
                if (b.Age >= s.lifeSeconds)
                {
                    _bursts.RemoveAt(i);
                    continue;
                }

                _bursts[i] = b;
            }

            BuildMesh(s);
        }

        private bool EnsureResources()
        {
            if (_material != null)
            {
                return true;
            }

            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                if (!_missingLogged)
                {
                    _missingLogged = true;
                    Debug.LogWarning($"[KillBurst] シェーダー '{ShaderName}' が見つからないので、撃破の破裂は出しません。");
                }

                return false;
            }

            _material = new Material(shader) { name = "KillBursts", hideFlags = HideFlags.DontSave };
            _mesh = new Mesh { name = "KillBursts", hideFlags = HideFlags.DontSave };
            _mesh.MarkDynamic();
            // 位置はワールドの位置のまま入れるので、この係のオブジェクトは原点から動かさない。
            transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            transform.localScale = Vector3.one;
            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return true;
        }

        private void BuildMesh(KillBurstSettings s)
        {
            _vertices.Clear();
            _colors.Clear();
            _uv0.Clear();
            _uv1.Clear();
            _indices.Clear();

            var size = s.size;
            var z = -s.lift;
            var fps = s.framesPerSecond;
            var frame = fps > 0f ? 1f / fps : 0f;
            foreach (var b in _bursts)
            {
                var age = KillBurstMath.Quantize(b.Age, fps);
                var k = Mathf.Clamp01(age / s.lifeSeconds);
                var tint = b.Color * s.colorBrightness;
                tint.a = 1f;

                // ① 白い星（最初の2コマだけ。芯の白飛び）。
                if (age < KillBurstMath.StarFrames * Mathf.Max(frame, 1f / 60f))
                {
                    var starSize = size * 0.6f * (1f - age / Mathf.Max(s.lifeSeconds, 0.01f));
                    var white = new Color(s.starBrightness, s.starBrightness, s.starBrightness, 1f);
                    AddQuad(b.Position, z - 0.01f, b.Angle * 0.25f, starSize, starSize, white, KindStar, 0f);
                }

                // ② 倒した人の色のしずくが飛び散る（ベタ塗り＋前半だけ白いつや）。
                var count = Mathf.Max(0, s.dropCount);
                for (var i = 0; i < count; i++)
                {
                    var jitter = Mathf.Sin(b.Seed + i * 12.9898f) * 0.35f;
                    var angle = b.Angle + i * Mathf.PI * 2f / Mathf.Max(1, count) + jitter;
                    var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    var distance = size * KillBurstMath.DropDistance(k) * (0.8f + 0.25f * Mathf.Cos(b.Seed * 3f + i));
                    var length = size * (0.6f * (1f - k) + 0.1f);
                    AddQuad(b.Position + dir * distance, z, angle, length, length * 0.6f, tint, KindDrop, k < 0.5f ? 1f : 0f);
                }

                // ③ 色の輪（2コマ目〜5コマ目）。
                if (frame > 0f && age >= frame && age < frame * 5f)
                {
                    var radius = size * (0.35f + 1.1f * k);
                    AddQuad(b.Position, z + 0.01f, 0f, radius * 1.15f, radius * 1.15f, tint, KindRing, 0.1f * (1f - k) + 0.02f);
                }
            }

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uv0);
            _mesh.SetUVs(1, _uv1);
            _mesh.SetTriangles(_indices, 0, true);
        }

        // 床と平行な四角形（中心・向き・半分の長さ・半分の幅）。uv0 は -1〜1、uv1 は (形の種類, 形ごとの値)。
        private void AddQuad(Vector2 centre, float z, float angle, float halfLength, float halfWidth, Color color, float kind, float param)
        {
            var axis = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * halfLength;
            var side = new Vector2(-Mathf.Sin(angle), Mathf.Cos(angle)) * halfWidth;
            var start = _vertices.Count;
            _vertices.Add(ToWorld(centre - axis - side, z));
            _vertices.Add(ToWorld(centre + axis - side, z));
            _vertices.Add(ToWorld(centre + axis + side, z));
            _vertices.Add(ToWorld(centre - axis + side, z));
            _uv0.Add(new Vector2(-1f, -1f));
            _uv0.Add(new Vector2(1f, -1f));
            _uv0.Add(new Vector2(1f, 1f));
            _uv0.Add(new Vector2(-1f, 1f));
            var extra = new Vector2(kind, param);
            for (var i = 0; i < 4; i++)
            {
                _colors.Add(color);
                _uv1.Add(extra);
            }

            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
            _indices.Add(start);
            _indices.Add(start + 2);
            _indices.Add(start + 3);
        }

        private static Vector3 ToWorld(Vector2 p, float z) => new Vector3(p.x, p.y, z);
    }
}
