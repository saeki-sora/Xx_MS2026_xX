// 赤スキル「極太レーザー」本体。RedLaserVisuals が作る帯のメッシュに使う。
// UV0: x = 根元からの距離 / 届く距離, y = 断面0〜1, z = 根元からの距離(m), w = その場所の見た目の太さ(m)
// UV1: x = その筋の長さ(m), y = その筋が壁で止まっているか(0/1)。壁で止まった筋は先をぼかさず、壁ぎわを明るくする。
// 色 = 斜めのしま模様(しまのグラデーション、先へ流れる) × 断面グラデーション(中心→ふち) × 長さグラデーション(根元→先端) × 明るさ
//      ＋ 真ん中のツヤ。流れるスジ・荒れたふち・ちらつきを足す。
// 合成は「光を足す」と「塗る」の中間（_BeamParams.w = 塗る強さ）。
Shader "MS2026/RedSkill/Beam"
{
    Properties
    {
        _CrossTex ("Cross Gradient (center -> edge)", 2D) = "white" {}
        _LengthTex ("Length Gradient (root -> tip)", 2D) = "white" {}
        _StripeTex ("Stripe Gradient (one period, repeat)", 2D) = "white" {}
        _StripeParams ("Stripe Period(m), Slant(tan), Scroll(m/s), Strength", Vector) = (3.2, 0.84, 7, 1)
        _ShineParams ("Core Shine, Wall Glow", Vector) = (0.25, 1.2, 0, 0)
        _PulseParams ("Pulse Strength, Spacing(m), Speed(m/s), Width", Vector) = (0, 5, 45, 0.25)
        _BeamParams ("Intensity, Length, TipFade, BodyOpacity", Vector) = (2, 10, 4, 0.35)
        _StreakParams ("Streak Strength, Density, Speed, Flicker", Vector) = (0.55, 10, 16, 0.12)
        _JagParams ("Jag Amount, Frequency, Speed, Seed", Vector) = (0.3, 1.4, 10, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RedSkillBeam"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RedSkillCommon.hlsl"

            TEXTURE2D(_CrossTex);
            SAMPLER(sampler_CrossTex);
            TEXTURE2D(_LengthTex);
            SAMPLER(sampler_LengthTex);
            TEXTURE2D(_StripeTex);
            SAMPLER(sampler_StripeTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _CrossTex_ST;
                float4 _LengthTex_ST;
                float4 _StripeTex_ST;
                float4 _StripeParams;
                float4 _ShineParams;
                float4 _PulseParams;
                float4 _BeamParams;
                float4 _StreakParams;
                float4 _JagParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;
                float4 lane : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 lane : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.lane = input.lane;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;
                float dist = input.uv.z;
                float width = max(input.uv.w, 0.001);
                float t = _Time.y;

                float intensity = _BeamParams.x;
                float laneLength = max(input.lane.x, 0.001);
                float laneBlocked = saturate(input.lane.y);
                float tipFade = _BeamParams.z;
                float opacity = _BeamParams.w;

                // 断面の位置 (0 = 中心, 1 = ふち)。ふちはノイズで荒らす（左右で別のノイズ）。
                float across = abs(v * 2.0 - 1.0);
                float side = v > 0.5 ? 17.3 : 3.1;
                float jagNoise = RedSkillValueNoise(float2(dist * _JagParams.y - t * _JagParams.z, side + _JagParams.w))
                               + 0.5 * RedSkillValueNoise(float2(dist * _JagParams.y * 2.7 - t * _JagParams.z * 1.6, side * 2.0 + _JagParams.w));
                float edge = 1.0 - _JagParams.x * (jagNoise / 1.5) * 0.7;
                float x = across / max(edge, 0.05);
                float inside = 1.0 - smoothstep(0.88, 1.0, x);

                half4 crossColor = SAMPLE_TEXTURE2D(_CrossTex, sampler_CrossTex, float2(saturate(x), 0.5));
                half4 alongColor = SAMPLE_TEXTURE2D(_LengthTex, sampler_LengthTex, float2(saturate(u), 0.5));

                // 流れるスジ（中心の白い芯にはかけない）。太さに合わせて本数が変わらないよう、断面の位置で作る。
                float streakNoise = RedSkillValueNoise(float2(v * _StreakParams.y, dist * 0.12 - t * _StreakParams.z * 0.12))
                                  * 0.65 + RedSkillValueNoise(float2(v * _StreakParams.y * 2.3 + 5.0, dist * 0.3 - t * _StreakParams.z * 0.3)) * 0.35;
                float streak = lerp(1.0, 0.35 + 1.3 * streakNoise, _StreakParams.x * saturate(x * 2.5));

                // 斜めのしま模様。断面の位置をメートルに直して傾けるので、レーザーが広がってもしまの傾きは同じ。先へ流れる。
                float acrossMeters = (v - 0.5) * width;
                float stripeCoord = (dist + acrossMeters * _StripeParams.y - t * _StripeParams.z) / max(_StripeParams.x, 0.05);
                half3 stripeColor = SAMPLE_TEXTURE2D(_StripeTex, sampler_StripeTex, float2(frac(stripeCoord), 0.5)).rgb;
                stripeColor = lerp(half3(1.0, 1.0, 1.0), stripeColor, _StripeParams.w);

                // 真ん中の白いツヤ（筒のような立体感）
                float shine = _ShineParams.x * pow(saturate(1.0 - x * 1.8), 3.0);

                // ちらつき
                float flicker = 1.0 + (RedSkillHash(floor(t * 30.0) + _JagParams.w) - 0.5) * 2.0 * _StreakParams.w;

                // 根元はやわらかく、壁で止まっていない筋は先端をぼかして消す（止まっている筋は壁でくっきり終わる）。
                float root = saturate(dist / max(0.25, width * 0.25));
                float tip = tipFade > 0.0 ? saturate((laneLength - dist) / tipFade) : 1.0;
                tip = lerp(tip, 1.0, laneBlocked);

                // 壁で止まっている筋は、壁ぎわを明るくする（ぶつかって押し返されている感じ）。
                float wall = laneBlocked * _ShineParams.y * pow(saturate(1.0 - (laneLength - dist) / max(0.6, width * 0.12)), 2.0);

                float alpha = saturate(crossColor.a * alongColor.a * inside * root * tip);
                // 光の波: 根元から先へ走る明るい帯（真ん中ほど明るい）。
                float pulsePhase = frac((dist - t * _PulseParams.z) / max(_PulseParams.y, 0.2));
                float pulseBand = saturate(1.0 - abs(pulsePhase - 0.5) / max(_PulseParams.w * 0.5, 0.01));
                float pulse = _PulseParams.x * pulseBand * pulseBand * (1.0 - x * 0.6);

                float3 color = (stripeColor * crossColor.rgb * alongColor.rgb * streak + shine + wall * float3(1.0, 0.55, 0.5)
                               + pulse * float3(1.0, 0.85, 0.85)) * intensity * flicker;

                // 加算（opacity 0）〜普通の重ね（opacity 1）。
                return half4(color * alpha, alpha * opacity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
