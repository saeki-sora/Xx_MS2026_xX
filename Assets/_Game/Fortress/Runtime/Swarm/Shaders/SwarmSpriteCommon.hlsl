// 群衆(Swarm)の描画で共通の部分: 敵1体分のデータ(_Instances)・マテリアルの値・頂点シェーダー(vert)。
// SwarmSprite.shader(本体)と SwarmSilhouette.shader(背景の裏に隠れた敵の影)の両方が使う。
// ここを変えると両方に効く(影が本体とずれないように、位置の計算はここだけに置く)。
#ifndef MS2026_SWARM_SPRITE_COMMON_INCLUDED
#define MS2026_SWARM_SPRITE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/_Game/Fortress/Runtime/Billboard/Shaders/FortressBillboard.hlsl"

struct SwarmInstance
{
    float4 a; // x, y, size, flash
    float4 b; // frame, direction, unused, unused
};

StructuredBuffer<SwarmInstance> _Instances;

// グローバル値(BillboardShaderGlobals)。
float _FortressSwarmBillboardStand;
float _FortressBillboardCutoff;

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _Tint;
    float _Cols;
    float _Rows;
    float _InstanceOffset;
CBUFFER_END

struct Attributes
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
    uint instanceID : SV_InstanceID;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
    float flash : TEXCOORD1;
};

Varyings vert(Attributes input)
{
    Varyings output;
    SwarmInstance inst = _Instances[input.instanceID + (uint)_InstanceOffset];

    // 四角形は -0.5〜0.5 の大きさ1。立たせないとき(stand=0)は従来通り地面に寝た位置になる。
    float size = inst.a.z;
    float3 offset = FortressBillboardOffset(
        input.positionOS.xy * size, float2(0.0, 0.0), float2(0.5 * size, 0.0), float2(0.0, 0.5 * size),
        _FortressSwarmBillboardStand);
    output.positionCS = TransformWorldToHClip(float3(inst.a.xy, 0.0) + offset);

    float frame = inst.b.x;
    float row = (_Rows - 1.0) - inst.b.y;
    output.uv = float2((input.uv.x + frame) / _Cols, (input.uv.y + row) / _Rows);
    output.flash = inst.a.w;
    return output;
}

#endif
