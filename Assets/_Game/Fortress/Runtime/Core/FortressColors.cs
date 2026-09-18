using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// プレイヤーごとの識別色。GripInputBridgeWindow(設計書9.1)と同じ配色を流用し、
    /// 入力ツールとゲーム本体で見た目の印象を統一する。
    /// </summary>
    public static class FortressColors
    {
        private static readonly Color[] PlayerColors =
        {
            new Color(0.898f, 0.282f, 0.302f), // P1 赤
            new Color(0.203f, 0.549f, 0.949f), // P2 青
            new Color(0.250f, 0.702f, 0.450f), // P3 緑
            new Color(0.949f, 0.800f, 0.153f)  // P4 黄
        };

        public static Color PlayerColor(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= PlayerColors.Length)
            {
                return Color.white;
            }

            return PlayerColors[playerIndex];
        }
    }
}
