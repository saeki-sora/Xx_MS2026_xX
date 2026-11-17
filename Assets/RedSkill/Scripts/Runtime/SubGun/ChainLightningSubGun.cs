using System;
using System.Collections.Generic;
using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Skills.SubGuns
{
    /// <summary>
    /// 黄の副砲「稲妻チェイン」: 一定の間隔で、本体のレーザーのどこかから横の敵へ稲妻が飛び、さらに次の敵へ乗り移る。
    /// 稲妻の出どころは、レーザーの近くにいる敵の中から毎回ランダムに選ぶ（レーザーの根元〜先のどこからでも出る）。
    /// 強く握るほど乗り移る回数が増える。本体の先が障害物・ボスの部位に当たっていれば、そこにも少しダメージ。
    /// 赤のスキルの稲妻と見分けられるよう、角ばったジグザグで、当たった敵に星形の光を付ける。止めのある2段で消える。
    /// </summary>
    [Serializable]
    public sealed class ChainLightningSubGun : SubGunSettings
    {
        [SkillQuick(1, "稲妻の間隔（秒）")]
        [Tooltip("稲妻を出す間隔（秒）。")]
        [SkillLabel("出す間隔（秒）")]
        public GripRange interval = new GripRange(0.55f, 0.35f);

        [SkillQuick(2, "乗り移る数（体）")]
        [Tooltip("1回の稲妻で当たる敵の数。小数は四捨五入。")]
        [SkillLabel("当たる敵の数（体）")]
        public GripRange hops = new GripRange(2f, 4f);

        [SkillQuick(3, "1体あたりのダメージ")]
        [Tooltip("稲妻が当たった敵1体に与えるダメージ（ふつうの敵の体力は 10）。")]
        [SkillLabel("1体あたりのダメージ")]
        [Min(0f)]
        public float damage = 10f;

        [SkillQuick(4, "レーザーから届く距離（m）")]
        [Tooltip("本体のレーザーから横へ、最初の敵まで届く距離（m）。レーザーのこの範囲にいる敵の中から、毎回ランダムに1体選んで稲妻を飛ばす。" +
                 "この中に敵がいなければ出ない。")]
        [SkillLabel("レーザーから届く距離（m）")]
        [Min(0f)]
        public float firstReach = 3f;

        [Tooltip("砲台のすぐそばからは出さない距離（m）。レーザーの根元からこの長さより先から出る。")]
        [SkillLabel("根元から出さない長さ（m）")]
        [Min(0f)]
        public float minAlong = 0.8f;

        [Tooltip("ON なら、本体のレーザーの中にいる敵（本体がもう当てている敵）は最初の相手に選ばない。横に離れた敵へ飛ぶので、稲妻が見やすい。")]
        [SkillLabel("本体の中の敵は選ばない")]
        public bool skipEnemiesInsideBeam = true;

        [Tooltip("当たった敵から、次の敵へ乗り移れる距離（m）。")]
        [SkillLabel("乗り移れる距離（m）")]
        [Min(0f)]
        public float hopReach = 2.2f;

        [Tooltip("本体の先が当たっている障害物・壁に、1回の稲妻で与えるダメージ。ボスの部位には上のダメージが入る。")]
        [SkillLabel("障害物へのダメージ")]
        [Min(0f)]
        public float obstacleDamage = 2f;

        [SkillQuick(5, "稲妻の太さ（m）")]
        [Tooltip("稲妻の線の太さ（m）。")]
        [SkillLabel("線の太さ（m）")]
        [Min(0.005f)]
        public float boltWidth = 0.14f;

        [Tooltip("ジグザグの横へのずれ（m）。")]
        [SkillLabel("ジグザグの大きさ（m）")]
        [Min(0f)]
        public float jaggedness = 0.32f;

        [Tooltip("1回の乗り移りを何回折り曲げるか。")]
        [SkillLabel("折れ曲がりの数")]
        [Range(1, 8)]
        public int bends = 4;

        [Tooltip("稲妻が見えている時間（秒）。前半ははっきり、後半はうすく（止めのある2段）。")]
        [SkillLabel("見えている時間（秒）")]
        [Min(0.02f)]
        public float showSeconds = 0.28f;

        [Tooltip("稲妻の芯（白い線）の太さ（線の太さに掛ける）。0 で芯なし。")]
        [SkillLabel("白い芯の太さ（倍）")]
        [Range(0f, 1f)]
        public float coreWidthScale = 0.35f;

        [Tooltip("線のまわりのぼんやりした光の太さ（線の太さに掛ける）。0 で光なし。")]
        [SkillLabel("光の太さ（倍）")]
        [Min(0f)]
        public float glowWidthScale = 3f;

        [Tooltip("ぼんやりした光の濃さ。")]
        [SkillLabel("光の濃さ")]
        [Range(0f, 1f)]
        public float glowAlpha = 0.35f;

        [Tooltip("稲妻を出したときの効果音・エフェクト（空なら無し）。エフェクトは最初の敵の場所に出る。")]
        [SkillLabel("出したとき（効果音・エフェクト）")]
        public FortressEffect zapEffect = new FortressEffect();

        public override SubGunRunner AddRunner(GameObject turretObject) => turretObject.AddComponent<ChainLightningSubGunRunner>();
    }

    /// <summary>「稲妻チェイン」を動かす係。</summary>
    public sealed class ChainLightningSubGunRunner : SubGunRunner
    {
        private const int MaxHops = 8;

        private sealed class Bolt
        {
            public readonly List<Vector2> Points = new List<Vector2>(64);
            public float Age;
        }

        private readonly List<Bolt> _bolts = new List<Bolt>(4);
        private readonly Stack<Bolt> _pool = new Stack<Bolt>();
        private readonly Vector2[] _chain = new Vector2[MaxHops + 1];
        private readonly int[] _chainIds = new int[MaxHops];
        private float _cooldown;
        private bool _pending;
        private Vector2 _pendingOrigin;
        private Vector2 _pendingDirection;
        private float _pendingLength;
        private float _pendingHalfWidth;
        private int _pendingHops;

        private ChainLightningSubGun Settings => (ChainLightningSubGun)BaseSettings;

        protected override void Tick(float dt)
        {
            var settings = Settings;
            if (MainBeamOn)
            {
                _cooldown -= dt;
                if (_cooldown <= 0f)
                {
                    _cooldown = Mathf.Max(0.05f, settings.interval.At(Grip01));
                    // 敵の場所は群衆の配列を読める瞬間にしか分からないので、そこで撃つ。
                    _pending = true;
                    _pendingOrigin = BeamOrigin;
                    _pendingDirection = BeamDirection;
                    _pendingLength = BeamLength;
                    _pendingHalfWidth = BeamHalfWidth;
                    _pendingHops = Mathf.Clamp(Mathf.RoundToInt(settings.hops.At(Grip01)), 1, MaxHops);
                    var impact = BeamImpactCollider;
                    if (impact != null)
                    {
                        SubGunHits.DamageCollider(impact, Turret, settings.damage, settings.obstacleDamage);
                    }
                }
            }
            else
            {
                _cooldown = Mathf.Min(_cooldown, 0.15f);
                _pending = false;
            }

            for (var i = _bolts.Count - 1; i >= 0; i--)
            {
                _bolts[i].Age += dt;
                if (_bolts[i].Age >= settings.showSeconds)
                {
                    _pool.Push(_bolts[i]);
                    _bolts.RemoveAt(i);
                }
            }
        }

        protected override void OnSwarmReadable(SwarmSystem swarm)
        {
            if (!_pending)
            {
                return;
            }

            _pending = false;
            var settings = Settings;
            var storage = swarm.Storage;
            var replica = swarm.IsReplica;
            if (!PickFirst(settings, swarm, out var firstIndex, out var firstPosition, out var from))
            {
                return;
            }

            // 1体目: レーザーの上の点から横の敵へ。
            var found = 0;
            _chain[0] = from;
            _chainIds[found] = storage.netId[firstIndex];
            found++;
            _chain[found] = firstPosition;
            SubGunHits.QueueSingle(swarm, firstPosition, from, settings.damage, Owner);
            AddFlash(firstPosition);
            from = firstPosition;
            var reach = settings.hopReach;

            // 2体目から: 近い敵へ乗り移る。
            for (var hop = 1; hop < _pendingHops; hop++)
            {
                var best = -1;
                var bestDistance = reach * reach;
                var bestPosition = Vector2.zero;
                for (var i = 0; i < storage.Count; i++)
                {
                    if (!SubGunHits.TryGetEnemy(storage, i, replica, out var position))
                    {
                        continue;
                    }

                    var d2 = (position - from).sqrMagnitude;
                    if (d2 >= bestDistance || AlreadyHit(storage.netId[i], found))
                    {
                        continue;
                    }

                    best = i;
                    bestDistance = d2;
                    bestPosition = position;
                }

                if (best < 0)
                {
                    break;
                }

                _chainIds[found] = storage.netId[best];
                found++;
                _chain[found] = bestPosition;
                SubGunHits.QueueSingle(swarm, bestPosition, from, settings.damage, Owner);
                AddFlash(bestPosition);
                from = bestPosition;
                reach = settings.hopReach;
            }

            if (found == 0)
            {
                return;
            }

            // 稲妻の出どころ（レーザーの上の点）にも小さな光。
            AddFlash(_chain[0], 0.6f);
            AddBolt(settings, found);
            FortressEffectPlayer.PlayOnce(settings.zapEffect, new Vector3(_chain[1].x, _chain[1].y, GroundZ), Quaternion.identity, Owner);
        }

        /// <summary>
        /// 最初の相手: 本体のレーザーの横（根元から少し先〜届いている所まで、横に「届く距離」以内）にいる敵の中から、ランダムに1体。
        /// from はその敵に一番近いレーザーの上の点（稲妻の出どころ）。
        /// </summary>
        private bool PickFirst(ChainLightningSubGun settings, SwarmSystem swarm, out int index, out Vector2 position, out Vector2 from)
        {
            var storage = swarm.Storage;
            var replica = swarm.IsReplica;
            var side = new Vector2(-_pendingDirection.y, _pendingDirection.x);
            var minLateral = settings.skipEnemiesInsideBeam ? _pendingHalfWidth : 0f;
            var seen = 0;
            index = -1;
            position = Vector2.zero;
            from = _pendingOrigin;
            for (var i = 0; i < storage.Count; i++)
            {
                if (!SubGunHits.TryGetEnemy(storage, i, replica, out var p))
                {
                    continue;
                }

                var d = p - _pendingOrigin;
                var along = Vector2.Dot(d, _pendingDirection);
                if (along < settings.minAlong || along > _pendingLength)
                {
                    continue;
                }

                var lateral = Mathf.Abs(Vector2.Dot(d, side));
                if (lateral < minLateral || lateral > settings.firstReach)
                {
                    continue;
                }

                // 候補の中から等しい確率で1体（全部を覚えずに選ぶやり方）。
                seen++;
                if (UnityEngine.Random.Range(0, seen) == 0)
                {
                    index = i;
                    position = p;
                    from = _pendingOrigin + _pendingDirection * along;
                }
            }

            return index >= 0;
        }

        private bool AlreadyHit(int id, int found)
        {
            for (var k = 0; k < found; k++)
            {
                if (_chainIds[k] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddBolt(ChainLightningSubGun settings, int found)
        {
            var bolt = _pool.Count > 0 ? _pool.Pop() : new Bolt();
            bolt.Age = 0f;
            bolt.Points.Clear();
            bolt.Points.Add(_chain[0]);
            for (var h = 1; h <= found; h++)
            {
                var a = _chain[h - 1];
                var b = _chain[h];
                var d = b - a;
                var side = new Vector2(-d.y, d.x).normalized;
                // 角ばったジグザグ（左右交互に折る）。
                for (var k = 1; k <= settings.bends; k++)
                {
                    var t = k / (settings.bends + 1f);
                    var swing = (k % 2 == 0 ? -1f : 1f) * settings.jaggedness * UnityEngine.Random.Range(0.5f, 1f);
                    bolt.Points.Add(a + d * t + side * swing);
                }

                bolt.Points.Add(b);
            }

            _bolts.Add(bolt);
        }

        protected override void Draw(SubGunLayer back, SubGunLayer front)
        {
            var settings = Settings;
            for (var i = 0; i < _bolts.Count; i++)
            {
                var bolt = _bolts[i];
                // 止めのある2段: 前半ははっきり、後半はうすく細く（なめらかに消さない）。
                var first = bolt.Age < settings.showSeconds * 0.5f;
                var alpha = first ? 1f : 0.45f;
                var width = settings.boltWidth * (first ? 1f : 0.6f);
                var color = settings.ColorFor(Owner, alpha);
                if (settings.glowWidthScale > 0f && settings.glowAlpha > 0f)
                {
                    var glow = color;
                    glow.a *= settings.glowAlpha;
                    front.Polyline(bolt.Points, 0, bolt.Points.Count, width * settings.glowWidthScale, glow);
                }

                OutlinedPolyline(front, bolt.Points, 0, bolt.Points.Count, width, color);
                if (settings.coreWidthScale > 0f && first)
                {
                    front.Polyline(bolt.Points, 0, bolt.Points.Count, width * settings.coreWidthScale, new Color(1f, 1f, 1f, settings.opacity));
                }
            }
        }
    }
}
