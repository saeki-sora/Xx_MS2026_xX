// 赤スキルの火花・光の粒。パーティクルの四角を、真ん中が明るい丸（伸ばすと細長い火花）として光を足して描く。
// 色はパーティクルの色 × _Intensity（明るさ）。
// _LineMode = 1 のときは線（LineRenderer）用: 線の横方向（UV の y）だけで、真ん中が明るい細い光にする（稲妻に使う）。
Shader "MS2026/RedSkill/Particle"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 2.5
        _Softness ("Softness", Float) = 1.6
        _LineMode ("Line Mode (0 = dot, 1 = line)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "RedSkillParticle"
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
                float _LineMode;
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
                float r = _LineMode > 0.5 ? saturate(abs(p.y)) : saturate(length(p));
                float glow = pow(1.0 - r, max(_Softness, 0.1));
                float core = pow(1.0 - r, 6.0);
                float alpha = input.color.a * glow;
                float3 color = (input.color.rgb + core) * _Intensity * alpha;
                return half4(color, 0.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
