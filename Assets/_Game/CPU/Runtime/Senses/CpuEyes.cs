using System;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の目が見た様子1回ぶん（その時刻の、砲台のまわりの敵の分布）。
    /// </summary>
    public sealed class CpuSight
    {
        public CpuSight(int sectorCount)
        {
            Weights = new float[sectorCount];
            ObstacleDistances = new float[sectorCount];
        }

        /// <summary>見た時刻（ゲームの時間）。</summary>
        public float Time;

        /// <summary>見るたびに増える番号（0 = まだ何も見ていない）。</summary>
        public int Stamp;

        /// <summary>扇ごとの敵の重み（数。コアに近い敵は多めに数える）。角度は <see cref="CpuSectorMath"/> の決まり。</summary>
        public readonly float[] Weights;

        /// <summary>扇ごとの、ビームが壁などで止まる距離（止まらなければ射程）。</summary>
        public readonly float[] ObstacleDistances;

        /// <summary>見えていた敵の合計（重み）。</summary>
        public float Total;

        /// <summary>画面に映る範囲を使ったか（使えなかったときは射程の中を全部見ている）。</summary>
        public bool HasView;

        public CpuView View;

        internal void CopyFrom(CpuSight other)
        {
            Time = other.Time;
            Stamp = other.Stamp;
            Array.Copy(other.Weights, Weights, Weights.Length);
            Array.Copy(other.ObstacleDistances, ObstacleDistances, ObstacleDistances.Length);
            Total = other.Total;
            HasView = other.HasView;
            View = other.View;
        }
    }

    /// <summary>
    /// CPU の目。人と同じ条件で見る:
    /// ・その席のプレイヤーの画面に映る範囲（<see cref="ICpuViewSource"/>。無ければ席のカメラの決まった視点）の中だけ
    /// ・ビームが届く所（射程の中、壁などより手前。ビームと同じ当たり判定で確かめる）だけ
    /// ・見えた様子は、上手さの「反応」の秒数だけ遅れて頭に届く（<see cref="Perceived"/>）
    /// ・敵の数の見積もりは少し外れる（上手さの「見積もりの外れ」）
    /// </summary>
    public sealed class CpuEyes
    {
        private const int HistoryLength = 24;
        private const float CoreBonusRadius = 8f;
        private const float CoreBonus = 1.5f;
        private const int CoreRefreshFrames = 120;

        private static readonly RaycastHit2D[] HitBuffer = new RaycastHit2D[16];
        private static readonly CoreCrystalController[] NoCores = new CoreCrystalController[0];
        private static CoreCrystalController[] _cores = NoCores;
        private static int _coreRefreshFrame = -CoreRefreshFrames;

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
            Array.Clear(sight.Weights, 0, sight.Weights.Length);
            sight.Total = 0f;

            var origin = (Vector2)turret.MuzzlePosition;
            var range = turret.tuning != null ? turret.tuning.range : 20f;
            MeasureObstacles(turret, origin, range, sight.ObstacleDistances);

            sight.HasView = TryGetView(context.Player, out sight.View);

            RefreshCores();
            var positions = sampler.Positions;
            for (var i = 0; i < sampler.Count; i++)
            {
                var point = positions[i];
                CpuSectorMath.Accumulate(sight.Weights, sight.ObstacleDistances, origin, range, sight.HasView, sight.View,
                    point, WeightOf(point));
            }

            // 見積もりの外れ（扇ごとに、見るたびに少し違う数に見える）。
            var noise = Mathf.Clamp01(context.Skill.judgementNoise);
            for (var s = 0; s < sight.Weights.Length; s++)
            {
                if (noise > 0f && sight.Weights[s] > 0f)
                {
                    sight.Weights[s] *= Mathf.Max(0f, 1f + (context.NextFloat() * 2f - 1f) * noise);
                }

                sight.Total += sight.Weights[s];
            }
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
