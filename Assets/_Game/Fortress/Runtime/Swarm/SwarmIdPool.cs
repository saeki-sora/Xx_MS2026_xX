using System.Collections.Generic;

namespace MS2026.Fortress
{
    /// <summary>
    /// 群衆の敵に振る通信用の番号(0〜size-1)の払い出し(Unity API非依存・EditModeテスト対象)。
    /// 返された番号は「一番古く返された物から」再利用する。消えた敵の番号がすぐ別の敵に使われると、
    /// 遅れて届いた古い位置の補正が新しい敵にかかってしまうため、できるだけ間を空ける。
    /// </summary>
    public sealed class SwarmIdPool
    {
        private readonly Queue<int> _free;
        private readonly int _size;

        public SwarmIdPool(int size)
        {
            _size = size;
            _free = new Queue<int>(size);
            Reset();
        }

        public int AvailableCount => _free.Count;

        public bool TryAllocate(out int id)
        {
            if (_free.Count == 0)
            {
                id = -1;
                return false;
            }

            id = _free.Dequeue();
            return true;
        }

        public void Release(int id)
        {
            if (id >= 0 && id < _size)
            {
                _free.Enqueue(id);
            }
        }

        /// <summary>全ての番号を未使用に戻す。</summary>
        public void Reset()
        {
            _free.Clear();
            for (var i = 0; i < _size; i++)
            {
                _free.Enqueue(i);
            }
        }
    }
}
