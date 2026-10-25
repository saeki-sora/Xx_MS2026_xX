using MS2026.Fortress;
using MS2026.UI;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 画面の切り替えの幕（UIスタジオの幕）が動いている間は「まだ準備ができていない」と、ウェーブの係に知らせる。
    /// 全員の幕が開いてから「WAVE 1！」を出すため（読み込みの遅いPCがいても、敵が先に来ないように）。
    /// </summary>
    internal static class WaveReadyHooks
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            FortressWaves.AddReadyBlocker(IsTransitionPlaying);
            // 幕が開ききってから、試合の始まりの演出（ステージの物が落ちてくる）を始めるため。
            FortressScreenCover.Add(IsTransitionPlaying);
        }

        private static bool IsTransitionPlaying()
        {
            var root = UiRoot.Active;
            return root != null && root.Overlay != null && root.Overlay.IsPlaying;
        }
    }
}
