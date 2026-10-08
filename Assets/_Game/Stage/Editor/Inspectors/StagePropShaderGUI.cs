using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>
    /// 専用シェーダー（MS2026/Stage/Prop）のマテリアルの見た目。項目は普通に並べ、
    /// テクスチャを入れた／外したときに必要なシェーダーの切り替え（キーワード）を自動で合わせる。
    /// </summary>
    public sealed class StagePropShaderGUI : ShaderGUI
    {
        public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
        {
            EditorGUILayout.HelpBox(
                "背景オブジェクト用のシェーダーです。色・テクスチャは URP の Lit と同じです。\n" +
                "透け・焦げ・光り・溶けはゲーム中に自動で付きます（ここで設定する物ではありません）。",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            base.OnGUI(materialEditor, properties);
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
