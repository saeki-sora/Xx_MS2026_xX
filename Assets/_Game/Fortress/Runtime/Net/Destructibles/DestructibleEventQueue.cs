using System.Collections.Generic;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Host側で、破壊可能物の変化を「起きた順」に貯めて、まとめて送るためのキュー(Unity API非依存・EditModeテスト対象)。
    /// レーザーのダメージは毎フレーム起きるため、同じ物への連続したダメージ/耐久変化は1件にまとめる
    /// (間に破壊・再生など別の出来事が挟まったら、まとめずに新しい件にする=順番は崩さない)。
    /// </summary>
    public sealed class DestructibleEventQueue
    {
        private readonly List<DestructibleNetEvent> _events = new();

        // 番号 → まだまとめられる(Damage/Healthの)件の位置。
        private readonly Dictionary<int, int> _openEntries = new();

        /// <summary>送る件数(取り消し済みを含む)。</summary>
        public int Count => _events.Count;

        public void OnDamaged(int index, float amount, byte source, sbyte attacker, float health)
        {
            if (_openEntries.TryGetValue(index, out var position))
            {
                var entry = _events[position];
                entry.Kind = DestructibleNetEventKind.Damage;
                entry.Amount += amount;
                entry.Source = source;
                if (attacker >= 0)
                {
                    entry.Attacker = attacker;
                }

                entry.Health = health;
                _events[position] = entry;
                return;
            }

            _openEntries[index] = _events.Count;
            _events.Add(new DestructibleNetEvent
            {
                Index = (ushort)index,
                Kind = DestructibleNetEventKind.Damage,
                Amount = amount,
                Source = source,
                Attacker = attacker,
                Health = health
            });
        }

        public void OnHealthChanged(int index, float health)
        {
            if (_openEntries.TryGetValue(index, out var position))
            {
                var entry = _events[position];
                entry.Health = health;
                _events[position] = entry;
                return;
            }

            _openEntries[index] = _events.Count;
            _events.Add(new DestructibleNetEvent
            {
                Index = (ushort)index,
                Kind = DestructibleNetEventKind.Health,
                Attacker = -1,
                Health = health
            });
        }

        public void OnDestroyed(int index, byte source, sbyte attacker)
        {
            _openEntries.Remove(index);
            _events.Add(new DestructibleNetEvent
            {
                Index = (ushort)index,
                Kind = DestructibleNetEventKind.Destroy,
                Source = source,
                Attacker = attacker
            });
        }

        public void OnRegenerated(int index)
        {
            // 再生の直前に出る「耐久の変化」は、再生そのものに含まれるので送らない
            // (送ると、Client側で壊れたままの物の耐久が一瞬戻って見える)。
            if (_openEntries.TryGetValue(index, out var position) && _events[position].Kind == DestructibleNetEventKind.Health)
            {
                var entry = _events[position];
                entry.Kind = DestructibleNetEventKind.None;
                _events[position] = entry;
            }

            _openEntries.Remove(index);
            _events.Add(new DestructibleNetEvent
            {
                Index = (ushort)index,
                Kind = DestructibleNetEventKind.Regenerate,
                Attacker = -1
            });
        }

        public void OnInvulnerabilityChanged(int index, bool isInvulnerable)
        {
            AddFlagEvent(index, DestructibleNetEventKind.Invulnerable, isInvulnerable);
        }

        public void OnActiveChanged(int index, bool isActive)
        {
            AddFlagEvent(index, DestructibleNetEventKind.Active, isActive);
        }

        /// <summary>貯まった件を取り出して空にする(取り消し済みは除く)。無ければnull。</summary>
        public DestructibleNetEvent[] Drain()
        {
            var count = 0;
            foreach (var e in _events)
            {
                if (e.Kind != DestructibleNetEventKind.None)
                {
                    count++;
                }
            }

            DestructibleNetEvent[] result = null;
            if (count > 0)
            {
                result = new DestructibleNetEvent[count];
                var i = 0;
                foreach (var e in _events)
                {
                    if (e.Kind != DestructibleNetEventKind.None)
                    {
                        result[i++] = e;
                    }
                }
            }

            Clear();
            return result;
        }

        public void Clear()
        {
            _events.Clear();
            _openEntries.Clear();
        }

        private void AddFlagEvent(int index, DestructibleNetEventKind kind, bool flag)
        {
            _openEntries.Remove(index);
            _events.Add(new DestructibleNetEvent
            {
                Index = (ushort)index,
                Kind = kind,
                Attacker = -1,
                Flag = flag
            });
        }
    }
}
