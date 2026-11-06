// スマッシュボールのしゃぼん玉（比較ページの G 案: 虹色の膜 ＋ 虹色のにじみ）。
// 1つのマテリアルで2回描く。
//   1回目「にじみ」: 泡をシェーダーの中で何倍かにふくらませて描き、ピクセルごとに「視線と泡の中心の距離」から
//                   光の強さを決める。どのプレイヤーのカメラ（遠近法・斜め・画面の端）から見ても泡の縁にぴったり付く。
//                   先に描くので、泡の後ろに回り込んだように見える。メッシュの大きさは変えないので、
//                   破壊可能物の「大きさへの合わせ方」（Renderer の大きさで測る）には影響しない。
//   2回目「膜」:     半透明の泡。縁ほど虹色が濃く、中にも色がゆっくり流れる。つやも付ける。
// 破壊可能物が掛ける _BaseColor は、濃さ（A）だけを使う。割れた後の薄い跡は効くが、
// 「ダメージで暗くする」の色（RGB）は無視して、割れる直前まで同じ見た目のままにする（ユーザー決定 2026-11-06）。
// メッシュは「原点が中心の球」を想定（Bubble2.fbx はそうなっている）。
Shader "MS2026/Fortress/SmashBubble"
{
    Properties
    {
        [HideInInspector] _BaseColor ("ゲームが掛ける色（白のまま）", Color) = (1, 1, 1, 1)

        [Header(Bubble)]
        _BubbleColor ("泡の色（A = 濃さ）", Color) = (0.78, 0.93, 1, 0.12)
        _FilmStrength ("虹色の膜の濃さ", Range(0, 1.5)) = 1
        _FilmRimPower ("膜の縁の細さ（大きいほど縁だけ）", Range(0.5, 4)) = 1.6
        _FilmSwirl ("膜の中を流れる色", Range(0, 1)) = 0.45
        _Highlight ("つやの明るさ", Range(0, 2)) = 1

        [Header(Halo)]
        _HaloStrength ("にじみの強さ", Range(0, 3)) = 1
        _HaloSize ("にじみの広さ（泡の何倍）", Range(1.1, 4)) = 2.2
        _HaloCore ("にじみの白い芯", Range(0, 2)) = 0.6
        _HaloAdditive ("にじみの光り方（0 = 色を重ねる / 1 = 光を足す）", Range(0, 1)) = 0.35

        [Header(Motion)]
        _HueSpeed ("色が回る速さ", Range(0, 3)) = 1
        _PulseSpeed ("脈打つ速さ", Range(0, 10)) = 3.6
        _PulseAmount ("脈打つ大きさ", Range(0, 1)) = 0.3
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _BubbleColor;
            half _FilmStrength;
            half _FilmRimPower;
            half _FilmSwirl;
            half _Highlight;
            half _HaloStrength;
            half _HaloSize;
            half _HaloCore;
            half _HaloAdditive;
            half _HueSpeed;
            half _PulseSpeed;
            half _PulseAmount;
        CBUFFER_END

        // 色相(0〜1) → 少しだけ白に寄せた虹色
        float3 BubbleRainbow(float hue)
        {
            float3 c = saturate(abs(frac(hue + float3(0.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
            return lerp(float3(1.0, 1.0, 1.0), c, 0.88);
        }
        ENDHLSL

        // 1回目: 虹色のにじみ（URP は SRPDefaultUnlit を UniversalForward より先に描く）
        Pass
        {
            Name "SmashBubbleHalo"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex HaloVert
            #pragma fragment HaloFrag

            struct HaloAttributes
            {
                float4 positionOS : POSITION;
            };

            struct HaloVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0; // 手前へ寄せた後の位置（このピクセルの視線の上にある点）
                float4 centerCS : TEXCOORD1;   // 泡の中心の画面位置（色の向きを決めるのに使う）
                float4 pixelCS : TEXCOORD2;
            };

            HaloVaryings HaloVert(HaloAttributes input)
            {
                HaloVaryings output;
                float pulse = 1.0 + 0.08 * _PulseAmount * sin(_Time.y * _PulseSpeed);

                // 泡（原点が中心の単位球）を _HaloSize 倍にふくらませた球。どのカメラから見ても、
                // この球の見える範囲 =「泡の中心から視線までの距離が _HaloSize 以内」の範囲になる。
                float3 inflatedOS = input.positionOS.xyz * (_HaloSize * pulse);
                float3 positionVS = TransformWorldToView(TransformObjectToWorld(inflatedOS));

                // 床に埋まらないよう、各点を「自分の視線に沿って」カメラ側へ寄せる。
                // 視線に沿って動かすので、画面上の位置は1ピクセルも変わらない（遠近法でも平行投影でも）。
                float3x3 objectToWorld = (float3x3)GetObjectToWorldMatrix();
                // 寄せる量 = 泡のワールドでの半径（いちばん長い軸）
                float push = max(max(length(mul(objectToWorld, float3(1, 0, 0))), length(mul(objectToWorld, float3(0, 1, 0)))), length(mul(objectToWorld, float3(0, 0, 1))));
                if (unity_OrthoParams.w > 0.5)
                {
                    positionVS.z += push;
                }
                else
                {
                    float depth = max(-positionVS.z, 1e-4);
                    positionVS *= max(depth - push, depth * 0.05) / depth;
                }

                output.positionCS = TransformWViewToHClip(positionVS);
                output.positionWS = mul(UNITY_MATRIX_I_V, float4(positionVS, 1.0)).xyz;
                output.centerCS = TransformWorldToHClip(TransformObjectToWorld(float3(0.0, 0.0, 0.0)));
                output.pixelCS = output.positionCS;
                return output;
            }

            half4 HaloFrag(HaloVaryings input) : SV_Target
            {
                float t = _Time.y;

                // このピクセルの視線と泡の中心との距離を、泡の形（単位球）の座標で測る。
                // 泡の輪郭ちょうどで 1 になるので、どの視点・画面のどこに泡があっても、にじみは泡の縁にぴったり付く。
                float3 pointOS = TransformWorldToObject(input.positionWS);
                float3 rayOS;
                if (unity_OrthoParams.w > 0.5)
                {
                    rayOS = mul((float3x3)GetWorldToObjectMatrix(), -UNITY_MATRIX_V[2].xyz);
                }
                else
                {
                    rayOS = pointOS - TransformWorldToObject(GetCameraPositionWS());
                }
                float d = length(cross(pointOS, normalize(rayOS)));
                float pulse = 1.0 + 0.08 * _PulseAmount * sin(t * _PulseSpeed);
                float spread = max(_HaloSize * pulse - 1.0, 1e-3);

                // 泡の縁で一番強く、外へ向かって消える。内側は泡の縁の近くだけ少し光る。
                float outer = saturate(1.0 - (d - 1.0) / spread);
                float glow = d < 1.0 ? smoothstep(0.55, 1.0, d) : outer * outer;

                // 色の向き: 画面の上で、泡の中心から見たこのピクセルの向き
                float2 delta = input.pixelCS.xy / input.pixelCS.w - input.centerCS.xy / input.centerCS.w;
                delta.x *= _ScreenParams.x / _ScreenParams.y;
                delta.y *= _ProjectionParams.x;
                float angle = atan2(delta.y, delta.x) * (0.5 / PI);
                float3 rainbow = BubbleRainbow(frac(angle + t * 0.15 * _HueSpeed));

                float strengthPulse = 1.0 + _PulseAmount * sin(t * _PulseSpeed);
                float glowAlpha = saturate(glow * _HaloStrength * strengthPulse * 0.6);
                float core = exp(-pow(max(d - 1.0, 0.0) / 0.35, 2.0)) * smoothstep(0.7, 1.0, d);
                float coreAlpha = saturate(core * _HaloCore * 0.8);

                // 乗算済みアルファ。A を減らすほど「光を足す」寄りになる。
                float3 color = rainbow * glowAlpha + coreAlpha.xxx;
                float alpha = saturate(glowAlpha + coreAlpha) * (1.0 - _HaloAdditive);

                color *= _BaseColor.a; // 色（RGB）は使わない: ダメージで暗くしない
                alpha *= _BaseColor.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }

        // 2回目: 虹色の膜の泡
        Pass
        {
            Name "SmashBubbleFilm"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex FilmVert
            #pragma fragment FilmFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct FilmAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct FilmVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            FilmVaryings FilmVert(FilmAttributes input)
            {
                FilmVaryings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            half4 FilmFrag(FilmVaryings input) : SV_Target
            {
                float t = _Time.y;
                float3 n = normalize(input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float facing = saturate(dot(n, v));
                float fresnel = 1.0 - facing;
                float3 normalVS = normalize(mul((float3x3)GetWorldToViewMatrix(), n));
                float angle = atan2(normalVS.y, normalVS.x) * (0.5 / PI);

                // 縁の虹色（画面上の向きで色が変わり、時間で回る）
                float rim = pow(fresnel, _FilmRimPower);
                float3 rimColor = BubbleRainbow(frac(angle + t * 0.45 * _HueSpeed));
                float rimAlpha = saturate(rim * 0.85 * _FilmStrength);

                // 中をゆっくり流れる色
                float2 flowDir = float2(cos(t * 0.9), sin(t * 0.9));
                float swirl = sin(dot(normalVS.xy, flowDir) * 3.5 + t * 1.3) * 0.5 + 0.5;
                float3 swirlColor = BubbleRainbow(frac(swirl * 0.6 + angle * 0.3 + t * 0.12 * _HueSpeed));
                float swirlAlpha = saturate(_FilmSwirl * 0.35 * _FilmStrength);

                // つや: 太陽の映り込み ＋ 左上の決まった位置のハイライト（どんな照明でも1つは見えるように）
                Light mainLight = GetMainLight();
                float3 h = normalize(mainLight.direction + v);
                float spec = pow(saturate(dot(n, h)), 96.0) * 1.5 * dot(mainLight.color, float3(0.2126, 0.7152, 0.0722));
                float fixedSpot = smoothstep(0.93, 0.99, dot(normalVS, normalize(float3(-0.45, 0.5, 0.75))));
                float shine = saturate((spec + fixedSpot) * _Highlight);

                float bodyAlpha = _BubbleColor.a;
                float3 color = _BubbleColor.rgb * bodyAlpha + swirlColor * swirlAlpha + rimColor * rimAlpha + shine.xxx;
                float alpha = saturate(bodyAlpha + swirlAlpha * 0.6 + rimAlpha * 0.7 + shine * 0.8);

                color *= _BaseColor.a; // 色（RGB）は使わない: ダメージで暗くしない
                alpha *= _BaseColor.a;
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
