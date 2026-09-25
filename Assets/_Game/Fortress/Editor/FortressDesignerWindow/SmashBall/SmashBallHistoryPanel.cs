using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>Play中に「誰がいつどのスマッシュボールを割ったか」を確認できるデバッグ用の履歴表示。</summary>
    public sealed class SmashBallHistoryPanel
    {
        private bool _foldout;

        public void Draw()
        {
            _foldout = EditorGUILayout.BeginFoldoutHeaderGroup(_foldout, $"破壊履歴（直近{SmashBallHistory.Records.Count}件）");
            if (_foldout)
            {
                if (!Application.isPlaying)
                {
                    EditorGUILayout.HelpBox("Play中にここへ記録されます。", MessageType.None);
                }
                else if (SmashBallHistory.Records.Count == 0)
                {
                    EditorGUILayout.HelpBox("まだ誰もスマッシュボールを割っていません。", MessageType.None);
                }
                else
                {
                    foreach (var record in SmashBallHistory.Records)
                    {
                        DrawRow(record);
                    }
                }

                using (new EditorGUI.DisabledScope(SmashBallHistory.Records.Count == 0))
                {
                    if (GUILayout.Button("履歴を消す"))
                    {
                        SmashBallHistory.Clear();
                    }
                }

                EditorGUILayout.Space(6);
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
        }

        private static void DrawRow(SmashBallBreakInfo record)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                var previous = GUI.color;
                GUI.color = record.Attacker.PlayerColor;
                EditorGUILayout.LabelField("●", GUILayout.Width(14));
                GUI.color = previous;

                EditorGUILayout.LabelField(record.Attacker.DisplayName, GUILayout.Width(70));
                EditorGUILayout.LabelField(record.DisplayName, GUILayout.MinWidth(80));
                EditorGUILayout.LabelField($"{Mathf.Max(0f, Time.time - record.Time):0}秒前", GUILayout.Width(56));

                if (GUILayout.Button("表示", GUILayout.Width(40)) && record.Obstacle != null)
                {
                    Selection.activeGameObject = record.Obstacle.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }
            }
        }
    }
}
