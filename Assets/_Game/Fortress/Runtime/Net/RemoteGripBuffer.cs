namespace MS2026.Fortress.Net
{
    /// <summary>
    /// Host側で、各Clientから届いた最新の握力を保持する(Unity API非依存・EditModeテスト対象)。
    /// 握力は取りこぼしても次がすぐ来るUnreliable送信なので、届く順番が入れ替わることがある。
    /// 連番(sequence)で古い値を捨て、一定時間届かなければ「離した」とみなす(切断・フリーズ対策)。
    /// </summary>
    public sealed class RemoteGripBuffer
    {
        private struct Entry
        {
            public bool HasValue;
            public bool IsGripping;
            public float Grip01;
            public uint Sequence;
            public double ReceivedTime;
        }

        private readonly Entry[] _entries;

        public RemoteGripBuffer(int playerCount)
        {
            _entries = new Entry[playerCount];
        }

        /// <summary>届いた値を記録する。古い連番なら捨ててfalseを返す。</summary>
        public bool Submit(int playerIndex, bool isGripping, float grip01, uint sequence, double now, double timeoutSeconds)
        {
            if (playerIndex < 0 || playerIndex >= _entries.Length)
            {
                return false;
            }

            ref var entry = ref _entries[playerIndex];

            // 途切れていた(再接続でClientの連番が1から振り直された等)なら連番に関わらず受け入れる。
            var isFresh = entry.HasValue && now - entry.ReceivedTime <= timeoutSeconds;
            if (isFresh && !IsNewer(sequence, entry.Sequence))
            {
                return false;
            }

            entry.HasValue = true;
            entry.IsGripping = isGripping;
            entry.Grip01 = grip01 < 0f ? 0f : grip01 > 1f ? 1f : grip01;
            entry.Sequence = sequence;
            entry.ReceivedTime = now;
            return true;
        }

        /// <summary>最新の値を返す。一度も届いていない・途切れている場合は「握っていない」を返す。</summary>
        public void Get(int playerIndex, double now, double timeoutSeconds, out bool isGripping, out float grip01)
        {
            isGripping = false;
            grip01 = 0f;

            if (playerIndex < 0 || playerIndex >= _entries.Length)
            {
                return;
            }

            var entry = _entries[playerIndex];
            if (!entry.HasValue || now - entry.ReceivedTime > timeoutSeconds)
            {
                return;
            }

            isGripping = entry.IsGripping;
            grip01 = entry.Grip01;
        }

        public void Clear(int playerIndex)
        {
            if (playerIndex >= 0 && playerIndex < _entries.Length)
            {
                _entries[playerIndex] = default;
            }
        }

        public void ClearAll()
        {
            for (var i = 0; i < _entries.Length; i++)
            {
                _entries[i] = default;
            }
        }

        // uintの一周(4,294,967,295の次が0)をまたいでも正しく新旧を比べる(シリアル番号算術)。
        private static bool IsNewer(uint candidate, uint current)
        {
            return (int)(candidate - current) > 0;
        }
    }
}
