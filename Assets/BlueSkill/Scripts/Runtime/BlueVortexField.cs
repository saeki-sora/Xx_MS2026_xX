using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Net;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Skills.Blue
{
    /// <summary>
    /// 出ている全ての渦（<see cref="BlueVortexArea"/>）の「敵への効果」をまとめてかける係。シーンに置く必要はない
    /// （最初の渦が出たときに "[BlueSkill] Vortex Field" として自動で作られ、渦も足止めも無くなると消える）。
    ///
    /// 群衆の敵（SwarmSystem）: 群衆の計算が止まっている安全な瞬間（<see cref="SwarmSystem.StorageReadable"/>）に、
    /// 全員を1回なめる Burst のジョブで次をかける。群衆のコードは変えていない（公開されている配列を読み書きするだけ）。
    ///  ・足止め: 渦の中の敵の「歩く速さの倍率」(speedScale) を下げる（1 = 止まる）。渦から出たら元に戻す。
    ///  ・巻き込み: 速度を「渦に沿って回る＋中心へ寄る」向きに置きかえる（壁・押し合いの計算はそのまま効く）。
    ///  ・被ダメージ増加: 前のフレームからの体力の減り（レーザー・爆発など）に倍率ぶんを上乗せする。
    ///  ・渦のダメージ: 渦が決めた間隔で一定量。最後に当てた人 = 渦を出した人（撃破数に入る）。
    ///  敵1体は「ネット番号」(netId) で見分けて、元の速さ・前の体力を覚えておく。
    /// Actor の敵（EnemyController）: LateUpdate で動いた分を足止めの強さぶん戻し、渦に沿って動かし、渦のダメージを与える。
    ///
    /// ネット対戦: 結果を決めるPC（オフライン／Host）だけが効果をかける。Client は Host から届く位置・体力をそのまま表示するので、
    /// 足止めも巻き込みも Client の画面に反映される（通信は増やしていない）。どのPCでも「つかまっている敵の位置」の見本だけは集め、
    /// 渦の見た目（つかまった敵から出る泡）に使う。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [AddComponentMenu("")]
    public sealed class BlueVortexField : MonoBehaviour
    {
        /// <summary>netId は 0〜65535（SwarmSystem のネット番号の範囲）。</summary>
        private const int IdCapacity = 65536;

        private const int MaxZones = 32;
        private const int MaxSamples = 48;

        private static BlueVortexField _instance;
        private static readonly List<Vector2> SampleList = new List<Vector2>(MaxSamples);

        private readonly Dictionary<EnemyController, Vector2> _actorPrev = new Dictionary<EnemyController, Vector2>();
        private readonly List<EnemyController> _actorRemove = new List<EnemyController>();

        private SwarmSystem _swarm;
        private NativeArray<BlueVortexZone> _zones;
        private NativeArray<float> _origSpeed;
        private NativeArray<float> _prevHp;
        private NativeArray<int> _seenFrame;
        private NativeArray<byte> _held;
        private NativeArray<float2> _samples;
        private NativeArray<int> _counters;
        private bool _allocated;
        private int _frame;
        private int _lastUnityFrame = -10;
        private int _heldCount;
        private int _affectedCount;

        /// <summary>今つかまっている群衆の敵の数（最後に計算したフレーム。Client では見本の範囲で数えた値）。</summary>
        public static int AffectedCount => _instance != null ? _instance._affectedCount : 0;

        /// <summary>つかまっている敵の位置の見本（見た目用、最大 48 個。毎フレーム入れ替わる）。</summary>
        public static IReadOnlyList<Vector2> TrappedSamples => SampleList;

        /// <summary>渦が出たときに呼ばれる（無ければ作る）。</summary>
        internal static void Ensure()
        {
            if (_instance != null)
            {
                return;
            }

            // ふつうのシーンのオブジェクトにする（DontSave にすると Play を止めたときに消えずに残ってしまう）。
            var go = new GameObject("[BlueSkill] Vortex Field");
            _instance = go.AddComponent<BlueVortexField>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            SampleList.Clear();
        }

        private void OnDestroy()
        {
            Subscribe(null);
            if (_allocated)
            {
                _zones.Dispose();
                _origSpeed.Dispose();
                _prevHp.Dispose();
                _seenFrame.Dispose();
                _held.Dispose();
                _samples.Dispose();
                _counters.Dispose();
                _allocated = false;
            }

            if (_instance == this)
            {
                _instance = null;
                SampleList.Clear();
            }
        }

        private void Awake()
        {
            Subscribe(SwarmSystem.Current);
        }

        private void Update()
        {
            Subscribe(SwarmSystem.Current);

            // 渦も、元に戻す敵も無くなったら片付ける（同じフレームに次の渦が出たら新しく作り直せるよう、先に外す）。
            if (BlueVortexArea.Active.Count == 0 && _heldCount == 0 && _actorPrev.Count == 0)
            {
                if (_instance == this)
                {
                    _instance = null;
                }

                SampleList.Clear();
                Subscribe(null);
                Destroy(gameObject);
            }
        }

        private void Subscribe(SwarmSystem swarm)
        {
            if (_swarm == swarm)
            {
                return;
            }

            if (_swarm != null)
            {
                _swarm.StorageReadable -= HandleStorageReadable;
            }

            _swarm = swarm;
            _heldCount = 0;
            if (_swarm != null)
            {
                _swarm.StorageReadable += HandleStorageReadable;
            }
        }

        private void Allocate()
        {
            if (_allocated)
            {
                return;
            }

            _zones = new NativeArray<BlueVortexZone>(MaxZones, Allocator.Persistent);
            _origSpeed = new NativeArray<float>(IdCapacity, Allocator.Persistent);
            _prevHp = new NativeArray<float>(IdCapacity, Allocator.Persistent);
            _seenFrame = new NativeArray<int>(IdCapacity, Allocator.Persistent);
            _held = new NativeArray<byte>(IdCapacity, Allocator.Persistent);
            _samples = new NativeArray<float2>(MaxSamples, Allocator.Persistent);
            _counters = new NativeArray<int>(4, Allocator.Persistent);
            _allocated = true;
        }

        /// <summary>群衆の計算が止まっている瞬間（SwarmSystem.Update の中）。ここでだけ群衆の配列を触れる。</summary>
        private void HandleStorageReadable()
        {
            var swarm = _swarm;
            if (swarm == null || swarm.Storage == null)
            {
                return;
            }

            var zoneCount = FillZones(swarm);
            if (zoneCount == 0 && _heldCount == 0)
            {
                SampleList.Clear();
                _affectedCount = 0;
                return;
            }

            var storage = swarm.Storage;
            var count = storage.Count;
            var simulate = FortressNet.HasSimulationAuthority && !swarm.IsReplica;
            Allocate();
            // 前のフレームに計算していなかった（途切れた）ときは、覚えている体力などを使わないように番号を1つ飛ばす。
            if (_lastUnityFrame != Time.frameCount - 1)
            {
                _frame++;
            }

            _frame++;
            _lastUnityFrame = Time.frameCount;
            _counters[0] = 0;
            _counters[1] = 0;
            _counters[2] = 0;

            if (count > 0)
            {
                var job = new BlueVortexJob
                {
                    pos = storage.pos,
                    vel = storage.vel,
                    hp = storage.hp,
                    speedScale = storage.speedScale,
                    netId = storage.netId,
                    lastHit = storage.lastHit,
                    count = count,
                    zones = _zones,
                    zoneCount = zoneCount,
                    origSpeed = _origSpeed,
                    prevHp = _prevHp,
                    seenFrame = _seenFrame,
                    held = _held,
                    samples = _samples,
                    counters = _counters,
                    frame = _frame,
                    simulate = simulate,
                    sampleChance = SampleChance()
                };
                job.Run();
            }

            _affectedCount = _counters[1];
            _heldCount = simulate ? _counters[2] : 0;
            SampleList.Clear();
            var samples = math.min(_counters[0], MaxSamples);
            for (var i = 0; i < samples; i++)
            {
                SampleList.Add(_samples[i]);
            }
        }

        /// <summary>見本を毎フレームだいたい MaxSamples 個集める確率（前のフレームのつかまっている数から決める）。</summary>
        private float SampleChance()
        {
            var affected = math.max(_affectedCount, 1);
            return math.saturate((float)MaxSamples / affected);
        }

        private int FillZones(SwarmSystem swarm)
        {
            var areas = BlueVortexArea.Active;
            var n = 0;
            if (areas.Count > 0)
            {
                Allocate();
            }

            for (var i = 0; i < areas.Count && n < MaxZones; i++)
            {
                var area = areas[i];
                if (area == null || !area.TryGetZone(out var zone))
                {
                    continue;
                }

                _zones[n++] = zone;
            }

            return n;
        }

        // ------------------------------------------------------------------
        // Actor の敵（GameObject の敵）
        // ------------------------------------------------------------------

        private void LateUpdate()
        {
            if (!FortressNet.HasSimulationAuthority)
            {
                _actorPrev.Clear();
                return;
            }

            var actors = EnemyController.Active;
            var areas = BlueVortexArea.Active;
            var dt = Time.deltaTime;

            _actorRemove.Clear();
            foreach (var pair in _actorPrev)
            {
                if (pair.Key == null || !pair.Key.isActiveAndEnabled)
                {
                    _actorRemove.Add(pair.Key);
                }
            }

            for (var i = 0; i < _actorRemove.Count; i++)
            {
                _actorPrev.Remove(_actorRemove[i]);
            }

            if (areas.Count == 0)
            {
                _actorPrev.Clear();
                return;
            }

            for (var i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                if (actor == null || actor.IsReplica)
                {
                    continue;
                }

                var position = (Vector2)actor.transform.position;
                if (!BestActorZone(areas, position, out var area, out var zone, out var weight))
                {
                    _actorPrev.Remove(actor);
                    continue;
                }

                if (_actorPrev.TryGetValue(actor, out var prev))
                {
                    var hold = Mathf.Clamp01(zone.hold * weight);
                    var moved = position - prev;
                    var offset = position - (Vector2)zone.center;
                    var distance = offset.magnitude;
                    var dir = distance > 0.0001f ? offset / distance : Vector2.right;
                    var tangent = new Vector2(-dir.y, dir.x) * zone.direction;
                    var pull = distance > zone.pullInner ? -dir * zone.pull : Vector2.zero;
                    var next = prev + moved * (1f - hold) + (tangent * zone.swirl + pull) * (weight * dt);
                    var t = actor.transform;
                    t.position = new Vector3(next.x, next.y, t.position.z);
                    position = next;
                }

                _actorPrev[actor] = position;

                if (zone.tickDamage > 0f && area.ActorTickThisFrame)
                {
                    var slot = PlayerSkillSlot.Find(zone.owner);
                    actor.ApplyLaserDamage(zone.tickDamage, null, slot != null ? slot.Turret : null);
                }
            }
        }

        private static bool BestActorZone(IReadOnlyList<BlueVortexArea> areas, Vector2 position, out BlueVortexArea best, out BlueVortexZone bestZone, out float bestWeight)
        {
            best = null;
            bestZone = default;
            bestWeight = 0f;
            for (var i = 0; i < areas.Count; i++)
            {
                var area = areas[i];
                if (area == null || !area.AffectsActors || !area.TryGetZone(out var zone))
                {
                    continue;
                }

                var weight = BlueVortexJob.Weight(zone, position);
                if (weight > bestWeight)
                {
                    best = area;
                    bestZone = zone;
                    bestWeight = weight;
                }
            }

            return best != null;
        }
    }

    /// <summary>渦1つぶんの効果（ジョブに渡す値）。</summary>
    public struct BlueVortexZone
    {
        public float2 center;
        public float radius;

        /// <summary>ふちで効果が弱まる幅（m）。0 ならふちでいきなり効く。</summary>
        public float softWidth;

        /// <summary>効果全体の強さ（0〜1。出てくる／消えるとき）。</summary>
        public float strength;

        public float hold;
        public float swirl;
        public float pull;

        /// <summary>これより内側へは引き寄せない（m）。</summary>
        public float pullInner;

        /// <summary>+1 = 反時計回り、-1 = 時計回り。</summary>
        public float direction;

        public float damageTakenMultiplier;

        /// <summary>このフレームに与える渦のダメージ（ダメージのフレーム以外は 0）。</summary>
        public float tickDamage;

        public int owner;
    }

    /// <summary>群衆の敵全員に渦の効果をかける（1スレッド、Burst）。</summary>
    [BurstCompile]
    internal struct BlueVortexJob : IJob
    {
        public NativeArray<float2> pos;
        public NativeArray<float2> vel;
        public NativeArray<float> hp;
        public NativeArray<float> speedScale;
        [ReadOnly] public NativeArray<int> netId;
        public NativeArray<int> lastHit;
        public int count;

        [ReadOnly] public NativeArray<BlueVortexZone> zones;
        public int zoneCount;

        public NativeArray<float> origSpeed;
        public NativeArray<float> prevHp;
        public NativeArray<int> seenFrame;
        public NativeArray<byte> held;

        public NativeArray<float2> samples;

        /// <summary>[0]=見本の数, [1]=つかまっている数, [2]=足止め中（元に戻す必要がある）数</summary>
        public NativeArray<int> counters;

        public int frame;
        public bool simulate;
        public float sampleChance;

        public static float Weight(in BlueVortexZone zone, float2 p)
        {
            var d = math.distance(p, zone.center);
            if (d >= zone.radius || zone.strength <= 0f)
            {
                return 0f;
            }

            var edge = zone.softWidth > 0.0001f ? math.saturate((zone.radius - d) / zone.softWidth) : 1f;
            return edge * edge * (3f - 2f * edge) * zone.strength;
        }

        public void Execute()
        {
            var sampleCount = 0;
            var affected = 0;
            var stillHeld = 0;

            for (var i = 0; i < count; i++)
            {
                var id = netId[i];
                if (id < 0 || id >= held.Length)
                {
                    continue;
                }

                // 前のフレームにも見ていた敵か（番号の使い回し・途中から始まった場合は、覚えている値を使わない）。
                var continuous = seenFrame[id] == frame - 1;
                seenFrame[id] = frame;
                if (!continuous)
                {
                    // 足止めしたまま途切れた場合も、止まったままにならないよう元の速さに戻す
                    // （番号が別の敵に使い回された場合は、前の敵の速さになるが、差は数割以内）。
                    if (simulate && held[id] != 0)
                    {
                        speedScale[i] = origSpeed[id];
                    }

                    held[id] = 0;
                    prevHp[id] = hp[i];
                }

                var p = pos[i];
                var bestWeight = 0f;
                var best = -1;
                var damageMul = 1f;
                var tick = 0f;
                var tickOwner = -1;
                for (var z = 0; z < zoneCount; z++)
                {
                    var zone = zones[z];
                    var w = Weight(zone, p);
                    if (w <= 0f)
                    {
                        continue;
                    }

                    if (w > bestWeight)
                    {
                        bestWeight = w;
                        best = z;
                    }

                    damageMul = math.max(damageMul, 1f + (zone.damageTakenMultiplier - 1f) * w);
                    if (zone.tickDamage > 0f)
                    {
                        tick += zone.tickDamage;
                        tickOwner = zone.owner;
                    }
                }

                if (best < 0)
                {
                    if (simulate && held[id] != 0)
                    {
                        speedScale[i] = origSpeed[id];
                    }

                    held[id] = 0;
                    prevHp[id] = hp[i];
                    continue;
                }

                affected++;
                if (sampleCount < samples.Length && Hash(id, frame) < sampleChance)
                {
                    samples[sampleCount++] = p;
                }

                if (!simulate)
                {
                    continue;
                }

                var health = hp[i];
                if (health > 0f)
                {
                    // 被ダメージ増加: 前のフレームからの減り（レーザー・爆発など）に倍率ぶんを足す。
                    var drop = prevHp[id] - health;
                    if (continuous && drop > 0f && damageMul > 1f)
                    {
                        health -= drop * (damageMul - 1f);
                    }

                    // 渦のダメージ（倍率はかけない）。
                    if (tick > 0f)
                    {
                        health -= tick;
                        if (tickOwner >= 0)
                        {
                            lastHit[i] = tickOwner;
                        }
                    }

                    hp[i] = health;
                }

                prevHp[id] = health;

                var zoneBest = zones[best];

                // 足止め: 歩く速さの倍率を下げる（元の値を覚えておき、出たら戻す）。
                if (held[id] == 0)
                {
                    origSpeed[id] = speedScale[i];
                    held[id] = 1;
                }

                speedScale[i] = origSpeed[id] * (1f - math.saturate(zoneBest.hold * bestWeight));
                stillHeld++;

                // 巻き込み: 渦に沿って回る＋中心へ寄る速度にする。
                var offset = p - zoneBest.center;
                var distance = math.length(offset);
                var dir = distance > 0.0001f ? offset / distance : new float2(1f, 0f);
                var tangent = new float2(-dir.y, dir.x) * zoneBest.direction;
                var target = tangent * zoneBest.swirl;
                if (distance > zoneBest.pullInner)
                {
                    target -= dir * zoneBest.pull;
                }

                if (zoneBest.swirl > 0f || zoneBest.pull > 0f)
                {
                    vel[i] = math.lerp(vel[i], target, bestWeight);
                }
            }

            counters[0] = sampleCount;
            counters[1] = affected;
            counters[2] = stillHeld;
        }

        private static float Hash(int id, int frame)
        {
            var h = (uint)(id * 73856093) ^ (uint)(frame * 19349663);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
