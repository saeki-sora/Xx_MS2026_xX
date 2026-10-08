using System;
using Unity.Collections;
using Unity.Netcode;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ロビーのやり取り（NGO の名前つきメッセージ。NetworkObject を増やさないので、ゲームのシーンの同期には一切触らない）。
    /// ・参加側 → ホスト: 名前（Hello）、準備OK（Ready）
    /// ・ホスト → 全員: 部屋の一覧と状態（State）。変わるたびに丸ごと送る（4人分なので100バイト程度）
    /// </summary>
    public static class LobbyMessages
    {
        public const string Hello = "MS2026.Lobby.Hello";
        public const string Ready = "MS2026.Lobby.Ready";
        public const string State = "MS2026.Lobby.State";

        private const int StateCapacity = 512;

        public static void Register(NetworkManager manager, CustomMessagingManager.HandleNamedMessageDelegate onHello,
            CustomMessagingManager.HandleNamedMessageDelegate onReady, CustomMessagingManager.HandleNamedMessageDelegate onState)
        {
            var messaging = manager != null ? manager.CustomMessagingManager : null;
            if (messaging == null)
            {
                return;
            }

            messaging.RegisterNamedMessageHandler(Hello, onHello);
            messaging.RegisterNamedMessageHandler(Ready, onReady);
            messaging.RegisterNamedMessageHandler(State, onState);
        }

        public static void Unregister(NetworkManager manager)
        {
            var messaging = manager != null ? manager.CustomMessagingManager : null;
            if (messaging == null)
            {
                return;
            }

            messaging.UnregisterNamedMessageHandler(Hello);
            messaging.UnregisterNamedMessageHandler(Ready);
            messaging.UnregisterNamedMessageHandler(State);
        }

        // ── 参加側 → ホスト ─────────────────
        public static void SendHello(NetworkManager manager, string name)
        {
            using var writer = new FastBufferWriter(128, Allocator.Temp);
            writer.WriteValueSafe(name ?? string.Empty);
            manager.CustomMessagingManager.SendNamedMessage(Hello, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        public static void SendReady(NetworkManager manager, bool ready)
        {
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            writer.WriteValueSafe(ready);
            manager.CustomMessagingManager.SendNamedMessage(Ready, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        public static string ReadHello(FastBufferReader reader)
        {
            reader.ReadValueSafe(out string name);
            return name;
        }

        public static bool ReadReady(FastBufferReader reader)
        {
            reader.ReadValueSafe(out bool ready);
            return ready;
        }

        // ── ホスト → 全員 ─────────────────
        public static void BroadcastState(NetworkManager manager, LobbyRoster roster)
        {
            using var writer = new FastBufferWriter(StateCapacity, Allocator.Temp);
            WriteState(writer, roster);
            manager.CustomMessagingManager.SendNamedMessageToAll(State, writer, NetworkDelivery.ReliableSequenced);
        }

        public static void SendState(NetworkManager manager, ulong clientId, LobbyRoster roster)
        {
            using var writer = new FastBufferWriter(StateCapacity, Allocator.Temp);
            WriteState(writer, roster);
            manager.CustomMessagingManager.SendNamedMessage(State, clientId, writer, NetworkDelivery.ReliableSequenced);
        }

        public static void ReadState(FastBufferReader reader, LobbyRoster into)
        {
            reader.ReadValueSafe(out byte phase);
            reader.ReadValueSafe(out float countdown);
            var incoming = new LobbyRoster();
            for (var i = 0; i < LobbyRoster.SeatCount; i++)
            {
                reader.ReadValueSafe(out bool present);
                reader.ReadValueSafe(out ulong clientId);
                reader.ReadValueSafe(out string name);
                reader.ReadValueSafe(out bool ready);
                reader.ReadValueSafe(out bool isHost);
                reader.ReadValueSafe(out ushort ping);
                incoming.seats[i] = new LobbyRoster.Seat { present = present, clientId = clientId, name = name, ready = ready, isHost = isHost, pingMs = ping };
            }

            incoming.phase = Enum.IsDefined(typeof(LobbyPhase), phase) ? (LobbyPhase)phase : LobbyPhase.Waiting;
            incoming.countdownRemaining = countdown;
            into.CopyFrom(incoming);
        }

        private static void WriteState(FastBufferWriter writer, LobbyRoster roster)
        {
            writer.WriteValueSafe((byte)roster.phase);
            writer.WriteValueSafe(roster.countdownRemaining);
            foreach (var seat in roster.seats)
            {
                writer.WriteValueSafe(seat.present);
                writer.WriteValueSafe(seat.clientId);
                writer.WriteValueSafe(seat.name ?? string.Empty);
                writer.WriteValueSafe(seat.ready);
                writer.WriteValueSafe(seat.isHost);
                writer.WriteValueSafe(seat.pingMs);
            }
        }
    }
}
