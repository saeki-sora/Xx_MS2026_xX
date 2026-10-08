using System.Collections.Generic;
using MS2026.StudioKit;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// TextMeshPro で日本語を出すための道具。
    /// ・日本語のフォントファイル（.ttf / .otf）から、文字を使う分だけ自動で足していく「動的な」TMP フォントを作る
    /// ・画面の標準の Text を TextMeshPro に置き換える（入力欄の中の文字は入力欄ごと替える必要があるので触らない）
    /// フォントファイルそのものはプロジェクトに入っていないので、使う人が用意する（ゲームに同梱してよいライセンスの物）。
    /// </summary>
    public static class UiTmpTools
    {
        public const string FontsFolder = "Assets/_Game/UI/Fonts";

        /// <summary>フォントファイルから動的な TMP フォントを作って保存する。</summary>
        public static TMP_FontAsset CreateDynamicFont(Font source)
        {
            var asset = TMP_FontAsset.CreateFontAsset(source, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (asset == null)
            {
                return null;
            }

            var path = StudioAssets.UniquePath(FontsFolder, $"{StudioAssets.SafeFileName(source.name)} SDF.asset");
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(asset, path);

            // 文字の絵（アトラス）とマテリアルは、フォントのファイルの中に一緒に保存する。
            foreach (var atlas in asset.atlasTextures)
            {
                if (atlas != null)
                {
                    atlas.name = asset.name + " Atlas";
                    AssetDatabase.AddObjectToAsset(atlas, asset);
                }
            }

            asset.material.name = asset.name + " Material";
            AssetDatabase.AddObjectToAsset(asset.material, asset);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            return asset;
        }

        /// <summary>プロジェクト内の TMP フォント（新しい順の目安としてパス順）。</summary>
        public static List<TMP_FontAsset> FindFonts()
        {
            var result = new List<TMP_FontAsset>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(TMP_FontAsset)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.StartsWith("Packages/"))
                {
                    var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                    if (font != null)
                    {
                        result.Add(font);
                    }
                }
            }

            return result;
        }

        /// <summary>画面の中の標準の Text を TextMeshPro に置き換える。戻り値は置き換えた数。</summary>
        public static int ConvertTexts(GameObject screenRoot, TMP_FontAsset font)
        {
            var count = 0;
            Undo.SetCurrentGroupName("文字を TextMeshPro にする");
            foreach (var text in screenRoot.GetComponentsInChildren<Text>(true))
            {
                if (text.GetComponentInParent<InputField>(true) != null)
                {
                    continue; // 入力欄の中の文字は、入力欄（InputField）ごと替えないと動かなくなる
                }

                var go = text.gameObject;
                var content = text.text;
                var size = text.fontSize;
                var color = text.color;
                var bold = text.fontStyle == FontStyle.Bold || text.fontStyle == FontStyle.BoldAndItalic;
                var alignment = ToTmp(text.alignment);
                var raycast = text.raycastTarget;
                var wrap = text.horizontalOverflow == HorizontalWrapMode.Wrap;

                Undo.DestroyObjectImmediate(text);
                var tmp = Undo.AddComponent<TextMeshProUGUI>(go);
                tmp.font = font;
                tmp.text = content;
                tmp.fontSize = size;
                tmp.color = color;
                tmp.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
                tmp.alignment = alignment;
                tmp.raycastTarget = raycast;
                tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
                EditorUtility.SetDirty(tmp);
                count++;
            }

            return count;
        }

        private static TextAlignmentOptions ToTmp(TextAnchor anchor) => anchor switch
        {
            TextAnchor.UpperLeft => TextAlignmentOptions.TopLeft,
            TextAnchor.UpperCenter => TextAlignmentOptions.Top,
            TextAnchor.UpperRight => TextAlignmentOptions.TopRight,
            TextAnchor.MiddleLeft => TextAlignmentOptions.Left,
            TextAnchor.MiddleRight => TextAlignmentOptions.Right,
            TextAnchor.LowerLeft => TextAlignmentOptions.BottomLeft,
            TextAnchor.LowerCenter => TextAlignmentOptions.Bottom,
            TextAnchor.LowerRight => TextAlignmentOptions.BottomRight,
            _ => TextAlignmentOptions.Center
        };
    }
}
