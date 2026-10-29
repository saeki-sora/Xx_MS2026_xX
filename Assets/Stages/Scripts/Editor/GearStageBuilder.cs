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
    /// ステージ3「歯車ステージ」のシーン（<see cref="SceneName"/>）を新しく作る道具（ステージ案「ステージがいっぱい動く案①」、2026-10-29）。
    /// 外まわりの通路（ぐるっと一周）と、コアのある真ん中の広場へ入る左右の通路。湧き位置は上と下。
    /// 外まわりの通路の 4 か所に「歯車の門」（棒の腕つきの歯車。<see cref="GimmickGear"/>）があり、決まった時刻に 90° 回って道を開け閉めする。
    /// 2 つが開いて 2 つが閉じているので、回るたびに上の敵は左回り ↔ 右回り、下の敵は右回り ↔ 左回りに道が変わる（遠回りの時間と近道の時間）。
    /// 左右の通路には、壁から出たり引っ込んだりする動く壁（<see cref="GimmickMover"/>）があり、通れる幅が変わる（流れる量が変わる）。
    /// 敵の仕組みは変えていない（仕掛けが動くと経路が作り直され、敵はそれに従うだけ）。
    /// 当たり判定は見えない四角（要塞デザイナーの「経路・障害物」タブで直せる）、見た目はステージ背景スタジオの背景（3D モデル。差し替えてよい）。
    /// 群衆・経路・敵を出す係・試合の決まりは Stage1 の物を、ウェーブの計画・カメラの視点は Stage2 の物を写す（共用しない）。
    /// </summary>
    internal static class GearStageBuilder
    {
        public const string SceneName = "Stage3_Gear";
        public const string DataFolder = "Assets/Stages/Data/Stage3";
        public const string WavePlanPath = DataFolder + "/WavePlan_Stage3_Gear.asset";
        public const string CameraPresetPath = "Assets/_Game/Fortress/Presets/Camera/CameraViewPreset_" + SceneName + ".asset";
        public const string StageSetName = "歯車";
        public const string StageSetPath = "Assets/_Game/Stage/Stages/歯車/StageSet_歯車.asset";
        public const string ModelFolder = "Assets/_Game/Stage/Models/Stage03_Gear";
        private const string MaterialFolder = ModelFolder + "/Materials";
        private const string PresetFolder = "Assets/_Game/Fortress/Presets";

        // ── 配置（make_models.py と同じ値。画面はおよそ横 50m × 縦 28m、カメラの中心は (0, -3.5)） ──
        private static readonly Vector2 CorePosition = new Vector2(0f, -2.5f);
        private static readonly Vector2 NavAreaCenter = new Vector2(0f, -2.5f);
        private static readonly Vector2 NavAreaSize = new Vector2(36f, 29f);

        private static readonly (string id, Vector2 position)[] SpawnPoints =
        {
            ("Spawn_Top", new Vector2(4f, 10.5f)),
            ("Spawn_Bottom", new Vector2(-4f, -15.5f)),
        };

        // 島（通路の間の壁。ビームは通す）
        private static readonly (string name, float x0, float x1, float y0, float y1)[] Islands =
        {
            ("Block_NW", -12.5f, -4f, -0.5f, 2.5f),
            ("Block_N", -4f, 4f, 1.5f, 2.5f),
            ("Block_NE", 4f, 12.5f, -0.5f, 2.5f),
            ("Block_SW", -12.5f, -4f, -7.5f, -4.5f),
            ("Block_S", -4f, 4f, -7.5f, -6.5f),
            ("Block_SE", 4f, 12.5f, -7.5f, -4.5f),
        };

        // 外のふち・湧き位置の通路の壁（ビームも止める）
        private static readonly (string name, float x0, float x1, float y0, float y1)[] Rims =
        {
            ("Rim_TopL", -17.5f, 1f, 6.5f, 7.5f),
            ("Rim_TopR", 7f, 17.5f, 6.5f, 7.5f),
            ("Rim_BottomL", -17.5f, -7f, -12.5f, -11.5f),
            ("Rim_BottomR", -1f, 17.5f, -12.5f, -11.5f),
            ("Rim_Left", -17.5f, -16.5f, -11.5f, 6.5f),
            ("Rim_Right", 16.5f, 17.5f, -11.5f, 6.5f),
            ("Lane_TopL", 1f, 2f, 7.5f, 12f),
            ("Lane_TopR", 6f, 7f, 7.5f, 12f),
            ("Lane_BottomL", -7f, -6f, -17f, -12.5f),
            ("Lane_BottomR", -2f, -1f, -17f, -12.5f),
        };

        // 歯車の門: 名前・位置・最初の向き（0 = 棒が通路に沿う＝通れる、90 = ふさぐ）・時計回りか
        // 砲台はこの真ん中（軸の上）に乗る。並びは P1 左上・P2 右上・P3 左下・P4 右下（ユーザー決定 2026-10-29）。
        private static readonly (string name, Vector2 position, float angle, bool clockwise)[] Gates =
        {
            ("Gate_TopL", new Vector2(-8f, 4.5f), 0f, true),
            ("Gate_TopR", new Vector2(9f, 4.5f), 90f, false),
            ("Gate_BottomL", new Vector2(-9f, -9.5f), 90f, true),
            ("Gate_BottomR", new Vector2(8f, -9.5f), 0f, false),
        };

        // 島の上で門にかみ合って回る小さい歯車（見た目だけ）
        private static readonly Vector2[] SmallGears =
        {
            new Vector2(-8f, 1f), new Vector2(9f, 1f), new Vector2(-9f, -6f), new Vector2(8f, -6f)
        };

        private const float GateFirstTurn = 8f;
        private const float GateTurnSeconds = 1.5f;
        private const float GateWaitSeconds = 8f;
        private const float GateRadius = 2f;
        private const float BarLength = 4.6f;
        private const float BarThickness = 0.7f;
        private const float IslandTop = 1.06f;
        private const float BackgroundDepth = 2.5f;

        public static string ScenePath => StageAssets.StageScenePath(SceneName);

        [MenuItem("Tools/ステージ/歯車ステージ（ステージ3）のシーンを作る", false, 31)]
        private static void BuildMenu()
        {
            var exists = File.Exists(ScenePath);
            var message = exists
                ? $"{ScenePath} はもうあります。作り直すと、手で直した壁・仕掛け・湧き位置・目印は元の配置に戻ります（ウェーブの計画・カメラの視点・背景のステージは今の物を使います。Git で戻せます）。作り直しますか？"
                : $"ステージ3「歯車ステージ」のシーンを新しく作ります（{ScenePath}）。\n\n" +
                  "・外まわりの通路と、コアのある広場へ入る左右の通路（壁はビームを通す）\n" +
                  "・通路の 4 か所に、決まった時刻に 90° 回って道を開け閉めする歯車の門\n" +
                  "・左右の通路に、出たり引っ込んだりする動く壁\n" +
                  "・上と下の湧き位置、コア・砲台 4 台の目印（砲台は歯車の門の上）、カメラの視点（ステージ2 から写す）\n" +
                  "・ステージ背景スタジオの背景（仮の 3D モデル: 床・壁・歯車）\n\n" +
                  "ルーレットにステージ3として足します。";
            if (!EditorUtility.DisplayDialog("歯車ステージ（ステージ3）", message, exists ? "作り直す" : "作る", "やめる"))
            {
                return;
            }

            var report = Build();
            var text = string.Join("\n", report);
            Debug.Log("[Stage] 歯車ステージ（ステージ3）:\n" + text);
            EditorUtility.DisplayDialog("歯車ステージ（ステージ3）", text.Length > 1800 ? text.Substring(0, 1800) + "\n…（続きはコンソール）" : text, "OK");
        }

        [MenuItem("Tools/ステージ/歯車ステージ（ステージ3）のシーンを作る", true)]
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
                report.Add($"{sourcePath} がありません（群衆などの設定を Stage1 から写すため）。");
                return report;
            }

            StageAssets.EnsureFolder(StageAssets.StageSceneFolder);
            StageAssets.EnsureFolder(DataFolder);
            var materials = GearStageModels.Prepare(ModelFolder, MaterialFolder, report);
            var plan = LoadOrCreateWavePlan(report);
            var cameraPreset = LoadOrCreateCameraPreset(report);
            var definition = StageAssets.CreateDefinition("Stage_3", "stage3", "歯車ステージ", "回る歯車が道を変える", SceneName, new Color(1f, 0.74f, 0.3f));
            report.Add($"ステージの設定 {StageAssets.DataFolder}/Stage_3.asset: 名前「歯車ステージ」・シーン {SceneName} にした。");

            var source = EditorSceneManager.OpenScene(sourcePath, OpenSceneMode.Single);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);

            // ── Stage1 から写す物（設定を引き継ぐ） ──
            var content = NewRoot(scene, StageSplitTool.ContentRootName);
            CopyFrom<SwarmSystem>(source, scene, content.transform, "SwarmSystem", report);
            var field = CopyFrom<NavigationField>(source, scene, content.transform, "NavigationField", report);
            var director = CopyFrom<EnemySpawnDirector>(source, scene, content.transform, "EnemySpawnDirector", report);
            CopyFrom<FortressMatchRules>(source, scene, null, "[Fortress] Match Rules", report);
            var stageSet = SetUpStageRoot(source, report);

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
                director.wave = null; // ウェーブの計画で湧かせる（Stage1 の時間割は湧き位置の名前が違うので外す）
                director.wavePlan = plan;
                director.spawnPoints = points.ToArray();
            }

            report.Add($"湧き位置: {string.Join(" / ", SpawnPoints.Select(s => s.id))}（ウェーブの計画 {WavePlanPath}）");

            // ── 当たり判定の壁（見えない四角。見た目は背景スタジオの背景） ──
            var obstacles = NewChild(content.transform, "Obstacles", Vector2.zero);
            foreach (var (name, x0, x1, y0, y1) in Islands)
            {
                Wall(obstacles.transform, name, x0, x1, y0, y1, passBeams: true);
            }

            foreach (var (name, x0, x1, y0, y1) in Rims)
            {
                Wall(obstacles.transform, name, x0, x1, y0, y1, passBeams: false);
            }

            report.Add($"壁: 島 {Islands.Length} 個（敵は止める・ビームは通す）、外のふち・湧き位置の通路 {Rims.Length} 個（ビームも止める）。見えない四角で、見た目は背景の 3D モデル。");

            // ── 仕掛け ──
            var gimmicks = NewChild(content.transform, "Gimmicks", Vector2.zero);
            var gates = BuildGates(gimmicks.transform, stageSet, materials);
            BuildDecoGears(gimmicks.transform, gates, stageSet, materials);
            BuildPistons(obstacles.transform, stageSet, materials);
            report.Add($"歯車の門 {Gates.Length} 個: {GateFirstTurn:0}秒から {GateTurnSeconds + GateWaitSeconds:0.#}秒ごとに 90° 回る（最初は左上・右下が開き、右上・左下が閉じている）。");
            report.Add("動く壁 2 個: 左右の通路に出たり引っ込んだりする（通れる幅 4m → 1.8m）。見た目だけの歯車: 真ん中の大歯車・島の上の小歯車 4・背景の大歯車 4。");

            // ── 配置の目印 ──
            var layoutGo = NewRoot(scene, StageSplitTool.LayoutName);
            var layout = layoutGo.AddComponent<StageLayout>();
            layout.definition = definition;
            layout.content = content;
            layout.cameraPreset = cameraPreset;
            layout.coreAnchor = NewChild(layoutGo.transform, "Core", CorePosition).transform;
            layout.turretAnchors = new Transform[StageLayout.TurretCount];
            for (var i = 0; i < StageLayout.TurretCount && i < Gates.Length; i++)
            {
                var position = Gates[i].position;
                var anchor = NewChild(layoutGo.transform, $"Turret_P{i + 1}", position).transform;
                anchor.rotation = Quaternion.Euler(0f, 0f, FacingDegrees(position, CorePosition));
                layout.turretAnchors[i] = anchor;
            }

            report.Add("配置の目印: コア＝真ん中の大歯車の上、P1〜P4＝歯車の門の真ん中（軸の上。歯車と棒は砲台の下で回る。向きはコアの方）。");

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
            report.Add($"ビルドのシーン一覧に {added} 個入れた。ルーレットにステージ3として足した。");
            report.Add("次: Tools › ステージ › チェック → 要塞デザイナー › 仕掛け タブで時間割を見る（「調べる」で点検）→ Stage3_Gear を開いて Play。");

            EditorSceneManager.OpenScene(StageAssets.GameScenePath, OpenSceneMode.Single);
            var opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(opened);
            return report;
        }

        // ───────── 仕掛け ─────────

        private static List<GimmickGear> BuildGates(Transform parent, StageSet set, GearStageModels.Materials materials)
        {
            var result = new List<GimmickGear>();
            foreach (var (name, position, angle, clockwise) in Gates)
            {
                var gear = NewGear(parent, name, position, 0f, angle);
                gear.motion = GearMotion.Step;
                gear.clockwise = clockwise;
                gear.firstTurnAt = GateFirstTurn;
                gear.stepDegrees = 90f;
                gear.turnSeconds = GateTurnSeconds;
                gear.waitSeconds = GateWaitSeconds;
                gear.teeth = 16;
                gear.radius = GateRadius;
                gear.memo = "棒の腕で通路をふさぐ門。棒が通路に沿うと通れる、横切るとふさぐ。";

                var arm = new GameObject("Arm_Bar");
                arm.transform.SetParent(gear.Rotor, false);
                var component = arm.AddComponent<GimmickGearArm>();
                component.laserPassesThrough = true;
                component.SetSize(BarLength, BarThickness);

                GearStageModels.AttachProp(gear.Rotor, "見た目_歯車", "Gear_Gate", gear.transform.position, angle, set, materials);
                GearStageModels.AttachProp(gear.Rotor, "見た目_棒", "Gate_Bar", gear.transform.position, angle, set, materials);
                result.Add(gear);
            }

            return result;
        }

        private static void BuildDecoGears(Transform parent, List<GimmickGear> gates, StageSet set, GearStageModels.Materials materials)
        {
            var core = NewGear(parent, "Gear_Core", CorePosition, 0f, 0f);
            core.motion = GearMotion.Continuous;
            core.firstTurnAt = 0f;
            core.degreesPerSecond = 6f;
            core.clockwise = true;
            core.teeth = 24;
            core.radius = 3.6f;
            core.memo = "コアの下の大歯車（見た目だけ。敵は上を歩ける）。";
            GearStageModels.AttachProp(core.Rotor, "見た目_歯車", "Gear_Core", core.transform.position, 0f, set, materials);

            for (var i = 0; i < SmallGears.Length && i < gates.Count; i++)
            {
                var small = NewGear(parent, $"Gear_Small_{i + 1}", SmallGears[i], -IslandTop, 0f);
                small.motion = GearMotion.Follow;
                small.driver = gates[i];
                small.teeth = 12;
                small.radius = 1.5f;
                small.memo = $"島の上で {gates[i].name} にかみ合って回る（見た目だけ）。";
                GearStageModels.AttachProp(small.Rotor, "見た目_歯車", "Gear_Small", small.transform.position, 0f, set, materials);
            }

            var bigA = BackgroundGear(parent, "Gear_Back_L1", new Vector2(-22f, 2f), "Gear_Big4", 24, 4f, set, materials);
            bigA.motion = GearMotion.Continuous;
            bigA.degreesPerSecond = 6f;
            bigA.clockwise = true;
            var bigB = BackgroundGear(parent, "Gear_Back_L2", new Vector2(-21f, -4.93f), "Gear_Big3", 18, 3f, set, materials);
            bigB.motion = GearMotion.Follow;
            bigB.driver = bigA;
            var bigC = BackgroundGear(parent, "Gear_Back_R1", new Vector2(22f, -6f), "Gear_Big45", 28, 4.5f, set, materials);
            bigC.motion = GearMotion.Continuous;
            bigC.degreesPerSecond = 5f;
            bigC.clockwise = false;
            var bigD = BackgroundGear(parent, "Gear_Back_R2", new Vector2(21f, 1.43f), "Gear_Big3", 18, 3f, set, materials);
            bigD.motion = GearMotion.Follow;
            bigD.driver = bigC;
        }

        private static GimmickGear BackgroundGear(Transform parent, string name, Vector2 position, string model, int teeth, float radius,
            StageSet set, GearStageModels.Materials materials)
        {
            var gear = NewGear(parent, name, position, BackgroundDepth, 0f);
            gear.firstTurnAt = 0f;
            gear.teeth = teeth;
            gear.radius = radius;
            gear.memo = "ステージの外の、床より下の背景の歯車（見た目だけ）。";
            GearStageModels.AttachProp(gear.Rotor, "見た目_歯車", model, gear.transform.position, 0f, set, materials);
            return gear;
        }

        private static void BuildPistons(Transform parent, StageSet set, GearStageModels.Materials materials)
        {
            Piston(parent, "Piston_W", new Vector2(-10.8f, 0.5f), new Vector2(0f, -2.4f), 4f, set, materials);
            Piston(parent, "Piston_E", new Vector2(10.8f, -5.5f), new Vector2(0f, 2.4f), 8.75f, set, materials);
        }

        /// <summary>島の中に隠れていて、決まった時刻に通路へせり出す壁（今ある「壊れない壁」と同じ作り＋動く障害物）。</summary>
        private static void Piston(Transform parent, string name, Vector2 position, Vector2 offset, float firstMoveAt, StageSet set,
            GearStageModels.Materials materials)
        {
            var go = NewChild(parent, name, position);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(1.6f, 1.6f);
            box.isTrigger = true; // ほかの島と同じく、ビームは通す
            go.AddComponent<NavigationObstacle>().mode = NavigationObstacleMode.Block;
            var mover = go.AddComponent<GimmickMover>();
            mover.CaptureHome();
            mover.loop = MoverLoop.PingPong;
            mover.firstMoveAt = firstMoveAt;
            mover.stops = new List<MoverStop>
            {
                new MoverStop { moveSeconds = 1.2f, waitSeconds = 6f },
                new MoverStop { offset = offset, moveSeconds = 1.2f, waitSeconds = 5f }
            };
            mover.memo = "島の中に隠れていて、通路へせり出すと通れる幅が 4m → 1.8m になる。";
            var prop = GearStageModels.AttachProp(go.transform, "見た目_動く壁", "Piston", go.transform.position, 0f, set, materials);
            if (prop != null)
            {
                // 始まりの演出（上から落ちてくる）は、動く壁ごと落とす（見た目だけ別に落とすと二重にずれる）。
                prop.gameObject.AddComponent<StageDropIntroSkip>().memo = "親の動く壁ごと落ちる。";
            }
        }

        private static GimmickGear NewGear(Transform parent, string name, Vector2 position, float z, float angle)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(position.x, position.y, z), Quaternion.Euler(0f, 0f, angle));
            var rotor = new GameObject(GimmickGear.RotorName);
            rotor.transform.SetParent(go.transform, false);
            var gear = go.AddComponent<GimmickGear>();
            gear.rotor = rotor.transform;
            return gear;
        }

        // ───────── ファイル ─────────

        /// <summary>ウェーブの計画: Stage2 の計画を写し、湧き位置の名前をこのステージの物にする（あれば今の物を使う）。</summary>
        private static WavePlan LoadOrCreateWavePlan(List<string> report)
        {
            var plan = AssetDatabase.LoadAssetAtPath<WavePlan>(WavePlanPath);
            if (plan != null)
            {
                report.Add($"ウェーブの計画 {WavePlanPath} はもうあるので、そのまま使った。");
                return plan;
            }

            if (!AssetDatabase.CopyAsset(BathStageBuilder.BathWavePlanPath, WavePlanPath))
            {
                plan = ScriptableObject.CreateInstance<WavePlan>();
                AssetDatabase.CreateAsset(plan, WavePlanPath);
                report.Add($"Stage2 の計画が無かったので、空の計画 {WavePlanPath} を作った（要塞デザイナー › 敵ウェーブで作ってください）。");
                return plan;
            }

            plan = AssetDatabase.LoadAssetAtPath<WavePlan>(WavePlanPath);
            plan.memo = "仮のレベルデザイン（Claude 2026-10-29）。Stage2 の計画（目標 120/250/400/530/700）を写し、" +
                        "左側の湧き位置の行を上（Spawn_Top）、右側の行を下（Spawn_Bottom）にした。歯車の門は 8 秒から 9.5 秒ごとに回る（仕掛けタブ）。";
            for (var w = 0; w < plan.waves.Count; w++)
            {
                var wave = plan.waves[w];
                wave.announceText = w == 0 ? "歯車が回ると道が変わる！" : w == 2 ? "動く壁に気をつけろ！" : "";
                foreach (var entry in wave.spawnEntries)
                {
                    if (entry == null)
                    {
                        continue;
                    }

                    var id = entry.spawnPointId ?? "";
                    entry.spawnPointId = id.EndsWith("R") ? "Spawn_Bottom" : "Spawn_Top";
                }
            }

            EditorUtility.SetDirty(plan);
            AssetDatabase.SaveAssetIfDirty(plan);
            report.Add($"ウェーブの計画 {WavePlanPath} を Stage2 の計画から写した（湧き位置を Spawn_Top / Spawn_Bottom に直した）。");
            return plan;
        }

        /// <summary>カメラの視点: Stage2（お風呂場）の視点を写す（画面の下の HUD に隠れない高さ。あれば今の物を使う）。</summary>
        private static CameraViewPreset LoadOrCreateCameraPreset(List<string> report)
        {
            var preset = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(CameraPresetPath);
            if (preset != null)
            {
                return preset;
            }

            var source = AssetDatabase.LoadAssetAtPath<CameraViewPreset>(BathStageBuilder.CameraPresetPath);
            preset = StageMenu.CopyCameraPreset(source, SceneName, "ステージ3", "Stage2_Bath");
            report.Add(preset != null
                ? $"カメラの視点 {AssetDatabase.GetAssetPath(preset)} を Stage2 の視点から写した。"
                : "Stage2 のカメラの視点が無いので、視点は空のまま（カメラタブで作ってください）。");
            return preset;
        }

        /// <summary>
        /// ステージ背景スタジオの置き場所（[Stage]）を作り、歯車ステージの背景（StageSet）を出す。
        /// StageSet が無ければ作り、光と反応の共通設定を Stage1 から写し、床・壁などの 3D モデルを背景スタジオの取り込み（分けて取り込む）で入れる。
        /// </summary>
        private static StageSet SetUpStageRoot(Scene source, List<string> report)
        {
            var set = AssetDatabase.LoadAssetAtPath<StageSet>(StageSetPath);
            var created = set == null;
            if (created)
            {
                set = StageAssetFactory.CreateStage(StageSetName);
                var from = FindIn<StageRoot>(source).Select(r => r.current).FirstOrDefault(c => c != null);
                if (from != null)
                {
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(from.lighting), set.lighting);
                    set.lookProfile = from.lookProfile != null ? from.lookProfile : set.lookProfile;
                }

                set.memo = $"{SceneName}（ステージ3「歯車ステージ」）用。光は Stage1 から写した。床・壁・歯車は仮の 3D モデル（Models/Stage03_Gear、作り直しは Source~/make_models.py）。";
                EditorUtility.SetDirty(set);
                AssetDatabase.SaveAssets();
            }

            if (!created)
            {
                var removed = GearStageModels.RemoveMissingStaticParts(set);
                if (removed > 0)
                {
                    report.Add($"床・壁のモデルから消えた部分の背景 {removed} 個を、ステージの Prefab から外した。");
                }
            }

            var root = new GameObject(StageSceneService.RootName).AddComponent<StageRoot>();
            StageSceneService.PlaceStage(root, set);
            if (created)
            {
                var count = GearStageModels.ImportStatic(root, set, report);
                StageSceneService.SaveToPrefab(root);
                report.Add($"ステージ背景スタジオのステージ {AssetDatabase.GetAssetPath(set)} を作り、床・壁などの背景 {count} 個を取り込んだ（光は Stage1 から写した）。");
            }
            else
            {
                report.Add($"ステージ背景スタジオのステージ {AssetDatabase.GetAssetPath(set)} はもうあるので、そのまま出した（背景は直した物のまま）。");
            }

            return set;
        }

        // ───────── 小さな道具 ─────────

        /// <summary>壊れない壁（見えない四角）。passBeams なら当たり判定をトリガーにして、ビームだけ通す（敵の経路では壁のまま）。</summary>
        private static void Wall(Transform parent, string name, float xMin, float xMax, float yMin, float yMax, bool passBeams)
        {
            var go = NewChild(parent, name, new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f));
            go.transform.localScale = new Vector3(xMax - xMin, yMax - yMin, 1f);
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;
            collider.isTrigger = passBeams;
            go.AddComponent<NavigationObstacle>().mode = NavigationObstacleMode.Block;
        }

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
    }
}
