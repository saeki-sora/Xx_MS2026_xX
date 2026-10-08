using System;
using MS2026.StudioKit;
using TMPro;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// 「文字を TextMeshPro にする（任意）」の欄。日本語フォントから TMP フォントを作り、選んだ画面の文字を置き換える。
    /// 雛形は標準の Text なので、このままでも日本語は出る。TextMeshPro にすると、縁取り・影・きれいな拡大ができる。
    /// </summary>
    public sealed class TmpFontPanel : VisualElement
    {
        private readonly Func<UiScreen> _screen;
        private readonly ObjectField _source;
        private readonly ObjectField _font;

        public TmpFontPanel(Func<UiScreen> screen)
        {
            _screen = screen;
            var card = StudioUi.Card("文字を TextMeshPro にする（任意）",
                "雛形の文字は標準の Text なので、このままでも日本語が出ます。TextMeshPro にすると縁取り・影・きれいな拡大が使えます。" +
                "日本語を出すには、日本語のフォントファイル（.ttf / .otf。ゲームに入れてよいライセンスの物）が必要です。");

            _source = new ObjectField("日本語のフォントファイル") { objectType = typeof(Font), allowSceneObjects = false, tooltip = "プロジェクトに入れた日本語のフォント（.ttf / .otf）。" };
            card.Add(_source);
            card.Add(StudioUi.Row(StudioUi.Button("TextMeshPro 用のフォントを作る", CreateFont,
                "上のフォントから、使った文字だけ自動で足していく TextMeshPro のフォントを作ります（Assets/_Game/UI/Fonts/）。", small: true)));

            _font = new ObjectField("使う TextMeshPro フォント") { objectType = typeof(TMP_FontAsset), allowSceneObjects = false, tooltip = "置き換えに使うフォント。上で作った物が自動で入ります。" };
            var existing = UiTmpTools.FindFonts();
            if (existing.Count > 0)
            {
                _font.SetValueWithoutNotify(existing[existing.Count - 1]);
            }

            card.Add(_font);
            card.Add(StudioUi.Row(StudioUi.Button("この画面の文字を TextMeshPro にする", Convert,
                "選んでいる画面の Text を、同じ文字・大きさ・色・寄せ方の TextMeshPro に置き換えます（入力欄の中の文字は除く）。元に戻すは Ctrl+Z（Prefab のときは元に戻せないので、先に複製しておくと安心）。", small: true)));
            Add(card);
        }

        private void CreateFont()
        {
            if (!(_source.value is Font source))
            {
                EditorUtility.DisplayDialog("フォントを選んでください", "日本語のフォントファイル（.ttf / .otf）をプロジェクトに入れて、上の欄に入れてください。", "OK");
                return;
            }

            var font = UiTmpTools.CreateDynamicFont(source);
            if (font == null)
            {
                EditorUtility.DisplayDialog("作れませんでした", "このフォントからは TextMeshPro のフォントを作れませんでした。別のフォントを試してください。", "OK");
                return;
            }

            _font.SetValueWithoutNotify(font);
            EditorGUIUtility.PingObject(font);
        }

        private void Convert()
        {
            var screen = _screen();
            if (screen == null || !(_font.value is TMP_FontAsset font))
            {
                EditorUtility.DisplayDialog("置き換えられません", "画面と、使う TextMeshPro フォントを選んでください。", "OK");
                return;
            }

            int count;
            if (EditorUtility.IsPersistent(screen))
            {
                // Prefab は中身を一時的に開いて置き換え、保存する。
                var path = AssetDatabase.GetAssetPath(screen);
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    count = UiTmpTools.ConvertTexts(contents, font);
                    PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
            else
            {
                count = UiTmpTools.ConvertTexts(screen.gameObject, font);
            }

            EditorUtility.DisplayDialog("置き換えました", $"{count} 個の文字を TextMeshPro にしました。", "OK");
        }
    }
}
