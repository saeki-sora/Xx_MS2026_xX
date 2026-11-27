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
        MedianThenExponentialMovingAverage,

        /// <summary>
        /// 1€フィルター。ゆっくり動くときは強くならし、速く動くときは遅れないように自動で切り替える。
        /// 経過時間で計算するので、フレームレートが違うPCでも同じ効き方になる。
        /// （列挙の値はアセットに数字で保存されるので、新しい種類は必ず末尾に足す）
        /// </summary>
        OneEuro,

        /// <summary>先にメディアンでスパイクを落とし、次に1€フィルターでならす。1€を使うならこちらがおすすめ。</summary>
        MedianThenOneEuro,

        /// <summary>フィルターを掛けない（生の値のまま）。設定アセットが無いときと同じ。</summary>
        None
    }

    /// <summary>
    /// ノイズフィルタの調整値をまとめたアセット。プレイヤー間で共有する
    /// （プレイヤーごとの個人差は <see cref="GripCalibrationProfile"/> 側で吸収する）。
    /// 設計書 7.4 データモデル 参照。
    /// </summary>
    [CreateAssetMenu(fileName = "GripFilterSettings", menuName = "MS2026/Grip Input Bridge/Filter Settings")]
    public sealed class GripFilterSettings : ScriptableObject
    {
        /// <summary>
        /// ゲームが起動時に読み込む設定の Resources 内の場所。ここにアセットがあればそのフィルターを使い、
        /// 無ければフィルター無し（生の値のまま）で動く。握力入力ブリッジの「なめらかさ」タブで作る。
        /// </summary>
        public const string GameSettingsResourcePath = "GripInputBridge/GripFilterSettings";

        [Tooltip("採用するノイズフィルタの種類。迷ったら既定の「MedianThenExponentialMovingAverage」でよい。")]
        public GripFilterType filterType = GripFilterType.MedianThenExponentialMovingAverage;

        [Tooltip("移動平均/メディアンフィルタで参照するサンプル数。推奨値は3。")]
        [Min(1)]
        public int windowSize = 3;

        [Tooltip("EMA(指数移動平均)の係数。0に近いほど滑らかだが反応が遅く、1に近いほど生値に近くなる。推奨値は0.3。")]
        [Range(0.01f, 1f)]
        public float emaAlpha = 0.3f;

        [Tooltip("1€フィルター: ゆっくり動くときのならし(Hz)。小さいほど震えが消えるが、ゆっくりした動きにも少し遅れる。推奨値は1.5。")]
        [Range(0.1f, 10f)]
        public float oneEuroMinCutoff = 1.5f;

        [Tooltip("1€フィルター: 速さへの反応。大きいほど、ぐっと握った瞬間の遅れが減る(そのぶん速い揺れも通る)。推奨値は2。")]
        [Range(0f, 20f)]
        public float oneEuroBeta = 2f;

        [Tooltip("1€フィルター: 速さの見積もりをならす強さ(Hz)。LANで届く値の段々を「速い動き」と取り違えないようにする。推奨値は1。")]
        [Range(0.1f, 10f)]
        public float oneEuroDerivativeCutoff = 1f;

        [Tooltip("握り始め(ON)と判定するしきい値。offThresholdより大きくすること(ヒステリシス)。Phase 4のエッジ検出で使用する。")]
        [Range(0f, 1f)]
        public float onThreshold = 0.12f;

        [Tooltip("離した(OFF)と判定するしきい値。onThresholdより小さくすること(ヒステリシス)。Phase 4のエッジ検出で使用する。")]
        [Range(0f, 1f)]
        public float offThreshold = 0.08f;
    }
}
