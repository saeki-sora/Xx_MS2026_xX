using System.Collections.Generic;
using MS2026.UI.Game;
using UnityEditor;
using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>
    /// UIスタジオの部品（値のつなぎ・ボタンの役目・ロビー・UIの置き場所・動き）のインスペクターを、日本語の項目名で並べる。
    /// 項目の説明（ツールチップ）は各部品の [Tooltip] がそのまま出る。
    /// </summary>
    [CustomEditor(typeof(UiBinding), true)]
    [CanEditMultipleObjects]
    public sealed class UiBindingEditor : UiComponentEditor
    {
    }

    [CustomEditor(typeof(UiButtonAction))]
    [CanEditMultipleObjects]
    public sealed class UiButtonActionEditor : UiComponentEditor
    {
    }

    [CustomEditor(typeof(LobbyScreenController))]
    public sealed class LobbyScreenControllerEditor : UiComponentEditor
    {
    }

    [CustomEditor(typeof(UiRoot))]
    public sealed class UiRootEditor : UiComponentEditor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("UIスタジオを開く", GUILayout.Height(24)))
            {
                UiStudioWindow.Open();
            }

            base.OnInspectorGUI();
        }
    }

    [CustomEditor(typeof(UiElementMotion))]
    [CanEditMultipleObjects]
    public sealed class UiElementMotionEditor : UiComponentEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("動きは UIスタジオの「動き」ページで、試しながら付けると簡単です。", MessageType.None);
            if (GUILayout.Button("UIスタジオの「動き」を開く"))
            {
                UiStudioWindow.OpenPage<UiMotionPage>();
            }

            base.OnInspectorGUI();
        }
    }

    /// <summary>項目を、日本語の名前の表に従って並べる共通部分（表に無い項目は Unity の名前のまま）。</summary>
    public abstract class UiComponentEditor : Editor
    {
        private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
        {
            { "key", "見る値" },
            { "format", "書式" },
            { "countUpSeconds", "数え上げる秒数" },
            { "whenEmpty", "値が無いときの文字" },
            { "fill", "伸び縮みする画像" },
            { "min", "最小（空・左端）の値" },
            { "max", "最大（満タン・右端）の値" },
            { "smoothSeconds", "なめらかさ（秒）" },
            { "trail", "残像の画像" },
            { "trailDelay", "残像が追い始めるまで（秒）" },
            { "trailSpeed", "残像が追う速さ" },
            { "gradient", "色の帯" },
            { "keepAlpha", "透明度はそのまま" },
            { "condition", "表示する条件" },
            { "threshold", "比べる数" },
            { "action", "押したときにすること" },
            { "transition", "画面切り替えの幕" },
            { "motionTarget", "動きの部品" },
            { "playerButtons", "P1〜P4 のボタン" },
            { "hostButton", "「ホストになる」ボタン" },
            { "joinButton", "「参加する」ボタン" },
            { "leaveButton", "「やめる」ボタン" },
            { "addressField", "IPアドレスの入力欄" },
            { "nextScreenId", "つながったら出す画面" },
            { "hideTemporaryConnectUi", "仮の接続画面を隠す" },
            { "defaultPlayer", "最初に選ぶプレイヤー" },
            { "layerSettings", "レイヤーの一覧" },
            { "catalog", "画面の一覧" },
            { "keepAcrossScenes", "シーンを切り替えてもUIを残す" },
            { "backWithEscape", "Esc・ゲームパッドBで戻る" },
            { "entries", "動きの一覧" }
        };

        /// <summary>部品ごとに意味が違う項目（例: ボタンの役目の target は「名前」）。</summary>
        private static readonly Dictionary<(System.Type, string), string> Overrides = new Dictionary<(System.Type, string), string>
        {
            { (typeof(UiButtonAction), "target"), "名前（画面・シーン・動き）" },
            { (typeof(UiBindColor), "target"), "色を変える部品" }
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var property = serializedObject.GetIterator();
            var enterChildren = true;
            while (property.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (property.propertyPath == "m_Script")
                {
                    continue;
                }

                EditorGUILayout.PropertyField(property, new GUIContent(LabelFor(property.name), property.tooltip), true);
            }

            serializedObject.ApplyModifiedProperties();
        }

        private string LabelFor(string field)
        {
            if (Overrides.TryGetValue((target.GetType(), field), out var special))
            {
                return special;
            }

            return Labels.TryGetValue(field, out var label) ? label : ObjectNames.NicifyVariableName(field);
        }
    }
}
