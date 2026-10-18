using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 以前の作り方でPlayの外に残ってしまった爆弾の「爆発の輪」（コアの近くのオレンジの輪）を、エディタで自動的に消す。
    /// スクリプトを読み込んだ直後と、Playを止めたときに <see cref="BombBlastRing.DestroyLeftovers"/> を呼ぶ。
    /// </summary>
    [InitializeOnLoad]
    internal static class BombBlastRingCleanup
    {
        static BombBlastRingCleanup()
        {
            EditorApplication.delayCall += Cleanup;
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    Cleanup();
                }
            };
        }

        private static void Cleanup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var count = BombBlastRing.DestroyLeftovers();
            if (count > 0)
            {
                Debug.Log($"[Fortress] 残っていた爆発の輪を {count} 個消しました。");
                SceneView.RepaintAll();
            }
        }
    }
}
