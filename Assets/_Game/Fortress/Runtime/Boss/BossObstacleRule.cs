using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>ボス戦で、この障害物をどうするか。</summary>
    public enum BossObstacleMode
    {
        /// <summary>画面の端（近い方の上下）へ逃がす。</summary>
        Escape = 0,

        /// <summary>残す（行き先の目印があればそこへ動く。無ければその場）。</summary>
        Keep = 1,

        /// <summary>何もしない（動かさない）。</summary>
        Ignore = 2
    }

    /// <summary>
    /// ボス戦での障害物の扱い（障害物に付ける。無くても動く。2026-11-15 ユーザー決定「少し残す・残さない物は画面の端へ」）。
    /// ステージのどの障害物にも付いていなければ、自動で <see cref="BossFormationShape.autoKeepCount"/> 個を残し、残りを画面の端へ逃がす。
    /// 1つでも付いていれば、付いていない障害物は逃がす。
    /// </summary>
    [AddComponentMenu("Fortress/ボス戦での障害物の扱い")]
    public sealed class BossObstacleRule : MonoBehaviour
    {
        [Tooltip("ボス戦でこの障害物をどうするか。")]
        public BossObstacleMode mode = BossObstacleMode.Keep;

        [Tooltip("残すときの行き先（空ならその場に残す）。")]
        public Transform keepTarget;
    }
}
