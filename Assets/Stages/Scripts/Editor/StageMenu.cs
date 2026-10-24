using System.Collections.Generic;
using System.IO;
using System.Linq;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.EditorTools;
using MS2026.Stage;
using MS2026.Stage.EditorTools;
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
            var number = NextFreeStageNumber(catalog);

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
                // 視点は写しもとと共用にしない（共用だと、新しいステージの視点を直したとき写しもとのステージも変わる）。
                layout.cameraPreset = CopyCameraPreset(layout.cameraPreset, sceneName, definition.DisplayName, Path.GetFileNameWithoutExtension(source));
                EditorUtility.SetDirty(layout);
            }

            // ウェーブの計画も写しもとと共用にしない（1 つを直すと両方のステージが変わってしまう）。
            foreach (var director in Object.FindObjectsByType<EnemySpawnDirector>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(d => d.gameObject.scene == scene))
            {
                director.wavePlan = CopyWavePlan(director.wavePlan, sceneName, Path.GetFileNameWithoutExtension(source));
                EditorUtility.SetDirty(director);
            }

            // 背景（ステージ背景スタジオの Prefab）と光の設定（StageSet）も写しもとと共用にしない（ユーザー決定 2026-10-24）。
            foreach (var root in Object.FindObjectsByType<StageRoot>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.scene == scene))
            {
                CopyStageBackground(root, definition.DisplayName);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeObject = definition;
            Debug.Log($"[Stage] {sceneName} を作りました（{path}）。名前・カードの色は {AssetDatabase.GetAssetPath(definition)}。");
        }

        /// <summary>
        /// 新しいステージの番号: シーン（StageN）・設定ファイル（Data/Stage_N）・一覧の id / シーン名のどれにも使われていない最小の番号。
        /// シーンだけを見ると、消したシーンの番号を使い回して、ほかのステージの設定ファイルを上書きしてしまう（例: 古い Stage2 を消すと
        /// 次のステージが「お風呂場」の Stage_2 を乗っ取る）。
        /// </summary>
        internal static int NextFreeStageNumber(StageCatalog catalog)
        {
            var stages = (catalog != null ? catalog.stages : null) ?? new StageDefinition[0];
            for (var number = 1; ; number++)
            {
                var sceneName = "Stage" + number;
                var taken = File.Exists(StageAssets.StageScenePath(sceneName))
                            || File.Exists($"{StageAssets.DataFolder}/Stage_{number}.asset")
                            || stages.Any(s => s != null && (s.sceneName == sceneName || s.stageId == "stage" + number));
                if (!taken)
                {
                    return number;
                }
            }
        }

        /// <summary>
        /// 写したシーンの背景（ステージ背景スタジオの置き場所 root の Prefab とステージ StageSet）を新しいステージ用に複製し、複製を指すようにする。
        /// 背景の中の材質（Materials）は写しもとと共用のまま（材質の色を変えると両方が変わる）。
        /// </summary>
        private static void CopyStageBackground(StageRoot root, string displayName)
        {
            var sourceSet = root.current;
            if (sourceSet == null)
            {
                return;
            }

            var folder = AssetDatabase.GenerateUniqueAssetPath($"{StageAssetFactory.StagesFolder}/{StageAssetFactory.MakeSafeFileName(displayName)}");
            StageAssets.EnsureFolder(folder);
            var safe = Path.GetFileName(folder);

            GameObject prefab = null;
            var sourcePrefabPath = sourceSet.stagePrefab != null ? AssetDatabase.GetAssetPath(sourceSet.stagePrefab) : null;
            if (!string.IsNullOrEmpty(sourcePrefabPath) && AssetDatabase.CopyAsset(sourcePrefabPath, $"{folder}/Stage_{safe}.prefab"))
            {
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/Stage_{safe}.prefab");
            }

            var setPath = $"{folder}/StageSet_{safe}.asset";
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceSet), setPath))
            {
                Debug.LogWarning($"[Stage] 背景の設定「{sourceSet.name}」を写せませんでした。新しいステージの背景は写しもとと共用のままです。");
                return;
            }

            var set = AssetDatabase.LoadAssetAtPath<StageSet>(setPath);
            set.displayName = displayName;
            set.stagePrefab = prefab != null ? prefab : sourceSet.stagePrefab;
            set.cameraPreset = null; // 視点はステージの「配置の目印」が持つ
            set.memo = $"「{sourceSet.Label}」を写して作った（{System.DateTime.Now:yyyy-MM-dd}）。";
            EditorUtility.SetDirty(set);
            AssetDatabase.SaveAssets();

            // シーンの背景の実体を、複製した Prefab のものに付け替える（シーンで直した分＝上書きはそのまま残る）。
            if (prefab != null && root.instance != null && PrefabUtility.IsPartOfPrefabInstance(root.instance))
            {
                PrefabUtility.ReplacePrefabAssetOfPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(root.instance), prefab, InteractionMode.AutomatedAction);
            }

            root.current = set;
            EditorUtility.SetDirty(root);
            Debug.Log($"[Stage] 背景と光の設定を複製しました: {setPath}（材質は写しもとと共用）。");
        }

        /// <summary>写しもとのステージの視点を複製して、新しいステージ専用の視点にする（もとが空なら空のまま）。</summary>
        internal static CameraViewPreset CopyCameraPreset(CameraViewPreset source, string sceneName, string displayName, string sourceSceneName)
        {
            if (source == null)
            {
                return null;
            }

            var copy = CameraPresetEditing.CreatePresetAsset("CameraViewPreset_" + sceneName, source);
            copy.presetName = displayName + "の視点";
            copy.memo = $"{sourceSceneName} の視点（{source.name}）を写した物。";
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            return copy;
        }

        /// <summary>写しもとのステージのウェーブの計画を複製して、新しいステージ専用にする（もとが空なら空のまま）。</summary>
        internal static WavePlan CopyWavePlan(WavePlan source, string sceneName, string sourceSceneName)
        {
            if (source == null)
            {
                return null;
            }

            var folder = $"{StageAssets.DataFolder}/{sceneName}";
            if (!AssetDatabase.IsValidFolder(folder))
            {
                AssetDatabase.CreateFolder(StageAssets.DataFolder, sceneName);
            }

            var path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/WavePlan_{sceneName}.asset");
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), path))
            {
                Debug.LogWarning($"[Stage] ウェーブの計画「{source.name}」を写せませんでした。新しいステージは写しもとと共用のままです。");
                return source;
            }

            var copy = AssetDatabase.LoadAssetAtPath<WavePlan>(path);
            copy.memo = $"{sourceSceneName} の計画（{source.name}）を写した物。" + (string.IsNullOrEmpty(copy.memo) ? "" : " / " + copy.memo);
            EditorUtility.SetDirty(copy);
            AssetDatabase.SaveAssetIfDirty(copy);
            return copy;
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

                if (!game.GetRootGameObjects().Any(r => r.GetComponentInChildren<FortressCameraRig>(true) != null))
                {
                    issues.Add("Game にカメラリグ（FortressCameraRig）がありません（要塞デザイナー › カメラ › カメラリグをセットアップ。Game だけを開いて押すこと）。");
                }

                var presetUsers = new Dictionary<CameraViewPreset, List<string>>();
                var planUsers = new Dictionary<WavePlan, List<string>>();
                var backgroundUsers = new Dictionary<StageSet, List<string>>();

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

                    if (roots.Any(r => r.GetComponentInChildren<FortressCameraRig>(true) != null || r.GetComponentInChildren<Camera>(true) != null))
                    {
                        issues.Add($"「{stage.DisplayName}」: カメラ（カメラリグ・Camera）があります。カメラは Game にだけ置き、視点は「配置の目印」の cameraPreset に入れてください（Play では使われず、止められます）。");
                    }

                    var layout = roots.Select(r => r.GetComponentInChildren<StageLayout>(true)).FirstOrDefault(l => l != null);
                    if (layout != null && layout.cameraPreset == null)
                    {
                        issues.Add($"「{stage.DisplayName}」: 配置の目印に視点（cameraPreset）がありません。Game のカメラの視点のままになります。");
                    }
                    else if (layout != null)
                    {
                        if (!presetUsers.TryGetValue(layout.cameraPreset, out var users))
                        {
                            presetUsers[layout.cameraPreset] = users = new List<string>();
                        }

                        users.Add($"「{stage.DisplayName}」");
                    }

                    var director = roots.Select(r => r.GetComponentInChildren<EnemySpawnDirector>(true)).FirstOrDefault(d => d != null);
                    if (director != null && director.wavePlan == null)
                    {
                        issues.Add($"「{stage.DisplayName}」: ウェーブの計画（WAVE 1〜5）がありません。時間割1本をくり返し、撃破数でクリアになります（要塞デザイナー › 敵ウェーブで作れます）。");
                    }
                    else if (director != null)
                    {
                        var planIssues = WavePlanChecks.Check(director.wavePlan);
                        foreach (var issue in planIssues.Where(x => x.Level == WavePlanIssueLevel.Error))
                        {
                            issues.Add($"「{stage.DisplayName}」のウェーブの計画: {(issue.Wave >= 0 ? $"WAVE {issue.Wave + 1}: " : "")}{issue.Message}");
                        }

                        if (!planUsers.TryGetValue(director.wavePlan, out var users))
                        {
                            planUsers[director.wavePlan] = users = new List<string>();
                        }

                        users.Add($"「{stage.DisplayName}」");
                    }

                    var stageRoot = roots.Select(r => r.GetComponentInChildren<StageRoot>(true)).FirstOrDefault(r => r != null);
                    if (stageRoot == null || stageRoot.current == null)
                    {
                        issues.Add($"「{stage.DisplayName}」: ステージ背景スタジオの置き場所（[Stage]）かそのステージ（StageSet）がありません。光（太陽・環境光・霧）は背景スタジオの「光」ページで調整する決まりです（ステージ背景スタジオで作れます）。");
                    }
                    else
                    {
                        if (!backgroundUsers.TryGetValue(stageRoot.current, out var users))
                        {
                            backgroundUsers[stageRoot.current] = users = new List<string>();
                        }

                        users.Add($"「{stage.DisplayName}」");
                    }

                    EditorSceneManager.CloseScene(scene, true);
                }

                foreach (var pair in backgroundUsers.Where(p => p.Value.Count > 1))
                {
                    issues.Add($"背景と光の設定「{pair.Key.name}」を {string.Join("・", pair.Value)} が共用しています。1 つを直すとほかも変わります。");
                }

                foreach (var pair in planUsers.Where(p => p.Value.Count > 1))
                {
                    issues.Add($"ウェーブの計画「{pair.Key.name}」を {string.Join("・", pair.Value)} が共用しています。1 つを直すとほかも変わります。");
                }

                foreach (var pair in presetUsers.Where(p => p.Value.Count > 1))
                {
                    issues.Add($"視点「{pair.Key.name}」を {string.Join("・", pair.Value)} が共用しています。1 つを直すとほかも変わります（要塞デザイナー › カメラ「このステージ専用にする」）。");
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
