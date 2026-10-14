using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 試合の終わり方の決まり（2026-10-13、ユーザー決定「一旦、敵を1000体倒したらクリア」）。ゲームのシーンに置くと、その値を使う。
    /// 置かなければ既定値（1000体でクリア・コアが壊れたらゲームオーバー・残りの敵は消す）で動く（<see cref="FortressMatch"/>）。
    /// 決めるのは Host（オフラインでは自分）。値は Host のシーンの物を使う。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Fortress/試合の決まり (FortressMatchRules)")]
    public sealed class FortressMatchRules : MonoBehaviour
    {
        [Tooltip("全員でこの数だけ敵を倒したらクリア。")]
        [Min(1)]
        public int clearKills = FortressMatch.DefaultClearKills;

        [Tooltip("ONなら、試合が終わった瞬間に敵の出現を止め、残っている敵を消す（消した敵は撃破数に入れない）。")]
        public bool removeEnemiesOnEnd = true;

        [Header("コア")]
        [Tooltip("試合の始めのコアの体力（最大HP）。0 ならコア（CoreCrystalController）に入っている値のまま。全員のPCで同じ値になる。")]
        [Min(0f)]
        public float coreMaxHealth = 100f;

        [Tooltip("コアが受けるダメージの倍率（1 = そのまま、0.5 = 半分、2 = 2倍）。敵の種類ごとの「コアへのダメージ」に掛ける。")]
        [Min(0f)]
        public float coreDamageMultiplier = 1f;

        [Header("演出（D-Drive の番号札。空なら鳴らさない）")]
        [Tooltip("クリアした瞬間（各PCで鳴らす）。位置はコア。")]
        public FortressEffect onClear = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("ゲームオーバーになった瞬間（各PCで鳴らす）。位置はコア。")]
        public FortressEffect onGameOver = new FortressEffect { tintWithPlayerColor = false };
    }
}
