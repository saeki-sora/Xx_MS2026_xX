using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「砲台配置」タブの「見た目（歯磨き粉ビーム）」。<see cref="LaserTuningConfig.look"/>（表面が流れる速さ）と、
    /// ビームのマテリアル（こぶ・つや・泡など）の調整を並べる。
    /// </summary>
    public static class LaserLookPanel
    {
        private const string BeamShaderName = "MS2026/Fortress/ToothpasteBeam";
        private const string FoldoutKey = "Fortress.LaserLook.Material";

        private static readonly (string path, string label)[] Fields =
        {
            ("look.enabled", "表面を流す"),
            ("look.flowSpeedWeak", "流れる速さ（弱く握ったとき）"),
            ("look.flowSpeedStrong", "流れる速さ（強く握ったとき）")
        };

        private static SerializedObject _serialized;
        private static MaterialEditor _materialEditor;

        public static void Draw(LaserTuningConfig tuning, LaserTurret[] turrets)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("見た目（歯磨き粉ビーム）", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "見た目だけの設定です（当たり判定・ダメージは変わりません）。ビームの縁が「こぶ」になって先へ流れ、" +
                "つや・きらめき・泡・すじも一緒に流れます。強く握るほど速く、つやも強くなります。マルチプレイでも通信せず、各PCが自分で描きます。",
                MessageType.None);

            if (_serialized == null || _serialized.targetObject != tuning)
            {
                _serialized = new SerializedObject(tuning);
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

            DrawBeamMaterial(turrets);
        }

        private static void DrawBeamMaterial(LaserTurret[] turrets)
        {
            EditorGUILayout.Space(4);
            var material = FindBeamMaterial(turrets);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField(
                    new GUIContent("ビームのマテリアル", "砲台の Laser Beam Visual の「ビームの線に使うマテリアル」。砲台ごとに変えられます（ここでは最初に見つかったもの）。"),
                    material, typeof(Material), false);
            }

            if (material == null)
            {
                EditorGUILayout.HelpBox(
                    "砲台の Laser Beam Visual に「ビームの線に使うマテリアル」が入っていません。" +
                    "Assets/_Game/Fortress/Effects/Toothpaste/Toothpaste_BeamLine.mat を入れると歯磨き粉ビームになります。",
                    MessageType.Info);
                return;
            }

            if (material.shader == null || material.shader.name != BeamShaderName)
            {
                EditorGUILayout.HelpBox(
                    "このマテリアルは歯磨き粉ビームのシェーダーではないため、こぶ・つや・泡・流れは出ません。",
                    MessageType.Info);
                if (GUILayout.Button(new GUIContent("歯磨き粉ビームのシェーダーに切り替える",
                        "縞の絵（Base Map）と色はそのまま引き継ぎます。元に戻すときは、以前のシェーダーを選び直してください。")))
                {
                    var shader = Shader.Find(BeamShaderName);
                    if (shader != null)
                    {
                        Undo.RecordObject(material, "Switch Beam Shader");
                        material.shader = shader;
                        EditorUtility.SetDirty(material);
                    }
                }

                return;
            }

            var open = SessionState.GetBool(FoldoutKey, false);
            open = EditorGUILayout.Foldout(open, "ビームのこぶ・つや・泡・すじを調整する（マテリアル）", true);
            SessionState.SetBool(FoldoutKey, open);
            if (!open)
            {
                return;
            }

            if (_materialEditor == null || _materialEditor.target != material)
            {
                if (_materialEditor != null)
                {
                    Object.DestroyImmediate(_materialEditor);
                }

                _materialEditor = (MaterialEditor)Editor.CreateEditor(material, typeof(MaterialEditor));
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(
                    "マテリアルを書き換えるので、同じマテリアルを使う全砲台に反映されます（Play中に変えても残ります）。" +
                    "以前の見た目（つやの無い縞だけ）は Toothpaste_BeamLine_Unlit.mat に残してあります。",
                    EditorStyles.wordWrappedMiniLabel);
                _materialEditor.PropertiesGUI();
            }
        }

        private static Material FindBeamMaterial(LaserTurret[] turrets)
        {
            foreach (var turret in turrets.Where(t => t != null))
            {
                if (!turret.TryGetComponent<LaserBeamVisual>(out var beam))
                {
                    continue;
                }

                if (beam.beamMaterial != null)
                {
                    return beam.beamMaterial;
                }

                if (beam.lineRenderer != null && beam.lineRenderer.sharedMaterial != null)
                {
                    return beam.lineRenderer.sharedMaterial;
                }
            }

            return null;
        }
    }
}
