// 歯磨き粉ビームの見た目（LineRenderer 用）。
// ・色は今までどおり「縞の絵」(_BaseMap。縦方向＝ビームの幅、白・赤・青の縞) × 線の色 × _BaseColor。光の当たり方で暗くならない（以前の Particles/Unlit と同じ考え方）。
// ・その上に、丸いチューブとしての「つや」、ジェルの透け、先へ流れる「こぶ」（縁がふくらんだりくびれたりして流れる）・きらめき・押し出しのすじ・泡を足す。
//   カメラが遠く、ビームが画面で10〜25ピクセルほどにしか写らないので、こぶ・太いつやの帯・大きめのきらめきなど「遠くからでも分かる」変化を主にしている。
// ・_Beam* は LaserTurretLook が毎フレーム MaterialPropertyBlock で入れる。入っていないとき（シーンビュー等）は下の既定値で静止して見える。
// ・ビームの幅方向は画面上の微分(ddx/ddy)から求めるので、LineRenderer のうねり・太さの変化にそのまま追従する。
Shader "MS2026/Fortress/ToothpasteBeam"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("縞の絵（縦方向＝ビームの幅）", 2D) = "white" {}
        [MainColor] _BaseColor ("色（線の色とかけ合わせる）", Color) = (1,1,1,1)

        [Header(Gloss)]
        _Gloss ("つやの強さ", Range(0, 2)) = 1.1
        _GlossSharpness ("つやの鋭さ", Range(4, 160)) = 18
        _GlossPowerBoost ("強く握ったときのつや増し", Range(0, 2)) = 0.6
        _SheenStrength ("ぼんやりした照り", Range(0, 1)) = 0.18
        _RimStrength ("ふちの明るさ", Range(0, 1)) = 0.3

        [Header(Gel)]
        _EdgeSoftness ("ふちの透け", Range(0, 0.6)) = 0.2
        _Translucency ("ジェルの透け感", Range(0, 1)) = 0.35

        [Header(Flow)]
        _PulseAmount ("流れるこぶの大きさ", Range(0, 0.7)) = 0.32
        _PulseSpacing ("こぶの間隔（メートル）", Range(0.2, 5)) = 1.3
        _GlintAmount ("流れるきらめき", Range(0, 2)) = 1.2
        _GlintScale ("きらめきの細かさ", Range(1, 30)) = 3
        _RibAmount ("押し出しのすじ", Range(0, 0.5)) = 0.14
        _RibSpacing ("すじの間隔（メートル）", Range(0.05, 2)) = 0.6

        [Header(Bubbles)]
        _BubbleAmount ("泡の量", Range(0, 1)) = 0.3
        _BubbleSize ("泡の大きさ", Range(0.05, 0.5)) = 0.2
        _BubbleCellsAcross ("幅に並ぶ泡の数", Range(1, 8)) = 3

        [HideInInspector] _BeamLength ("Beam Length", Float) = 10
        [HideInInspector] _BeamWidth ("Beam Width", Float) = 0.3
        [HideInInspector] _BeamFlow ("Beam Flow", Float) = 0
        [HideInInspector] _BeamPower ("Beam Power", Float) = 0.5
        [HideInInspector] _BeamTipRound ("Beam Tip Round", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "ToothpasteBeam"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half _Gloss;
                half _GlossSharpness;
                half _GlossPowerBoost;
                half _SheenStrength;
                half _RimStrength;
                half _EdgeSoftness;
                half _Translucency;
                half _PulseAmount;
                float _PulseSpacing;
                half _GlintAmount;
                float _GlintScale;
                half _RibAmount;
                float _RibSpacing;
                half _BubbleAmount;
                half _BubbleSize;
                float _BubbleCellsAcross;
                float _BeamLength;
                float _BeamWidth;
                float _BeamFlow;
                half _BeamPower;
                half _BeamTipRound;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 w = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, w.x), lerp(c, d, w.x), w.y);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float u = input.uv.x;
                float v = input.uv.y;

                // ビームの幅方向（vが増える向き）を世界座標で求める。分岐より前に微分を取る。
                float3 dpdx = ddx(input.positionWS);
                float3 dpdy = ddy(input.positionWS);
                float dvdx = ddx(v);
                float dvdy = ddy(v);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 across = dvdx * dpdx + dvdy * dpdy;
                float acrossLength = length(across);
                across = acrossLength > 1e-7 ? across / acrossLength : float3(1.0, 0.0, 0.0);

                float along = u * _BeamLength;   // 発射口からの距離（メートル）
                float s = along - _BeamFlow;     // 表面と一緒に先へ流れる座標
                float x = v * 2.0 - 1.0;         // 幅方向 -1〜1
                half alpha = 1.0;

                // 届く距離の先（何にも当たっていないとき）: 先端を丸くして、絞り出した歯磨き粉の先のように終わらせる。
                // 先から「太さの半分×1.3」の所から細くなり始め、先で0になる（四角く途切れない）。
                if (_BeamTipRound > 0.001 && _BeamWidth > 0.0001)
                {
                    float capLength = _BeamWidth * 0.65;
                    float c = saturate(1.0 - (_BeamLength - along) / capLength);
                    float capRadius = lerp(1.0, sqrt(saturate(1.0 - c * c)), _BeamTipRound);
                    alpha *= 1.0 - smoothstep(capRadius - 0.1, capRadius, abs(x));
                    x /= max(capRadius, 0.15);
                }

                // 流れるこぶ: 縁がふくらんだりくびれたりしながら先へ流れる（強く握るほどはっきり）。こぶの所は少し明るい。
                float lump = 1.0;
                if (_PulseAmount > 0.001)
                {
                    float phase = s / max(_PulseSpacing, 0.05) * 6.2832 + ValueNoise(float2(s * 0.6, 7.7)) * 2.0;
                    lump = 0.5 + 0.5 * sin(phase);
                    float pulseRadius = 1.0 - _PulseAmount * (0.55 + 0.45 * _BeamPower) * (1.0 - lump);
                    alpha *= 1.0 - smoothstep(pulseRadius - 0.1, pulseRadius, abs(x));
                    x /= max(pulseRadius, 0.15);
                }

                x = clamp(x, -1.0, 1.0);
                float ax = abs(x);
                float nz = sqrt(saturate(1.0 - x * x));
                float vShape = x * 0.5 + 0.5;

                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, float2(u, vShape) * _BaseMap_ST.xy + _BaseMap_ST.zw);
                half3 color = tex.rgb * _BaseColor.rgb * input.color.rgb;
                alpha *= tex.a * _BaseColor.a * input.color.a;
                alpha *= smoothstep(1.0, 1.0 - max(_EdgeSoftness, 0.001), ax);

                // 押し出しのすじ（先へ流れる）
                float rib = sin(s / max(_RibSpacing, 0.01) * 6.2832 + ValueNoise(float2(s * 1.7, 3.1)) * 3.0);
                color *= (1.0 + rib * _RibAmount * nz) * (1.0 + 0.12 * lump * nz);

                // ジェルの透け: ふちは明るく薄く、芯はほんのり明るい
                color = lerp(color, color * 1.08 + 0.06, (1.0 - nz) * _Translucency);
                color *= 1.0 + _Translucency * 0.15 * nz;

                // つや: 丸いチューブとして、ステージの太陽の向きで光る。カメラ側からの照りも少し足して、どの向きでもつやが見えるようにする。
                float3 normalWS = normalize(across * x + viewDir * nz);
                Light mainLight = GetMainLight();
                half lightMax = max(max(mainLight.color.r, mainLight.color.g), max(mainLight.color.b, 0.001));
                half3 specColor = mainLight.color / lightMax;
                float3 halfDir = normalize(mainLight.direction + viewDir);
                float3 cameraHalf = normalize(viewDir - across * 0.45);
                float nh = saturate(dot(normalWS, halfDir));
                float nhCamera = saturate(dot(normalWS, cameraHalf));
                half gloss = _Gloss * (1.0 + _GlossPowerBoost * _BeamPower);
                half spec = max(pow(nh, _GlossSharpness), 0.6 * pow(nhCamera, _GlossSharpness)) * gloss
                    + pow(max(nh, nhCamera), 6.0) * _SheenStrength;

                // 流れるきらめき（つやの帯の近くほど強い）
                float2 glintPos = float2(s * _GlintScale * 0.35, vShape * 5.0);
                float glintNoise = ValueNoise(glintPos) * 0.65 + ValueNoise(glintPos * 2.3 + 7.1) * 0.35;
                half glint = smoothstep(0.62, 0.9, glintNoise) * _GlintAmount * (0.5 + 0.8 * _BeamPower)
                    * (0.25 + pow(max(nh, nhCamera), 4.0)) * nz;

                // 泡（ビームの太さに合わせた大きさで、表面と一緒に流れる）
                if (_BubbleAmount > 0.001 && _BeamWidth > 0.0001)
                {
                    float cell = _BeamWidth / max(_BubbleCellsAcross, 1.0);
                    float2 bubblePos = float2(s, vShape * _BeamWidth) / cell;
                    float2 id = floor(bubblePos);
                    float2 f = frac(bubblePos);
                    float2 h = Hash22(id + 31.7);
                    float present = step(h.x, _BubbleAmount * (0.6 + 0.4 * _BeamPower));
                    float2 center = 0.25 + 0.5 * Hash22(id + 4.3);
                    float radius = _BubbleSize * (0.6 + 0.6 * h.y);
                    float d = length(f - center);
                    float inside = smoothstep(0.15, 0.5, nz) * present;
                    float body = (1.0 - smoothstep(radius * 0.85, radius, d)) * inside;
                    float ring = body * smoothstep(radius * 0.45, radius * 0.9, d);
                    float spot = (1.0 - smoothstep(0.0, radius * 0.28, length(f - center - float2(-0.35, 0.35) * radius))) * inside;
                    color = lerp(color, color * 0.82 + 0.12, body * 0.6);
                    color += ring * 0.28 + spot * 0.9;
                    alpha *= 1.0 - body * 0.25;
                }

                half rim = pow(1.0 - nz, 3.0) * _RimStrength;
                color += specColor * spec + glint + rim;
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
