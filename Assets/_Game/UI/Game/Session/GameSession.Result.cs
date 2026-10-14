using System.Collections;
using MS2026.Fortress;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.UI.Game
{
    // リザルトのあと、全員でタイトルへ戻る（2026-10-13、ユーザー決定「リザルトの後はタイトルへ」）。
    // だれかが握り続けた／時間切れになった PC が ReturnToTitleFromResult を呼ぶ。参加側はホストに頼み、ホストが全員に「タイトルへ」を配ってから自分も戻る
    // （ホストが先に抜けると、参加側は「切れた」と思ってさがし直してしまうため、先に知らせる）。
    public sealed partial class GameSession
    {
        private const string ToTitleMessage = "MS2026.Lobby.ToTitle";
        private const string ToTitleRequestMessage = "MS2026.Lobby.ToTitleRequest";

        // ホストが「タイトルへ」を配ってから、自分が通信を止めるまでの秒数（知らせが届くように）。
        private const float ToTitleFlushSeconds = 0.3f;

        // 参加側がホストに頼んでも返事が来ないとき、自分だけで戻るまでの秒数。
        private const float ToTitleRequestTimeout = 3f;

        private bool _toTitleRequested;

        /// <summary>リザルトのあと、全員でタイトルへ戻る（オフラインなら自分だけ）。</summary>
        public void ReturnToTitleFromResult()
        {
            if (State == SessionState.Leaving)
            {
                return;
            }

            var manager = NetworkManager.Singleton;
            if (!IsOnline || manager == null)
            {
                GoToTitle();
                return;
            }

            if (manager.IsServer)
            {
                using (var writer = new FastBufferWriter(1, Allocator.Temp))
                {
                    manager.CustomMessagingManager.SendNamedMessageToAll(ToTitleMessage, writer, NetworkDelivery.ReliableSequenced);
                }

                StartCoroutine(GoToTitleAfter(ToTitleFlushSeconds));
                return;
            }

            if (_toTitleRequested)
            {
                return;
            }

            _toTitleRequested = true;
            using (var writer = new FastBufferWriter(1, Allocator.Temp))
            {
                manager.CustomMessagingManager.SendNamedMessage(ToTitleRequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
            }

            StartCoroutine(GoToTitleAfter(ToTitleRequestTimeout));
        }

        private IEnumerator GoToTitleAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (State != SessionState.Leaving)
            {
                _expectingDisconnect = true;
                GoToTitle();
            }
        }

        private void RegisterResultMessages(NetworkManager manager)
        {
            _toTitleRequested = false;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(ToTitleMessage, (sender, reader) =>
            {
                if (IsHost || State == SessionState.Leaving)
                {
                    return;
                }

                _expectingDisconnect = true;
                GoToTitle();
            });
            manager.CustomMessagingManager.RegisterNamedMessageHandler(ToTitleRequestMessage, (sender, reader) =>
            {
                // リザルトを見ている間の頼みだけ聞く（前の試合の頼みが遅れて届いても、キャラ選択やゲームの途中でタイトルへ戻らない）。
                if (IsHost && IsShowingResult)
                {
                    ReturnToTitleFromResult();
                }
            });
        }

        private static void UnregisterResultMessages(NetworkManager manager)
        {
            manager.CustomMessagingManager?.UnregisterNamedMessageHandler(ToTitleMessage);
            manager.CustomMessagingManager?.UnregisterNamedMessageHandler(ToTitleRequestMessage);
        }

        /// <summary>試合が終わってリザルトを見ている間か（このときに切れたら、さがし直さずにタイトルへ戻る）。</summary>
        private static bool IsShowingResult => FortressMatch.InMatch && FortressMatch.HasEnded;
    }
}
