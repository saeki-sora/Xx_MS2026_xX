using MS2026.Fortress;
using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>【実験】行き先の決まりに使う数（<see cref="CpuMoveSettings"/> から作る）。</summary>
    public struct CpuMoveTuning
    {
        public float AvoidWeight;
        public float ThreatFullCount;
        public float ThreatIgnoreCount;
        public float HomeFreeRadius;
        public float HomePullRange;
        public float HomeWeight;
        public float DeadZone;

        public static CpuMoveTuning From(CpuMoveSettings settings) => new CpuMoveTuning
        {
            AvoidWeight = settings.avoidWeight,
            ThreatFullCount = settings.threatFullCount,
            ThreatIgnoreCount = settings.threatIgnoreCount,
            HomeFreeRadius = settings.homeFreeRadius,
            HomePullRange = settings.homePullRange,
            HomeWeight = settings.homeWeight,
            DeadZone = settings.deadZone
        };
    }

    /// <summary>【実験】行き先の決まり（テストできるように Unity のオブジェクトに触らない）。</summary>
    public static class CpuMoveMath
    {
        /// <summary>
        /// すぐそばの敵の「押してくる向き」の合計（扇ごとの向き × 輪の重み。内側の輪 1、ひとつ外側 outerShare）。
        /// 敵がいる向きを指す（避けるならこの反対へ）。
        /// </summary>
        public static Vector2 Threat(CpuSight sight, float outerShare, out float count)
        {
            count = 0f;
            var threat = Vector2.zero;
            if (sight == null || sight.RingCount < 1)
            {
                return threat;
            }

            var sectors = sight.SectorCount;
            for (var s = 0; s < sectors; s++)
            {
                var inner = sight.RingWeights[s * sight.RingCount];
                var outer = sight.RingCount > 1 ? sight.RingWeights[s * sight.RingCount + 1] : 0f;
                var weight = inner + outer * outerShare;
                if (weight <= 0f)
                {
                    continue;
                }

                var angle = CpuSectorMath.CenterOf(s, sectors) * Mathf.Deg2Rad;
                threat += new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * weight;
                count += weight;
            }

            return threat;
        }

        /// <summary>
        /// 行きたい向き（長さ 0〜1）。敵から離れる向きと、席へ戻る向きを足す。
        /// </summary>
        public static Vector2 Plan(Vector2 position, bool hasHome, Vector2 home, Vector2 threat, float threatCount, in CpuMoveTuning settings)
        {
            var result = Vector2.zero;

            // 敵を避ける（少しの敵は無視する）。向きが打ち消し合う（四方から囲まれた）ときは強く避けない。
            if (threatCount > settings.ThreatIgnoreCount && threat.sqrMagnitude > 1e-6f)
            {
                var strength = Mathf.Clamp01((threatCount - settings.ThreatIgnoreCount) /
                                             Mathf.Max(0.01f, settings.ThreatFullCount - settings.ThreatIgnoreCount));
                var lopsided = Mathf.Clamp01(threat.magnitude / Mathf.Max(0.01f, threatCount));
                result -= threat.normalized * (strength * Mathf.Lerp(0.35f, 1f, lopsided) * settings.AvoidWeight);
            }

            // 席のまわりに戻る。
            if (hasHome)
            {
                var offset = home - position;
                var distance = offset.magnitude;
                if (distance > settings.HomeFreeRadius && distance > 1e-4f)
                {
                    var pull = Mathf.Clamp01((distance - settings.HomeFreeRadius) / Mathf.Max(0.1f, settings.HomePullRange));
                    result += offset / distance * (pull * settings.HomeWeight);
                }
            }

            result = Vector2.ClampMagnitude(result, 1f);
            return result.magnitude < settings.DeadZone ? Vector2.zero : result;
        }
    }

    /// <summary>
    /// 【実験】CPU の行き先を決める係（「手が2本」の移動の方。撃つなどの行動と同時に動く）。
    /// ・目が頭に届けた様子（反応の遅れあり）で、すぐそばの敵から離れる
    /// ・席から離れすぎたら戻る（席はステージが知らせた場所。席替えで変わる。知らせが無ければ初めて見た場所）
    /// ・決め直すのは決まった間隔ごと。向きはなめらかに変える
    /// 記録「移動した秒」「囲まれた秒」。
    /// </summary>
    internal sealed class CpuMovePlanner : ICpuMovePlanner
    {
        public const string MovedSeconds = "移動した秒";
        public const string SurroundedSeconds = "囲まれた秒";

        private Vector2 _target;
        private Vector2 _current;
        private float _planTimer;
        private bool _hasFirstSeen;
        private Vector2 _firstSeen;
        private LaserTurret _firstSeenTurret;

        public Vector2 Plan(CpuContext context, float deltaTime)
        {
            var settings = CpuMoveSettings.Load();
            var turret = context.Turret;
            if (turret == null || !TurretMoveHooks.CanMove(context.Player))
            {
                _target = Vector2.zero;
                _current = Vector2.zero;
                return Vector2.zero;
            }

            var position = (Vector2)turret.transform.position;
            var sight = context.Eyes.Perceived;

            _planTimer -= deltaTime;
            if (_planTimer <= 0f)
            {
                _planTimer = settings.planInterval;
                var threat = CpuMoveMath.Threat(sight, settings.outerRingShare, out var count);
                var hasHome = TryGetHome(context.Player, turret, position, out var home);
                _target = context.Eyes.HasSeen ? CpuMoveMath.Plan(position, hasHome, home, threat, count, CpuMoveTuning.From(settings)) : Vector2.zero;

                if (sight.RingCount > 0)
                {
                    var inner = 0f;
                    for (var s = 0; s < sight.SectorCount; s++)
                    {
                        inner += sight.RingWeights[s * sight.RingCount];
                    }

                    if (inner >= settings.surroundedCount)
                    {
                        context.Stats.Add(SurroundedSeconds, settings.planInterval);
                    }
                }
            }

            _current = Vector2.Lerp(_current, _target, 1f - Mathf.Exp(-settings.turnResponse * deltaTime));
            if (_current.magnitude > 0.2f)
            {
                context.Stats.Add(MovedSeconds, deltaTime);
            }

            return _current;
        }

        public void Reset()
        {
            _target = Vector2.zero;
            _current = Vector2.zero;
            _planTimer = 0f;
            _hasFirstSeen = false;
            _firstSeenTurret = null;
        }

        private bool TryGetHome(int player, LaserTurret turret, Vector2 position, out Vector2 home)
        {
            if (TurretMoveHooks.TryGetHome(player, out home))
            {
                return true;
            }

            if (!_hasFirstSeen || _firstSeenTurret != turret)
            {
                _hasFirstSeen = true;
                _firstSeenTurret = turret;
                _firstSeen = position;
            }

            home = _firstSeen;
            return true;
        }
    }
}
