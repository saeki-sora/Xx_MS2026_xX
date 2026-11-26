using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MS2026.Fortress;
using MS2026.Stages;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MS2026.HowToPlay.EditorTools
{
    /// <summary>
    /// 練習のシーン（Assets/HowToPlay/Scenes/PracticeTutorial.unity）を作る（2026-11-13）。
    /// 本番の Game とステージ（StageCatalog の一番上）を開き、本物の部品だけを新しいシーンへ移して保存する:
    /// ・Game から: 砲台とコア（Fortress）、スキルを付ける係、同期の部品（[Fortress] NetSync。4人で同じ敵・砲台を見るため）、
    ///   そのほか通信の土台・画面・BGM 以外の物
    /// ・ステージから: 群衆・出現場所・敵を出す係・壊せる物（スマッシュボール）・経路（[Stage] Content）
    /// 背景や置物はステージの物を使わず、簡単な台と光を置く。通信の土台（NetworkManager など）は入れない（ロビーの物を使う）。
    /// 練習のシーンは、通信中は Host が通信経由で読み込む（全員に届く）。
    /// 練習の係（PracticeSceneDriver）とカメラを置き、敵の出し方は WAVE 1 の出現場所・種類から作る。
    /// 開いていた Game とステージは保存しない（元に戻す）。作ったシーンはビルドの一覧に入れる。
    /// </summary>
    public static class PracticeSceneBuilder
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string SceneFolder = "Assets/HowToPlay/Scenes";
        private const string ArtFolder = "Assets/HowToPlay/Art/Field";

        // Game のシーンで使わない物（通信・画面・BGM・カメラ・入力など）。コンポーネントの型名で見分ける。
        private static readonly string[] SkipGameRoots =
        {
            "NetworkManager", "FortressNetworkBootstrap", "StageBootstrap", "UiRoot", "FortressBgmPlayer",
            "GameSceneSessionGuard", "EventSystem", "ArduinoSerialReader", "FortressCameraRig", "FortressConnectUI"
        };

        // 同期の部品（入れる）。これがある物は、通信の部品が付いていても移す。
        private static readonly string[] KeepNetworkRoots = { "TurretNetworkHub", "SwarmNetworkHub", "DestructibleNetworkHub", "EnemyNetworkHub" };

        [MenuItem("Tools/あそびかた/練習のシーンを作る（本物の砲台・敵・スキル）")]
        public static void Run()
        {
            if (EditorApplication.isPlaying)
            {
                EditorUtility.DisplayDialog("練習のシーン", "再生中はできません。再生を止めてから押してください。", "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var setup = EditorSceneManager.GetSceneManagerSetup();
            string report;
            try
            {
                report = Build();
            }
            catch (Exception e)
            {
                report = "作れませんでした: " + e.Message;
                Debug.LogException(e);
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                }
                else
                {
                    EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                }
            }

            EditorUtility.DisplayDialog("練習のシーン", report, "OK");
        }

        /// <summary>バッチモード用（-executeMethod MS2026.HowToPlay.EditorTools.PracticeSceneBuilder.RunBatch）。</summary>
        public static void RunBatch()
        {
            var report = Build();
            Debug.Log("[PracticeSceneBuilder] " + report);
            EditorApplication.Exit(0);
        }

        private static string Build()
        {
            var lines = new List<string>();
            var settings = HowToPlaySettings.Load();
            var sceneName = settings.screen != null && !string.IsNullOrEmpty(settings.screen.tutorialSceneName) ? settings.screen.tutorialSceneName : "PracticeTutorial";
            Directory.CreateDirectory(SceneFolder);
            Directory.CreateDirectory(ArtFolder);

            var game = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);
            var catalog = StageCatalog.Load();
            var stageDef = catalog != null ? catalog.Get(0) : null;
            var stagePath = stageDef != null ? FindScenePath(stageDef.sceneName) : null;
            if (stagePath == null)
            {
                throw new Exception("ステージのシーンが見つかりません（StageCatalog の一番上）。");
            }

            var stage = EditorSceneManager.OpenScene(stagePath, OpenSceneMode.Additive);
            var practice = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            // Game から移す物。
            foreach (var root in game.GetRootGameObjects())
            {
                var keepSync = HasAny(root, KeepNetworkRoots);
                if (!keepSync && (HasAny(root, SkipGameRoots) || HasNetworkPart(root)))
                {
                    continue;
                }

                SceneManager.MoveGameObjectToScene(root, practice);
                lines.Add("Game から: " + root.name);
            }

            // ステージから移す物（群衆・出現場所・敵を出す係・壊せる物・経路）と、試合の決まり。
            foreach (var root in stage.GetRootGameObjects())
            {
                if (root.GetComponentInChildren<SwarmSystem>(true) != null || root.GetComponentInChildren<EnemySpawnDirector>(true) != null ||
                    root.GetComponentInChildren<FortressMatchRules>(true) != null)
                {
                    SceneManager.MoveGameObjectToScene(root, practice);
                    lines.Add("ステージから: " + root.name);
                }
            }

            // 同期の部品以外に付いている通信の部品は外す（念のため）。
            RemoveNetworkParts(practice, KeepNetworkRoots);

            // 泡（スマッシュボール）: 1人に1つ。自分の砲台の横に置き、持ち主の印を付ける（ほかの人の泡は割れない）。
            lines.Add(PlaceOwnedBalls(practice));

            // 試合の終わりにならないように（コアは減らない・撃破数の目標は来ない）。
            foreach (var rules in FindIn<FortressMatchRules>(practice))
            {
                rules.clearKills = int.MaxValue / 2;
                rules.coreDamageMultiplier = 0f;
                rules.removeEnemiesOnEnd = false;
            }

            // 敵の出し方: WAVE 1 の出現場所・種類から、少しずつ出し続ける。ウェーブの計画は外す（ウェーブが始まらないように）。
            var director = FindIn<EnemySpawnDirector>(practice).FirstOrDefault();
            var entries = new List<EnemySpawnEntry>();
            if (director != null)
            {
                var wave = director.wavePlan != null ? director.wavePlan.Get(0) : null;
                if (wave != null && wave.spawnEntries != null && wave.spawnEntries.Count > 0)
                {
                    var type = wave.spawnEntries[0].enemyType;
                    var points = wave.spawnEntries.Where(e => e != null && !string.IsNullOrEmpty(e.spawnPointId)).Select(e => e.spawnPointId).Distinct().ToList();
                    var step = 1.2f;
                    for (var i = 0; i < points.Count; i++)
                    {
                        entries.Add(new EnemySpawnEntry
                        {
                            triggerTime = 1f + (i * step),
                            spawnPointId = points[i],
                            enemyType = type,
                            count = 1000,
                            intervalBetweenSpawns = step * Mathf.Max(1, points.Count),
                            timing = EnemySpawnTiming.Interval
                        });
                    }

                    lines.Add($"練習の敵: {type?.name} を {points.Count} か所から");
                }

                director.wavePlan = null;
                director.autoStartOnPlay = false;
                EditorUtility.SetDirty(director);
            }

            // 台・光・カメラ・練習の係。
            var floorMaterial = FloorMaterial();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "[Practice] Table";
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            floor.transform.SetPositionAndRotation(new Vector3(0f, 0f, 1.5f), Quaternion.Euler(90f, 0f, 0f));
            floor.transform.localScale = new Vector3(34f, 0.2f, 34f);
            floor.GetComponent<MeshRenderer>().sharedMaterial = floorMaterial;
            SceneManager.MoveGameObjectToScene(floor, practice);

            var lightGo = new GameObject("[Practice] Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightGo.transform.rotation = Quaternion.Euler(-35f, 25f, 0f);
            SceneManager.MoveGameObjectToScene(lightGo, practice);

            var camGo = new GameObject("[Practice] Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color32(0xb9, 0xb3, 0xdc, 0xff);
            cam.depth = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 300f;
            cam.fieldOfView = 55f;
            camGo.AddComponent<UniversalAdditionalCameraData>();
            camGo.transform.SetPositionAndRotation(new Vector3(0f, 0f, -16f), Quaternion.identity);
            SceneManager.MoveGameObjectToScene(camGo, practice);

            var driverGo = new GameObject("[Practice] Driver");
            var driver = driverGo.AddComponent<PracticeSceneDriver>();
            SceneManager.MoveGameObjectToScene(driverGo, practice);
            var so = new SerializedObject(driver);
            so.FindProperty("viewCamera").objectReferenceValue = cam;
            so.FindProperty("director").objectReferenceValue = director;
            var list = so.FindProperty("practiceEntries");
            list.ClearArray();
            so.ApplyModifiedPropertiesWithoutUndo();
            var field = typeof(PracticeSceneDriver).GetField("practiceEntries", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(driver, entries);
            EditorUtility.SetDirty(driver);

            var path = $"{SceneFolder}/{sceneName}.unity";
            EditorSceneManager.SaveScene(practice, path);

            // 通信の部品の番号（シーンごとに決まる）を、保存した新しいシーンで決め直して、もう一度保存する。
            var networkObjects = 0;
            foreach (var root in practice.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<Component>(true))
                {
                    if (c != null && c.GetType().FullName == "Unity.Netcode.NetworkObject")
                    {
                        var validate = c.GetType().GetMethod("OnValidate", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                        validate?.Invoke(c, null);
                        EditorUtility.SetDirty(c);
                        networkObjects++;
                    }
                }
            }

            EditorSceneManager.MarkSceneDirty(practice);
            EditorSceneManager.SaveScene(practice, path);
            lines.Add($"同期の部品: {networkObjects} 個（4人で同じ敵・砲台を見る）");
            AddToBuildSettings(path);
            lines.Insert(0, $"作りました: {path}（ビルドの一覧にも入れました）");
            lines.Add("※ 開いていた Game とステージは保存していません。");
            return string.Join("\n", lines);
        }

        // ───────── 小さな部品 ─────────

        private static bool HasAny(GameObject root, string[] typeNames)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (c != null && typeNames.Contains(c.GetType().Name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNetwork(Component c)
        {
            for (var t = c.GetType(); t != null; t = t.BaseType)
            {
                if (t.FullName == "Unity.Netcode.NetworkObject" || t.FullName == "Unity.Netcode.NetworkBehaviour" || t.FullName == "Unity.Netcode.NetworkManager")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasNetworkPart(GameObject root)
        {
            foreach (var c in root.GetComponents<Component>())
            {
                if (c != null && IsNetwork(c))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>通信の部品を外す（NetworkBehaviour を先に、NetworkObject は後で）。keep の型がある物はそのまま。</summary>
        private static void RemoveNetworkParts(Scene scene, string[] keep)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                if (HasAny(root, keep))
                {
                    continue;
                }

                var parts = root.GetComponentsInChildren<Component>(true).Where(c => c != null && IsNetwork(c)).ToList();
                foreach (var c in parts.Where(c => c.GetType().FullName != "Unity.Netcode.NetworkObject"))
                {
                    Object.DestroyImmediate(c);
                }

                foreach (var c in parts.Where(c => c != null))
                {
                    Object.DestroyImmediate(c);
                }
            }
        }

        /// <summary>泡を4つ（1P〜4P）にし、それぞれの砲台の横（コアから見て横の向き）に置く。順番待ち（ローテーション）は切る。</summary>
        private static string PlaceOwnedBalls(Scene scene)
        {
            var balls = FindIn<SmashBallModule>(scene).ToList();
            var turrets = FindIn<LaserTurret>(scene).Where(t => t.playerIndex >= 0 && t.playerIndex < 4)
                .GroupBy(t => t.playerIndex).Select(g => g.First()).OrderBy(t => t.playerIndex).ToList();
            if (balls.Count == 0 || turrets.Count == 0)
            {
                return "泡: ステージに泡か砲台が無いので、1人1つにできませんでした";
            }

            var template = balls[0];
            for (var i = 1; i < balls.Count; i++)
            {
                Object.DestroyImmediate(balls[i].gameObject);
            }

            var core = FindIn<CoreCrystalController>(scene).FirstOrDefault();
            var center = core != null ? core.transform.position : Vector3.zero;
            var z = template.transform.position.z;
            var parent = template.transform.parent;
            foreach (var turret in turrets)
            {
                var p = turret.playerIndex;
                var ball = p == turrets[0].playerIndex ? template : Object.Instantiate(template.gameObject, parent).GetComponent<SmashBallModule>();
                ball.gameObject.name = $"Smash Ball {p + 1}P";
                ball.settings.exclusiveRotation = false;
                ball.settings.label = $"{p + 1}P";
                ball.gameObject.SetActive(true);

                var t = turret.transform.position;
                var radial = new Vector2(t.x - center.x, t.y - center.y);
                radial = radial.sqrMagnitude > 0.0001f ? radial.normalized : Vector2.up;
                var side = new Vector2(-radial.y, radial.x);
                var pos = new Vector2(t.x, t.y) + (side * BallSideOffset) + (radial * BallOutOffset);
                ball.transform.position = new Vector3(pos.x, pos.y, z);

                var owner = ball.GetComponent<PracticeBallOwner>();
                if (owner == null)
                {
                    owner = ball.gameObject.AddComponent<PracticeBallOwner>();
                }

                owner.playerIndex = p;
                EditorUtility.SetDirty(ball);
                EditorUtility.SetDirty(owner);
            }

            return $"泡: {turrets.Count} 人ぶん（それぞれの砲台の横。持ち主しか割れない）";
        }

        // 泡を置く場所: 砲台から横へ（メートル）・外へ（メートル）。
        private const float BallSideOffset = 3.2f;
        private const float BallOutOffset = 0.4f;

        private static IEnumerable<T> FindIn<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var c in root.GetComponentsInChildren<T>(true))
                {
                    yield return c;
                }
            }
        }

        private static Material FloorMaterial()
        {
            var path = $"{ArtFolder}/PracticeTable.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            material = new Material(shader) { name = "PracticeTable" };
            var color = new Color32(0x5f, 0xd3, 0xa6, 0xff);
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            material.color = color;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static string FindScenePath(string sceneName)
        {
            foreach (var guid in AssetDatabase.FindAssets($"{sceneName} t:Scene"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == sceneName)
                {
                    return path;
                }
            }

            return null;
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.Any(s => s.path == path))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
