using Unity.Netcode;

namespace MS2026.Fortress.Net
{
    public enum DestructibleNetEventKind : byte
    {
        /// <summary>取り消し済み(送らない)。</summary>
        None,

        /// <summary>ダメージ(その間に受けた合計量)と、その後の耐久。</summary>
        Damage,

        /// <summary>ダメージ以外の耐久の変化(自己修復など)。</summary>
        Health,

        Destroy,
        Regenerate,
        Invulnerable,

        /// <summary>GameObjectの表示ON/OFF(スマッシュボールのローテーションなど)。</summary>
        Active
    }

    /// <summary>Hostで起きた破壊可能物の変化1件。起きた順に送り、Clientは同じ順に反映する。</summary>
    public struct DestructibleNetEvent : INetworkSerializeByMemcpy
    {
        /// <summary>DestructibleNetIndexでの番号。</summary>
        public ushort Index;
        public DestructibleNetEventKind Kind;

        /// <summary>DamageSource。</summary>
        public byte Source;

        /// <summary>攻撃したプレイヤー番号(0-3)。不明なら-1。</summary>
        public sbyte Attacker;

        /// <summary>Invulnerable/Activeの値。</summary>
        public bool Flag;

        public float Health;
        public float Amount;
    }

    /// <summary>破壊可能物1つの今の状態。途中参加したClientに一度だけ送る。</summary>
    public struct DestructibleNetState : INetworkSerializeByMemcpy
    {
        public float Health;
        public float RegenProgress01;
        public int DestroyCount;
        public sbyte DestroyedBy;
        public sbyte LastAttacker;
        public bool IsDestroyed;
        public bool IsInvulnerable;
        public bool IsActive;
    }

    /// <summary>浮遊中のスマッシュボールの位置1件。</summary>
    public struct DestructibleNetPosition : INetworkSerializeByMemcpy
    {
        public ushort Index;
        public float X;
        public float Y;
    }
}
