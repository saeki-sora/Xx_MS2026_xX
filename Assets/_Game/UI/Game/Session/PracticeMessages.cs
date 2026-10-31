using MS2026.HowToPlay;
using Unity.Collections;
using Unity.Netcode;

namespace MS2026.UI.Game
{
    /// <summary>
    /// あそびかたの練習のやり取り（NGO の名前つきメッセージ）。
    /// ・参加側 → ホスト: 自分の握力・熱・オーバーヒート・課題の進み（Input）。変わったときだけ、1 秒に 15 回まで
    /// ・ホスト → 全員: 全員分の様子と残り時間（State）。変わったときだけ、1 秒に 15 回まで（4人分で 20 バイトほど）
    /// 席にいるかどうかは部屋の一覧（<see cref="LobbyRoster"/>）で分かるので送らない。
    /// </summary>
    public static class PracticeMessages
    {
        public const string Input = "MS2026.Practice.Input";
        public const string State = "MS2026.Practice.State";

        private const byte FlagGripping = 1;
        private const byte FlagOverheated = 2;

        public static void Register(NetworkManager manager, CustomMessagingManager.HandleNamedMessageDelegate onInput, CustomMessagingManager.HandleNamedMessageDelegate onState)
        {
            var messaging = manager != null ? manager.CustomMessagingManager : null;
            if (messaging == null)
            {
                return;
            }

            messaging.RegisterNamedMessageHandler(Input, onInput);
            messaging.RegisterNamedMessageHandler(State, onState);
        }

        public static void Unregister(NetworkManager manager)
        {
            var messaging = manager != null ? manager.CustomMessagingManager : null;
            if (messaging == null)
            {
                return;
            }

            messaging.UnregisterNamedMessageHandler(Input);
            messaging.UnregisterNamedMessageHandler(State);
        }

        // ── 参加側 → ホスト ─────────────────
        public static void SendInput(NetworkManager manager, PracticeBoard.Seat seat)
        {
            using var writer = new FastBufferWriter(8, Allocator.Temp);
            WriteSeat(writer, seat);
            manager.CustomMessagingManager.SendNamedMessage(Input, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        public static PracticeBoard.Seat ReadInput(FastBufferReader reader) => ReadSeat(reader);

        // ── ホスト → 全員 ─────────────────
        public static void BroadcastState(NetworkManager manager, PracticeBoard board)
        {
            using var writer = new FastBufferWriter(64, Allocator.Temp);
            writer.WriteValueSafe(board.remaining);
            writer.WriteValueSafe(board.finished);
            foreach (var seat in board.seats)
            {
                WriteSeat(writer, seat);
            }

            manager.CustomMessagingManager.SendNamedMessageToAll(State, writer, NetworkDelivery.ReliableSequenced);
        }

        /// <summary>届いた様子を入れる（exceptSeat = 自分の席。自分の値は自分の PC の方が新しいので、課題の進みだけ受け取る）。</summary>
        public static void ReadState(FastBufferReader reader, PracticeBoard into, int exceptSeat)
        {
            reader.ReadValueSafe(out float remaining);
            reader.ReadValueSafe(out bool finished);
            var seats = new PracticeBoard.Seat[PracticeBoard.SeatCount];
            for (var i = 0; i < seats.Length; i++)
            {
                seats[i] = ReadSeat(reader);
            }

            into.Apply(remaining, finished, seats, exceptSeat);
        }

        private static void WriteSeat(FastBufferWriter writer, PracticeBoard.Seat seat)
        {
            var flags = (byte)((seat.gripping ? FlagGripping : 0) | (seat.overheated ? FlagOverheated : 0));
            writer.WriteValueSafe(flags);
            writer.WriteValueSafe(seat.grip);
            writer.WriteValueSafe(seat.heat);
            writer.WriteValueSafe((byte)seat.step);
        }

        private static PracticeBoard.Seat ReadSeat(FastBufferReader reader)
        {
            reader.ReadValueSafe(out byte flags);
            reader.ReadValueSafe(out byte grip);
            reader.ReadValueSafe(out byte heat);
            reader.ReadValueSafe(out byte step);
            return new PracticeBoard.Seat
            {
                gripping = (flags & FlagGripping) != 0,
                overheated = (flags & FlagOverheated) != 0,
                grip = grip,
                heat = heat,
                step = step <= (byte)PracticeStep.Done ? (PracticeStep)step : PracticeStep.Grip
            };
        }
    }
}
