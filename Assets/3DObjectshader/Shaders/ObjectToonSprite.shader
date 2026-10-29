// 2Dの絵（SpriteRenderer）を、3Dの物のトゥーンに合わせるためのシェーダー。
// ・絵のまわりに輪郭線（透明な所の近くを8か所読んで、絵の外側に線を引く）
// ・明暗を段に分けて、暗い所を影の色にする（アニメ塗りの感じ）
// ・内側のふちを少し光らせる / 彩度・明るさを 3D と同じ考え方でそろえる / ビームの光で照らされる
// 輪郭線は絵の四角の中にしか描けない。絵の端ぎりぎりまで色がある絵は、Sprite の Mesh Type を Full Rect にして、余白をあけておく。
// 3D のトゥーン（MS2026/3DObject/Toon）と同じ考え方で、機能のオンオフは材質の値（全画素で同じ分岐）で切り替える。
Shader "MS2026/3DObject/ToonSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("絵（SpriteRenderer が入れる）", 2D) = "white" {}
        _Color("色をかける", Color) = (1,1,1,1)

        [Header(Outline)]
        [ToggleUI] _OutlineOn("輪郭線を出す", Float) = 1
        _OutlineWidth("輪郭線の太さ（絵の画素）", Range(0, 8)) = 2
        _OutlineColor("輪郭線の色", Color) = (0.25, 0.22, 0.28, 1)
        _OutlineAlphaThreshold("どこから絵とみなすか（透明度）", Range(0.01, 1)) = 0.5

        [Header(Cel)]
        [IntRange] _CelSteps("明るさの段の数（0=分けない）", Range(0, 6)) = 0
        _ShadowColor("暗い所の色", Color) = (0.62, 0.66, 0.82, 1)
        _ShadowThreshold("暗い所とみなす明るさ", Range(0, 1)) = 0
        _ShadowSoftness("暗い所の境目のぼかし", Range(0, 1)) = 0.1

        [Header(Inner Rim)]
        [ToggleUI] _RimOn("内側のふちを光らせる", Float) = 0
        [HDR] _RimColor("ふちの光の色", Color) = (0.35, 0.35, 0.35, 1)
        _RimWidth("ふちの光の幅（絵の画素）", Range(0, 8)) = 1.5

        [Header(Finish)]
        _BeamLightStrength("ビームに照らされる強さ", Range(0, 2)) = 1
        _Saturation("彩度", Range(0, 2)) = 1
        _Brightness("全体の明るさ", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            Name "ObjectToonSprite"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Assets/_Game/Fortress/Runtime/Laser/Light/Shaders/FortressBeamLight.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize; // SpriteRenderer が絵ごとに入れるので、材質の値（CBUFFER）には入れない

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half  _OutlineOn;
                half  _OutlineWidth;
                half4 _OutlineColor;
                half  _OutlineAlphaThreshold;
                half  _CelSteps;
                half4 _ShadowColor;
                half  _ShadowThreshold;
                half  _ShadowSoftness;
                half  _RimOn;
                half4 _RimColor;
                half  _RimWidth;
                half  _BeamLightStrength;
                half  _Saturation;
                half  _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
                float2 positionWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            static const float2 Around[8] =
            {
                float2(1, 0), float2(-1, 0), float2(0, 1), float2(0, -1),
                float2(0.7071, 0.7071), float2(-0.7071, 0.7071), float2(0.7071, -0.7071), float2(-0.7071, -0.7071)
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                float3 positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionWS = positionWS.xy;
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half SampleAlpha(float2 uv)
            {
                return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = texel * input.color;

                // ── 明暗の段・暗い所の色 ──
                half luma = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                if (_CelSteps >= 1.0h)
                {
                    half stepped = (floor(luma * _CelSteps) + 0.5h) / _CelSteps;
                    color.rgb *= stepped / max(luma, 1e-3h);
                    luma = stepped;
                }

                half soft = max(_ShadowSoftness * 0.5h, 1e-3h);
                half lit = smoothstep(_ShadowThreshold - soft, _ShadowThreshold + soft, luma);
                color.rgb *= lerp(_ShadowColor.rgb, 1.0h, lit);

                // ── 内側のふち・外側の輪郭線（まわり8か所の透明度を見る）──
                if (_OutlineOn > 0.5h || _RimOn > 0.5h)
                {
                    float2 texelSize = _MainTex_TexelSize.xy;
                    half outer = 0.0h;
                    half inner = 1.0h;
                    [unroll]
                    for (int i = 0; i < 8; i++)
                    {
                        outer = max(outer, SampleAlpha(input.uv + Around[i] * texelSize * _OutlineWidth));
                        inner = min(inner, SampleAlpha(input.uv + Around[i] * texelSize * _RimWidth));
                    }

                    if (_RimOn > 0.5h)
                    {
                        color.rgb += _RimColor.rgb * saturate(1.0h - inner) * step(_OutlineAlphaThreshold, texel.a);
                    }

                    // 絵の外側（透明な所）だけに線を引く。線の濃さは絵の色の透明度（被弾の点滅などで薄くなる）に合わせる。
                    if (_OutlineOn > 0.5h && _OutlineWidth > 0.0h)
                    {
                        half outlineMask = step(_OutlineAlphaThreshold, outer) * (1.0h - step(_OutlineAlphaThreshold, texel.a));
                        color.rgb = lerp(color.rgb, _OutlineColor.rgb, outlineMask);
                        color.a = max(color.a, outlineMask * _OutlineColor.a * input.color.a);
                    }
                }

                clip(color.a - 0.001h);

                // ── ビームの光・仕上げ ──
                color.rgb *= 1.0h + FortressBeamEnemyLight(input.positionWS) * _BeamLightStrength;
                half finalLuma = dot(color.rgb, half3(0.2126h, 0.7152h, 0.0722h));
                color.rgb = max(0.0h, lerp(finalLuma.xxx, color.rgb, _Saturation)) * _Brightness;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack "Sprites/Default"
}
