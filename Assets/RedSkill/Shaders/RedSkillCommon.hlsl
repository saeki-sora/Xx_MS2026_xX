// 赤スキルのシェーダー共通の小道具（乱数・ノイズ）。
#ifndef MS2026_REDSKILL_COMMON_INCLUDED
#define MS2026_REDSKILL_COMMON_INCLUDED

float RedSkillHash(float n)
{
    return frac(sin(n * 12.9898) * 43758.5453);
}

float RedSkillHash2(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

// 0〜1 のなめらかなノイズ。
float RedSkillValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 s = f * f * (3.0 - 2.0 * f);
    float a = RedSkillHash2(i);
    float b = RedSkillHash2(i + float2(1.0, 0.0));
    float c = RedSkillHash2(i + float2(0.0, 1.0));
    float d = RedSkillHash2(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, s.x), lerp(c, d, s.x), s.y);
}

#endif
