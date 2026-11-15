using System;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の目。人と同じ条件で見る:
    /// ・その席のプレイヤーの画面に映る範囲（<see cref="ICpuViewSource"/>。無ければ席のカメラの決まった視点）の中だけ
    /// ・ビームが届く所（射程の中、壁などより手前。ビームと同じ当たり判定で確かめる）だけ
    /// ・見えた様子は、上手さの「反応」の秒数だけ遅れて頭に届く（<see cref="Perceived"/>）
    /// ・敵の数の見積もりは少し外れる（上手さの「見積もりの外れ」）
    /// ・コア（HUD のバーで誰でも分かる体力と、まわりの敵）とスマッシュボール（画面に映っている物）も見る
    /// </summary>
    public sealed class CpuEyes
    {
        private const int HistoryLength = 24;
        private const float CoreBonusRadius = 8f;
        private const float CoreBonus = 1.5f;
        private const int CoreRefreshFrames = 120;

        /// <summary>「コアのまわり」とみなす距離（緑の守護霊の葉が回る広さに近い）。</summary>
        public const float NearCoreRadius = 8.5f;

        private static readonly RaycastHit2D[] HitBuffer = new RaycastHit2D[16];
        private static readonly CoreCrystalController[] NoCores = new CoreCrystalController[0];
        private static CoreCrystalController[] _cores = NoCores;
        private static int _coreRefreshFrame = -CoreRefreshFrames;
        private static SmashBallModule[] _balls = new SmashBallModule[0];
        private static LaserTurret[] _turrets = new LaserTurret[0];

        // 仲間まで届くかを確かめるとき、仲間の手前のこの距離までにある当たりは数えない（倒れた仲間に付く当たりなど）。
        private const float TeammateBodyAllowance = 1f;

        // ほかの人のビームが仲間の上を通っているとみなす、ビームの線から仲間までの距離（m）。
        private const float HelperReach = 0.9f;

        // ほかの人のビームがもう当たっているとみなす、ビームの線から敵までの距離（m）。
        private const float ContestReach = 1.2f;

        private readonly Vector2[] _otherBeamFrom = new Vector2[4];
        private readonly Vector2[] _otherBeamTo = new Vector2[4];
        private int _otherBeams;

        private readonly CpuSight[] _history = new CpuSight[HistoryLength];
        private readonly CpuSight _perceived;
        private readonly int _sectorCount;
        private int _newest = -1;
        private int _filled;
        private int _seenVersion = -1;
        private int _stamp;
        private LaserBeamVisual _beam;
        private LaserTurret _beamOwner;

        public CpuEyes(int sectorCount = CpuSectorMath.DefaultSectorCount)
        {
            _sectorCount = Mathf.Max(8, sectorCount);
            for (var i = 0; i < _history.Length; i++)
            {
                _history[i] = new CpuSight(_sectorCount);
            }

            _perceived = new CpuSight(_sectorCount);
        }

        public int SectorCount => _sectorCount;

        /// <summary>頭に届いている様子（反応の秒数だけ前の様子）。まだ何も見ていなければ空。</summary>
        public CpuSight Perceived => _perceived;

        /// <summary>いちばん新しく見た様子（頭の中の表示用。行動は <see cref="Perceived"/> を使うこと）。</summary>
        public CpuSight Latest => _newest >= 0 ? _history[_newest] : _perceived;

        /// <summary>何か見たことがあるか。</summary>
        public bool HasSeen => _filled > 0;

        /// <summary>群衆の新しい写しが来ていれば見て、頭に届く様子を進める。毎フレーム呼ぶ。</summary>
        public void Tick(CpuContext context, CpuSwarmSampler sampler)
        {
            var turret = context.Turret;
            if (turret != null && sampler != null && sampler.Version != _seenVersion)
            {
                _seenVersion = sampler.Version;
                Look(context, turret, sampler);
            }

            UpdatePerceived(context.Time, context.Skill.reactionSeconds);
        }

        private void Look(CpuContext context, LaserTurret turret, CpuSwarmSampler sampler)
        {
            _newest = (_newest + 1) % _history.Length;
            _filled = Mathf.Min(_filled + 1, _history.Length);
            var sight = _history[_newest];
            sight.Time = context.Time;
            sight.Stamp = ++_stamp;
            sight.Clear();

            var origin = (Vector2)turret.MuzzlePosition;
            var range = turret.tuning != null ? turret.tuning.range : 20f;
            sight.Origin = origin;
            sight.Range = range;
            MeasureObstacles(turret, origin, range, sight.ObstacleDistances);

            sight.HasView = TryGetView(context.Player, out sight.View);

            RefreshCores();
            var core = NearestCore(origin);
            sight.HasCore = core != null && (!sight.HasView || sight.View.Contains(core.transform.position, 1f));
            if (core != null)
            {
                // 体力は HUD のコアのバーで見えるので、コアが画面の外でも分かる。
                sight.CorePosition = core.transform.position;
                sight.CoreHealth01 = core.maxHealth > 0f ? Mathf.Clamp01(core.CurrentHealth / core.maxHealth) : 1f;
            }

            CollectOtherBeams(context.Player);

            var rings = sight.RingCount;
            var ringSize = Mathf.Max(0.01f, sight.RingSize);
            var nearCoreSqr = NearCoreRadius * NearCoreRadius;
            var positions = sampler.Positions;
            for (var i = 0; i < sampler.Count; i++)
            {
                var point = positions[i];
                if (core != null && (point - sight.CorePosition).sqrMagnitude <= nearCoreSqr && (!sight.HasView || sight.View.Contains(point)))
                {
                    sight.NearCoreCount += 1f;
                }

                if (!CpuSectorMath.TryLocate(sight.SectorCount, sight.ObstacleDistances, origin, range, sight.HasView, sight.View, point,
                        out var sector, out var distance))
                {
                    continue;
                }

                var weight = WeightOf(point);
                sight.Weights[sector] += weight;
                sight.RingWeights[sector * rings + Mathf.Min(rings - 1, (int)(distance / ringSize))] += weight;
                if (IsContested(point))
                {
                    sight.ContestedWeights[sector] += weight;
                }
            }

            // 見積もりの外れ（扇ごとに、見るたびに少し違う数に見える）。
            var noise = Mathf.Clamp01(context.Skill.judgementNoise);
            for (var s = 0; s < sight.Weights.Length; s++)
            {
                if (noise > 0f && sight.Weights[s] > 0f)
                {
                    var factor = Mathf.Max(0f, 1f + (context.NextFloat() * 2f - 1f) * noise);
                    sight.Weights[s] *= factor;
                    sight.ContestedWeights[s] *= factor;
                    for (var r = 0; r < rings; r++)
                    {
                        sight.RingWeights[s * rings + r] *= factor;
                    }
                }

                sight.Total += sight.Weights[s];
            }

            if (noise > 0f && sight.NearCoreCount > 0f)
            {
                sight.NearCoreCount *= Mathf.Max(0f, 1f + (context.NextFloat() * 2f - 1f) * noise * 0.5f);
            }

            LookAtBalls(sight, origin, range);
            LookAtTeammates(context.Player, sight, origin, range);
        }

        // 画面に映っていて射程の中にいる仲間の砲台。調子は差し込み口（仲間の調子）から。
        private void LookAtTeammates(int self, CpuSight sight, Vector2 origin, float range)
        {
            var status = CpuPlugins.TeammateStatus;
            if (status != null)
            {
                sight.SelfCondition = status.GetCondition(self);
                sight.SelfHealth01 = status.GetHealth01(self);
            }

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(_beam != null ? _beam.targetLayerMask : (LayerMask)~0);
            foreach (var mate in _turrets)
            {
                if (mate == null || !mate.isActiveAndEnabled || mate.playerIndex == self || mate.playerIndex < 0)
                {
                    continue;
                }

                var position = (Vector2)mate.transform.position;
                var toMate = position - origin;
                var distance = toMate.magnitude;
                if (distance > range || distance < 0.01f || (sight.HasView && !sight.View.Contains(position)))
                {
                    continue;
                }

                sight.Teammates.Add(new CpuTeammateSight
                {
                    Player = mate.playerIndex,
                    Position = position,
                    Angle = CpuSectorMath.AngleOf(toMate),
                    Distance = distance,
                    Condition = status != null ? status.GetCondition(mate.playerIndex) : CpuTeammateCondition.Normal,
                    Health01 = status != null ? status.GetHealth01(mate.playerIndex) : 1f,
                    InLine = ClearUntil(origin, toMate / distance, distance - TeammateBodyAllowance, filter),
                    Helpers = CountHelpers(self, mate, position)
                });
            }
        }

        // origin から direction へ distance まで、ビームを止める当たりが無いか。
        private static bool ClearUntil(Vector2 origin, Vector2 direction, float distance, ContactFilter2D filter)
        {
            if (distance <= 0.01f)
            {
                return true;
            }

            var hits = Physics2D.Raycast(origin, direction, filter, HitBuffer, distance);
            for (var i = 0; i < hits; i++)
            {
                if (HitBuffer[i].distance > 1e-4f)
                {
                    return false;
                }
            }

            return true;
        }

        // 今その仲間の上をビームで通している、ほかの人の数（撃っていて、砲身の線が仲間の近くを通る砲台）。
        private static int CountHelpers(int self, LaserTurret mate, Vector2 position)
        {
            var count = 0;
            foreach (var other in _turrets)
            {
                if (other == null || other == mate || other.playerIndex == self || !other.IsFiring || other.tuning == null)
                {
                    continue;
                }

                Vector2 from = other.MuzzlePosition;
                var to = from + other.AimDirection * other.tuning.range;
                if (DistanceToSegment(position, from, to) <= HelperReach)
                {
                    count++;
                }
            }

            return count;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            var lengthSq = ab.sqrMagnitude;
            var t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / lengthSq) : 0f;
            return (a + ab * t - p).magnitude;
        }

        // 画面に映っていて、射程の中にあるスマッシュボール（割れている・待っている物は見えない）。
        private void LookAtBalls(CpuSight sight, Vector2 origin, float range)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(_beam != null ? _beam.targetLayerMask : (LayerMask)~0);
            foreach (var ball in _balls)
            {
                if (ball == null || !ball.isActiveAndEnabled || ball.Obstacle == null || ball.Obstacle.IsDestroyed || ball.Obstacle.IsInvulnerable)
                {
                    continue;
                }

                var collider = ball.GetComponentInChildren<Collider2D>();
                var position = collider != null ? (Vector2)collider.bounds.center : (Vector2)ball.transform.position;
                var toBall = position - origin;
                var distance = toBall.magnitude;
                if (distance > range || distance < 0.01f || (sight.HasView && !sight.View.Contains(position)))
                {
                    continue;
                }

                var radius = collider != null ? Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y) : 0.5f;
                sight.Balls.Add(new CpuBallSight
                {
                    Position = position,
                    Angle = CpuSectorMath.AngleOf(toBall),
                    Distance = distance,
                    Radius = radius,
                    Health01 = ball.Obstacle.Health01,
                    InLine = FirstHitIs(origin, toBall / distance, distance + radius, ball, filter),
                    Contenders = CountBeamsNear(position, radius + 0.5f)
                });
            }
        }

        // ほかの人（自分以外）の、今撃っているビームの線（撃ち出し口から射程まで）。
        private void CollectOtherBeams(int self)
        {
            _otherBeams = 0;
            foreach (var other in _turrets)
            {
                if (_otherBeams >= _otherBeamFrom.Length)
                {
                    break;
                }

                if (other == null || other.playerIndex == self || !other.IsFiring || other.tuning == null)
                {
                    continue;
                }

                _otherBeamFrom[_otherBeams] = other.MuzzlePosition;
                _otherBeamTo[_otherBeams] = (Vector2)other.MuzzlePosition + other.AimDirection * other.tuning.range;
                _otherBeams++;
            }
        }

        private bool IsContested(Vector2 point) => CountBeamsNear(point, ContestReach) > 0;

        private int CountBeamsNear(Vector2 point, float reach)
        {
            var count = 0;
            for (var i = 0; i < _otherBeams; i++)
            {
                if (DistanceToSegment(point, _otherBeamFrom[i], _otherBeamTo[i]) <= reach)
                {
                    count++;
                }
            }

            return count;
        }

        // ビームと同じ当たり判定で、いちばん近い当たりがそのボールか。
        private static bool FirstHitIs(Vector2 origin, Vector2 direction, float distance, SmashBallModule ball, ContactFilter2D filter)
        {
            var hits = Physics2D.Raycast(origin, direction, filter, HitBuffer, distance);
            var nearest = float.PositiveInfinity;
            Collider2D first = null;
            for (var i = 0; i < hits; i++)
            {
                if (HitBuffer[i].distance > 1e-4f && HitBuffer[i].distance < nearest)
                {
                    nearest = HitBuffer[i].distance;
                    first = HitBuffer[i].collider;
                }
            }

            return first != null && first.GetComponentInParent<SmashBallModule>() == ball;
        }

        // 反応の秒数だけ前の様子を、頭に届いた様子にする（それより新しい様子しか無ければ、いちばん古い様子）。
        private void UpdatePerceived(float now, float reactionSeconds)
        {
            if (_filled == 0)
            {
                return;
            }

            var target = now - Mathf.Max(0f, reactionSeconds);
            CpuSight chosen = null;
            for (var k = 0; k < _filled; k++)
            {
                var sight = _history[(_newest - k + _history.Length) % _history.Length];
                chosen = sight;
                if (sight.Time <= target)
                {
                    break;
                }
            }

            if (chosen != null && chosen.Stamp != _perceived.Stamp)
            {
                _perceived.CopyFrom(chosen);
            }
        }

        // 扇ごとに、ビームと同じ当たり判定（トリガーは通り抜ける・いちばん近い実体で止まる）で止まる距離を測る。
        private void MeasureObstacles(LaserTurret turret, Vector2 origin, float range, float[] distances)
        {
            if (!ReferenceEquals(_beamOwner, turret))
            {
                _beamOwner = turret;
                _beam = turret.GetComponent<LaserBeamVisual>();
            }

            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(_beam != null ? _beam.targetLayerMask : (LayerMask)~0);
            for (var s = 0; s < distances.Length; s++)
            {
                var angle = CpuSectorMath.CenterOf(s, distances.Length) * Mathf.Deg2Rad;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var hits = Physics2D.Raycast(origin, direction, filter, HitBuffer, range);
                var nearest = range;
                for (var i = 0; i < hits; i++)
                {
                    // 撃ち始めの点に重なっている物（足元の当たりなど）は、ビームと同じく数えない。
                    if (HitBuffer[i].distance > 1e-4f && HitBuffer[i].distance < nearest)
                    {
                        nearest = HitBuffer[i].distance;
                    }
                }

                distances[s] = nearest;
            }
        }

        // コアに近い敵ほど急いで倒したいので、多めに数える。
        private static float WeightOf(Vector2 point)
        {
            var weight = 1f;
            foreach (var core in _cores)
            {
                if (core == null)
                {
                    continue;
                }

                var d = Vector2.Distance(point, core.transform.position);
                if (d < CoreBonusRadius)
                {
                    weight = Mathf.Max(weight, 1f + CoreBonus * (1f - d / CoreBonusRadius));
                }
            }

            return weight;
        }

        private static void RefreshCores()
        {
            if (UnityEngine.Time.frameCount - _coreRefreshFrame < CoreRefreshFrames)
            {
                return;
            }

            _coreRefreshFrame = UnityEngine.Time.frameCount;
            _cores = UnityEngine.Object.FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None);
            _balls = UnityEngine.Object.FindObjectsByType<SmashBallModule>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            _turrets = UnityEngine.Object.FindObjectsByType<LaserTurret>(FindObjectsSortMode.None);
        }

        private static CoreCrystalController NearestCore(Vector2 from)
        {
            CoreCrystalController best = null;
            var bestSqr = float.PositiveInfinity;
            foreach (var core in _cores)
            {
                if (core == null)
                {
                    continue;
                }

                var sqr = ((Vector2)core.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = core;
                }
            }

            return best;
        }

        // その席の画面に映る範囲（差し込みがあればそれ、無ければ席のカメラの決まった視点）。
        private static bool TryGetView(int player, out CpuView view)
        {
            var source = CpuPlugins.ViewSource;
            if (source != null && source.TryGetView(player, out view))
            {
                return true;
            }

            return CpuDefaultView.TryGet(player, out view);
        }
    }

    /// <summary>席のカメラの決まった視点から、画面に映る範囲を出す（傾きは無視する。<see cref="CameraViewArea"/> と同じ見積もり）。</summary>
    public static class CpuDefaultView
    {
        public static bool TryGet(int player, out CpuView view)
        {
            var rig = FortressCameraRig.Active;
            if (rig == null || rig.preset == null || !ViewerIndex.IsPlayer(player))
            {
                view = default;
                return false;
            }

            var settings = rig.preset.GetView(FortressSeats.CameraSeatOf(player));
            var halfHeight = Mathf.Max(0.5f, settings.VisibleHalfHeight);
            var halfWidth = halfHeight * Mathf.Max(0.1f, rig.Aspect);
            view = new CpuView(settings.center, new Vector2(halfWidth, halfHeight), CameraViewMath.ScreenUp(settings));
            return true;
        }
    }
}
