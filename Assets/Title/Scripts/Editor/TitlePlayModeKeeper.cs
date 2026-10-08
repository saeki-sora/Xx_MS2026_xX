using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Title.EditorTools
{
    /// <summary>
    /// 再生中に Inspector で変えた値は、普通は再生を止めると元に戻る。
    /// 「再生中に変えた値を残す」を押すと、その時点の [Title] とカメラの調整値を写し取り、再生を止めた直後にシーンへ書き戻す。
    /// （位置・向き・大きさは再生中は動きが上書きしているので対象外。位置は再生を止めてから動かす）
    /// </summary>
    [InitializeOnLoad]
    internal static class TitlePlayModeKeeper
    {
        private const string RootKey = "MS2026.Title.KeptValues.Root";
        private const string CameraKey = "MS2026.Title.KeptValues.Camera";
        private const string RootName = "[Title]";

        static TitlePlayModeKeeper()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        public static bool HasPending => !string.IsNullOrEmpty(SessionState.GetString(RootKey, ""));

        /// <summary>今の値を写し取る（再生中のみ）。</summary>
        public static void Keep()
        {
            if (!EditorApplication.isPlaying) return;
            var root = FindRoot();
            if (root == null)
            {
                Debug.LogWarning($"[Title] シーンに「{RootName}」が見つからないので、値を残せませんでした。");
                return;
            }

            SessionState.SetString(RootKey, JsonUtility.ToJson(TitleValueSnapshot.Capture(root, includeTransforms: false)));
            var camera = Camera.main;
            SessionState.SetString(CameraKey, camera != null ? JsonUtility.ToJson(TitleValueSnapshot.Capture(camera.transform, includeTransforms: false)) : "");
            Debug.Log("[Title] 今の値を写し取りました。再生を止めるとシーンに書き戻します（そのあとシーンを保存してください）。");
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !HasPending) return;

            var rootJson = SessionState.GetString(RootKey, "");
            var cameraJson = SessionState.GetString(CameraKey, "");
            SessionState.EraseString(RootKey);
            SessionState.EraseString(CameraKey);

            var root = FindRoot();
            if (root == null) return;

            const string undoName = "再生中に変えた値を残す";
            var applied = JsonUtility.FromJson<TitleValueSnapshot>(rootJson).Apply(root, undoName);
            var camera = Camera.main;
            if (camera != null && !string.IsNullOrEmpty(cameraJson))
            {
                applied += JsonUtility.FromJson<TitleValueSnapshot>(cameraJson).Apply(camera.transform, undoName);
            }

            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Debug.Log($"[Title] 再生中に変えた値をシーンに書き戻しました（{applied} 部品）。シーンを保存すると確定します（元に戻すなら Ctrl+Z）。");
        }

        private static Transform FindRoot()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                foreach (var go in scene.GetRootGameObjects())
                {
                    if (go.name == RootName) return go.transform;
                }
            }
            return null;
        }
    }
}
