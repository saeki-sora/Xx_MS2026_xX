using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    [CustomEditor(typeof(FortressLayoutController))]
    public sealed class FortressLayoutControllerEditor : Editor
    {
        private FortressLayoutConfig _presetToApply;
        private FortressLayoutConfig _presetToSave;

        public override void OnInspectorGUI()
        {
            var controller = (FortressLayoutController)target;

            EditorGUILayout.HelpBox(
                "砲台の位置はシーン上でGameObjectを選び、通常の移動ツールで自由に動かすだけで配置できます。\n" +
                "ここでは配置をプリセット(Fortress Layout Config)として保存・読み込みできます。",
                MessageType.Info);

            DrawDefaultInspector();

            controller.EnsureTurrets();
            if (controller.HasDuplicatePlayerIndex())
            {
                EditorGUILayout.HelpBox(
                    "複数の砲台が同じPlayer Indexを使っています。0-3が重複しないようにしてください。",
                    MessageType.Warning);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("配置プリセット", EditorStyles.boldLabel);

            _presetToApply = (FortressLayoutConfig)EditorGUILayout.ObjectField(
                new GUIContent("読み込むプリセット", "選択したプリセットの座標を今すぐ砲台に適用します。"),
                _presetToApply, typeof(FortressLayoutConfig), false);

            using (new EditorGUI.DisabledScope(_presetToApply == null))
            {
                if (GUILayout.Button("プリセットを適用"))
                {
                    ApplyPreset(controller, _presetToApply);
                }
            }

            EditorGUILayout.Space(4);

            _presetToSave = (FortressLayoutConfig)EditorGUILayout.ObjectField(
                new GUIContent("保存先プリセット", "現在の砲台の座標をこのアセットに書き込みます。"),
                _presetToSave, typeof(FortressLayoutConfig), false);

            using (new EditorGUI.DisabledScope(_presetToSave == null))
            {
                if (GUILayout.Button("現在の配置をプリセットに保存"))
                {
                    Undo.RecordObject(_presetToSave, "Capture Fortress Layout");
                    controller.CaptureLayout(_presetToSave);
                    EditorUtility.SetDirty(_presetToSave);
                }
            }
        }

        private static void ApplyPreset(FortressLayoutController controller, FortressLayoutConfig preset)
        {
            controller.EnsureTurrets();

            if (controller.turrets != null)
            {
                foreach (var turret in controller.turrets)
                {
                    if (turret != null)
                    {
                        Undo.RecordObject(turret.transform, "Apply Fortress Layout");
                    }
                }
            }

            controller.ApplyLayout(preset);
        }
    }
}
