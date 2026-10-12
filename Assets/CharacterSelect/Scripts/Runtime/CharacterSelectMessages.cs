using Unity.Collections;
using Unity.Netcode;

namespace MS2026.CharacterSelect
{
    /// <summary>キャラ選択でプレイヤーがしたこと（参加側 → ホストへ送る）。</summary>
    public enum CharacterSelectAction : byte
    {
        None = 0,

        /// <summary>次のキャラへ（短く握った）。</summary>
        Next = 1,

        /// <summary>今のキャラで決定（長く握った）。</summary>
        Confirm = 2,

        /// <summary>決定を取り消す（決定後に長く握った）。</summary>
        Cancel = 3,

        /// <summary>今の状態を送ってほしい（参加側の選択画面が動き出したとき）。</summary>
        RequestState = 4
    }

    /// <summary>
    /// キャラ選択のやり取り（NGO の名前つきメッセージ。ロビーと同じく NetworkObject は増やさない）。
    /// ・参加側 → ホスト: したこと（Input）
    /// ・ホスト → 全員: 4人分のカーソル・決定・ルール・残り時間（State）。変わるたびに丸ごと送る（約30バイト）
    /// </summary>
    public static class CharacterSelectMessages
    {
        public const string Input = "MS2026.CharaSelect.Input";
        public const string State = "MS2026.CharaSelect.State";

        private const int StateCapacity = 64;

        public static void Register(NetworkManager manager, CustomMessagingManager.HandleNamedMessageDelegate onInput,
            CustomMessagingManager.HandleNamedMessageDelegate onState)
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
        /// <summary>character = 参加側の画面でカーソルがあったキャラ（決定のとき、ホスト側でカーソルが動いていたら断るために使う）。</summary>
        public static void SendInput(NetworkManager manager, CharacterSelectAction action, int character = 0)
        {
            using var writer = new FastBufferWriter(4, Allocator.Temp);
            writer.WriteValueSafe((byte)action);
            writer.WriteValueSafe((byte)(character < 0 ? 0 : character));
            manager.CustomMessagingManager.SendNamedMessage(Input, NetworkManager.ServerClientId, writer, NetworkDelivery.ReliableSequenced);
        }

        public static CharacterSelectAction ReadInput(FastBufferReader reader, out int character)
        {
            reader.ReadValueSafe(out byte action);
            reader.ReadValueSafe(out byte seen);
            character = seen;
            return action <= (byte)CharacterSelectAction.RequestState ? (CharacterSelectAction)action : CharacterSelectAction.None;
        }

        // ── ホスト → 全員 ─────────────────
        public static void BroadcastState(NetworkManager manager, CharacterSelectBoard board)
        {
            using var writer = new FastBufferWriter(StateCapacity, Allocator.Temp);
            WriteState(writer, board);
            manager.CustomMessagingManager.SendNamedMessageToAll(State, writer, NetworkDelivery.ReliableSequenced);
        }

        public static void SendState(NetworkManager manager, ulong clientId, CharacterSelectBoard board)
        {
            using var writer = new FastBufferWriter(StateCapacity, Allocator.Temp);
            WriteState(writer, board);
            manager.CustomMessagingManager.SendNamedMessage(State, clientId, writer, NetworkDelivery.ReliableSequenced);
        }

        public static void ReadState(FastBufferReader reader, CharacterSelectBoard into)
        {
            reader.ReadValueSafe(out byte characterCount);
            reader.ReadValueSafe(out bool allowDuplicates);
            reader.ReadValueSafe(out bool hasTimeLimit);
            reader.ReadValueSafe(out float remaining);
            reader.ReadValueSafe(out bool finished);
            var seats = new CharacterSelectBoard.Seat[CharacterSelectBoard.SeatCount];
            for (var i = 0; i < seats.Length; i++)
            {
                reader.ReadValueSafe(out bool active);
                reader.ReadValueSafe(out byte cursor);
                reader.ReadValueSafe(out bool confirmed);
                seats[i] = new CharacterSelectBoard.Seat { active = active, cursor = cursor, confirmed = confirmed };
            }

            into.Apply(characterCount, allowDuplicates, hasTimeLimit, remaining, finished, seats);
        }

        private static void WriteState(FastBufferWriter writer, CharacterSelectBoard board)
        {
            writer.WriteValueSafe((byte)board.CharacterCount);
            writer.WriteValueSafe(board.AllowDuplicates);
            writer.WriteValueSafe(board.HasTimeLimit);
            writer.WriteValueSafe(board.remaining);
            writer.WriteValueSafe(board.Finished);
            foreach (var seat in board.seats)
            {
                writer.WriteValueSafe(seat.active);
                writer.WriteValueSafe((byte)(seat.cursor < 0 ? 0 : seat.cursor));
                writer.WriteValueSafe(seat.confirmed);
            }
        }
    }
}
