using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「テスト」タブの「コアが危ないときの画面の色」。シーンのコアの <see cref="CoreCrystalController.dangerLook"/> を並べ、
    /// Play 中は HP を減らさずに見た目だけを試せるボタンを出す。
    /// </summary>
    public static class CoreDangerPanel
    {
        private static readonly (string path, string label)[] Fields =
        {
            ("dangerLook.enabled", "コアが危ないと画面を赤くする"),
            ("dangerLook.startHealth01", "赤くなり始める HP（割合）"),
            ("dangerLook.tint", "一番危ないときの画面の色"),
            ("dangerLook.saturation", "一番危ないときの鮮やかさ"),
            ("dangerLook.edgeColor", "画面のふちの色"),
            ("dangerLook.edgeStrength", "画面のふちの暗さ"),
            ("dangerLook.pulseBelowHealth01", "鼓動し始める HP（割合）"),
            ("dangerLook.pulsePerSecond", "鼓動の速さ（回/秒）"),
            ("dangerLook.response", "色が追いつく速さ")
        };

        private static SerializedObject _serialized;

        public static void Draw()
        {
            EditorGUILayout.LabelField("コアが危ないときの画面の色", EditorStyles.boldLabel);
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core == null)
            {
                EditorGUILayout.HelpBox("シーンにコア（CoreCrystalController）がありません。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                "コアの HP が減るほど、画面全体が赤く熱っぽくなり、ふちが赤く染まります（見た目だけ）。" +
                "設定はシーンのコアに保存されるので、Play していないときに変えて Ctrl+S で保存してください（Play 中の変更は Play 終了で戻ります）。",
                EditorStyles.wordWrappedMiniLabel);

            if (_serialized == null || _serialized.targetObject != core)
            {
                _serialized = new SerializedObject(core);
            }

            _serialized.Update();
            foreach (var field in Fields)
            {
                var property = _serialized.FindProperty(field.path);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(field.label, property.tooltip));
                }
            }

            _serialized.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("見た目だけ試す（HP は減りません）", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                Preset("HP 50%", 0.5f);
                Preset("HP 25%", 0.25f);
                Preset("HP 10%", 0.1f);
                if (GUILayout.Button(new GUIContent("やめる", "本当の HP に合わせた色に戻します。"), EditorStyles.miniButton))
                {
                    CoreDangerLook.PreviewHealth01 = -1f;
                }
            }

            if (CoreDangerLook.PreviewHealth01 >= 0f)
            {
                EditorGUILayout.LabelField($"試している HP：{CoreDangerLook.PreviewHealth01:P0}", EditorStyles.miniLabel);
            }
        }

        private static void Preset(string label, float health01)
        {
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                CoreDangerLook.PreviewHealth01 = health01;
            }
        }
    }
}
