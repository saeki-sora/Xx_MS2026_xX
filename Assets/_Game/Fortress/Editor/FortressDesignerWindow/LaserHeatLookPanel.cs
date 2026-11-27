using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「砲台配置」タブの「熱の見せ方」（<see cref="LaserTuningConfig.heatLook"/>、2026-11-27 案A）。
    /// 開いた瞬間はよく触る3つだけを見せ、残りは「詳しい設定」にしまう（Tool_UI_Analysis T2）。
    /// </summary>
    public static class LaserHeatLookPanel
    {
        private const string DetailKey = "Fortress.LaserHeatLook.Detail";

        private static readonly (string path, string label)[] MainFields =
        {
            ("heatLook.enabled", "熱を見せる"),
            ("heatLook.showRing", "砲台のまわりに熱の輪を出す"),
            ("heatLook.ringRadius", "輪の半径（m）")
        };

        private static readonly (string path, string label)[] DetailFields =
        {
            ("heatLook.coreColors", "芯の色（左＝冷たい、右＝オーバーヒート）"),
            ("heatLook.coreTintStrength", "芯に色を付ける強さ"),
            ("heatLook.coreWidenAtFull", "熱いときに芯が太くなる量"),
            ("effects.heatWarnAt", "点滅を始める熱（0〜1）"),
            ("heatLook.blinkPerSecondStart", "点滅の速さ（始め・回/秒）"),
            ("heatLook.blinkPerSecondEnd", "点滅の速さ（直前・回/秒）"),
            ("heatLook.blinkDepth", "点滅で暗くなる量"),
            ("heatLook.ringWidth", "輪の太さ（m）"),
            ("heatLook.ringTrackColor", "輪の下地（縁取り）の色"),
            ("heatLook.ringTrackWidthScale", "下地の太さ（倍）"),
            ("heatLook.ringShowFrom", "輪を出し始める熱（0〜1）"),
            ("heatLook.ringFadeSpeed", "輪が出る・消える速さ"),
            ("heatLook.overheatRingColor", "オーバーヒート中の輪の色")
        };

        private static SerializedObject _serialized;

        public static void Draw(LaserTuningConfig tuning)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("熱の見せ方（芯の色・砲台の輪）", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "熱いほどビームの芯が 白→黄→橙→赤 になり、砲台のまわりの輪が溜まっていきます。" +
                "危ない所を越えると点滅し、オーバーヒート中は灰色の輪が減って、いつ戻るか分かります。見た目だけの設定です。",
                MessageType.None);

            if (_serialized == null || _serialized.targetObject != tuning)
            {
                _serialized = new SerializedObject(tuning);
            }

            _serialized.Update();
            DrawFields(MainFields);

            var open = EditorGUILayout.Foldout(EditorPrefs.GetBool(DetailKey, false), "詳しい設定", true);
            EditorPrefs.SetBool(DetailKey, open);
            if (open)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    DrawFields(DetailFields);
                }
            }

            _serialized.ApplyModifiedProperties();
        }

        private static void DrawFields((string path, string label)[] fields)
        {
            foreach (var field in fields)
            {
                var property = _serialized.FindProperty(field.path);
                if (property != null)
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(field.label, property.tooltip), true);
                }
            }
        }
    }
}
