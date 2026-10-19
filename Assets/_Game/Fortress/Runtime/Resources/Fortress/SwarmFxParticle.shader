// 群衆の演出用の光の粒（SwarmFxParticles が1回の描画でまとめて描く。加算）。
// ・粒(kind 0): 倒した敵から散って砲台へ飛ぶ光。速さに合わせて進む向きに伸びる。
// ・閃光(kind 1): 倒した瞬間・爆弾の爆発の、丸く広がって消える光。
// 粒のデータ(_FxParticles)は C# で毎フレーム作る。地面(z=0)より少し手前(-Z)に、地面に寝た四角形として描く。
Shader "MS2026/Fortress/SwarmFxParticle"
{
    Properties
    {
        _Stretch ("Stretch (速さ1メートル/秒あたりの伸び)", Float) = 0.035
        _Lift ("Lift (地面からの高さ。-Zが上)", Float) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "SwarmFxParticle"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct FxParticle
            {
                float4 a; // x, y, 大きさ, 濃さ
                float4 b; // r, g, b, 種類(0=粒 1=閃光)
                float4 c; // 速度x, 速度y, 予備, 予備
            };

            StructuredBuffer<FxParticle> _FxParticles;

            CBUFFER_START(UnityPerMaterial)
                float _Stretch;
                float _Lift;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
                half3 color : TEXCOORD1;
                half kind : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                FxParticle p = _FxParticles[input.instanceID];
                float kind = p.b.w;
                float size = p.a.z;

                float2 velocity = p.c.xy;
                float speed = length(velocity);
                float2 dir = speed > 0.01 ? velocity / speed : float2(0.0, 1.0);
                float2 side = float2(-dir.y, dir.x);
                float length2 = kind < 0.5 ? size + speed * _Stretch : size;

                float2 offset = dir * (input.positionOS.y * length2) + side * (input.positionOS.x * size);
                output.positionCS = TransformWorldToHClip(float3(p.a.xy + offset, -_Lift));
                output.local = input.positionOS.xy * 2.0;
                output.color = p.b.rgb * p.a.w;
                output.kind = kind;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float d = length(input.local);
                half glow = saturate(1.0 - d);
                glow *= glow;
                // 芯は白っぽく光らせる（粒は芯を強め、閃光はやわらかく）。
                half coreWidth = input.kind < 0.5 ? 2.4 : 1.6;
                half core = saturate(1.0 - d * coreWidth);
                core *= core;
                half3 color = input.color * glow + core * 0.7 * max(input.color.r, max(input.color.g, input.color.b));
                return half4(color, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
