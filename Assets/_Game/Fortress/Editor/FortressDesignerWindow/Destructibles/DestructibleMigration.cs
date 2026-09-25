using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 旧バージョンの破壊可能物（見た目・演出などの部品が付いていないもの）を、シーンを開いたときに新形式へ更新する。
    /// 耐久・再生の値は DestructibleObstacle が読み込み時に自動で引き継ぐので、ここでは足りない部品を追加するだけ。
    /// </summary>
    [InitializeOnLoad]
    public static class DestructibleMigration
    {
        static DestructibleMigration()
        {
            EditorSceneManager.sceneOpened += (scene, mode) => Migrate(scene);
            EditorApplication.delayCall += () =>
            {
                for (var i = 0; i < SceneManager.sceneCount; i++)
                {
                    Migrate(SceneManager.GetSceneAt(i));
                }
            };
        }

        private static void Migrate(Scene scene)
        {
            if (Application.isPlaying || !scene.isLoaded)
            {
                return;
            }

            var changed = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var obstacle in root.GetComponentsInChildren<DestructibleObstacle>(true))
                {
                    var before = obstacle.GetComponents<Component>().Length;
                    obstacle.EnsureRequiredComponents();
                    changed |= obstacle.GetComponents<Component>().Length != before;
                }
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                Debug.Log($"[FortressDesigner] 旧形式の破壊可能物を新形式へ更新しました（{scene.name}）。シーンを保存してください。");
            }
        }
    }
}
