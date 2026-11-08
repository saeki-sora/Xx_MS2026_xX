// 歯のコア（3Dモデル）用のシェーダー。ステージ1の物と同じトゥーン（MS2026/3DObject/Toon）の光の当たり方・輪郭線をそのまま使い、
// コアの状態を足す（2026-11-07 ユーザー決定: 比較ページの A を基本、ひびだけ C）:
// ・汚れ: HP が減るほど、黄ばみのしみが根元から広がる（後半は茶色く）。
// ・ふちの光: 歯のふちの光の帯がゆっくり脈打つ。HP が減るほど赤く速く。
// ・ひび: HP がしきい値を下回ると、描いた暗い線が少しずつ入り、線の中から赤い光が漏れる。ダメージの瞬間は白く光る。
// トゥーンの値（影の色・輪郭線…）は材質に入れる（項目名は MS2026/3DObject/Toon と同じなので、同じ調整ができる）。
// コアの状態（HP・脈・色）は CoreToothLook が MaterialPropertyBlock で毎フレーム入れる。
// トゥーンの処理は Assets/3DObjectshader/Shaders/ の HLSL を読むだけで使う（それらのファイルは変えない）。
Shader "MS2026/Fortress/CoreTooth"
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
            Name "CoreToothForward"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One Zero
            ZWrite On
            Cull [_Cull]

            // ステージの物と同じ「敵の影の印」（群衆の影を歯の上に出す）
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
            #pragma fragment CoreToothForwardFragment

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            // 歯はコアそのものなので、「砲台・コアを隠す所に穴をあける」処理は使わない。
            #define _STAGE_NO_HOLES 1
            #include "Assets/3DObjectshader/Shaders/ObjectToonPasses.hlsl"
            #include "Assets/_Game/Fortress/Runtime/Resources/Fortress/CoreTooth.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "CoreToothOutline"
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
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #define _STAGE_NO_HOLES 1
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
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            #define _STAGE_NO_HOLES 1
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
            #pragma multi_compile_instancing

            #define _STAGE_NO_HOLES 1
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
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            #define _STAGE_NO_HOLES 1
            #include "Assets/3DObjectshader/Shaders/ObjectToonPasses.hlsl"
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "MS2026.ObjectToon.EditorTools.ObjectToonShaderGUI"
}
