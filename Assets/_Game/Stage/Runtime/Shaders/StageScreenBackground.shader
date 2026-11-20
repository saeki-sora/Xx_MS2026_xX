// 画面いっぱいの背景画像（StageScreenBackground が使う）。
// ・頂点の位置は無視して、画面全体を覆う四角を直接描く（カメラの位置・揺れ・ズームに関係なく、いつも画面ぴったり）。
// ・ライトは当てない。画像の色がそのまま出る（後処理のブルームなどは掛かる）。
// ・描く順は一番先（Queue=Background）。深度は「一番奥の少し手前」を書くので、
//   床・障害物・敵など後から描く物はすべて手前に乗り、Skybox だけがこの背景に隠れる。
// ・UV の範囲（_UvRect）は C# が画面と画像の縦横比から計算して渡す（はみ出た分を切る／伸ばす）。
Shader "MS2026/Stage/ScreenBackground"
{
    Properties
    {
        [MainTexture] _BaseMap("背景の画像", 2D) = "black" {}
        [MainColor] _BaseColor("色の掛け算", Color) = (1,1,1,1)
        [HideInInspector] _UvRect("UV の範囲（内部用: 拡大 xy・ずらし zw）", Vector) = (1,1,0,0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "ScreenBackground"
            Tags { "LightMode" = "UniversalForwardOnly" }

            ZWrite On
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float4 _UvRect;
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

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float2 p = input.uv * 2.0 - 1.0;

                // 一番奥（far）の少し手前。far ちょうどだと Skybox が上から描かれてしまう。
            #if UNITY_REVERSED_Z
                float z = 1e-5;
            #else
                float z = 1.0 - 1e-5;
            #endif

                // 中間テクスチャに描くときは上下が反転した投影になるので、それに合わせる。
                output.positionCS = float4(p.x, p.y * _ProjectionParams.x, z, 1.0);
                output.uv = input.uv * _UvRect.xy + _UvRect.zw;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
            }
            ENDHLSL
        }
    }
}
