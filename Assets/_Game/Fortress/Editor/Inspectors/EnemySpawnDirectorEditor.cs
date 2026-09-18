using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    [CustomEditor(typeof(EnemySpawnDirector))]
    public sealed class EnemySpawnDirectorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var director = (EnemySpawnDirector)target;

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("テスト再生", EditorStyles.boldLabel);

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Play Modeに入るとここからウェーブの再生テストができます。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("状態", director.IsPlaying
                ? $"再生中 ({director.ElapsedTime:0.0}秒経過)"
                : "停止中");

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(director.IsPlaying))
                {
                    if (GUILayout.Button("ウェーブ開始"))
                    {
                        director.StartWave();
                    }
                }

                using (new EditorGUI.DisabledScope(!director.IsPlaying))
                {
                    if (GUILayout.Button("停止"))
                    {
                        director.StopWave();
                    }
                }
            }

            Repaint();
        }
    }
}
