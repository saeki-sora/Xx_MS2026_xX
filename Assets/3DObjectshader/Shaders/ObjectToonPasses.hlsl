// 3Dオブジェクト用トゥーンの頂点・画素の処理（全パス共通）。
// 元: SimpleURPToonLitOutlineExample_Shared.hlsl（ColinLeung-NiloCat, MIT。ThirdPartyNotices を参照）を書き直したもの。
// 変更点: ステージの物の反応（透け穴・薄さ・溶け・熱・焦げ・被弾の光り = StagePropEffects.hlsl）とビームの光を入れた。
//         輪郭線は光の計算をせず色を塗るだけにした（輪郭線のパスを軽く）。両面描画のときは裏面の向きを反転する。
// パスごとに、include の前に次のどれかを define する:
//   OBJECT_TOON_OUTLINE / OBJECT_TOON_SHADOW_CASTER / （どれも無し = 本体・深度）
#ifndef MS2026_OBJECT_TOON_PASSES_INCLUDED
#define MS2026_OBJECT_TOON_PASSES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Assets/3DObjectshader/Shaders/ObjectToonInput.hlsl"
#include "Assets/3DObjectshader/Shaders/ObjectToonOutlineUtil.hlsl"
// ステージの物と同じ反応・ビームの光を使う（読むだけ。これらのファイルは変えない）。
#include "Assets/_Game/Stage/Runtime/Shaders/StagePropEffects.hlsl"
#include "Assets/_Game/Fortress/Runtime/Laser/Light/Shaders/FortressBeamLight.hlsl"
#include "Assets/3DObjectshader/Shaders/ObjectToonLighting.hlsl"

struct Attributes
{
    float3 positionOS : POSITION;
    half3  normalOS   : NORMAL;
    float2 uv         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                  : TEXCOORD0;
    float4 positionWSAndFog    : TEXCOORD1; // xyz = ワールド位置, w = 霧
    half3  normalWS            : TEXCOORD2;
    float4 positionCS          : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

#if defined(OBJECT_TOON_SHADOW_CASTER)
float3 _LightDirection;
float3 _LightPosition;
#endif

Varyings ObjectToonVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float3 positionWS = TransformObjectToWorld(input.positionOS);
    half3 normalWS = TransformObjectToWorldNormal(input.normalOS);

#if defined(OBJECT_TOON_OUTLINE)
    positionWS = ObjectToonOutlinePositionWS(positionWS, normalWS);
#endif

    float4 positionCS = TransformWorldToHClip(positionWS);

#if defined(OBJECT_TOON_OUTLINE)
    positionCS = ObjectToonApplyZOffset(positionCS, _OutlineZOffset);
#endif

#if defined(OBJECT_TOON_SHADOW_CASTER)
    #if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
    #else
    float3 lightDirectionWS = _LightDirection;
    #endif
    positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
#endif

    output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
    output.positionWSAndFog = float4(positionWS, ComputeFogFactor(positionCS.z));
    output.normalWS = normalWS;
    output.positionCS = positionCS;
    return output;
}

half4 ObjectToonBaseColor(float2 uv)
{
    return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;
}

void ObjectToonAlphaClip(float2 uv)
{
#if defined(_ALPHATEST_ON)
    clip(ObjectToonBaseColor(uv).a - _Cutoff);
#endif
}

// 本体（UniversalForwardOnly）
half4 ObjectToonForwardFragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = input.positionWSAndFog.xyz;
    StageClip(positionWS, input.positionCS);

    half4 baseColor = ObjectToonBaseColor(input.uv);
#if defined(_ALPHATEST_ON)
    clip(baseColor.a - _Cutoff);
#endif

    ObjectToonSurface s;
    s.albedo = baseColor.rgb;
    s.emission = _EmissionColor.rgb;
    half smoothness = 0.5h; // トゥーンでは使わないが、反応の関数の引数として渡す
    StageModifySurface(positionWS, s.albedo, s.emission, smoothness);

    s.normalWS = normalize(input.normalWS) * IS_FRONT_VFACE(isFrontFace, 1.0h, -1.0h);
    s.viewDirWS = GetWorldSpaceNormalizeViewDir(positionWS);
    s.positionWS = positionWS;
    s.positionCS = input.positionCS;
    s.uv = input.uv;

    half3 color = ObjectToonGrade(ObjectToonShade(s));
    color = MixFog(color, input.positionWSAndFog.w);
    return half4(color, 1.0h);
}

// 輪郭線（SRPDefaultUnlit）。光の計算はせず、線の色（物の色を混ぜられる）を塗るだけ。
half4 ObjectToonOutlineFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    float3 positionWS = input.positionWSAndFog.xyz;
    StageClip(positionWS, input.positionCS);

    half4 baseColor = ObjectToonBaseColor(input.uv);
#if defined(_ALPHATEST_ON)
    clip(baseColor.a - _Cutoff);
#endif

    half3 albedo = baseColor.rgb;
    half3 emission = 0;
    half smoothness = 0.5h;
    StageModifySurface(positionWS, albedo, emission, smoothness);

    half3 color = lerp(_OutlineColor.rgb, albedo * _OutlineColor.rgb, _OutlineBaseColorMix) + emission;
    color = MixFog(ObjectToonGrade(color), input.positionWSAndFog.w);
    return half4(color, 1.0h);
}

// 影・深度（色は書かない）
void ObjectToonDepthClipFragment(Varyings input)
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ObjectToonAlphaClip(input.uv);
}

half4 ObjectToonDepthOnlyFragment(Varyings input) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ObjectToonAlphaClip(input.uv);
    return input.positionCS.z;
}

half4 ObjectToonDepthNormalsFragment(Varyings input, FRONT_FACE_TYPE isFrontFace : FRONT_FACE_SEMANTIC) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    ObjectToonAlphaClip(input.uv);

    float3 normalWS = normalize((float3)input.normalWS) * IS_FRONT_VFACE(isFrontFace, 1.0, -1.0);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
#else
    return half4(NormalizeNormalPerPixel(normalWS), 0.0);
#endif
}

#endif
