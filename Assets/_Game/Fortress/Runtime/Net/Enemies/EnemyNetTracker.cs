using System.Collections.Generic;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Host側で、Actorの敵に通信用の番号を振り、出現/消滅のイベントを起きた順に貯める(Unity API非依存・EditModeテスト対象)。
    /// 敵は生成された瞬間にはまだ種類が設定されていない(生成直後に設定される)ため、出現はいったん「保留」にして、
    /// 次に<see cref="ResolvePending"/>を呼んだとき(送信の直前)に番号を振る。保留中に消えた敵は、最初から無かったことにする。
    /// </summary>
    public sealed class EnemyNetTracker<T> where T : class
    {
        /// <summary>敵の種類の番号と位置を答える。同期できない(種類に番号が無い等)ならfalse。</summary>
        public delegate bool Describe(T item, out ushort typeIndex, out float x, out float y);

        private readonly List<T> _pending = new();
        private readonly Dictionary<T, uint> _ids = new();
        private readonly List<EnemyNetEvent> _events = new();
        private uint _nextId = 1;

        /// <summary>番号を振った(Clientに出現を伝えた)敵。</summary>
        public IReadOnlyDictionary<T, uint> Ids => _ids;

        public int PendingCount => _pending.Count;

        public void OnAdded(T item)
        {
            if (!_ids.ContainsKey(item) && !_pending.Contains(item))
            {
                _pending.Add(item);
            }
        }

        public void OnRemoved(T item, byte reason)
        {
            if (_pending.Remove(item))
            {
                return;
            }

            if (_ids.TryGetValue(item, out var id))
            {
                _ids.Remove(item);
                _events.Add(new EnemyNetEvent { Id = id, Kind = EnemyNetEventKind.Despawn, Reason = reason });
            }
        }

        /// <summary>保留中の敵に番号を振り、出現イベントにする。同期できない敵は追跡しない。追跡できなかった数を返す。</summary>
        public int ResolvePending(Describe describe)
        {
            var unsupported = 0;
            foreach (var item in _pending)
            {
                if (!describe(item, out var typeIndex, out var x, out var y))
                {
                    unsupported++;
                    continue;
                }

                var id = _nextId++;
                _ids[item] = id;
                _events.Add(new EnemyNetEvent { Id = id, Kind = EnemyNetEventKind.Spawn, TypeIndex = typeIndex, X = x, Y = y });
            }

            _pending.Clear();
            return unsupported;
        }

        /// <summary>貯まったイベントを取り出して空にする。無ければnull。</summary>
        public EnemyNetEvent[] Drain()
        {
            if (_events.Count == 0)
            {
                return null;
            }

            var result = _events.ToArray();
            _events.Clear();
            return result;
        }

        /// <summary>イベントだけ捨てる(送り先のClientがいないとき)。番号は残すので、後から来たClientには全体の状態で伝わる。</summary>
        public void DiscardEvents()
        {
            _events.Clear();
        }

        public void Clear()
        {
            _pending.Clear();
            _ids.Clear();
            _events.Clear();
        }
    }
}
