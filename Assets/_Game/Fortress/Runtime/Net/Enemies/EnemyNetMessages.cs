using Unity.Netcode;

namespace MS2026.Fortress.Net
{
    public enum EnemyNetEventKind : byte
    {
        Spawn,
        Despawn
    }

    /// <summary>Actorの敵の出現/消滅1件。起きた順に送り、Clientは同じ順に反映する。</summary>
    public struct EnemyNetEvent : INetworkSerializeByMemcpy
    {
        public uint Id;
        public EnemyNetEventKind Kind;

        /// <summary>Spawn: 敵の種類(EnemyTypeNetIndexの番号)。</summary>
        public ushort TypeIndex;

        /// <summary>Despawn: 消えた理由(EnemyRemovalReason)。</summary>
        public byte Reason;

        public float X;
        public float Y;
    }

    /// <summary>Actorの敵1体の位置と速度。毎秒十数回、全員分まとめて送る。</summary>
    public struct EnemyNetPosition : INetworkSerializeByMemcpy
    {
        public uint Id;
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
    }
}
