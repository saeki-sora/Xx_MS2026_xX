using UnityEngine;

namespace MS2026.Cpu.Experimental
{
    /// <summary>
    /// 【実験】CPU の移動の設定（Resources/CpuMoveSettings。無ければ初期値）。移動が不採用になったら、このフォルダごと消す。
    /// 強さごとの分け方（段6）は、本体の上手さと同じ仕組みで「動き」の分野として足す。
    /// </summary>
    [CreateAssetMenu(fileName = "CpuMoveSettings", menuName = "MS2026/CPU/CPU の移動の設定（実験）")]
    public sealed class CpuMoveSettings : ScriptableObject
    {
        public const string ResourcePath = "CpuMoveSettings";

        [Header("敵を避ける")]
        [Tooltip("すぐそば（射程を 10 等分した、いちばん内側の輪）の敵をどれだけ避けるか。")]
        [Range(0f, 3f)]
        public float avoidWeight = 1.2f;

        [Tooltip("ひとつ外側の輪の敵をどれだけ数えるか（すぐそばを 1 として）。")]
        [Range(0f, 1f)]
        public float outerRingShare = 0.35f;

        [Tooltip("この数（重み）の敵が近くにいたら、全速で避ける。")]
        [Min(0.5f)]
        public float threatFullCount = 6f;

        [Tooltip("この数より少なければ、避けない（少しの敵は撃って倒す）。")]
        [Min(0f)]
        public float threatIgnoreCount = 1f;

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
        [Tooltip("行き先を決め直す間隔（秒）。長いほど反応が遅い。")]
        [Range(0.05f, 1.5f)]
        public float planInterval = 0.3f;

        [Tooltip("行きたい向きが変わる速さ（大きいほどキビキビ向きを変える）。")]
        [Range(0.5f, 30f)]
        public float turnResponse = 6f;

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
