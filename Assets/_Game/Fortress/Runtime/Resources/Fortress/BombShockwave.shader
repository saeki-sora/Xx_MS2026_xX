// 爆弾の衝撃波（BombShockwaves が爆発ごとに1枚、地面に寝た四角形として置く）。
// 床・背景の物の絵（カメラの不透明テクスチャ _CameraOpaqueTexture）を、広がる輪のところだけ外側へずらして描くので、地面が波打って見える。
// 敵・砲台・コアの絵（半透明）は不透明テクスチャに入っていないので歪まない。そのため群衆の敵より前の描画順(Transparent-20)で描き、
// 敵などはこの上に普通に描かれる。ずらす位置はワールド座標で計算してから画面へ写すので、画面を回した視点でも正しい位置に出る。
// 値（_Progress など）は MaterialPropertyBlock で入れる。
Shader "MS2026/Fortress/BombShockwave"
{
    Properties
    {
        _Progress ("Progress (輪の半径。爆発の半径を1とする)", Float) = 0.5
        _Strength ("Strength (ずらす量。メートル)", Float) = 0.25
        _RingWidth ("Ring Width (輪の太さ。爆発の半径を1とする)", Float) = 0.22
        _Extent ("Extent (四角形の半分の大きさ。爆発の半径を1とする)", Float) = 1.25
        _Tint ("Tint (輪のふちの色と強さ)", Color) = (1, 0.8, 0.5, 0.25)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "BombShockwave"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float _Strength;
                float _RingWidth;
                float _Extent;
                half4 _Tint;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float2 local : TEXCOORD1; // -1〜1（四角形の中心が0。向きはワールドのXYと同じ）
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.local = input.positionOS.xy * 2.0;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = length(input.local) * _Extent;
                float ring = 1.0 - saturate(abs(r - _Progress) / max(_RingWidth, 1e-3));
                ring = ring * ring * (3.0 - 2.0 * ring) * (1.0 - saturate(_Progress));
                clip(ring - 0.002);

                float2 dir = input.local / max(length(input.local), 1e-4);
                float3 displaced = input.positionWS + float3(dir * (ring * _Strength), 0.0);
                float4 screen = ComputeScreenPos(TransformWorldToHClip(displaced));
                half3 color = SampleSceneColor(screen.xy / screen.w);
                color += _Tint.rgb * (ring * _Tint.a);
                return half4(color, saturate(ring * 3.0));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
