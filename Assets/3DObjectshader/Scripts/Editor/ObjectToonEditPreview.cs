using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MS2026.ObjectToon.EditorTools
{
    /// <summary>
    /// 再生していないときもトゥーンで見るためのプレビュー（見るだけ。シーンは変えない）。調整ウィンドウを開いている間だけオンになる。
    /// ・保存の直前に元の材質へ戻してから書き込み、保存のあとでまたトゥーンにする（シーンのファイルには残らない）。
    /// ・再生を始める直前にも戻す（再生中は ObjectToonApplier が差し替える）。
    /// 戻すときは表を逆に引くだけなので、スクリプトの再読み込みをはさんでも元に戻せる。
    /// </summary>
    [InitializeOnLoad]
    public static class ObjectToonEditPreview
    {
        private const string StateKey = "MS2026.ObjectToon.EditPreview";

        static ObjectToonEditPreview()
        {
            EditorSceneManager.sceneSaving += (scene, path) => Revert(scene);
            EditorSceneManager.sceneSaved += scene => { if (IsOn) Refresh(); };
            EditorSceneManager.sceneOpened += (scene, mode) => { if (IsOn) Refresh(); };
            EditorSceneManager.sceneClosing += (scene, removing) => Revert(scene);
            EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        }

        public static bool IsOn
        {
            get => SessionState.GetBool(StateKey, false);
            set
            {
                if (value == IsOn)
                {
                    return;
                }

                SessionState.SetBool(StateKey, value);
                Refresh();
            }
        }

        /// <summary>表を変えたあとに呼ぶ（いったん全部戻してから、プレビュー中なら当て直す）。</summary>
        public static void Refresh()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            var profile = ObjectToonMaterials.LoadProfile();
            if (profile == null)
            {
                return;
            }

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    ObjectToonApplier.Revert(scene, profile);
                }
            }

            if (IsOn)
            {
                ObjectToonApplier.ApplyAllLoaded(profile);
            }

            ObjectToonMaterials.RepaintViews();
        }

        private static void HandlePlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
            {
                RevertAll();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                // 実行中に調整ウィンドウを閉じていたら、プレビューもやめる。
                if (!EditorWindow.HasOpenInstances<ObjectToonWindow>())
                {
                    SessionState.SetBool(StateKey, false);
                }

                if (IsOn)
                {
                    Refresh();
                }
            }
        }

        /// <summary>プレビューの切り替えに関係なく、トゥーン材質を全部元に戻す。</summary>
        public static void RevertAll()
        {
            var profile = ObjectToonMaterials.LoadProfile();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Revert(SceneManager.GetSceneAt(i), profile);
            }
        }

        private static void Revert(Scene scene)
        {
            Revert(scene, ObjectToonMaterials.LoadProfile());
        }

        // 開いているシーンだけ（Prefab の編集画面は SceneManager に入らないので対象外。Prefab に保存されない）。
        private static void Revert(Scene scene, ObjectToonProfile profile)
        {
            if (!EditorApplication.isPlaying && profile != null && scene.isLoaded)
            {
                ObjectToonApplier.Revert(scene, profile);
            }
        }
    }
}
