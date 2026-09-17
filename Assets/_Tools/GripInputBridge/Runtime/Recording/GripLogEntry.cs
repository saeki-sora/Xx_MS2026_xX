using System;

namespace MS2026.GripInputBridge.Recording
{
    /// <summary>
    /// JSON Lines形式のログ1行分。設計書 10.1 参照。
    /// UnityのJsonUtilityでそのままシリアライズ/デシリアライズできるよう、publicフィールドのみで構成する。
    /// </summary>
    [Serializable]
    public struct GripLogEntry
    {
        /// <summary>記録開始からの経過ミリ秒。</summary>
        public long t;

        /// <summary>プレイヤー番号(0-3)。</summary>
        public int p;

        /// <summary>フィルタ適用前の生値。</summary>
        public float raw;

        /// <summary>フィルタ適用後・キャリブレーション適用前の値。</summary>
        public float filtered;

        /// <summary>キャリブレーション適用後の最終値(ゲームが実際に使う値)。</summary>
        public float normalized;
    }
}
