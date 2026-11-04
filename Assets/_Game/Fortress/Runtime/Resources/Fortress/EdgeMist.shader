// 画面のふちの黒い靄（EdgeMistLook / EdgeMistPass が、透明な物まで描いた後・光のにじみ等の後処理の前にかける）。
// 靄の中だけ「夜の見え方」にする: 物の色を暗く青く沈め、光っている物（HDR の光・ビーム）はそのまま残し、
// ビームの近くは靄がビームの色でほんのり光って、元の床の色が少し見える（光だまり）。靄の内側のふちには紫がうっすら乗る。
Shader "Hidden/MS2026/Fortress/EdgeMist"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "EdgeMist"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Assets/_Game/Fortress/Runtime/Laser/Light/Shaders/FortressBeamLight.hlsl"
            #include "Assets/_Game/Fortress/Runtime/Look/EdgeMist/Shaders/FortressEdgeMist.hlsl"

            float4 _MistColor;
            float4 _ShadeTint;      // rgb=靄の中の物に掛ける色, a=暗さ
            float4 _BandColor;      // rgb=内側のふちの色, a=濃さ
            float4 _Keep;           // x=光っている物とみなす明るさ, y=その幅, z=残す量
            float4 _BeamGlow;       // x=靄が光る明るさ, y=範囲の倍率, z=元の色を見せる量
            float4x4 _EdgeMistInvViewProj;

            // 画面の位置から、地面（z=0 の面）のどこが映っているか。
            float2 EdgeMistFloorPoint(float2 uv)
            {
                float2 ndc = uv * 2.0 - 1.0;
                float4 nearPoint = mul(_EdgeMistInvViewProj, float4(ndc, -1.0, 1.0));
                float4 farPoint = mul(_EdgeMistInvViewProj, float4(ndc, 1.0, 1.0));
                nearPoint.xyz /= nearPoint.w;
                farPoint.xyz /= farPoint.w;
                float denom = farPoint.z - nearPoint.z;
                float t = abs(denom) > 1e-5 ? saturate(-nearPoint.z / denom) : 0.0;
                return lerp(nearPoint.xyz, farPoint.xyz, t).xy;
            }

            // ビームが靄を照らす色（ビームの線からの距離で弱くなる）。ビームが出ていない枠は0。
            half3 EdgeMistBeamGlow(float2 position)
            {
                half3 sum = 0;
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float4 light = _FortressBeamFloorColors[i];
                    if (light.r + light.g + light.b <= 0.0)
                    {
                        continue;
                    }

                    float4 segment = _FortressBeamLightSegments[i];
                    float2 a = segment.xy;
                    float2 ab = segment.zw - a;
                    float t = saturate(dot(position - a, ab) / max(dot(ab, ab), 1e-5));
                    float d = length(position - (a + ab * t));
                    float falloff = saturate(1.0 - d / max(light.a * _BeamGlow.y, 1e-3));
                    sum += light.rgb * (falloff * falloff);
                }

                return sum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                half mist = FortressEdgeMistAmount(uv);
                if (mist <= 0.001h)
                {
                    return color;
                }

                const half3 luma = half3(0.2126h, 0.7152h, 0.0722h);
                half keep = smoothstep(_Keep.x, _Keep.x + _Keep.y, dot(color.rgb, luma)) * _Keep.z;

                // 夜の見え方: 暗く青く沈めて、靄の色を足す。
                half3 night = color.rgb * (1.0h - _ShadeTint.a) * _ShadeTint.rgb + _MistColor.rgb;

                // ビームの光だまり: 靄がビームの色で光り、光の当たる所だけ元の色が見える。
                half3 glow = EdgeMistBeamGlow(EdgeMistFloorPoint(uv));
                half glowAmount = saturate(dot(glow, luma));
                night = lerp(night, color.rgb, glowAmount * _BeamGlow.z) + glow * (_BeamGlow.x * 0.25h);

                half3 result = lerp(color.rgb, night, mist * (1.0h - keep));
                half band = mist * (1.0h - mist) * 4.0h;
                result += _BandColor.rgb * (band * _BandColor.a * (1.0h - keep));
                return half4(result, color.a);
            }
            ENDHLSL
        }
    }
}
