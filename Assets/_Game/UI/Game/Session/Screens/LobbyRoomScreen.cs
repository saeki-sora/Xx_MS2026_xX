using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 部屋の画面: 4人の席（名前・準備OK・ホストの印・通信の遅れは値の部品で出す）、準備OK、ゲーム開始、待たずに開始、
    /// 開始をやめる、部屋を出る、席ごとの「外す」（ホストだけ）。
    /// 「ゲーム開始」を押せるかどうかは値の部品（lobby.canStart）で切り替える。
    /// </summary>
    [AddComponentMenu("UI Studio/ロビー/部屋の画面 (LobbyRoomScreen)")]
    public sealed class LobbyRoomScreen : SessionScreenBase
    {
        [Tooltip("準備OKを切り替えるボタン（握り続けても切り替わる）。")]
        public Button readyButton;

        [Tooltip("ゲーム開始ボタン（ホストだけ。全員が準備OKのとき押せる）。")]
        public Button startButton;

        [Tooltip("準備OKを待たずに開始するボタン（ホストだけ）。")]
        public Button forceStartButton;

        [Tooltip("カウントダウンをやめるボタン（ホストだけ）。")]
        public Button cancelButton;

        [Tooltip("部屋を出るボタン（ホストなら部屋を閉じる）。")]
        public Button leaveButton;

        [Tooltip("席ごとの「外す」ボタン（P1〜P4 の順。ホストにだけ、ほかの人の席に出る）。")]
        public Button[] kickButtons = new Button[4];

        private void Awake()
        {
            Bind(readyButton, s => s.ToggleReady());
            Bind(startButton, s => s.StartMatch());
            Bind(forceStartButton, s => s.StartMatch(true));
            Bind(cancelButton, s => s.CancelCountdown());
            Bind(leaveButton, s => s.LeaveRoom());
            for (var i = 0; i < kickButtons.Length; i++)
            {
                var seat = i;
                Bind(kickButtons[i], s => s.Kick(seat));
            }
        }

        private void Update()
        {
            var session = Session;
            for (var i = 0; i < kickButtons.Length; i++)
            {
                var kick = kickButtons[i];
                if (kick == null)
                {
                    continue;
                }

                var show = session != null && session.IsHost && session.Roster.seats[i].present && !session.Roster.seats[i].isHost &&
                           session.Roster.phase == LobbyPhase.Waiting;
                if (kick.gameObject.activeSelf != show)
                {
                    kick.gameObject.SetActive(show);
                }
            }

            if (forceStartButton != null && session != null)
            {
                forceStartButton.gameObject.SetActive(session.IsHost && session.allowForceStart && session.Roster.phase == LobbyPhase.Waiting && !session.Roster.AllReady);
            }
        }
    }
}
