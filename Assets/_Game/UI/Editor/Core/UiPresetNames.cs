using System.Collections.Generic;
using DDrive.Runtime.Ui;

namespace MS2026.UI.EditorTools
{
    /// <summary>D-Drive の定番の動き（UiPreset）の日本語名と分類。動きのページ・選択欄で使う。</summary>
    public static class UiPresetNames
    {
        public enum Group
        {
            Appear,
            Disappear,
            Loop,
            Emphasis
        }

        private static readonly Dictionary<UiPreset, string> Names = new Dictionary<UiPreset, string>
        {
            { UiPreset.None, "なし" },
            { UiPreset.FadeIn, "ふわっと出る" },
            { UiPreset.SlideInLeft, "左から滑り込む" },
            { UiPreset.SlideInRight, "右から滑り込む" },
            { UiPreset.SlideInTop, "上から滑り込む" },
            { UiPreset.SlideInBottom, "下から滑り込む" },
            { UiPreset.ScaleIn, "大きくなって出る" },
            { UiPreset.PopIn, "ポンと出る" },
            { UiPreset.BounceIn, "弾んで出る" },
            { UiPreset.ElasticIn, "びよーんと出る" },
            { UiPreset.FlipInX, "縦にめくれて出る" },
            { UiPreset.FlipInY, "横にめくれて出る" },
            { UiPreset.RotateIn, "回りながら出る" },
            { UiPreset.ZoomInFade, "寄りながら出る" },
            { UiPreset.SlideFadeInLeft, "左からすっと出る" },
            { UiPreset.SlideFadeInRight, "右からすっと出る" },
            { UiPreset.SlideFadeInTop, "上からすっと出る" },
            { UiPreset.SlideFadeInBottom, "下からすっと出る" },
            { UiPreset.ExpandWidth, "横に開く" },
            { UiPreset.ExpandHeight, "縦に開く" },
            { UiPreset.TypeFillIn, "塗られて出る" },
            { UiPreset.FadeOut, "ふわっと消える" },
            { UiPreset.SlideOutLeft, "左へ抜ける" },
            { UiPreset.SlideOutRight, "右へ抜ける" },
            { UiPreset.SlideOutTop, "上へ抜ける" },
            { UiPreset.SlideOutBottom, "下へ抜ける" },
            { UiPreset.ScaleOut, "小さくなって消える" },
            { UiPreset.PopOut, "ポンと消える" },
            { UiPreset.BounceOut, "弾んで消える" },
            { UiPreset.ElasticOut, "びよーんと消える" },
            { UiPreset.FlipOutX, "縦にめくれて消える" },
            { UiPreset.FlipOutY, "横にめくれて消える" },
            { UiPreset.RotateOut, "回りながら消える" },
            { UiPreset.ZoomOutFade, "引きながら消える" },
            { UiPreset.SlideFadeOutLeft, "左へすっと消える" },
            { UiPreset.SlideFadeOutRight, "右へすっと消える" },
            { UiPreset.SlideFadeOutTop, "上へすっと消える" },
            { UiPreset.SlideFadeOutBottom, "下へすっと消える" },
            { UiPreset.CollapseWidth, "横に閉じる" },
            { UiPreset.CollapseHeight, "縦に閉じる" },
            { UiPreset.Pulse, "どくどく" },
            { UiPreset.Blink, "点滅" },
            { UiPreset.Float, "ふわふわ上下" },
            { UiPreset.Sway, "ゆらゆら左右" },
            { UiPreset.Breathe, "ゆっくり呼吸" },
            { UiPreset.RotateLoop, "くるくる回る" },
            { UiPreset.ShimmerAlpha, "きらめく" },
            { UiPreset.RainbowTint, "虹色に変わる" },
            { UiPreset.WobbleLoop, "ぷるぷる" },
            { UiPreset.PunchScale, "ぽんっと弾む" },
            { UiPreset.PunchRotation, "くいっと傾く" },
            { UiPreset.Shake, "ぶるっと震える" },
            { UiPreset.ShakeHard, "激しく震える" },
            { UiPreset.Flash, "ぴかっと光る" },
            { UiPreset.ColorFlash, "色がぱっと変わる" },
            { UiPreset.HeartBeat, "どきっ" },
            { UiPreset.Jelly, "ぷにっ" },
            { UiPreset.Tada, "じゃーん" },
            { UiPreset.RubberBand, "ゴムのように伸びる" },
            { UiPreset.AttentionJump, "ぴょんと跳ねる" }
        };

        public static string Name(UiPreset preset) => Names.TryGetValue(preset, out var name) ? name : preset.ToString();

        /// <summary>選択欄に出す名前（例: 「ポンと出る（PopIn）」）。</summary>
        public static string Label(UiPreset preset) => preset == UiPreset.None ? "なし" : $"{Name(preset)}（{preset}）";

        public static Group GroupOf(UiPreset preset)
        {
            var value = (int)preset;
            if (value <= (int)UiPreset.TypeFillIn) return Group.Appear;
            if (value <= (int)UiPreset.CollapseHeight) return Group.Disappear;
            if (value <= (int)UiPreset.WobbleLoop) return Group.Loop;
            return Group.Emphasis;
        }

        public static string GroupLabel(Group group) => group switch
        {
            Group.Appear => "出る",
            Group.Disappear => "消える",
            Group.Loop => "ずっと繰り返す",
            _ => "目立たせる（1回）"
        };

        /// <summary>この動きに合うきっかけ（動きを足すときの初期値）。</summary>
        public static UiMotionTrigger SuggestedTrigger(UiPreset preset) => GroupOf(preset) switch
        {
            Group.Appear => UiMotionTrigger.OnShow,
            Group.Disappear => UiMotionTrigger.OnHide,
            Group.Loop => UiMotionTrigger.Loop,
            _ => UiMotionTrigger.OnClick
        };
    }
}
