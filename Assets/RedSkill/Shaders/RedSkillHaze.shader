// 赤スキル「極太レーザー」の陽炎（光の奔流、2026-11-09）。RedLaserVisuals が本体と同じ筋から作る帯のメッシュに使う。
// 画面のコピーは使わない（ステージの水や床は半透明で、画面のコピー（Opaque Texture）には写らず、暗い帯になってしまったため）。
// 代わりに、後ろの景色を「明るく・暗く」細かくゆらがせて、熱で光が曲がっているように見せる（掛け算の重ね方: 0.5 で変化なし）。
// UV0: x = 根元からの距離 / 届く距離, y = 断面0〜1, z = 根元からの距離(m), w = その場所の帯の太さ(m)
// UV1: x = その筋の長さ(m), y = 壁で止まっているか(0/1)   UV2: 影のふち（RedSkillCutFade を参照）
Shader "MS2026/RedSkill/Haze"
{
    Properties
    {
        _HazeParams ("Strength, Scale, Speed, Outer Edge(0-1, 0 = off)", Vector) = (0.09, 1.3, 3, 1)
        _HazeShape ("Beam Edge(0-1), Cut Softness(m), TipFade(m), Seed", Vector) = (0.6, 0.5, 4, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "RedSkillHaze"
            // 結果 = 後ろの色 × 2 × 出した色。出した色が 0.5 なら変化なし、0.5 より上で明るく、下で暗く。
            Blend DstColor SrcColor
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "RedSkillCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HazeParams;
                float4 _HazeShape;
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
                float3 positionWS : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.lane = input.lane;
                output.cut = input.cut;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                if (_HazeParams.w <= 0.0 || _HazeParams.x <= 0.0001)
                {
                    return half4(0.5, 0.5, 0.5, 1.0);
                }

                float v = input.uv.y;
                float dist = input.uv.z;
                float width = max(input.uv.w, 0.001);
                float laneLength = max(input.lane.x, 0.001);
                float blocked = saturate(input.lane.y);
                float t = _Time.y;

                // ゆらぐ強さ: レーザーのまわりで強く、外へ行くほど弱く。根元・先・途切れる所ではやわらかく消える。
                float x = abs(v * 2.0 - 1.0);
                float edge = saturate(_HazeShape.x);
                float outer = 1.0 - smoothstep(edge, max(saturate(_HazeParams.w), edge + 0.01), x);
                float root = saturate(dist / max(0.8, width * 0.3));
                float tip = _HazeShape.z > 0.0 ? saturate((laneLength - dist) / _HazeShape.z) : 1.0;
                tip = lerp(tip, saturate((laneLength - dist) / 0.3), blocked);
                float cutFade;
                float cutEdge;
                RedSkillCutFade(input.cut, dist, width, _HazeShape.y, cutFade, cutEdge);
                float mask = outer * root * tip * cutFade;

                // 立ちのぼる熱のゆらぎ（世界の位置で作るので、レーザーが首を振っても景色に張り付いて見える）。
                // 2つの細長いノイズを重ねて、細かくゆれる明暗のすじにする。
                float2 p = input.positionWS.xy * _HazeParams.y;
                float speed = _HazeParams.z;
                float n1 = RedSkillValueNoise(float2(p.x * 1.7 + _HazeShape.w, p.y * 0.6 - t * speed));
                float n2 = RedSkillValueNoise(float2(p.x * 3.1 + 4.3, p.y * 1.3 - t * speed * 1.6 + _HazeShape.w));
                float ripple = (n1 * 0.6 + n2 * 0.4 - 0.5) * 2.0;

                float amplitude = saturate(_HazeParams.x * 1.2);
                float shade = 0.5 + ripple * amplitude * 0.5 * mask;
                return half4(shade, shade, shade, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
