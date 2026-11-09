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

// 影のふち（極太レーザーの帯のメッシュ用、2026-11-09）。
// cut: x = 帯の中の横の位置(0 左〜1 右), y = 左どなりの帯の長さ(m、無ければ -1), z = 右どなりの帯の長さ, w = 帯の幅(見た目の太さに対する割合)。
// となりの帯が dist より手前で終わっている側（＝その先は障害物の後ろ）を、帯の中だけで softness(m) ぶんぼかす。
// fade = 濃さに掛ける値(0〜1)、edge = ぼかしの真ん中で一番大きくなる値（明るいふち用、0〜1）。
void RedSkillCutFade(float4 cut, float dist, float width, float softness, out float fade, out float edge)
{
    fade = 1.0;
    edge = 0.0;
    if (softness <= 0.0005)
    {
        return;
    }

    float stripMeters = max(cut.w * width, 0.001);
    float soft = min(softness, stripMeters);
    float leftCut = cut.y >= 0.0 ? saturate((dist - cut.y) / 0.25) : 0.0;
    float rightCut = cut.z >= 0.0 ? saturate((dist - cut.z) / 0.25) : 0.0;
    float fromLeft = saturate(cut.x) * stripMeters;
    float fromRight = (1.0 - saturate(cut.x)) * stripMeters;
    float fadeLeft = lerp(1.0, smoothstep(0.0, soft, fromLeft), leftCut);
    float fadeRight = lerp(1.0, smoothstep(0.0, soft, fromRight), rightCut);
    fade = fadeLeft * fadeRight;
    edge = saturate(fadeLeft * (1.0 - fadeLeft) * 4.0 * leftCut + fadeRight * (1.0 - fadeRight) * 4.0 * rightCut);
}

#endif
