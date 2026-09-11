Shader "Hidden/ShaderFX/Screen"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off
        ZTest Always

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D_X(_BlitTexture);

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }
        ENDHLSL

        Pass
        {
            Name "Grayscale"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float _Intensity;

            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);
                half luma = dot(col.rgb, half3(0.299, 0.587, 0.114));
                col.rgb = lerp(col.rgb, luma.xxx, saturate(_Intensity));
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Posterize"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float _Levels;

            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);
                float levels = max(_Levels, 2.0);
                col.rgb = floor(col.rgb * levels) / (levels - 1.0);
                return col;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Pixelate"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D_X(_MaskTexture);

            float2 _BlockCount;
            float _UseMask;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 blockCount = max(_BlockCount, float2(1.0, 1.0));
                float2 snappedUV = (floor(input.uv * blockCount) + 0.5) / blockCount;
                half4 pixelated = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, snappedUV);

                if (_UseMask > 0.5)
                {
                    half mask = SAMPLE_TEXTURE2D_X(_MaskTexture, sampler_LinearClamp, input.uv).r;
                    half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);
                    return lerp(original, pixelated, mask);
                }

                return pixelated;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            float4 _OutlineColor;
            float _Thickness;
            float _DepthThreshold;
            float _NormalThreshold;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 texel = (1.0 / _ScreenParams.xy) * max(_Thickness, 0.5);

                float depthC = SampleSceneDepth(input.uv);
                float depthL = SampleSceneDepth(input.uv - float2(texel.x, 0));
                float depthR = SampleSceneDepth(input.uv + float2(texel.x, 0));
                float depthU = SampleSceneDepth(input.uv + float2(0, texel.y));
                float depthD = SampleSceneDepth(input.uv - float2(0, texel.y));
                float depthDiff = abs(depthL - depthC) + abs(depthR - depthC) + abs(depthU - depthC) + abs(depthD - depthC);
                float edgeDepth = step(_DepthThreshold, depthDiff);

                float3 normalC = SampleSceneNormals(input.uv);
                float3 normalL = SampleSceneNormals(input.uv - float2(texel.x, 0));
                float3 normalR = SampleSceneNormals(input.uv + float2(texel.x, 0));
                float3 normalU = SampleSceneNormals(input.uv + float2(0, texel.y));
                float3 normalD = SampleSceneNormals(input.uv - float2(0, texel.y));
                float normalDiff = distance(normalL, normalC) + distance(normalR, normalC) + distance(normalU, normalC) + distance(normalD, normalC);
                float edgeNormal = step(_NormalThreshold, normalDiff);

                float edge = saturate(edgeDepth + edgeNormal);

                half4 original = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);
                return lerp(original, _OutlineColor, edge * _OutlineColor.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Mask"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex MaskVert
            #pragma fragment MaskFrag

            struct MaskAttributes
            {
                float4 positionOS : POSITION;
            };

            struct MaskVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            MaskVaryings MaskVert(MaskAttributes input)
            {
                MaskVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 MaskFrag(MaskVaryings input) : SV_Target
            {
                return half4(1, 1, 1, 1);
            }
            ENDHLSL
        }

        Pass
        {
            // Used only to upscale the screen-effect chain's working resolution back to the
            // camera's actual target resolution when ShaderFXQuality.ScreenEffectResolutionScale
            // downsamples the effects (design doc §5「品質スケーリング」) — bilinear via
            // sampler_LinearClamp, same as every other pass here.
            Name "Copy"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            half4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Shockwave"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float2 _ShockwaveCenter;
            float _ShockwaveProgress;
            float _ShockwaveStrength;
            float _ShockwaveWidth;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 toCenter = input.uv - _ShockwaveCenter;
                float dist = length(toCenter);
                float ring = 1.0 - saturate(abs(dist - _ShockwaveProgress) / max(_ShockwaveWidth, 0.001));
                float2 dir = dist > 0.0001 ? toCenter / dist : float2(0.0, 0.0);
                float2 distortedUV = input.uv + dir * ring * _ShockwaveStrength;
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, distortedUV);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ScreenFlash"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 _ScreenFlashColor;
            float _ScreenFlashAmount;

            half4 Frag(Varyings input) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.uv);
                col.rgb = lerp(col.rgb, _ScreenFlashColor.rgb, saturate(_ScreenFlashAmount));
                return col;
            }
            ENDHLSL
        }
    }
}
