using System.Collections.Generic;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Host側で、群衆の出現/消滅を起きた順に貯め、1回に送る数の上限つきで少しずつ取り出す(Unity API非依存・EditModeテスト対象)。
    /// 3万体が一斉に湧いても、回線や送信待ちの領域をあふれさせないため。
    /// まだ送っていない出現の敵が消えたら、出現ごと取り消す(送る意味が無いため)。
    /// </summary>
    public sealed class SwarmNetEventQueue
    {
        private const int CompactThreshold = 4096;

        private readonly List<SwarmNetEvent> _events = new();

        // 番号 → まだ送っていない出現の位置。
        private readonly Dictionary<ushort, int> _unsentSpawns = new();
        private readonly List<ushort> _keyScratch = new();
        private int _head;

        /// <summary>まだ送っていない件数(取り消し済みを含む)。</summary>
        public int Count => _events.Count - _head;

        public void EnqueueSpawn(SwarmNetEvent spawn)
        {
            spawn.Kind = SwarmNetEventKind.Spawn;
            _unsentSpawns[spawn.Id] = _events.Count;
            _events.Add(spawn);
        }

        public void EnqueueDespawn(ushort id, byte reason)
        {
            if (_unsentSpawns.TryGetValue(id, out var position))
            {
                _unsentSpawns.Remove(id);
                var cancelled = _events[position];
                cancelled.Kind = SwarmNetEventKind.None;
                _events[position] = cancelled;
                return;
            }

            _events.Add(new SwarmNetEvent { Id = id, Kind = SwarmNetEventKind.Despawn, TypeOrReason = reason });
        }

        /// <summary>先頭から最大max件(取り消し済みは数えない)を取り出す。無ければnull。(テスト用。実際の送信はGCを出さない TakeInto を使う)</summary>
        public SwarmNetEvent[] Take(int max)
        {
            var taken = new List<SwarmNetEvent>(System.Math.Min(max, Count));
            TakeInto(taken, max);
            return taken.Count > 0 ? taken.ToArray() : null;
        }

        /// <summary>先頭から最大max件(取り消し済みは数えない)を output の末尾へ取り出し、取り出した数を返す。output は使い回せる(GCを出さない)。</summary>
        public int TakeInto(List<SwarmNetEvent> output, int max)
        {
            var taken = 0;
            while (_head < _events.Count && taken < max)
            {
                var e = _events[_head];
                if (e.Kind == SwarmNetEventKind.Spawn && _unsentSpawns.TryGetValue(e.Id, out var position) && position == _head)
                {
                    _unsentSpawns.Remove(e.Id);
                }

                _head++;
                if (e.Kind != SwarmNetEventKind.None)
                {
                    output.Add(e);
                    taken++;
                }
            }

            Compact();
            return taken;
        }

        public void Clear()
        {
            _events.Clear();
            _unsentSpawns.Clear();
            _head = 0;
        }

        // 送り終えた分を捨てる(全部送り終えたとき、または溜まりすぎたとき)。
        private void Compact()
        {
            if (_head == _events.Count)
            {
                Clear();
                return;
            }

            if (_head < CompactThreshold)
            {
                return;
            }

            _events.RemoveRange(0, _head);
            _keyScratch.Clear();
            foreach (var key in _unsentSpawns.Keys)
            {
                _keyScratch.Add(key);
            }

            foreach (var key in _keyScratch)
            {
                _unsentSpawns[key] -= _head;
            }

            _head = 0;
        }
    }
}
