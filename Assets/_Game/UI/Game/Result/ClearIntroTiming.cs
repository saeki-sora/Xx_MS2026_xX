using UnityEngine;

namespace MS2026.UI.Game
{
    public static class ClearIntroTiming
    {
        public const float ZoomEnd = 0.8f;
        public const float FillStart = 2.9f;
        // 埋まる所要時間を0.9秒から1.8秒へ。以降の各演出も0.9秒ずらす。
        public const float Covered = 4.7f;
        public const float OpenStart = 5.5f;
        public const float TitleReady = 6.9f;
        public const float NoiseStart = 9.3f;
        public const float Finished = 10.7f;
        public const float CoreSlideSeconds = 1.05f;
        public const float CoreSlideStart = Finished - CoreSlideSeconds;
        public static float CoreSlide(float time) => Mathf.InverseLerp(CoreSlideStart, Finished, time);

        public static float Zoom(float time) => Mathf.SmoothStep(0f, 1f, time / ZoomEnd);
        public static float Fill(float time) => Mathf.InverseLerp(FillStart, Covered, time);
        public static float Open(float time) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OpenStart, TitleReady, time));
        public static float Noise(float time) => Mathf.InverseLerp(NoiseStart, Finished, time);
    }
}
