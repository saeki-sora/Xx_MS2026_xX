using MS2026.Fortress;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「敵ウェーブ」タブの「全体」ページの「席替え」の欄（<see cref="WavePlan.seatRotation"/>。2026-10-31）。
    /// 上に「席替えする」「回る向き」と流れの説明、折りたたみの中にジャンプ・カメラ・YOU の矢印の細かい値と演出。
    /// Play 中は今の席（だれがどの席か）も出す。
    /// </summary>
    public sealed class WaveSeatRotationPanel
    {
        // 欄はデータの並び順（見出しはデータ側の [Header] がそのまま出る）。
        private static readonly (string path, string label)[] DetailFields =
        {
            ("jumpSeconds", "跳ぶ秒数"),
            ("jumpHeight", "浮く高さ(m)"),
            ("jumpScale", "浮いたときに大きく見せる量"),
            ("landSquash", "着地でつぶれる量"),
            ("landSquashSeconds", "つぶれが戻る秒数"),
            ("cameraDelaySeconds", "カメラが動き出すまで(秒)"),
            ("cameraBlendSeconds", "カメラが次の席へ移る秒数"),
            ("shakeOnLand", "着地で自分の画面を揺らす"),
            ("landShake", "着地の揺れ"),
            ("showYouMarker", "着地した後に「YOU」の矢印を出す"),
            ("youMarkerSeconds", "「YOU」の矢印を出す秒数"),
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
                          $"着地して「YOU」の矢印が消えてから、次の WAVE の発表が始まります（席替えの段階 {SeatChangeSeconds(serialized):0.#} 秒）。" +
                          "4回で一周して元の席に戻ります。席の並び（時計回りの順）は、ステージの砲台の目印（Turret_P1〜P4）とコアの場所から自動で決まります。"
                        : "席替えしません（全員、最初の席のまま）。Play 中の欄の「今すぐ1つ席替え」は OFF でも使えます。",
                    EditorStyles.wordWrappedMiniLabel);

                DrawLiveSeats();

                _detailsFoldout = EditorGUILayout.Foldout(_detailsFoldout, "ジャンプ・カメラ・「YOU」の矢印の細かい値", true);
                if (_detailsFoldout)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        foreach (var (path, label) in DetailFields)
                        {
                            Prop(root, path, label);
                        }
                    }
                }

                _effectsFoldout = EditorGUILayout.Foldout(_effectsFoldout, "席替えの演出（D-Drive の番号札。VFX は砲台ごと、SE は自分の砲台で1回）", true);
                if (_effectsFoldout)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        Prop(root, "onJump", "跳び始め");
                        Prop(root, "onLand", "着地");
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
