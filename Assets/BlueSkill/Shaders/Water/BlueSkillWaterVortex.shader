// 青スキルの「水の渦」（水のシェーダー版）。
// 元: 2D-Water-Shader（Made By Stefan Jovanović, https://github.com/StefanJo3107/2D-Water-Shader）の Custom/WaterShader。
// MIT License, Copyright (c) 2020 Stefan Jovanovic。ライセンス全文は同じフォルダの LICENSE_2D-Water-Shader.txt。
//
// 元のシェーダーからの変更:
// ・URP 用に書き直した（CGPROGRAM → HLSLPROGRAM）。
// ・横に流れる水面ではなく、渦にした: ゆがみの絵2枚を「中心のまわりの角度 × 中心からの距離（対数）」で貼り、
//   角度の方向へ流す（回る）＋ 距離の方向へ流す（中心へ吸い込まれる）。距離を対数で取るので、中心に近いほど模様が細かく速くなり、
//   排水口へ落ちていくように見える。座標はうず巻き状にねじってから取る。
// ・元はカメラの絵（レンダーテクスチャ）をゆがませて映していたが、ここでは「中心→ふち」の色をゆがませて塗る。
// ・泡の出し方（ゆがみの2つの値が両方しきい値を超えた所が泡）は元と同じ考え方。元の「水面のふちの泡」は渦のふちの泡にした。
// ・足したもの: 渦の深さ（中心へ向かうほど暗い漏斗）、水面のきらめき、うがい薬の泡（丸い泡が流れに乗って中心へ吸い込まれる）、
//   排水口の渦のような細いうず巻きの溝（中心ほどきつく巻く筋。片側がツヤで光り、反対側が少し暗い。水のゆがみで少し揺れる）、
//   消えるとき（吸い込み 0→1）に中心の穴が広がって全部が穴へ落ちる。
// ・光るふちの輪・中心の目の輪は MS2026/BlueSkill/Vortex と同じ。プロパティの _Params〜_Params3 も同じ並び（BlueVortexVisual が両方に同じ値を渡す）。
//
// _RadialTex: 中心→ふちの色（横1列、HDR。アルファ=濃さ）
// _Params : x=うず巻きの本数, y=ねじれ, z=筋のくっきり具合, w=水のゆらぎ
// _Params2: x=回転(ラジアン、CPUで積算), y=中心の目の大きさ(0〜1), z=濃さ, w=明るさ
// _Params3: x=時間(秒), y=ふちの明るさ(点滅), z=回る向き(+1/-1), w=中心への吸い込み(0〜1、消えるとき)
// _WaterParams : x=ゆがみの絵の回る速さ, y=細かいゆがみの絵の回る速さ, z=ゆがみの強さ（元の 1/Displacement Amount Divider）, w=泡の出やすさのしきい値
// _WaterParams2: x=角度方向のくり返し数, y=距離方向の大きさ, z=消えるときの穴の広がり(0〜1), w=ふちの泡の幅
// _WaterParams3: x=泡の濃さ（元の Foam Alpha）, y=筋の模様の強さ, z=中心へ流れ込んだ量（CPUで積算）, w=水面のきらめき
// _WaterParams4: x=うがい薬の泡の量(0〜1), y=泡の1周の数, z=泡の大きさ(0〜0.5), w=渦の深さ（中心の暗さ, 0〜1）
// _WaterParams5: x=うず巻きの溝の本数, y=溝の巻きのきつさ, z=溝のツヤの強さ, w=溝の影の強さ
// _RingParams : x=輪にする(0〜1。1 で中心の目の内側を透明にして、キャラのまわりを回る水の輪にする), y=内側のふちの泡の強さ, z/w=（未使用）
Shader "MS2026/BlueSkill/WaterVortex"
{
    Properties
    {
        _RadialTex ("Radial Gradient", 2D) = "white" {}
        _DisplacementTex ("Displacement Texture", 2D) = "gray" {}
        _DisplacementDetailTex ("Displacement Detail Texture", 2D) = "gray" {}
        [HDR] _FoamColor ("Foam Color", Color) = (0.75, 1.25, 1.9, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.35, 1.1, 2.4, 1)
        [HDR] _EyeColor ("Eye Ring Color", Color) = (1.6, 1.9, 2.6, 1)
        [HDR] _WaterFoamColor ("Water Foam Color", Color) = (1.6, 1.9, 2.2, 1)
        [HDR] _BubbleColor ("Gargle Bubble Color", Color) = (0.9, 1.5, 2.0, 1)
        _Params ("Arms, Twist, Sharpness, Turbulence", Vector) = (4, 4.5, 2.6, 0.45)
        _Params2 ("Rotation, Eye Size, Opacity, Intensity", Vector) = (0, 0.17, 0.85, 1.2)
        _Params3 ("Time, Rim, Direction, Suck", Vector) = (0, 1, 1, 0)
        _WaterParams ("Speed, Detail Speed, Distortion, Foam Threshold", Vector) = (0.035, 0.017, 0.06, 0.85)
        _WaterParams2 ("Angular Repeat, Radial Scale, Drain Eye, Edge Foam", Vector) = (3, 1.5, 0.75, 0.08)
        _WaterParams3 ("Foam Alpha, Arm Strength, Inflow Phase, Sheen", Vector) = (0.85, 0.6, 0, 0.8)
        _WaterParams4 ("Bubble Amount, Bubble Repeat, Bubble Size, Depth", Vector) = (0.35, 16, 0.32, 0.6)
        _WaterParams5 ("Groove Count, Groove Tightness, Groove Shine, Groove Shade", Vector) = (11, 5, 1.2, 0.3)
        _RingParams ("Hollow, Inner Foam, -, -", Vector) = (0, 1, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "BlueSkillWaterVortex"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../BlueSkillCommon.hlsl"

            TEXTURE2D(_RadialTex);
            SAMPLER(sampler_RadialTex);
            TEXTURE2D(_DisplacementTex);
            SAMPLER(sampler_DisplacementTex);
            TEXTURE2D(_DisplacementDetailTex);
            SAMPLER(sampler_DisplacementDetailTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _RadialTex_ST;
                float4 _DisplacementTex_ST;
                float4 _DisplacementDetailTex_ST;
                half4 _FoamColor;
                half4 _RimColor;
                half4 _EyeColor;
                half4 _WaterFoamColor;
                half4 _BubbleColor;
                float4 _Params;
                float4 _Params2;
                float4 _Params3;
                float4 _WaterParams;
                float4 _WaterParams2;
                float4 _WaterParams3;
                float4 _WaterParams4;
                float4 _WaterParams5;
                float4 _RingParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Band(float d, float w)
            {
                return exp(-(d * d) / max(w * w, 1e-6));
            }

            // うがい薬の泡: 「角度 × 対数の距離」の升目（この座標は形がつぶれないので、泡は丸いまま中心へ近づくほど小さくなる）に、
            // ところどころ丸い泡（ふちが光る輪＋小さなツヤ）を置く。cell = 升目の座標。戻り値 x=泡のふち, y=ツヤ, z=泡の中。
            // repeatX = 1周の升目の数（角度のつなぎ目で泡が切れないよう、升目の番号を1周で回す）。
            float3 GargleBubbles(float2 cell, float repeatX, float amount, float size)
            {
                float2 id = floor(cell);
                id.x -= repeatX * floor(id.x / repeatX);
                float2 f = frac(cell) - 0.5;
                float exists = step(1.0 - amount, BlueSkillHash2(id + 3.7));
                float2 center = (float2(BlueSkillHash2(id + 11.1), BlueSkillHash2(id + 27.3)) - 0.5) * (1.0 - 2.0 * size) * 0.9;
                float radius = size * lerp(0.55, 1.0, BlueSkillHash2(id + 41.9));
                float d = length(f - center);
                float rimLine = Band(d - radius, radius * 0.18) * exists;
                float shine = Band(length(f - center - float2(-0.35, 0.35) * radius), radius * 0.22) * exists;
                float inside = (1.0 - smoothstep(radius * 0.85, radius, d)) * exists;
                return float3(rimLine, shine, inside);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                if (r >= 1.0)
                {
                    return half4(0, 0, 0, 0);
                }

                float arms = max(_Params.x, 1.0);
                float twist = _Params.y;
                float sharp = max(_Params.z, 0.1);
                float turb = _Params.w;
                float rotation = _Params2.x;
                float opacity = _Params2.z;
                float intensity = _Params2.w;
                float time = _Params3.x;
                float rim = _Params3.y;
                float dir = _Params3.z >= 0.0 ? 1.0 : -1.0;
                float suck = saturate(_Params3.w);
                float inflow = _WaterParams3.z;

                // 消えるときは中心の穴が広がって、まわりが穴へ落ちていく。
                float drain = smoothstep(0.0, 1.0, suck);
                float eye = lerp(_Params2.y, max(_Params2.y, _WaterParams2.z), drain);

                // 中心に近いほど強くねじる（うず巻き）。消えるときは中心へ吸い込むようにさらにねじる。
                float inner = 1.0 - r;
                float twirl = dir * (twist * inner * inner + twist * 0.35 * inner + suck * 6.0 * inner) + rotation;
                float2 q = BlueSkillRotate(p, twirl);
                float a = atan2(q.y, q.x);
                float logR = log(max(r, 0.02));

                // ── 元の WaterShader の「ゆがみの絵2枚を流して足す」を、渦の座標（角度・対数の距離）でやる ──
                // 角度方向 = 回る流れ（元の横スクロール）、距離方向 = 中心へ吸い込まれる流れ（流れ込んだ量は CPU で積算）。
                // atan2 のつなぎ目でミップが飛ばないよう、一番細かい段で読む（絵はミップなしで取り込んである）。
                float repeat = max(1.0, round(_WaterParams2.x));
                float2 flow = float2(a / 6.2831853 * repeat, logR * _WaterParams2.y * 0.5 + inflow);
                float2 flowMain = flow + float2(time * _WaterParams.x * dir, 0.0);
                float2 flowDetail = flow * 2.0 + float2(time * _WaterParams.y * dir, 0.37);
                float2 offset = SAMPLE_TEXTURE2D_LOD(_DisplacementTex, sampler_DisplacementTex, flowMain, 0).rg
                              + SAMPLE_TEXTURE2D_LOD(_DisplacementDetailTex, sampler_DisplacementDetailTex, flowDetail, 0).rg;
                float2 wobble = (offset - 0.5) * _WaterParams.z;

                // うず巻きの筋（水の流れでゆがむ）
                float armWave = cos(a * arms + wobble.x * turb * 40.0 + logR * 2.0);
                float armMask = pow(saturate(armWave * 0.5 + 0.5), sharp) * _WaterParams3.y;

                // 色（中心→ふち）を、ゆがみでずらした距離で読む（元はカメラの絵をずらして読んでいた）。
                float rWarp = saturate(r + wobble.y);
                float4 grad = SAMPLE_TEXTURE2D(_RadialTex, sampler_RadialTex, float2(rWarp, 0.5));
                float shade = saturate(offset.x * 0.5);
                float3 color = grad.rgb * lerp(0.55, 1.15, armMask) * lerp(0.8, 1.2, shade);

                // 渦の深さ: 穴へ向かうほど暗い漏斗（消えるときは穴と一緒に広がる）。
                float hollow = saturate(_RingParams.x);
                float wall = smoothstep(eye, eye + 0.5, r);
                color *= lerp(1.0 - saturate(_WaterParams4.w) * (1.0 - 0.7 * hollow), 1.0, wall);

                float eyeMask = smoothstep(eye * 0.6, eye + 0.06, r);
                float edge = 1.0 - smoothstep(0.86, 1.0, r);
                float alpha = saturate(grad.a * opacity * edge);

                // ── うず巻きの溝（排水口へ落ちる水の細い筋）: 対数うず巻き「本数 × 角度 + きつさ × log(距離)」が同じ所が1本の溝 ──
                // 中心へ流れ込むのに合わせて溝も内へ進む。中心のすぐそばは細かくなりすぎるので消す。
                float grooveCount = max(1.0, round(_WaterParams5.x));
                float groovePhase = grooveCount * a + dir * _WaterParams5.y * logR - inflow * 6.2831853 + wobble.x * 18.0;
                float grooveZone = smoothstep(eye, eye + 0.2, r) * (1.0 - smoothstep(0.82, 0.98, r));
                float ridge = sin(groovePhase) * 0.5 + 0.5;
                color *= lerp(1.0, lerp(1.0 - saturate(_WaterParams5.w), 1.0, ridge), grooveZone);
                float grooveShine = pow(saturate(sin(groovePhase + 0.9)), 14.0) * grooveZone * (0.55 + 0.45 * saturate(offset.y * 0.6));

                // ── 泡（元と同じ: ゆがみの2つの値が両方しきい値を超えた所が泡）──
                float foamZone = smoothstep(eye, eye + 0.2, r);
                float foam = step(_WaterParams.w, offset.x - 0.5) * step(_WaterParams.w, offset.y - 0.5) * foamZone;
                // ふちの泡（元は水面の上のふち。ここでは渦のふちで、ゆがみで波打つ）
                float edgeFoam = step(0.86 - _WaterParams2.w * (0.4 + offset.x), r) * edge;
                // 輪にするときは、内側のふちにも波打つ泡を付ける。
                float innerFoam = step(eye, r) * step(r, eye + 0.04 + _WaterParams2.w * (0.4 + offset.y)) * hollow * _RingParams.y;
                foam = saturate(foam + edgeFoam + innerFoam);

                // 光（足し算）: 筋の泡・水面のきらめき・ふちの輪・目のまわりの輪
                float stripeZone = smoothstep(eye, eye + 0.25, r) * (1.0 - smoothstep(0.8, 1.0, r));
                float3 glow = _FoamColor.rgb * armMask * 0.45 * stripeZone * (0.35 + 0.65 * shade);
                float sheen = pow(saturate((offset.x + offset.y) * 0.5 - 0.62) * 2.6, 3.0) * _WaterParams3.w;
                glow += _WaterFoamColor.rgb * sheen * stripeZone;
                glow += _WaterFoamColor.rgb * grooveShine * _WaterParams5.z;
                glow += _RimColor.rgb * Band(r - 0.9, 0.05) * rim * (0.75 + 0.25 * shade);
                glow += _RimColor.rgb * 0.35 * Band(r - 0.9, 0.14) * rim;
                glow += _EyeColor.rgb * Band(r - eye, 0.035) * (0.6 + 0.4 * armMask) * step(0.001, eye);

                float3 rgb = color * alpha * eyeMask + color * alpha * (1.0 - eyeMask) * 0.35;
                rgb += glow * edge;

                // 泡を重ねる（元: 泡の濃さの分だけ白に寄せる）。泡の所は床を塗る濃さも上げる。
                float foamAmount = foam * saturate(_WaterParams3.x);
                float foamAlpha = max(alpha, foamAmount * edge);
                rgb = lerp(rgb, _WaterFoamColor.rgb * foamAlpha, foamAmount);
                alpha = foamAlpha;

                // ── うがい薬の泡（ブクブク）: 水の流れに乗って回りながら中心へ吸い込まれる ──
                float bubbleRepeat = max(3.0, round(_WaterParams4.y));
                float bubbleScale = bubbleRepeat / 6.2831853;
                float2 bubbleCell = float2(a / 6.2831853 * bubbleRepeat + time * _WaterParams.x * dir * bubbleRepeat * 0.5,
                                           logR * bubbleScale + inflow * bubbleScale * 1.3);
                float3 bubble = GargleBubbles(bubbleCell, bubbleRepeat, saturate(_WaterParams4.x), clamp(_WaterParams4.z, 0.05, 0.5));
                float bubbleZone = smoothstep(eye, eye + 0.15, r) * edge;
                float bubbleLight = saturate(bubble.x + bubble.y * 1.5) * bubbleZone;
                rgb = lerp(rgb, rgb * 1.25 + _BubbleColor.rgb * 0.08, bubble.z * bubbleZone);
                rgb += _BubbleColor.rgb * bubbleLight * max(alpha, 0.3);
                alpha = max(alpha, bubbleLight * 0.6);

                // 輪にするときは、目の内側を透明にする（キャラ・ボトルが見えるように）。
                float hollowMask = lerp(1.0, smoothstep(eye - 0.03, eye + 0.05, r), hollow);
                rgb *= hollowMask;
                alpha *= hollowMask;

                rgb *= intensity;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
