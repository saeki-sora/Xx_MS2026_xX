using Unity.Mathematics;
using Unity.Netcode;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    public enum SwarmNetEventKind : byte
    {
        /// <summary>使わない(既定値)。</summary>
        None,
        Spawn,
        Despawn
    }

    /// <summary>群衆の敵1体の出現/消滅1件(14バイト)。写真(スナップショット)の「新しく出た敵」「消えた敵」と、全員分の写真に使う。</summary>
    public struct SwarmNetEvent : INetworkSerializeByMemcpy
    {
        public ushort Id;
        public SwarmNetEventKind Kind;

        /// <summary>Spawn: 敵の種類(EnemyTypeNetIndexの番号) / Despawn: 消えた理由(EnemyRemovalReason)。</summary>
        public byte TypeOrReason;

        public short X;
        public short Y;

        /// <summary>速さのばらつき(half)。</summary>
        public ushort SpeedScale;

        /// <summary>アニメーションの開始位置(half)。</summary>
        public ushort AnimStart;

        /// <summary>向き(0-255で一周)。</summary>
        public byte Facing;
    }

    /// <summary>
    /// 群衆の値を小さく詰める/戻す(Unity API非依存・EditModeテスト対象)。
    /// 位置: 1/500単位の16bit整数(±65.5まで、精度0.002)。向き: 256分割。速さ等: 16bit浮動小数(half)。
    /// </summary>
    public static class SwarmNetQuantize
    {
        public const float PositionScale = 500f;
        public const float MaxPosition = short.MaxValue / PositionScale;

        public static short ToShort(float value)
        {
            var scaled = math.round(value * PositionScale);
            return (short)math.clamp(scaled, short.MinValue, short.MaxValue);
        }

        public static float FromShort(short value)
        {
            return value / PositionScale;
        }

        public static byte ToAngleByte(Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-8f)
            {
                return 0;
            }

            var turns = math.atan2(direction.y, direction.x) / (2f * math.PI);
            return (byte)((int)math.round(turns * 256f) & 0xFF);
        }

        public static Vector2 FromAngleByte(byte value)
        {
            var angle = value / 256f * 2f * math.PI;
            return new Vector2(math.cos(angle), math.sin(angle));
        }

        public static ushort ToHalf(float value)
        {
            return new half(value).value;
        }

        public static float FromHalf(ushort value)
        {
            return new half { value = value };
        }
    }
}
