using System.Collections.Generic;
using System.IO;
using System.Linq;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Stage;
using MS2026.Stage.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Stages.EditorTools
{
    /// <summary>
    /// ステージ2「お風呂場」のシーン（<see cref="SceneName"/>）を新しく作る道具（ステージ案② / 実装計画 2026-10-22）。
    /// 画面の上が壁（タイル）、真ん中がバスタブ（ふち 2 本・すき間 1 つ・橋）、下が床。仮の箱で作り、絵は後で差し替える。
    /// 群衆・経路・敵を出す係・試合の決まりは Stage1 の物を写して設定を引き継ぎ、障害物・湧き位置・目印は下の配置で作り直す。
    /// 光（太陽・環境光・霧）はステージ背景スタジオの置き場所（[Stage]）とお風呂場用のステージ（<see cref="StageSetPath"/>）に持たせ、
    /// 初めて作るときは Stage1 の光（＝分ける前の Game の光）を写す。調整は背景スタジオの「光」ページで行う（ユーザー決定 2026-10-24）。
    /// </summary>
    internal static class BathStageBuilder
    {
        public const string SceneName = "Stage2_Bath";
        public const string DataFolder = "Assets/Stages/Data/Stage2";
        public const string WavePath = DataFolder + "/Stage2_Wave1.asset";
        public const string CameraPresetPath = "Assets/_Game/Fortress/Presets/Camera/CameraViewPreset_Stage2.asset";
        private const string CameraSourcePath = "Assets/_Game/Fortress/Presets/Camera/CameraViewPreset_CoreBottom.asset";
        private const string PresetFolder = "Assets/_Game/Fortress/Presets";
        public const string StageSetName = "お風呂場";
        public const string StageSetPath = "Assets/_Game/Stage/Stages/お風呂場/StageSet_お風呂場.asset";

        // ── 配置（目安。画面はおよそ横 50m × 縦 28m。数値は Unity で見ながら直してよい） ──
        // カメラの中心。プランナー案（2026-10-22）で、画面の中のステージ全体を少し上げるため -0.5 → -3.5（画面の下は HUD で隠れる）。
        private static readonly Vector2 ViewCenter = new Vector2(0f, -3.5f);
        // 敵が動ける範囲：上は湧き位置の上（y 14）、下は床の下の端まで。
        private static readonly Vector2 NavAreaCenter = new Vector2(0f, (14f + FloorBottom) * 0.5f);
        private static readonly Vector2 NavAreaSize = new Vector2(50f, 14f - FloorBottom);
        private const float RimY = 2f;
        private const float GapLeft = -5f;
        private const float GapRight = 4f;
        private const float TubHalfWidth = 25f;
        private const float TubWallBottom = -8f;
        // 床の下の端（プランナー案 2026-10-22: 床の帯を半分くらいに。画面の下は HUD〈自分のパネル・撃破数〉で隠れるため）。
        private const float FloorBottom = -11.25f;
        private const float BridgeY = -6.5f; // 橋の上の面がコア（大きさ 2.5）の下の端に届く高さ
        // 橋の幅（プランナー案 2026-10-22: 横幅を縮め、コアの真下を中心にする。前は -17〜15）。
        private const float BridgeLeft = -11f;
        private const float BridgeRight = 11f;
        private const float BridgeHealth = 600f;
        private static readonly Vector2 CorePosition = new Vector2(0f, -5f);
        private static readonly Vector2 CoreDropPosition = new Vector2(0f, -9.5f); // 床の上。撃破数（画面の下の真ん中）に隠れない高さ
        private static readonly Vector2 SmashBallPosition = new Vector2(8f, -2f); // バスタブの中（浮いて動き回る）

        private static readonly (string id, Vector2 position)[] SpawnPoints =
        {
            ("Spawn_TopL", new Vector2(-13f, 9f)), // カメラを下げても画面に入る高さ
            ("Spawn_TopR", new Vector2(14f, 9f)),
            ("Spawn_SideL", new Vector2(-23f, -4f)),
            ("Spawn_SideR", new Vector2(23f, -3f)),
        };

        private static readonly Vector2[] TurretPositions =
        {
            new Vector2(-13f, 4f), // P1 赤（ふちの上・左）
            new Vector2(15f, 4f), // P2 青（ふちの上・右）
            new Vector2(-14f, -9.5f), // P3 緑（床・左。左の他キャラのアイコンの右横。左下の自分のパネル・アイコンに隠れないように）
            new Vector2(14f, -9.5f), // P4 黄（床・右。緑の左右反対の位置）
        };

        public static string ScenePath => StageAssets.StageScenePath(SceneName);

        [MenuItem("Tools/ステージ/お風呂場（ステージ2）のシーンを作る", false, 30)]
        private static void BuildMenu()
        {
            var exists = File.Exists(ScenePath);
            var message = exists
                ? $"{ScenePath} はもうあります。作り直すと、手で動かした障害物・湧き位置・目印は元の配置に戻ります（Git で戻せます）。作り直しますか？"
                : $"ステージ2「お風呂場」のシーンを新しく作ります（{ScenePath}）。\n\n" +
                  "・上の壁に湧き位置 2、バスタブの横に湧き位置 2\n" +
                  "・バスタブのふち 2 本（敵は止める・ビームは通す）とバスタブの左右の壁\n" +
                  "・コアが乗る橋（ビームで壊れる・再生しない）\n" +
                  "・コア・砲台 4 台の目印と、全員同じ向きのカメラの視点\n" +
                  "・ステージ背景スタジオの置き場所と太陽（光は Stage1 と同じ値から始める）\n\n" +
                  "ルーレットのステージ2がこのシーンになります。";
            if (!EditorUtility.DisplayDialog("お風呂場（ステージ2）", message, exists ? "作り直す" : "作る", "やめる"))
            {
                return;
            }

            var report = Build();
            var text = string.Join("\n", report);
            Debug.Log("[Stage] お風呂場（ステージ2）:\n" + text);
            EditorUtility.DisplayDialog("お風呂場（ステージ2）", text, "OK");
        }

        [MenuItem("Tools/ステージ/お風呂場（ステージ2）のシーンを作る", true)]
        private static bool BuildMenuValidate() => !EditorApplication.isPlayingOrWillChangePlaymode;

        /// <summary>シーン・設定ファイルを作る。やったこと（または、できなかった理由）の一覧を返す。</summary>
        public static List<string> Build()
        {
            var report = new List<string>();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                report.Add("やめました（保存していないシーンがあります）。");
                return report;
            }

            var sourcePath = StageAssets.StageScenePath("Stage1");
            if (!File.Exists(sourcePath))
            {
                report.Add($"{sourcePath} がありません。先に「今の Game を共通と Stage1 に分ける」を押してください（群衆などの設定を Stage1 から写すため）。");
                return report;
            }

            StageAssets.EnsureFolder(StageAssets.StageSceneFolder);
            StageAssets.EnsureFolder(DataFolder);
            var wave = LoadOrCreateWave(report);
            var cameraPreset = LoadOrCreateCameraPreset(report);
            var definition = StageAssets.CreateDefinition("Stage_2", "stage2", "お風呂場", "橋が崩れるとコアが落ちる", SceneName, new Color(0.55f, 0.8f, 1f));
            report.Add($"ステージの設定 {StageAssets.DataFolder}/Stage_2.asset: 名前「お風呂場」・シーン {SceneName} にした。");

            var source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            // ── Stage1 から写す物（設定を引き継ぐ） ──
            var content = NewRoot(scene, StageSplitTool.ContentRootName);
            CopyFrom<SwarmSystem>(source, scene, content.transform, "SwarmSystem", report);
            var field = CopyFrom<NavigationField>(source, scene, content.transform, "NavigationField", report);
            var director = CopyFrom<EnemySpawnDirector>(source, scene, content.transform, "EnemySpawnDirector", report);
            CopyFrom<FortressMatchRules>(source, scene, null, "[Fortress] Match Rules", report);
            SetUpStageRoot(source, report);
            var destructibleSource = FindIn<DestructibleObstacle>(source).FirstOrDefault(d => d.GetComponent<SmashBallModule>() == null);

            if (field != null)
            {
                field.areaCenter = NavAreaCenter;
                field.areaSize = NavAreaSize;
            }

            // ── 湧き位置 ──
            var spawnRoot = NewChild(content.transform, "SpawnPoints", Vector2.zero);
            var points = new List<EnemySpawnPoint>();
            foreach (var (id, position) in SpawnPoints)
            {
                var point = NewChild(spawnRoot.transform, id, position).AddComponent<EnemySpawnPoint>();
                point.spawnPointId = id;
                points.Add(point);
            }

            if (director != null)
            {
                director.wave = wave;
                director.spawnPoints = points.ToArray();
            }

            report.Add($"湧き位置: {string.Join(" / ", SpawnPoints.Select(s => s.id))}");

            // ── 障害物（ふち・バスタブの左右の壁・橋） ──
            var obstacles = NewChild(content.transform, "Obstacles", Vector2.zero);
            var rimColor = new Color(0.9f, 0.92f, 0.95f);
            // ふちはバスタブの壁までつなげ、敵が下へ降りられるのは真ん中のすき間だけにする。
            Wall(obstacles.transform, "Rim_L", -TubHalfWidth, GapLeft, RimY - 0.5f, RimY + 0.5f, rimColor, true);
            Wall(obstacles.transform, "Rim_R", GapRight, TubHalfWidth, RimY - 0.5f, RimY + 0.5f, rimColor, true);
            var tubWallColor = new Color(0.42f, 0.44f, 0.5f);
            Wall(obstacles.transform, "TubWall_L", -TubHalfWidth - 0.5f, -TubHalfWidth + 0.5f, TubWallBottom, RimY + 0.5f, tubWallColor, false);
            Wall(obstacles.transform, "TubWall_R", TubHalfWidth - 0.5f, TubHalfWidth + 0.5f, TubWallBottom, RimY + 0.5f, tubWallColor, false);
            report.Add("ふち Rim_L / Rim_R（当たり判定はトリガー＝敵は止める・ビームは通す）、バスタブの壁 TubWall_L / TubWall_R を置いた。");

            var bridge = Bridge(obstacles.transform, destructibleSource);
            report.Add($"橋 Bridge（壊れる障害物・体力 {BridgeHealth}・再生しない・敵の通り道はふさがない）を置いた。");
            CopySmashBall(source, obstacles.transform, report);

            // ── 仮の見た目（当たり判定なし） ──
            var look = NewRoot(scene, "[Stage] 仮の見た目");
            Panel(look.transform, "TileWall", -TubHalfWidth, TubHalfWidth, RimY + 0.5f, 15f, new Color(0.8f, 0.87f, 0.9f));
            Panel(look.transform, "TubInside", -TubHalfWidth + 0.5f, TubHalfWidth - 0.5f, TubWallBottom, RimY - 0.5f, new Color(0.6f, 0.82f, 0.95f));
            Panel(look.transform, "Floor", -TubHalfWidth, TubHalfWidth, FloorBottom, TubWallBottom, new Color(0.72f, 0.7f, 0.66f));

            // ── 配置の目印 ──
            var layoutGo = NewRoot(scene, StageSplitTool.LayoutName);
            var layout = layoutGo.AddComponent<StageLayout>();
            layout.definition = definition;
            layout.content = content;
            layout.cameraPreset = cameraPreset;
            layout.coreAnchor = NewChild(layoutGo.transform, "Core", CorePosition).transform;
            layout.turretAnchors = new Transform[StageLayout.TurretCount];
            for (var i = 0; i < StageLayout.TurretCount; i++)
            {
                var anchor = NewChild(layoutGo.transform, $"Turret_P{i + 1}", TurretPositions[i]).transform;
                anchor.rotation = Quaternion.Euler(0f, 0f, FacingDegrees(TurretPositions[i], CorePosition));
                layout.turretAnchors[i] = anchor;
            }

            var coreDrop = NewChild(layoutGo.transform, "CoreDrop", CoreDropPosition).transform;
            report.Add("配置の目印: コア＝橋の上、P1/P2＝ふちの上、P3/P4＝床（向きはコアの方）。コアが落ちる先の目印 CoreDrop も置いた。");

            // 橋が壊れたらコアが落ちる仕掛け（中身の下に置き、Game がそろってから働かせる）。
            var drop = NewChild(content.transform, "StageCoreDrop", Vector2.zero).AddComponent<StageCoreDrop>();
            drop.bridge = bridge;
            drop.dropTarget = coreDrop;
            report.Add("仕掛け StageCoreDrop: 橋が壊れたらコアが CoreDrop へ落ちる。");

            if (File.Exists(ScenePath))
            {
                AssetDatabase.DeleteAsset(ScenePath);
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            report.Add($"保存: {ScenePath}");
            EditorSceneManager.CloseScene(source, true);

            var catalog = StageAssets.LoadOrCreateCatalog();
            StageAssets.AddToCatalog(catalog, definition);
            AssetDatabase.SaveAssets();
            var added = StageAssets.SyncBuildSettings(catalog, report);
            report.Add($"ビルドのシーン一覧に {added} 個入れた。");
            report.Add("次: Tools › ステージ › チェック → 一覧の「Default Stage Index」を 1 にして Game を Play（または Stage2_Bath を開いて Play）。");

            EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Single);
            var opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(opened);
            Selection.activeObject = bridge != null ? bridge.gameObject : null;
            return report;
        }

        /// <summary>砲台の向き（Z 回転）: 砲台の上（transform.up）が target を向く角度。</summary>
        private static float FacingDegrees(Vector2 from, Vector2 target)
        {
            var d = target - from;
            return Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f;
        }

        private static GameObject NewRoot(Scene scene, string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }

        private static GameObject NewChild(Transform parent, string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            return go;
        }

        private static IEnumerable<T> FindIn<T>(Scene scene) where T : Component
        {
            return scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        }

        /// <summary>Stage1 にある T の物を写す（設定・アセットの参照はそのまま）。無ければ新しく作る。</summary>
        private static T CopyFrom<T>(Scene source, Scene scene, Transform parent, string name, List<string> report) where T : Component
        {
            var original = FindIn<T>(source).FirstOrDefault();
            GameObject go;
            if (original != null)
            {
                go = Object.Instantiate(original.gameObject);
                SceneManager.MoveGameObjectToScene(go, scene);
                report.Add($"Stage1 から写した: {name}");
            }
            else
            {
                go = new GameObject(name);
                SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<T>();
                report.Add($"Stage1 に {name} が無かったので新しく作った（設定を確かめること）。");
            }

            go.name = name;
            if (parent != null)
            {
                go.transform.SetParent(parent, true);
            }

            var component = go.GetComponent<T>();
            if (component is SwarmSystem swarm && swarm.settings == null)
            {
                swarm.settings = AssetDatabase.LoadAssetAtPath<SwarmSettings>(PresetFolder + "/Default_SwarmSettings.asset");
            }

            return component;
        }

        /// <summary>
        /// ステージ背景スタジオの置き場所（[Stage]）を今のシーン（作っているシーン）に作り、お風呂場用のステージを出す。
        /// ステージ（StageSet）が無ければ作り、光と反応の共通設定を Stage1 のステージから写す（あれば今の物をそのまま使う）。
        /// 出すと、太陽（[Stage] Sun）と環境光・霧がステージの光の値になる（背景スタジオの「シーンに出す」と同じ）。
        /// </summary>
        private static void SetUpStageRoot(Scene source, List<string> report)
        {
            var set = AssetDatabase.LoadAssetAtPath<StageSet>(StageSetPath);
            if (set == null)
            {
                set = StageAssetFactory.CreateStage(StageSetName);
                var from = FindIn<StageRoot>(source).Select(r => r.current).FirstOrDefault(c => c != null);
                if (from != null)
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from.lighting), set.lighting);
                    set.lookProfile = from.lookProfile != null ? from.lookProfile : set.lookProfile;
                }

                set.memo = $"{SceneName}（ステージ2「お風呂場」）用。光は Stage1 の「{(from != null ? from.Label : "既定値")}」から写した。";
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
                report.Add($"ステージ背景スタジオのステージ {AssetDatabase.GetAssetPath(set)} を作った（光は Stage1 から写した）。");
            }

            var root = new GameObject(StageSceneService.RootName).AddComponent<StageRoot>();
            StageSceneService.PlaceStage(root, set);
            report.Add($"ステージ背景スタジオの置き場所 {StageSceneService.RootName} と太陽を置いた（光の調整は背景スタジオの「光」ページ）。");
        }

        /// <summary>壊れない壁（仮の四角）。passBeams なら当たり判定をトリガーにして、ビームだけ通す（敵の経路では壁のまま）。</summary>
        private static void Wall(Transform parent, string name, float xMin, float xMax, float yMin, float yMax, Color color, bool passBeams)
        {
            var go = NewChild(parent, name, new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f));
            go.transform.localScale = new Vector3(xMax - xMin, yMax - yMin, 1f);
            var visual = go.AddComponent<PlaceholderVisual>();
            visual.shape = PlaceholderShape.Square;
            visual.color = color;
            visual.sortingOrder = -1;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = passBeams;
            go.AddComponent<NavigationObstacle>().mode = NavigationObstacleMode.Block;
            visual.Apply();
        }

        /// <summary>Stage1 のスマッシュボール（設定・演出ごと）を写して、バスタブの中に置く。浮いて動く範囲は経路の範囲に合わせる設定のまま。</summary>
        private static void CopySmashBall(Scene source, Transform parent, List<string> report)
        {
            var original = FindIn<SmashBallModule>(source).FirstOrDefault();
            if (original == null)
            {
                report.Add("Stage1 にスマッシュボールが無かったので、置かなかった。");
                return;
            }

            var copy = Object.Instantiate(original.gameObject);
            copy.name = "SmashBall_0";
            copy.transform.SetParent(parent, false);
            copy.transform.position = new Vector3(SmashBallPosition.x, SmashBallPosition.y, 0f);
            report.Add($"Stage1 のスマッシュボールを写して SmashBall_0 を {SmashBallPosition} に置いた。");
        }

        /// <summary>見た目だけの仮の板（当たり判定なし。敵・ビームに関係しない）。地面より少し奥に置く。</summary>
        private static void Panel(Transform parent, string name, float xMin, float xMax, float yMin, float yMax, Color color)
        {
            var go = NewChild(parent, name, new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f));
            go.transform.position += new Vector3(0f, 0f, 0.05f);
            go.transform.localScale = new Vector3(xMax - xMin, yMax - yMin, 1f);
            var visual = go.AddComponent<PlaceholderVisual>();
            visual.shape = PlaceholderShape.Square;
            visual.color = color;
            visual.sortingOrder = -20;
            visual.Apply();
        }

        /// <summary>コアが乗る橋。Stage1 の壊れる物の設定（演出など）を写し、体力・再生なし・通り道をふさがないにする。</summary>
        private static DestructibleObstacle Bridge(Transform parent, DestructibleObstacle template)
        {
            var go = NewChild(parent, "Bridge", new Vector2((BridgeLeft + BridgeRight) * 0.5f, BridgeY));
            go.transform.localScale = new Vector3(BridgeRight - BridgeLeft, 1f, 1f);
            // 壊れる障害物の仮の見た目は、同じ物に付いた PlaceholderVisual を使う（無いと何も描かれない）。
            var placeholder = go.AddComponent<PlaceholderVisual>();
            placeholder.shape = PlaceholderShape.Square;
            placeholder.color = new Color(0.45f, 0.8f, 0.5f);
            placeholder.sortingOrder = -1;
            placeholder.Apply();
            var bridge = go.AddComponent<DestructibleObstacle>();
            bridge.EnsureRequiredComponents();
            if (template != null)
            {
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(template.settings), bridge.settings);
            }

            var settings = bridge.settings;
            settings.durability.maxHealth = BridgeHealth;
            settings.durability.regenerates = false;
            settings.durability.selfRepairPerSecond = 0f;
            settings.collision.blocksEnemies = false;
            settings.collision.leavesRubble = false;
            settings.visual.visualPrefab = null;
            if (settings.feedback.onDestroyed.IsEmpty)
            {
                // 今の仮の破壊演出（爆弾スピキの爆発と同じ VFX・SE の番号札）。
                var bomb = AssetDatabase.LoadAssetAtPath<EnemyTypeDefinition>(PresetFolder + "/Bomb_EnemyType.asset");
                if (bomb != null)
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(bomb.bomb.onExplode), settings.feedback.onDestroyed);
                }
            }

            EditorUtility.SetDirty(bridge);
            return bridge;
        }

        /// <summary>仮の敵の時間割（上の出口を多め・横を少なめ）。もうあれば中身はそのまま使う（プランナーが直した値を消さない）。</summary>
        private static EnemyWaveConfig LoadOrCreateWave(List<string> report)
        {
            var wave = AssetDatabase.LoadAssetAtPath<EnemyWaveConfig>(WavePath);
            if (wave != null)
            {
                return wave;
            }

            var spiki = AssetDatabase.LoadAssetAtPath<EnemyTypeDefinition>(PresetFolder + "/Default_EnemyType.asset");
            var bomb = AssetDatabase.LoadAssetAtPath<EnemyTypeDefinition>(PresetFolder + "/Bomb_EnemyType.asset");
            wave = ScriptableObject.CreateInstance<EnemyWaveConfig>();
            wave.waveName = "お風呂場 WAVE1（仮）";
            wave.loop = true;
            wave.loopInterval = 3f;
            wave.spawnEntries = new List<EnemySpawnEntry>
            {
                Entry(0f, "Spawn_TopL", spiki, 40, 0.05f),
                Entry(0f, "Spawn_TopR", spiki, 40, 0.05f),
                Entry(4f, "Spawn_SideL", spiki, 10, 0.2f),
                Entry(4f, "Spawn_SideR", spiki, 10, 0.2f),
                Entry(8f, "Spawn_TopL", bomb, 3, 0.5f),
                Entry(8f, "Spawn_TopR", bomb, 3, 0.5f),
            };
            AssetDatabase.CreateAsset(wave, WavePath);
            report.Add($"仮の敵の時間割を作った: {WavePath}（くり返す）");
            return wave;
        }

        private static EnemySpawnEntry Entry(float time, string point, EnemyTypeDefinition type, int count, float interval)
        {
            return new EnemySpawnEntry { triggerTime = time, spawnPointId = point, enemyType = type, count = count, intervalBetweenSpawns = interval };
        }

        /// <summary>全員同じ向き（回転なし・上が壁・下が床）で全体が入る視点。今の視点（歯が画面の下）の画角・距離を写して作る。</summary>
        private static CameraViewPreset LoadOrCreateCameraPreset(List<string> report)
        {
            var preset = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(CameraPresetPath);
            if (preset != null)
            {
                return preset;
            }

            var template = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(CameraSourcePath);
            preset = template != null ? Object.Instantiate(template) : ScriptableObject.CreateInstance<CameraViewPreset>();
            preset.presetName = "お風呂場（全員同じ向き・上が壁）";
            preset.memo = "ステージ2用(2026-10-22)。4人とも回転なしで、画面の上が壁・下が床。画角・距離は CameraViewPreset_CoreBottom と同じ。";
            preset.overview = Centered(preset.overview);
            preset.EnsurePlayerSlots();
            for (var i = 0; i < preset.players.Length; i++)
            {
                preset.players[i] = Centered(preset.players[i]);
            }

            AssetDatabase.CreateAsset(preset, CameraPresetPath);
            report.Add($"カメラの視点を作った: {CameraPresetPath}");
            return preset;
        }

        private static CameraViewSettings Centered(CameraViewSettings view)
        {
            view.center = ViewCenter;
            view.rollDegrees = 0f;
            return view;
        }
    }
}
