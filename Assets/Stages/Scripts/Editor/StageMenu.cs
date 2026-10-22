using System.Collections.Generic;
using System.IO;
using System.Linq;
using MS2026.Fortress;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Stages.EditorTools
{
    /// <summary>メニュー「Tools › ステージ」（分ける道具以外）。</summary>
    internal static class StageMenu
    {
        [MenuItem("Tools/ステージ/ステージの一覧を開く", false, 20)]
        private static void OpenCatalog()
        {
            var catalog = StageAssets.LoadOrCreateCatalog();
            Selection.activeObject = catalog;
            EditorGUIUtility.PingObject(catalog);
        }

        [MenuItem("Tools/ステージ/選んだステージを Game と一緒に開く（編集用）", false, 21)]
        private static void OpenWithGame()
        {
            var path = SelectedStagePath() ?? FirstStagePath();
            if (path == null)
            {
                EditorUtility.DisplayDialog("ステージ", "開くステージのシーンが見つかりません。Project ウィンドウでステージのシーン（Assets/Scenes/Stages）を選んでから押してください。", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Single);
            var stage = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(stage);
            Debug.Log($"[Stage] Game と {Path.GetFileNameWithoutExtension(path)} を一緒に開きました。障害物・背景・敵はステージのシーン、コア・砲台は Game にあります。ステージのシーンを選んだまま Play すると、そのステージで遊べます。");
        }

        [MenuItem("Tools/ステージ/選んだステージを Game と一緒に開く（編集用）", true)]
        private static bool OpenWithGameValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/ステージ/新しいステージを作る（選んだステージを写す）", false, 22)]
        private static void CreateStage()
        {
            var source = SelectedStagePath() ?? FirstStagePath();
            if (source == null)
            {
                EditorUtility.DisplayDialog("ステージ", "写すもとのステージがありません。先に「今の Game を共通と Stage1 に分ける」を押してください。", "OK");
                return;
            }

            var catalog = StageAssets.LoadOrCreateCatalog();
            var number = 1;
            while (File.Exists(StageAssets.StageScenePath("Stage" + number)))
            {
                number++;
            }

            var sceneName = "Stage" + number;
            var path = StageAssets.StageScenePath(sceneName);
            if (!EditorUtility.DisplayDialog("新しいステージ", $"{Path.GetFileNameWithoutExtension(source)} を写して {sceneName} を作り、一覧とビルドのシーン一覧に入れます。", "作る", "やめる"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() || !AssetDatabase.CopyAsset(source, path))
            {
                return;
            }

            var color = Color.HSVToRGB(Mathf.Repeat(0.13f * number, 1f), 0.55f, 1f);
            var definition = StageAssets.CreateDefinition("Stage_" + number, "stage" + number, $"ステージ{number}", "", sceneName, color);
            StageAssets.AddToCatalog(catalog, definition);
            AssetDatabase.SaveAssets();
            StageAssets.SyncBuildSettings(catalog);

            EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Single);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            foreach (var layout in Object.FindObjectsByType<StageLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(l => l.gameObject.scene == scene))
            {
                layout.definition = definition;
                EditorUtility.SetDirty(layout);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = definition;
            Debug.Log($"[Stage] {sceneName} を作りました（{path}）。名前・カードの色は {AssetDatabase.GetAssetPath(definition)}。");
        }

        [MenuItem("Tools/ステージ/新しいステージを作る（選んだステージを写す）", true)]
        private static bool CreateStageValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        [MenuItem("Tools/ステージ/ビルドのシーン一覧を整える", false, 40)]
        private static void SyncBuild()
        {
            var report = new List<string>();
            var added = StageAssets.SyncBuildSettings(StageAssets.LoadOrCreateCatalog(), report);
            report.Insert(0, $"ステージのシーンを {added} 個、Game のすぐ後ろに入れました。");
            EditorUtility.DisplayDialog("ステージ", string.Join("\n", report), "OK");
        }

        [MenuItem("Tools/ステージ/チェック", false, 41)]
        private static void Check()
        {
            var issues = StageChecks.Run();
            var text = issues.Count == 0 ? "問題は見つかりませんでした。" : string.Join("\n", issues);
            Debug.Log("[Stage] チェック:\n" + text);
            EditorUtility.DisplayDialog("ステージのチェック", text, "OK");
        }

        [MenuItem("Tools/ステージ/チェック", true)]
        private static bool CheckValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        private static string SelectedStagePath()
        {
            var path = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (!string.IsNullOrEmpty(path) && path.EndsWith(".unity") && path != StageAssets.GameScenePath)
            {
                return path;
            }

            if (Selection.activeObject is StageDefinition definition)
            {
                return StageAssets.FindScenePath(definition.sceneName);
            }

            return null;
        }

        private static string FirstStagePath()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageAssets.CatalogPath);
            if (catalog == null)
            {
                return null;
            }

            return (catalog.stages ?? new StageDefinition[0]).Where(s => s != null).Select(s => StageAssets.FindScenePath(s.sceneName)).FirstOrDefault(p => p != null);
        }
    }

    /// <summary>ステージの仕組みの確かめ（シーンを開いて中身を見る）。</summary>
    internal static class StageChecks
    {
        public static List<string> Run()
        {
            var issues = new List<string>();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                issues.Add("やめました（保存していないシーンがあります）。");
                return issues;
            }

            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageAssets.CatalogPath);
            if (catalog == null || catalog.Count == 0)
            {
                issues.Add($"ステージの一覧（{StageAssets.CatalogPath}）が無いか空です（「今の Game を共通と Stage1 に分ける」を押すと作られます）。");
                return issues;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var buildPaths = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToList();
                var game = EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Single);
                if (!game.GetRootGameObjects().Any(r => r.GetComponentInChildren<StageBootstrap>(true) != null))
                {
                    issues.Add("Game に「ステージを読み込む係」（StageBootstrap）がありません。");
                }

                if (game.GetRootGameObjects().Any(r => r.GetComponentInChildren<SwarmSystem>(true) != null))
                {
                    issues.Add("Game にまだ群衆（SwarmSystem）があります（ステージ側へ移すこと）。");
                }

                for (var i = 0; i < catalog.Count; i++)
                {
                    var stage = catalog.Get(i);
                    if (stage == null)
                    {
                        issues.Add($"一覧の {i + 1} 番目が空です。");
                        continue;
                    }

                    var path = StageAssets.FindScenePath(stage.sceneName);
                    if (path == null)
                    {
                        issues.Add($"「{stage.DisplayName}」: シーン「{stage.sceneName}」がありません。");
                        continue;
                    }

                    if (!buildPaths.Contains(path))
                    {
                        issues.Add($"「{stage.DisplayName}」: ビルドのシーン一覧に入っていません（Tools › ステージ › ビルドのシーン一覧を整える）。");
                    }

                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    var roots = scene.GetRootGameObjects();
                    CountOne<StageLayout>(roots, stage, "配置の目印（StageLayout）", issues);
                    CountOne<SwarmSystem>(roots, stage, "群衆（SwarmSystem）", issues);
                    CountOne<NavigationField>(roots, stage, "経路（NavigationField）", issues);
                    CountOne<EnemySpawnDirector>(roots, stage, "敵を出す係（EnemySpawnDirector）", issues);
                    if (roots.Any(r => r.GetComponentInChildren<NetworkObject>(true) != null))
                    {
                        issues.Add($"「{stage.DisplayName}」: NetworkObject があります。ステージのシーンには置かないでください（通信の部品は Game にまとめる）。");
                    }

                    if (roots.Any(r => r.GetComponentInChildren<LaserTurret>(true) != null || r.GetComponentInChildren<CoreCrystalController>(true) != null))
                    {
                        issues.Add($"「{stage.DisplayName}」: 砲台かコアがあります。位置は「配置の目印」で決め、本体は Game にだけ置いてください。");
                    }

                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
            }

            return issues;
        }

        private static void CountOne<T>(GameObject[] roots, StageDefinition stage, string label, List<string> issues) where T : Component
        {
            var count = roots.Sum(r => r.GetComponentsInChildren<T>(true).Length);
            if (count != 1)
            {
                issues.Add($"「{stage.DisplayName}」: {label} が {count} 個あります（1 個にすること）。");
            }
        }
    }
}
