using MS2026.Skills.Blue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Skills.Blue.EditorTools
{
    /// <summary>メニュー「Tools › 青スキル」。開いているシーンにスキルを入れる・設定ファイルを開く。</summary>
    internal static class BlueSkillMenu
    {
        // スキルを配る係は 4 色共通の1つ（2026-10-25 から。青専用のプレハブは「全員に配るスキル」で青を配るだけだったので使わない）。
        public const string PrefabPath = "Assets/RedSkill/Prefabs/RedSkill_Installer.prefab";
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
                EditorUtility.DisplayDialog(Title,
                    "このシーンにはもうスキルを配る係（" + existing.name + "）があります。それを選択しました。\n\n" +
                    "青スキルは、キャラ選択で青のキャラを選んだ人に配られます（キャラとスキルの対応表）。\n" +
                    "キャラ選択を通らないときは「プレイヤーごとのスキル」の席（ふつうは P2）に配られます。",
                    "OK");
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
                // プレハブが無い（消された）場合は、その場で作る（青は P2）。
                instance = new GameObject("[Skill] Skill Installer");
                var installer = instance.AddComponent<PlayerSkillInstaller>();
                installer.perPlayerSkills[1] = skill;
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
