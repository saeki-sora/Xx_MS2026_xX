using System;
using System.Collections;
using UnityEngine;

namespace MS2026.PlayRecorder.Hooks
{
    /// <summary>
    /// 録画中だけ、見えない GameObject に付く係。毎フレームの描画が終わった瞬間（WaitForEndOfFrame）と Update を知らせる。
    /// エディタ専用アセンブリの MonoBehaviour は GameObject に付けられないため、この 2 つの係だけ別アセンブリ
    /// （defineConstraints: UNITY_EDITOR ＝ エディタでだけコンパイルされ、ゲームのビルドには入らない）に置いている。
    /// </summary>
    [AddComponentMenu("")]
    public sealed class PlayRecorderCapture : MonoBehaviour
    {
        public Action EndOfFrame;
        public Action Tick;

        IEnumerator Start()
        {
            var endOfFrame = new WaitForEndOfFrame();
            while (true)
            {
                yield return endOfFrame;
                EndOfFrame?.Invoke();
            }
        }

        void Update() => Tick?.Invoke();
    }
}
