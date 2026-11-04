// 画面に斜めに差し込む光の筋（LightRaysLook / LightRaysPass が、光のにじみ等の後処理の前にかける）。霧は使わない。
// 差し込む向きに沿って伸びる、太さ・明るさがまちまちな筋を、ゆっくり横に流し、少しゆらめかせる。
// 差し込む側（画面の上の辺）から遠いほど薄くなる。スクリーン合成なので、明るい所ほど足す量が減り、画面全体は明るくなりすぎない。
Shader "Hidden/MS2026/Fortress/LightRays"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "LightRays"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _RayColor;  // rgb=色, a=明るさ
            float4 _RayShape;  // x=傾き(ラジアン), y=本数の多さ, z=くっきりさ, w=届く長さ
            float4 _RayMotion; // x=流れる速さ, y=ゆらめき, z=画面の横÷縦

            float LightRaysHash(float x)
            {
                return frac(sin(x * 127.1) * 43758.5453);
            }

            // 1次元のなめらかなでこぼこ（0〜1）。
            float LightRaysNoise(float x)
            {
                float i = floor(x);
                float f = frac(x);
                float u = f * f * (3.0 - 2.0 * f);
                return lerp(LightRaysHash(i), LightRaysHash(i + 1.0), u);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

                float aspect = _RayMotion.z;
                float2 p = float2(uv.x * aspect, uv.y);
                float angle = _RayShape.x;
                float2 dir = float2(sin(angle), -cos(angle)); // 光が進む向き（上から下へ、傾き分だけ横へ）
                float2 across = float2(-dir.y, dir.x);         // 筋を横切る向き

                // 差し込む側: 傾きの向きと反対側の上の角を起点にする（左上から差すなら左上）。
                float2 origin = float2(angle >= 0.0 ? 0.0 : aspect, 1.0);
                float along = dot(p - origin, dir);
                float side = dot(p, across);

                float t = _Time.y * _RayMotion.x;
                float density = _RayShape.y;
                float band = LightRaysNoise(side * density + t) * 0.6 + LightRaysNoise(side * density * 2.37 - t * 0.7 + 13.1) * 0.4;
                band = pow(saturate(band), _RayShape.z);

                float fade = smoothstep(0.0, 1.0, saturate(1.0 - along / max(_RayShape.w, 1e-3)));
                float shimmer = 1.0 - _RayMotion.y * (0.5 + 0.5 * sin(_Time.y * 1.7 + side * 4.0));

                half amount = (half)(band * fade * shimmer * _RayColor.a);
                half3 light = _RayColor.rgb * amount;
                // スクリーン合成: 明るい所ほど足す量が減る。
                color.rgb = color.rgb + light * saturate(1.0h - color.rgb);
                return color;
            }
            ENDHLSL
        }
    }
}
