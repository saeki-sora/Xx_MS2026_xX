using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// コアが壊れた瞬間のスローモーション（見せ方だけ）の設定。コアが壊れると試合はそこで終わる（ゲームオーバー）ので、遊びには影響しない。
    /// 各PCが「コアが壊れた」知らせ（CoreCrystalController.OnCoreDestroyed。Client では Host から届いた HP で起きる）で、自分の画面だけ時間をゆっくりにする。
    /// ステージの見た目の設定（StageLookProfile.coreBreakSlowMotion）が持ち、<see cref="CoreBreakSlowMotion"/> が使う。
    /// </summary>
    [Serializable]
    public sealed class CoreBreakSlowMotionSettings
    {
        [Tooltip("OFFにすると、コアが壊れても時間をゆっくりにしない。")]
        public bool enabled = true;

        [Range(0.02f, 1f), Tooltip("ゆっくりの速さ（1=ふつう、0.2=5分の1の速さ）。")]
        public float slowScale = 0.2f;

        [Min(0f), Tooltip("ゆっくりのまま見せる秒数（実際の時間）。")]
        public float holdSeconds = 0.7f;

        [Min(0.01f), Tooltip("ゆっくりから、ふつうの速さへなめらかに戻る秒数（実際の時間）。")]
        public float recoverSeconds = 1.1f;
    }
}
