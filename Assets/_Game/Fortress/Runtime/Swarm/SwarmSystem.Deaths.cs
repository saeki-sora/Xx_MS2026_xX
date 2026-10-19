using System;
using System.Collections.Generic;
using Unity.Collections;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆の敵が倒されたことを、演出（倒した敵が光の粒になる <see cref="SwarmKillSparks"/> など）に知らせる。
    /// 詰め直しのジョブ（<see cref="SwarmCompactScanJob"/>）が、消える直前の位置と倒した人を記録する。
    /// Host・オフラインは体力0になった敵、ネット対戦の Client は Host から「倒された」と届いた敵（通信は増やしていない）。
    /// Client の「倒した人」は、自分のPCで見えていたビームから決める（<see cref="SwarmLaserJob.recordOwnerWithoutDamage"/>。見た目用で、撃破数には使わない）。
    /// </summary>
    public sealed partial class SwarmSystem
    {
        /// <summary>このフレームに倒された敵の一覧（演出用。一覧は次のフレームで使い回すので、受け取った場で使い切ること）。</summary>
        public event Action<IReadOnlyList<SwarmDeath>> AgentsDied;

        private readonly List<SwarmDeath> _reportedDeaths = new List<SwarmDeath>();
        private NativeList<SwarmDeath> _deaths;

        private void InitializeDeaths()
        {
            _deaths = new NativeList<SwarmDeath>(256, Allocator.Persistent);
        }

        private void DisposeDeaths()
        {
            if (_deaths.IsCreated)
            {
                _deaths.Dispose();
            }
        }

        /// <summary>ジョブの回収後に呼ぶ。</summary>
        private void CollectDeaths()
        {
            var count = _deaths.Length;
            if (count == 0 || AgentsDied == null)
            {
                return;
            }

            _reportedDeaths.Clear();
            for (var i = 0; i < count; i++)
            {
                _reportedDeaths.Add(_deaths[i]);
            }

            AgentsDied.Invoke(_reportedDeaths);
        }
    }
}
