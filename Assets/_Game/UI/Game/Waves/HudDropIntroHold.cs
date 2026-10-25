using MS2026.Stages;
using MS2026.UI;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 試合の始まりの演出（ステージの物が上から落ちてくる、<see cref="StageDropIntro"/>）の間は HUD を隠し、終わったらスッと出す。
    /// 隠すのは設定（<see cref="StageDropIntroSettings.hudScreens"/>）の画面の CanvasGroup の透明度だけ。
    /// 画面の出る動き（フェードイン）と同じ値を書くので、描く直前（<see cref="Canvas.willRenderCanvases"/>）に書いて、こちらを優先する。
    /// 演出が終わってフェードし終わったら、もう触らない。
    /// </summary>
    internal static class HudDropIntroHold
    {
        private static bool _hooked;
        private static float _fadeStart = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Unhook();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            StageDropIntro.Began -= OnBegan;
            StageDropIntro.Began += OnBegan;
        }

        private static void OnBegan()
        {
            if (!StageDropIntro.IsHudHidden)
            {
                return;
            }

            _fadeStart = -1f;
            if (!_hooked)
            {
                Canvas.willRenderCanvases += Apply;
                _hooked = true;
            }
        }

        private static void Unhook()
        {
            if (_hooked)
            {
                Canvas.willRenderCanvases -= Apply;
                _hooked = false;
            }

            _fadeStart = -1f;
        }

        private static void Apply()
        {
            if (StageDropIntro.IsHudHidden)
            {
                _fadeStart = -1f;
                SetAlpha(0f);
                return;
            }

            var seconds = StageDropIntro.Settings.hudFadeSeconds;
            if (_fadeStart < 0f)
            {
                _fadeStart = Time.unscaledTime;
            }

            var alpha = seconds > 0f ? Mathf.Clamp01((Time.unscaledTime - _fadeStart) / seconds) : 1f;
            SetAlpha(alpha * alpha * (3f - 2f * alpha));
            if (alpha >= 1f)
            {
                Unhook();
            }
        }

        private static void SetAlpha(float alpha)
        {
            var root = UiRoot.Active;
            var ids = StageDropIntro.Settings.hudScreens;
            if (root == null || ids == null)
            {
                return;
            }

            foreach (var id in ids)
            {
                var screen = string.IsNullOrEmpty(id) ? null : root.GetIfCreated(id);
                if (screen != null && screen.IsVisible && screen.TryGetComponent(out CanvasGroup group))
                {
                    group.alpha = alpha;
                }
            }
        }
    }
}
