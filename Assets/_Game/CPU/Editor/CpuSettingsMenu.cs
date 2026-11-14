using System.IO;
using UnityEditor;
using UnityEngine;

namespace MS2026.Cpu.EditorTools
{
    /// <summary>
    /// メニュー「Tools/CPU/CPU の設定を開く」: CPU の設定（Resources/CpuSettings）を開く。無ければ作る。
    /// 専用の調整スタジオ（段10）ができるまでの入口。
    /// </summary>
    internal static class CpuSettingsMenu
    {
        private const string Folder = "Assets/_Game/CPU/Runtime/Resources";
        private const string AssetPath = Folder + "/" + CpuSettings.ResourcePath + ".asset";

        [MenuItem("Tools/CPU/CPU の設定を開く", priority = 0)]
        private static void Open()
        {
            var settings = AssetDatabase.LoadAssetAtPath<CpuSettings>(AssetPath);
            if (settings == null)
            {
                Directory.CreateDirectory(Folder);
                settings = ScriptableObject.CreateInstance<CpuSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[CPU] 設定を作りました: {AssetPath}");
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("Tools/CPU/今の CPU の席を Console に出す", priority = 20)]
        private static void LogSeats()
        {
            var director = CpuDirector.Current;
            if (!Application.isPlaying || director == null)
            {
                Debug.Log("[CPU] Play 中だけ使えます。");
                return;
            }

            Debug.Log($"[CPU] CPU の席: {CpuSeatRules.Describe(director.CpuMask)}（{CpuLevels.Label(director.Level)}）");
            for (var p = 0; p < CpuSeatRules.PlayerCount; p++)
            {
                var cpu = director.GetPlayer(p);
                if (cpu == null)
                {
                    continue;
                }

                var current = cpu.Brain.Current;
                Debug.Log($"[CPU]   P{p + 1}: 行動 {(current != null ? current.Id : "なし")}・握力 {cpu.Hands.Grip01:0.00}" +
                          (cpu.CanMove ? $"・行きたい向き {cpu.MoveIntent}" : string.Empty));
            }
        }
    }
}
