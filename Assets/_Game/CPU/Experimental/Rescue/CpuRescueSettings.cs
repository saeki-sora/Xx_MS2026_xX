using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験】CPU の「救助」「抜け出す」の設定（Resources/CpuRescueSettings。無ければ初期値）。倒れる仕組みが不採用になったら消す。
    /// 強さで変わる値は「4つの目安」（<see cref="CpuLevelCurve"/>）で持ち、分野「救助」の上手さの目盛り（強さ + 慣れ）で決める。
    /// </summary>
    [CreateAssetMenu(fileName = "CpuRescueSettings", menuName = "MS2026/CPU/CPU の救助の設定（実験）")]
    public sealed class CpuRescueSettings : ScriptableObject
    {
        public const string ResourcePath = "CpuRescueSettings";

        [Header("救助（味方にビームを当てる）")]
        [Tooltip("倒れた仲間を助けに行く気持ちの強さ（0 = 助けない、1 = 見えたら必ず助ける。4つの目安）。")]
        public CpuLevelCurve reviveAttention = new CpuLevelCurve(0.3f, 0.6f, 0.85f, 1f);

        [Tooltip("弱っている（汚れている）仲間を洗いに行く気持ちの強さ（0 = 洗わない。4つの目安）。")]
        public CpuLevelCurve healAttention = new CpuLevelCurve(0f, 0.2f, 0.5f, 0.8f);

        [Tooltip("仲間の体力がこれより低いと、洗いに行く。")]
        [Range(0f, 1f)]
        public float healBelowHealth = 0.6f;

        [Tooltip("ほかの人がもう助けているときの気持ちの倍率（0 = 任せる、1 = 一緒に助ける。4つの目安。上手いほど空いている仲間へ回る）。")]
        public CpuLevelCurve shareWithHelpers = new CpuLevelCurve(1f, 0.7f, 0.4f, 0.25f);

        [Tooltip("仲間に当てるとみなす、ビームの線から仲間までの距離（m。体の半径くらい）。")]
        [Range(0.1f, 2f)]
        public float hitReach = 0.6f;

        [Header("抜け出す（倒れたとき）")]
        [Tooltip("倒れたときの「にぎにぎ」の速さ（1 秒の回数。4つの目安）。")]
        public CpuLevelCurve strugglePressesPerSecond = new CpuLevelCurve(2f, 3f, 4f, 5.5f);

        private static CpuRescueSettings _fallback;

        public static CpuRescueSettings Load()
        {
            var settings = Resources.Load<CpuRescueSettings>(ResourcePath);
            if (settings != null)
            {
                return settings;
            }

            if (_fallback == null)
            {
                _fallback = CreateInstance<CpuRescueSettings>();
                _fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _fallback;
        }
    }
}
