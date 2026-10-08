using UnityEngine.UI;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 試合中に Esc で出る一時メニュー（対戦中なのでゲームは止まらない）:
    /// つづける／全員でロビーに戻る（ホスト）／試合から抜ける（参加側）／タイトルへ。
    /// ホストだけ・参加側だけのボタンの出し分けは値の部品（lobby.isHost）で行う。
    /// </summary>
    [AddComponentMenu("UI Studio/ロビー/一時メニュー (PauseScreen)")]
    public sealed class PauseScreen : SessionScreenBase
    {
        [Tooltip("閉じて試合に戻るボタン。")]
        public Button resumeButton;

        [Tooltip("全員をロビー（部屋の画面）に戻すボタン（ホストだけ）。")]
        public Button lobbyButton;

        [Tooltip("自分だけ試合から抜けるボタン（参加側）。")]
        public Button leaveButton;

        [Tooltip("通信を終えてタイトルへ戻るボタン（ホストなら部屋が閉じる）。")]
        public Button titleButton;

        private void Awake()
        {
            Bind(resumeButton, s => UiRoot.Active?.Close(s.pauseScreen));
            Bind(lobbyButton, s => s.ReturnEveryoneToLobby());
            Bind(leaveButton, s => s.LeaveMatch());
            Bind(titleButton, s => s.GoToTitle());
        }
    }
}
