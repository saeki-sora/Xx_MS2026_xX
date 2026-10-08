using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「砲台配置」タブの「演出（エフェクト・効果音）」。レーザーの3つの演出欄（発射口・着弾点・群衆の敵に当たった瞬間）と、
    /// 群衆ヒットの数の上限（群衆の設定に保存）、試し再生に使う砲台を並べる。各欄の中身は <see cref="FortressEffectDrawer"/> が描く。
    /// </summary>
    public static class LaserEffectsPanel
    {
        private static readonly string[] PlayerLabels = { "P1", "P2", "P3", "P4" };

        private static SerializedObject _tuningSerialized;
        private static SerializedObject _swarmSerialized;

        public static void Draw(LaserTuningConfig tuning)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("演出（エフェクト・効果音）", EditorStyles.miniBoldLabel);
            EditorGUILayout.HelpBox(
                "素材は D-Drive の番号札で選びます（エフェクト＝VFX、音＝SE）。エフェクトの Prefab を " +
                "Assets/_Game/DDrive/SourceAssets/Vfx/<分類>/ に置くと、自動で VFX として登録されます。" +
                "マルチプレイでも各PCがそれぞれ鳴らすので、全員の画面に出ます。各欄は ▶ をクリックすると開きます。",
                MessageType.None);

            if (_tuningSerialized == null || _tuningSerialized.targetObject != tuning)
            {
                _tuningSerialized = new SerializedObject(tuning);
            }

            _tuningSerialized.Update();
            DrawSlot("effects.muzzle", "発射口（撃っている間）");
            DrawSlot("effects.impact", "着弾点（壁などに当たっている間）");
            DrawSlot("effects.swarmHit", "群衆の敵に当たった瞬間");
            DrawSlot("effects.steam", "撃ったあとの湯気（撃った時間ぶん続く）");
            _tuningSerialized.ApplyModifiedProperties();

            DrawSwarmBudget();

            FortressEffectPreview.PreferredPlayer = EditorGUILayout.Popup(
                new GUIContent("「▶ 試す」に使う砲台", "Play中に「▶ 試す」を押したとき、どのプレイヤーの砲台の発射口で鳴らすか。"),
                Mathf.Clamp(FortressEffectPreview.PreferredPlayer, 0, PlayerLabels.Length - 1),
                PlayerLabels);

            DrawTips();
        }

        private static void DrawSlot(string path, string label)
        {
            var property = _tuningSerialized.FindProperty(path);
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent(label, property.tooltip), true);
            }
        }

        private static void DrawSwarmBudget()
        {
            var swarm = Object.FindFirstObjectByType<SwarmSystem>();
            if (swarm == null || swarm.settings == null)
            {
                EditorGUILayout.LabelField(
                    new GUIContent("群衆ヒットの上限：1秒に60個（既定）",
                        "シーンに群衆の設定アセットが無いため既定値で動きます。変えるときは「群衆」タブで設定アセットを作成してください。"),
                    EditorStyles.miniLabel);
                return;
            }

            if (_swarmSerialized == null || _swarmSerialized.targetObject != swarm.settings)
            {
                _swarmSerialized = new SerializedObject(swarm.settings);
            }

            _swarmSerialized.Update();
            var property = _swarmSerialized.FindProperty(nameof(SwarmSettings.hitEffectsPerSecond));
            if (property != null)
            {
                EditorGUILayout.PropertyField(property, new GUIContent("群衆ヒットの上限（個/秒・全員合計）", property.tooltip));
            }

            _swarmSerialized.ApplyModifiedProperties();
        }

        private static void DrawTips()
        {
            var key = "Fortress.LaserEffects.Tips";
            var open = SessionState.GetBool(key, false);
            open = EditorGUILayout.Foldout(open, "エフェクト（VFX）を作るときのコツ", true);
            SessionState.SetBool(key, open);
            if (!open)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "・寿命（Life Mode）：発射口・着弾点は「Loop」（撃っている間/当たっている間だけ出続け、終わると止まる）。" +
                "群衆ヒットは「OneShot」か「Duration」。\n" +
                "・向き：エフェクトの上方向(Y+)が、発射口ではレーザーの向き、着弾点と群衆ヒットでは砲台の方を向いて出ます。" +
                "出した後も砲台の回転に合わせたいときは、VFX Editor の「出す位置」で Follow Rotation を ON。\n" +
                "・プレイヤー色：VFX Editor の Params に「Color」という名前の色の項目を作ると、P1赤・P2青…の色が自動で入ります。\n" +
                "・ネット設定：VFX の Flags › Net は「Local」のままにしてください（各PCが自分で鳴らすため。Cosmeticにすると二重に出ます）。\n" +
                "・音：同時に鳴る数や連打の間引きは、SE の Max Concurrent / Cooldown Sec で調整します。",
                MessageType.None);
        }
    }
}
