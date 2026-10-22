using System;
using System.Text;

namespace MS2026.GripInputBridge.Relay
{
    /// <summary>放送1回分に入っている、センサー1つ分の値。</summary>
    public struct GripRelaySensorSample
    {
        /// <summary>センサー特性の変換後の値(0-1)。受け取ったPCで、フィルタ・キャリブレーションを通してゲームに使う。</summary>
        public float Raw;

        /// <summary>マイコンから届いたままの値(0-1、変換前)。診断表示用。</summary>
        public float Received;

        /// <summary>このセンサーの値が今届いているか（Arduino がこのセンサーの値を送ってきている）。</summary>
        public bool Connected;
    }

    /// <summary>
    /// 「握力の放送」1回分（1台の Arduino に4人分のセンサーをつなぎ、そのPCが LAN の全員へ配る）。
    /// 送るのも受け取るのも <see cref="GripRelayCodec"/> で、バイト列との変換はそちらで行う。
    /// </summary>
    public sealed class GripRelayPacket
    {
        public const int SensorCount = GripInputBridgeConstants.PlayerCount;

        /// <summary>放送しているゲーム（起動1回ごとに変わる番号）。自分の放送を見分けたり、放送が2か所から来ているのに気づくのに使う。</summary>
        public uint SenderId;

        /// <summary>1回送るごとに1増える番号。同じ放送が複数の宛先から重なって届く・順番が入れ替わるので、古い物を捨てるのに使う。</summary>
        public uint Sequence;

        /// <summary>Arduino が無くても、テストのためにキーボードの値を放送している（起動オプション -grip-relay-send）。</summary>
        public bool ForcedTestSender;

        /// <summary>放送しているPCの名前（診断表示用）。</summary>
        public string SenderName = string.Empty;

        public readonly GripRelaySensorSample[] Sensors = new GripRelaySensorSample[SensorCount];

        // 受け取り側で、名前が前と同じなら文字列を作り直さないための控え（1秒に数百回受け取るので、毎回作るとゴミが増える）。
        internal readonly byte[] NameBytesCache = new byte[GripRelayCodec.MaxNameBytes];
        internal int NameBytesCacheLength = -1;

