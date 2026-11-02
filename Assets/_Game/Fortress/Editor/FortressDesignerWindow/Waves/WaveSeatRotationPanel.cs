using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの「全体」ページの「席替え」の欄（<see cref="WavePlan.seatRotation"/>。2026-10-31）。
    /// 上に「席替えする」「回る向き」と流れの説明、折りたたみの中にジャンプ・演出（2026-11-02 の見本帳の番号）の細かい値と、効果音・エフェクトの欄。
    /// Play 中は今の席（だれがどの席か）も出す。
    /// </summary>
    public sealed class WaveSeatRotationPanel
    {
        // 欄はデータの並び順（見出しはデータ側の [Header] がそのまま出る）。3つ目は「この欄が ON のときだけ触れる」チェック（無ければ null）。
        // 番号は演出の見本帳（2026-11-02 ユーザー決定）の番号。
        private static readonly (string path, string label, string needs)[] DetailFields =
        {
            ("jumpSeconds", "跳ぶ秒数", null),
            ("jumpHeight", "浮く高さ(m)", null),
            ("jumpScale", "浮いたときに大きく見せる量", null),
            ("landSquash", "着地でつぶれる量", null),
            ("landSquashSeconds", "つぶれが戻る秒数", null),
            ("anticipation", "1. 溜め（しゃがんでブルブル）", null),
            ("anticipationSeconds", "溜めの秒数", "anticipation"),
            ("crouchAmount", "しゃがむ量", "anticipation"),
            ("crouchShake", "震える幅(m)", "anticipation"),
            ("launchStretch", "跳ぶ瞬間に縦へ伸びる量", "anticipation"),
            ("showPathPreview", "2. 行き先の点線", null),
            ("showDirectionRing", "3. コアのまわりを回る矢印の輪", null),
            ("showBanner", "4. 「席替え！」の帯", null),
            ("bannerText", "帯の大きい文字", "showBanner"),
            ("bannerSubText", "帯の小さい文字（空なら回る向きから）", "showBanner"),
            ("bannerFont", "帯の文字のフォント（空なら標準）", "showBanner"),
            ("darkenEdges", "5. 画面の端を暗く", null),
            ("darkenAmount", "暗さ", "darkenEdges"),
            ("showShadow", "6. 地面の影", null),
            ("showTrail", "8. 光の尾", null),
            ("spinInAir", "9. 空中で回る", null),
            ("spinTurns", "回る回数", "spinInAir"),
            ("hangAtApex", "10. 頂点でふわっと止まる", null),
            ("hangSeconds", "ふわっとの分、足す秒数", "hangAtApex"),
            ("hangPower", "止まる強さ", "hangAtApex"),
            ("cameraPullBack", "11. カメラを引きながら回す", null),
            ("cameraPullAmount", "引く量", "cameraPullBack"),
            ("speedLines", "12. 画面の端に流れる線", null),
            ("cameraDelaySeconds", "カメラが動き出すまで(秒)", null),
            ("cameraBlendSeconds", "カメラが次の席へ移る秒数", null),
            ("shakeOnLand", "着地で自分の画面を揺らす", null),
            ("landShake", "着地の揺れ", "shakeOnLand"),
            ("staggerLanding", "13. 4基が順番に着地（ドドドドン）", null),
            ("staggerSeconds", "1基ごとにずらす秒数", "staggerLanding"),
            ("shockwave", "14. 衝撃波の輪", null),
            ("shockwaveRadius", "輪の大きさ(m)", "shockwave"),
            ("hitStop", "17. ヒットストップ（一瞬止まる）", null),
            ("hitStopSeconds", "止まる秒数（着地1回ごと）", "hitStop"),
            ("floorWave", "19. 床に光の波", null),
            ("floorTileSize", "光るタイルの大きさ(m)", "floorWave"),
            ("sparkle", "20. キラッ", null),
            ("showYouMarker", "着地した後に「YOU」の矢印を出す", null),
            ("youMarkerSeconds", "締めの秒数（YOU の矢印・光の柱など）", null),
            ("youPillar", "21. YOU の光の柱（矢印が弾んで落ちる）", null),
            ("gatherToCore", "22. 4色の光がコアへ集まる", null),
            ("okMarks", "23. 頭の上に OK マーク", null),
        };

        private static readonly (string path, string label)[] EffectFields =
        {
            ("onAnticipate", "溜め始め"),
            ("onJump", "跳び始め"),
            ("onLand", "着地（4基それぞれ鳴る）"),
            ("onSparkle", "キラッ"),
            ("onGather", "光がコアに届いた"),
            ("onOk", "OK マーク（4基それぞれ）"),
        };

        private bool _detailsFoldout;
        private bool _effectsFoldout;

        public void Draw(SerializedObject serialized)
        {
            var root = serialized.FindProperty("seatRotation");
            if (root == null)
            {
                return;
            }

            using (new WaveLabelWidth())
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("席替え", EditorStyles.boldLabel);
                var enabled = root.FindPropertyRelative("enabled");
                Prop(root, "enabled", "ウェーブごとに席を回す");
                using (new EditorGUI.DisabledScope(!enabled.boolValue))
                {
                    Prop(root, "direction", "回る向き");
                }

                EditorGUILayout.LabelField(
                    enabled.boolValue
                        ? "合間（歯みがきタイムなど）が終わると、全員の砲台が1つ隣の席へ跳んで移ります。カメラも自分の砲台に付いていきます。" +
                          $"溜め → ジャンプ → 順番に着地 → 締め（YOU の矢印・光の柱・OK マーク）が終わってから、次の WAVE の発表が始まります（席替えの段階 {SeatChangeSeconds(serialized):0.#} 秒）。" +
                          "4回で一周して元の席に戻ります。席の並び（時計回りの順）は、ステージの砲台の目印（Turret_P1〜P4）とコアの場所から自動で決まります。"
                        : "席替えしません（全員、最初の席のまま）。Play 中の欄の「今すぐ1つ席替え」は OFF でも使えます。",
                    EditorStyles.wordWrappedMiniLabel);

                DrawLiveSeats();

                _detailsFoldout = EditorGUILayout.Foldout(_detailsFoldout, "ジャンプ・跳ぶ前・跳んでいる間・着地・締めの細かい値（番号は演出の見本帳）", true);
                if (_detailsFoldout)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var (path, label, needs) in DetailFields)
                        {
                            var off = needs != null && root.FindPropertyRelative(needs) is { boolValue: false };
                            using (new EditorGUI.DisabledScope(off))
                            using (new EditorGUI.IndentLevelScope(needs != null ? 1 : 0))
                            {
                                Prop(root, path, label);
                            }
                        }
                    }
                }

                _effectsFoldout = EditorGUILayout.Foldout(_effectsFoldout, "席替えの効果音・エフェクト（D-Drive の番号札。VFX は砲台ごと。SE は跳ぶ・キラッは自分の砲台で1回）", true);
                if (_effectsFoldout)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var (path, label) in EffectFields)
                        {
                            Prop(root, path, label);
                        }
                    }
                }
            }
        }

        // Play 中: 何回回したかと、だれがどの席か。
        private static void DrawLiveSeats()
        {
            if (!Application.isPlaying || !FortressWaves.IsActive)
            {
                return;
            }

            var text = $"今: {FortressSeats.Shift} 回目の席";
            for (var p = 0; p < FortressSeats.SeatCount; p++)
            {
                text += $" ・ {p + 1}P → 席 {FortressSeats.SeatOf(p) + 1}";
            }

            if (FortressSeats.IsMoving)
            {
                text += "（跳んでいる）";
            }

            EditorGUILayout.LabelField(text, EditorStyles.miniBoldLabel);
        }

        private static float SeatChangeSeconds(SerializedObject serialized) =>
            serialized.targetObject is WavePlan plan && plan.seatRotation != null ? plan.seatRotation.WaveSeconds : 0f;

        private static void Prop(SerializedProperty root, string path, string label)
        {
            var property = root.FindPropertyRelative(path);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }
    }
}
