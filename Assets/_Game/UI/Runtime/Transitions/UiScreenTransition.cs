using UnityEngine;
using SeId = DDrive.Foundation.Identity.AssetId<DDrive.Runtime.Audio.SeMarker>;

namespace MS2026.UI
{
    /// <summary>画面切り替えの幕の模様。</summary>
    public enum UiTransitionPattern
    {
        /// <summary>全体がじわっと暗くなる。</summary>
        Fade = 0,

        /// <summary>一方向から塗りつぶす。</summary>
        Wipe = 1,

        /// <summary>まるく閉じる（中心へ向かって）。</summary>
        Iris = 2,

        /// <summary>ブラインドのように何本もの帯で閉じる。</summary>
        Blinds = 3,

        /// <summary>ひし形のタイルが波のように閉じる。</summary>
        Diamonds = 4,

        /// <summary>白黒の画像（ルール画像）の暗い所から順に塗る。好きな画像で自由な模様を作れる。</summary>
        RuleTexture = 5,

        /// <summary>ふちが光りながら焼けるように閉じる（このゲームらしい「灼ける」）。</summary>
        Burn = 6
    }

    /// <summary>
    /// 画面切り替えの幕（全画面のワイプなど）の設定1つ。閉じる（覆う）→ 裏で画面を入れ替える → 開く。
    /// UIスタジオの「画面切り替え」ページで選び・試せる。好きなだけ作れる。
    /// </summary>
    [CreateAssetMenu(menuName = "UI/Screen Transition", fileName = "UiScreenTransition")]
    public sealed class UiScreenTransition : ScriptableObject
    {
        [Tooltip("ツールに出す名前。")]
        public string displayName = "新しい切り替え";

        [Tooltip("幕の模様。")]
        public UiTransitionPattern pattern = UiTransitionPattern.Wipe;

        [Tooltip("幕の色。")]
        public Color color = new Color(0.07f, 0.07f, 0.09f, 1f);

        [Tooltip("閉じる（覆う）のにかける秒数。")]
        [Min(0.01f)] public float coverSeconds = 0.35f;

        [Tooltip("覆ったまま待つ秒数（画面の入れ替えが終わるのを待つ時間とは別に、間を作りたいとき）。")]
        [Min(0f)] public float holdSeconds = 0.05f;

        [Tooltip("開く（現れる）のにかける秒数。")]
        [Min(0.01f)] public float revealSeconds = 0.35f;

        [Tooltip("動き方の緩急（横=時間、縦=進み具合）。")]
        public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("ワイプ・ブラインド・ひし形の向き（度。0=左→右、90=下→上）。")]
        public float angle;

        [Tooltip("ONなら、開くときは閉じたのと同じ向きへ幕が通り過ぎていく。OFFなら来た向きへ戻っていく。")]
        public bool revealPassThrough = true;

        [Tooltip("ブラインドの帯の数／ひし形の細かさ。")]
        [Range(1f, 40f)] public float count = 8f;

        [Tooltip("境目のぼかし。")]
        [Range(0.001f, 0.5f)] public float softness = 0.06f;

        [Tooltip("「まるく閉じる」「灼ける」の中心（画面の割合。0.5,0.5=真ん中）。")]
        public Vector2 center = new Vector2(0.5f, 0.5f);

        [Tooltip("ルール画像（白黒）。暗い所から先に覆われる。")]
        public Texture2D ruleTexture;

        [Tooltip("ONなら、覆う順番を逆にする。")]
        public bool invert;

        [Tooltip("「灼ける」のふちの色。")]
        [ColorUsage(false, true)] public Color edgeColor = new Color(4f, 1.4f, 0.3f);

        [Tooltip("「灼ける」のふちの太さ。")]
        [Range(0f, 0.3f)] public float edgeWidth = 0.08f;

        [Tooltip("閉じ始めに鳴らす効果音（D-Drive）。")]
        public SeId seCover;

        [Tooltip("開き始めに鳴らす効果音（D-Drive）。")]
        public SeId seReveal;

        public string Label => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        /// <summary>シェーダーに設定を入れる（progress は 0=覆っていない 1=全部覆う）。</summary>
        public void ApplyTo(Material material, float progress, bool revealing, float aspect)
        {
            var reverse = revealing && revealPassThrough; // 覆う順番を逆にすると、開くとき同じ向きへ通り過ぎる
            material.SetColor(UiTransitionShaderIds.Color, color);
            material.SetFloat(UiTransitionShaderIds.Progress, progress);
            material.SetFloat(UiTransitionShaderIds.Pattern, (float)pattern);
            material.SetFloat(UiTransitionShaderIds.Angle, angle + (reverse && pattern != UiTransitionPattern.Iris ? 180f : 0f));
            material.SetFloat(UiTransitionShaderIds.Count, count);
            material.SetFloat(UiTransitionShaderIds.Softness, softness);
            material.SetVector(UiTransitionShaderIds.Center, center);
            material.SetTexture(UiTransitionShaderIds.RuleTex, ruleTexture != null ? ruleTexture : Texture2D.grayTexture);
            material.SetFloat(UiTransitionShaderIds.Invert, invert ^ (reverse && pattern == UiTransitionPattern.RuleTexture) ? 1f : 0f);
            material.SetColor(UiTransitionShaderIds.EdgeColor, edgeColor);
            material.SetFloat(UiTransitionShaderIds.EdgeWidth, edgeWidth);
            material.SetFloat(UiTransitionShaderIds.Aspect, aspect);
        }

        /// <summary>何も設定しないときの標準（暗転のフェード）。</summary>
        public static UiScreenTransition CreateDefault()
        {
            var t = CreateInstance<UiScreenTransition>();
            t.displayName = "暗転";
            t.pattern = UiTransitionPattern.Fade;
            t.hideFlags = HideFlags.HideAndDontSave;
            return t;
        }
    }

    public static class UiTransitionShaderIds
    {
        public const string ShaderName = "MS2026/UI/ScreenTransition";
        public static readonly int Color = Shader.PropertyToID("_Color");
        public static readonly int Progress = Shader.PropertyToID("_Progress");
        public static readonly int Pattern = Shader.PropertyToID("_Pattern");
        public static readonly int Angle = Shader.PropertyToID("_Angle");
        public static readonly int Count = Shader.PropertyToID("_Count");
        public static readonly int Softness = Shader.PropertyToID("_Softness");
        public static readonly int Center = Shader.PropertyToID("_Center");
        public static readonly int RuleTex = Shader.PropertyToID("_RuleTex");
        public static readonly int Invert = Shader.PropertyToID("_Invert");
        public static readonly int EdgeColor = Shader.PropertyToID("_EdgeColor");
        public static readonly int EdgeWidth = Shader.PropertyToID("_EdgeWidth");
        public static readonly int Aspect = Shader.PropertyToID("_Aspect");
    }
}
