using MS2026.Skills.Red;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Skills.EditorTools
{
    /// <summary>メニュー「Tools › 赤スキル」。開いているシーンにスキルを入れる・設定ファイルを開く。</summary>
    internal static class RedSkillMenu
    {
        public const string PrefabPath = "Assets/RedSkill/Prefabs/RedSkill_Installer.prefab";
        public const string SkillAssetPath = "Assets/RedSkill/Data/RedLaserSkill.asset";

        [MenuItem("Tools/赤スキル/開いているシーンにスキルを入れる", false, 0)]
        private static void AddToOpenScene()
        {
            var existing = Object.FindAnyObjectByType<PlayerSkillInstaller>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                EditorUtility.DisplayDialog("赤スキル", "このシーンにはもうスキルを配る係（" + existing.name + "）があります。それを選択しました。", "OK");
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
                instance = new GameObject("[RedSkill] Skill Installer");
                var installer = instance.AddComponent<PlayerSkillInstaller>();
                installer.defaultSkill = AssetDatabase.LoadAssetAtPath<RedLaserSkill>(SkillAssetPath);
            }

            Undo.RegisterCreatedObjectUndo(instance, "赤スキルをシーンに入れる");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            Debug.Log("[RedSkill] シーンに「" + instance.name + "」を置きました。シーンを保存してください（Ctrl+S）。");
        }

        [MenuItem("Tools/赤スキル/開いているシーンにスキルを入れる", true)]
        private static bool AddToOpenSceneValidate()
        {
            return !EditorApplication.isPlaying;
        }

        [MenuItem("Tools/赤スキル/スキルの設定ファイルを開く", false, 20)]
        private static void SelectSkillAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<RedLaserSkill>(SkillAssetPath);
            if (asset == null)
            {
                EditorUtility.DisplayDialog("赤スキル", "設定ファイルが見つかりません: " + SkillAssetPath, "OK");
                return;
            }

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }
    }
}
