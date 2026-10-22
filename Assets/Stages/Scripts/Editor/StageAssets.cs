using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace MS2026.Stages.EditorTools
{
    /// <summary>ステージの設定ファイル・一覧・ビルドのシーン一覧を作る／整える道具（メニューと分ける道具が使う）。</summary>
    internal static class StageAssets
    {
        public const string GameScenePath = "Assets/Scenes/Game.unity";
        public const string StageSceneFolder = "Assets/Scenes/Stages";
        public const string DataFolder = "Assets/Stages/Data";
        public const string CatalogPath = "Assets/Stages/Resources/StageCatalog.asset";
        public const string StripesPath = "Assets/CharacterSelect/Art/CS_Stripes.png";

        public static string StageScenePath(string sceneName) => StageSceneFolder + "/" + sceneName + ".unity";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>ステージの一覧（無ければ作る。後ろのしまはキャラ選択の絵を使う）。</summary>
        public static StageCatalog LoadOrCreateCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(CatalogPath);
            if (catalog != null)
            {
                return catalog;
            }

            EnsureFolder(Path.GetDirectoryName(CatalogPath)?.Replace('\\', '/'));
            catalog = ScriptableObject.CreateInstance<StageCatalog>();
            catalog.roulette.stripes = AssetDatabase.LoadAssetAtPath<Texture2D>(StripesPath);
            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        /// <summary>ステージの設定ファイルを作る（同じ名前があればそれを使い、中身を合わせる）。</summary>
        public static StageDefinition CreateDefinition(string fileName, string stageId, string displayName, string subtitle, string sceneName, Color color)
        {
            EnsureFolder(DataFolder);
            var path = DataFolder + "/" + fileName + ".asset";
            var definition = AssetDatabase.LoadAssetAtPath<StageDefinition>(path);
            var created = definition == null;
            if (created)
            {
                definition = ScriptableObject.CreateInstance<StageDefinition>();
            }

            definition.stageId = stageId;
            definition.displayName = displayName;
            definition.subtitle = subtitle;
            definition.sceneName = sceneName;
            definition.themeColor = color;
            if (created)
            {
                AssetDatabase.CreateAsset(definition, path);
            }
            else
            {
                EditorUtility.SetDirty(definition);
            }

            return definition;
        }

        /// <summary>一覧の最後に足す（もう入っていれば何もしない）。</summary>
        public static void AddToCatalog(StageCatalog catalog, StageDefinition definition)
        {
            var list = (catalog.stages ?? new StageDefinition[0]).Where(s => s != null).ToList();
            if (!list.Contains(definition))
            {
                list.Add(definition);
            }

            Undo.RecordObject(catalog, "ステージを一覧に足す");
            catalog.stages = list.ToArray();
            EditorUtility.SetDirty(catalog);
        }

        /// <summary>
        /// ビルドのシーン一覧に、一覧のステージのシーンを Game のすぐ後ろへ入れる（無い物だけ。順番は一覧どおり）。
        /// 入れたシーンの数を返す。
        /// </summary>
        public static int SyncBuildSettings(StageCatalog catalog, List<string> report = null)
        {
            if (catalog == null)
            {
                return 0;
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            var commonPath = scenes.FirstOrDefault(s => Path.GetFileNameWithoutExtension(s.path) == catalog.commonSceneName)?.path;
            var insertAt = commonPath != null ? scenes.FindIndex(s => s.path == commonPath) + 1 : scenes.Count;
            var added = 0;
            foreach (var stage in catalog.stages ?? new StageDefinition[0])
            {
                if (stage == null || string.IsNullOrEmpty(stage.sceneName))
                {
                    continue;
                }

                var path = FindScenePath(stage.sceneName);
                if (path == null)
                {
                    report?.Add($"ステージ「{stage.DisplayName}」のシーン「{stage.sceneName}」が見つかりません。");
                    continue;
                }

                var existing = scenes.FindIndex(s => s.path == path);
                if (existing >= 0)
                {
                    if (!scenes[existing].enabled)
                    {
                        scenes[existing] = new EditorBuildSettingsScene(path, true);
                        added++;
                    }

                    continue;
                }

                scenes.Insert(Mathf.Clamp(insertAt, 0, scenes.Count), new EditorBuildSettingsScene(path, true));
                insertAt++;
                added++;
            }

            if (added > 0)
            {
                EditorBuildSettings.scenes = scenes.ToArray();
            }

            return added;
        }

        /// <summary>そのシーン名のシーンファイル（ステージのフォルダを優先。無ければプロジェクト全体から）。</summary>
        public static string FindScenePath(string sceneName)
        {
            var preferred = StageScenePath(sceneName);
            if (File.Exists(preferred))
            {
                return preferred;
            }

            foreach (var guid in AssetDatabase.FindAssets(sceneName + " t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return path;
                }
            }

            return null;
        }
    }
}
