using MS2026.Fortress;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// Phase1（土台）の動作確認用に、現在開いているシーンへ最小構成の要塞テストレイアウトを
    /// 自動生成するワンクリックツール。地形案（時計仕掛け/二層構造/花弁シンメトリー等）が
    /// チーム内で未確定なため、あくまで汎用的な仮配置。本番の地形が決まったら
    /// FortressLayoutConfig プリセットや EnemySpawnPoint の位置を差し替えるだけでよい。
    /// </summary>
    public static class Phase1TestSceneBuilder
    {
        private const string PresetFolder = "Assets/_Game/Fortress/Presets";

        [MenuItem("Tools/Fortress/Build Phase1 Test Scene")]
        public static void Build()
        {
            if (GameObject.Find("Fortress") != null)
            {
                EditorUtility.DisplayDialog(
                    "既に生成済みです",
                    "シーンに 'Fortress' という名前のGameObjectが既に存在します。\n" +
                    "作り直す場合は、いったんそのGameObjectを削除してから再実行してください。",
                    "OK");
                return;
            }

            EditorAssetUtility.EnsureFolder(PresetFolder);

            var tuning = EditorAssetUtility.LoadOrCreateAsset<LaserTuningConfig>($"{PresetFolder}/Default_LaserTuningConfig.asset");
            var enemyType = EditorAssetUtility.LoadOrCreateAsset<EnemyTypeDefinition>($"{PresetFolder}/Default_EnemyType.asset");
            enemyType.displayName = "小型ドローン";

            var root = new GameObject("Fortress");
            Undo.RegisterCreatedObjectUndo(root, "Build Phase1 Test Scene");

            var core = BuildCore(root.transform);
            var turrets = BuildTurrets(root.transform, tuning);
            var layoutController = root.AddComponent<FortressLayoutController>();
            layoutController.turrets = turrets;

            var obstacleTuning = EditorAssetUtility.LoadOrCreateAsset<DestructibleObstacleTuning>(
                $"{PresetFolder}/Default_DestructibleObstacleTuning.asset");
            BuildObstacles(root.transform, turrets, obstacleTuning);

            var spawnPoints = BuildSpawnPoints(root.transform);
            var wave = EditorAssetUtility.LoadOrCreateAsset<EnemyWaveConfig>($"{PresetFolder}/Default_EnemyWaveConfig.asset");
            PopulateSampleWave(wave, enemyType);

            var directorGo = new GameObject("EnemySpawnDirector");
            directorGo.transform.SetParent(root.transform);
            var director = directorGo.AddComponent<EnemySpawnDirector>();
            director.spawnPoints = spawnPoints;
            director.wave = wave;

            SetupCamera();

            EditorUtility.SetDirty(tuning);
            EditorUtility.SetDirty(enemyType);
            EditorUtility.SetDirty(obstacleTuning);
            EditorUtility.SetDirty(wave);
            AssetDatabase.SaveAssets();

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log("[Fortress] Phase1テストシーンを生成しました。Fortress Designer(Tools > Fortress)から調整できます。");
        }

        private static CoreCrystalController BuildCore(Transform parent)
        {
            var go = new GameObject("CoreCrystal");
            go.transform.SetParent(parent);
            go.transform.position = Vector3.zero;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderSpriteFactory.CreateCircleSprite();
            sr.color = new Color(0.6f, 0.85f, 1f);
            sr.transform.localScale = Vector3.one * 2.5f;

            return go.AddComponent<CoreCrystalController>();
        }

        private static LaserTurret[] BuildTurrets(Transform parent, LaserTuningConfig tuning)
        {
            // 汎用の仮配置（十字型）。地形案が決まり次第、FortressLayoutConfigプリセットで差し替える。
            var slots = new (int playerIndex, Vector2 position, float facingDegrees)[]
            {
                (0, new Vector2(0f, 6f), 180f),   // 北 → 中心向き
                (1, new Vector2(6f, 0f), 90f),    // 東 → 中心向き
                (2, new Vector2(0f, -6f), 0f),    // 南 → 中心向き
                (3, new Vector2(-6f, 0f), 270f),  // 西 → 中心向き
            };

            var turrets = new LaserTurret[slots.Length];

            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                var go = new GameObject($"Turret_P{slot.playerIndex}");
                go.transform.SetParent(parent);
                go.transform.position = slot.position;
                go.transform.rotation = Quaternion.Euler(0f, 0f, slot.facingDegrees);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = PlaceholderSpriteFactory.CreateCircleSprite();
                sr.color = FortressColors.PlayerColor(slot.playerIndex);
                sr.transform.localScale = Vector3.one * 1.2f;

                var turret = go.AddComponent<LaserTurret>();
                turret.playerIndex = slot.playerIndex;
                turret.tuning = tuning;

                go.AddComponent<LaserBeamVisual>();

                turrets[i] = turret;
            }

            return turrets;
        }

        private static EnemySpawnPoint[] BuildSpawnPoints(Transform parent)
        {
            const int count = 8;
            const float radius = 10f;

            var spawnRoot = new GameObject("SpawnPoints");
            spawnRoot.transform.SetParent(parent);

            var points = new EnemySpawnPoint[count];

            for (var i = 0; i < count; i++)
            {
                var angle = i * Mathf.PI * 2f / count;
                var position = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * radius;

                var go = new GameObject($"Spawn_{i}");
                go.transform.SetParent(spawnRoot.transform);
                go.transform.position = position;

                var spawnPoint = go.AddComponent<EnemySpawnPoint>();
                spawnPoint.spawnPointId = $"Spawn_{i}";
                points[i] = spawnPoint;
            }

            return points;
        }

        private static void PopulateSampleWave(EnemyWaveConfig wave, EnemyTypeDefinition enemyType)
        {
            if (wave.spawnEntries != null && wave.spawnEntries.Count > 0)
            {
                return; // 既に編集済みのウェーブは上書きしない。
            }

            wave.waveName = "サンプルウェーブ";
            wave.loop = true;
            wave.loopInterval = 5f;
            wave.spawnEntries = new System.Collections.Generic.List<EnemySpawnEntry>
            {
                new EnemySpawnEntry { triggerTime = 0f, spawnPointId = "Spawn_0", enemyType = enemyType, count = 3, intervalBetweenSpawns = 0.5f },
                new EnemySpawnEntry { triggerTime = 2f, spawnPointId = "Spawn_2", enemyType = enemyType, count = 3, intervalBetweenSpawns = 0.5f },
                new EnemySpawnEntry { triggerTime = 4f, spawnPointId = "Spawn_4", enemyType = enemyType, count = 5, intervalBetweenSpawns = 0.4f },
                new EnemySpawnEntry { triggerTime = 6f, spawnPointId = "Spawn_6", enemyType = enemyType, count = 5, intervalBetweenSpawns = 0.4f },
            };
        }

        private static void SetupCamera()
        {
            var cameraGo = GameObject.Find("Main Camera");
            var camera = cameraGo != null ? cameraGo.GetComponent<Camera>() : null;

            if (camera == null)
            {
                return;
            }

            Undo.RecordObject(camera, "Setup 2D Camera");
            Undo.RecordObject(camera.transform, "Setup 2D Camera");

            camera.orthographic = true;
            camera.orthographicSize = 14f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            camera.transform.rotation = Quaternion.identity;
        }

        private static void BuildObstacles(Transform parent, LaserTurret[] turrets, DestructibleObstacleTuning tuning)
        {
            // 企画書の「プレイヤーの体面に障害物を置き」に対応する仮配置。地形案が決まり次第、
            // 「地形破壊」タブの自動配置/個別配置で本番の位置に差し替えればよい。
            const float distanceFromTurret = 3.5f;
            var obstacleSize = new Vector2(1.2f, 2.5f);

            var root = new GameObject("Obstacles");
            root.transform.SetParent(parent);

            foreach (var turret in turrets)
            {
                var go = new GameObject($"Obstacle_P{turret.playerIndex}");
                go.transform.SetParent(root.transform);
                go.transform.position = turret.transform.position + turret.transform.up * distanceFromTurret;
                go.transform.rotation = turret.transform.rotation;

                var obstacle = go.AddComponent<DestructibleObstacle>();
                obstacle.tuning = tuning;
                obstacle.ResetToFull(); // tuningをAwake後に割り当てているため、HPスナップショットを取り直す。

                var wander = go.AddComponent<ObstacleMovement>();
                wander.nearAnchor = turret.transform;

                go.AddComponent<ObstacleVisual>();

                // transform.localScaleを動かすので、コリジョンと見た目に同時に反映される。
                obstacle.SetSize(obstacleSize);
            }
        }
    }
}
