// 撃破の破裂（KillBurstLook が毎フレーム作る四角形の集まり）。形は画像を使わず、四角形の中で計算して切り抜く（ふちはくっきり＝アニメのベタ塗り）。
// uv0 = 四角形の中の位置(-1〜1)、uv1.x = 形（0=白い星, 1=しずく, 2=輪）、uv1.y = 形ごとの値（しずく: つやを出すか / 輪: 太さ）。
// 色は HDR（1より明るいと、後の光のにじみでぼんやり光る）。光の影響・影・深度は無し。
Shader "Hidden/MS2026/Fortress/KillBurst"
{
    SubShader
    {
        Tags { "Queue" = "Transparent+60" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "KillBurst"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.color = input.color;
                output.uv0 = input.uv0;
                output.uv1 = input.uv1;
                return output;
            }

            // 距離 d（0より小さいと内側）を、画素1つ分だけなめらかにして 0〜1 の濃さにする。
            float Fill(float d)
            {
                float w = max(fwidth(d), 1e-4);
                return 1.0 - smoothstep(-w, w, d);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv0;
                float kind = input.uv1.x;
                float param = input.uv1.y;
                float3 rgb = input.color.rgb;
                float alpha;

                if (kind < 0.5)
                {
                    // 白い星（4つのとげ）。
                    float f = sqrt(abs(p.x)) + sqrt(abs(p.y));
                    alpha = Fill(f - 1.0);
                }
                else if (kind < 1.5)
                {
                    // しずく: 後ろの丸い所から、進む向き(+x)の先へ細くとがる。
                    float2 tail = float2(-0.5, 0.0);
                    float radius = 0.48;
                    float dTail = length(p - tail) - radius;
                    float x01 = saturate((p.x - tail.x) / (1.0 - tail.x));
                    float halfWidth = radius * pow(1.0 - x01, 0.9);
                    float dCone = (p.x >= tail.x && p.x <= 1.0) ? abs(p.y) - halfWidth : 1.0;
                    alpha = Fill(min(dTail, dCone));
                    // 前半だけ、丸い所に白いつや（アニメの塗りのハイライト）。
                    float shine = param * Fill(length(p - float2(-0.62, 0.17)) - 0.15);
                    rgb = lerp(rgb, max(rgb, 1.6), shine);
                }
                else
                {
                    // 輪。
                    float r = length(p);
                    alpha = Fill(abs(r - 0.85) - param);
                }

                return float4(rgb, alpha * input.color.a);
            }
            ENDHLSL
        }
    }
}
