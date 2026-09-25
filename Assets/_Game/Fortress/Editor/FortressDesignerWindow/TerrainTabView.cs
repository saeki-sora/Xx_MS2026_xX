using System.Linq;
using MS2026.Fortress;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MS2026.Fortress.EditorTools
{
    /// <summary>
    /// 「地形破壊」タブ。レーザーで削れる障害物(DestructibleObstacle)の配置と、
    /// 耐久・再生チューニングをまとめて編集する。砲台に紐づかない汎用オブジェクトなので、
    /// シーン上の好きな位置に好きな数だけ置ける。
    /// </summary>
    public sealed class TerrainTabView : VisualElement
    {
        private const string DefaultTuningPath = "Assets/_Game/Fortress/Presets/Default_DestructibleObstacleTuning.asset";

        private readonly IMGUIContainer _imgui;

        private DestructibleObstacleTuning _tuningForNewObstacles;
        private Vector2 _newObstacleSize = new Vector2(1.2f, 2.5f);
        private float _autoPlaceDistance = 3.5f;

        public TerrainTabView()
        {
            AddToClassList("fd-tab-content");
            _imgui = new IMGUIContainer(OnIMGUI);
            Add(_imgui);
        }

        public void Refresh()
        {
            _imgui.MarkDirtyRepaint();
        }

        private void OnIMGUI()
        {
            EditorGUILayout.LabelField("地形障害物", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "障害物はレーザーを当てるほどひびが濃くなり、限界に達すると崩れ落ちて破壊されます。" +
                "健在な間は奥の敵にレーザーが届かず、破壊されると自動で素通りするようになり、しばらくすると再生します。" +
                "プレイヤーとコアの間をランダムに漂う、あるいはコアを中心に円形に周回する動きも持たせられます。" +
                "砲台に紐づかない汎用オブジェクトなので、シーン上の好きな位置に好きな数だけ置けます。",
                MessageType.None);

            EditorGUILayout.Space(6);
            DrawCreationControls();

            // シーン走査+ソートは1回のOnGUI呼び出しにつき1回だけにする(IMGUIはLayout/Repaint等で
            // 複数回呼ばれるため、2箇所で別々に取得すると毎回二重に走査してしまう)。
            var obstacles = Object.FindObjectsByType<DestructibleObstacle>(FindObjectsSortMode.None)
                .OrderBy(o => o.name)
                .ToArray();

            EditorGUILayout.Space(10);
            DrawObstacleList(obstacles);

            EditorGUILayout.Space(12);
            DrawSharedTuningSection(obstacles);
        }

        private void DrawCreationControls()
        {
            EditorGUILayout.LabelField("配置", EditorStyles.boldLabel);

            _tuningForNewObstacles = (DestructibleObstacleTuning)EditorGUILayout.ObjectField(
                new GUIContent("新規作成時に割り当てるチューニング", "未設定のままボタンを押すと、デフォルトのチューニングアセットを自動で使います。"),
                _tuningForNewObstacles, typeof(DestructibleObstacleTuning), false);

            _newObstacleSize = EditorGUILayout.Vector2Field(
                new GUIContent("新規障害物のサイズ(幅, 高さ)"), _newObstacleSize);
            _newObstacleSize = new Vector2(Mathf.Max(0.1f, _newObstacleSize.x), Mathf.Max(0.1f, _newObstacleSize.y));

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("シーン中心に新規障害物を追加", GUILayout.Height(24)))
                {
                    var tuning = ResolveTuningForNewObstacles();
                    var index = Object.FindObjectsByType<DestructibleObstacle>(FindObjectsSortMode.None).Length;
                    var created = CreateObstacle(Vector3.zero, 0f, tuning, _newObstacleSize, $"Obstacle_{index}");
                    Selection.activeGameObject = created.gameObject;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                _autoPlaceDistance = EditorGUILayout.FloatField(
                    new GUIContent("配置距離", "各砲台の正面、この距離だけ離れた位置に配置します。"),
                    _autoPlaceDistance);
                _autoPlaceDistance = Mathf.Max(0.1f, _autoPlaceDistance);

                if (GUILayout.Button("各砲台の正面に自動配置", GUILayout.Height(24)))
                {
                    AutoPlaceInFrontOfTurrets();
                }
            }
        }

        private DestructibleObstacleTuning ResolveTuningForNewObstacles()
        {
            if (_tuningForNewObstacles != null)
            {
                return _tuningForNewObstacles;
            }

            _tuningForNewObstacles = EditorAssetUtility.LoadOrCreateAsset<DestructibleObstacleTuning>(DefaultTuningPath);
            return _tuningForNewObstacles;
        }

        private void AutoPlaceInFrontOfTurrets()
        {
            var controller = Object.FindFirstObjectByType<FortressLayoutController>();
            if (controller == null)
            {
                EditorUtility.DisplayDialog(
                    "砲台が見つかりません",
                    "シーンに FortressLayoutController が見つかりません。先に「砲台配置」タブで砲台を用意してください。",
                    "OK");
                return;
            }

            controller.EnsureTurrets();
            var tuning = ResolveTuningForNewObstacles();
            var existing = Object.FindObjectsByType<DestructibleObstacle>(FindObjectsSortMode.None);

            foreach (var turret in (controller.turrets ?? System.Array.Empty<LaserTurret>()).Where(t => t != null))
            {
                var obstacleName = $"Obstacle_P{turret.playerIndex}";
                var position = turret.transform.position + turret.transform.up * _autoPlaceDistance;
                var facing = turret.transform.rotation.eulerAngles.z;

                var found = existing.FirstOrDefault(o => o != null && o.name == obstacleName);
                if (found != null)
                {
                    Undo.RecordObject(found.transform, "Reposition Obstacle");
                    found.transform.position = position;
                    found.transform.rotation = Quaternion.Euler(0f, 0f, facing);
                    EnsureMovementComponent(found, turret);
                    continue;
                }

                CreateObstacle(position, facing, tuning, _newObstacleSize, obstacleName, turret);
            }
        }

        private static DestructibleObstacle CreateObstacle(
            Vector3 position, float facingDegrees, DestructibleObstacleTuning tuning, Vector2 size, string name,
            LaserTurret ownerTurret = null)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create Destructible Obstacle");
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, 0f, facingDegrees);

            var obstacle = go.AddComponent<DestructibleObstacle>();
            obstacle.tuning = tuning;

            // tuningはAddComponent後に割り当てているため、Awake時点のHPスナップショットを取り直す。
            obstacle.ResetToFull();

            var movement = go.AddComponent<ObstacleMovement>();
            if (ownerTurret != null)
            {
                // どの砲台の正面に置かれたか分かっている場合は、最近傍探索に頼らず直接指定する。
                movement.nearAnchor = ownerTurret.transform;
            }

            go.AddComponent<ObstacleVisual>();

            // transform.localScaleを動かすので、コリジョンと見た目に同時に反映される。
            obstacle.SetSize(size);

            return obstacle;
        }

        /// <summary>
        /// この機能を追加する前に置かれた障害物には ObstacleMovement が付いていないため、
        /// 無ければ後付けする。既に付いている場合はnearAnchorだけ最新の砲台に合わせる。
        /// </summary>
        private static void EnsureMovementComponent(DestructibleObstacle obstacle, LaserTurret ownerTurret)
        {
            var movement = obstacle.GetComponent<ObstacleMovement>();
            if (movement == null)
            {
                movement = Undo.AddComponent<ObstacleMovement>(obstacle.gameObject);
            }

            if (ownerTurret != null)
            {
                Undo.RecordObject(movement, "Assign Movement Anchor");
                movement.nearAnchor = ownerTurret.transform;
            }
        }

        private void DrawObstacleList(DestructibleObstacle[] obstacles)
        {
            EditorGUILayout.LabelField("シーン内の障害物", EditorStyles.boldLabel);

            if (obstacles.Length == 0)
            {
                EditorGUILayout.HelpBox("障害物がまだ1つもありません。上のボタンから追加してください。", MessageType.Info);
                return;
            }

            foreach (var obstacle in obstacles)
            {
                DrawObstacleRow(obstacle);
            }
        }

        private static void DrawObstacleRow(DestructibleObstacle obstacle)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(obstacle.name, GUILayout.Width(120));

                var pos = obstacle.transform.position;
                EditorGUILayout.LabelField($"({pos.x:0.00}, {pos.y:0.00})", GUILayout.Width(110));

                if (Application.isPlaying)
                {
                    var barRect = GUILayoutUtility.GetRect(90, 16, GUILayout.Width(90));
                    var isDestroyed = obstacle.State == ObstacleState.Destroyed;
                    var label = isDestroyed
                        ? $"再生まで{obstacle.RegenDelayRemaining:0.0}s"
                        : $"HP {obstacle.HealthRatio01 * 100f:0}%";

                    var prevColor = GUI.color;
                    GUI.color = isDestroyed ? Color.gray : Color.Lerp(Color.red, Color.green, obstacle.HealthRatio01);
                    EditorGUI.ProgressBar(barRect, obstacle.HealthRatio01, label);
                    GUI.color = prevColor;
                }
                else
                {
                    var tuningName = obstacle.tuning != null ? obstacle.tuning.name : "(チューニング未設定)";
                    EditorGUILayout.LabelField(tuningName, GUILayout.Width(180));
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("シーンで選択", GUILayout.Width(90)))
                {
                    Selection.activeGameObject = obstacle.gameObject;
                    SceneView.lastActiveSceneView?.FrameSelected();
                }

                if (GUILayout.Button("削除", GUILayout.Width(50)))
                {
                    Undo.DestroyObjectImmediate(obstacle.gameObject);
                    GUIUtility.ExitGUI();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14);
                EditorGUILayout.LabelField("サイズ", GUILayout.Width(106));

                EditorGUI.BeginChangeCheck();
                var newSize = EditorGUILayout.Vector2Field(GUIContent.none, obstacle.Size, GUILayout.Width(180));
                if (EditorGUI.EndChangeCheck())
                {
                    DestructibleObstacleEditor.ResizeWithUndo(obstacle, newSize);
                }
            }

            if (obstacle.tuning == null)
            {
                EditorGUILayout.HelpBox($"「{obstacle.name}」にチューニングが割り当てられていません。", MessageType.Warning);
            }

            if (obstacle.GetComponent<ObstacleMovement>() == null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.HelpBox(
                        $"「{obstacle.name}」はランダム移動を追加する前に作られたため、移動コンポーネントがありません。",
                        MessageType.Warning);

                    if (GUILayout.Button("移動を追加", GUILayout.Width(90), GUILayout.Height(38)))
                    {
                        EnsureMovementComponent(obstacle, null);
                    }
                }
            }
        }

        private void DrawSharedTuningSection(DestructibleObstacle[] obstacles)
        {
            EditorGUILayout.LabelField("耐久・再生チューニング", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "ここでの変更は選択中の Destructible Obstacle Tuning アセットそのものを書き換えます。" +
                "同じアセットを参照している障害物すべてに即反映されます。",
                MessageType.None);

            var tuning = obstacles.Select(o => o.tuning).FirstOrDefault(t => t != null) ?? _tuningForNewObstacles;
            tuning = (DestructibleObstacleTuning)EditorGUILayout.ObjectField(
                new GUIContent("対象の Destructible Obstacle Tuning"), tuning, typeof(DestructibleObstacleTuning), false);

            if (tuning == null)
            {
                return;
            }

            EditorGUI.BeginChangeCheck();

            var maxHealth = EditorGUILayout.FloatField(new GUIContent("最大HP"), tuning.maxHealth);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("レーザーの強さ → 壊れる速さ", EditorStyles.miniBoldLabel);
            var scaleByStrength = EditorGUILayout.Toggle(
                new GUIContent("強さで壊れる速さを変える", "オフ=太さに関係なく一定速度で壊れる。"),
                tuning.scaleBreakSpeedByLaserStrength);
            AnimationCurve strengthCurve;
            using (new EditorGUI.DisabledScope(!scaleByStrength))
            {
                strengthCurve = EditorGUILayout.CurveField(
                    new GUIContent("太さ→ダメージ倍率カーブ", "横軸=レーザーの太さ(0-1)、縦軸=ダメージ倍率。"),
                    tuning.laserStrengthToBreakSpeedCurve);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("再生（時間経過で戻る）", EditorStyles.miniBoldLabel);
            var regenDelay = EditorGUILayout.FloatField(
                new GUIContent("再生までの待機時間(秒)", "破壊されてから再生を始めるまでの時間。"), tuning.regenDelay);
            var regenMode = (ObstacleRegenMode)EditorGUILayout.EnumPopup(
                new GUIContent("再生方式", "Instant=待機後に瞬時に満タン復活 / Gradual=時間をかけて少しずつ回復"), tuning.regenMode);
            var regenDuration = EditorGUILayout.FloatField(
                new GUIContent("再生にかかる時間(秒)", "Gradual方式のとき、0から満タンになるまでの所要時間。"), tuning.regenDuration);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("ひび割れ演出", EditorStyles.miniBoldLabel);
            var enableCrack = EditorGUILayout.Toggle(
                new GUIContent("ひびを表示する", "オフ=ダメージを受けても色の変化のみ。"), tuning.enableCrackVisual);
            AnimationCurve crackCurve;
            using (new EditorGUI.DisabledScope(!enableCrack))
            {
                crackCurve = EditorGUILayout.CurveField(
                    new GUIContent("残りHP→ひびの濃さカーブ", "横軸=残りHP(0-1)、縦軸=ひびの濃さ(0-1)。"),
                    tuning.crackAlphaCurve);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("破壊演出（ひびが限界に達して崩れ落ちる）", EditorStyles.miniBoldLabel);
            var fragmentCount = EditorGUILayout.IntSlider(
                new GUIContent("破片の数"), tuning.breakFragmentCount, 1, 12);
            var breakDuration = EditorGUILayout.FloatField(
                new GUIContent("破片が消えるまでの時間(秒)"), tuning.breakEffectDuration);
            var breakLaunchSpeed = EditorGUILayout.FloatField(
                new GUIContent("破片が飛び散る速さ"), tuning.breakEffectLaunchSpeed);
            var breakSpin = EditorGUILayout.FloatField(
                new GUIContent("破片の回転速度(度/秒)"), tuning.breakEffectSpinSpeedDegrees);
            var breakGravity = EditorGUILayout.FloatField(
                new GUIContent("破片の重力", "0にすると重力なしで飛び散るだけになる。"), tuning.breakEffectGravity);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("移動", EditorStyles.miniBoldLabel);
            var movementMode = (ObstacleMovementMode)EditorGUILayout.EnumPopup(
                new GUIContent(
                    "動き方",
                    "Stationary=動かない / Wander=プレイヤーとコアの間をランダムに漂う(直線移動) / " +
                    "Orbit=コアを中心に円形に周回する(自転ではなく公転)"),
                tuning.movementMode);

            float wanderSpeed;
            Vector2 wanderRange;
            Vector2 wanderPause;
            using (new EditorGUI.DisabledScope(movementMode != ObstacleMovementMode.Wander))
            {
                EditorGUILayout.LabelField("　Wander設定", EditorStyles.miniLabel);
                wanderSpeed = EditorGUILayout.FloatField(new GUIContent("移動速度"), tuning.wanderSpeed);
                wanderRange = EditorGUILayout.Vector2Field(
                    new GUIContent("移動できる範囲(0=プレイヤー側, 1=コア側)"), tuning.wanderRange01);
                wanderPause = EditorGUILayout.Vector2Field(
                    new GUIContent("立ち止まる時間の範囲(秒)"), tuning.wanderPauseSecondsRange);
            }

            float orbitRadius;
            float orbitSpeed;
            using (new EditorGUI.DisabledScope(movementMode != ObstacleMovementMode.Orbit))
            {
                EditorGUILayout.LabelField("　Orbit設定", EditorStyles.miniLabel);
                orbitRadius = EditorGUILayout.FloatField(new GUIContent("周回半径"), tuning.orbitRadius);
                orbitSpeed = EditorGUILayout.FloatField(
                    new GUIContent("周回速度(度/秒)", "負の値で回転方向が逆になる。"), tuning.orbitSpeedDegreesPerSecond);
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("見た目", EditorStyles.miniBoldLabel);
            var healthyColor = EditorGUILayout.ColorField(new GUIContent("健在時の色"), tuning.healthyColor);
            var damagedColor = EditorGUILayout.ColorField(new GUIContent("損傷時の色"), tuning.damagedColor);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(tuning, "Edit Destructible Obstacle Tuning");
                tuning.maxHealth = Mathf.Max(1f, maxHealth);
                tuning.scaleBreakSpeedByLaserStrength = scaleByStrength;
                tuning.laserStrengthToBreakSpeedCurve = strengthCurve;
                tuning.regenDelay = Mathf.Max(0f, regenDelay);
                tuning.regenMode = regenMode;
                tuning.regenDuration = Mathf.Max(0.01f, regenDuration);
                tuning.enableCrackVisual = enableCrack;
                tuning.crackAlphaCurve = crackCurve;
                tuning.breakFragmentCount = fragmentCount;
                tuning.breakEffectDuration = Mathf.Max(0.05f, breakDuration);
                tuning.breakEffectLaunchSpeed = Mathf.Max(0f, breakLaunchSpeed);
                tuning.breakEffectSpinSpeedDegrees = breakSpin;
                tuning.breakEffectGravity = Mathf.Max(0f, breakGravity);
                tuning.movementMode = movementMode;
                tuning.wanderSpeed = Mathf.Max(0f, wanderSpeed);
                tuning.wanderRange01 = new Vector2(Mathf.Clamp01(wanderRange.x), Mathf.Clamp01(wanderRange.y));
                tuning.wanderPauseSecondsRange = new Vector2(Mathf.Max(0f, wanderPause.x), Mathf.Max(0f, wanderPause.y));
                tuning.orbitRadius = Mathf.Max(0.1f, orbitRadius);
                tuning.orbitSpeedDegreesPerSecond = orbitSpeed;
                tuning.healthyColor = healthyColor;
                tuning.damagedColor = damagedColor;
                EditorUtility.SetDirty(tuning);
            }

            EditorGUILayout.Space(4);
            if (GUILayout.Button("この設定が未割り当ての障害物に適用"))
            {
                foreach (var obstacle in obstacles)
                {
                    if (obstacle.tuning == null)
                    {
                        Undo.RecordObject(obstacle, "Assign Destructible Obstacle Tuning");
                        obstacle.tuning = tuning;
                    }
                }
            }
        }
    }
}
