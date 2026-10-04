using System;
using MS2026.Fortress.Cameras;
using UnityEditor;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>視点プリセットの書き換え(Undo対応)と、カメラ関連アセットの新規作成。</summary>
    public static class CameraPresetEditing
    {
        public const string AssetFolder = "Assets/_Game/Fortress/Presets/Camera";

        public static void SetView(CameraViewPreset preset, int viewer, CameraViewSettings view, string undoLabel)
        {
            if (preset == null || preset.GetView(viewer).Approximately(view.Sanitized()))
            {
                return;
            }

            Undo.RecordObject(preset, undoLabel);
            preset.SetView(viewer, view);
            EditorUtility.SetDirty(preset);
        }

        /// <summary>全視点(全体+P1〜P4)をまとめて書き換える。Undoは1回で戻せる。</summary>
        public static void ModifyAll(CameraViewPreset preset, string undoLabel, Func<int, CameraViewSettings, CameraViewSettings> modify)
        {
            if (preset == null)
            {
                return;
            }

            Undo.RecordObject(preset, undoLabel);
            foreach (var viewer in ViewerIndex.All)
            {
                preset.SetView(viewer, modify(viewer, preset.GetView(viewer)));
            }

            EditorUtility.SetDirty(preset);
        }

        /// <summary>プレイヤー4人分だけを書き換える(全体視点はそのまま)。</summary>
        public static void ModifyPlayers(CameraViewPreset preset, string undoLabel, Func<int, CameraViewSettings, CameraViewSettings> modify)
        {
            ModifyAll(preset, undoLabel, (viewer, view) => ViewerIndex.IsPlayer(viewer) ? modify(viewer, view) : view);
        }

        /// <summary>新しい視点プリセットを作る。sourceを渡すとその複製になる。</summary>
        public static CameraViewPreset CreatePresetAsset(string presetName, CameraViewPreset source = null)
        {
            var preset = ScriptableObject.CreateInstance<CameraViewPreset>();
            if (source != null)
            {
                EditorUtility.CopySerialized(source, preset);
            }

            preset.presetName = presetName;
            preset.EnsurePlayerSlots();
            return CreateAsset(preset, presetName);
        }

        public static CameraFeedbackConfig CreateFeedbackConfigAsset()
        {
            var config = ScriptableObject.CreateInstance<CameraFeedbackConfig>();
            config.EnsureAllEvents();
            return CreateAsset(config, "CameraFeedbackConfig");
        }

        private static T CreateAsset<T>(T asset, string fileName) where T : ScriptableObject
        {
            EnsureFolder(AssetFolder);
            var safeName = string.Join("_", fileName.Split(System.IO.Path.GetInvalidFileNameChars()));
            var path = AssetDatabase.GenerateUniqueAssetPath($"{AssetFolder}/{safeName}.asset");
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            return asset;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
