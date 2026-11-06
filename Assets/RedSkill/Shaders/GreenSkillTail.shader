// 緑スキル: 守護霊のしっぽの煙（守護霊の足元から緑の砲台へつながる帯）。
// メッシュの UV: x = 根元からの距離（m）、y = 横方向 0〜1、z = その場所の半分の太さ（m）、w = 全体の長さ（m）。
// 中はうすく（_FillTop〜_FillBottom、_Fill.a の濃さ）、ふちは濃い輪郭の線。ふちは煙らしく、ゆっくり波打つ。
Shader "MS2026/GreenSkill/Tail"
{
    Properties
    {
        _FillTop ("Fill (center)", Color) = (0.97, 1, 0.88, 0.74)
        _FillBottom ("Fill (edge)", Color) = (0.78, 0.96, 0.64, 0.74)
        _Outline ("Outline Color", Color) = (0.37, 0.88, 0.29, 1)
        // x = 輪郭の太さ（m）, y = 全体の濃さ, z = 根元をぼかす長さ（m）, w = ふちの波の強さ
        _Params ("Outline Width, Opacity, Root Fade, Puff", Vector) = (0.09, 1, 0.6, 0.12)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Tail"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _FillTop;
                half4 _FillBottom;
                half4 _Outline;
                float4 _Params;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float dist = input.uv.x;
                float v = input.uv.y;
                float halfWidth = max(input.uv.z, 1e-3);
                float total = max(input.uv.w, 1e-3);
                float across = abs(v * 2.0 - 1.0);
                float t = _Time.y;

                // ふちを煙らしく波打たせる（2つの波を重ねる）。
                float puff = 0.5 + 0.25 * sin(dist * 4.1 - t * 3.3) + 0.25 * sin(dist * 9.7 + t * 2.1);
                float edge = 1.0 - _Params.w * puff;
                float pixel = max(fwidth(across), 1e-4);
                float inside = saturate((edge - across) / pixel);
                if (inside <= 0.0)
                {
                    discard;
                }

                float outlineWidth = saturate(_Params.x / halfWidth);
                float rim = saturate((across - (edge - outlineWidth)) / pixel);

                half4 fill = lerp(_FillTop, _FillBottom, smoothstep(0.0, edge, across));
                half3 color = lerp(fill.rgb, _Outline.rgb, rim);
                half alpha = lerp(fill.a, _Outline.a, rim) * inside * _Params.y;

                // 根元は守護霊の体に溶け込むように、先はやわらかく消す。
                alpha *= saturate(dist / max(_Params.z, 1e-3));
                alpha *= saturate((total - dist) / 0.15);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
