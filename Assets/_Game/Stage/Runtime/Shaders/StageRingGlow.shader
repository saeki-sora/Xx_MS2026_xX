// 丸いリング（ステージの枠など）の輪郭から、外と内へ柔らかく広がる光（StageRingGlow が使う）。
// ・メッシュは C# が作るドーナツ形の帯。UV.x = 輪郭からの距離（0=輪郭, 1=光の端。0 より小さい所はリングの下に隠れる重なり）、
//   UV.y = 0 外側の光 / 1 内側の光。
// ・加算で描くので、暗い背景の上で「光っている」ように見える。ライトは当てない。
Shader "MS2026/Stage/RingGlow"
{
    Properties
    {
        [HDR] _GlowColor("光の色", Color) = (0.45, 0.5, 1, 1)
        _GlowIntensity("強さ", Float) = 1.2
        _OuterStrength("外側の強さ", Float) = 1
        _InnerStrength("内側の強さ", Float) = 0.35
        _Softness("やわらかさ（大きいほど早く消える）", Float) = 2.2
        _PulseAmount("脈打つ量", Float) = 0
        _PulsePeriod("脈打つ間隔（秒）", Float) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            // 半透明の床（floor1 は Transparent-50）より後に描いて、内側の光が床に隠れないようにする。
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "RingGlow"
            Tags { "LightMode" = "UniversalForwardOnly" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                float _GlowIntensity;
                float _OuterStrength;
                float _InnerStrength;
                float _Softness;
                float _PulseAmount;
                float _PulsePeriod;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = saturate(input.uv.x);
                float falloff = pow(1.0 - t, max(_Softness, 0.01));
                float side = lerp(_OuterStrength, _InnerStrength, saturate(input.uv.y));
                float pulse = 1.0 + _PulseAmount * sin(_Time.y * TWO_PI / max(_PulsePeriod, 0.05));
                half3 rgb = _GlowColor.rgb * (_GlowIntensity * side * pulse * falloff);
                return half4(rgb, 0);
            }
            ENDHLSL
        }
    }
}