        /// <summary>値が届いているセンサーが1つでもあるか。</summary>
        public bool HasConnectedSensor
        {
            get
            {
                for (var i = 0; i < SensorCount; i++)
                {
                    if (Sensors[i].Connected)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public void CopyFrom(GripRelayPacket other)
        {
            SenderId = other.SenderId;
            Sequence = other.Sequence;
            ForcedTestSender = other.ForcedTestSender;
            SenderName = other.SenderName;
            Array.Copy(other.Sensors, Sensors, SensorCount);
        }
    }

    /// <summary>放送を読んだ結果。</summary>
    public enum GripRelayReadResult
    {
        Ok,

        /// <summary>このゲームの放送ではない（目印が違う・短すぎる）。</summary>
        NotRelayPacket,

        /// <summary>このゲームの放送だが、形の版が違う（ビルドが違うPCが放送している）。</summary>
        WrongVersion,

        /// <summary>目印と版は合っているが、中身が壊れている。</summary>
        Malformed
    }

    /// <summary>
    /// 握力の放送とバイト列の変換（純粋関数・テストあり）。
    /// 形（リトルエンディアン）: 目印 "MSGR"(4) / 版(1) / 印(1: bit0=テスト用の放送) / 放送の番号(4) / 連番(4) / センサー数(1) /
    /// センサーごとに [変換後の値 0-65535(2) / 届いたままの値 0-65535(2) / 状態(1: bit0=値が届いている)] / 名前のバイト数(1) / 名前(UTF-8)。
    /// </summary>
    public static class GripRelayCodec
    {
        public const byte Version = 1;
        public const int MaxNameBytes = 40;
        private const int HeaderBytes = 4 + 1 + 1 + 4 + 4 + 1;
        private const int SensorBytes = 5;
        public const int MaxPacketBytes = HeaderBytes + GripRelayPacket.SensorCount * SensorBytes + 1 + MaxNameBytes;

        private static readonly byte[] Magic = { (byte)'M', (byte)'S', (byte)'G', (byte)'R' };

        /// <summary>パケットを buffer（<see cref="MaxPacketBytes"/> 以上）に書き、書いたバイト数を返す。名前が長すぎるときは切る。</summary>
        public static int Write(GripRelayPacket packet, byte[] buffer)
        {
            if (packet == null)
            {
                throw new ArgumentNullException(nameof(packet));
            }

            if (buffer == null || buffer.Length < MaxPacketBytes)
            {
                throw new ArgumentException($"buffer は {MaxPacketBytes} バイト以上必要です。", nameof(buffer));
            }

            var at = 0;
            for (var i = 0; i < Magic.Length; i++)
            {
                buffer[at++] = Magic[i];
            }

            buffer[at++] = Version;
            buffer[at++] = (byte)(packet.ForcedTestSender ? 1 : 0);
            WriteUInt32(buffer, ref at, packet.SenderId);
            WriteUInt32(buffer, ref at, packet.Sequence);
            buffer[at++] = GripRelayPacket.SensorCount;
            for (var i = 0; i < GripRelayPacket.SensorCount; i++)
            {
                var sensor = packet.Sensors[i];
                WriteUInt16(buffer, ref at, ToUInt16(sensor.Raw));
                WriteUInt16(buffer, ref at, ToUInt16(sensor.Received));
                buffer[at++] = (byte)(sensor.Connected ? 1 : 0);
            }

            var name = TruncateName(packet.SenderName);
            var nameBytes = Encoding.UTF8.GetBytes(name, 0, name.Length, buffer, at + 1);
            buffer[at] = (byte)nameBytes;
            at += 1 + nameBytes;
            return at;
        }

        /// <summary>受け取ったバイト列を読む。Ok 以外のときは into の中身は変えない。</summary>
        public static GripRelayReadResult TryRead(byte[] data, int length, GripRelayPacket into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (data == null || length < Magic.Length + 1 || length > data.Length)
            {
                return GripRelayReadResult.NotRelayPacket;
            }

            for (var i = 0; i < Magic.Length; i++)
            {
                if (data[i] != Magic[i])
                {
                    return GripRelayReadResult.NotRelayPacket;
                }
            }

            if (data[Magic.Length] != Version)
            {
                return GripRelayReadResult.WrongVersion;
            }

            if (length < HeaderBytes + GripRelayPacket.SensorCount * SensorBytes + 1)
            {
                return GripRelayReadResult.Malformed;
            }

            var at = Magic.Length + 1;
            var flags = data[at++];
            var senderId = ReadUInt32(data, ref at);
            var sequence = ReadUInt32(data, ref at);
            if (data[at++] != GripRelayPacket.SensorCount)
            {
                return GripRelayReadResult.Malformed;
            }

            var sensorStart = at;
            at += GripRelayPacket.SensorCount * SensorBytes;
            var nameLength = data[at++];
            if (nameLength > MaxNameBytes || at + nameLength != length)
            {
                return GripRelayReadResult.Malformed;
            }

            var name = into.SenderName;
            if (!SameBytes(into, data, at, nameLength))
            {
                try
                {
                    name = Encoding.UTF8.GetString(data, at, nameLength);
                }
                catch (ArgumentException)
                {
                    return GripRelayReadResult.Malformed;
                }

                Buffer.BlockCopy(data, at, into.NameBytesCache, 0, nameLength);
                into.NameBytesCacheLength = nameLength;
            }

            into.ForcedTestSender = (flags & 1) != 0;
            into.SenderId = senderId;
            into.Sequence = sequence;
            into.SenderName = name;
            at = sensorStart;
            for (var i = 0; i < GripRelayPacket.SensorCount; i++)
            {
                into.Sensors[i].Raw = ReadUInt16(data, ref at) / 65535f;
                into.Sensors[i].Received = ReadUInt16(data, ref at) / 65535f;
                into.Sensors[i].Connected = (data[at++] & 1) != 0;
            }

            return GripRelayReadResult.Ok;
        }

        /// <summary>名前を UTF-8 で <see cref="MaxNameBytes"/> バイト以内に切る（文字の途中では切らない）。</summary>
        public static string TruncateName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return string.Empty;
            }

            if (Encoding.UTF8.GetByteCount(name) <= MaxNameBytes)
            {
                return name;
            }

            var length = name.Length;
            while (length > 0)
            {
                length--;
                if (length > 0 && char.IsLowSurrogate(name[length]) && char.IsHighSurrogate(name[length - 1]))
                {
                    continue; // サロゲートペアの片方だけを残さない。
                }

                var candidate = name.Substring(0, length);
                if (Encoding.UTF8.GetByteCount(candidate) <= MaxNameBytes)
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        // 前に読んだ名前のバイト列と同じか（同じなら into.SenderName をそのまま使う）。
        private static bool SameBytes(GripRelayPacket into, byte[] data, int at, int length)
        {
            if (into.NameBytesCacheLength != length)
            {
                return false;
            }

            for (var i = 0; i < length; i++)
            {
                if (into.NameBytesCache[i] != data[at + i])
                {
                    return false;
                }
            }

            return true;
        }

        private static ushort ToUInt16(float value01)
        {
            if (float.IsNaN(value01) || value01 <= 0f)
            {
                return 0;
            }

            return value01 >= 1f ? ushort.MaxValue : (ushort)(value01 * 65535f + 0.5f);
        }

        private static void WriteUInt16(byte[] buffer, ref int at, ushort value)
        {
            buffer[at++] = (byte)value;
            buffer[at++] = (byte)(value >> 8);
        }

        private static void WriteUInt32(byte[] buffer, ref int at, uint value)
        {
            buffer[at++] = (byte)value;
            buffer[at++] = (byte)(value >> 8);
            buffer[at++] = (byte)(value >> 16);
            buffer[at++] = (byte)(value >> 24);
        }

        private static ushort ReadUInt16(byte[] data, ref int at)
        {
            var value = (ushort)(data[at] | (data[at + 1] << 8));
            at += 2;
            return value;
        }

        private static uint ReadUInt32(byte[] data, ref int at)
        {
            var value = (uint)(data[at] | (data[at + 1] << 8) | (data[at + 2] << 16) | (data[at + 3] << 24));
            at += 4;
            return value;
        }
    }
}
