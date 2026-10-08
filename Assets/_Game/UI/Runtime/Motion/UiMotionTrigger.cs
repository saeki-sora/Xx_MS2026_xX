namespace MS2026.UI
{
    /// <summary>動きを始めるきっかけ。</summary>
    public enum UiMotionTrigger
    {
        /// <summary>画面（またはこの部品）が出たとき。</summary>
        OnShow,

        /// <summary>画面（またはこの部品）が消えるとき。消えるのは動きが終わってから。</summary>
        OnHide,

        /// <summary>クリック・決定したとき。</summary>
        OnClick,

        /// <summary>マウスが乗ったとき・選ばれたとき。</summary>
        OnHover,

        /// <summary>押した瞬間。</summary>
        OnPress,

        /// <summary>見ている値が増えたとき（例: スコアが入った）。</summary>
        OnValueUp,

        /// <summary>見ている値が減ったとき（例: ダメージを受けた）。</summary>
        OnValueDown,

        /// <summary>表示中ずっと繰り返す（ふわふわ・点滅など、繰り返す種類の動きを選ぶ）。</summary>
        Loop,

        /// <summary>名前で呼んだとき（プログラムやボタンの設定から）。</summary>
        Manual
    }

    public static class UiMotionTriggerNames
    {
        public static string DisplayName(this UiMotionTrigger trigger) => trigger switch
        {
            UiMotionTrigger.OnShow => "出たとき",
            UiMotionTrigger.OnHide => "消えるとき",
            UiMotionTrigger.OnClick => "押したとき",
            UiMotionTrigger.OnHover => "マウスが乗ったとき",
            UiMotionTrigger.OnPress => "押した瞬間",
            UiMotionTrigger.OnValueUp => "値が増えたとき",
            UiMotionTrigger.OnValueDown => "値が減ったとき",
            UiMotionTrigger.Loop => "ずっと繰り返す",
            UiMotionTrigger.Manual => "名前で呼んだとき",
            _ => trigger.ToString()
        };
    }
}
