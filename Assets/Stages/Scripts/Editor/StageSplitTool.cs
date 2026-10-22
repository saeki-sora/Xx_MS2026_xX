using System.Collections.Generic;
using System.IO;
using System.Linq;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Stage;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Stages.EditorTools
{
    /// <summary>
    /// 今の Game シーンを「共通（Game）」と「ステージの中身（Stage1）」に分け、仮の Stage2 を作る道具（1 回だけ使う）。
    /// ステージ側へ移す物: ステージの背景（`[Stage]`）、試合の決まり、要塞の下の 湧き位置・敵を出す係・群衆・障害物・経路。
    /// 共通に残す物: 通信・カメラ・UI・センサー・BGM・コア・砲台・スキルを配る係・番人。
    /// 移した後、コアと砲台の今の位置・向きと、カメラの視点をステージの「配置の目印」（`[Stage] Layout`）に写す。
    /// 物はシーンの間で動かすだけ（作り直さない）なので、設定・参照はそのまま。共通とステージの間の参照は無いことを確かめてある（2026-10-21）。
    /// </summary>
    internal static class StageSplitTool
    {
        public const string ContentRootName = "[Stage] Content";
        public const string LayoutName = "[Stage] Layout";
        public const string BootstrapName = "[Stage] Bootstrap";

        [MenuItem("Tools/ステージ/今の Game を共通と Stage1 に分ける（1回だけ）", false, 0)]
        private static void SplitMenu()
        {
            if (!EditorUtility.DisplayDialog("ステージに分ける",
                    "今の Game シーンを、\n" +
                    "・共通（Game）: 通信・カメラ・UI・コア・砲台など\n" +
                    "・Stage1: 背景・障害物・敵・群衆・経路・試合の決まり\n" +
                    "に分けます。仮の Stage2（障害物と湧き位置を左右反対、クリアの撃破数を増やした物）・ステージの一覧・ビルドのシーン一覧も作ります。\n\n" +
                    "Game シーンは書き換わります（Git で元に戻せます）。続けますか？",
                    "分ける", "やめる"))
            {
                return;
            }

            var report = Split();
            var text = string.Join("\n", report);
            Debug.Log("[Stage] ステージに分けました:\n" + text);
            EditorUtility.DisplayDialog("ステージに分ける", text, "OK");
        }

        [MenuItem("Tools/ステージ/今の Game を共通と Stage1 に分ける（1回だけ）", true)]
        private static bool SplitMenuValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>分ける。やったこと（または、できなかった理由）の一覧を返す。</summary>
        public static List<string> Split()
        {
            var report = new List<string>();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Add("やめました（保存していないシーンがあります）。");
                return report;
            }

            if (!File.Exists(StageAssets.GameScenePath))
            {
                report.Add($"Game シーン（{StageAssets.GameScenePath}）がありません。");
                return report;
            }

            var stage1Path = StageAssets.StageScenePath("Stage1");
            var stage2Path = StageAssets.StageScenePath("Stage2");
            if (File.Exists(stage1Path))
            {
                report.Add($"{stage1Path} がもうあります（分け済み）。やり直すときは、Git で Game を戻して Stage1/Stage2 を消してから押してください。");
                return report;
            }

            var game = EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Single);
            var movers = CollectStageObjects(game, out var fortressRoot);
            if (movers.Count == 0)
            {
                report.Add("Game シーンにステージの中身（群衆・障害物・敵を出す係など）が見つかりませんでした。分け済みかもしれません。");
                return report;
            }

            // ── Stage1 を作って移す ──
            StageAssets.EnsureFolder(StageAssets.StageSceneFolder);
            var stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            var content = new GameObject(ContentRootName);
            SceneManager.MoveGameObjectToScene(content, stage);
            if (fortressRoot != null)
            {
                content.transform.SetPositionAndRotation(fortressRoot.position, fortressRoot.rotation);
            }

            foreach (var item in movers)
            {
                var underFortress = fortressRoot != null && item.transform.parent == fortressRoot;
                item.transform.SetParent(null, true);
                SceneManager.MoveGameObjectToScene(item, stage);
                if (underFortress)
                {
                    item.transform.SetParent(content.transform, true);
                }

                report.Add($"Stage1 へ移した: {item.name}");
            }

            if (content.transform.childCount == 0)
            {
                Object.DestroyImmediate(content);
                content = null;
            }

            var layout = CreateLayout(stage, game);
            layout.content = content;
            report.Add("配置の目印（コア・砲台 4 台・カメラの視点）を今の Game の位置から作った。");

            EditorSceneManager.SaveScene(stage, stage1Path);
            report.Add($"保存: {stage1Path}");

            // ── Game に「ステージを読み込む係」を置く ──
            if (Object.FindObjectsByType<StageBootstrap>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(b => b.gameObject.scene != game))
            {
                var bootstrap = new GameObject(BootstrapName);
                SceneManager.MoveGameObjectToScene(bootstrap, game);
                bootstrap.AddComponent<StageBootstrap>();
                report.Add("Game に「ステージを読み込む係」（[Stage] Bootstrap）を置いた。");
            }

            EditorSceneManager.MarkSceneDirty(game);
            EditorSceneManager.SaveScene(game);
            report.Add($"保存: {StageAssets.GameScenePath}（共通）");

            // ── 設定ファイル・一覧 ──
            var catalog = StageAssets.LoadOrCreateCatalog();
            var def1 = StageAssets.CreateDefinition("Stage_1", "stage1", "ステージ1", "はじまりの要塞", "Stage1", new Color(0.35f, 0.85f, 1f));
            StageAssets.AddToCatalog(catalog, def1);
            layout.definition = def1;
            EditorUtility.SetDirty(layout);
            EditorSceneManager.MarkSceneDirty(stage);
            EditorSceneManager.SaveScene(stage);

            // ── 仮の Stage2（Stage1 を写して、障害物と湧き位置を左右反対に） ──
            EditorSceneManager.CloseScene(stage, true);
            if (AssetDatabase.CopyAsset(stage1Path, stage2Path))
            {
                var def2 = StageAssets.CreateDefinition("Stage_2", "stage2", "ステージ2（仮）", "障害物が左右反対", "Stage2", new Color(1f, 0.45f, 0.75f));
                StageAssets.AddToCatalog(catalog, def2);
                var stage2 = EditorSceneManager.OpenScene(stage2Path, OpenSceneMode.Additive);
                MakeVariant(stage2, def2, report);
                EditorSceneManager.SaveScene(stage2);
                EditorSceneManager.CloseScene(stage2, true);
                report.Add($"保存: {stage2Path}（仮のステージ。障害物と湧き位置を左右反対、クリアの撃破数を 1.5 倍）");
            }
            else
            {
                report.Add("Stage2 を作れませんでした（Stage1 を写せなかった）。");
            }

            AssetDatabase.SaveAssets();
            var added = StageAssets.SyncBuildSettings(catalog, report);
            report.Add($"ビルドのシーン一覧に {added} 個入れた（Game のすぐ後ろ）。");
            report.Add("ステージの一覧: " + StageAssets.CatalogPath);
            report.Add("次: Game を Play すると Stage1 が重なって始まります。Stage2 を試すときは一覧の「Default Stage Index」を 1 に。");

            // 作業しやすいように、Game と Stage1 を一緒に開いておく。
            EditorSceneManager.OpenScene(stage1Path, OpenSceneMode.Additive);
            return report;
        }

        /// <summary>Game の中の、ステージ側へ移す物（ルートか、要塞の直下）。</summary>
        private static List<GameObject> CollectStageObjects(Scene game, out Transform fortressRoot)
        {
            var result = new List<GameObject>();
            fortressRoot = null;
            var layoutController = Object.FindObjectsByType<FortressLayoutController>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.scene == game);
            if (layoutController != null)
            {
                fortressRoot = layoutController.transform;
            }

            foreach (var root in game.GetRootGameObjects())
            {
                if (root.GetComponent<StageRoot>() != null || root.GetComponent<FortressMatchRules>() != null)
                {
                    result.Add(root);
                }
            }

            var parent = fortressRoot;
            if (parent == null)
            {
                // 要塞の親が見つからないときは、コアの親を要塞とみなす。
                var core = Object.FindObjectsByType<CoreCrystalController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.scene == game);
                parent = core != null ? core.transform.parent : null;
                fortressRoot = parent;
            }

            if (parent == null)
            {
                return result;
            }

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i).gameObject;
                if (IsStageContent(child))
                {
                    result.Add(child);
                }
            }

            return result;
        }

        /// <summary>ステージの中身か（湧き位置・敵を出す係・群衆・経路・障害物を含み、コア・砲台を含まない）。</summary>
        private static bool IsStageContent(GameObject go)
        {
            if (go.GetComponentInChildren<CoreCrystalController>(true) != null || go.GetComponentInChildren<LaserTurret>(true) != null)
            {
                return false;
            }

            return go.GetComponentInChildren<EnemySpawnPoint>(true) != null
                   || go.GetComponentInChildren<EnemySpawnDirector>(true) != null
                   || go.GetComponentInChildren<SwarmSystem>(true) != null
                   || go.GetComponentInChildren<NavigationField>(true) != null
                   || go.GetComponentInChildren<NavigationObstacle>(true) != null
                   || go.GetComponentInChildren<DestructibleObstacle>(true) != null;
        }

        /// <summary>ステージに「配置の目印」を作る（コア・砲台の今の位置・向き、カメラの視点を写す）。</summary>
        private static StageLayout CreateLayout(Scene stage, Scene game)
        {
            var go = new GameObject(LayoutName);
            SceneManager.MoveGameObjectToScene(go, stage);
            var layout = go.AddComponent<StageLayout>();
            var core = Object.FindObjectsByType<CoreCrystalController>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(c => c.gameObject.scene == game);
            layout.coreAnchor = MakeAnchor(go.transform, "Core", core != null ? core.transform : null);
            layout.turretAnchors = new Transform[StageLayout.TurretCount];
            var turrets = Object.FindObjectsByType<LaserTurret>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(t => t.gameObject.scene == game).ToArray();
            for (var i = 0; i < StageLayout.TurretCount; i++)
            {
                var turret = turrets.FirstOrDefault(t => t.playerIndex == i);
                layout.turretAnchors[i] = MakeAnchor(go.transform, $"Turret_P{i + 1}", turret != null ? turret.transform : null);
            }

            var rig = Object.FindObjectsByType<FortressCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(r => r.gameObject.scene == game);
            layout.cameraPreset = rig != null ? rig.preset : null;
            return layout;
        }

        private static Transform MakeAnchor(Transform parent, string anchorName, Transform from)
        {
            var anchor = new GameObject(anchorName).transform;
            anchor.SetParent(parent, false);
            if (from != null)
            {
                anchor.SetPositionAndRotation(from.position, Quaternion.Euler(0f, 0f, from.eulerAngles.z));
            }

            return anchor;
        }

        /// <summary>仮のステージ: 障害物と湧き位置をコアを通る縦の線で左右反対にし、クリアの撃破数を 1.5 倍にする。</summary>
        private static void MakeVariant(Scene scene, StageDefinition definition, List<string> report)
        {
            var layout = Object.FindObjectsByType<StageLayout>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(l => l.gameObject.scene == scene);
            if (layout != null)
            {
                layout.definition = definition;
                EditorUtility.SetDirty(layout);
            }

            var centerX = layout != null && layout.coreAnchor != null ? layout.coreAnchor.position.x : 0f;
            var content = scene.GetRootGameObjects().FirstOrDefault(r => r.name == ContentRootName);
            var mirrored = 0;
            if (content != null)
            {
                foreach (var t in content.GetComponentsInChildren<Transform>(true))
                {
                    if (!IsMirrorTarget(t) || HasMirrorTargetAncestor(t, content.transform))
                    {
                        continue;
                    }

                    var p = t.position;
                    t.position = new Vector3(2f * centerX - p.x, p.y, p.z);
                    var e = t.eulerAngles;
                    t.rotation = Quaternion.Euler(e.x, e.y, -e.z);
                    EditorUtility.SetDirty(t);
                    mirrored++;
                }
            }

            report.Add($"Stage2: 障害物・湧き位置 {mirrored} 個を左右反対にした。");
            foreach (var rules in Object.FindObjectsByType<FortressMatchRules>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.gameObject.scene == scene))
            {
                rules.clearKills = Mathf.Max(1, Mathf.RoundToInt(rules.clearKills * 1.5f));
                EditorUtility.SetDirty(rules);
                report.Add($"Stage2: クリアの撃破数を {rules.clearKills} にした。");
            }

            EditorSceneManager.MarkSceneDirty(scene);
        }

        private static bool IsMirrorTarget(Transform t)
        {
            return t.GetComponent<NavigationObstacle>() != null || t.GetComponent<EnemySpawnPoint>() != null || t.GetComponent<DestructibleObstacle>() != null;
        }

        private static bool HasMirrorTargetAncestor(Transform t, Transform stopAt)
        {
            for (var p = t.parent; p != null && p != stopAt; p = p.parent)
            {
                if (IsMirrorTarget(p))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
