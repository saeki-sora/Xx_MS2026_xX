// 群衆(Swarm)用のインスタンシング描画シェーダー。
// 全ての敵の位置・大きさ・アニメーションのコマ・向き・被弾フラッシュを、StructuredBuffer(_Instances)から読む。
// スプライトシートは「横=コマ(左→右)、縦=向き(上→下に0,1,2...)」の格子。
// ビルボード(カメラタブの「絵を立たせる」)がONのときは、グローバル値 _FortressSwarmBillboardStand に従って
// 地面に寝た四角形をカメラに向けて立たせる。0のときは従来と全く同じ描画になる。
Shader "MS2026/Fortress/SwarmSprite"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Cols ("Columns (Frames)", Float) = 10
        _Rows ("Rows (Directions)", Float) = 8
        _InstanceOffset ("Instance Offset", Float) = 0
        [HideInInspector] _SwarmZWrite ("ZWrite (SwarmRendererが設定)", Float) = 0
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
            // 立たせているときだけ深度を書き、手前の敵が奥の敵を正しく隠すようにする(SwarmRendererが切り替える)。
            ZWrite [_SwarmZWrite]
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #include "Assets/_Game/Fortress/Runtime/Swarm/Shaders/SwarmSpriteCommon.hlsl"

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;
                // 透明なピクセルは書き込まない（重なった大群での無駄なブレンド処理を減らす）。
                // 立たせているときは、深度を書くので半透明の縁を切り落とす(縁が奥の敵を四角く隠さないように)。
                clip(color.a - (_FortressSwarmBillboardStand > 0.0 ? _FortressBillboardCutoff : 0.01));
                color.rgb = lerp(color.rgb, half3(1, 1, 1), saturate(input.flash));
                return color;
            }
            ENDHLSL
        }
    }
}
