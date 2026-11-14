using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// 自動プレイのエディタの入口。
    /// ・「Tools/CPU/自動プレイで Play する」: 設定の試合数・早送りで、全部の席を CPU にして今のシーンを Play する
    ///   （Game かステージのシーンを開いておく。ステージのシーンを開いて Play すると共通の Game は自動で読み込まれる）
    /// ・Play 中は、ビルドの設定に入っていないシーンも読み直せるように、エディタの読み込みを使わせる
    /// </summary>
    [InitializeOnLoad]
    internal static class CpuAutoplayMenu
    {
        private const string RequestKey = "MS2026.Cpu.AutoplayRequest";

        static CpuAutoplayMenu()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/CPU/自動プレイで Play する", priority = 10)]
        private static void PlayAutoplay()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[CPU] Play を止めてから使ってください。");
                return;
            }

            var settings = CpuSettings.Load();
            SessionState.SetInt(RequestKey, Mathf.Max(1, settings.autoplayMatches));
            EditorApplication.EnterPlaymode();
        }

        [MenuItem("Tools/CPU/結果の表のフォルダを開く", priority = 11)]
        private static void OpenLogFolder()
        {
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "CPU");
            Directory.CreateDirectory(folder);
            EditorUtility.RevealInFinder(folder + Path.DirectorySeparatorChar);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.EnteredPlayMode:
                    CpuAutoplay.EditorSceneLoader = LoadInPlayMode;
                    CpuAutoplay.EditorRequestMatches = SessionState.GetInt(RequestKey, 0);
                    SessionState.EraseInt(RequestKey);
                    break;
                case PlayModeStateChange.ExitingPlayMode:
                case PlayModeStateChange.EnteredEditMode:
                    CpuAutoplay.EditorRequestMatches = 0;
                    CpuAutoplay.EditorSceneLoader = null;
                    break;
            }
        }

        private static bool LoadInPlayMode(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return false;
            }

            EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
            return true;
        }
    }
}
