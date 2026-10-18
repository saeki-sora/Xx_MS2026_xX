// 群衆(Swarm)の敵の足元の影。全ての敵を1回の描画でまとめて描く(SwarmRenderer が本体より先に描く)。
// 敵のデータ(_Instances)と、絵を立たせる計算の考え方は本体(SwarmSprite.shader)と同じ SwarmSpriteCommon.hlsl を使う。
// ・絵を立たせているとき: 足元(絵の下端が地面に付く所)に、地面に寝た楕円の影を落とす。
// ・寝かせたまま(真上視点など)のとき: 絵の真下(中心)に影を置く。
// 影の大きさ・濃さは群衆の設定「足元の影」(SwarmSettings)から SwarmRenderer が入れる。
Shader "MS2026/Fortress/SwarmShadow"
{
    Properties
    {
        _ShadowSize ("Shadow Size (絵の大きさに対する割合)", Float) = 0.75
        _ShadowDarkness ("Shadow Darkness", Float) = 0.4
        _InstanceOffset ("Instance Offset", Float) = 0
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
            Name "SwarmShadow"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma target 4.5

            #include "Assets/_Game/Fortress/Runtime/Swarm/Shaders/SwarmSpriteCommon.hlsl"

            float _ShadowSize;
            float _ShadowDarkness;

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0; // -1〜1（中心0）
            };

            ShadowVaryings vertShadow(Attributes input)
            {
                ShadowVaryings output;
                SwarmInstance inst = _Instances[input.instanceID + (uint)_InstanceOffset];
                float size = inst.a.z;

                float2 groundUp = FortressGroundUp();
                float2 groundRight = float2(groundUp.y, -groundUp.x);

                // 本体と同じ「絵の下端」を求める（SwarmSpriteCommon の vert と同じ並べ方）。
                float2 spriteUp = SwarmSpriteUp();
                float2 spriteRight = float2(spriteUp.y, -spriteUp.x);
                float bottom = -(abs(dot(spriteRight * (0.5 * size), groundUp)) + abs(dot(spriteUp * (0.5 * size), groundUp)));
                float standing = saturate(_FortressSwarmBillboardStand * 4.0);
                float2 center = inst.a.xy + groundUp * (bottom * _FortressBillboardFeet * standing);

                // 横長の楕円（画面の横方向に広い）。
                float radiusX = 0.5 * size * _ShadowSize;
                float radiusY = radiusX * 0.55;
                float2 offset = groundRight * (input.positionOS.x * 2.0 * radiusX) + groundUp * (input.positionOS.y * 2.0 * radiusY);

                // カメラは +Z を向いているので、床(z=0)より少しだけ手前(-Z)に置いてちらつきを防ぐ。
                output.positionCS = TransformWorldToHClip(float3(center + offset, -0.01));
                output.local = input.positionOS.xy * 2.0;
                return output;
            }

            half4 fragShadow(ShadowVaryings input) : SV_Target
            {
                float d = length(input.local);
                half alpha = (1.0 - smoothstep(0.35, 1.0, d)) * _ShadowDarkness;
                clip(alpha - 0.003);
                return half4(0.0, 0.0, 0.0, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
