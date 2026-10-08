using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.UI.EditorTools
{
    /// <summary>画面の Prefab ファイルの片付け（削除）。一覧からの取り外し・シーンに置いた分・開いている編集画面もまとめて扱う。</summary>
    public static class UiScreenAssets
    {
        /// <summary>開いているシーンに置かれた、この Prefab の画面。</summary>
        public static List<UiScreen> FindSceneInstances(UiScreen prefab)
        {
            var result = new List<UiScreen>();
            foreach (var screen in Object.FindObjectsByType<UiScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (PrefabUtility.GetCorrespondingObjectFromSource(screen) == prefab)
                {
                    result.Add(screen);
                }
            }

            return result;
        }

        /// <summary>
        /// 画面を消す: すべての画面一覧から外し、シーンに置かれた分を消し（Ctrl+Z で戻る）、
        /// Prefab ファイルはパソコンのごみ箱へ移す（AssetDatabase.MoveAssetToTrash。ごみ箱から戻せる）。
        /// </summary>
        public static void Delete(UiScreen prefab, IReadOnlyList<UiScreen> sceneInstances)
        {
            var path = AssetDatabase.GetAssetPath(prefab);
            Undo.SetCurrentGroupName("画面を削除");
            var group = Undo.GetCurrentGroup();

            foreach (var catalog in UiStudioSetup.FindAll<UiScreenCatalog>())
            {
                if (catalog.screens.Contains(prefab))
                {
                    Undo.RecordObject(catalog, "画面を削除");
                    catalog.screens.RemoveAll(s => s == prefab);
                    EditorUtility.SetDirty(catalog);
                }
            }

            foreach (var instance in sceneInstances)
            {
                if (instance != null)
                {
                    Undo.DestroyObjectImmediate(instance.gameObject);
                }
            }

            Undo.CollapseUndoOperations(group);

            // 消す Prefab を編集画面で開いていたら、先にシーンへ戻る。
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.assetPath == path)
            {
                StageUtility.GoToMainStage();
            }

            AssetDatabase.SaveAssets();
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.MoveAssetToTrash(path);
            }
        }
    }
}
