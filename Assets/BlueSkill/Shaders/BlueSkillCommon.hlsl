// 青スキルのシェーダー共通の小道具（乱数・ノイズ・回転）。
#ifndef MS2026_BLUESKILL_COMMON_INCLUDED
#define MS2026_BLUESKILL_COMMON_INCLUDED

float BlueSkillHash(float n)
{
    return frac(sin(n * 12.9898) * 43758.5453);
}

float BlueSkillHash2(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

// 0〜1 のなめらかなノイズ。
float BlueSkillValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 s = f * f * (3.0 - 2.0 * f);
    float a = BlueSkillHash2(i);
    float b = BlueSkillHash2(i + float2(1.0, 0.0));
    float c = BlueSkillHash2(i + float2(0.0, 1.0));
    float d = BlueSkillHash2(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, s.x), lerp(c, d, s.x), s.y);
}

// ノイズを3段重ねた、水のゆらぎっぽいノイズ（0〜1）。
float BlueSkillFbm(float2 p)
{
    float v = 0.0;
    v += 0.5 * BlueSkillValueNoise(p);
    v += 0.3 * BlueSkillValueNoise(p * 2.03 + 17.1);
    v += 0.2 * BlueSkillValueNoise(p * 4.11 + 41.7);
    return v;
}

float2 BlueSkillRotate(float2 p, float angle)
{
    float s = sin(angle);
    float c = cos(angle);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

#endif
