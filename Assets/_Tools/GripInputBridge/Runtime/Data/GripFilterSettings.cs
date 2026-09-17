using UnityEngine;

namespace MS2026.GripInputBridge.Data
{
    /// <summary>設計書 7.2 参照。適用するノイズフィルタの種類。</summary>
    public enum GripFilterType
    {
        /// <summary>直近Nサンプルの単純移動平均。</summary>
        MovingAverage,

        /// <summary>指数移動平均(EMA)。直近値を重視しつつ滑らかにする。</summary>
        ExponentialMovingAverage,

        /// <summary>単発の外れ値(スパイクノイズ)に強いメディアンフィルタ。</summary>
        Median,

        /// <summary>
        /// 設計書7.2の推奨初期設定。先にメディアンでスパイクを落とし、次にEMAで滑らかにする。
        /// </summary>
        MedianThenExponentialMovingAverage
    }

    /// <summary>
    /// ノイズフィルタの調整値をまとめたアセット。プレイヤー間で共有する
    /// （プレイヤーごとの個人差は <see cref="GripCalibrationProfile"/> 側で吸収する）。
    /// 設計書 7.4 データモデル 参照。
    /// </summary>
    [CreateAssetMenu(fileName = "GripFilterSettings", menuName = "MS2026/Grip Input Bridge/Filter Settings")]
    public sealed class GripFilterSettings : ScriptableObject
    {
        [Tooltip("採用するノイズフィルタの種類。迷ったら既定の「MedianThenExponentialMovingAverage」でよい。")]
        public GripFilterType filterType = GripFilterType.MedianThenExponentialMovingAverage;

        [Tooltip("移動平均/メディアンフィルタで参照するサンプル数。推奨値は3。")]
        [Min(1)]
        public int windowSize = 3;

        [Tooltip("EMA(指数移動平均)の係数。0に近いほど滑らかだが反応が遅く、1に近いほど生値に近くなる。推奨値は0.3。")]
        [Range(0.01f, 1f)]
        public float emaAlpha = 0.3f;

        [Tooltip("握り始め(ON)と判定するしきい値。offThresholdより大きくすること(ヒステリシス)。Phase 4のエッジ検出で使用する。")]
        [Range(0f, 1f)]
        public float onThreshold = 0.12f;

        [Tooltip("離した(OFF)と判定するしきい値。onThresholdより小さくすること(ヒステリシス)。Phase 4のエッジ検出で使用する。")]
        [Range(0f, 1f)]
        public float offThreshold = 0.08f;
    }
}
