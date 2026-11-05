using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Spectator
{
    /// <summary>
    /// 観客の「送る」を Host へ取り次ぐための、ゲームの通信の知らせ 3 つ（メインスレッドだけ）。
    ///   Order  : Client → Host   uint 取り次ぎ番号, ushort 観客の番号, byte 色, string 出口の名前
    ///   Result : Host → その Client  uint 取り次ぎ番号, byte 1=出した 0=出せなかった
    ///   Status : Host → 全員（1 秒 4 回）int 上限の残り, ushort たまり, ushort ボスまで, byte 残りのボス, byte 出たボス
    ///   Cards  : Host → その Client（試合が終わったとき 1 回）int 試合の見分け, ushort 枚数,
    ///            1 枚ごとに ushort 番号, 送った回数, 体数, 倒された, かじりついた, 順位, 人数, いちばんの番号, その回数
    /// 出口は名前で送る（PC ごとに並びが同じとは限らないため）。
    /// </summary>
    internal static class SpectatorRelay
    {
        public const string OrderMessage = "MS2026.Spectator.Order";
        public const string ResultMessage = "MS2026.Spectator.Result";
        public const string StatusMessage = "MS2026.Spectator.Status";
        public const string CardsMessage = "MS2026.Spectator.Cards";

        private const int UShortsPerCard = 9;

        /// <summary>Host: Client から「送る」が届いた（送り主の通信番号, 取り次ぎ番号, 観客の番号, 色, 出口の名前）。</summary>
        public static event Action<ulong, uint, int, int, string> OrderReceived;

        /// <summary>Client: 取り次いだ「送る」の結果が届いた。</summary>
        public static event Action<uint, bool> ResultReceived;

        /// <summary>Client: Host の受付の数が届いた。</summary>
        public static event Action<SpectatorHostStatus> StatusReceived;

        /// <summary>Client: この PC で受け付けた人たちの戦績が届いた。</summary>
        public static event Action<System.Collections.Generic.List<SpectatorResultCard>> CardsReceived;

        private static NetworkManager _registered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            OrderReceived = null;
            ResultReceived = null;
            StatusReceived = null;
            CardsReceived = null;
            _registered = null;
        }

        public static bool IsConnectedClient
        {
            get
            {
                var manager = NetworkManager.Singleton;
                return manager != null && manager.IsListening && !manager.IsServer && manager.IsConnectedClient;
            }
        }

        public static void EnsureMessaging()
        {
            var manager = NetworkManager.Singleton;
            var listening = manager != null && manager.IsListening && manager.CustomMessagingManager != null;
            if (!listening)
            {
                Unregister();
                return;
            }

            if (ReferenceEquals(_registered, manager))
            {
                return;
            }

            Unregister();
            var messaging = manager.CustomMessagingManager;
            messaging.RegisterNamedMessageHandler(OrderMessage, OnOrder);
            messaging.RegisterNamedMessageHandler(ResultMessage, OnResult);
            messaging.RegisterNamedMessageHandler(StatusMessage, OnStatus);
            messaging.RegisterNamedMessageHandler(CardsMessage, OnCards);
            _registered = manager;
        }

        public static void Unregister()
        {
            if (_registered == null)
            {
                return;
            }

            try
            {
                var messaging = _registered.CustomMessagingManager;
                messaging?.UnregisterNamedMessageHandler(OrderMessage);
                messaging?.UnregisterNamedMessageHandler(ResultMessage);
                messaging?.UnregisterNamedMessageHandler(StatusMessage);
                messaging?.UnregisterNamedMessageHandler(CardsMessage);
            }
            catch (Exception)
            {
                // 通信を止めた後で、もう登録が消えている。
            }

            _registered = null;
        }

        // ───────── 送る ─────────

        public static bool SendOrder(uint seq, int number, int colorIndex, string exitId)
        {
            var manager = _registered;
            if (manager == null || manager.IsServer || !manager.IsConnectedClient)
            {
                return false;
            }

            using var writer = new FastBufferWriter(16 + (exitId?.Length ?? 0) * 2 + 8, Allocator.Temp);
            writer.WriteValueSafe(seq);
            writer.WriteValueSafe((ushort)Mathf.Clamp(number, 0, ushort.MaxValue));
            writer.WriteValueSafe((byte)Mathf.Clamp(colorIndex, 0, 255));
            writer.WriteValueSafe(exitId ?? "");
            manager.CustomMessagingManager.SendNamedMessage(OrderMessage, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
            return true;
        }

        public static void SendResult(ulong clientId, uint seq, bool ok)
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(seq);
            writer.WriteValueSafe((byte)(ok ? 1 : 0));
            manager.CustomMessagingManager.SendNamedMessage(ResultMessage, clientId, writer, NetworkDelivery.Reliable);
        }

        public static void SendStatus(SpectatorHostStatus status)
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer || manager.ConnectedClientsIds.Count <= 1)
            {
                return;
            }

            using var writer = new FastBufferWriter(16, Allocator.Temp);
            writer.WriteValueSafe(Mathf.Max(0, status.Allowance));
            writer.WriteValueSafe((ushort)Mathf.Clamp(status.Pool, 0, ushort.MaxValue));
            writer.WriteValueSafe((ushort)Mathf.Clamp(status.PoolMax, 0, ushort.MaxValue));
            writer.WriteValueSafe((byte)Mathf.Clamp(status.BossesLeft, 0, 255));
            writer.WriteValueSafe((byte)Mathf.Clamp(status.BossesDone, 0, 255));
            manager.CustomMessagingManager.SendNamedMessageToAll(StatusMessage, writer, NetworkDelivery.UnreliableSequenced);
        }

        public static void SendCards(ulong clientId, int matchId, System.Collections.Generic.List<SpectatorResultCard> cards)
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer || cards.Count == 0)
            {
                return;
            }

            var count = Mathf.Min(cards.Count, 2000);
            using var writer = new FastBufferWriter(8 + count * UShortsPerCard * 2, Allocator.Temp);
            writer.WriteValueSafe(matchId);
            writer.WriteValueSafe((ushort)count);
            for (var i = 0; i < count; i++)
            {
                var c = cards[i];
                Write(writer, c.Number);
                Write(writer, c.Sends);
                Write(writer, c.Germs);
                Write(writer, c.Killed);
                Write(writer, c.Bites);
                Write(writer, c.Rank);
                Write(writer, c.Of);
                Write(writer, c.TopNumber);
                Write(writer, c.TopBites);
            }

            var delivery = writer.Length > 1200 ? NetworkDelivery.ReliableFragmentedSequenced : NetworkDelivery.Reliable;
            manager.CustomMessagingManager.SendNamedMessage(CardsMessage, clientId, writer, delivery);
        }

        private static void Write(FastBufferWriter writer, int value) => writer.WriteValueSafe((ushort)Mathf.Clamp(value, 0, ushort.MaxValue));

        // ───────── 受け取る ─────────

        private static void OnOrder(ulong sender, FastBufferReader reader)
        {
            var manager = _registered;
            if (manager == null || !manager.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out uint seq);
            reader.ReadValueSafe(out ushort number);
            reader.ReadValueSafe(out byte color);
            reader.ReadValueSafe(out string exitId);
            OrderReceived?.Invoke(sender, seq, number, color, exitId);
        }

        private static void OnResult(ulong sender, FastBufferReader reader)
        {
            if (_registered == null || _registered.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out uint seq);
            reader.ReadValueSafe(out byte ok);
            ResultReceived?.Invoke(seq, ok != 0);
        }

        private static void OnCards(ulong sender, FastBufferReader reader)
        {
            if (_registered == null || _registered.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out int matchId);
            reader.ReadValueSafe(out ushort count);
            var cards = new System.Collections.Generic.List<SpectatorResultCard>(count);
            for (var i = 0; i < count; i++)
            {
                reader.ReadValueSafe(out ushort number);
                reader.ReadValueSafe(out ushort sends);
                reader.ReadValueSafe(out ushort germs);
                reader.ReadValueSafe(out ushort killed);
                reader.ReadValueSafe(out ushort bites);
                reader.ReadValueSafe(out ushort rank);
                reader.ReadValueSafe(out ushort of);
                reader.ReadValueSafe(out ushort topNumber);
                reader.ReadValueSafe(out ushort topBites);
                cards.Add(new SpectatorResultCard
                {
                    MatchId = matchId,
                    Number = number,
                    Sends = sends,
                    Germs = germs,
                    Killed = killed,
                    Bites = bites,
                    Rank = rank,
                    Of = of,
                    TopNumber = topNumber,
                    TopBites = topBites
                });
            }

            CardsReceived?.Invoke(cards);
        }

        private static void OnStatus(ulong sender, FastBufferReader reader)
        {
            if (_registered == null || _registered.IsServer)
            {
                return;
            }

            reader.ReadValueSafe(out int allowance);
            reader.ReadValueSafe(out ushort pool);
            reader.ReadValueSafe(out ushort poolMax);
            reader.ReadValueSafe(out byte bossesLeft);
            reader.ReadValueSafe(out byte bossesDone);
            StatusReceived?.Invoke(new SpectatorHostStatus
            {
                Allowance = allowance,
                Pool = pool,
                PoolMax = poolMax,
                BossesLeft = bossesLeft,
                BossesDone = bossesDone
            });
        }
    }
}
