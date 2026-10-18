using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「砲台配置」タブの「ビームの光（まわりを照らす）」。<see cref="LaserTuningConfig.beamLight"/> を並べる。
    /// </summary>
    public static class LaserLightPanel
    {
        private static readonly (string path, string label)[] MainFields =
        {
            ("beamLight.enabled", "ビームでまわりを照らす")
        };

        private static readonly (string path, string label)[] FloorFields =
        {
            ("beamLight.floorIntensity", "明るさ"),
            ("beamLight.floorRange", "光の届く距離（m）"),
            ("beamLight.lightHeight", "光の高さ（m）"),
            ("beamLight.impactBoost", "当たっている場所の明るさ（倍）"),
            ("beamLight.surfaceColorInfluence", "床の色の影響（0で光の色そのまま）")
        };

        private static readonly (string path, string label)[] EnemyFields =
        {
            ("beamLight.enemyStrength", "照らす強さ"),
            ("beamLight.enemyRadius", "照らす距離（m）"),
            ("beamLight.enemyColorOverlay", "光の色を乗せる量")
        };

        private static readonly (string path, string label)[] CommonFields =
        {
            ("beamLight.colorSaturation", "光の色の鮮やかさ"),
            ("beamLight.weakGripBrightness", "弱く握ったときの明るさ（割合）"),
            ("beamLight.fadeSpeed", "つく／消える速さ")
        };

        private static SerializedObject _serialized;

        public static void Draw(LaserTuningConfig tuning)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("ビームの光（まわりを照らす）", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "撃っている間、ビームのまわりの床・背景の物・群衆の敵を、その砲台のプレイヤー色で照らします（見た目だけ）。" +
                "床・背景の物はビームの線に沿って途切れなく照らします（ステージ背景の物だけ。絵で描いた砲台・コアは照らしません）。マルチプレイでも通信せず、各PCが自分で描きます。",
                MessageType.None);

            if (_serialized == null || _serialized.targetObject != tuning)
            {
                _serialized = new SerializedObject(tuning);
            }

            _serialized.Update();
            DrawFields(MainFields);
            var enabled = _serialized.FindProperty("beamLight.enabled")?.boolValue ?? true;
            using (new EditorGUI.DisabledScope(!enabled))
            {
                DrawGroup("床・背景の物", FloorFields);
                DrawGroup("群衆の敵", EnemyFields);
                DrawGroup("共通", CommonFields);
            }

            _serialized.ApplyModifiedProperties();
        }

        private static void DrawGroup(string title, (string path, string label)[] fields)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(title, EditorStyles.miniLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                DrawFields(fields);
            }
        }

        private static void DrawFields((string path, string label)[] fields)
        {
            foreach (var field in fields)
            {
                var property = _serialized.FindProperty(field.path);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(field.label, property.tooltip));
                }
            }
        }
    }
}
