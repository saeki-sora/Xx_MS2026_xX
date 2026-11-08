using MS2026.Fortress;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// コアのページ: HP を変えて見た目を試す（<see cref="CoreDangerLook.PreviewHealth01"/>。見た目だけで、本当の HP は変えない）と、
    /// ダメージの白い光・赤い輪を試すボタン。
    /// </summary>
    public static class CorePreviewPanel
    {
        private const double AnimateSeconds = 1.5;
        private static double _animateUntil;

        public static VisualElement Build(CoreCrystalController core)
        {
            var card = StudioUi.Card();
            card.Add(StudioUi.Section("試す", "見た目だけを変えます。本当の HP は変わりません。"));

            var slider = new Slider("HP を試す", 0f, 1f)
            {
                value = CoreDangerLook.PreviewHealth01 >= 0f ? CoreDangerLook.PreviewHealth01 : 1f,
                showInputField = true,
                tooltip = "この HP のときの見た目にします（歯の汚れ・ひび・光と、画面が赤くなる演出）。"
            };
            slider.RegisterValueChangedCallback(e =>
            {
                CoreDangerLook.PreviewHealth01 = e.newValue;
                Animate();
            });
            card.Add(slider);
            card.Add(StudioUi.Row(
                StudioUi.Button("試すのをやめる", () =>
                {
                    CoreDangerLook.PreviewHealth01 = -1f;
                    slider.SetValueWithoutNotify(1f);
                    Animate();
                }, "本当の HP の見た目に戻します。", small: true),
                StudioUi.Button("ダメージの光を試す", () =>
                {
                    if (core.TryGetComponent<CoreToothLook>(out var look))
                    {
                        look.PlayHit();
                    }

                    if (Application.isPlaying && core.TryGetComponent<CoreHitRings>(out var rings))
                    {
                        rings.PlayHit();
                    }

                    Animate();
                }, "ダメージを受けた瞬間の白い光を出します（赤い輪は Play 中だけ）。", small: true)));
            return card;
        }

        // 編集中はシーンが変わったときしか更新されないので、少しの間シーンビューを動かし続ける（脈・白い光が見えるように）。
        private static void Animate()
        {
            if (_animateUntil <= EditorApplication.timeSinceStartup)
            {
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
            }

            _animateUntil = EditorApplication.timeSinceStartup + AnimateSeconds;
        }

        private static void Tick()
        {
            EditorApplication.QueuePlayerLoopUpdate();
            SceneView.RepaintAll();
            if (EditorApplication.timeSinceStartup > _animateUntil)
            {
                EditorApplication.update -= Tick;
            }
        }
    }
}
