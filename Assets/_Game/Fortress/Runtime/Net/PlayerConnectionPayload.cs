namespace MS2026.Fortress.Net
{
    /// <summary>
    /// NetworkManager.NetworkConfig.ConnectionData に載せる最小限のペイロード(セッショントークン + PlayerIndex)。
    /// D-Driveはこの中身に関与しないため、ゲーム側でエンコード/デコードを持つ([14_networking.md] §12)。
    /// </summary>
    public static class PlayerConnectionPayload
    {
        // constのままだと定数式は常にcheckedコンテキストで評価され、下のbyteへの縮小キャストがコンパイルエラーになるためstatic readonlyにする。
        private static readonly uint SessionToken = 0x4D533230; // "MS20" の簡易マーカー。別ゲームからの誤接続を弾く程度の用途。
        private const int PayloadLength = 5;

        public static byte[] Encode(int playerIndex)
        {
            return new[]
            {
                (byte)(SessionToken >> 24),
                (byte)(SessionToken >> 16),
                (byte)(SessionToken >> 8),
                (byte)SessionToken,
                (byte)playerIndex
            };
        }

        public static bool TryDecode(byte[] payload, out int playerIndex)
        {
            playerIndex = -1;

            if (payload == null || payload.Length != PayloadLength)
            {
                return false;
            }

            var token = (uint)((payload[0] << 24) | (payload[1] << 16) | (payload[2] << 8) | payload[3]);
            if (token != SessionToken)
            {
                return false;
            }

            var candidate = payload[4];
            if (candidate > 3)
            {
                return false;
            }

            playerIndex = candidate;
            return true;
        }
    }
}
