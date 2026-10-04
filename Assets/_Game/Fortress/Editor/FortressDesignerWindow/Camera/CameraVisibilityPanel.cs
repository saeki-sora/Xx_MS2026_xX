using System.Collections.Generic;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>映り込みチェックの結果を視点ごとに一覧表示する。行をクリックするとその視点を選択する。</summary>
    public sealed class CameraVisibilityPanel
    {
        public void Draw(CameraTabContext context, List<CameraVisibilityIssue> issues)
        {
            var problemCount = 0;
            foreach (var issue in issues)
            {
                if (issue.Severity >= MessageType.Warning)
                {
                    problemCount++;
                }
            }

            var title = problemCount > 0 ? $"映り込みチェック（要確認 {problemCount}件）" : "映り込みチェック（問題なし）";
            if (!CameraGui.Section("visibility", title, problemCount > 0))
            {
                return;
            }

            EditorGUILayout.LabelField(
                $"安全域 {context.Guides.safeAreaRatio:P0}・縦横比 {context.Guides.referenceResolution.x}x{context.Guides.referenceResolution.y} で判定しています。",
                EditorStyles.miniLabel);

            if (issues.Count == 0)
            {
                EditorGUILayout.HelpBox("全ての視点で、自分の砲台・コア・砲台・敵の出現地点が画面内に収まっています。", MessageType.None);
                return;
            }

            foreach (var viewer in ViewerIndex.All)
            {
                DrawViewerIssues(viewer, issues);
            }
        }

        private static void DrawViewerIssues(int viewer, List<CameraVisibilityIssue> issues)
        {
            foreach (var issue in issues)
            {
                if (issue.Viewer != viewer)
                {
                    continue;
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (CameraGui.ColoredButton(ViewerIndex.Label(viewer), ViewerIndex.Color(viewer), 0.6f, GUI.skin.button, GUILayout.Width(44)))
                    {
                        CameraToolState.SelectedViewer = viewer;
                    }

                    EditorGUILayout.HelpBox(issue.Message, issue.Severity);
                }
            }
        }
    }
}
