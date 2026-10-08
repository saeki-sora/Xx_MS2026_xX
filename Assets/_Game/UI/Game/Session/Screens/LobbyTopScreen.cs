using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ロビーの最初の画面: 名前の欄、P1〜P4 を選ぶボタン、「ホストになる」「参加する」「タイトルへ」。
    /// 選んでいる番号の印・握力のゲージ・このPCのアドレスは値の部品（lobby.selectedPlayer / local.grip / lobby.address）で出す。
    /// </summary>
    [AddComponentMenu("UI Studio/ロビー/最初の画面 (LobbyTopScreen)")]
    public sealed class LobbyTopScreen : SessionScreenBase
    {
        [Tooltip("名前の入力欄（空ならP1などになる）。")]
        public InputField nameField;

        [Tooltip("P1〜P4 を選ぶボタン（順番どおり4つ）。")]
        public Button[] seatButtons = new Button[4];

        [Tooltip("部屋を作る（ホストになる）ボタン。")]
        public Button hostButton;

        [Tooltip("部屋を探して参加する画面へ進むボタン。")]
        public Button joinButton;

        [Tooltip("タイトルへ戻るボタン。")]
        public Button titleButton;

        private void Awake()
        {
            for (var i = 0; i < seatButtons.Length; i++)
            {
                var seat = i;
                Bind(seatButtons[i], s => s.SelectSeat(seat));
            }

            Bind(hostButton, s =>
            {
                ApplyName(s);
                s.Host();
            });
            Bind(joinButton, s =>
            {
                ApplyName(s);
                s.OpenJoin();
            });
            Bind(titleButton, s => s.GoToTitle());
            if (nameField != null)
            {
                nameField.characterLimit = LobbyRoster.MaxNameLength;
                nameField.onEndEdit.AddListener(text => Session?.SetLocalName(text));
            }
        }

        private void OnEnable()
        {
            if (nameField != null && Session != null)
            {
                nameField.SetTextWithoutNotify(Session.LocalName);
            }
        }

        private void ApplyName(GameSession session)
        {
            if (nameField != null)
            {
                session.SetLocalName(nameField.text);
            }
        }
    }
}
