using System;
using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Stage;
using UnityEngine;

namespace MS2026.Stages
{
    /// <summary>
    /// 試合の始まりの演出「ステージの物が上から落ちてくる」（2026-10-26。ユーザーが見比べた 6 案から「ドスン」を選んだ）。
    /// ・ステージがコア・砲台に当てはめられたら（<see cref="StageLayout.Applied"/>）、ステージの目印に自動で付く（シーンの編集は要らない。全ステージ共通）
    /// ・すぐに障害物・コア・砲台（キャラの絵）を画面の上の外へ隠し、幕（<see cref="FortressScreenCover"/>）が全部消えてから
    ///   障害物（ばらばらの順）→ コア → 1P〜4P の順に、だんだん速く落として、着地でつぶして戻す。着地ごとに演出と画面の揺れ
    /// ・落ちる向きは、そのPCの画面の上から（カメラの向きが席ごとに違っても、それぞれの画面の上から落ちる）
    /// ・動かすのは見た目だけ（背景の物は Visual、壊せる物は作られた見た目）。終わったら元の位置に戻し、敵の通り道を作り直す
    /// ・終わるまでは、このPCは「準備ができていない」（<see cref="FortressWaves.AddReadyBlocker"/>）ので、WAVE 1 の発表は全員の演出の後
    /// ・途中参加など、もう試合が進んでいるときは演出しない（すぐに元の位置に置く）
    /// 通信はしない（各PCが自分で動かす）。設定は Resources の <see cref="StageDropIntroSettings"/>。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(1000)] // ほかの係（背景の物の揺れなど）が書いた後に、演出の分を足す
    [AddComponentMenu("")]
    public sealed class StageDropIntro : MonoBehaviour
    {
        private const string DestructibleVisualName = "[DestructibleVisual]";
        private const int OrderSeed = 2026;
        private const float FallbackDistance = 40f;

        private enum Step
        {
            Holding,
            Playing,
            Done
        }

        private static StageDropIntro _active;

        private readonly List<StageDropTarget> _targets = new List<StageDropTarget>();
        private StageDropIntroSettings _settings;
        private Step _step = Step.Done;
        private bool _isTest;
        private bool _needsCollect;
        private bool _lockedTurrets;
        private float _holdStart;
        private float _readyAt = -1f;
        private float _time;
        private float _endTime;

        /// <summary>演出が終わるまで待っている（隠している・落としている）か。試合の準備ができていない扱いになる。</summary>
        public static bool IsHolding => _active != null && _active._step != Step.Done && !_active._isTest;

        /// <summary>HUD を隠しておく間か。</summary>
        public static bool IsHudHidden => IsHolding && _active._settings != null && _active._settings.hideHud;

        /// <summary>今の設定（HUD の画面の名前・フェードの秒数を UI の側が読む）。</summary>
        public static StageDropIntroSettings Settings => _active != null && _active._settings != null ? _active._settings : StageDropIntroSettings.Load();

        /// <summary>今読み込まれているステージの演出（無ければ null）。</summary>
        public static StageDropIntro Active => _active;

        /// <summary>演出を始めた（物を隠した）。</summary>
        public static event Action Began;

        /// <summary>演出が終わった（全部着地した・飛ばした）。</summary>
        public static event Action Finished;

        /// <summary>落としている途中か（試しの再生も含む）。</summary>
        public bool IsRunning => _step != Step.Done;

        /// <summary>落としている物の数（ツールの表示用）。</summary>
        public int TargetCount => _targets.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _active = null;
            Began = null;
            Finished = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void Register()
        {
            FortressWaves.AddReadyBlocker(() => IsHolding);
            StageLayout.Applied -= OnLayoutApplied;
            StageLayout.Applied += OnLayoutApplied;
        }

        private static void OnLayoutApplied(StageLayout layout)
        {
            if (!Application.isPlaying || layout == null)
            {
                return;
            }

            var settings = StageDropIntroSettings.Load();
            if (!settings.enabled || (layout.definition != null && !layout.definition.playDropIntro))
            {
                return;
            }

            if (!layout.TryGetComponent(out StageDropIntro intro))
            {
                intro = layout.gameObject.AddComponent<StageDropIntro>();
            }

            intro.Begin(settings, false);
        }

