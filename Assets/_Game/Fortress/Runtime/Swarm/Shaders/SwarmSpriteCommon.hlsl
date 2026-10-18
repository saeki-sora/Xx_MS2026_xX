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
    float4 b; // frame, facing angle (ラジアン, ワールド, 0=+X 反時計回り), unused, blink (爆弾の敵の点滅 0〜1。点滅の色は _BlinkColor)
};

StructuredBuffer<SwarmInstance> _Instances;

// グローバル値(BillboardShaderGlobals)。
float _FortressSwarmBillboardStand;
float _FortressBillboardCutoff;
// 1なら絵の上を画面の上に合わせる(画面を回した視点用)。0ならワールドの+Yが絵の上。視点プリセットの「敵の絵を画面の向きに合わせる」。
float _FortressEnemyScreenAlign;

// ビームの光(BeamLightGlobals / LaserBeamLight)。4本ぶん。xy=始点 zw=終点、rgb=色×明るさ a=照らす距離(メートル)。
// 撃っていないビームは明るさ0。値が一度も入っていない(群衆だけのテストシーンなど)ときは0のままで何も変わらない。
float4 _FortressBeamLightSegments[4];
float4 _FortressBeamLightColors[4];

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);

CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    float4 _Tint;
    float _Cols;
    float _Rows;
    float _InstanceOffset;
    float4 _BlinkColor;
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
    float blink : TEXCOORD2;
    half3 beamLight : TEXCOORD3;
};

// 敵の位置(地面)に届くビームの光の合計。敵1体につき頂点で計算する(画素ごとではないので軽い)。
half3 SwarmBeamLight(float2 position)
{
    half3 sum = 0;
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float4 segment = _FortressBeamLightSegments[i];
        float4 light = _FortressBeamLightColors[i];
        float2 a = segment.xy;
        float2 ab = segment.zw - a;
        float t = saturate(dot(position - a, ab) / max(dot(ab, ab), 1e-5));
        float d = length(position - (a + ab * t));
        float falloff = saturate(1.0 - d / max(light.a, 1e-3));
        sum += light.rgb * (falloff * falloff);
    }

    return sum;
}

// 絵の「上」にする地面上の向き。画面に合わせるときは描画中のカメラの上方向なので、4人の画面がそれぞれ回っていても各自の画面で正立する。
float2 SwarmSpriteUp()
{
    return _FortressEnemyScreenAlign > 0.5 ? FortressGroundUp() : float2(0.0, 1.0);
}

// 向いている角度(ワールド)から、スプライトシートの行(0=右 から反時計回り)を選ぶ。角度は絵の右方向から測るので、画面に合わせると左右も画面基準になる。
float SwarmDirectionIndex(float worldAngle, float2 spriteRight, float directions)
{
    if (directions < 1.5)
    {
        return 0.0;
    }

    const float twoPi = 6.28318530718;
    float angle = worldAngle - atan2(spriteRight.y, spriteRight.x);
    angle -= twoPi * floor(angle / twoPi);
    return fmod(floor(angle / (twoPi / directions) + 0.5), directions);
}

Varyings vert(Attributes input)
{
    Varyings output;
    SwarmInstance inst = _Instances[input.instanceID + (uint)_InstanceOffset];

    // 四角形は -0.5〜0.5 の大きさ1。立たせないとき(stand=0)は従来通り地面に寝た位置になる。
    float size = inst.a.z;
    float2 spriteUp = SwarmSpriteUp();
    float2 spriteRight = float2(spriteUp.y, -spriteUp.x);
    float2 quad = (spriteRight * input.positionOS.x + spriteUp * input.positionOS.y) * size;
    float3 offset = FortressBillboardOffset(
        quad, float2(0.0, 0.0), spriteRight * (0.5 * size), spriteUp * (0.5 * size),
        _FortressSwarmBillboardStand);
    output.positionCS = TransformWorldToHClip(float3(inst.a.xy, 0.0) + offset);

    float frame = inst.b.x;
    float row = (_Rows - 1.0) - SwarmDirectionIndex(inst.b.y, spriteRight, _Rows);
    output.uv = float2((input.uv.x + frame) / _Cols, (input.uv.y + row) / _Rows);
    output.flash = inst.a.w;
    output.blink = inst.b.w;
    output.beamLight = SwarmBeamLight(inst.a.xy);
    return output;
}

#endif
