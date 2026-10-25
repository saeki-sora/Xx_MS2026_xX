// 青スキルのしぶき・泡・きらめき。パーティクルの四角を光として足し算で描く。色はパーティクルの色 × _Intensity（明るさ）。
// _Mode = 0: 真ん中が明るい丸（伸ばすと細長いしぶき）
// _Mode = 1: 泡（ふちが光る輪＋左上の小さなハイライト）
Shader "MS2026/BlueSkill/Particle"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 2.4
        _Softness ("Softness", Float) = 1.6
        _Mode ("Mode (0 = droplet, 1 = bubble)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "BlueSkillParticle"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
                float _Mode;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = saturate(length(p));
                float value;
                float core;
                if (_Mode > 0.5)
                {
                    float rim = exp(-pow((r - 0.78) / 0.14, 2.0));
                    float fill = 0.12 * (1.0 - r);
                    float2 h = p - float2(-0.35, 0.38);
                    core = exp(-dot(h, h) / 0.025);
                    value = (rim + fill) * (1.0 - smoothstep(0.92, 1.0, r));
                }
                else
                {
                    value = pow(1.0 - r, max(_Softness, 0.1));
                    core = pow(1.0 - r, 6.0);
                }

                float alpha = input.color.a;
                float3 color = (input.color.rgb * value + core) * _Intensity * alpha;
                return half4(color, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
