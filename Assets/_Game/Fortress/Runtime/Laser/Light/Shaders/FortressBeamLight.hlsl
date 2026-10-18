// ビームがまわりを照らす光（LaserBeamLight → BeamLightGlobals が毎フレーム入れるグローバル値）。4本ぶん、砲台（プレイヤー）ごとに1枠。
// ・群衆の敵: FortressBeamEnemyLight（SwarmSpriteCommon.hlsl が敵1体ごとに使う）。
// ・床・背景の物: FortressBeamSurfaceLight（ステージ背景の MS2026/Stage/Prop が画素ごとに使う）。
//   ビームの線分そのものを光源にするので、点の光を並べたときのようなまだらにならず、線に沿って途切れなく照らす。
// 値が一度も入っていないとき（ビームの無いシーン）は全部0なので、何も変わらない。
#ifndef MS2026_FORTRESS_BEAM_LIGHT_INCLUDED
#define MS2026_FORTRESS_BEAM_LIGHT_INCLUDED

float4 _FortressBeamLightSegments[4]; // xy=始点, zw=終点（地面の座標）
float4 _FortressBeamLightColors[4];   // 敵用: rgb=色×明るさ, a=照らす距離(メートル)
float4 _FortressBeamFloorColors[4];   // 床・背景用: rgb=色×明るさ, a=光の届く距離(メートル)
float4 _FortressBeamFloorParams[4];   // x=ビームの高さ(z), y=光を浮かせる高さ(-Zが上), z=当たっている場所の明るさの追加分(0=当たっていない), w=物の色の影響(0〜1)
float _FortressBeamEnemyOverlay;      // 照らされた敵に光の色をそのまま乗せる量(0〜1)

// 地面上の位置に届く、敵用の光の合計（線分からの距離で弱くなる）。
half3 FortressBeamEnemyLight(float2 position)
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

// 1つの光源点からの明るさ（距離で弱くなる・面の向きで変わる。真横からでも少しは照らす）。
half3 FortressBeamLightFrom(float3 lightPosition, float3 positionWS, half3 normalWS, half3 color, float range)
{
    float3 toLight = lightPosition - positionWS;
    float d = length(toLight);
    float falloff = saturate(1.0 - d / max(range, 1e-3));
    half facing = saturate(dot(normalWS, toLight / max(d, 1e-4))) * 0.75h + 0.25h;
    return color * (falloff * falloff) * facing;
}

// 床・背景の物の面に足す色。物の色(albedo)の掛かり方は「物の色の影響」(param.w)で変える:
// 1=本物の光と同じく物の色を掛ける（緑の床に赤い光はくすむ） / 0=物の明るさだけ掛けて、光の色をそのまま乗せる（色がはっきり出る）。
half3 FortressBeamSurfaceLight(float3 positionWS, half3 normalWS, half3 albedo)
{
    half3 sum = 0;
    half albedoLuma = dot(albedo, half3(0.2126h, 0.7152h, 0.0722h));
    [unroll]
    for (int i = 0; i < 4; i++)
    {
        float4 light = _FortressBeamFloorColors[i];
        if (light.r + light.g + light.b <= 0.0)
        {
            continue;
        }

        float4 segment = _FortressBeamLightSegments[i];
        float4 param = _FortressBeamFloorParams[i];
        float z = param.x - param.y;
        float3 a = float3(segment.xy, z);
        float3 b = float3(segment.zw, z);
        float3 ab = b - a;
        // この画素から一番近い、ビーム上の点を光源にする（線光源）。
        float t = saturate(dot(positionWS - a, ab) / max(dot(ab, ab), 1e-5));
        half3 received = FortressBeamLightFrom(a + ab * t, positionWS, normalWS, light.rgb, light.a);

        if (param.z > 0.0)
        {
            received += FortressBeamLightFrom(b, positionWS, normalWS, light.rgb * param.z, light.a);
        }

        sum += received * lerp(albedoLuma.xxx, albedo, saturate(param.w));
    }

    return sum;
}

#endif
