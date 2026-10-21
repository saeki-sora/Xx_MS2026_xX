// 緑スキルの「コアへ送るジェル」。歯磨きジェルのような半透明の緑の帯を、光を足さずに普通に重ねて描く。
// 帯の中: 芯は明るいミント → ふちは緑 → 一番外は濃い輪郭（トゥーン風）。上側につやの線、中を泡が流れていく、うっすら流れる縞。
// メッシュの UV0: x = 根元からの距離(m), y = 横の位置 0〜1, z = その場所の太さ(m), w = 全体の長さ(m)。値は GreenGelLink が作る。
// _Flow: x = 流れた距離(m、時間とともに増える), y = 濃さ(0〜1), z = 泡の多さ, w = 握っている強さ(0〜1、光が強くなる)
Shader "MS2026/GreenSkill/Gel"
{
    Properties
    {
        _CoreColor ("Core Color", Color) = (0.6, 1, 0.8, 1)
        _EdgeColor ("Edge Color", Color) = (0.2, 0.8, 0.5, 1)
        _RimColor ("Rim Color", Color) = (0.03, 0.2, 0.13, 1)
        [HDR] _GlowColor ("Glow Color", Color) = (0.55, 1.6, 0.95, 1)
        _Flow ("Flow, Opacity, Bubbles, Boost", Vector) = (0, 0.9, 1, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "GreenSkillGel"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _EdgeColor;
                half4 _RimColor;
                half4 _GlowColor;
                float4 _Flow;
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

            float GelHash(float n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            // 泡1つ: 白い輪＋うすい中身。dist はメートル。
            float Bubble(float2 delta, float radius)
            {
                float d = length(delta);
                float ring = saturate(1.0 - abs(d - radius) / max(radius * 0.28, 0.008));
                float fill = saturate(1.0 - d / max(radius, 1e-3)) * 0.25;
                return max(ring, fill);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float dist = input.uv.x;
                float v = input.uv.y;
                float width = max(input.uv.z, 1e-3);
                float total = max(input.uv.w, 1e-3);
                float across = abs(v * 2.0 - 1.0);
                float flow = _Flow.x;
                float boost = _Flow.w;

                // 芯 → ふち の色。
                float3 color = lerp(_CoreColor.rgb, _EdgeColor.rgb, smoothstep(0.2, 0.85, across));

                // 流れる縞（うっすら）。
                float stripe = sin((dist - flow) * 6.2831853 / 1.3);
                color *= 1.0 + stripe * 0.06;

                // 泡: 0.55 m ごとに1つ、横の位置と大きさはばらばら。前後のマスも見て、はみ出した泡も描く。
                float bubbles = 0.0;
                float cellSize = 0.55 / max(_Flow.z, 0.05);
                float local = (dist - flow) / cellSize;
                float cell = floor(local);
                [unroll]
                for (int k = -1; k <= 1; k++)
                {
                    float id = cell + k;
                    float h1 = GelHash(id * 1.37 + 3.1);
                    float h2 = GelHash(id * 2.11 + 7.7);
                    float h3 = GelHash(id * 0.73 + 1.9);
                    float cx = (id + 0.2 + 0.6 * h1) * cellSize;
                    float cy = (0.22 + 0.56 * h2 - 0.5) * width;
                    float radius = lerp(0.05, 0.14, h3) * min(width / 0.6, 1.6);
                    float2 delta = float2((dist - flow) - cx, (v - 0.5) * width - cy);
                    bubbles = max(bubbles, Bubble(delta, radius) * step(0.25, h3 + 0.2));
                }
                color = lerp(color, float3(1.0, 1.0, 1.0), bubbles * 0.75);

                // つやの線（上側）。
                float shine = exp(-pow((v - 0.3) / 0.07, 2.0)) * (0.55 + 0.45 * sin(dist * 0.9 - flow * 0.5));
                color = lerp(color, float3(1.0, 1.0, 1.0), saturate(shine) * 0.7);

                // 握っているほど芯が光る。
                color += _GlowColor.rgb * boost * 0.35 * (1.0 - across);

                // 一番外の輪郭（トゥーン風の濃い線）。
                float rim = smoothstep(0.8, 0.9, across);
                color = lerp(color, _RimColor.rgb, rim);

                float alpha = lerp(0.72, 1.0, max(rim, bubbles)) * _Flow.y;
                // 帯のふちは1ピクセルぶんぼかす。
                alpha *= saturate((1.0 - across) * width * 40.0);
                // 根元と先は少しだけやわらかく消す。
                alpha *= saturate(dist / 0.12) * saturate((total - dist) / 0.12);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
