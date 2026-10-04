using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 被ダメージ・破壊・再生などの演出の設定。演出欄はプロジェクト共通の <see cref="FortressEffect"/>（D-DriveのVFX/SE）。
    /// エフェクトの消え方（寿命）・音量・連打の間引きは D-Drive 側（VFX/SE）の設定で決まる。
    /// </summary>
    [Serializable]
    public sealed class DestructibleFeedbackSettings
    {
        [Tooltip("ダメージを受けている間の演出（下の間隔ごとに繰り返す）。プレイヤー色は攻撃したプレイヤーの色。")]
        public FortressEffect onHit = new FortressEffect();

        [Tooltip("onHitを繰り返し発生させる最短の間隔(秒)。レーザーは毎フレーム当たるので、間引かないと鳴りっぱなしになる。")]
        [Min(0.02f)]
        public float hitInterval = 0.15f;

        [Tooltip("見た目の段階が進んだ（ひびが入った等）瞬間の演出。")]
        public FortressEffect onStageChanged = new FortressEffect();

        [Tooltip("破壊された瞬間の演出。プレイヤー色は壊したプレイヤーの色。")]
        public FortressEffect onDestroyed = new FortressEffect();

        [Tooltip("再生した瞬間の演出。")]
        public FortressEffect onRegenerated = new FortressEffect();

        [Header("仮の破片（素材が無い間の代わり）")]
        [Tooltip("破壊時のエフェクト(VFX)が空のとき、四角い仮の破片を飛び散らせる。")]
        public bool placeholderDebris = true;

        [Range(0, 40)]
        public int debrisCount = 10;

        public Color debrisColor = new Color(0.72f, 0.52f, 0.34f);
    }
}
