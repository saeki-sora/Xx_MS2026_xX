using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験】CPU の移動の設定（Resources/CpuMoveSettings。無ければ初期値）。移動が不採用になったら、このフォルダごと消す。
    /// 強さで変わる値は「4つの目安」（<see cref="CpuLevelCurve"/>。初めて遊ぶ人／少し慣れた人／上手い人／とても上手い人）で持ち、
    /// 分野「動き」の上手さの目盛り（強さ + 慣れ）で決める。
    /// </summary>
    [CreateAssetMenu(fileName = "CpuMoveSettings", menuName = "MS2026/CPU/CPU の移動の設定（実験）")]
    public sealed class CpuMoveSettings : ScriptableObject
    {
        public const string ResourcePath = "CpuMoveSettings";

        [Header("敵を避ける")]
        [Tooltip("すぐそば（射程を 10 等分した、いちばん内側の輪）の敵をどれだけ避けるか（4つの目安）。")]
        public CpuLevelCurve avoidWeight = new CpuLevelCurve(0.4f, 0.8f, 1.2f, 1.5f);

        [Tooltip("ひとつ外側の輪の敵をどれだけ数えるか（すぐそばを 1 として）。")]
        [Range(0f, 1f)]
        public float outerRingShare = 0.35f;

        [Tooltip("この数（重み）の敵が近くにいたら、全速で避ける。")]
        [Min(0.5f)]
        public float threatFullCount = 6f;

        [Tooltip("この数より少なければ、避けない（少しの敵は撃って倒す。4つの目安）。")]
        public CpuLevelCurve threatIgnoreCount = new CpuLevelCurve(3f, 2f, 1f, 0.5f);

        [Header("席のまわりにいる")]
        [Tooltip("席からこの距離までは、戻ろうとしない（m）。")]
        [Min(0f)]
        public float homeFreeRadius = 2.5f;

        [Tooltip("自由な距離を超えてから、この距離だけ離れると全速で戻る（m）。")]
        [Min(0.1f)]
        public float homePullRange = 3f;

        [Tooltip("席へ戻る強さ。")]
        [Range(0f, 3f)]
        public float homeWeight = 1f;

        [Header("動き方")]
        [Tooltip("行き先を決め直す間隔（秒）。長いほど反応が遅い（4つの目安）。")]
        public CpuLevelCurve planInterval = new CpuLevelCurve(1f, 0.6f, 0.35f, 0.2f);

        [Tooltip("行きたい向きが変わる速さ（大きいほどキビキビ向きを変える。4つの目安）。")]
        public CpuLevelCurve turnResponse = new CpuLevelCurve(2f, 4f, 6f, 9f);

        [Tooltip("この長さより短い「行きたい向き」は、動かないのと同じにする（細かく震えないように）。")]
        [Range(0f, 0.5f)]
        public float deadZone = 0.15f;

        [Tooltip("この数（重み）以上の敵がすぐそばにいる間を「囲まれた」として記録する。")]
        [Min(0.5f)]
        public float surroundedCount = 4f;

        private static CpuMoveSettings _fallback;

        public static CpuMoveSettings Load()
        {
            var settings = Resources.Load<CpuMoveSettings>(ResourcePath);
            if (settings != null)
            {
                return settings;
            }

            if (_fallback == null)
            {
                _fallback = CreateInstance<CpuMoveSettings>();
                _fallback.hideFlags = HideFlags.HideAndDontSave;
            }

            return _fallback;
        }
    }
}
