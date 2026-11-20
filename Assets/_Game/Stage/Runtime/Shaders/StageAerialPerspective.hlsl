// 空気遠近法（遠くの物ほど空気の色に霞む）。MS2026/Stage/Prop の「空気遠近法を使う」を ON にしたマテリアルだけで動く。
// ・霞む量は2つの理由を合わせて決める。
//     1) ステージの中心から遠いほど霞む（画面の外側＝遠景に置いた飾りが奥に引っ込んで見える）
//     2) 床に近い所ほど霞む（真上から見るカメラでは「低い所＝カメラから遠い所」なので、足もとが空気に沈む）
// ・霞むと「色が抜ける → 霞の色に寄る」の順で変わる。光・影・ビームの光を付けた後の色に掛けるので、遠くの物は明暗の差も弱まる。
// ・この値はマテリアルごとの設定。OFF のマテリアル（ほかの背景すべて）には何も足さない（シェーダーの切り替えで丸ごと消える）。
//   ON の版だけは値が UnityPerMaterial の外にあるので SRP Batcher でまとめて描かれないが、使う物が少ないので気にしなくてよい。
#ifndef MS2026_STAGE_AERIAL_PERSPECTIVE_INCLUDED
#define MS2026_STAGE_AERIAL_PERSPECTIVE_INCLUDED

#if defined(_STAGE_AERIAL)

half4 _AerialColor;        // 霞の色（空気の色）
half _AerialStrength;      // いちばん霞んだときの強さ（0..1）
half _AerialDesaturate;    // 霞むときに色を抜く量（0..1）
float4 _AerialCenter;      // xy = 霞の中心（ふつうはステージの中心）
float _AerialNear;         // 中心からこの距離までは霞まない（m）
float _AerialFar;          // 中心からこの距離で霞みきる（m）
half _AerialFloor;         // 足もとの霞の強さ（0..1）
float _AerialFloorHeight;  // 床からこの高さより上は足もとの霞が消える（m）

// 0 = 霞まない、1 = 霞の色そのもの。
half StageAerialAmount(float3 positionWS)
{
    float fromCenter = length(positionWS.xy - _AerialCenter.xy);
    half far01 = saturate((fromCenter - _AerialNear) / max(_AerialFar - _AerialNear, 0.001));

    // マップは XY 平面（z = 0 が床）、上は −Z。
    float height = -positionWS.z;
    half low01 = 1.0 - smoothstep(0.0, max(_AerialFloorHeight, 0.001), height);

    // 2つを「重ねる」（どちらかが強ければ霞む。両方なら少し足される）。
    half amount = 1.0 - (1.0 - far01) * (1.0 - _AerialFloor * low01);
    return saturate(amount * _AerialStrength);
}

half3 StageApplyAerialPerspective(half3 color, float3 positionWS)
{
    half amount = StageAerialAmount(positionWS);
    half luma = dot(color, half3(0.2126, 0.7152, 0.0722));
    color = lerp(color, luma.xxx, amount * _AerialDesaturate);
    return lerp(color, _AerialColor.rgb, amount);
}

#endif // _STAGE_AERIAL

#endif // MS2026_STAGE_AERIAL_PERSPECTIVE_INCLUDED
