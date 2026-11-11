// 群衆(Swarm)用のインスタンシング描画シェーダー。
// 全ての敵の位置・大きさ・アニメーションのコマ・向き・被弾フラッシュを、StructuredBuffer(_Instances)から読む。
// スプライトシートは「横=コマ(左→右)、縦=向き(上→下に0,1,2...)」の格子。
// ビルボード(カメラタブの「絵を立たせる」)がONのときは、グローバル値 _FortressSwarmBillboardStand に従って
// 地面に寝た四角形をカメラに向けて立たせる。0のときは従来と全く同じ描画になる。
// 近くを撃っているビームがあれば、その色でほんのり照らす（FortressBeamLight.hlsl の FortressBeamEnemyLight）。
Shader "MS2026/Fortress/SwarmSprite"
{
    Properties
    {
        _MainTex ("Sprite Sheet", 2D) = "white" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Cols ("Columns (Frames)", Float) = 10
        _Rows ("Rows (Directions)", Float) = 8
        _InstanceOffset ("Instance Offset", Float) = 0
        _BlinkColor ("Blink Color (爆弾の点滅)", Color) = (1,0.15,0.1,1)
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
            #include "Assets/_Game/Fortress/Runtime/Look/EdgeMist/Shaders/FortressEdgeMist.hlsl"

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Tint;
                // 透明なピクセルは書き込まない（重なった大群での無駄なブレンド処理を減らす）。
                // 立たせているときは、深度を書くので半透明の縁を切り落とす(縁が奥の敵を四角く隠さないように)。
                clip(color.a - (_FortressSwarmBillboardStand > 0.0 ? _FortressBillboardCutoff : 0.01));
                // ビームの光（近くのビームの色で照らす）。暗い色の絵でも分かるよう、光の色そのものも「光の色を乗せる量」だけ足す。
                // 群れのうねり: 混んだ所を少し暗くして立体感を出し、白いつやの波を流す（SwarmSpriteCommon.hlsl の SwarmCrowdWave）。
                color.rgb *= 1.0 - input.crowd.x * _FortressCrowdSheen.y;
                color.rgb += _FortressCrowdSheenColor.rgb * (input.crowd.x * input.crowd.y * _FortressCrowdSheen.x);
                // 敵の質感: 絵の縁を濃い色に寄せて、明るい床の上でも形（シルエット）がはっきり読めるようにする（_FortressEnemyLook.w）。
                if (_FortressEnemyLook.w > 0.0 && _FortressEnemyEdge.a > 0.0)
                {
                    float2 edgeCell = float2(1.0 / _Cols, 1.0 / _Rows) * _FortressEnemyEdge.a;
                    half n0 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2(edgeCell.x, 0.0), 0).a;
                    half n1 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv - float2(edgeCell.x, 0.0), 0).a;
                    half n2 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2(0.0, edgeCell.y), 0).a;
                    half n3 = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv - float2(0.0, edgeCell.y), 0).a;
                    half edge = saturate((color.a - min(min(n0, n1), min(n2, n3))) * 2.0);
                    color.rgb = lerp(color.rgb, _FortressEnemyEdge.rgb, edge * _FortressEnemyLook.w);
                }
                color.rgb = color.rgb * (1.0 + input.beamLight) + input.beamLight * _FortressBeamEnemyOverlay;
                // 画面のふちの靄の中だけ、左上と右上の縁に冷たい光を付けて、暗い靄の中でも敵の形が浮かぶようにする（FortressEdgeMist.hlsl）。
                if (_FortressEdgeMistCore.w > 0.5 && _FortressEdgeMistRimWidth > 0.0)
                {
                    half mist = FortressEdgeMistAmount(GetNormalizedScreenSpaceUV(input.positionCS));
                    if (mist > 0.001h)
                    {
                        float2 cell = float2(1.0 / _Cols, 1.0 / _Rows) * _FortressEdgeMistRimWidth;
                        half upLeft = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2(-cell.x, cell.y), 0).a;
                        half upRight = SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, input.uv + float2(cell.x, cell.y), 0).a;
                        half rim = saturate(color.a - min(upLeft, upRight));
                        color.rgb += _FortressEdgeMistRimColor.rgb * (rim * mist);
                    }
                }
                // 爆弾の敵の点滅（SwarmBombMath）。被弾フラッシュ(白)はその上にかける。
                color.rgb = lerp(color.rgb, _BlinkColor.rgb, saturate(input.blink));
                color.rgb = lerp(color.rgb, half3(1, 1, 1), saturate(input.flash));
                return color;
            }
            ENDHLSL
        }
    }
}
