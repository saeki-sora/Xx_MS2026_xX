// 3Dオブジェクト（ステージの背景の物）用のトゥーンシェーダー。
// 元: ColinLeung-NiloCat/UnityURPToonLitShaderExample の SimpleURPToonLitOutlineExample.shader（MIT License）。
//     著作権表示と許諾文は Assets/3DObjectshader/ThirdPartyNotices/ にある（消さないこと）。
// 変更点（軽くするため）:
//   ・使っていない機能のキーワード（ライトマップ・デカール・SSAO・ライトクッキー・反射プローブ等）を外し、シェーダーの種類を大幅に減らした。
//   ・オクルージョン・自己発光のテクスチャを外し、テクスチャを読むのは基本の色の1枚だけ。
//   ・輪郭線は光の計算をしない。輪郭線を使わない材質では輪郭線のパスそのものを止める（描画1回ぶん減る）。
//   ・ハイライト・ふちの光・追加の光は材質の値で切り替える（全画素で同じ分岐なので、ほぼ負担なし）。
// ステージの物（MS2026/Stage/Prop）と同じく、透け穴・薄さ・溶け・熱・焦げ・被弾の光り・ビームの光・敵の影の印（ステンシル）に対応する。
Shader "MS2026/3DObject/Toon"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap("基本の色（テクスチャ）", 2D) = "white" {}
        [MainColor] _BaseColor("基本の色", Color) = (1,1,1,1)
        [Toggle(_ALPHATEST_ON)] _AlphaClip("透明部分を切り抜く", Float) = 0
        _Cutoff("切り抜きのしきい値", Range(0, 1)) = 0.5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("裏面を描くか（Off=両面）", Float) = 2

        [Header(Shadow)]
        _ShadowColor("影の色", Color) = (0.62, 0.66, 0.82, 1)
        _CelMidPoint("明暗の境目の位置", Range(-1, 1)) = 0
        _CelSoftness("明暗の境目のぼかし", Range(0, 1)) = 0.05
        _ReceiveShadowStrength("落ち影の濃さ", Range(0, 1)) = 0.65
        _ReceiveShadowOffset("落ち影のずらし（しま模様が出るとき）", Range(0, 1)) = 0

        [Header(Direct Light)]
        _DirectStrength("太陽の明るさ", Range(0, 2)) = 1
        _IgnoreLightIntensity("太陽の強さを無視する", Range(0, 1)) = 1
        _LightColorInfluence("太陽の色を乗せる量", Range(0, 1)) = 1
        [ToggleUI] _AdditionalLightsOn("追加の光（点光源など）を受ける", Float) = 1
        _AdditionalLightStrength("追加の光の強さ", Range(0, 2)) = 0.25
        _AdditionalLightCelBlend("追加の光の明暗をなくす", Range(0, 1)) = 0.9

        [Header(Indirect Light)]
        _IndirectMinColor("まわりの光の最低の明るさ", Color) = (0.1, 0.1, 0.1, 1)
        _IndirectStrength("まわりの光の強さ", Range(0, 2)) = 1
        _IndirectFlatten("まわりの光を平らにする", Range(0, 1)) = 0.7
        _LightAddBlend("光の合わせ方（0=明るい方 / 1=足す）", Range(0, 1)) = 0

        [Header(Specular)]
        [ToggleUI] _SpecularOn("ハイライトを出す", Float) = 0
        [HDR] _SpecularColor("ハイライトの色", Color) = (0.6, 0.6, 0.6, 1)
        _SpecularSize("ハイライトの大きさ", Range(0, 1)) = 0.3
        _SpecularSoftness("ハイライトのぼかし", Range(0, 1)) = 0.1

        [Header(Rim)]
        [ToggleUI] _RimOn("ふちの光を出す", Float) = 0
        [HDR] _RimColor("ふちの光の色", Color) = (0.35, 0.35, 0.35, 1)
        _RimWidth("ふちの光の幅", Range(0, 1)) = 0.25
        _RimSoftness("ふちの光のぼかし", Range(0, 1)) = 0.1
        _RimLitSideOnly("明るい側だけに出す", Range(0, 1)) = 0.5

        [Header(Mirror)]
        [ToggleUI] _ReflectOn("映り込みを出す（鏡・金属）", Float) = 0
        _ReflectStrength("映り込みの強さ", Range(0, 1)) = 0.85
        _ReflectSkyColor("映り込みの上の色", Color) = (0.82, 0.93, 1, 1)
        _ReflectHorizonColor("映り込みの真ん中の色", Color) = (0.6, 0.76, 0.9, 1)
        _ReflectGroundColor("映り込みの下の色", Color) = (0.4, 0.48, 0.6, 1)
        [IntRange] _ReflectSteps("色の段の数（0=なめらか）", Range(0, 8)) = 3
        _ReflectFresnel("ふちほど強く映す", Range(0, 1)) = 0.3
        _ReflectEnvBlend("まわりの景色を混ぜる（テクスチャを1回読む）", Range(0, 1)) = 0
        [ToggleUI] _StreakOn("斜めの光の筋を出す", Float) = 0
        [HDR] _StreakColor("光の筋の色", Color) = (1.2, 1.2, 1.2, 1)
        _StreakAngle("光の筋の角度", Range(-90, 90)) = 35
        _StreakWidth("光の筋の太さ", Range(0, 0.5)) = 0.06
        _StreakGap("2本目の筋までの間", Range(-1, 1)) = 0.16
        _StreakOffset("光の筋の位置", Range(-2, 2)) = 0.5
        _StreakSoftness("光の筋のぼかし", Range(0, 1)) = 0.1
        [ToggleUI] _StreakScreenSpace("筋を画面に固定する（オフ=物に貼り付く）", Float) = 0

        [Header(Emission and Finish)]
        [HDR] _EmissionColor("自己発光の色", Color) = (0, 0, 0, 1)
        _BeamLightStrength("ビームに照らされる強さ", Range(0, 2)) = 1
        _Saturation("彩度", Range(0, 2)) = 1
        _Brightness("全体の明るさ", Range(0, 2)) = 1

        [Header(Outline)]
        [ToggleUI] _OutlineOn("輪郭線を出す", Float) = 1
        _OutlineWidth("輪郭線の太さ", Range(0, 10)) = 1
        _OutlineColor("輪郭線の色", Color) = (0.25, 0.22, 0.28, 1)
        _OutlineBaseColorMix("輪郭線に物の色を混ぜる", Range(0, 1)) = 0.5
        _OutlineDirection("押し出す向き（0=面の向き / 1=中心から外）", Range(0, 1)) = 0
        _OutlineZOffset("輪郭線を奥へずらす", Range(0, 1)) = 0.0001

        [Header(Stage)]
        [Toggle(_STAGE_NO_HOLES)] _StageNoHoles("透け穴をあけない（床用）", Float) = 0
        [IntRange] _StencilRef("敵の影の印（64=出す / 0=出さない）", Range(0, 255)) = 64
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "Queue" = "Geometry"
        }
        LOD 300

        Pass
        {
            Name "ObjectToonForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]

            // ステージの物と同じ「敵の影の印」（群衆の影を物の上に出す）
            Stencil
            {
                Ref [_StencilRef]
                WriteMask 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ObjectToonVertex
            #pragma fragment ObjectToonForwardFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _STAGE_NO_HOLES

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // URP は Forward+（_CLUSTER_LIGHT_LOOP）と _ADDITIONAL_LIGHTS を同時には付けないので、1組にまとめて種類を減らす
            // （_CLUSTER_LIGHT_LOOP のときは Core.hlsl が _ADDITIONAL_LIGHTS も定義する）。
            #pragma multi_compile _ _ADDITIONAL_LIGHTS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "Assets/3DObjectshader/Shaders/ObjectToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            // 輪郭線。材質の「輪郭線を出す」をオフにすると、このパスごと止まる（ObjectToonShaderGUI / 調整ウィンドウが切り替える）。
            Name "ObjectToonOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One Zero
            ZWrite On
            Cull Front

            Stencil
            {
                Ref [_StencilRef]
                WriteMask 64
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ObjectToonVertex
            #pragma fragment ObjectToonOutlineFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _STAGE_NO_HOLES
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #define OBJECT_TOON_OUTLINE
            #include "Assets/3DObjectshader/Shaders/ObjectToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ObjectToonVertex
            #pragma fragment ObjectToonDepthClipFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #define OBJECT_TOON_SHADOW_CASTER
            #include "Assets/3DObjectshader/Shaders/ObjectToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ObjectToonVertex
            #pragma fragment ObjectToonDepthOnlyFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_instancing

            #include "Assets/3DObjectshader/Shaders/ObjectToonPasses.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormalsOnly"
            Tags { "LightMode" = "DepthNormalsOnly" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ObjectToonVertex
            #pragma fragment ObjectToonDepthNormalsFragment

            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #include "Assets/3DObjectshader/Shaders/ObjectToonPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "MS2026.ObjectToon.EditorTools.ObjectToonShaderGUI"
}
