using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 演出欄（<see cref="FortressEffect"/>）の共通の描き方。要塞デザイナーのどのタブでも、Inspectorでも同じ見た目になる。
    /// 折りたたみの見出しに「エフェクト／効果音が入っているか」を出し、開くと D-Drive の番号札の選択欄・プレイヤー色・
    /// Asset Browser を開くボタン・Play中の「▶ 試す」ボタンが並ぶ。
    /// </summary>
    [CustomPropertyDrawer(typeof(FortressEffect))]
    public sealed class FortressEffectDrawer : PropertyDrawer
    {
        private const string AssetBrowserMenu = "Tools/D-Drive/Asset Browser";
        private const string VfxEditorMenu = "Tools/D-Drive/Editors/VFX";
        private const float Gap = 2f;

        private static float Line => EditorGUIUtility.singleLineHeight;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var height = Line;
            if (!property.isExpanded)
            {
                return height;
            }

            height += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(FortressEffect.vfx)));
            height += Gap + EditorGUI.GetPropertyHeight(property.FindPropertyRelative(nameof(FortressEffect.se)));
            height += Gap + Line;
            if (property.FindPropertyRelative(nameof(FortressEffect.tintWithPlayerColor)).boolValue)
            {
                height += Gap + Line;
            }

            height += Gap + Line;
            return height;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var vfx = property.FindPropertyRelative(nameof(FortressEffect.vfx));
            var se = property.FindPropertyRelative(nameof(FortressEffect.se));
            var tint = property.FindPropertyRelative(nameof(FortressEffect.tintWithPlayerColor));
            var colorParam = property.FindPropertyRelative(nameof(FortressEffect.colorParam));

            var row = new Rect(position.x, position.y, position.width, Line);
            property.isExpanded = EditorGUI.Foldout(row, property.isExpanded, label, true);
            DrawSummary(row, vfx, se);

            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;

                row = NextRow(row, EditorGUI.GetPropertyHeight(vfx));
                EditorGUI.PropertyField(row, vfx, new GUIContent("エフェクト(VFX)", vfx.tooltip));

                row = NextRow(row, EditorGUI.GetPropertyHeight(se));
                EditorGUI.PropertyField(row, se, new GUIContent("効果音(SE)", se.tooltip));

                row = NextRow(row, Line);
                EditorGUI.PropertyField(row, tint, new GUIContent("プレイヤー色で色付け", tint.tooltip));

                if (tint.boolValue)
                {
                    row = NextRow(row, Line);
                    EditorGUI.PropertyField(row, colorParam, new GUIContent("色を入れる項目名", colorParam.tooltip));
                }

                row = NextRow(row, Line);
                DrawButtons(EditorGUI.IndentedRect(row), property);

                EditorGUI.indentLevel--;
            }

            EditorGUI.EndProperty();
        }

        private static Rect NextRow(Rect previous, float height)
        {
            return new Rect(previous.x, previous.yMax + Gap, previous.width, height);
        }

        private static void DrawSummary(Rect header, SerializedProperty vfx, SerializedProperty se)
        {
            var hasVfx = IsAssigned(vfx);
            var hasSe = IsAssigned(se);
            var text = !hasVfx && !hasSe
                ? "（未設定）"
                : $"{(hasVfx ? "エフェクト✓" : "")}{(hasVfx && hasSe ? "  " : "")}{(hasSe ? "効果音✓" : "")}";

            var width = Mathf.Min(160f, header.width * 0.45f);
            var rect = new Rect(header.xMax - width, header.y, width, header.height);
            EditorGUI.LabelField(rect, text, EditorStyles.miniLabel);
        }

        private static bool IsAssigned(SerializedProperty assetId)
        {
            var value = assetId.FindPropertyRelative("value");
            return value != null && (value.hasMultipleDifferentValues || value.ulongValue != 0);
        }

        private static void DrawButtons(Rect rect, SerializedProperty property)
        {
            var third = (rect.width - 8f) / 3f;
            var r1 = new Rect(rect.x, rect.y, third, rect.height);
            var r2 = new Rect(r1.xMax + 4f, rect.y, third, rect.height);
            var r3 = new Rect(r2.xMax + 4f, rect.y, third, rect.height);

            if (GUI.Button(r1, new GUIContent("Asset Browser", "D-Drive の Asset Browser を開きます。VFX・SE の新規作成や試聴はここから。")))
            {
                EditorApplication.ExecuteMenuItem(AssetBrowserMenu);
            }

            if (GUI.Button(r2, new GUIContent("VFX Editor", "D-Drive の VFX Editor を開きます。寿命・出す位置・色の項目(Params)はここで設定します。")))
            {
                EditorApplication.ExecuteMenuItem(VfxEditorMenu);
            }

            using (new EditorGUI.DisabledScope(!FortressEffectPreview.CanPlay))
            {
                var tooltip = FortressEffectPreview.CanPlay
                    ? "この演出をその場で1回鳴らします（ループするエフェクトは数秒で止めます）。"
                    : "Play中に使えます。";
                if (GUI.Button(r3, new GUIContent("▶ 試す", tooltip)))
                {
                    FortressEffectPreview.Play(property);
                }
            }
        }
    }
}