        /// <summary>
        /// 試しにもう一度落とす（Play 中、設定の画面のボタンから）。試合の準備・HUD・レーザーには影響しない。
        /// </summary>
        public static bool PlayTest()
        {
            var layout = StageLayout.Active;
            if (!Application.isPlaying || layout == null)
            {
                return false;
            }

            if (!layout.TryGetComponent(out StageDropIntro intro))
            {
                intro = layout.gameObject.AddComponent<StageDropIntro>();
            }

            intro.Begin(StageDropIntroSettings.Load(), true);
            return true;
        }

        private void Begin(StageDropIntroSettings settings, bool isTest)
        {
            if (_step != Step.Done)
            {
                if (isTest)
                {
                    Finish(false);
                }
                else
                {
                    // ステージの当てはめ直し（同じステージ）。もう始めている。
                    return;
                }
            }

            _settings = settings;
            _isTest = isTest;
            _targets.Clear();
            _active = this;
            _step = Step.Holding;
            _holdStart = Time.unscaledTime;
            _readyAt = -1f;
            _time = 0f;
            // 集めるのは、このフレームの最後（LateUpdate）。ステージがゲームより後に読み込まれたときは、
            // 当てはめ（ステージの目印の Awake）がステージのほかの物の準備より先に来るので、
            // ここで集めると背景の物（StageProp の登録は OnEnable）がまだ見つからない。LateUpdate なら描く前に隠せる。
            _needsCollect = true;
            Began?.Invoke();
        }

        // 集めて、隠す（試しの再生は幕を待たずにすぐ落とす）。落とす物が無ければ終わる。
        private void CollectAndHide()
        {
            _needsCollect = false;
            Collect();
            if (_targets.Count == 0)
            {
                Finish(false);
                return;
            }

            if (_isTest)
            {
                StartPlaying();
            }
            else
            {
                HideAll();
            }
        }

        // 隠している間に、あとから準備ができた背景の物があれば足す（読み込みの順番が変わっても漏れないように）。
        private void CollectLateProps()
        {
            var scene = gameObject.scene;
            foreach (var prop in StageProp.All)
            {
                if (prop == null || prop.gameObject.scene != scene || prop.role == StagePropRole.Floor || prop.visualRoot == null ||
                    IsTracked(prop.visualRoot) || IsSkipped(prop.transform) || !HasRenderer(prop.visualRoot))
                {
                    continue;
                }

                _targets.Add(new StageDropTarget(prop.visualRoot, StageDropKind.Prop, IsFlat(prop.visualRoot), -1, RadiusOf(prop.visualRoot)));
            }
        }

