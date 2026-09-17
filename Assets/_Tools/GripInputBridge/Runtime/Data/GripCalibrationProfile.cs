using UnityEngine;

namespace MS2026.GripInputBridge.Data
{
    /// <summary>
    /// プレイヤー1人分のキャリブレーション情報。「子供と大人が対等に対戦できる」を実現する
    /// 握力の相対値化はここで行う。設計書 7.2・7.4 参照。
    /// </summary>
    [CreateAssetMenu(fileName = "GripCalibrationProfile", menuName = "MS2026/Grip Input Bridge/Calibration Profile")]
    public sealed class GripCalibrationProfile : ScriptableObject
    {
        [Tooltip("このプロファイルが対応するプレイヤー番号(0-3)。")]
        [Range(0, 3)]
        public int playerIndex;

        [Tooltip("較正で記録した最小握力の生値(通常は0のままでよい)。")]
        public float minRawValue;

        [Tooltip("較正で記録した最大握力の生値。キャリブレーションウィザード(Phase 6)で計測して設定する。")]
        public float maxRawValue = 1f;

        [Tooltip("min-max正規化後にさらに通す応答カーブ。線形のままだと「渾身」域が窮屈に感じやすいため、必要に応じて調整する。")]
        public AnimationCurve responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("この値以下は0とみなす(センサー個体差の暗電流ノイズ吸収)。")]
        [Range(0f, 0.3f)]
        public float deadZone = 0.03f;

        [Tooltip("最後にキャリブレーションを実行した日時(ISO8601文字列)。運用ログ用で、計算には使わない。")]
        public string lastCalibratedAt = string.Empty;

        /// <summary>
        /// フィルタ適用後の生値を、このプレイヤーの較正情報を使って0-1の正規化値に変換する。
        /// 適用順序は 設計書7.2 のとおり「デッドゾーン → min-max正規化 → 応答カーブ」。
        /// </summary>
        public float Normalize(float filteredRawValue)
        {
            // デッドゾーンは較正前の生値スケールで扱う(暗電流ノイズは個人差より前の話のため)。
            var deadZoned = filteredRawValue <= deadZone ? 0f : filteredRawValue;

            var range = maxRawValue - minRawValue;
            var linear = Mathf.Approximately(range, 0f)
                ? 0f
                : Mathf.Clamp01((deadZoned - minRawValue) / range);

            var curved = responseCurve != null ? responseCurve.Evaluate(linear) : linear;
            return Mathf.Clamp01(curved);
        }

        /// <summary>
        /// 較正アセットが用意されていないプレイヤー向けの既定プロファイル(線形・デッドゾーン0.03)を生成する。
        /// 開発初期やテストで、実際のキャリブレーションが未実施でも動作を止めないために使う。
        /// </summary>
        public static GripCalibrationProfile CreateDefault(int playerIndex)
        {
            var profile = CreateInstance<GripCalibrationProfile>();
            profile.playerIndex = playerIndex;
            profile.minRawValue = 0f;
            profile.maxRawValue = 1f;
            profile.responseCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            profile.deadZone = 0.03f;
            return profile;
        }
    }
}
