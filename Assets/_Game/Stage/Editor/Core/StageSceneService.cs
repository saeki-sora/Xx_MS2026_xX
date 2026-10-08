using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>開いているシーンの StageRoot（今のステージの置き場所）を扱う。</summary>
    public static class StageSceneService
    {
        public const string RootName = "[Stage]";

        public static StageRoot FindRoot()
        {
            return StageRoot.Active != null ? StageRoot.Active : Object.FindFirstObjectByType<StageRoot>(FindObjectsInactive.Include);
        }

        /// <summary>置き場所を作る。シーンにもうあれば、それを返す（二重に作らない）。</summary>
        public static StageRoot CreateRoot()
        {
            var existing = FindRoot();
            if (existing != null)
            {
                return existing;
            }

            var go = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(go, "ステージの置き場所を作成");
            var root = go.AddComponent<StageRoot>();
            MarkSceneDirty();
            return root;
        }

        /// <summary>
        /// ステージをシーンに出す。前のステージの実体は消し、Prefabのつながりを保ったまま置く。
        /// 光・カメラの見え方・反応の設定もそのステージのものにする。
        /// </summary>
        public static void PlaceStage(StageRoot root, StageSet stage)
        {
            Undo.RecordObject(root, "ステージをシーンに出す");
            if (root.instance != null)
            {
                Undo.DestroyObjectImmediate(root.instance);
            }

            root.current = stage;
            root.instance = null;
            if (stage != null && stage.stagePrefab != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(stage.stagePrefab, root.transform);
                Undo.RegisterCreatedObjectUndo(instance, "ステージをシーンに出す");
                root.instance = instance;
            }

            root.ApplyEnvironment(applyCamera: true);
            MarkSceneDirty();
        }

        /// <summary>シーンで直した中身を、ステージのPrefabへ書き戻す。</summary>
        public static bool SaveToPrefab(StageRoot root)
        {
            if (root == null || root.instance == null || !PrefabUtility.IsPartOfPrefabInstance(root.instance))
            {
                return false;
            }

            PrefabUtility.ApplyPrefabInstance(root.instance, InteractionMode.UserAction);
            return true;
        }

        /// <summary>シーンの中身がステージのPrefabとずれているか（保存していない変更があるか）。</summary>
        public static bool HasUnsavedChanges(StageRoot root)
        {
            return root != null && root.instance != null &&
                   PrefabUtility.IsPartOfPrefabInstance(root.instance) &&
                   PrefabUtility.HasPrefabInstanceAnyOverrides(root.instance, false);
        }

        /// <summary>背景オブジェクトを入れる親（Prefabの中の Props）。無ければ作る。</summary>
        public static Transform PropsContainer(StageRoot root)
        {
            if (root == null || root.instance == null)
            {
                return null;
            }

            var container = root.instance.transform.Find(StageAssetFactory.PropsContainerName);
            if (container != null)
            {
                return container;
            }

            var go = new GameObject(StageAssetFactory.PropsContainerName);
            Undo.RegisterCreatedObjectUndo(go, "Props を作成");
            go.transform.SetParent(root.instance.transform, false);
            return go.transform;
        }

        /// <summary>今のステージの背景オブジェクト（無効なものも含む、並びはヒエラルキー順）。</summary>
        public static List<StageProp> CollectProps(StageRoot root)
        {
            var result = new List<StageProp>();
            if (root != null && root.instance != null)
            {
                root.instance.GetComponentsInChildren(true, result);
            }

            return result;
        }

        public static void MarkSceneDirty()
        {
            if (!Application.isPlaying)
            {
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            }
        }
    }
}
