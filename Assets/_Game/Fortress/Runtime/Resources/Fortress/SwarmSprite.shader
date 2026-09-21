// 群衆(Swarm)用のインスタンシング描画シェーダー。
// 全ての敵の位置・大きさ・アニメーションのコマ・向き・被弾フラッシュを、StructuredBuffer(_Instances)から読む。
// スプライトシートは「横=コマ(左→右)、縦=向き(上→下に0,1,2...)」の格子。
Shader "MS2026/Fortress/SwarmSprite"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Cols ("Columns (Frames)", Float) = 10
        _Rows ("Rows (Directions)", Float) = 8
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
            Name "SwarmSprite"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct SwarmInstance
            {
                float4 a; // x, y, size, flash
                float4 b; // frame, direction, unused, unused
            };

            StructuredBuffer<SwarmInstance> _Instances;

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Tint;
                float _Cols;
                float _Rows;
                float _InstanceOffset;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                uint instanceID : SV_InstanceID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float flash : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                SwarmInstance inst = _Instances[input.instanceID + (uint)_InstanceOffset];

                float2 world = inst.a.xy + input.positionOS.xy * inst.a.z;
                output.positionCS = TransformWorldToHClip(float3(world, 0.0));

                float frame = inst.b.x;
                float row = (_Rows - 1.0) - inst.b.y;
                output.uv = float2((input.uv.x + frame) / _Cols, (input.uv.y + row) / _Rows);
                output.flash = inst.a.w;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;
                // 透明なピクセルは書き込まない（重なった大群での無駄なブレンド処理を減らす）。
                clip(color.a - 0.01);
                color.rgb = lerp(color.rgb, half3(1, 1, 1), saturate(input.flash));
                return color;
            }
            ENDHLSL
        }
    }
}
