using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「この視点でプレイ」。P1〜P4のどれかの視点でPlayを始めるボタンと、通常のPlayでもその視点にする固定スイッチ。
    /// 実際の切り替えは <see cref="CameraPlayAsViewer"/> が行う。Play Mode外だけ表示する(Play中は「Play中のカメラ」で切り替える)。
    /// </summary>
    public sealed class CameraPlayAsPanel
    {
        private static readonly string[] ViewerNames = { "全体視点", "プレイヤー1", "プレイヤー2", "プレイヤー3", "プレイヤー4" };

        private GUIStyle _boldButton;

        public void Draw(CameraTabContext context)
        {
            if (!CameraGui.Section("playAs", "この視点でプレイ（テスト用）"))
            {
                return;
            }

            _boldButton ??= new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };

            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    for (var player = 0; player < ViewerIndex.PlayerCount; player++)
                    {
                        // 編集中の視点のボタンを目立たせる(いじった視点をすぐ試せるように)。
                        var editing = player == context.SelectedViewer;
                        var label = new GUIContent(
                            $"▶ {ViewerIndex.Label(player)}でプレイ",
                            $"{ViewerIndex.LongLabel(player)}の画面でPlayを始めます。このPCの画面だけの一時的な切り替えで、保存されるデータは変わりません。");

                        if (CameraGui.ColoredButton(label, ViewerIndex.Color(player), editing ? 1f : 0.45f, editing ? _boldButton : GUI.skin.button, GUILayout.Height(26f)))
                        {
                            CameraPlayAsViewer.PlayAs(player);
                        }
                    }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                CameraPlayAsViewer.Sticky = EditorGUILayout.ToggleLeft(
                    new GUIContent("通常のPlay（▶ボタン・Ctrl+P）でもこの視点で始める", "ONの間は、上のボタンを使わずにPlayしても右の視点で始まります。Unityを閉じるとOFFに戻ります。"),
                    CameraPlayAsViewer.Sticky);

                using (new EditorGUI.DisabledScope(!CameraPlayAsViewer.Sticky))
                {
                    var index = CameraPlayAsViewer.StickyViewer + 1;
                    var next = EditorGUILayout.Popup(index, ViewerNames, GUILayout.Width(100));
                    if (next != index)
                    {
                        CameraPlayAsViewer.StickyViewer = next - 1;
                    }
                }
            }

            EditorGUILayout.LabelField(
                "通信で決まる視点より優先されます。Play中は「Play中のカメラ」で別の視点に切り替えたり、「自動に戻す」で元に戻せます。",
                EditorStyles.wordWrappedMiniLabel);
        }
    }
}
