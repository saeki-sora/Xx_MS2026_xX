// 背景オブジェクトの「反応」の計算（透け穴・全体の薄さ・溶け・熱・焦げ・暗さ・被弾の光り）。
// 値の意味と送り元は Runtime/Rendering/StageShaderIds.cs を参照。どれも0が「何もしない」。
#ifndef MS2026_STAGE_PROP_EFFECTS_INCLUDED
#define MS2026_STAGE_PROP_EFFECTS_INCLUDED

#define STAGE_MAX_FOCUS 8
#define STAGE_MAX_HEAT 8

// ── 全体（StageFocusPoints / StageLookProfile が毎フレーム設定）──
float4 _StageFocusPoints[STAGE_MAX_FOCUS]; // xyz=砲台・コアの位置, w=透かす半径
float _StageFocusCount;
float _StageHoleSoftness;
half4 _StageHeatGlowColor;
half4 _StageHeatHotColor;
half4 _StageScorchColor;

// ── オブジェクトごと（StagePropRenderer が MaterialPropertyBlock で設定）──
float _StageFade;
float _StageNoHole;
float _StageDarken;
float _StageFlash;
half4 _StageFlashColor;
float _StageDissolve;
half4 _StageDissolveColor;
float _StageHeatCount;
float4 _StageHeatPoints[STAGE_MAX_HEAT]; // xyz=位置, w=半径
float4 _StageHeatValues[STAGE_MAX_HEAT]; // x=光り, y=焦げ

float StageHash(float3 p)
{
    p = frac(p * 0.3183099 + 0.1);
    p *= 17.0;
    return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
}

// なめらかな3Dの値ノイズ（0..1）。溶けのまだら・焦げのムラに使う。
float StageNoise(float3 x)
{
    float3 i = floor(x);
    float3 f = frac(x);
    f = f * f * (3.0 - 2.0 * f);
    return lerp(
        lerp(lerp(StageHash(i + float3(0, 0, 0)), StageHash(i + float3(1, 0, 0)), f.x),
             lerp(StageHash(i + float3(0, 1, 0)), StageHash(i + float3(1, 1, 0)), f.x), f.y),
        lerp(lerp(StageHash(i + float3(0, 0, 1)), StageHash(i + float3(1, 0, 1)), f.x),
             lerp(StageHash(i + float3(0, 1, 1)), StageHash(i + float3(1, 1, 1)), f.x), f.y),
        f.z);
}

// 4x4の網目（ディザ）。半透明を「画素を間引く」ことで表し、不透明のまま描けるようにする。
float StageDither(float2 pixel)
{
    static const float bayer[16] = { 0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5 };
    uint2 p = uint2(pixel) % 4u;
    return (bayer[p.y * 4u + p.x] + 0.5) / 16.0;
}

// 砲台・コアとカメラを結ぶ線の近く（＝それらを隠している所）ほど0になる。
float StageHoleVisibility(float3 positionWS)
{
#if defined(_STAGE_NO_HOLES)
    return 1.0;
#else
    if (_StageNoHole > 0.5)
    {
        return 1.0;
    }

    float visibility = 1.0;
    bool ortho = unity_OrthoParams.w > 0.5;
    float3 viewBack = UNITY_MATRIX_V[2].xyz; // カメラの後ろ向き（=画素からカメラへ向かう向き。正射影用）
    int count = (int)_StageFocusCount;

    [loop]
    for (int i = 0; i < STAGE_MAX_FOCUS; i++)
    {
        if (i >= count)
        {
            break;
        }

        float4 focus = _StageFocusPoints[i];
        float3 toCamera = ortho ? viewBack : normalize(_WorldSpaceCameraPos - focus.xyz);
        float3 toPixel = positionWS - focus.xyz;
        float along = dot(toPixel, toCamera);
        if (along <= 0.0)
        {
            continue; // 砲台より奥の画素は隠していない
        }

        float distanceFromLine = length(toPixel - toCamera * along);
        float soft = max(1e-3, _StageHoleSoftness * focus.w);
        float inside = smoothstep(focus.w - soft, focus.w, distanceFromLine);
        // 砲台のすぐ近く（真横）では穴をあけない。少し手前から効かせる。
        visibility = min(visibility, lerp(1.0, inside, saturate(along / 0.3)));
    }

    return visibility;
#endif
}

// 透け・全体の薄さ・溶けで、この画素を描かない（clip）かどうかを決める。不透明の描画のまま使える。
void StageClip(float3 positionWS, float4 positionCS)
{
    float visibility = StageHoleVisibility(positionWS) * (1.0 - saturate(_StageFade));
    clip(visibility - StageDither(positionCS.xy) - 1e-4);

    if (_StageDissolve > 0.0)
    {
        float n = StageNoise(positionWS * 3.0);
        clip(n - _StageDissolve * 1.02 + 0.001);
    }
}

// 色・光り・つやを、暗さ・熱・焦げ・被弾の光り・溶けのふちに合わせて変える。
void StageModifySurface(float3 positionWS, inout half3 albedo, inout half3 emission, inout half smoothness)
{
    albedo *= 1.0 - saturate(_StageDarken);

    int heatCount = (int)_StageHeatCount;
    if (heatCount > 0)
    {
        float glow = 0.0;
        float scorch = 0.0;

        [loop]
        for (int i = 0; i < STAGE_MAX_HEAT; i++)
        {
            if (i >= heatCount)
            {
                break;
            }

            float4 p = _StageHeatPoints[i];
            float4 v = _StageHeatValues[i];
            float r = max(p.w, 1e-3);
            float d = distance(positionWS, p.xyz);
            float fall = saturate(1.0 - d / r);
            fall = fall * fall * (3.0 - 2.0 * fall);
            glow = max(glow, v.x * fall);
            scorch = max(scorch, v.y * saturate(1.0 - d / (r * 1.3)));
        }

        float grain = StageNoise(positionWS * 9.0);
        scorch = saturate(scorch * (0.55 + grain * 0.9));
        albedo = lerp(albedo, _StageScorchColor.rgb, scorch * 0.85);
        smoothness *= 1.0 - scorch * 0.7;

        half3 heatColor = lerp(_StageHeatGlowColor.rgb, _StageHeatHotColor.rgb, saturate(glow * glow));
        emission += heatColor * glow * (0.6 + 0.4 * grain);
    }

    emission += _StageFlashColor.rgb * saturate(_StageFlash);

    if (_StageDissolve > 0.0)
    {
        float n = StageNoise(positionWS * 3.0);
        float edge = 1.0 - saturate((n - _StageDissolve) / 0.08);
        emission += _StageDissolveColor.rgb * edge;
    }
}

#endif
