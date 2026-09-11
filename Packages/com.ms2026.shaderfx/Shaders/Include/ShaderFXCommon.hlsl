#ifndef SHADERFX_COMMON_INCLUDED
#define SHADERFX_COMMON_INCLUDED

float ShaderFXHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Value noise (no texture dependency, so the Dissolve module never needs an
// external noise texture asset to be assigned).
float ShaderFXNoise(float2 uv)
{
    float2 i = floor(uv);
    float2 f = frac(uv);

    float a = ShaderFXHash(i);
    float b = ShaderFXHash(i + float2(1.0, 0.0));
    float c = ShaderFXHash(i + float2(0.0, 1.0));
    float d = ShaderFXHash(i + float2(1.0, 1.0));

    float2 u = f * f * (3.0 - 2.0 * f);
    return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
}

#endif
