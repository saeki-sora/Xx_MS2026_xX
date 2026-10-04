using UnityEngine;

namespace MS2026.GripInputBridge.Data
{
    /// <summary>
    /// 実機シリアル接続の設定値。設計書 6.2・7.4 参照。
    /// 本番当日用・開発用で別アセットを用意し、差し替えるだけで運用できるようにする(C-05)。
    /// </summary>
    [CreateAssetMenu(fileName = "GripDeviceConfig", menuName = "MS2026/Grip Input Bridge/Device Config")]
    public sealed class GripDeviceConfig : ScriptableObject
    {
        [Tooltip("接続先のCOMポート名(例: COM3)。Windowsのデバイスマネージャーで確認できる。")]
        public string serialPortName = "COM3";

        [Tooltip("マイコン側と一致させるボーレート。")]
        public int baudRate = 115200;

        [Tooltip("この時間(ミリ秒)以上データが来なければ「切断」とみなす。設計書6.2の推奨値は200ms。")]
        [Min(1)]
        public int heartbeatTimeoutMs = 200;

        [Tooltip("将来のC-01(自動割り当て)向けの予約フラグ。現バージョンでは未実装で効果を持たない。")]
        public bool autoAssignPlayerIndex;

        [Header("センサー特性")]
        [Tooltip("ONなら「中心値からどれだけ離れたか」を握力の生値とする。" +
                 "何もしていないときに中間の値(Unoなら約512)を出し、磁石のN極/S極で上下に振れる" +
                 "リニアホールセンサー(49E等)向け。どちらの極を近づけても反応する。")]
        public bool centeredSensor;

        [Tooltip("centeredSensorがONのとき、何もしていない状態の値(0-1)。Unoの512なら0.5。")]
        [Range(0.01f, 0.99f)]
        public float sensorCenter01 = 0.5f;

        /// <summary>
        /// 受信した値(0-1)を、握力の生値(0=握っていない、1=最大)に変換する。
        /// <see cref="centeredSensor"/> がOFFなら何もしない。
        /// ONなら中心からの距離を、中心から端までの距離で割って0-1にする。
        /// </summary>
        public float ToGripRawValue(float receivedValue01)
        {
            if (!centeredSensor)
            {
                return receivedValue01;
            }

            var center = Mathf.Clamp(sensorCenter01, 0.01f, 0.99f);
            var maxDistance = Mathf.Max(center, 1f - center);
            return Mathf.Clamp01(Mathf.Abs(receivedValue01 - center) / maxDistance);
        }
    }
}
