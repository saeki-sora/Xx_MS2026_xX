// 背景オブジェクト用シェーダー（URP Lit と同じ質感＋背景の反応）。
// ・URP の Lit と同じ項目名なので、Lit のマテリアルからそのまま切り替えられる（ステージ背景スタジオの「専用シェーダーに切り替え」）。
// ・追加の反応: 砲台・コアを隠す所に穴をあける / 全体を薄く / 熱で光る・焦げる / 壊れると溶ける / 被弾で光る / 暗くなる。
//   値は C# の StagePropRenderer・StageFocusPoints が渡す（Runtime/Rendering/StageShaderIds.cs）。
// ・ステンシルに印（64）を書き、群衆の敵がこの裏に隠れたときだけシルエットを出せるようにする（床用は 0 にする）。
// ・影・深度は URP の Lit のパスをそのまま使う（穴は色の描画にだけ効く。デプスプライミングOFFが前提）。
Shader "MS2026/Stage/Prop"
{
    Properties
    {
        [MainTexture] _BaseMap("基本の色（テクスチャ）", 2D) = "white" {}
        [MainColor] _BaseColor("基本の色", Color) = (1,1,1,1)
        _Cutoff("切り抜きのしきい値", Range(0.0, 1.0)) = 0.5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("透明部分を切り抜く", Float) = 0.0

        _Smoothness("つや", Range(0.0, 1.0)) = 0.5
        _Metallic("金属っぽさ", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("金属・つや（テクスチャ）", 2D) = "white" {}
        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular（テクスチャ）", 2D) = "white" {}

        _BumpScale("凹凸の強さ", Float) = 1.0
        [Normal] _BumpMap("凹凸（ノーマルマップ）", 2D) = "bump" {}
        _Parallax("Parallax", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Parallax（テクスチャ）", 2D) = "black" {}
        _OcclusionStrength("陰りの強さ", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("陰り（テクスチャ）", 2D) = "white" {}

        [HDR] _EmissionColor("自己発光の色", Color) = (0,0,0)
        _EmissionMap("自己発光（テクスチャ）", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Detail Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Detail Normal Scale", Range(0.0, 2.0)) = 1.0
        [Normal] _DetailNormalMap("Detail Normal", 2D) = "bump" {}
        _ClearCoatMask("Clear Coat Mask", Float) = 0.0
        _ClearCoatSmoothness("Clear Coat Smoothness", Float) = 0.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull("裏面を描くか（Off=両面）", Float) = 2.0

        [Header(Stage)]
        [Toggle(_STAGE_NO_HOLES)] _StageNoHoles("透け穴をあけない（床用）", Float) = 0.0
        [IntRange] _StencilRef("敵の影の印（64=出す / 0=出さない）", Range(0, 255)) = 64

        // URP の Lit と同じ内部用の値（触らない）
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _QueueOffset("Queue offset", Float) = 0.0
        [HideInInspector] _ReceiveShadows("Receive Shadows", Float) = 1.0
        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Base Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "StageForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]

            Stencil
            {
                Ref [_StencilRef]
                WriteMask 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex LitPassVertex
            #pragma fragment StagePropFragment

            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local _PARALLAXMAP
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF
            #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_local_fragment _OCCLUSIONMAP
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _STAGE_NO_HOLES

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.core/ShaderLibrary/FoveatedRenderingKeywords.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile _ USE_LEGACY_LIGHTMAPS
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ProbeVolumeVariants.hlsl"

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            #include "Assets/_Game/Stage/Runtime/Shaders/StagePropEffects.hlsl"

            // URP の LitPassFragment と同じ流れに、背景の反応（StageClip / StageModifySurface）を足したもの。
            void StagePropFragment(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                StageClip(input.positionWS, input.positionCS);

            #if defined(_PARALLAXMAP)
            #if defined(REQUIRES_TANGENT_SPACE_VIEW_DIR_INTERPOLATOR)
                half3 viewDirTS = input.viewDirTS;
            #else
                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 viewDirTS = GetViewDirectionTangentSpace(input.tangentWS, input.normalWS, viewDirWS);
            #endif
                ApplyPerPixelDisplacement(viewDirTS, input.uv);
            #endif

                SurfaceData surfaceData;
                InitializeStandardLitSurfaceData(input.uv, surfaceData);
                StageModifySurface(input.positionWS, surfaceData.albedo, surfaceData.emission, surfaceData.smoothness);

            #ifdef LOD_FADE_CROSSFADE
                LODFadeCrossFade(input.positionCS);
            #endif

                InputData inputData;
                InitializeInputData(input, surfaceData.normalTS, inputData);
                SETUP_DEBUG_TEXTURE_DATA(inputData, UNDO_TRANSFORM_TEX(input.uv, _BaseMap));

            #if defined(_DBUFFER)
                ApplyDecalToSurfaceData(input.positionCS, surfaceData, inputData);
            #endif

                InitializeBakedGIData(input, inputData);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1.0;

                outColor = color;

            #ifdef _WRITE_RENDERING_LAYERS
                outRenderingLayers = EncodeMeshRenderingLayer();
            #endif
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
        UsePass "Universal Render Pipeline/Lit/META"
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "MS2026.Stage.EditorTools.StagePropShaderGUI"
}
