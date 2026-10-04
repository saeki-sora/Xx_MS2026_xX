using Unity.Collections;
using Unity.Netcode;

namespace MS2026.Fortress.Net
{
    /// <summary>名前なしメッセージの先頭1バイトに書く、メッセージの種類。</summary>
    public enum FortressNetChannel : byte
    {
        None = 0,
        SwarmEvents = 1,
        SwarmFullState = 2,
        SwarmCorrections = 3
    }

    /// <summary>
    /// 量の多い同期データ用の送受信口。NGOの「名前なしメッセージ」を使い、先頭1バイトの種類(FortressNetChannel)で受け手に振り分ける。
    ///
    /// RPCをやめた理由(2026-10-04 段階3): 配列を引数にしたRPCは送受信のたびに配列を新しく作り(=GCの元)、
    /// 名前付きメッセージは送るたびにメッセージ名を文字列からバイト配列へ変換する(これもGCの元)。
    /// 名前なしメッセージ＋事前確保なしのTemp領域(FastBufferWriter/Allocator.Temp)なら、送受信でGCが出ない。
    /// D-Driveは名前なしメッセージを使っていない(2026-10-04確認)。
    ///
    /// 使い方: 受け手はOnNetworkSpawnでRegister、OnNetworkDespawnでUnregister。送り手は Begin で書き始めて Send する。
    /// </summary>
    public static class FortressNetMessages
    {
        public delegate void Handler(ulong senderClientId, FastBufferReader reader);

        private static readonly Handler[] Handlers = new Handler[256];
        private static CustomMessagingManager _subscribedTo;
        private static int _handlerCount;

        /// <summary>この種類のメッセージの受け手を登録する(接続ごとに作り直されるCustomMessagingManagerへも自動で付け直す)。</summary>
        public static void Register(NetworkManager networkManager, FortressNetChannel channel, Handler handler)
        {
            var manager = networkManager != null ? networkManager.CustomMessagingManager : null;
            if (manager == null)
            {
                return;
            }

            if (!ReferenceEquals(_subscribedTo, manager))
            {
                if (_subscribedTo != null)
                {
                    _subscribedTo.OnUnnamedMessage -= OnUnnamedMessage;
                }

                manager.OnUnnamedMessage += OnUnnamedMessage;
                _subscribedTo = manager;
            }

            if (Handlers[(byte)channel] == null)
            {
                _handlerCount++;
            }

            Handlers[(byte)channel] = handler;
        }

        public static void Unregister(FortressNetChannel channel)
        {
            if (Handlers[(byte)channel] == null)
            {
                return;
            }

            Handlers[(byte)channel] = null;
            _handlerCount--;
            if (_handlerCount <= 0 && _subscribedTo != null)
            {
                _subscribedTo.OnUnnamedMessage -= OnUnnamedMessage;
                _subscribedTo = null;
                _handlerCount = 0;
            }
        }

        /// <summary>書き始める(先頭に種類を書いた状態で返す)。使い終わったら必ず Dispose する(using推奨)。</summary>
        public static FastBufferWriter Begin(FortressNetChannel channel, int capacityBytes, int maxCapacityBytes = -1)
        {
            var writer = new FastBufferWriter(capacityBytes + 1, Allocator.Temp, maxCapacityBytes < 0 ? -1 : maxCapacityBytes + 1);
            writer.WriteValueSafe((byte)channel);
            return writer;
        }

        public static void Send(NetworkManager networkManager, ulong clientId, FastBufferWriter writer, NetworkDelivery delivery)
        {
            networkManager.CustomMessagingManager.SendUnnamedMessage(clientId, writer, delivery);
        }

        private static void OnUnnamedMessage(ulong senderClientId, FastBufferReader reader)
        {
            if (!reader.TryBeginRead(1))
            {
                return;
            }

            reader.ReadValue(out byte channel);
            Handlers[channel]?.Invoke(senderClientId, reader);
        }
    }
}
