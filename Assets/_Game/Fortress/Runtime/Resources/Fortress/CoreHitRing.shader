// コアがダメージを受けた瞬間に広がる、警告の光の輪（CoreHitRings がダメージごとに1枚、地面に寝た四角形として置く。加算）。
// 輪は内側の半径(_Inner)から外側(1)へ広がりながら消える。値は MaterialPropertyBlock で入れる。
Shader "MS2026/Fortress/CoreHitRing"
{
    Properties
    {
        _Progress ("Progress (0〜1)", Float) = 0.5
        _Inner ("Inner (広がり始める半径。四角形の半分を1とする)", Float) = 0.2
        _Width ("Width (輪の太さ。四角形の半分を1とする)", Float) = 0.06
        _Color ("Color (rgb=色, a=強さ)", Color) = (1, 0.2, 0.15, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "CoreHitRing"
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float _Inner;
                float _Width;
                half4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 local : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.local = input.positionOS.xy * 2.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = saturate(_Progress);
                // 速く広がって、ゆっくり止まる。
                float grow = 1.0 - (1.0 - t) * (1.0 - t);
                float radius = lerp(_Inner, 0.97, grow);
                float r = length(input.local);
                float width = _Width * (1.0 + grow);
                float ring = 1.0 - saturate(abs(r - radius) / max(width, 1e-3));
                ring *= ring;
                // 輪の内側にうっすら赤い面を残す（広がった跡）。
                float fill = r < radius ? 0.12 * (r / max(radius, 1e-3)) : 0.0;
                float fade = (1.0 - t) * (1.0 - t);
                half3 color = _Color.rgb * ((ring + fill) * fade * _Color.a);
                return half4(color, 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