        private bool IsTracked(Transform transform)
        {
            foreach (var target in _targets)
            {
                if (target.Transform == transform)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnDisable()
        {
            // ステージが外れた・壊れたときは、元に戻して終わる（準備の待ちを残さない）。
            if (_step != Step.Done)
            {
                Finish(false);
            }

            if (_active == this)
            {
                _active = null;
            }
        }

        private void LateUpdate()
        {
            if (_needsCollect && _step != Step.Done)
            {
                CollectAndHide();
                if (_step == Step.Done || _isTest)
                {
                    return;
                }
            }

            switch (_step)
            {
                case Step.Holding:
                    TickHolding();
                    break;
                case Step.Playing:
                    TickPlaying(Time.deltaTime);
                    break;
            }
        }

        private void TickHolding()
        {
            LockTurrets();
            CollectLateProps();
            HideAll();
            var now = Time.unscaledTime;
            if (now - _holdStart > _settings.maxWaitSeconds)
            {
                Finish(false);
                return;
            }

            if (FortressScreenCover.IsShowing)
            {
                _readyAt = -1f;
                return;
            }

            if (_readyAt < 0f)
            {
                _readyAt = now + _settings.startDelay;
            }

            if (now < _readyAt)
            {
                return;
            }

            if (MatchAlreadyRunning())
            {
                // 途中参加など。演出なしで元の位置に置く。
                Finish(false);
                return;
            }

            StartPlaying();
        }

        private void StartPlaying()
        {
            Schedule();
            _time = 0f;
            _step = Step.Playing;
            TickPlaying(0f);
        }

        private void TickPlaying(float dt)
        {
            if (!_isTest && MatchAlreadyRunning())
            {
                Finish(false);
                return;
            }

            LockTurrets();
            _time += dt;
            var camera = FindCamera();
            var up = ScreenUp(camera);
            foreach (var target in _targets)
            {
                if (!target.IsAlive)
                {
                    continue;
                }

                var kind = _settings.Get(target.Kind);
                var pose = StageDropTimeline.Evaluate(_time - target.Delay, kind.fallSeconds, kind.fallStretch, kind.squash, kind.squashSeconds);
                var landing = target.LandingPosition;
                var offset = pose.Height01 > 0f ? up * (DistanceToLeaveScreen(camera, landing, up, target.Radius) * pose.Height01) : Vector3.zero;
                target.Apply(offset, ScaleFor(target, pose));
                if (pose.Landed && !target.Landed)
                {
                    target.Landed = true;
                    PlayLand(target, kind, landing);
                }
            }

            if (_time >= _endTime)
            {
                Finish(true);
            }
        }

        private void HideAll()
        {
            var camera = FindCamera();
            var up = ScreenUp(camera);
            foreach (var target in _targets)
            {
                if (target.IsAlive)
                {
                    target.Apply(up * DistanceToLeaveScreen(camera, target.LandingPosition, up, target.Radius), Vector3.one);
                }
            }
        }

        private void Finish(bool played)
        {
            foreach (var target in _targets)
            {
                target.Release();
            }

            _targets.Clear();
            var wasTest = _isTest;
            _step = Step.Done;
            if (_lockedTurrets)
            {
                _lockedTurrets = false;
                // 合間（Break）のロックはウェーブの係のものなので触らない。
                if (FortressWaves.Phase != WavePhase.Break)
                {
                    LaserTurret.InputLocked = false;
                }
            }

            if (!wasTest)
            {
                // 見た目だけ動かしたが、根元ごと動かした物（絵の壁・コアなど）もあるので、敵の通り道を作り直しておく。
                if (EnemyNavigation.Current is NavigationField field)
                {
                    field.MarkDirty();
                }
            }

            if (played && _settings != null)
            {
                FortressEffectPlayer.PlayOnce(_settings.onFinished, transform.position, Quaternion.identity);
            }

            Finished?.Invoke();
        }

        private void LockTurrets()
        {
            if (_isTest || _settings == null || !_settings.lockTurrets)
            {
                return;
            }

            // ウェーブの係が試合の始まりにロックを外すことがあるので、演出の間は毎フレームかけ直す。
            LaserTurret.InputLocked = true;
            _lockedTurrets = true;
        }

        private static bool MatchAlreadyRunning()
        {
            // 計画の無いステージ（敵がすぐ来る）、もう WAVE が始まっている（途中参加）ときは演出しない。
            return FortressWaves.Phase != WavePhase.WaitingForPlayers;
        }

        // ───────── 落とす物を集める ─────────

        private void Collect()
        {
            _targets.Clear();
            var scene = gameObject.scene;
            var used = new HashSet<Transform>();
            var props = new List<StageDropTarget>();

            // 背景の物（床は落とさない）。動かすのは揺れの支点（Visual）。
            foreach (var prop in StageProp.All)
            {
                if (prop == null || prop.gameObject.scene != scene || prop.role == StagePropRole.Floor || prop.visualRoot == null || IsSkipped(prop.transform))
                {
                    continue;
                }

                AddProp(props, used, prop.visualRoot);
            }

            // 背景の物ではない障害物（壁・壊せる物）。壊せる物は作られた見た目、それ以外は根元ごと。
            foreach (var obstacle in FindObjectsByType<NavigationObstacle>(FindObjectsSortMode.None))
            {
                AddObstacle(props, used, obstacle.transform, scene);
            }

            foreach (var obstacle in FindObjectsByType<DestructibleObstacle>(FindObjectsSortMode.None))
            {
                AddObstacle(props, used, obstacle.transform, scene);
            }

            // どのPCでも同じ順になるように、場所の名前で並べてから混ぜる。
            props.Sort((a, b) => string.CompareOrdinal(PathOf(a.Transform), PathOf(b.Transform)));
            var order = StageDropTimeline.ShuffledOrder(props.Count, OrderSeed);
            foreach (var index in order)
            {
                _targets.Add(props[index]);
            }

            var core = FindFirstObjectByType<CoreCrystalController>();
            if (core != null && !IsSkipped(core.transform))
            {
                _targets.Add(new StageDropTarget(core.transform, StageDropKind.Core, true, -1, RadiusOf(core.transform)));
            }

            var turrets = new List<LaserTurret>(FindObjectsByType<LaserTurret>(FindObjectsSortMode.None));
            turrets.Sort((a, b) => a.playerIndex.CompareTo(b.playerIndex));
            foreach (var turret in turrets)
            {
                if (!IsSkipped(turret.transform))
                {
                    _targets.Add(new StageDropTarget(turret.transform, StageDropKind.Player, true, turret.playerIndex, RadiusOf(turret.transform)));
                }
            }
        }

        private static void AddProp(List<StageDropTarget> props, HashSet<Transform> used, Transform visual)
        {
            if (!used.Add(visual) || !HasRenderer(visual))
            {
                return;
            }

            props.Add(new StageDropTarget(visual, StageDropKind.Prop, IsFlat(visual), -1, RadiusOf(visual)));
        }

        private static void AddObstacle(List<StageDropTarget> props, HashSet<Transform> used, Transform root, UnityEngine.SceneManagement.Scene scene)
        {
            if (root.gameObject.scene != scene || root.GetComponentInParent<StageProp>() != null || IsSkipped(root))
            {
                return;
            }

            // 親がもう落とす物なら、一緒に落ちるので足さない。
            for (var parent = root.parent; parent != null; parent = parent.parent)
            {
                if (used.Contains(parent))
                {
                    return;
                }
            }

            var built = root.Find(DestructibleVisualName);
            AddProp(props, used, built != null && HasRenderer(built) ? built : root);
        }

        private static bool IsSkipped(Transform transform)
        {
            return transform.GetComponentInParent<StageDropIntroSkip>(true) != null;
        }

        private static bool HasRenderer(Transform root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (IsShape(renderer))
                {
                    return true;
                }
            }

            return false;
        }

        // 絵だけの物か（3D モデルが入っていれば false）。
        private static bool IsFlat(Transform root)
        {
            var hasSprite = false;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    return false;
                }

                hasSprite |= renderer is SpriteRenderer;
            }

