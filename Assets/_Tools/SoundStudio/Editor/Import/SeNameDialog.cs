using System;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.SoundStudio
{
    /// <summary>新しい音の英字の名前を聞く小さな窓。</summary>
    public sealed class SeNameDialog : EditorWindow
    {
        private Action<string> _onOk;
        private TextField _field;
        private Label _preview;
        private string _folder;

        public static void Ask(string suggested, string folder, Action<string> onOk)
        {
            var window = CreateInstance<SeNameDialog>();
            window._onOk = onOk;
            window._folder = folder;
            window.titleContent = new GUIContent("新しい音の名前");
            window.minSize = window.maxSize = new Vector2(380, 150);
            window.Build(suggested);
            window.ShowUtility();
        }

        private void Build(string suggested)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StudioShell.StylePath);
            if (sheet != null)
            {
                rootVisualElement.styleSheets.Add(sheet);
            }

            rootVisualElement.AddToClassList("sk-root");
            rootVisualElement.Add(StudioUi.Lead("英字の名前を付けてください（D-Drive の番号札の名前になります）。"));
            _field = new TextField("名前") { value = suggested, tooltip = "英字と数字だけ。単語の頭は大文字にする（例 StartPush）。" };
            _field.RegisterValueChangedCallback(_ => UpdatePreview());
            rootVisualElement.Add(_field);
            _preview = new Label();
            rootVisualElement.Add(_preview);
            rootVisualElement.Add(StudioUi.Row(
                StudioUi.Button("登録する", Ok, "D-Drive の素材フォルダに入れて登録する。", primary: true),
                StudioUi.Button("やめる", Close, "何もしない。")));
            UpdatePreview();
            _field.RegisterCallback<KeyDownEvent>(e =>
            {
                if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    Ok();
                }
            });
        }

        private void UpdatePreview()
        {
            _preview.text = "番号札の名前: SEID." + _folder + SeImportNaming.SuggestName(_field.value, null);
        }

        private void Ok()
        {
            var name = SeImportNaming.SuggestName(_field.value, null);
            var callback = _onOk;
            Close();
            callback?.Invoke(name);
        }
    }
}
