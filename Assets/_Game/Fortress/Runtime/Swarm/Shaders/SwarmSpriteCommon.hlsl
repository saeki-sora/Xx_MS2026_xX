// 群衆(Swarm)の描画で共通の部分: 敵1体分のデータ(_Instances)・マテリアルの値・頂点シェーダー(vert)。
// SwarmSprite.shader(本体)と SwarmSilhouette.shader(背景の裏に隠れた敵の影)の両方が使う。
// ここを変えると両方に効く(影が本体とずれないように、位置の計算はここだけに置く)。
#ifndef MS2026_SWARM_SPRITE_COMMON_INCLUDED
#define MS2026_SWARM_SPRITE_COMMON_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Assets/_Game/Fortress/Runtime/Billboard/Shaders/FortressBillboard.hlsl"
#include "Assets/_Game/Fortress/Runtime/Laser/Light/Shaders/FortressBeamLight.hlsl"

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
    // ビームの光（FortressBeamLight.hlsl）。敵1体につき頂点で計算する（画素ごとではないので軽い）。
    output.beamLight = FortressBeamEnemyLight(inst.a.xy);
    return output;
}

#endif
