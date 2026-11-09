// 赤スキル「極太レーザー」の、床を照らす光（光の奔流、2026-11-09）。RedLaserVisuals が本体と同じ筋から作る太い帯のメッシュに使う。
// 帯は筋ごとに壁で止まるので、障害物の後ろは自然に影になる（光は回り込まない）。となりの帯が手前で終わる所は本体と同じくぼかす。
// UV0: x = 根元からの距離 / 届く距離, y = 断面0〜1, z = 根元からの距離(m), w = その場所の帯の太さ(m)
// UV1: x = その筋の長さ(m), y = 壁で止まっているか(0/1)   UV2: 影のふち（RedSkillCutFade を参照）
// 2つのパスで描く: ①床の色の緑と青を少し落として赤く染める（掛け算） ②床の色に光の色を掛けて足す（照らされた明るさ。明るい床でも白く飛びにくい）。
Shader "MS2026/RedSkill/FloorLight"
{
    Properties
    {
        _FloorColor ("Light Color (HDR)", Color) = (1.6, 0.35, 0.4, 1)
        _FloorParams ("Tint, Beam Edge(0-1), Contact Glow, Intensity", Vector) = (0.45, 0.4, 1.2, 1)
        _CutParams ("Cut Softness(m), -, TipFade(m), Outer Edge(0-1)", Vector) = (0.5, 0, 4, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "RedSkillCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _FloorColor;
            float4 _FloorParams;
            float4 _CutParams;
        CBUFFER_END

        struct Attributes
        {
            float4 positionOS : POSITION;
            float4 uv : TEXCOORD0;
            float4 lane : TEXCOORD1;
            float4 cut : TEXCOORD2;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float4 uv : TEXCOORD0;
            float4 lane : TEXCOORD1;
            float4 cut : TEXCOORD2;
        };

        Varyings vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            output.lane = input.lane;
            output.cut = input.cut;
            return output;
        }

        // 光の強さ（0〜1くらい）と、当たった所の明るさ。
        float FloorAmount(Varyings input, out float contact)
        {
            float v = input.uv.y;
            float dist = input.uv.z;
            float width = max(input.uv.w, 0.001);
            float laneLength = max(input.lane.x, 0.001);
            float blocked = saturate(input.lane.y);

            // 断面: レーザーの見た目のふちの少し内側までは全部、そこから外へなめらかに弱くなる。
            float x = abs(v * 2.0 - 1.0);
            float beamEdge = saturate(_FloorParams.y);
            float outerEdge = _CutParams.w > 0.0 ? saturate(_CutParams.w) : 1.0;
            float t = saturate((x - beamEdge * 0.7) / max(outerEdge - beamEdge * 0.7, 0.01));
            float across = (1.0 - t) * (1.0 - t);

            // 根元はやわらかく、壁で止まっていない筋は先をぼかす。壁で止まった筋は壁ぎわを明るくする。
            float root = saturate(dist / max(0.6, width * 0.2));
            float tip = _CutParams.z > 0.0 ? saturate((laneLength - dist) / _CutParams.z) : 1.0;
            tip = lerp(tip, saturate((laneLength - dist) / 0.15 + 0.2), blocked);
            contact = blocked * pow(saturate(1.0 - (laneLength - dist) / 2.2), 2.0) * (1.0 - t * 0.6);

            float cutFade;
            float cutEdge;
            RedSkillCutFade(input.cut, dist, width, _CutParams.x, cutFade, cutEdge);
            return across * root * tip * cutFade;
        }
        ENDHLSL

        // ① 赤く染める（緑と青を落とす）。
        Pass
        {
            Name "RedSkillFloorTint"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend DstColor Zero
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragTint

            half4 fragTint(Varyings input) : SV_Target
            {
                float contact;
                float amount = FloorAmount(input, contact) * _FloorParams.w;
                float k = saturate(amount * _FloorParams.x);
                return half4(lerp(float3(1.0, 1.0, 1.0), float3(1.0, 0.42, 0.48), k), 1.0);
            }
            ENDHLSL
        }

        // ② 照らす（床の色 × 光の色 を足す）。
        Pass
        {
            Name "RedSkillFloorLight"
            Tags { "LightMode" = "UniversalForward" }
            Blend DstColor One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragLight

            half4 fragLight(Varyings input) : SV_Target
            {
                float contact;
                float amount = FloorAmount(input, contact);
                float3 light = _FloorColor.rgb * (amount * 0.6 + contact * _FloorParams.z) * _FloorParams.w;
                return half4(max(light, 0.0), 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
