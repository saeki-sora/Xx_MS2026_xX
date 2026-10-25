using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// プレイヤーごとの識別色。GripInputBridgeWindow(設計書9.1)と同じ配色を流用し、
    /// 入力ツールとゲーム本体で見た目の印象を統一する。
    /// キャラ選択を通ってきたら、その人が選んだキャラの色になる（<see cref="ChosenColor"/>。席の色のままにしたい所は <see cref="SeatColor"/>）。
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

        /// <summary>
        /// その席の人が選んだキャラの色（選んでいなければ null）。キャラ選択の側が入れる（このアセンブリはキャラ選択を知らないので）。
        /// </summary>
        public static Func<int, Color?> ChosenColor;

        /// <summary>その人の色。キャラを選んでいればキャラの色、選んでいなければ席の色。</summary>
        public static Color PlayerColor(int playerIndex)
        {
            var chosen = ChosenColor?.Invoke(playerIndex);
            return chosen ?? SeatColor(playerIndex);
        }

        /// <summary>席の色（P1 赤・P2 青・P3 緑・P4 黄）。キャラ選択の画面など、席そのものを表す所で使う。</summary>
        public static Color SeatColor(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= PlayerColors.Length)
            {
                return Color.white;
            }

            return PlayerColors[playerIndex];
        }
    }
}
