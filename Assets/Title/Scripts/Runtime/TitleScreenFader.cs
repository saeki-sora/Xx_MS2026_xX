using UnityEngine;
using UnityEngine.UI;

namespace MS2026.Title
{
    /// <summary>
    /// 画面全体を覆う1枚の板で、暗転・明転・フラッシュをする。
    /// 画面の一番手前（Screen Space - Overlay の Canvas）に置くので、カメラワークの影響を受けない。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class TitleScreenFader : MonoBehaviour
    {
        private Image image;
        private Color from;
        private Color to;
        private float duration;
        private float time;
        private bool running;

        public bool IsRunning => running;

        private Image Image => image != null ? image : image = GetComponent<Image>();

        /// <summary>すぐにこの色にする。</summary>
        public void Set(Color color)
        {
            running = false;
            Image.color = color;
            Image.enabled = color.a > 0f;
        }

        /// <summary>今の色から <paramref name="target"/> へ <paramref name="seconds"/> 秒で変える。</summary>
        public void FadeTo(Color target, float seconds)
        {
            FadeFromTo(Image.color, target, seconds);
        }

        /// <summary><paramref name="start"/> の色から <paramref name="target"/> へ変える（フラッシュは 白→透明）。</summary>
        public void FadeFromTo(Color start, Color target, float seconds)
        {
            from = start;
            to = target;
            duration = Mathf.Max(0f, seconds);
            time = 0f;
            running = true;
            Image.enabled = true;
            Step(0f);
        }

        private void Update()
        {
            if (running) Step(Time.unscaledDeltaTime);
        }

        private void Step(float dt)
        {
            time += dt;
            var t = duration <= 0f ? 1f : Mathf.Clamp01(time / duration);
            Image.color = Color.Lerp(from, to, t);
            if (t < 1f) return;
            running = false;
            Image.enabled = to.a > 0f;
        }
    }
}
