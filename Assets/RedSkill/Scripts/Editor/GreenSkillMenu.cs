using MS2026.Fortress;
using MS2026.Skills.Green;
using UnityEditor;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>
    /// メニュー「Tools › 緑スキル」と、緑スキルの設定ファイルのインスペクターに出すテスト道具（コアを減らす・満タンにする）。
    /// 回復はコアが減っていないと見えないので、Play 中にコアを減らしてから「今すぐ撃つ」で試す。
    /// </summary>
    [InitializeOnLoad]
    internal static class GreenSkillMenu
    {
        public const string SkillAssetPath = "Assets/RedSkill/Data/GreenMintSkill.asset";

        static GreenSkillMenu()
        {
            SkillTestButtons.DrawExtraTools += DrawTools;
        }

        [MenuItem("Tools/緑スキル/スキルの設定ファイルを開く", false, 0)]
        private static void SelectSkillAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<GreenMintSkill>(SkillAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("緑スキル", "設定ファイルが見つかりません: " + SkillAssetPath, "OK");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("Tools/緑スキル/コアを30%減らす（回復を試す・Play中）", false, 20)]
        private static void DamageCore()
        {
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core != null)
            {
                core.ApplyReplicatedHealth(Mathf.Max(1f, core.CurrentHealth - core.maxHealth * 0.3f));
            }
        }

        [MenuItem("Tools/緑スキル/コアを30%減らす（回復を試す・Play中）", true)]
        private static bool DamageCoreValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("Tools/緑スキル/緑スキルを今すぐ撃つ（Play中）", false, 21)]
        private static void FireNow()
        {
            var slots = PlayerSkillSlot.All;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Skill is GreenMintSkill)
                {
                    slots[i].GrantCharge();
                    slots[i].ActivateNow();
                    return;
                }
            }

            Debug.LogWarning("[GreenSkill] 緑スキルを持っている砲台がありません（スキルを配る係の「プレイヤーごとのスキル」の P3 を確認してください）。");
        }

        [MenuItem("Tools/緑スキル/緑スキルを今すぐ撃つ（Play中）", true)]
        private static bool FireNowValidate()
        {
            return EditorApplication.isPlaying;
        }

        private static void DrawTools(CharacterSkill skill)
        {
            if (!(skill is GreenMintSkill) || !EditorApplication.isPlaying)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("テスト（コア）", EditorStyles.boldLabel);
            var core = Object.FindFirstObjectByType<CoreCrystalController>();
            if (core == null)
            {
                EditorGUILayout.HelpBox("シーンにコア（CoreCrystalController）がありません。", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("コアの体力", $"{core.CurrentHealth:0.0} / {core.maxHealth:0}（{core.CurrentHealth / Mathf.Max(1f, core.maxHealth):P0}）");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("コアを30%減らす", "回復を試すために、コアの体力を最大の30%ぶん減らす（1 は残すので壊れない）。")))
                {
                    core.ApplyReplicatedHealth(Mathf.Max(1f, core.CurrentHealth - core.maxHealth * 0.3f));
                }

                if (GUILayout.Button(new GUIContent("コアを満タンにする", "コアの体力を最大に戻す。")))
                {
                    core.ApplyReplicatedHealth(core.maxHealth);
                }
            }
        }
    }
}
