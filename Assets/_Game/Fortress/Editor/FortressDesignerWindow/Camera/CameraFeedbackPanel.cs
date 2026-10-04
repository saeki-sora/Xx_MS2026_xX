using System;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 出来事ごとのカメラ演出(揺れ・寄り)の設定を編集する。Play中は試し撃ちと発火履歴(<see cref="CameraFeedbackTester"/>)も出す。
    /// </summary>
    public sealed class CameraFeedbackPanel
    {
        private readonly CameraFeedbackTester _tester = new CameraFeedbackTester();
        private SerializedObject _serialized;

        public void Draw(CameraTabContext context)
        {
            if (!CameraGui.Section("feedback", "演出（揺れ・寄り）", false))
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "スマッシュボール破壊・コア被弾・オーバーヒートのときに画面を揺らしたり一瞬寄せたりします。各PCは自分の画面だけを揺らすので、" +
                "「割った本人だけ強く」「オーバーヒートした本人だけ」のように画面ごとに出し分けられます。揺れは簡易揺れ（アセット不要）か、" +
                "D-Driveの揺れアセットを選べます。",
                MessageType.None);

            var director = context.Director;
            if (director == null)
            {
                EditorGUILayout.HelpBox("演出の係(CameraFeedbackDirector)がシーンにありません。上の「足りない部品を追加」で追加できます。", MessageType.Warning);
                return;
            }

            var config = DrawConfigField(director);
            if (config == null)
            {
                return;
            }

            DrawReactions(config);
            _tester.Draw(context);
        }

        private static CameraFeedbackConfig DrawConfigField(CameraFeedbackDirector director)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var picked = (CameraFeedbackConfig)EditorGUILayout.ObjectField("演出の設定", director.config, typeof(CameraFeedbackConfig), false);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(director, "Assign Camera Feedback Config");
                    director.config = picked;
                    EditorUtility.SetDirty(director);
                }

                if (GUILayout.Button("新規作成", GUILayout.Width(80)))
                {
                    Undo.RecordObject(director, "Create Camera Feedback Config");
                    director.config = CameraPresetEditing.CreateFeedbackConfigAsset();
                    EditorUtility.SetDirty(director);
                }
            }

            return director.config;
        }

        private void DrawReactions(CameraFeedbackConfig config)
        {
            if (config.EnsureAllEvents())
            {
                EditorUtility.SetDirty(config);
            }

            if (_serialized == null || _serialized.targetObject != config)
            {
                _serialized = new SerializedObject(config);
            }

            _serialized.Update();

            var globalStrength = _serialized.FindProperty(nameof(CameraFeedbackConfig.globalStrength));
            EditorGUILayout.PropertyField(globalStrength, new GUIContent("全体の強さ", globalStrength.tooltip));

            var reactions = _serialized.FindProperty(nameof(CameraFeedbackConfig.reactions));
            foreach (CameraFeedbackEvent eventType in Enum.GetValues(typeof(CameraFeedbackEvent)))
            {
                var element = FindReaction(reactions, eventType);
                if (element != null)
                {
                    CameraReactionDrawer.Draw(element, eventType);
                }
            }

            _serialized.ApplyModifiedProperties();
        }

        private static SerializedProperty FindReaction(SerializedProperty reactions, CameraFeedbackEvent eventType)
        {
            for (var i = 0; i < reactions.arraySize; i++)
            {
                var element = reactions.GetArrayElementAtIndex(i);
                if (element.FindPropertyRelative(nameof(CameraReaction.eventType)).enumValueIndex == (int)eventType)
                {
                    return element;
                }
            }

            return null;
        }
    }
}
