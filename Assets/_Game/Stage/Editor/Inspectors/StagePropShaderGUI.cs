using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 専用シェーダー（MS2026/Stage/Prop）のマテリアルの見た目。項目は普通に並べ、
    /// テクスチャを入れた／外したときに必要なシェーダーの切り替え（キーワード）を自動で合わせる。
    /// 空気遠近法の細かい項目（_Aerial〜）は「空気遠近法を使う」が ON のときだけ出す。
    /// </summary>
    public sealed class StagePropShaderGUI : ShaderGUI
    {
        private const string AerialToggle = "_StageAerial";
        private const string AerialPrefix = "_Aerial";

        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox(
                "背景オブジェクト用のシェーダーです。色・テクスチャは URP の Lit と同じです。\n" +
                "透け・焦げ・光り・溶けはゲーム中に自動で付きます（ここで設定する物ではありません）。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            DrawProperties(materialEditor, properties);
            if (EditorGUI.EndChangeCheck())
            {
                foreach (var target in materialEditor.targets)
                {
                    if (target is Material material)
                    {
                        SetupKeywords(material);
                    }
                }
            }
        }

        /// <summary>base.OnGUI と同じ並びで描き、空気遠近法の細かい項目だけ ON のときに限って出す。</summary>
        private static void DrawProperties(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            var shader = (materialEditor.target as Material)?.shader;
            var toggle = FindProperty(AerialToggle, properties, false);
            var aerialOn = toggle != null && (toggle.hasMixedValue || toggle.floatValue > 0.5f);

            materialEditor.SetDefaultGUIWidths();
            foreach (var property in properties)
            {
                if (IsHidden(shader, property.name))
                {
                    continue;
                }

                var isAerialDetail = property.name.StartsWith(AerialPrefix, System.StringComparison.Ordinal);
                if (isAerialDetail && !aerialOn)
                {
                    continue;
                }

                if (property.name == AerialToggle)
                {
                    EditorGUILayout.Space();
                    EditorGUILayout.LabelField("空気遠近法", EditorStyles.boldLabel);
                }

                EditorGUI.indentLevel += isAerialDetail ? 1 : 0;
                materialEditor.ShaderProperty(property, property.displayName);
                EditorGUI.indentLevel -= isAerialDetail ? 1 : 0;

                if (property.name == AerialToggle && aerialOn)
                {
                    EditorGUILayout.HelpBox(
                        "ステージの中心から遠いほど、また床に近いほど「霞の色」に霞みます。\n" +
                        "霞の色は背景（画面の奥の絵）の色に近づけると、奥に溶けこんで見えます。",
                        MessageType.None);
                }
            }

            EditorGUILayout.Space();
            materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
        }

        private static bool IsHidden(Shader shader, string propertyName)
        {
            if (shader == null)
            {
                return false;
            }

            var index = shader.FindPropertyIndex(propertyName);
            return index >= 0 && (shader.GetPropertyFlags(index) & UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) != 0;
        }

        public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
        {
            base.AssignNewShaderToMaterial(material, oldShader, newShader);
            SetupKeywords(material);
        }

        /// <summary>テクスチャや色の有無に合わせて、シェーダーの機能のON/OFFをそろえる。</summary>
        public static void SetupKeywords(Material material)
        {
            Set(material, "_NORMALMAP", material.GetTexture("_BumpMap") != null || material.GetTexture("_DetailNormalMap") != null);
            Set(material, "_METALLICSPECGLOSSMAP", material.GetTexture("_MetallicGlossMap") != null);
            Set(material, "_OCCLUSIONMAP", material.GetTexture("_OcclusionMap") != null);
            Set(material, "_ALPHATEST_ON", material.GetFloat("_AlphaClip") > 0.5f);
            Set(material, "_STAGE_NO_HOLES", material.GetFloat("_StageNoHoles") > 0.5f);
            Set(material, "_STAGE_AERIAL", material.HasProperty(AerialToggle) && material.GetFloat(AerialToggle) > 0.5f);

            var emission = material.GetColor("_EmissionColor");
            var hasEmission = emission.maxColorComponent > 0.001f || material.GetTexture("_EmissionMap") != null;
            Set(material, "_EMISSION", hasEmission);
            material.globalIlluminationFlags = hasEmission
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        }

        private static void Set(Material material, string keyword, bool on)
        {
            if (on)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }
    }
}
