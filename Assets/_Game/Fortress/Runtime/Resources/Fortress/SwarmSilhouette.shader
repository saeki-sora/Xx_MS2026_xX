// 群衆(Swarm)の敵のうち、背景オブジェクトの裏に隠れた部分だけを単色の影(シルエット)で描く。
// 背景用シェーダー(MS2026/Stage/Prop)がステンシルに書いた印(64)の所で、背景より奥にある敵の画素だけを塗る。
// SwarmRenderer は、グローバル値 _FortressSilhouetteEnabled が 0 より大きいとき(ステージ背景スタジオの「隠れた敵を影で見せる」がON)だけ
// このシェーダーで2回目を描く。0(既定・背景の無いシーン)のときは描画そのものをしないので負荷は増えない。
// 位置の計算は本体(SwarmSprite.shader)と同じ SwarmSpriteCommon.hlsl を使う。
Shader "MS2026/Fortress/SwarmSilhouette"
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
            Name "SwarmSilhouette"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Greater
            Cull Off

            Stencil
            {
                Ref 64
                ReadMask 64
                Comp Equal
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragSilhouette
            #pragma target 4.5

            #include "Assets/_Game/Fortress/Runtime/Swarm/Shaders/SwarmSpriteCommon.hlsl"

            // ステージ側(StageLookProfile)が設定するグローバル値。
            half4 _FortressSilhouetteColor;

            half4 fragSilhouette(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a * _Tint.a;
                clip(alpha - 0.3);
                return _FortressSilhouetteColor;
            }
            ENDHLSL
        }
    }
}
