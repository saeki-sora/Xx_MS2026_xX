namespace MS2026.Fortress
{
    /// <summary>
    /// 数値でダメージを受けられるもの。レーザー以外（将来の敵の攻撃・爆発・スクリプト）から
    /// 破壊可能物などへダメージを与える入口になる。
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(float amount);
    }
}
