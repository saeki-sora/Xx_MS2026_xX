using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「テスト」タブの「コアの見た目」。シーンのコアの <see cref="CoreCrystalController.gemLook"/>（宝石）・
    /// <see cref="CoreCrystalController.hitRing"/>（ダメージの輪）・<see cref="CoreCrystalController.dangerLook"/>（危ないときの画面の色）を並べ、
    /// Play 中は HP を減らさずに見た目だけを試せるボタンを出す。
    /// </summary>
    public static class CoreDangerPanel
    {
        private const string GemFoldoutKey = "Fortress.CoreLook.Gem";
        private const string RingFoldoutKey = "Fortress.CoreLook.Ring";
        private const string DangerFoldoutKey = "Fortress.CoreLook.Danger";

        private static readonly (string path, string label)[] GemFields =
        {
            ("gemLook.enabled", "コアを宝石のように見せる"),
            ("gemLook.opacity", "宝石の層の濃さ"),
            ("gemLook.refraction", "後ろが曲がって透ける強さ"),
            ("gemLook.rimStrength", "ふちの光"),
            ("gemLook.highlight", "つやの点の明るさ"),
            ("gemLook.caustic", "中でゆらめく光の筋"),
            ("gemLook.glowColor", "脈打つ光の色（HP が多いとき）"),
            ("gemLook.hurtColor", "脈打つ光の色（HP が少ないとき）"),
            ("gemLook.glowStrength", "脈打つ光の明るさ"),
            ("gemLook.calmPulsePerSecond", "脈の速さ（HP が多いとき・回/秒）"),
            ("gemLook.hurtPulsePerSecond", "脈の速さ（HP が0に近いとき・回/秒）"),
            ("gemLook.crackStartHealth01", "ひびが入り始める HP（割合）"),
            ("gemLook.crackColor", "ひびから漏れる光の色"),
            ("gemLook.crackWidth", "ひびの太さ"),
            ("gemLook.hitFlashSeconds", "ダメージの瞬間に白く光る秒数")
        };

        private static readonly (string path, string label)[] RingFields =
        {
            ("hitRing.enabled", "ダメージを受けたら輪を出す"),
            ("hitRing.color", "輪の色"),
            ("hitRing.brightness", "輪の明るさ"),
            ("hitRing.radius", "輪が広がる半径（m）"),
            ("hitRing.seconds", "輪が消えるまでの秒数"),
            ("hitRing.width", "輪の太さ（m）"),
            ("hitRing.minInterval", "次の輪までの最短の間隔（秒）")
        };

        private static readonly (string path, string label)[] DangerFields =
        {
            ("dangerLook.enabled", "コアが危ないと画面を赤くする"),
            ("dangerLook.startHealth01", "赤くなり始める HP（割合）"),
            ("dangerLook.tint", "一番危ないときの画面の色"),
            ("dangerLook.saturation", "一番危ないときの鮮やかさ"),
            ("dangerLook.edgeColor", "画面のふちの色"),
            ("dangerLook.edgeStrength", "画面のふちの暗さ"),
            ("dangerLook.pulseBelowHealth01", "鼓動し始める HP（割合）"),
            ("dangerLook.pulsePerSecond", "鼓動の速さ（回/秒）"),
            ("dangerLook.response", "色が追いつく速さ")
        };

        private static SerializedObject _serialized;

        public static void Draw()
        {
            EditorGUILayout.LabelField("コアの見た目", EditorStyles.boldLabel);
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core == null)
            {
                EditorGUILayout.HelpBox("シーンにコア（CoreCrystalController）がありません。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField(
                "どれも見た目だけです。設定はシーンのコアに保存されるので、Play していないときに変えて Ctrl+S で保存してください（Play 中の変更は Play 終了で戻ります）。",
                EditorStyles.wordWrappedMiniLabel);

            if (_serialized == null || _serialized.targetObject != core)
            {
                _serialized = new SerializedObject(core);
            }

            _serialized.Update();
            DrawSection(GemFoldoutKey, "宝石のコア（屈折・脈打つ光・ひび）", GemFields);
            DrawSection(RingFoldoutKey, "ダメージを受けた瞬間の警告の輪", RingFields);
            DrawSection(DangerFoldoutKey, "コアが危ないときの画面の色", DangerFields);
            _serialized.ApplyModifiedProperties();

            if (!Application.isPlaying)
            {
                return;
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("見た目だけ試す（HP は減りません）", EditorStyles.miniLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                Preset("HP 50%", 0.5f);
                Preset("HP 25%", 0.25f);
                Preset("HP 10%", 0.1f);
                if (GUILayout.Button(new GUIContent("やめる", "本当の HP に合わせた見た目に戻します。"), EditorStyles.miniButton))
                {
                    CoreDangerLook.PreviewHealth01 = -1f;
                }
            }

            if (GUILayout.Button(new GUIContent("ダメージを受けた瞬間を試す", "宝石が白く光り、警告の輪が広がります（HP は減りません）。"), EditorStyles.miniButton))
            {
                if (core.TryGetComponent<CoreGemLook>(out var gem))
                {
                    gem.PlayHit();
                }

                if (core.TryGetComponent<CoreHitRings>(out var rings))
                {
                    rings.PlayHit();
                }
            }

            if (CoreDangerLook.PreviewHealth01 >= 0f)
            {
                EditorGUILayout.LabelField($"試している HP：{CoreDangerLook.PreviewHealth01:P0}（宝石のひび・脈と画面の色に効きます）", EditorStyles.miniLabel);
            }
        }

        private static void DrawSection(string key, string title, (string path, string label)[] fields)
        {
            var open = SessionState.GetBool(key, true);
            open = EditorGUILayout.Foldout(open, title, true);
            SessionState.SetBool(key, open);
            if (!open)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var field in fields)
                {
                    var property = _serialized.FindProperty(field.path);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, new GUIContent(field.label, property.tooltip));
                    }
                }
            }
        }

        private static void Preset(string label, float health01)
        {
            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                CoreDangerLook.PreviewHealth01 = health01;
            }
        }
    }
}
