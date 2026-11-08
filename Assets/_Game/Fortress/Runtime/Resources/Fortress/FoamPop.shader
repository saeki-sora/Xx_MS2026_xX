// 敵を倒したときの「光る泡」（FoamPopLook が毎フレーム作る四角形の集まり）。形は画像を使わず、四角形の中で計算して切り抜く。
// 頂点は4つとも泡の中心にあり、ここでカメラの向きに合わせて四角形に広げる（どのカメラから見ても丸く見え、「上へ浮く」もカメラの上方向になる）。
// uv0 = 角(-1〜1)、uv1.x = 形（0=包む泡のかたまり, 1=光る泡, 2=光の点）、uv1.y = 形ごとの値（かたまり: 形の乱数 / 泡: ふちの太さ）、
// uv2 = (半分の大きさ m, 上へ浮いた量 m)、uv3 = (光の色 rgb, 光の強さ)、頂点色 = 倒した人の色（a = 濃さ）。
// ふちは外側が倒した人の色、内側がミント（_PlayerMix = 外側の帯の割合）。光の影響・影・深度は無し。
Shader "Hidden/MS2026/Fortress/FoamPop"
{
    Properties
    {
        _BubbleFill ("Bubble Fill", Range(0, 1)) = 0.5
        _MintColor ("Mint", Color) = (0.23, 0.76, 0.69, 1)
        _PlayerMix ("Player Color Share Of Rim", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Transparent+60" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "FoamPop"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _BubbleFill;
                float4 _MintColor;
                float _PlayerMix;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float2 uv2 : TEXCOORD2;
                float4 uv3 : TEXCOORD3;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
                float4 glow : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                // ビュー行列の1行目・2行目 = カメラの右・上の向き（ワールド）。
                float3 right = UNITY_MATRIX_V[0].xyz;
                float3 up = UNITY_MATRIX_V[1].xyz;
                float3 centre = TransformObjectToWorld(input.positionOS) + up * input.uv2.y;
                float3 world = centre + (right * input.uv0.x + up * input.uv0.y) * input.uv2.x;
                output.positionCS = TransformWorldToHClip(world);
                output.color = input.color;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                output.glow = input.uv3;
                return output;
            }

            // 距離 d（0より小さいと内側）を、画素1つ分だけなめらかにして 0〜1 の濃さにする。
            float Fill(float d)
            {
                float w = max(fwidth(d), 1e-4);
                return 1.0 - smoothstep(-w, w, d);
            }

            // 色を重ねる（src を dst の上に）。a は濃さ。
            float4 Over(float4 src, float4 dst)
            {
                float a = src.a + dst.a * (1.0 - src.a);
                float3 rgb = a > 1e-4 ? (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / a : 0;
                return float4(rgb, a);
            }

            static const float3 FoamWhite = float3(0.93, 1.0, 0.98);

            // ふちの中の位置 u（0=内側, 1=外側）から色を決める。外側の _PlayerMix ぶんが倒した人の色、残りがミント。
            float3 RimColor(float u, float3 player)
            {
                float edge = max(fwidth(u), 1e-3);
                float outer = smoothstep(1.0 - _PlayerMix - edge, 1.0 - _PlayerMix + edge, u);
                return lerp(_MintColor.rgb, player, outer);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv0;
                float kind = input.uv1.x;
                float param = input.uv1.y;
                float3 rim = input.color.rgb;
                float4 result = 0;

                if (kind < 0.5)
                {
                    // 包む泡のかたまり: 丸を5つ重ねた雲の形。白い塗り＋ふちの色の線＋つや。
                    float d = length(p) - 0.6;
                    [unroll]
                    for (int i = 0; i < 4; i++)
                    {
                        float a = param + i * 1.5708;
                        float2 c = float2(cos(a), sin(a)) * 0.42;
                        float r = 0.36 + 0.06 * sin(param * 3.1 + i * 2.3);
                        d = min(d, length(p - c) - r);
                    }

                    result = float4(lerp(FoamWhite, _MintColor.rgb, 0.12), Fill(d));
                    float edge = Fill(abs(d + 0.06) - 0.06);
                    result = Over(float4(RimColor(saturate((d + 0.12) / 0.12), rim), edge), result);
                    float shine = Fill(length(p - float2(-0.25, 0.28)) - 0.16);
                    result = Over(float4(1, 1, 1, shine * result.a), result);
                }
                else if (kind < 1.5)
                {
                    // 光る泡: 四角形は泡の 1.25 倍。外側にほんのり光、中は白っぽい塗り、ふちの色の線、左上に白いつや。
                    float r = length(p) * 1.25;
                    float glow = input.glow.a * 0.3 * smoothstep(0.55, 0.97, r) * (1.0 - smoothstep(1.0, 1.24, r));
                    result = float4(input.glow.rgb, saturate(glow));
                    float inside = Fill(r - 1.0);
                    result = Over(float4(FoamWhite, inside * _BubbleFill), result);
                    float w = param;
                    float edge = Fill(abs(r - (1.0 - w * 0.5)) - w * 0.5);
                    result = Over(float4(RimColor(saturate((r - (1.0 - w)) / w), rim), edge), result);
                    float shine = Fill(length(p * 1.25 - float2(-0.38, 0.38)) - 0.26);
                    result = Over(float4(1, 1, 1, shine), result);
                }
                else
                {
                    // 光の点: 光の色の丸＋白い芯。
                    float r = length(p);
                    result = float4(input.glow.rgb, Fill(r - 1.0));
                    result = Over(float4(1, 1, 1, 0.9 * Fill(r - 0.45)), result);
                }

                result.a *= input.color.a;
                return result;
            }
            ENDHLSL
        }
    }
}
