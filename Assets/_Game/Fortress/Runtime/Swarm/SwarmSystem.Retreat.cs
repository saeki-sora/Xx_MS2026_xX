using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの目標を倒した後の「撤退」（2026-10-24）。
    /// ・全員のPC: <see cref="SetRetreating"/> の間、ビームが当たらず、コアに着いても何もせず、張り付いていた敵も離れる
    /// ・Host: <see cref="SetRetreatField"/> で逃げる道（画面の外へ）と速さを入れ、<see cref="RequestRemove"/> で画面の外へ出た敵を消す（撃破数に入らない）
    /// 消した敵は「消えた」として Client へ届くので、通信は増えない。
    /// </summary>
    public sealed partial class SwarmSystem
    {
        private NativeArray<float2> _retreatDirs;
        private float2[] _pendingRetreatDirs;
        private bool _retreatDirsDirty;
        private int _retreatHasField;
        private float _retreatSpeed = 1f;

        /// <summary>撤退中か（全員のPC）。</summary>
        public bool Retreating { get; private set; }

        /// <summary>撤退を始める・やめる（全員のPC）。やめると、逃げる道も消える。</summary>
        public void SetRetreating(bool on)
        {
            Retreating = on;
            if (!on)
            {
                _pendingRetreatDirs = null;
                _retreatDirsDirty = true;
                _retreatSpeed = 1f;
            }
        }

        /// <summary>
        /// Host: 逃げる道（経路のマップと同じマス目で、マスごとの向き。null ならコアの反対へまっすぐ）と速さの倍率。
        /// 中身は次にジョブを組むときに写す（今動いているジョブとぶつからない）。
        /// </summary>
        public void SetRetreatField(FlowFieldResult field, float speedMultiplier)
        {
            _retreatSpeed = math.max(0.1f, speedMultiplier);
            _retreatDirsDirty = true;
            if (field == null || !Navigation.IsCreated || field.Grid.Width != Navigation.Width || field.Grid.Height != Navigation.Height)
            {
                _pendingRetreatDirs = null;
                return;
            }

            var width = Navigation.Width;
            var dirs = new float2[Navigation.CellCount];
            for (var y = 0; y < Navigation.Height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var d = field.DirectionAtCell(x, y);
                    dirs[y * width + x] = new float2(d.x, d.y);
                }
            }

            _pendingRetreatDirs = dirs;
        }

        /// <summary>Host: この番号の敵を消す（撃破数に入らない）。<see cref="StorageReadable"/> の中で呼ぶと、そのフレームに消える。</summary>
        public void RequestRemove(int netId)
        {
            if (!IsReplica && netId >= 0)
            {
                _pendingRemovals.Add(netId);
            }
        }

        /// <summary>Host: いくつかまとめて消す。</summary>
        public void RequestRemove(IReadOnlyList<int> netIds)
        {
            for (var i = 0; i < netIds.Count; i++)
            {
                RequestRemove(netIds[i]);
            }
        }

        // ジョブに渡す「逃げる道」を用意する（ジョブが動いていない間に呼ぶ）。道が無いときも、ジョブには1つ分の空の配列を渡す。
        private void PrepareRetreatJobInputs()
        {
            if (!_retreatDirs.IsCreated)
            {
                _retreatDirs = new NativeArray<float2>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            }

            if (!_retreatDirsDirty)
            {
                return;
            }

            _retreatDirsDirty = false;
            var dirs = _pendingRetreatDirs;
            if (dirs == null)
            {
                _retreatHasField = 0;
                return;
            }

            if (_retreatDirs.Length != dirs.Length)
            {
                _retreatDirs.Dispose();
                _retreatDirs = new NativeArray<float2>(dirs.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            _retreatDirs.CopyFrom(dirs);
            _retreatHasField = 1;
        }

        private void DisposeRetreat()
        {
            if (_retreatDirs.IsCreated)
            {
                _retreatDirs.Dispose();
            }
        }
    }
}
