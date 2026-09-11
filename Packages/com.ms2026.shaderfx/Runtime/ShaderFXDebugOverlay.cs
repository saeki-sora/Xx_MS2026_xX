using UnityEngine;

namespace MS2026.ShaderFX
{
    // Design doc §5「デバッグオーバーレイ」 — a quick, always-available read of the Director's
    // internal state without needing the Profiler/Frame Debugger open. Add this component to any
    // GameObject in a scene (or a dedicated empty one) to show it; it's opt-in, not auto-created
    // the way EffectDirector is, since a debug overlay shouldn't appear in a shipped build unless
    // a developer deliberately put it there.
    [AddComponentMenu("ShaderFX/Debug Overlay")]
    public sealed class ShaderFXDebugOverlay : MonoBehaviour
    {
        [Tooltip("画面のどこに表示するかです。")]
        public TextAnchor anchor = TextAnchor.UpperLeft;

        [Tooltip("表示のON/OFFです。スクリプトから visible を切り替えれば、実行中に開発者用キー等で" +
                 "トグルする使い方もできます。")]
        public bool visible = true;

        private GUIStyle style;

        private void OnGUI()
        {
            if (!visible || !EffectDirector.HasInstance) return;

            style ??= new GUIStyle(GUI.skin.box)
            {
                fontSize = 14,
                alignment = TextAnchor.UpperLeft,
                padding = new RectOffset(10, 10, 8, 8),
                normal = { textColor = Color.white },
            };

            var director = EffectDirector.Instance;
            int registered = director.RegisteredTargetCount;
            int cachedMaterials = director.CachedMaterialCount;
            int overrideCount = director.CountRenderersWithPropertyBlockOverride();

            string text =
                "[ShaderFX]\n" +
                $"登録 EffectTarget 数: {registered}\n" +
                $"キャッシュ Material 数: {cachedMaterials} (≒ バッチ数の目安)\n" +
                $"個体差オーバーライド中: {overrideCount} (個別描画コスト)";

            GUI.Box(ComputeRect(text), text, style);
        }

        private Rect ComputeRect(string text)
        {
            style.CalcMinMaxWidth(new GUIContent(text), out _, out float preferredWidth);
            float width = Mathf.Max(220f, preferredWidth + 20f);
            float height = style.CalcHeight(new GUIContent(text), width);
            const float margin = 10f;

            float x = anchor is TextAnchor.UpperRight or TextAnchor.MiddleRight or TextAnchor.LowerRight
                ? Screen.width - width - margin
                : margin;
            float y = anchor is TextAnchor.LowerLeft or TextAnchor.LowerCenter or TextAnchor.LowerRight
                ? Screen.height - height - margin
                : margin;

            return new Rect(x, y, width, height);
        }
    }
}
