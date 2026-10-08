using System;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 「部屋を選ぶ画面」の1行（見つかった部屋1つ）。部屋の名前・人数・状態・アドレスと、P1〜P4 の空き具合を出す。
    /// 文字の部品は空でもよい（あるものだけ書き換える）。
    /// </summary>
    [AddComponentMenu("UI Studio/ロビー/部屋の行 (LobbyHostRow)")]
    public sealed class LobbyHostRow : MonoBehaviour
    {
        [Tooltip("押すとこの部屋に参加するボタン。")]
        public Button button;

        [Tooltip("部屋の名前（ホストの名前）。")]
        public Text title;

        [Tooltip("人数と状態（例: 2/4人・準備中）。")]
        public Text info;

        [Tooltip("アドレス。")]
        public Text address;

        [Tooltip("ビルドが違うなどの注意。")]
        public Text warning;

        [Tooltip("P1〜P4 の席の印（使われていれば色、空いていれば薄く）。")]
        public Graphic[] seatMarks = new Graphic[4];

        [Tooltip("空いている席の印の色。")]
        public Color freeSeatColor = new Color(1f, 1f, 1f, 0.18f);

        public void Show(LanDiscovery.FoundHost host, int selectedSeat, Action onClick)
        {
            var reply = host.reply;
            if (title != null)
            {
                title.text = string.IsNullOrEmpty(reply.hostName) ? "（名前なし）" : $"{reply.hostName} の部屋";
            }

            if (info != null)
            {
                var phase = reply.phase switch
                {
                    LobbyPhase.InGame => "試合中（途中から参加）",
                    LobbyPhase.Countdown => "まもなく開始",
                    LobbyPhase.Loading => "読み込み中",
                    _ => "参加できます"
                };
                info.text = $"{reply.players}/4人・{phase}";
            }

            if (address != null)
            {
                address.text = host.address;
            }

            var full = reply.players >= LobbyRoster.SeatCount;
            if (warning != null)
            {
                warning.text = !host.sameBuild ? "ビルドが違います（同じビルドで遊んでください）"
                    : full ? "満員です"
                    : host.IsTaken(selectedSeat) ? $"P{selectedSeat + 1} は使われています（空いている番号で入ります）"
                    : string.Empty;
            }

            for (var i = 0; i < seatMarks.Length; i++)
            {
                if (seatMarks[i] != null)
                {
                    seatMarks[i].color = host.IsTaken(i) ? FortressColors.PlayerColor(i) : freeSeatColor;
                }
            }

            if (button != null)
            {
                button.interactable = !full;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick?.Invoke());
            }
        }
    }
}
