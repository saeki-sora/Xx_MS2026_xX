using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Stages;
using MS2026.UI;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>
    /// 試合の始まりの演出（ステージの物が上から落ちてくる、<see cref="StageDropIntro"/>）の間は HUD を隠し、終わったらスッと出す。
    /// 2026-11-11 から、落ちてきた後も「コアを守れ！」→ WAVE 1 の発表が終わるまで隠したまま（<see cref="FortressWaves.IsMatchIntro"/>）。
    /// その間も、始まりの札（「コアを守れ！」・発表・NEW ENEMY・ふちの矢印・3・2・1）だけは見せる（親の透明度を無視させる）。
    /// 隠すのは設定（<see cref="StageDropIntroSettings.hudScreens"/>）の画面の CanvasGroup の透明度だけ。
    /// 画面の出る動き（フェードイン）と同じ値を書くので、描く直前（<see cref="Canvas.willRenderCanvases"/>）に書いて、こちらを優先する。
    /// 演出が終わってフェードし終わったら、もう触らない。
    /// </summary>
    internal static class HudDropIntroHold
    {
        // HUD を隠している間も見せる部品（HUD の画面のすぐ下の名前。UIスタジオの「ウェーブの表示を入れる」が作る）。
        private static readonly string[] IntroParts = { "WaveOpening", "WaveAnnounce", "WaveNewEnemies", "WaveEdgeMarkers", "WaveCountdown" };

        private static readonly List<CanvasGroup> PassedThrough = new List<CanvasGroup>();

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

        // 物が落ちている間と、その後の「コアを守れ！」・WAVE 1 の発表が終わるまで。
        private static bool IsHidden => StageDropIntro.IsHudHidden || (StageDropIntro.Settings.hideHud && FortressWaves.IsActive && FortressWaves.IsMatchIntro);

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

            SetPassThrough(false);
            _fadeStart = -1f;
        }

        private static void Apply()
        {
            if (IsHidden)
            {
                _fadeStart = -1f;
                SetAlpha(0f);
                // 物が落ちている間は何も見せない。落ちてきた後は、始まりの札だけ見せる。
                SetPassThrough(!StageDropIntro.IsHudHidden);
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

        // 始まりの札に、HUD の画面の透明度を無視させる（on）／元に戻す（off）。
        private static void SetPassThrough(bool on)
        {
            if (!on)
            {
                foreach (var group in PassedThrough)
                {
                    if (group != null)
                    {
                        group.ignoreParentGroups = false;
                    }
                }

                PassedThrough.Clear();
                return;
            }

            if (PassedThrough.Count > 0)
            {
                return;
            }

            var root = UiRoot.Active;
            var ids = StageDropIntro.Settings.hudScreens;
            if (root == null || ids == null)
            {
                return;
            }

            foreach (var id in ids)
            {
                var screen = string.IsNullOrEmpty(id) ? null : root.GetIfCreated(id);
                if (screen == null)
                {
                    continue;
                }

                foreach (var name in IntroParts)
                {
                    var part = screen.transform.Find(name);
                    if (part == null)
                    {
                        continue;
                    }

                    if (!part.TryGetComponent(out CanvasGroup group))
                    {
                        group = part.gameObject.AddComponent<CanvasGroup>();
                    }

                    group.ignoreParentGroups = true;
                    PassedThrough.Add(group);
                }
            }
        }
    }
}
