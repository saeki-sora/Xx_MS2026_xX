using System.Globalization;

namespace MS2026.GripInputBridge.Transports
{
    /// <summary>
    /// 設計書 6.2 のシリアル通信プロトコルのパーサー。
    /// フォーマット: <c>G,&lt;player_index 0-3&gt;,&lt;raw_value 0-4095&gt;,&lt;device_timestamp_ms&gt;</c>
    /// 例: <c>G,0,2048,183920</c>
    ///
    /// SerialPort等のI/Oに一切依存しない純粋な文字列処理として切り出してあるため、
    /// 実機・仮想COMポートが無くても Edit Mode テストでロジックを検証できる。
    /// </summary>
    public static class GripSerialProtocol
    {
        /// <summary>設計書が前提とする12bit ADCの最大値。</summary>
        public const int RawValueMax = 4095;

        public readonly struct ParsedSample
        {
            public readonly int PlayerIndex;
            public readonly float NormalizedRawValue;
            public readonly long DeviceTimestampMs;

            public ParsedSample(int playerIndex, float normalizedRawValue, long deviceTimestampMs)
            {
                PlayerIndex = playerIndex;
                NormalizedRawValue = normalizedRawValue;
                DeviceTimestampMs = deviceTimestampMs;
            }
        }

        /// <summary>
        /// 1行分の受信データをパースする。フォーマット不正・範囲外の値は例外を投げず false を返す
        /// （ノイズで1行だけ化けても通信全体を落とさないため）。
        /// </summary>
        public static bool TryParseLine(string line, out ParsedSample sample)
        {
            sample = default;

            if (string.IsNullOrEmpty(line))
            {
                return false;
            }

            var tokens = line.Split(',');
            if (tokens.Length != 4 || tokens[0] != "G")
            {
                return false;
            }

            if (!int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var playerIndex)
                || playerIndex < 0 || playerIndex >= GripInputBridgeConstants.PlayerCount)
            {
                return false;
            }

            if (!int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rawAdcValue)
                || rawAdcValue < 0 || rawAdcValue > RawValueMax)
            {
                return false;
            }

            if (!long.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestampMs))
            {
                return false;
            }

            var normalized = (float)rawAdcValue / RawValueMax;
            sample = new ParsedSample(playerIndex, normalized, timestampMs);
            return true;
        }

        /// <summary>
        /// テスト・ダミー送信スクリプト向け: パラメータから実際に送信する1行を組み立てる
        /// （<see cref="TryParseLine"/> のラウンドトリップ検証や、実機無しでのプロトコル確認に使う）。
        /// </summary>
        public static string BuildLine(int playerIndex, int rawAdcValue, long deviceTimestampMs)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "G,{0},{1},{2}",
                playerIndex,
                rawAdcValue,
                deviceTimestampMs);
        }
    }
}
