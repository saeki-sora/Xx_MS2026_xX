// 地面に寝ているSpriteRendererの絵を、描画中のカメラに向けて立たせるシェーダー。
// BillboardDirector が対象のSpriteRendererにだけ実行時に差し替える(OFFにすれば元のマテリアルに戻る)。
// 色(SpriteRenderer.color)・反転(flipX/Y)・並び順(Sorting Order)は通常のスプライトと同じように効く。
// 立たせた絵同士が正しく前後に重なるよう、半透明の縁は切り落として深度を書く。
Shader "MS2026/Fortress/BillboardSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _BillboardBounds ("Bounds (center xy, extents zw)", Vector) = (0, 0, 0.5, 0.5)
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "TransparentCutout"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
            // 頂点を「物体の基準点からのずれ」として扱うため、複数の絵を1つにまとめる描画最適化を止める。
            "DisableBatching" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite On

        Pass
        {
            Name "BillboardSprite"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Assets/_Game/Fortress/Runtime/Billboard/Shaders/FortressBillboard.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // グローバル値(BillboardShaderGlobals)。マテリアル単位の値ではないのでCBUFFERの外に置く。
            float _FortressBillboardStand;
            float _FortressBillboardCutoff;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _BillboardBounds;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3x3 linearPart = (float3x3)objectToWorld;
                float3 pivotWS = float3(objectToWorld._m03, objectToWorld._m13, objectToWorld._m23);

                // 物体の回転・拡大を反映した「地面上のずれ」と、絵の外形(足元を求めるため)をワールドで求める。
                float2 offsetWS = mul(linearPart, positionOS).xy;
                float2 boundsCenterWS = mul(linearPart, float3(_BillboardBounds.xy * unity_SpriteProps.xy, 0.0)).xy;
                float2 boundsAxisXWS = mul(linearPart, float3(_BillboardBounds.z, 0.0, 0.0)).xy;
                float2 boundsAxisYWS = mul(linearPart, float3(0.0, _BillboardBounds.w, 0.0)).xy;

                float3 positionWS = pivotWS + FortressBillboardOffset(offsetWS, boundsCenterWS, boundsAxisXWS, boundsAxisYWS, _FortressBillboardStand);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                clip(color.a - _FortressBillboardCutoff);
                return color;
            }
            ENDHLSL
        }
    }
}
