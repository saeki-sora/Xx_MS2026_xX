using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// 目印: この物（と子）は、試合の始まりの演出（<see cref="StageDropIntro"/>）で落とさず、最初から置いておく。
    /// 大きな背景の壁など、落ちてくると変に見える物に付ける。
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("MS2026/ステージ/開始の演出で落とさない")]
    public sealed class StageDropIntroSkip : MonoBehaviour
    {
        [Tooltip("メモ（なぜ落とさないか。ゲームには影響しない）。")]
        [TextArea(1, 3)]
        public string memo;
    }
}
