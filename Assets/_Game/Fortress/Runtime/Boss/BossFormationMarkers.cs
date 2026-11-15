using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ステージごとのボス戦の配置の目印（無くても動く。2026-11-15 ユーザー決定「自動で作って、目印で直せる」）。
    /// ステージのシーンに1つ置く。空の目印は自動の値を使う。ボススタジオから置く・動かす。
    /// </summary>
    [AddComponentMenu("Fortress/ボス戦の配置の目印")]
    public sealed class BossFormationMarkers : MonoBehaviour
    {
        [Tooltip("コアの場所（空なら自動）。")]
        public Transform core;

        [Tooltip("P1〜P4 の砲台の場所（空なら自動。P1 が上）。")]
        public Transform[] turrets = new Transform[4];

        [Tooltip("ボスが止まる場所（上下にゆれる真ん中。空なら自動）。")]
        public Transform bossStop;

        [Tooltip("ボスが入ってくる前にいる場所（画面の外。空なら自動）。")]
        public Transform bossEntry;

        [Tooltip("ボスの中心がこの目印の X より左に来たら「コアに届いた」（空なら自動）。")]
        public Transform reachLine;

        /// <summary>今読み込まれているステージの目印（無ければ null）。</summary>
        public static BossFormationMarkers Find() => FindFirstObjectByType<BossFormationMarkers>();

        /// <summary>自動の配置に、置いてある目印を上書きする。</summary>
        public BossFormationLayout Apply(BossFormationLayout layout)
        {
            if (core != null)
            {
                layout.core = core.position;
            }

            if (turrets != null && layout.turrets != null)
            {
                for (var i = 0; i < Mathf.Min(turrets.Length, layout.turrets.Length); i++)
                {
                    if (turrets[i] != null)
                    {
                        layout.turrets[i] = turrets[i].position;
                    }
                }
            }

            if (bossStop != null)
            {
                layout.bossStop = bossStop.position;
            }

            if (bossEntry != null)
            {
                layout.bossEntry = bossEntry.position;
            }

            if (reachLine != null)
            {
                layout.reachCenterX = reachLine.position.x;
            }

            return layout;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.4f, 0.8f, 0.9f);
            if (core != null)
            {
                Gizmos.DrawWireSphere(core.position, 1f);
            }

            if (turrets != null)
            {
                foreach (var t in turrets)
                {
                    if (t != null)
                    {
                        Gizmos.DrawWireCube(t.position, Vector3.one * 0.8f);
                    }
                }
            }

            if (bossStop != null)
            {
                Gizmos.DrawWireSphere(bossStop.position, 2f);
            }
        }
    }
}
