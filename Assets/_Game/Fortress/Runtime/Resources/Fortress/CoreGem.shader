// コアクリスタルを宝石のように見せる「上に重ねる層」（CoreGemLook がコアの絵と同じ絵・同じ色で、1つ上の並び順に描く）。
// ・宝石: 絵の形を丸い玉とみなし、後ろの床・背景（カメラの不透明テクスチャ）を屈折させて透かす。ふちの光・つやの点・中でゆらめく光の筋。
// ・脈: 中心からの光がゆっくり脈打つ。HP が減るほど速く、赤くなる。
// ・ひび: HP がしきい値を下回ると、ひびが少しずつ広がり、ひびから赤い光が漏れる。ダメージを受けた瞬間はひびが白く光る。
// 色は SpriteRenderer.color（＝コアの絵の色。CoreDirtVisual で HP に合わせて汚れた色になる）を宝石の色として使う。
// 絵を立たせる（ビルボード）計算は BillboardSprite.shader と同じ（同じグローバル値・同じ外形 _BillboardBounds）なので、元の絵とぴったり重なる。
// 値は CoreGemLook が MaterialPropertyBlock で入れる。
Shader "MS2026/Fortress/CoreGem"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _BillboardBounds ("Bounds (center xy, extents zw)", Vector) = (0, 0, 0.5, 0.5)
        [PerRendererData] _GemGlowColor ("Glow Color", Color) = (0.75, 0.95, 1, 1)
        [PerRendererData] _GemHurtColor ("Hurt Color", Color) = (1, 0.25, 0.15, 1)
        [PerRendererData] _GemCrackColor ("Crack Color", Color) = (1, 0.45, 0.2, 1)
        [PerRendererData] _GemLook ("x=opacity y=refraction z=glow w=rim", Vector) = (0.92, 0.035, 0.6, 0.8)
        [PerRendererData] _GemDetail ("x=highlight y=caustic z=crack width w=unused", Vector) = (0.9, 0.35, 0.06, 0)
        [PerRendererData] _GemState ("x=health y=damage z=crack amount w=hit flash", Vector) = (1, 0, 0, 0)
        [PerRendererData] _GemPulse ("x=calm rate y=hurt rate", Vector) = (0.5, 2.4, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "DisableBatching" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        ZTest LEqual
        Offset -1, -1

        Pass
        {
            Name "CoreGem"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "Assets/_Game/Fortress/Runtime/Billboard/Shaders/FortressBillboard.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _FortressBillboardStand;

            CBUFFER_START(UnityPerMaterial)
                float4 _BillboardBounds;
                half4 _GemGlowColor;
                half4 _GemHurtColor;
                half4 _GemCrackColor;
                float4 _GemLook;
                float4 _GemDetail;
                float4 _GemState;
                float4 _GemPulse;
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
                float2 local : TEXCOORD1; // 絵の外形の中で -1〜1
                UNITY_VERTEX_OUTPUT_STEREO
            };

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
                return lerp(lerp(Hash21(i), Hash21(i + float2(1, 0)), w.x), lerp(Hash21(i + float2(0, 1)), Hash21(i + float2(1, 1)), w.x), w.y);
            }

            // ボロノイ（ひび割れ）: ひびの線までの距離と、その線の場所ごとの乱数。
            float2 CrackEdge(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float d1 = 8.0;
                float d2 = 8.0;
                float2 nearest = 0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 site = offset + Hash22(cell + offset) - f;
                        float d = dot(site, site);
                        if (d < d1)
                        {
                            d2 = d1;
                            d1 = d;
                            nearest = cell + offset;
                        }
                        else if (d < d2)
                        {
                            d2 = d;
                        }
                    }
                }

                return float2(sqrt(d2) - sqrt(d1), Hash21(nearest + 7.7));
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                float4x4 objectToWorld = GetObjectToWorldMatrix();
                float3x3 linearPart = (float3x3)objectToWorld;
                float3 pivotWS = float3(objectToWorld._m03, objectToWorld._m13, objectToWorld._m23);

                float2 offsetWS = mul(linearPart, positionOS).xy;
                float2 boundsCenter = _BillboardBounds.xy * unity_SpriteProps.xy;
                float2 boundsCenterWS = mul(linearPart, float3(boundsCenter, 0.0)).xy;
                float2 boundsAxisXWS = mul(linearPart, float3(_BillboardBounds.z, 0.0, 0.0)).xy;
                float2 boundsAxisYWS = mul(linearPart, float3(0.0, _BillboardBounds.w, 0.0)).xy;

                float3 positionWS = pivotWS + FortressBillboardOffset(offsetWS, boundsCenterWS, boundsAxisXWS, boundsAxisYWS, _FortressBillboardStand);

                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = input.uv;
                output.color = input.color * unity_SpriteColor;
                output.local = (positionOS.xy - boundsCenter) / max(_BillboardBounds.zw, 1e-4);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half mask = tex.a * input.color.a;
                clip(mask - 0.01);

                float t = _Time.y;
                float health = saturate(_GemState.x);
                float damage = saturate(_GemState.y);
                float crackAmount = saturate(_GemState.z);
                float hitFlash = saturate(_GemState.w);

                float2 p = input.local;
                float r2 = saturate(dot(p, p));
                float nz = sqrt(1.0 - r2);
                half3 gemTint = tex.rgb * input.color.rgb;

                // 屈折して透ける後ろの床・背景（玉のふちほど大きく曲がる）。
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                half3 behind = SampleSceneColor(screenUV + p * (_GemLook.y * (1.2 - nz)));
                half3 color = behind * lerp(half3(1, 1, 1), gemTint, 0.75) * 0.8 + gemTint * 0.3;

                // 中心から脈打つ光。HP が減るほど速く、赤く。
                float rate = lerp(_GemPulse.x, _GemPulse.y, damage);
                float pulse = 0.5 + 0.5 * sin(t * rate * 6.2832);
                half3 glowColor = lerp(_GemGlowColor.rgb, _GemHurtColor.rgb, damage);
                float glow = pow(saturate(1.0 - sqrt(r2)), 1.5) * (0.45 + 0.55 * pulse) * _GemLook.z;
                color += glowColor * glow;

                // 中でゆらめく光の筋。
                float c1 = ValueNoise(p * 2.6 + float2(t * 0.15, -t * 0.11));
                float c2 = ValueNoise(p * 4.1 + float2(-t * 0.21, t * 0.17) + 3.1);
                float caustic = pow(saturate(1.0 - abs(c1 - c2) * 4.0), 4.0) * _GemDetail.y * nz;
                color += (gemTint + 0.3) * caustic;

                // ふちの光とつやの点。
                float rim = pow(1.0 - nz, 3.0) * _GemLook.w;
                color += lerp(gemTint, half3(1, 1, 1), 0.5) * rim;
                float2 h = p - float2(-0.35, 0.42);
                color += exp(-dot(h, h) * 22.0) * _GemDetail.x;

                // ひび（HP が減るほど広がる）。ひびから赤い光が漏れ、ダメージの瞬間は白く光る。
                half crack = 0;
                if (crackAmount > 0.001)
                {
                    float2 edge = CrackEdge(p * 3.2 + 11.3);
                    float width = _GemDetail.z * (0.6 + 0.6 * crackAmount);
                    half crackLine = 1.0 - smoothstep(width * 0.5, width, edge.x);
                    // 線ごとの乱数と場所のゆらぎで、少しずつ現れるようにする。
                    half reveal = step(edge.y * 0.6 + ValueNoise(p * 1.7) * 0.4, crackAmount * 1.1);
                    crack = crackLine * reveal;
                    half3 crackLight = _GemCrackColor.rgb * (1.1 + 1.4 * pulse * damage);
                    color = lerp(color, crackLight, crack);
                }

                color += hitFlash * (0.35 + crack * 1.5);

                half alpha = mask * saturate(_GemLook.x + crack * 0.5);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
