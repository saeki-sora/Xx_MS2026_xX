using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.CharacterSelect
{
    /// <summary>
    /// 途中から入った人に、キャラ選択の結果（4人分のキャラ）をホストから届ける係（2026-11-15。起動時に自動で作られる見えないオブジェクト）。
    /// ・キャラ選択を通らずに試合へ入った PC は結果を持っていないので、ホストに頼む（届くまで 1 秒ごと）
    /// ・ホストは結果を持っていれば、キャラの id を4つ返す。届いた PC は、読み込まれているキャラから同じ id を探して結果に入れる
    ///   （スキルは <see cref="CharacterSelectionResult.Changed"/> で配り直される。CPU の席に途中から入った人は、CPU のキャラを引き継ぐ）
    /// 名前つきメッセージだけで、NetworkObject は使わない（キャラ選択と同じ）。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class CharacterResultSync : MonoBehaviour
    {
        public const string RequestMessage = "MS2026.CharaSelect.ResultRequest";
        public const string ResultMessage = "MS2026.CharaSelect.Result";

        private const float RequestInterval = 1f;

        private CustomMessagingManager _registered;
        private float _nextRequest;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CharacterResultSync>() != null)
            {
                return;
            }

            var go = new GameObject("[CharacterSelect] ResultSync");
            DontDestroyOnLoad(go);
            go.AddComponent<CharacterResultSync>();
        }

        private void OnDisable()
        {
            Unregister();
        }

        private void Update()
        {
            var manager = NetworkManager.Singleton;
            var messaging = manager != null && manager.IsListening ? manager.CustomMessagingManager : null;
            if (!ReferenceEquals(messaging, _registered))
            {
                Unregister();
                if (messaging != null)
                {
                    messaging.RegisterNamedMessageHandler(RequestMessage, OnRequest);
                    messaging.RegisterNamedMessageHandler(ResultMessage, OnResult);
                    _registered = messaging;
                    _nextRequest = 0f;
                }
            }

            // 参加側で、まだ結果が無ければホストに頼む（つながったばかりで、ホストの受け取り口が先に用意されていないこともあるので、届くまで繰り返す）。
            if (messaging == null || manager.IsServer || !manager.IsConnectedClient || CharacterSelectionResult.HasResult)
            {
                return;
            }

            if (Time.unscaledTime < _nextRequest)
            {
                return;
            }

            _nextRequest = Time.unscaledTime + RequestInterval;
            using var writer = new FastBufferWriter(1, Allocator.Temp);
            writer.WriteValueSafe((byte)1);
            messaging.SendNamedMessage(RequestMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        }

        private void Unregister()
        {
            if (_registered == null)
            {
                return;
            }

            _registered.UnregisterNamedMessageHandler(RequestMessage);
            _registered.UnregisterNamedMessageHandler(ResultMessage);
            _registered = null;
        }

        // ホスト: 頼まれたら、結果があればキャラの id を4つ返す（無ければ返さない。参加側はまた頼む）。
        private void OnRequest(ulong senderClientId, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsServer || !CharacterSelectionResult.HasResult)
            {
                return;
            }

            using var writer = new FastBufferWriter(4 * 66, Allocator.Temp);
            for (var seat = 0; seat < CharacterSelectBoard.SeatCount; seat++)
            {
                var character = CharacterSelectionResult.Get(seat);
                writer.WriteValueSafe(new FixedString64Bytes(character != null ? character.id : string.Empty));
            }

            manager.CustomMessagingManager.SendNamedMessage(ResultMessage, senderClientId, writer, NetworkDelivery.Reliable);
        }

        // 参加側: 届いた id のキャラを、読み込まれているキャラから探して結果に入れる。
        private void OnResult(ulong senderClientId, FastBufferReader reader)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.IsServer || senderClientId != NetworkManager.ServerClientId || CharacterSelectionResult.HasResult)
            {
                return;
            }

            var loaded = Resources.FindObjectsOfTypeAll<CharacterDefinition>();
            var chosen = new CharacterDefinition[CharacterSelectBoard.SeatCount];
            for (var seat = 0; seat < chosen.Length; seat++)
            {
                reader.ReadValueSafe(out FixedString64Bytes id);
                chosen[seat] = Find(loaded, id.ToString());
            }

            CharacterSelectionResult.Store(chosen);
            Debug.Log("[キャラ選択] 途中から入ったので、ホストからキャラ選択の結果を受け取りました。");
        }

        private static CharacterDefinition Find(CharacterDefinition[] loaded, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            foreach (var character in loaded)
            {
                if (character != null && character.id == id)
                {
                    return character;
                }
            }

            return null;
        }
    }
}
