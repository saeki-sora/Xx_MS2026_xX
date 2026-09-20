using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 敵が「次にどちらへ進むか」を問い合わせる窓口。フロー・フィールド、A*アセット、
    /// スクリプトによる固定ルートなど、実装を差し替えても敵側のコードは変わらない。
    /// </summary>
    public interface IEnemyNavigator
    {
        /// <summary>進むべき単位方向。方向が決められない場合はVector2.zero（敵側は目標へ直進にフォールバック）。</summary>
        Vector2 GetDirection(Vector2 position, NavigationProfile profile);

        /// <summary>その位置での移動速度倍率（泥地帯などの減速に使う）。通常は1。</summary>
        float GetSpeedMultiplier(Vector2 position);
    }

    /// <summary>現在有効なナビゲーターへのグローバルな参照。シーンにNavigationFieldがあれば自動で登録される。</summary>
    public static class EnemyNavigation
    {
        public static IEnemyNavigator Current { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Current = null;
        }
    }
}
