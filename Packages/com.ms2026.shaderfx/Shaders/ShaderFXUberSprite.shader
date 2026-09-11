Shader "ShaderFX/Uber Sprite"
{
    // 2D版のオブジェクト系Uberシェーダー(design doc: 2D対応)。RimLight/Dissolve/HitFlash/
    // EmissionControl/Fire/Frost/Hologram/Glitch/UVScroll の9モジュールを、
    // ShaderFXUber.shader(3D用)と全く同じプロパティ名・キーワード名で実装しています。
    // EffectModule側のC#コード(RimLightModule.ApplyTo等)はプロパティ名でMaterialに値を
    // 書き込むだけなので、このシェーダーを使うMaterialに対しても無変更でそのまま動作します —
    // EffectProfile/EffectTarget/EffectDirector/Tween/Timeline/Volumeなど、パッケージの他の
    // 仕組みは一切2D/3Dを区別していません。
    //
    // ToonShadingModuleだけは非対応です(3D専用。理由はToonShadingModule.cs参照 — このシェーダーは
    // アンリット描画のため、量子化すべき陰影の勾配がそもそも存在しません)。
    //
    // 使い方: このシェーダーを使ったMaterialをSpriteRendererに設定するだけです(3D用の
    // ShaderFX/Uberの代わりにこちらを使ってください)。
    //
    // スコープ外: ShadowCaster/DepthNormalsパスは含まないため、ShaderFXが適用されたスプライトは
    // 影を落とさず、画面系のOutlineエフェクトの輪郭検出対象にもなりません(深度・法線情報が
    // ないため)。Grayscale/Posterize/Pixelateの画面系エフェクトはカメラの絵全体にかかる後処理
    // なので、2D/3Dを問わず普通に効きます。
    Properties
    {
        [Header(Base)]
        _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint Color", Color) = (1, 1, 1, 1)

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
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Include/ShaderFXCommon.hlsl"

        // 個体差パラメータ用GPUインスタンシング・バッファ。ShaderFXUber.shader(3D)と同じ設計 —
        // EffectTarget.SetInstanceDissolveAmount/SetInstanceHitFlashAmount がこれを狙う。
        UNITY_INSTANCING_BUFFER_START(ShaderFXPerInstance)
            UNITY_DEFINE_INSTANCED_PROP(float, _DissolveAmount)
            UNITY_DEFINE_INSTANCED_PROP(float, _HitFlashAmount)
        UNITY_INSTANCING_BUFFER_END(ShaderFXPerInstance)

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
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
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct Varyings
        {
            float4 positionHCS : SV_POSITION;
            float4 color : COLOR;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        Varyings Vert(Attributes input)
        {
            Varyings output = (Varyings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);

            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionHCS = TransformWorldToHClip(output.positionWS);
            output.color = input.color * _Color;
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            return output;
        }

        // 2D版のリムライト: 3Dのように法線とカメラ方向の角度を使う代わりに、スプライトの
        // シルエット境界(アルファが不透明→透明へ変わる場所)付近を検出して光らせます。
        // これは2Dスプライト用シェーダーで一般的に使われる近似手法です。
        //
        // オフセットには _MainTex_TexelSize ではなく fwidth(uv)(画面空間でのUV変化量)を
        // 使っています。SpriteRendererはテクスチャをMaterialPropertyBlock経由(アトラス内の
        // どのスプライトを使うかは描画のたびに変わる)で渡すため、Material自体が持つ
        // _MainTex_TexelSize は実際のスプライトテクスチャに対して更新されず、実機検証で
        // 固定値 (1,1,1,1) のままになることを確認しました。fwidth は特定のテクスチャに依存せず
        // 常に「今描いている1ピクセル分のUV変化量」を返すため、この用途により適しています。
        float ShaderFXSpriteRimFactor(float2 uv, float centerAlpha)
        {
            float2 texel = fwidth(uv) * max(_RimPower, 0.1) * 4.0;
            float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texel.x, 0)).a;
            float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(texel.x, 0)).a;
            float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, texel.y)).a;
            float a4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, texel.y)).a;
            float neighborTransparency = 4.0 - (a1 + a2 + a3 + a4);
            return saturate(neighborTransparency) * centerAlpha;
        }

        float ShaderFXSpriteDissolveNoise(float3 positionWS)
        {
            return ShaderFXNoise(positionWS.xy * (_DissolveNoiseScale * 0.1));
        }

        // 2D版のFire: 3D版と同じ2オクターブ構成ですが、ワールドXY(スプライトの平面)を使います。
        float ShaderFXSpriteFireNoise(float3 positionWS)
        {
            float2 uv = float2(positionWS.x, positionWS.y - _Time.y * _FireScrollSpeed) * (_FireNoiseScale * 0.1);
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
                float dissolveNoise = ShaderFXSpriteDissolveNoise(input.positionWS);
                clip(dissolveNoise - dissolveAmount);
            #endif

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

            half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);

            #if defined(_FX_GLITCH)
            {
                float2 splitOffset = float2(_GlitchRGBSplit * _GlitchAmount, 0);
                float splitR = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV + splitOffset).r;
                float splitB = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV - splitOffset).b;
                texColor.r = lerp(texColor.r, splitR, _GlitchAmount);
                texColor.b = lerp(texColor.b, splitB, _GlitchAmount);
            }
            #endif

            half4 col = texColor * input.color;
            float3 color = col.rgb;

            #if defined(_FX_RIM)
            {
                float rim = ShaderFXSpriteRimFactor(sampleUV, texColor.a);
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
                float fireNoise = ShaderFXSpriteFireNoise(input.positionWS);
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

                float sparkleNoise = ShaderFXNoise(input.positionWS.xy * 6.0);
                float sparkle = saturate((sparkleNoise - _FrostSparkleThreshold) / max(1.0 - _FrostSparkleThreshold, 0.001));
                color += _FrostSparkleColor.rgb * sparkle * _FrostAmount;
            }
            #endif

            #if defined(_FX_HOLOGRAM)
            {
                // 2Dにはフレネル(法線・カメラ角度)が意味を持たないため、RimLightと同じ
                // シルエット境界検出を「フレネル」の代わりに使っています。
                float fresnel = ShaderFXSpriteRimFactor(sampleUV, texColor.a);
                float scanline = sin(input.positionWS.y * _HologramScanlineDensity - _Time.y * _HologramScanlineSpeed) * 0.5 + 0.5;
                float flickerVal = 1.0 + sin(_Time.y * _HologramFlickerSpeed) * _HologramFlickerIntensity;
                float holo = saturate(fresnel + scanline * 0.25) * flickerVal;
                color = lerp(color * 0.15, _HologramColor.rgb, holo);
                color += _HologramColor.rgb * fresnel * flickerVal;
            }
            #endif

            #if defined(_FX_UVSCROLL)
            {
                float2 dir = normalize(_UVScrollDirection.xy + float2(0.0001, 0.0001));
                float2 perp = float2(-dir.y, dir.x);
                float2 baseUV = input.uv * _UVScrollPatternScale;
                float alongCoord = dot(baseUV, dir) - _Time.y * _UVScrollSpeed;
                float perpCoord = dot(baseUV, perp);
                float pattern = ShaderFXNoise(float2(alongCoord * 0.25, perpCoord));
                float streak = smoothstep(_UVScrollSharpness, 1.0, pattern);
                color = lerp(color, _UVScrollColor.rgb * 1.3, streak);
            }
            #endif

            return half4(color, col.a);
        }
        ENDHLSL

        // 通常の3D用 Universal Renderer(UniversalRendererData)配下でSpriteRendererを描画する
        // ためのパス。このプロジェクトの PC_Renderer/Mobile_Renderer のような標準構成では
        // こちらが使われます。
        Pass
        {
            Name "SpriteForward"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #pragma shader_feature_local _FX_RIM
            #pragma shader_feature_local _FX_DISSOLVE
            #pragma shader_feature_local _FX_HITFLASH
            #pragma shader_feature_local _FX_EMISSION
            #pragma shader_feature_local _FX_FIRE
            #pragma shader_feature_local _FX_FROST
            #pragma shader_feature_local _FX_HOLOGRAM
            #pragma shader_feature_local _FX_GLITCH
            #pragma shader_feature_local _FX_UVSCROLL
            ENDHLSL
        }

        // URPの専用2D Renderer(Renderer2DData)を使うプロジェクト向けの同一パス。
        Pass
        {
            Name "SpriteForward2DRenderer"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #pragma shader_feature_local _FX_RIM
            #pragma shader_feature_local _FX_DISSOLVE
            #pragma shader_feature_local _FX_HITFLASH
            #pragma shader_feature_local _FX_EMISSION
            #pragma shader_feature_local _FX_FIRE
            #pragma shader_feature_local _FX_FROST
            #pragma shader_feature_local _FX_HOLOGRAM
            #pragma shader_feature_local _FX_GLITCH
            #pragma shader_feature_local _FX_UVSCROLL
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
