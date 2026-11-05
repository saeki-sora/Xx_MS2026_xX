// 3Dオブジェクト用トゥーンの材質の値。全部のパスで同じ並びにして SRP Batcher を効かせる。
// 元: ColinLeung-NiloCat/UnityURPToonLitShaderExample（MIT。ThirdPartyNotices を参照）を書き直したもの。
#ifndef MS2026_OBJECT_TOON_INPUT_INCLUDED
#define MS2026_OBJECT_TOON_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _Cutoff;

    // 影（明暗の境目）
    half4  _ShadowColor;
    half   _CelMidPoint;
    half   _CelSoftness;
    half   _ReceiveShadowStrength;
    float  _ReceiveShadowOffset;

    // 太陽の光
    half   _DirectStrength;
    half   _IgnoreLightIntensity;
    half   _LightColorInfluence;

    // 追加の光（点光源など）
    half   _AdditionalLightsOn;
    half   _AdditionalLightStrength;
    half   _AdditionalLightCelBlend;

    // まわりの光
    half4  _IndirectMinColor;
    half   _IndirectStrength;
    half   _IndirectFlatten;
    half   _LightAddBlend;

    // ハイライト
    half   _SpecularOn;
    half4  _SpecularColor;
    half   _SpecularSize;
    half   _SpecularSoftness;

    // ふちの光
    half   _RimOn;
    half4  _RimColor;
    half   _RimWidth;
    half   _RimSoftness;
    half   _RimLitSideOnly;

    // 鏡・つや（映り込みは絵の具で描いたような色の段。テクスチャは読まない）
    half   _ReflectOn;
    half   _ReflectStrength;
    half4  _ReflectSkyColor;
    half4  _ReflectHorizonColor;
    half4  _ReflectGroundColor;
    half   _ReflectSteps;
    half   _ReflectFresnel;
    half   _ReflectEnvBlend;

    // 鏡の斜めの光の筋
    half   _StreakOn;
    half4  _StreakColor;
    half   _StreakAngle;
    half   _StreakWidth;
    half   _StreakGap;
    half   _StreakOffset;
    half   _StreakSoftness;
    half   _StreakScreenSpace;

    // 自己発光・ビームの光・仕上げ
    half4  _EmissionColor;
    half   _BeamLightStrength;
    half   _Saturation;
    half   _Brightness;

    // 輪郭線
    float  _OutlineWidth;
    half4  _OutlineColor;
    half   _OutlineBaseColorMix;
    float  _OutlineDirection;
    float  _OutlineZOffset;
CBUFFER_END

#endif
