using System.Collections.Generic;

namespace MS2026.Spectator
{
    /// <summary>Host が配る、観客の受付に要る数（上限の残り・ボスのたまり）。Host でない PC はこれで断るかを決め、スマホに見せる。</summary>
    public struct SpectatorHostStatus
    {
        public int Allowance;
        public int Pool;
        public int PoolMax;
        public int BossesLeft;
        public int BossesDone;
    }

    /// <summary>
    /// Host でない PC が Host に取り次いだ「送る」の控え（番号 → スマホの id）。
    /// Host から「出せなかった」と返ってきたとき・返事が来ないまま時間が過ぎたときに、その人の待ち時間を返すために使う。
    /// メインスレッドだけで使う。Unity に頼らない。
    /// </summary>
    public sealed class SpectatorForwardBook
    {
        private struct Entry
        {
            public string SpectatorId;
            public double SentAt;
        }

        private readonly Dictionary<uint, Entry> _pending = new Dictionary<uint, Entry>();
        private readonly List<uint> _expired = new List<uint>();
        private uint _next = 1;

        public int Count => _pending.Count;

        /// <summary>控えを作り、取り次ぎの番号を返す（0 は使わない）。</summary>
        public uint Add(string spectatorId, double now)
        {
            var seq = _next++;
            if (_next == 0)
            {
                _next = 1;
            }

            _pending[seq] = new Entry { SpectatorId = spectatorId, SentAt = now };
            return seq;
        }

        /// <summary>返事が来た。控えを消し、その人の id を返す（知らない番号なら false）。</summary>
        public bool TryComplete(uint seq, out string spectatorId)
        {
            spectatorId = null;
            if (!_pending.TryGetValue(seq, out var entry))
            {
                return false;
            }

            _pending.Remove(seq);
            spectatorId = entry.SpectatorId;
            return true;
        }

        /// <summary>timeout 秒たっても返事が無い控えを消し、その人たちの id を into に入れる。</summary>
        public void Expire(double now, double timeout, List<string> into)
        {
            into.Clear();
            _expired.Clear();
            foreach (var pair in _pending)
            {
                if (now - pair.Value.SentAt > timeout)
                {
                    _expired.Add(pair.Key);
                    into.Add(pair.Value.SpectatorId);
                }
            }

            foreach (var seq in _expired)
            {
                _pending.Remove(seq);
            }
        }

        public void Clear() => _pending.Clear();
    }
}
