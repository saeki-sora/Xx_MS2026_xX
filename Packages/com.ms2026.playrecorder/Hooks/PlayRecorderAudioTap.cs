using System;
using UnityEngine;

namespace MS2026.PlayRecorder.Hooks
{
    /// <summary>
    /// AudioListener と同じ GameObject に付けて、スピーカーに出る音をそのまま横取りコピーする係。
    /// 音は変えない（読むだけ）。<see cref="Sink"/> はオーディオスレッドから呼ばれる。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PlayRecorderAudioTap : MonoBehaviour
    {
        /// <summary>(サンプル配列, チャンネル数)。null なら何もしない。</summary>
        public volatile Action<float[], int> Sink;
        public volatile bool Paused;

        void OnAudioFilterRead(float[] data, int channels)
        {
            var sink = Sink;
            if (sink == null || Paused) return;
            sink(data, channels);
        }
    }
}
