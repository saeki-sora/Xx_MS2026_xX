// 青スキルの「水の渦」。四角い板1枚に、うず巻きの筋・水のゆらぎ・中心の暗い目・光るふちをコードの数値だけで描く（画像を使わない）。
// 床を青く塗る部分（アルファ）と、筋・ふち・目の輪の光（足し算）を、プリマルチプライドの1回の描画で出す。
// 数値は BlueVortexVisual が渡す。
// _RadialTex: 中心→ふちの色（横1列、HDR。アルファ=濃さ）
// _Params : x=うず巻きの本数, y=ねじれ, z=筋のくっきり具合, w=水のゆらぎ
// _Params2: x=回転(ラジアン、CPUで積算), y=中心の目の大きさ(0〜1), z=濃さ, w=明るさ
// _Params3: x=時間(秒), y=ふちの明るさ(点滅), z=回る向き(+1/-1), w=中心への吸い込み(0〜1、消えるとき)
Shader "MS2026/BlueSkill/Vortex"
{
    Properties
    {
        _RadialTex ("Radial Gradient", 2D) = "white" {}
        [HDR] _FoamColor ("Foam Color", Color) = (0.75, 1.25, 1.9, 1)
        [HDR] _RimColor ("Rim Color", Color) = (0.35, 1.1, 2.4, 1)
        [HDR] _EyeColor ("Eye Ring Color", Color) = (1.6, 1.9, 2.6, 1)
        _Params ("Arms, Twist, Sharpness, Turbulence", Vector) = (4, 4.5, 2.6, 0.45)
        _Params2 ("Rotation, Eye Size, Opacity, Intensity", Vector) = (0, 0.17, 0.85, 1.2)
        _Params3 ("Time, Rim, Direction, Suck", Vector) = (0, 1, 1, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Pass
        {
            Name "BlueSkillVortex"
            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "BlueSkillCommon.hlsl"

            TEXTURE2D(_RadialTex);
            SAMPLER(sampler_RadialTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _RadialTex_ST;
                half4 _FoamColor;
                half4 _RimColor;
                half4 _EyeColor;
                float4 _Params;
                float4 _Params2;
                float4 _Params3;
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
                float eye = _Params2.y;
                float opacity = _Params2.z;
                float intensity = _Params2.w;
                float time = _Params3.x;
                float rim = _Params3.y;
                float dir = _Params3.z >= 0.0 ? 1.0 : -1.0;
                float suck = saturate(_Params3.w);

                // 中心に近いほど強くねじる（うず巻き）。消えるときは中心へ吸い込むようにさらにねじる。
                float inner = 1.0 - r;
                float twirl = dir * (twist * inner * inner + twist * 0.35 * inner + suck * 6.0 * inner) + rotation;
                float2 q = BlueSkillRotate(p, twirl);

                // 水のゆらぎ（ねじった座標で取るので、角度のつなぎ目が出ない）
                float n = BlueSkillFbm(q * 3.2 + float2(time * 0.35, -time * 0.21));
                float n2 = BlueSkillFbm(q * 7.5 - float2(time * 0.6, time * 0.4));

                // うず巻きの筋
                float a = atan2(q.y, q.x);
                float armWave = cos(a * arms + (n - 0.5) * turb * 6.0 + r * 3.0);
                float armMask = pow(saturate(armWave * 0.5 + 0.5), sharp);
                float thin = pow(saturate(cos(a * arms * 2.0 + (n2 - 0.5) * turb * 8.0) * 0.5 + 0.5), sharp * 3.0);

                // 色（中心→ふち）
                float4 grad = SAMPLE_TEXTURE2D(_RadialTex, sampler_RadialTex, float2(saturate(r), 0.5));
                float3 color = grad.rgb * lerp(0.5, 1.15, armMask) * lerp(0.85, 1.15, n);

                // 中心へ流れ込む細かい波（同心円）
                float ripple = sin(r * 22.0 + time * 5.0 + n * 4.0) * 0.5 + 0.5;
                color *= lerp(0.9, 1.1, ripple * smoothstep(eye, eye + 0.3, r));

                float eyeMask = smoothstep(eye * 0.6, eye + 0.06, r);
                float edge = 1.0 - smoothstep(0.86, 1.0, r);
                float alpha = saturate(grad.a * opacity * edge);

                // 光（足し算）: 筋の泡・細いしぶき・ふちの輪・目のまわりの輪
                float foamZone = smoothstep(eye, eye + 0.25, r) * (1.0 - smoothstep(0.8, 1.0, r));
                float3 glow = _FoamColor.rgb * (armMask * 0.55 + thin * 0.45) * foamZone * (0.35 + 0.65 * n2);
                glow += _RimColor.rgb * Band(r - 0.9, 0.05) * rim * (0.75 + 0.25 * n);
                glow += _RimColor.rgb * 0.35 * Band(r - 0.9, 0.14) * rim;
                glow += _EyeColor.rgb * Band(r - eye, 0.035) * (0.6 + 0.4 * armMask) * step(0.001, eye);

                float3 rgb = color * alpha * eyeMask + color * alpha * (1.0 - eyeMask) * 0.35;
                rgb += glow * edge;
                rgb *= intensity;
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
