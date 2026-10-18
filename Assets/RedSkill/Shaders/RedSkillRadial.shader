// 赤スキルの「光の板」。四角い板1枚に、芯の光・トゲ・輪をコードの数値だけで描く（画像を使わない）。
// 溜めの光・発射口の光・当たった場所の光・壁に沿って広がる光・衝撃の輪に使う。数値は RedLaserVisuals がパーツごとに渡す。
// _ShapeA: x=芯の大きさ(0〜1), y=芯のしぼり, z=トゲの本数, w=トゲの強さ
// _ShapeB: x=トゲの細さ, y=回転(ラジアン), z=輪の半径(0〜1), w=輪の太さ
// _ShapeC: x=輪の強さ, y=（未使用）, z=トゲの長さの乱数の種, w=トゲの長さのばらつき
Shader "MS2026/RedSkill/Radial"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (2, 0.3, 0.2, 1)
        _ShapeA ("Core Size, Core Power, Ray Count, Ray Strength", Vector) = (0.4, 2, 8, 1)
        _ShapeB ("Ray Sharpness, Rotation, Ring Radius, Ring Width", Vector) = (6, 0, 0, 0)
        _ShapeC ("Ring Strength, (unused), Ray Seed, Ray Jitter", Vector) = (0, 0, 0, 0.5)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RedSkillRadial"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RedSkillCommon.hlsl"

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

            // 線分(角度方向の帯)の光: d = 線からの距離。
            float Line(float d, float w)
            {
                return exp(-(d * d) / max(w * w, 1e-6));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                float rot = _ShapeB.y;
                float angle = atan2(p.y, p.x);

                // 芯の光
                float core = pow(saturate(1.0 - r / max(_ShapeA.x, 1e-3)), max(_ShapeA.y, 0.01));

                // トゲ（本数・長さのばらつき・細さ）
                float rays = 0.0;
                if (_ShapeA.z >= 1.0)
                {
                    float sector = (angle + rot) * _ShapeA.z / 6.2831853;
                    float id = floor(sector);
                    float f = frac(sector) - 0.5;
                    float len = lerp(1.0, 0.35 + 0.65 * RedSkillHash(id * 7.31 + _ShapeC.z), _ShapeC.w);
                    float ray = pow(saturate(1.0 - abs(f) * 2.0), max(_ShapeB.x, 0.5));
                    rays = ray * saturate(1.0 - r / max(len, 0.05)) * _ShapeA.w;
                }

                // 輪
                float ring = _ShapeC.x * Line(r - _ShapeB.z, max(_ShapeB.w, 0.002));

                float value = (core + rays + ring) * (1.0 - smoothstep(0.92, 1.0, r));
                return half4(_Color.rgb * value * _Color.a, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
