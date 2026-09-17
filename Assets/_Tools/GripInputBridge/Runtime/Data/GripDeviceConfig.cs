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
    }
}
