// 青スキルの「光の板」。四角い板1枚に、芯の光・トゲ（水の王冠）・輪（点線にもできる）をコードの数値だけで描く（画像を使わない）。
// 溜めの光・破裂の光・衝撃の輪・波紋・落ちる場所の目印に使う。数値は BlueFx.Glow がパーツごとに渡す。足し算で光る。
// _ShapeA: x=芯の大きさ(0〜1), y=芯のしぼり, z=トゲの本数, w=トゲの強さ
// _ShapeB: x=トゲの細さ, y=回転(ラジアン), z=輪の半径(0〜1), w=輪の太さ
// _ShapeC: x=輪の強さ, y=輪の点線の数(0=つながった輪), z=トゲの長さの乱数の種, w=トゲの長さのばらつき
Shader "MS2026/BlueSkill/Glow"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (0.4, 1.2, 2.5, 1)
        _ShapeA ("Core Size, Core Power, Ray Count, Ray Strength", Vector) = (0.4, 2, 0, 0)
        _ShapeB ("Ray Sharpness, Rotation, Ring Radius, Ring Width", Vector) = (6, 0, 0, 0)
        _ShapeC ("Ring Strength, Dash Count, Ray Seed, Ray Jitter", Vector) = (0, 0, 0, 0.5)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "BlueSkillGlow"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BlueSkillCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _ShapeA;
                float4 _ShapeB;
                float4 _ShapeC;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Band(float d, float w)
            {
                return exp(-(d * d) / max(w * w, 1e-6));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float rot = _ShapeB.y;
                float angle = atan2(p.y, p.x);

                float core = pow(saturate(1.0 - r / max(_ShapeA.x, 1e-3)), max(_ShapeA.y, 0.01)) * step(1e-3, _ShapeA.x);

                float rays = 0.0;
                if (_ShapeA.z >= 1.0)
                {
                    float sector = (angle + rot) * _ShapeA.z / 6.2831853;
                    float id = floor(sector);
                    float f = frac(sector) - 0.5;
                    float len = lerp(1.0, 0.35 + 0.65 * BlueSkillHash(id * 7.31 + _ShapeC.z), _ShapeC.w);
                    float ray = pow(saturate(1.0 - abs(f) * 2.0), max(_ShapeB.x, 0.5));
                    rays = ray * saturate(1.0 - r / max(len, 0.05)) * _ShapeA.w;
                }

                float ring = _ShapeC.x * Band(r - _ShapeB.z, max(_ShapeB.w, 0.002));
                if (_ShapeC.y >= 1.0)
                {
                    float dash = frac((angle + rot) / 6.2831853 * _ShapeC.y);
                    ring *= smoothstep(0.0, 0.12, dash) * (1.0 - smoothstep(0.5, 0.62, dash));
                }

                float value = (core + rays + ring) * (1.0 - smoothstep(0.92, 1.0, r));
                return half4(_Color.rgb * value * _Color.a, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