            return hasSprite;
        }

        private static float RadiusOf(Transform root)
        {
            var has = false;
            var bounds = new Bounds(root.position, Vector3.zero);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!IsShape(renderer))
                {
                    continue;
                }

                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            // 足元から見た大きさ。絵は立てて描かれることもあるので、高さ（Z）も含める。
            var extents = bounds.extents;
            var fromRoot = (Vector2)(bounds.center - root.position);
            return Mathf.Max(extents.x, extents.y, extents.z * 2f) + fromRoot.magnitude + 0.5f;
        }

        private static bool IsShape(Renderer renderer)
        {
            return renderer != null && renderer.enabled && !(renderer is ParticleSystemRenderer) && !(renderer is TrailRenderer) && !(renderer is LineRenderer);
        }

        private static string PathOf(Transform transform)
        {
            var path = transform.name;
            for (var parent = transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        // ───────── 時間割 ─────────

        private void Schedule()
        {
            var propCount = 0;
            foreach (var target in _targets)
            {
                if (target.Kind == StageDropKind.Prop)
                {
                    propCount++;
                }
            }

            var delays = StageDropTimeline.PropDelays(propCount, _settings.propsSpreadSeconds, _settings.propsMaxInterval);
            var propIndex = 0;
            var propsLanded = 0f;
            foreach (var target in _targets)
            {
                if (target.Kind == StageDropKind.Prop)
                {
                    target.Delay = delays[propIndex++];
                    propsLanded = Mathf.Max(propsLanded, target.Delay + _settings.props.fallSeconds);
                }
            }

            var coreLanded = propsLanded;
            foreach (var target in _targets)
            {
                if (target.Kind == StageDropKind.Core)
                {
                    target.Delay = Mathf.Max(0f, propsLanded + _settings.coreGap);
                    coreLanded = Mathf.Max(coreLanded, target.Delay + _settings.core.fallSeconds);
                }
            }

            var playerIndex = 0;
            foreach (var target in _targets)
            {
                if (target.Kind == StageDropKind.Player)
                {
                    target.Delay = Mathf.Max(0f, coreLanded + _settings.playersGap + playerIndex++ * _settings.playersInterval);
                }
            }

            _endTime = 0f;
            foreach (var target in _targets)
            {
                target.Landed = false;
                _endTime = Mathf.Max(_endTime, target.Delay + _settings.Get(target.Kind).TotalSeconds);
            }
        }

        // ───────── 見た目 ─────────

        private static Vector3 ScaleFor(StageDropTarget target, StageDropPose pose)
        {
            if (target.Flat)
            {
                // 絵: 落ちる間は縦に伸び、着地で横に広がって縦に縮む。
                return new Vector3(1f - pose.Stretch * 0.5f + pose.Squash, 1f + pose.Stretch - pose.Squash, 1f);
            }

            // 3D モデル: 床から上（Z）が高さ。着地で床に沿って広がり、高さが縮む。
            return new Vector3(1f - pose.Stretch * 0.5f + pose.Squash, 1f - pose.Stretch * 0.5f + pose.Squash, 1f + pose.Stretch - pose.Squash);
        }

        private static void PlayLand(StageDropTarget target, StageDropKindSettings kind, Vector3 landing)
        {
            FortressEffectPlayer.PlayOnce(kind.onLand, landing, Quaternion.identity, target.PlayerIndex);
            var rig = FortressCameraRig.Active;
            if (rig != null && kind.landShake != null && kind.landShake.amplitude > 0f)
            {
                rig.AddModifier(new SimpleShakeModifier(kind.landShake, 1f));
            }
        }

        private static Camera FindCamera()
        {
            var rig = FortressCameraRig.Active;
            return rig != null && rig.targetCamera != null ? rig.targetCamera : Camera.main;
        }

        /// <summary>このPCの画面の上の向き（床の上の向き）。</summary>
        private static Vector3 ScreenUp(Camera camera)
        {
            if (camera == null)
            {
                return Vector3.up;
            }

            var up = camera.transform.up;
            up.z = 0f;
            return up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        }

        /// <summary>着地点から画面の上へ、どれだけずらせば画面の外に出きるか。</summary>
        private float DistanceToLeaveScreen(Camera camera, Vector3 landing, Vector3 up, float radius)
        {
            if (camera == null)
            {
                return FallbackDistance;
            }

            var viewport = camera.WorldToViewportPoint(landing);
            if (viewport.z <= 0f)
            {
                return FallbackDistance;
            }

            var ray = camera.ViewportPointToRay(new Vector3(Mathf.Clamp(viewport.x, -0.5f, 1.5f), 1f + _settings.startAboveScreen, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-4f)
            {
                return FallbackDistance;
            }

            // 着地点と同じ高さ（Z）の面で、画面の上のふちの少し外にあたる所。
            var along = (landing.z - ray.origin.z) / ray.direction.z;
            if (along <= 0f)
            {
                return FallbackDistance;
            }

            var edge = ray.origin + ray.direction * along;
            return Mathf.Max(radius, Vector3.Dot(edge - landing, up) + radius);
        }
    }
}
