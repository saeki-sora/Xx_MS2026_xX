using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Stages.EditorTools
{
    /// <summary>
    /// 要塞デザイナーのカメラタブに「ステージごとの視点」を教える（<see cref="ICameraStageBinding"/> の実装。起動時に登録）。
    /// ・ステージが開いていれば、タブで編集・切り替えた視点はそのステージの「配置の目印」（<see cref="StageLayout.cameraPreset"/>）に入る。
    ///   Game のカメラリグには見せるだけ（<see cref="StageEditPreview"/>。Game のファイルは変えない）。
    /// ・ステージのシーンにカメラリグが紛れ込んでいたら、タブがエラーを出して消せるようにする。
    /// </summary>
    [InitializeOnLoad]
    internal sealed class StageCameraBinding : ICameraStageBinding
    {
        private const double CacheSeconds = 2.0;

        private static readonly Dictionary<string, (double time, string[] presets)> DependencyCache = new Dictionary<string, (double, string[])>();

        static StageCameraBinding()
        {
            CameraStageBinding.Current = new StageCameraBinding();
        }

        public bool IsStageScene(Scene scene)
        {
            return scene.IsValid() && scene.isLoaded && scene.GetRootGameObjects().Any(r => r.GetComponentInChildren<StageLayout>(true) != null);
        }

        public CameraStageInfo FindOpenStage()
        {
            StageLayout layout;
            if (Application.isPlaying)
            {
                layout = StageLayout.Active;
            }
            else
            {
                var layouts = Object.FindObjectsByType<StageLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                var active = SceneManager.GetActiveScene();
                layout = layouts.FirstOrDefault(l => l.gameObject.scene == active) ?? layouts.FirstOrDefault();
            }

            if (layout == null)
            {
                return null;
            }

            var scene = layout.gameObject.scene;
            return new CameraStageInfo
            {
                DisplayName = layout.definition != null ? layout.definition.DisplayName : scene.name,
                SceneName = scene.name,
                ScenePath = scene.path,
                Preset = layout.cameraPreset,
                Owner = layout
            };
        }

        public CameraStageInfo FindStageForPlay()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageAssets.CatalogPath);
            if (catalog == null || catalog.Count == 0)
            {
                return null;
            }

            // StageBootstrap と同じ順（起動引数はエディタの Play には無いので、一覧の既定 → 最初の物）。
            var definition = catalog.Get(catalog.defaultStageIndex) ?? (catalog.stages ?? new StageDefinition[0]).FirstOrDefault(s => s != null);
            if (definition == null)
            {
                return null;
            }

            var path = StageAssets.FindScenePath(definition.sceneName);
            return new CameraStageInfo
            {
                DisplayName = definition.DisplayName,
                SceneName = definition.sceneName,
                ScenePath = path,
                Preset = path != null ? PresetsUsedBy(path).Select(LoadPreset).FirstOrDefault(p => p != null) : null
            };
        }

        public void AssignPreset(CameraStageInfo stage, CameraViewPreset preset)
        {
            if (Application.isPlaying || stage == null || !(stage.Owner is StageLayout layout) || layout == null)
            {
                return;
            }

            if (layout.cameraPreset != preset)
            {
                Undo.RecordObject(layout, "ステージの視点を変える");
                layout.cameraPreset = preset;
                EditorUtility.SetDirty(layout);
                EditorSceneManager.MarkSceneDirty(layout.gameObject.scene);
                DependencyCache.Clear();
                Debug.Log($"[Stage] ステージ「{layout.gameObject.scene.name}」の視点を「{(preset != null ? preset.name : "なし")}」にしました（ステージのシーンを保存すると残ります）。");
            }

            // Game のリグにも見せる（元の視点は覚えておき、Game を保存するときは元に戻して保存する）。
            StageEditPreview.Place(layout, false);
        }

        public IReadOnlyList<string> OtherStagesUsing(CameraStageInfo stage, CameraViewPreset preset)
        {
            var result = new List<string>();
            var presetGuid = preset != null ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(preset)) : null;
            var catalog = AssetDatabase.LoadAssetAtPath<StageCatalog>(StageAssets.CatalogPath);
            if (string.IsNullOrEmpty(presetGuid) || catalog == null)
            {
                return result;
            }

            foreach (var definition in catalog.stages ?? new StageDefinition[0])
            {
                if (definition == null || (stage != null && definition.sceneName == stage.SceneName))
                {
                    continue;
                }

                var path = StageAssets.FindScenePath(definition.sceneName);
                if (path != null && PresetsUsedBy(path).Contains(presetGuid))
                {
                    result.Add($"「{definition.DisplayName}」");
                }
            }

            return result;
        }

        public void OpenCommonScene()
        {
            if (Application.isPlaying)
            {
                return;
            }

            var active = SceneManager.GetActiveScene();
            var game = SceneManager.GetSceneByPath(StageAssets.GameScenePath);
            if (!game.isLoaded)
            {
                EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Additive);
            }

            if (active.IsValid())
            {
                SceneManager.SetActiveScene(active);
            }
        }

        public void OpenWithCommonScene(CameraStageInfo stage)
        {
            if (Application.isPlaying || stage == null || string.IsNullOrEmpty(stage.ScenePath) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            if (!SceneManager.GetSceneByPath(StageAssets.GameScenePath).isLoaded)
            {
                EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Single);
            }

            var scene = EditorSceneManager.OpenScene(stage.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
        }

        /// <summary>シーンのファイルが直接参照している視点プリセットの GUID（数秒だけ覚える。カメラタブは何度も描き直すので）。</summary>
        internal static string[] PresetsUsedBy(string scenePath)
        {
            var now = EditorApplication.timeSinceStartup;
            if (DependencyCache.TryGetValue(scenePath, out var cached) && now - cached.time < CacheSeconds)
            {
                return cached.presets;
            }

            var presets = AssetDatabase.GetDependencies(scenePath, false)
                .Where(p => AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(CameraViewPreset))
                .Select(AssetDatabase.AssetPathToGUID)
                .ToArray();
            DependencyCache[scenePath] = (now, presets);
            return presets;
        }

        private static CameraViewPreset LoadPreset(string guid)
        {
            return AssetDatabase.LoadAssetAtPath<CameraViewPreset>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
