using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵を倒したときの「光る泡」（見た目と効果音だけ）。設定は <see cref="Settings"/>（ステージの見た目の設定 StageLookProfile.foamPop を、
    /// ステージが開いたときに入れる）。群衆の「倒された」知らせ（<see cref="SwarmSystem.AgentsDied"/>。Client では Host から届いた分）を聞いて、
    /// Play 中だけ、係のオブジェクトが描く。通信しない（各PCで描いて鳴らす）。
    /// </summary>
    public static class FoamPopLook
    {
        private static FoamPopSettings _settings;

        /// <summary>今の光る泡の設定。null なら出さない（ステージが無いシーン）。</summary>
        public static FoamPopSettings Settings
        {
            get => _settings;
            set
            {
                _settings = value;
                if (value != null && Application.isPlaying)
                {
                    FoamPopRunner.Ensure();
                }
            }
        }

        /// <summary>試しに、指定の位置で倒されたことにして光る泡を出す（エディタの「試す」ボタン用。Play 中だけ）。</summary>
        public static void Preview(Vector2 position, int owner, int count)
        {
            if (_settings == null || !Application.isPlaying)
            {
                return;
            }

            FoamPopRunner.Ensure();
            FoamPopRunner.Instance?.SpawnPreview(position, owner, count);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _settings = null;
        }
    }

    /// <summary><see cref="FoamPopLook"/> の泡を動かして描き、効果音を鳴らす係（Play 中だけの隠しオブジェクト）。</summary>
    [AddComponentMenu("")]
    internal sealed class FoamPopRunner : MonoBehaviour
    {
        private const string ShaderName = "Hidden/MS2026/Fortress/FoamPop";
        private const float KindWrap = 0f;
        private const float KindBubble = 1f;
        private const float KindDot = 2f;

        // 泡の四角形は、まわりの光の分だけ泡より大きくする（シェーダーと同じ値）。
        private const float BubbleQuadScale = 1.25f;
        private const float DefaultSpriteSize = 0.8f;
        private const float RimMeters = 0.034f;

        private static readonly int BubbleFillId = Shader.PropertyToID("_BubbleFill");
        private static readonly int MintColorId = Shader.PropertyToID("_MintColor");
        private static readonly int PlayerMixId = Shader.PropertyToID("_PlayerMix");

        private struct Part
        {
            public float Kind;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rise;
            public float RiseSpeed;
            public float Age;
            public float End;
            public float Life;
            public float Size;
            public float Seed;
            public Color Rim;
            public Color Glow;
        }

        public static FoamPopRunner Instance { get; private set; }

        private readonly List<Part> _parts = new List<Part>();
        private readonly Dictionary<long, Vector2> _crowd = new Dictionary<long, Vector2>();
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<Color> _colors = new List<Color>();
        private readonly List<Vector2> _uv0 = new List<Vector2>();
        private readonly List<Vector2> _uv1 = new List<Vector2>();
        private readonly List<Vector2> _uv2 = new List<Vector2>();
        private readonly List<Vector4> _uv3 = new List<Vector4>();
        private readonly List<int> _indices = new List<int>();

        private SwarmSystem _subscribed;
        private int _spawnedThisFrame;
        private int _pendingKills;
        private float _pendingTimer;
        private Mesh _mesh;
        private MeshRenderer _renderer;
        private Material _material;
        private bool _missingLogged;

        public static void Ensure()
        {
            if (Instance != null)
            {
                return;
            }

            // DontSave は付けない（付けると Play を止めても消えずに残り、止まった泡を描き続けてしまう）。
            var go = new GameObject("[Look] Foam Pops");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<FoamPopRunner>();
        }

        private void OnDestroy()
        {
            Subscribe(null);
            if (Instance == this)
            {
                Instance = null;
            }

            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
        }

        private void Update()
        {
            // 群衆のシステムが入れ替わったら（試合のやり直し・ステージの切り替え）、聞き直す。
            Subscribe(FoamPopLook.Settings != null ? SwarmSystem.Current : null);
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
            var s = FoamPopLook.Settings;
            if (s == null || !s.enabled || deaths.Count == 0)
            {
                return;
            }

            QueueSound(s, deaths.Count);
            for (var i = 0; i < deaths.Count; i++)
            {
                if (_spawnedThisFrame >= s.maxNewPerFrame)
                {
                    return;
                }

                var death = deaths[i];
                var definition = _subscribed != null ? _subscribed.GetRegisteredType(death.type) : null;
                var spriteSize = definition != null && definition.swarm != null ? definition.swarm.spriteSize : DefaultSpriteSize;
                Spawn(s, new Vector2(death.position.x, death.position.y), death.owner, spriteSize);
            }
        }

        public void SpawnPreview(Vector2 position, int owner, int count)
        {
            var s = FoamPopLook.Settings;
            if (s == null || !s.enabled)
            {
                return;
            }

            count = Mathf.Max(1, count);
            QueueSound(s, count);
            for (var i = 0; i < count; i++)
            {
                // たくさんのときは横一列（レーザーでまとめて倒した感じ）に並べる。
                var offset = count == 1 ? Vector2.zero : new Vector2((i - (count - 1) * 0.5f) * 0.45f, Random.Range(-0.15f, 0.15f));
                Spawn(s, position + offset, owner, DefaultSpriteSize);
            }
        }

        private void QueueSound(FoamPopSettings s, int kills)
        {
            if (_pendingKills == 0)
            {
                _pendingTimer = s.soundGroupSeconds;
            }

            _pendingKills += kills;
        }

        private void Spawn(FoamPopSettings s, Vector2 position, int owner, float spriteSize)
        {
            _spawnedThisFrame++;
            var crowd = AddCrowd(position);
            var player = owner >= 0 ? FortressColors.PlayerColor(owner) : new Color(1f, 0.95f, 0.85f, 1f);
            // ふちは「外側＝倒した人の色、内側＝ミント」の2色の帯（混ぜると赤とミントが灰色になるので、混ぜずに並べる）。
            var rim = player;
            rim.a = 1f;
            var glow = Color.Lerp(player, Color.white, s.glowWhiten);
            glow.a = 1f;

            // ① 包む泡のかたまり。
            if (s.wrapSeconds > 0f && _parts.Count < s.maxParts)
            {
                _parts.Add(new Part
                {
                    Kind = KindWrap, Position = position, Rise = spriteSize * 0.2f, Life = s.wrapSeconds,
                    Size = spriteSize * s.wrapSize * 0.5f, Seed = Random.value * 6.2832f, Rim = rim, Glow = glow
                });
            }

            // ② 光る泡（1つめが一番大きく、真ん中に）。
            var count = FoamPopMath.BubbleCount(crowd, s.bubbleCount, s.crowdedBubbleCount, s.crowdStart, s.crowdFull);
            var crowded = crowd >= s.crowdStart;
            var baseAngle = Random.value * Mathf.PI * 2f;
            for (var k = 0; k < count && _parts.Count < s.maxParts; k++)
            {
                var angle = baseAngle + k * Mathf.PI * 2f / Mathf.Max(1, count) + Random.Range(-0.4f, 0.4f);
                var distance = k == 0 ? 0f : s.bubbleSpread * Random.Range(0.45f, 1f);
                var offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var size = k == 0 ? s.bubbleSize * Random.Range(0.9f, 1.1f) : s.bubbleSize * Random.Range(0.45f, 0.8f);
                // 混んでいる所では早めに消して、画面を泡で埋めない。
                var end = s.bubbleSeconds * (crowded ? 0.7f : 1f) + k * s.bubbleStagger + Random.Range(0f, 0.08f);
                _parts.Add(new Part
                {
                    Kind = KindBubble, Position = position + offset, Velocity = Random.insideUnitCircle * 0.12f,
                    Rise = spriteSize * 0.22f + Random.Range(-0.05f, 0.05f), RiseSpeed = s.riseSpeed * Random.Range(0.75f, 1.25f),
                    Age = -(Mathf.Min(s.wrapSeconds, 0.05f)), End = end, Life = end + s.shrinkSeconds, Size = size, Rim = rim, Glow = glow
                });
            }
        }

        // 1m のマスごとに、直前に倒れた数を数える（時間とともに減る）。まわり 3×3 マスの合計を返す。
        private float AddCrowd(Vector2 position)
        {
            var now = Time.time;
            if (_crowd.Count > 4096)
            {
                _crowd.Clear();
            }

            var cx = Mathf.FloorToInt(position.x);
            var cy = Mathf.FloorToInt(position.y);
            var sum = 0f;
            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    if (_crowd.TryGetValue(Key(cx + dx, cy + dy), out var cell))
                    {
                        sum += FoamPopMath.Decay(cell.x, now - cell.y);
                    }
                }
            }

            var key = Key(cx, cy);
            var value = _crowd.TryGetValue(key, out var own) ? FoamPopMath.Decay(own.x, now - own.y) : 0f;
            _crowd[key] = new Vector2(value + 1f, now);
            return sum;
        }

        private static long Key(int x, int y) => ((long)x << 32) ^ (uint)y;

        private void LateUpdate()
        {
            var s = FoamPopLook.Settings;
            var active = s != null && s.enabled && EnsureResources();
            _spawnedThisFrame = 0;
            if (!active)
            {
                _parts.Clear();
                _pendingKills = 0;
                if (_renderer != null) _renderer.enabled = false;
                return;
            }

            var dt = Time.deltaTime;
            UpdateSound(s, dt);
            Step(s, dt);
            _renderer.enabled = _parts.Count > 0;
            if (_parts.Count > 0)
            {
                _material.SetFloat(BubbleFillId, s.bubbleFill);
                _material.SetColor(MintColorId, s.mintColor);
                _material.SetFloat(PlayerMixId, s.playerColorMix);
                BuildMesh(s);
            }
        }

        private void UpdateSound(FoamPopSettings s, float dt)
        {
            if (_pendingKills <= 0)
            {
                return;
            }

            _pendingTimer -= dt;
            if (_pendingTimer > 0f)
            {
                return;
            }

            var useMany = FoamPopMath.UseManySound(_pendingKills, s.manyKillsCount) && s.soundMany != null && s.soundMany.HasSe;
            var effect = useMany ? s.soundMany : s.sound;
            if (effect != null && effect.HasSe)
            {
                FortressEffectPlayer.PlaySound(effect, Vector3.zero);
            }

            _pendingKills = 0;
        }

        private void Step(FoamPopSettings s, float dt)
        {
            for (var i = _parts.Count - 1; i >= 0; i--)
            {
                var p = _parts[i];
                p.Age += dt;
                if (p.Age > 0f)
                {
                    p.Position += p.Velocity * dt;
                    p.Rise += p.RiseSpeed * dt;
                }

                if (p.Age >= p.Life)
                {
                    // ③ 縮み終わった泡は、光の点になって昇る。
                    if (p.Kind == KindBubble && s.dotSeconds > 0f)
                    {
                        p.Kind = KindDot;
                        p.Age = 0f;
                        p.Life = s.dotSeconds;
                        p.Size = Mathf.Max(0.035f, p.Size * 0.4f);
                        p.RiseSpeed = s.dotRiseSpeed;
                        p.Velocity *= 0.5f;
                        _parts[i] = p;
                        continue;
                    }

                    var last = _parts.Count - 1;
                    _parts[i] = _parts[last];
                    _parts.RemoveAt(last);
                    continue;
                }

                _parts[i] = p;
            }
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
                    Debug.LogWarning($"[FoamPop] シェーダー '{ShaderName}' が見つからないので、光る泡は出しません。");
                }

                return false;
            }

            _material = new Material(shader) { name = "FoamPops", hideFlags = HideFlags.DontSave };
            _mesh = new Mesh { name = "FoamPops", hideFlags = HideFlags.DontSave, indexFormat = IndexFormat.UInt32 };
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

        private void BuildMesh(FoamPopSettings s)
        {
            _vertices.Clear();
            _colors.Clear();
            _uv0.Clear();
            _uv1.Clear();
            _uv2.Clear();
            _uv3.Clear();
            _indices.Clear();

            var z = -s.lift;
            var glowStrength = s.glowStrength;
            foreach (var p in _parts)
            {
                if (p.Age < 0f)
                {
                    continue;
                }

                var t = p.Life > 0f ? p.Age / p.Life : 1f;
                if (p.Kind == KindWrap)
                {
                    var radius = p.Size * FoamPopMath.WrapScale(t);
                    var alpha = 1f - Mathf.Clamp01((t - 0.6f) / 0.4f);
                    AddQuad(p, z, radius, alpha, KindWrap, p.Seed, 0f);
                }
                else if (p.Kind == KindBubble)
                {
                    var radius = p.Size * FoamPopMath.BubbleScale(p.Age, p.End, p.Life - p.End);
                    if (radius <= 0f)
                    {
                        continue;
                    }

                    var rimWidth = Mathf.Clamp(RimMeters / Mathf.Max(radius, 0.01f), 0.1f, 0.4f);
                    AddQuad(p, z, radius * BubbleQuadScale, 1f, KindBubble, rimWidth, glowStrength);
                }
                else
                {
                    var radius = p.Size * (1f - t);
                    if (radius <= 0f)
                    {
                        continue;
                    }

                    AddQuad(p, z, radius, 1f, KindDot, 0f, glowStrength);
                }
            }

            _mesh.Clear();
            if (_vertices.Count == 0)
            {
                return;
            }

            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetUVs(0, _uv0);
            _mesh.SetUVs(1, _uv1);
            _mesh.SetUVs(2, _uv2);
            _mesh.SetUVs(3, _uv3);
            _mesh.SetTriangles(_indices, 0, false);
            // 頂点は中心にあり、四角形はシェーダーでカメラに向けて広げるので、範囲は広めにとる（画面の外で消えないように）。
            _mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        }

        // 中心に4つの頂点を置き、シェーダーがカメラの向きに合わせて四角形に広げる。
        // uv0 = 角(-1〜1)、uv1 = (形の種類, 形ごとの値)、uv2 = (半分の大きさ m, 上へ浮いた量 m)、uv3 = (光の色 rgb, 光の強さ)。
        private void AddQuad(in Part p, float z, float halfSize, float alpha, float kind, float param, float glowStrength)
        {
            var start = _vertices.Count;
            var centre = new Vector3(p.Position.x, p.Position.y, z);
            var rim = p.Rim;
            rim.a = alpha;
            var extra = new Vector2(kind, param);
            var sizeRise = new Vector2(halfSize, p.Rise);
            var glow = new Vector4(p.Glow.r, p.Glow.g, p.Glow.b, glowStrength);
            for (var i = 0; i < 4; i++)
            {
                _vertices.Add(centre);
                _colors.Add(rim);
                _uv1.Add(extra);
                _uv2.Add(sizeRise);
                _uv3.Add(glow);
            }

            _uv0.Add(new Vector2(-1f, -1f));
            _uv0.Add(new Vector2(1f, -1f));
            _uv0.Add(new Vector2(1f, 1f));
            _uv0.Add(new Vector2(-1f, 1f));
            _indices.Add(start);
            _indices.Add(start + 1);
            _indices.Add(start + 2);
            _indices.Add(start);
            _indices.Add(start + 2);
            _indices.Add(start + 3);
        }
    }
}
