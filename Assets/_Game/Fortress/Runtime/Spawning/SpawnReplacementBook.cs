using System.Collections.Generic;

namespace MS2026.Fortress
{
    /// <summary>
    /// 「ほかの所（観客の菌など）が代わりに出した敵」の帳簿。ウェーブがこれから出す予定の敵を、その数だけ出さずに済ませる。
    /// 種類ごとに数え、同じ種類の敵が出る番になったら 1 体ずつ打ち消す。ウェーブの敵の総数（＝難しさ）を変えずに、
    /// どこから・いつ出るかだけを入れ替えるためのもの。Unity に頼らない（テストできる）。
    /// </summary>
    public sealed class SpawnReplacementBook<TType> where TType : class
    {
        private readonly Dictionary<TType, int> _pending = new Dictionary<TType, int>();

        /// <summary>まだ打ち消していない数（全部の種類の合計）。</summary>
        public int Pending { get; private set; }

        /// <summary>これまでに打ち消した数（<see cref="Clear"/> で 0 に戻る）。</summary>
        public int Replaced { get; private set; }

        /// <summary>count 体ぶん、この種類の出る予定を減らす。</summary>
        public void Add(TType type, int count)
        {
            if (type == null || count <= 0)
            {
                return;
            }

            _pending.TryGetValue(type, out var current);
            _pending[type] = current + count;
            Pending += count;
        }

        /// <summary>この種類の敵が出る番。打ち消す分が残っていれば 1 減らして true（その敵は出さない）。</summary>
        public bool TryConsume(TType type)
        {
            if (type == null || !_pending.TryGetValue(type, out var current) || current <= 0)
            {
                return false;
            }

            if (current == 1)
            {
                _pending.Remove(type);
            }
            else
            {
                _pending[type] = current - 1;
            }

            Pending--;
            Replaced++;
            return true;
        }

        public int PendingOf(TType type) => type != null && _pending.TryGetValue(type, out var current) ? current : 0;

        /// <summary>新しい試合。打ち消す予定も、打ち消した数も忘れる。</summary>
        public void Clear()
        {
            _pending.Clear();
            Pending = 0;
            Replaced = 0;
        }
    }
}
