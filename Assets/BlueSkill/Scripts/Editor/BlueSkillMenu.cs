using MS2026.Skills.Blue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Skills.Blue.EditorTools
{
    /// <summary>メニュー「Tools › 青スキル」。開いているシーンにスキルを入れる・設定ファイルを開く。</summary>
    internal static class BlueSkillMenu
    {
        public const string PrefabPath = "Assets/BlueSkill/Prefabs/BlueSkill_Installer.prefab";
        public const string SkillAssetPath = "Assets/BlueSkill/Data/BlueVortexSkill.asset";
        private const string Title = "青スキル";

        [MenuItem("Tools/青スキル/開いているシーンにスキルを入れる", false, 0)]
        private static void AddToOpenScene()
        {
            var skill = AssetDatabase.LoadAssetAtPath<BlueVortexSkill>(SkillAssetPath);
            var existing = Object.FindAnyObjectByType<PlayerSkillInstaller>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                if (existing.defaultSkill == skill)
                {
                    EditorUtility.DisplayDialog(Title, "このシーンの「" + existing.name + "」はもう青スキルを配っています。", "OK");
                    return;
                }

                var current = existing.defaultSkill != null ? existing.defaultSkill.DisplayName : "なし";
                var change = EditorUtility.DisplayDialog(Title,
                    "このシーンにはもうスキルを配る係（" + existing.name + "）があります（今の「全員に配るスキル」: " + current + "）。\n\n" +
                    "「全員に配るスキル」を青スキルに変えますか？\n（キャラ選択を通ってきたときは、キャラとスキルの対応表が優先されます）",
                    "青スキルに変える", "やめる");
                if (!change)
                {
                    return;
                }

                Undo.RecordObject(existing, "青スキルを配るように変える");
                existing.defaultSkill = skill;
                EditorUtility.SetDirty(existing);
                EditorSceneManager.MarkSceneDirty(existing.gameObject.scene);
                Debug.Log("[BlueSkill] 「" + existing.name + "」の「全員に配るスキル」を青スキルにしました。シーンを保存してください（Ctrl+S）。");
                return;
            }

            GameObject instance;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab != null)
            {
                instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            }
            else
            {
                // プレハブが無い（消された）場合は、その場で同じ物を作る。
                instance = new GameObject("[BlueSkill] Skill Installer");
                var installer = instance.AddComponent<PlayerSkillInstaller>();
                installer.defaultSkill = skill;
            }

            Undo.RegisterCreatedObjectUndo(instance, "青スキルをシーンに入れる");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Debug.Log("[BlueSkill] シーンに「" + instance.name + "」を置きました。シーンを保存してください（Ctrl+S）。");
        }

        [MenuItem("Tools/青スキル/開いているシーンにスキルを入れる", true)]
        private static bool AddToOpenSceneValidate()
        {
            return !EditorApplication.isPlaying;
        }

        [MenuItem("Tools/青スキル/スキルの設定ファイルを開く", false, 20)]
        private static void SelectSkillAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<BlueVortexSkill>(SkillAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog(Title, "設定ファイルが見つかりません: " + SkillAssetPath, "OK");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        [MenuItem("Tools/青スキル/全員を使える状態にする（Play中）", false, 40)]
        private static void ChargeAll()
        {
            var slots = PlayerSkillSlot.All;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].Skill is BlueVortexSkill)
                {
                    slots[i].GrantCharge();
                }
            }
        }

        [MenuItem("Tools/青スキル/全員を使える状態にする（Play中）", true)]
        private static bool ChargeAllValidate()
        {
            return EditorApplication.isPlaying;
        }

        [MenuItem("Tools/青スキル/出ている渦を消す（Play中）", false, 41)]
        private static void DismissAll()
        {
            var areas = BlueVortexArea.Active;
            for (var i = areas.Count - 1; i >= 0; i--)
            {
                if (areas[i] != null)
                {
                    areas[i].Dismiss();
                }
            }
        }

        [MenuItem("Tools/青スキル/出ている渦を消す（Play中）", true)]
        private static bool DismissAllValidate()
        {
            return EditorApplication.isPlaying;
        }
    }
}
