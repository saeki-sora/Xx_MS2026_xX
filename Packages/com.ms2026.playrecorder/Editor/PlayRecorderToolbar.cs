#if UNITY_6000_3_OR_NEWER
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// Unity 6.3 以降：公式のツールバー拡張（MainToolbarElement）で、再生ボタンのある真ん中の列に置く。
    /// 置き場所はツールバーの空きを右クリック → 表示/並べ替え で変えられる（Unity の機能）。
    /// </summary>
    internal static class PlayRecorderToolbar
    {
        const string ElementPath = PlayRecorderUi.ToolName;

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Middle)]
        static MainToolbarElement Create()
        {
            var content = new MainToolbarContent(PlayRecorderUi.Label(), PlayRecorderUi.Icon(), PlayRecorderUi.Tooltip());
            var button = new MainToolbarButton(content, PlayRecorderController.Toggle);
            button.populateContextMenu = FillMenu;
            return button;
        }

        static void FillMenu(DropdownMenu menu)
        {
            foreach (var e in PlayRecorderUi.ContextMenu())
            {
                if (e.IsSeparator)
                {
                    menu.AppendSeparator();
                    continue;
                }
                var action = e.Action;
                menu.AppendAction(e.Label, _ => action(),
                    e.Enabled ? DropdownMenuAction.Status.Normal : DropdownMenuAction.Status.Disabled);
            }
        }

        const string AutoShownKey = "MS2026.PlayRecorder.ToolbarAutoShown.";
        const int MaxShowTries = 40;
        static int s_ShowTries;
        static double s_NextShowTry;

        [InitializeOnLoadMethod]
        static void Init()
        {
            PlayRecorderController.Changed += () => MainToolbar.Refresh(ElementPath);
            if (!EditorPrefs.GetBool(AutoShownKey + PlayRecorderSettings.ProjectRoot, false))
                EditorApplication.update += ShowOnceUpdate;
        }

        /// <summary>
        /// Unity 6.3 は新しく足されたツールバー部品を「非表示」で登録するので、入れた直後に 1 回だけ表示にする。
        /// その後ユーザーが右クリックで隠したら、その選択を尊重する（プロジェクトごとに 1 回きり）。
        /// </summary>
        static void ShowOnceUpdate()
        {
            if (EditorApplication.timeSinceStartup < s_NextShowTry) return;
            s_NextShowTry = EditorApplication.timeSinceStartup + 0.5;

            bool done = TryShowOverlay() || ++s_ShowTries >= MaxShowTries;
            if (!done) return;
            if (s_ShowTries >= MaxShowTries) TryShowAll();
            EditorApplication.update -= ShowOnceUpdate;
            EditorPrefs.SetBool(AutoShownKey + PlayRecorderSettings.ProjectRoot, true);
        }

        const BindingFlags InternalStatic = BindingFlags.NonPublic | BindingFlags.Static;

        /// <summary>メニュー「ツールバーにボタンを表示する」から。隠してしまったボタンを戻す。</summary>
        internal static void ShowButton()
        {
            if (!TryShowOverlay()) TryShowAll();
            MainToolbar.Refresh(ElementPath);
        }

        static bool TryShowOverlay()
        {
            try
            {
                var tryGet = typeof(MainToolbar).GetMethod("TryGetOverlay", InternalStatic);
                if (tryGet == null) return TryShowAll();
                var args = new object[] { ElementPath, null };
                if (!(bool)tryGet.Invoke(null, args) || !(args[1] is Overlay overlay)) return false; // まだツールバーができていない
                overlay.displayed = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[録画プレイ] ツールバーにボタンを自動で表示できませんでした: " + e.Message);
                return true;
            }
        }

        static bool TryShowAll()
        {
            try
            {
                var showAll = typeof(MainToolbar).GetMethod("ShowAll", InternalStatic);
                if (showAll == null) return true;
                showAll.Invoke(null, new object[] { ElementPath });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[録画プレイ] ツールバーにボタンを自動で表示できませんでした: " + e.Message);
            }
            return true;
        }
    }
}
#else
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.PlayRecorder.Editor
{
    /// <summary>
    /// Unity 2021.3〜6.2：公式の拡張口が無いので、ツールバーの再生ボタンの列（ToolbarZonePlayMode）に
    /// ボタンを差し込む。見つからない Unity でも、メニュー Tools › 録画しながらプレイ から使える。
    /// </summary>
    [InitializeOnLoad]
    internal static class PlayRecorderToolbar
    {
        static Button s_Button;
        static double s_NextTry;

        static PlayRecorderToolbar()
        {
            EditorApplication.update += Update;
            PlayRecorderController.Changed += Refresh;
        }

        static void Update()
        {
            if (s_Button != null && s_Button.panel != null) return;
            if (EditorApplication.timeSinceStartup < s_NextTry) return;
            s_NextTry = EditorApplication.timeSinceStartup + 1.0;
            TryInject();
        }

        static void TryInject()
        {
            var toolbarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Toolbar");
            if (toolbarType == null) return;
            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            if (toolbars.Length == 0) return;
            var rootField = toolbarType.GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
            var root = rootField?.GetValue(toolbars[0]) as VisualElement;
            var zone = root?.Q("ToolbarZonePlayMode");
            if (zone == null) return;

            s_Button = new Button(PlayRecorderController.Toggle);
            s_Button.style.marginRight = 6;
            s_Button.style.paddingLeft = 6;
            s_Button.style.paddingRight = 6;
            s_Button.style.unityFontStyleAndWeight = FontStyle.Bold;
            s_Button.RegisterCallback<MouseUpEvent>(e =>
            {
                if (e.button != 1) return;
                PlayRecorderUi.ToGenericMenu(PlayRecorderUi.ContextMenu()).ShowAsContext();
                e.StopPropagation();
            });
            zone.Insert(0, s_Button);
            Refresh();
        }

        /// <summary>メニュー「ツールバーにボタンを表示する」から。差し込みをやり直す。</summary>
        internal static void ShowButton()
        {
            s_NextTry = 0;
            if (s_Button == null || s_Button.panel == null) TryInject();
        }

        static void Refresh()
        {
            if (s_Button == null) return;
            s_Button.text = PlayRecorderUi.Label();
            s_Button.tooltip = PlayRecorderUi.Tooltip();
            s_Button.style.color = PlayRecorderController.IsRecording
                ? new StyleColor(new Color(1f, 0.45f, 0.45f))
                : new StyleColor(StyleKeyword.Null);
        }
    }
}
#endif
