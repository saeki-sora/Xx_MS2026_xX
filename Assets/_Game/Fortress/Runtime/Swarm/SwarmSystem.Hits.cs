using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// レーザーが群衆の敵に「当たった瞬間」の記録（演出用）。記録はレーザー判定ジョブ（<see cref="SwarmLaserJob"/>）が行い、
    /// ジョブの回収時に <see cref="BeamHitsReported"/> で配る。購読者がいないときは記録しない。
    /// 数は <see cref="SwarmSettings.hitEffectsPerSecond"/>（全レーザー合計・1秒あたり）で間引く。
    /// ネット対戦のClient(レプリカ)でも、ダメージ0のビームで被弾フラッシュは計算しているので同じように記録される。
    /// </summary>
    public sealed partial class SwarmSystem
    {
        /// <summary>
        /// このフレームに記録された「当たった瞬間」の一覧（全レーザー分。<see cref="SwarmBeamHit.owner"/> で撃った人を区別する）。
        /// 一覧は次のフレームで使い回されるので、受け取った場で使い切ること。
        /// </summary>
        public event Action<IReadOnlyList<SwarmBeamHit>> BeamHitsReported;

        private readonly List<SwarmBeamHit> _reportedHits = new List<SwarmBeamHit>();
        private readonly HitEffectBudget _hitBudget = new HitEffectBudget(SwarmLimits.MaxHitReportsPerFrame);
        private NativeArray<SwarmBeamHit> _newHits;
        private NativeArray<int> _newHitCount;
        private int _hitBeamOffset;
        private bool _hitReportScheduled;

        private void InitializeHitReports()
        {
            _newHits = new NativeArray<SwarmBeamHit>(SwarmLimits.MaxHitReportsPerFrame, Allocator.Persistent);
            _newHitCount = new NativeArray<int>(1, Allocator.Persistent);
        }

        private void DisposeHitReports()
        {
            DisposeArray(ref _newHits);
            DisposeArray(ref _newHitCount);
        }

        /// <summary>毎フレーム、上限の枠を回復する（ジョブを組む前に呼ぶ）。</summary>
        private void RefillHitBudget()
        {
            _hitBudget.PerSecond = _settings.hitEffectsPerSecond;
            _hitBudget.Refill(Time.deltaTime);
        }

        /// <summary>このフレームのレーザー判定ジョブに記録してよい数を決める。0なら記録しない。</summary>
        private int PrepareHitReport()
        {
            _hitBeamOffset = (_hitBeamOffset + 1) & 0xFFFF;
            var max = BeamHitsReported != null ? Mathf.Min(_hitBudget.Available, _newHits.Length) : 0;
            _hitReportScheduled = max > 0;
            return max;
        }

        /// <summary>ジョブ回収後に記録を取り出して配る。</summary>
        private void CollectBeamHits()
        {
            if (!_hitReportScheduled)
            {
                return;
            }

            _hitReportScheduled = false;
            var count = Mathf.Min(_newHitCount[0], _newHits.Length);
            if (count <= 0)
            {
                return;
            }

            _reportedHits.Clear();
            for (var i = 0; i < count; i++)
            {
                _reportedHits.Add(_newHits[i]);
            }

            _hitBudget.Consume(count);
            BeamHitsReported?.Invoke(_reportedHits);
        }
    }
}
