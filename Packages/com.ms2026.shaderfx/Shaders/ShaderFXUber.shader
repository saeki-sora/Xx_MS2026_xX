Shader "ShaderFX/Uber"
{
    Properties
    {
        [Header(Base)]
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [Space(8)][Header(Rim Light)]
        [Toggle(_FX_RIM)] _FXRimToggle("Enable Rim Light", Float) = 0
        _RimColor("Rim Color", Color) = (0.3, 0.9, 1, 1)
        _RimPower("Rim Power", Range(0.1, 10)) = 3
        _RimIntensity("Rim Intensity", Range(0, 5)) = 1.5

        [Space(8)][Header(Dissolve)]
        [Toggle(_FX_DISSOLVE)] _FXDissolveToggle("Enable Dissolve", Float) = 0
        _DissolveAmount("Dissolve Amount", Range(0, 1)) = 0
        _DissolveEdgeWidth("Dissolve Edge Width", Range(0.001, 0.5)) = 0.08
        _DissolveEdgeColor("Dissolve Edge Color", Color) = (1, 0.55, 0.05, 1)
        _DissolveNoiseScale("Dissolve Noise Scale", Range(1, 64)) = 12

        [Space(8)][Header(Hit Flash)]
        [Toggle(_FX_HITFLASH)] _FXHitFlashToggle("Enable Hit Flash", Float) = 0
        _HitFlashColor("Hit Flash Color", Color) = (1, 1, 1, 1)
        _HitFlashAmount("Hit Flash Amount", Range(0, 1)) = 0

        [Space(8)][Header(Emission Control)]
        [Toggle(_FX_EMISSION)] _FXEmissionToggle("Enable Emission", Float) = 0
        _FXEmissionColor("Emission Color", Color) = (1, 1, 1, 1)
        _FXEmissionIntensity("Emission Intensity", Range(0, 10)) = 1
        _FXEmissionPulseSpeed("Emission Pulse Speed", Range(0, 10)) = 0

        [Space(8)][Header(Fire)]
        [Toggle(_FX_FIRE)] _FXFireToggle("Enable Fire", Float) = 0
        _FireColor("Fire Color", Color) = (1, 0.45, 0.05, 1)
        _FireIntensity("Fire Intensity", Range(0, 10)) = 2
        _FireScrollSpeed("Fire Scroll Speed", Range(0, 10)) = 1.5
        _FireNoiseScale("Fire Noise Scale", Range(1, 64)) = 24

        [Space(8)][Header(Frost)]
        [Toggle(_FX_FROST)] _FXFrostToggle("Enable Frost", Float) = 0
        _FrostColor("Frost Color", Color) = (0.65, 0.85, 1, 1)
        _FrostAmount("Frost Amount", Range(0, 1)) = 0.7
        _FrostSparkleColor("Frost Sparkle Color", Color) = (0.9, 0.98, 1, 1)
        _FrostSparkleThreshold("Frost Sparkle Threshold", Range(0.5, 0.99)) = 0.85

        [Space(8)][Header(Hologram)]
        [Toggle(_FX_HOLOGRAM)] _FXHologramToggle("Enable Hologram", Float) = 0
        _HologramColor("Hologram Color", Color) = (0.2, 0.9, 1, 1)
        _HologramFresnelPower("Hologram Fresnel Power", Range(0.5, 8)) = 2.5
        _HologramScanlineSpeed("Hologram Scanline Speed", Range(0, 40)) = 4
        _HologramScanlineDensity("Hologram Scanline Density", Range(1, 200)) = 60
        _HologramFlickerSpeed("Hologram Flicker Speed", Range(0, 20)) = 6
        _HologramFlickerIntensity("Hologram Flicker Intensity", Range(0, 1)) = 0.15

        [Space(8)][Header(Glitch)]
        [Toggle(_FX_GLITCH)] _FXGlitchToggle("Enable Glitch", Float) = 0
        _GlitchAmount("Glitch Amount", Range(0, 1)) = 0.5
        _GlitchBlockSize("Glitch Block Size", Range(2, 64)) = 16
        _GlitchSpeed("Glitch Speed", Range(0, 40)) = 12
        _GlitchRGBSplit("Glitch RGB Split", Range(0, 0.1)) = 0.02

        [Space(8)][Header(UV Scroll)]
        [Toggle(_FX_UVSCROLL)] _FXUVScrollToggle("Enable UV Scroll", Float) = 0
        _UVScrollColor("UV Scroll Color", Color) = (1, 0.7, 0.1, 1)
        _UVScrollDirection("UV Scroll Direction", Vector) = (0, 1, 0, 0)
        _UVScrollSpeed("UV Scroll Speed", Range(0, 10)) = 1
        _UVScrollPatternScale("UV Scroll Pattern Scale", Range(1, 64)) = 30
        _UVScrollSharpness("UV Scroll Sharpness", Range(0.5, 0.99)) = 0.6

        [Space(8)][Header(Toon Shading)]
        [Toggle(_FX_TOON)] _FXToonToggle("Enable Toon Shading", Float) = 0
        _ToonSteps("Toon Steps", Range(1, 6)) = 2
        _ToonShadeColor("Toon Shade Color", Color) = (0.55, 0.55, 0.65, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        #include "Include/ShaderFXCommon.hlsl"

        // 個体差パラメータ(敵ごとの被弾フラッシュ・溶解進行度など)専用のGPUインスタンシング・バッファ。
        // 通常の CBUFFER_START(UnityPerMaterial) と違い、同じキャッシュMaterialを共有する各Renderer
        // が MaterialPropertyBlock で異なる値を持っていても、Unityが自動的に1回のインスタンス描画へ
        // まとめてくれる(=SRP Batcherの対象からは外れるが、GPU Instancingでバッチされたまま安く描ける)。
        // EffectTarget.SetInstanceDissolveAmount/SetInstanceHitFlashAmount はこのバッファを狙って
        // MaterialPropertyBlock を書き込む。全パスで同じ2プロパティを宣言しているのは、
        // CBUFFER_START(UnityPerMaterial) 側を全パスで同一レイアウトに保つ既存方針との一貫性のため。
        UNITY_INSTANCING_BUFFER_START(ShaderFXPerInstance)
            UNITY_DEFINE_INSTANCED_PROP(float, _DissolveAmount)
            UNITY_DEFINE_INSTANCED_PROP(float, _HitFlashAmount)
        UNITY_INSTANCING_BUFFER_END(ShaderFXPerInstance)
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma shader_feature_local _FX_RIM
            #pragma shader_feature_local _FX_DISSOLVE
            #pragma shader_feature_local _FX_HITFLASH
            #pragma shader_feature_local _FX_EMISSION
            #pragma shader_feature_local _FX_FIRE
            #pragma shader_feature_local _FX_FROST
            #pragma shader_feature_local _FX_HOLOGRAM
            #pragma shader_feature_local _FX_GLITCH
            #pragma shader_feature_local _FX_UVSCROLL
            #pragma shader_feature_local _FX_TOON

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _DissolveEdgeWidth;
                float4 _DissolveEdgeColor;
                float _DissolveNoiseScale;
                float4 _HitFlashColor;
                float4 _FXEmissionColor;
                float _FXEmissionIntensity;
                float _FXEmissionPulseSpeed;
                float4 _FireColor;
                float _FireIntensity;
                float _FireScrollSpeed;
                float _FireNoiseScale;
                float4 _FrostColor;
                float _FrostAmount;
                float4 _FrostSparkleColor;
                float _FrostSparkleThreshold;
                float4 _HologramColor;
                float _HologramFresnelPower;
                float _HologramScanlineSpeed;
                float _HologramScanlineDensity;
                float _HologramFlickerSpeed;
                float _HologramFlickerIntensity;
                float _GlitchAmount;
                float _GlitchBlockSize;
                float _GlitchSpeed;
                float _GlitchRGBSplit;
                float4 _UVScrollColor;
                float4 _UVScrollDirection;
                float _UVScrollSpeed;
                float _UVScrollPatternScale;
                float _UVScrollSharpness;
                float _ToonSteps;
                float4 _ToonShadeColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float fogCoord : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                output.positionWS = vertexInput.positionWS;
                output.positionHCS = vertexInput.positionCS;
                output.normalWS = normalInput.normalWS;
                output.viewDirWS = GetWorldSpaceViewDir(vertexInput.positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogCoord = ComputeFogFactor(vertexInput.positionCS.z);

                return output;
            }

            float ShaderFXDissolveNoise(float3 positionWS)
            {
                return ShaderFXNoise(positionWS.xz * (_DissolveNoiseScale * 0.1) + positionWS.y * 0.05);
            }

            // Two octaves so Fire reads as turbulent flicker rather than a single smooth blob —
            // the noise scrolls "upward" by subtracting time from the sampled Y coordinate.
            float ShaderFXFireNoise(float3 positionWS)
            {
                float2 uv = float2(positionWS.x + positionWS.z, positionWS.y - _Time.y * _FireScrollSpeed) * (_FireNoiseScale * 0.1);
                float n1 = ShaderFXNoise(uv);
                float n2 = ShaderFXNoise(uv * 2.3 + float2(5.2, 1.3)) * 0.5;
                return saturate(n1 + n2) / 1.5;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float dissolveAmount = UNITY_ACCESS_INSTANCED_PROP(ShaderFXPerInstance, _DissolveAmount);
                float hitFlashAmount = UNITY_ACCESS_INSTANCED_PROP(ShaderFXPerInstance, _HitFlashAmount);

                #if defined(_FX_DISSOLVE)
                    float dissolveNoise = ShaderFXDissolveNoise(input.positionWS);
                    clip(dissolveNoise - dissolveAmount);
                #endif

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(input.viewDirWS);

                float2 sampleUV = input.uv;
                #if defined(_FX_GLITCH)
                {
                    float blockSeed = floor(_Time.y * _GlitchSpeed);
                    float2 blockUV = floor(input.uv * _GlitchBlockSize) / _GlitchBlockSize;
                    float jitterX = (ShaderFXHash(blockUV + blockSeed) - 0.5) * 0.1 * _GlitchAmount;
                    float jitterY = (ShaderFXHash(blockUV + blockSeed + 7.3) - 0.5) * 0.05 * _GlitchAmount;
                    sampleUV += float2(jitterX, jitterY);
                }
                #endif

                float4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV) * _BaseColor;

                #if defined(_FX_GLITCH)
                {
                    float2 splitOffset = float2(_GlitchRGBSplit * _GlitchAmount, 0);
                    float splitR = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV + splitOffset).r;
                    float splitB = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, sampleUV - splitOffset).b;
                    albedo.r = lerp(albedo.r, splitR, _GlitchAmount);
                    albedo.b = lerp(albedo.b, splitB, _GlitchAmount);
                }
                #endif

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                float ndotl = saturate(dot(normalWS, mainLight.direction));
                #if defined(_FX_TOON)
                {
                    float steps = max(_ToonSteps, 1.0);
                    ndotl = floor(ndotl * steps) / max(steps - 1.0, 1.0);
                }
                #endif
                float3 lighting = mainLight.color * (ndotl * mainLight.shadowAttenuation);
                lighting += SampleSH(normalWS);

                #if defined(_ADDITIONAL_LIGHTS)
                {
                    uint additionalLightsCount = GetAdditionalLightsCount();
                    for (uint i = 0u; i < additionalLightsCount; i++)
                    {
                        Light additionalLight = GetAdditionalLight(i, input.positionWS);
                        float additionalNdotL = saturate(dot(normalWS, additionalLight.direction));
                        lighting += additionalLight.color * (additionalNdotL * additionalLight.distanceAttenuation * additionalLight.shadowAttenuation);
                    }
                }
                #endif

                float3 color = albedo.rgb * lighting;

                #if defined(_FX_TOON)
                {
                    color = lerp(albedo.rgb * _ToonShadeColor.rgb, color, ndotl);
                }
                #endif

                #if defined(_FX_RIM)
                {
                    float rim = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _RimPower);
                    color += _RimColor.rgb * rim * _RimIntensity;
                }
                #endif

                #if defined(_FX_DISSOLVE)
                {
                    float edge = 1.0 - saturate((dissolveNoise - dissolveAmount) / max(_DissolveEdgeWidth, 0.0001));
                    color += _DissolveEdgeColor.rgb * edge;
                }
                #endif

                #if defined(_FX_HITFLASH)
                {
                    color = lerp(color, _HitFlashColor.rgb, saturate(hitFlashAmount));
                }
                #endif

                #if defined(_FX_EMISSION)
                {
                    float pulse = _FXEmissionPulseSpeed > 0.0001
                        ? (0.5 + 0.5 * sin(_Time.y * _FXEmissionPulseSpeed))
                        : 1.0;
                    color += _FXEmissionColor.rgb * _FXEmissionIntensity * pulse;
                }
                #endif

                #if defined(_FX_FIRE)
                {
                    // Purely-additive fire on top of an already fully-lit surface blows out to a
                    // washed-out near-white almost everywhere (verified: looked like flat pale
                    // pink, not flame). Lerping toward the fire color based on noise strength
                    // instead keeps unlit/low-noise areas showing the original surface, so the
                    // flicker pattern actually reads instead of getting clipped away.
                    float fireNoise = ShaderFXFireNoise(input.positionWS);
                    float3 fireGlow = _FireColor.rgb * (0.4 + fireNoise * 0.6) * _FireIntensity;
                    color = lerp(color, fireGlow, saturate(fireNoise * 1.3));
                }
                #endif

                #if defined(_FX_FROST)
                {
                    float luma = dot(color, float3(0.299, 0.587, 0.114));
                    float3 desaturated = lerp(color, luma.xxx, 0.6);
                    float3 tinted = desaturated * _FrostColor.rgb;
                    color = lerp(color, tinted, _FrostAmount);

                    float sparkleNoise = ShaderFXNoise(input.positionWS.xz * 6.0 + input.positionWS.y * 4.0);
                    float sparkle = saturate((sparkleNoise - _FrostSparkleThreshold) / max(1.0 - _FrostSparkleThreshold, 0.001));
                    color += _FrostSparkleColor.rgb * sparkle * _FrostAmount;
                }
                #endif

                #if defined(_FX_HOLOGRAM)
                {
                    float fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), _HologramFresnelPower);
                    float scanline = sin(input.positionWS.y * _HologramScanlineDensity - _Time.y * _HologramScanlineSpeed) * 0.5 + 0.5;
                    float flickerVal = 1.0 + sin(_Time.y * _HologramFlickerSpeed) * _HologramFlickerIntensity;
                    float holo = saturate(fresnel + scanline * 0.25) * flickerVal;
                    color = lerp(color * 0.15, _HologramColor.rgb, holo);
                    color += _HologramColor.rgb * fresnel * flickerVal;
                }
                #endif

                #if defined(_FX_UVSCROLL)
                {
                    // Sampling noise directly (isotropic) reads as drifting blobs, not "lines".
                    // Building a direction-aligned basis and compressing the along-direction axis
                    // stretches noise features into streaks that flow along _UVScrollDirection —
                    // confirmed visually (isotropic sampling looked like blotches, not veins).
                    float2 dir = normalize(_UVScrollDirection.xy + float2(0.0001, 0.0001));
                    float2 perp = float2(-dir.y, dir.x);
                    float2 baseUV = input.uv * _UVScrollPatternScale;
                    float alongCoord = dot(baseUV, dir) - _Time.y * _UVScrollSpeed;
                    float perpCoord = dot(baseUV, perp);
                    float pattern = ShaderFXNoise(float2(alongCoord * 0.25, perpCoord));
                    float streak = smoothstep(_UVScrollSharpness, 1.0, pattern);
                    // Adding (not replacing) at streak==1 on an already fully-lit surface clips
                    // every channel to white, losing the color entirely (same failure mode found
                    // and fixed for Fire above) — lerp toward the glow color instead.
                    color = lerp(color, _UVScrollColor.rgb * 1.3, streak);
                }
                #endif

                color = MixFog(color, input.fogCoord);
                return half4(color, albedo.a);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #pragma shader_feature_local _FX_DISSOLVE
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #pragma multi_compile_instancing

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _DissolveEdgeWidth;
                float4 _DissolveEdgeColor;
                float _DissolveNoiseScale;
                float4 _HitFlashColor;
                float4 _FXEmissionColor;
                float _FXEmissionIntensity;
                float _FXEmissionPulseSpeed;
            CBUFFER_END

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));

                #if UNITY_REVERSED_Z
                    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif

                output.positionHCS = positionCS;
                output.positionWS = positionWS;
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_FX_DISSOLVE)
                    float dissolveNoise = ShaderFXNoise(input.positionWS.xz * (_DissolveNoiseScale * 0.1) + input.positionWS.y * 0.05);
                    clip(dissolveNoise - UNITY_ACCESS_INSTANCED_PROP(ShaderFXPerInstance, _DissolveAmount));
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #pragma shader_feature_local _FX_DISSOLVE
            #pragma multi_compile_instancing

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _DissolveEdgeWidth;
                float4 _DissolveEdgeColor;
                float _DissolveNoiseScale;
                float4 _HitFlashColor;
                float4 _FXEmissionColor;
                float _FXEmissionIntensity;
                float _FXEmissionPulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 DepthFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_FX_DISSOLVE)
                    float dissolveNoise = ShaderFXNoise(input.positionWS.xz * (_DissolveNoiseScale * 0.1) + input.positionWS.y * 0.05);
                    clip(dissolveNoise - UNITY_ACCESS_INSTANCED_PROP(ShaderFXPerInstance, _DissolveAmount));
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            // Required for URP's combined depth+normals prepass (SampleSceneNormals) — screen-space
            // effects like ShaderFX's Outline module read this. Without it objects using this shader
            // are simply absent from _CameraNormalsTexture.
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            #pragma shader_feature_local _FX_DISSOLVE
            #pragma multi_compile_instancing

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _RimColor;
                float _RimPower;
                float _RimIntensity;
                float _DissolveEdgeWidth;
                float4 _DissolveEdgeColor;
                float _DissolveNoiseScale;
                float4 _HitFlashColor;
                float4 _FXEmissionColor;
                float _FXEmissionIntensity;
                float _FXEmissionPulseSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                #if defined(_FX_DISSOLVE)
                    float dissolveNoise = ShaderFXNoise(input.positionWS.xz * (_DissolveNoiseScale * 0.1) + input.positionWS.y * 0.05);
                    clip(dissolveNoise - UNITY_ACCESS_INSTANCED_PROP(ShaderFXPerInstance, _DissolveAmount));
                #endif
                return half4(normalize(input.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
