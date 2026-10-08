using System.Collections.Generic;
using System.IO;
using MS2026.StudioKit;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stage.EditorTools
{
    /// <summary>ステージ関係のアセット（StageSet・Prefab・共通設定）の置き場所と作り方。</summary>
    public static class StageAssetFactory
    {
        public const string StagesFolder = "Assets/_Game/Stage/Stages";
        public const string PresetsFolder = "Assets/_Game/Stage/Presets";
        public const string DefaultLookProfilePath = PresetsFolder + "/StageLookProfile_Default.asset";
        public const string PropsContainerName = "Props";

        public static List<StageSet> FindAllStageSets()
        {
            var result = new List<StageSet>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(StageSet)))
            {
                var stage = AssetDatabase.LoadAssetAtPath<StageSet>(AssetDatabase.GUIDToAssetPath(guid));
                if (stage != null)
                {
                    result.Add(stage);
                }
            }

            result.Sort((a, b) => string.CompareOrdinal(a.Label, b.Label));
            return result;
        }

        /// <summary>共通の反応設定（無ければ作る）。</summary>
        public static StageLookProfile GetOrCreateDefaultLookProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<StageLookProfile>(DefaultLookProfilePath);
            if (profile != null)
            {
                return profile;
            }

            EnsureFolder(PresetsFolder);
            profile = ScriptableObject.CreateInstance<StageLookProfile>();
            AssetDatabase.CreateAsset(profile, DefaultLookProfilePath);
            AssetDatabase.SaveAssets();
            return profile;
        }

        /// <summary>
        /// 新しいステージを作る: Assets/_Game/Stage/Stages/{名前}/ に StageSet と空のPrefab（中に Props）を置く。
        /// </summary>
        public static StageSet CreateStage(string displayName)
        {
            var safe = StudioAssets.SafeFileName(displayName, "NewStage");
            // 親（Stages）が無いと GenerateUniqueAssetPath が空文字を返すので、UniquePath で親を先に作る。
            var folder = StudioAssets.UniquePath(StagesFolder, safe);
            EnsureFolder(folder);

            var root = new GameObject($"Stage_{safe}");
            new GameObject(PropsContainerName).transform.SetParent(root.transform, false);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{folder}/Stage_{safe}.prefab");
            Object.DestroyImmediate(root);

            var stage = ScriptableObject.CreateInstance<StageSet>();
            stage.displayName = displayName;
            stage.stagePrefab = prefab;
            stage.lookProfile = GetOrCreateDefaultLookProfile();
            AssetDatabase.CreateAsset(stage, $"{folder}/StageSet_{safe}.asset");
            AssetDatabase.SaveAssets();
            return stage;
        }

        /// <summary>ステージのフォルダ（StageSetと同じ場所）。</summary>
        public static string FolderOf(StageSet stage)
        {
            var path = AssetDatabase.GetAssetPath(stage);
            return string.IsNullOrEmpty(path) ? StagesFolder : Path.GetDirectoryName(path)?.Replace('\\', '/');
        }

        /// <summary>フォルダが無ければ親から順に作る（StudioKit の共通処理）。</summary>
        public static void EnsureFolder(string folder) => StudioAssets.EnsureFolder(folder);

        public static string MakeSafeFileName(string name) => StudioAssets.SafeFileName(name);
    }
}
