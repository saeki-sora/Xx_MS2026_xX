// 倒したときの手描き風エフェクト（パラパラ漫画の絵を並べた1枚の画像）を描くパーティクル用シェーダー。
// コマの切り替えは ParticleSystem の Texture Sheet Animation がUVを動かして行う。
// 絵が無いPC（素材を読み込んでいないPC）では透明になるよう、絵の初期値を "black"（透明）にしてある。
Shader "MS2026/3DObject/AnimeFx"
{
    Properties
    {
        _MainTex("絵（コマを並べた画像）", 2D) = "black" {}
        [HDR] _TintColor("色をかける", Color) = (1,1,1,1)
        _Brightness("明るさ", Range(0, 4)) = 1.2
        _AlphaCut("ふちをくっきりさせる位置", Range(0, 1)) = 0.35
        _AlphaSoft("ふちのぼかし", Range(0, 0.5)) = 0.15
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend("重ね方（元）", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend("重ね方（先）（One=光らせる / OneMinusSrcAlpha=ふつう）", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+60"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Blend [_SrcBlend] [_DstBlend]
        ZWrite Off
        Cull Off

        Pass
        {
            Name "AnimeFx"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4  _TintColor;
                half   _Brightness;
                half   _AlphaCut;
                half   _AlphaSoft;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4  color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color * _TintColor;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // 手描きらしく、半透明のふちを少しくっきりさせる（_AlphaSoft を大きくすると元の絵に近い）。
                half soft = max(_AlphaSoft, 1e-3h);
                half alpha = smoothstep(_AlphaCut - soft, _AlphaCut + soft, texel.a) * input.color.a;
                half3 color = texel.rgb * input.color.rgb * _Brightness;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
