using MS2026.Fortress.Cameras;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MS2026.Fortress
{
    /// <summary>
    /// 【実験・ブランチ「佐伯＿ちょっと移動の実験」だけ。main には入れない】
    /// WASD で自分の砲台を動かす係（起動時に自動で作られる、見えないオブジェクト。シーンの編集は要らない）。
    ///
    /// ・射撃は今まで通り（握る / Q キー）。向きも今まで通り自動で回る。動くのは場所だけ。
    /// ・障害物（トリガーではない Collider2D）とコア（歯）に当たって止まる。壁に当たると沿って滑る。
    /// ・敵（群衆）と重なると押し出される。押し出された先でも壁には埋まらない。
    /// ・動ける範囲は既定でステージの床全域（<see cref="TurretMoveHooks.SetFloorAreaProvider"/>。床が無ければ NavigationField の範囲）。
    ///   設定で「席のまわりの円」にも戻せる（そのときは端に近づくと薄い輪が出る）。端は少しずつ弱まる。
    /// ・カメラは少し遅れて付いてきて、動いている方向の先を少し見せ、全速の間だけ少し引く（<see cref="ICameraFollowSource"/>）。
    /// ・落下演出の間（<see cref="TurretMoveHooks.IsHeld"/>）は動けず、カメラも追わずに元の視点のまま。
    /// ・歯みがきタイム・席替えの間（<see cref="LaserTurret.InputLocked"/> / <see cref="FortressSeats.IsMoving"/>）は動けない。
    ///   席替えでは今いる場所から次の席へ跳ぶので、着地すると席に戻っている。
    /// ・菌に触れ続けると倒れる。倒れている間は撃てない・動けない・菌は素通り。味方のビーム／握る連打で復活し、直後は少しやられない
    ///   （<see cref="TurretDownSystem"/>。Host・オフラインが決め、Client へ配る。倒れた人の画面にだけ案内 <see cref="TurretDownOverlay"/>）。
    /// ・LAN: 各 PC が自分の砲台を動かして位置を送る（<see cref="TurretMoveNet"/>）。他の人の砲台は届いた位置へなめらかに追う。
    /// 設定: Resources/TurretMoveSettings（<see cref="TurretMoveSettings"/>）。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-20)]
    internal sealed class TurretMoveDirector : MonoBehaviour, ICameraFollowSource
    {
        private const int PlayerCount = 4;
        private const float TurretRefreshInterval = 1f;

        private readonly LaserTurret[] _turrets = new LaserTurret[PlayerCount];
        private readonly Vector2[] _capturedHomes = new Vector2[PlayerCount];
        private readonly bool[] _hasCapturedHome = new bool[PlayerCount];
        private readonly TurretMoveNet _net = new TurretMoveNet();
        private readonly TurretMoveRing _ring = new TurretMoveRing();
        private readonly CoreCrystalController[] _cores = new CoreCrystalController[8];

        // 歯がビームを止めるための当たり（コアごとに1つ。コアの子にはしない＝レーザーが「当てた物」を親へたどってもコアに届かない）。
        private readonly CircleCollider2D[] _coreBlockers = new CircleCollider2D[8];
        private readonly float[] _coreRadii = new float[8];

        private readonly TurretMoveRegion _region = new TurretMoveRegion();

        // 菌にやられる・復活する
        private readonly TurretDownSystem _down = new TurretDownSystem();
        private readonly TurretDownView[] _downViews = new TurretDownView[PlayerCount];
        private readonly TurretDownOverlay _downOverlay = new TurretDownOverlay();
        private readonly TurretFade[] _fades = { new TurretFade(), new TurretFade(), new TurretFade(), new TurretFade() };
        private readonly TurretDownedFx[] _downedFx = { new TurretDownedFx(), new TurretDownedFx(), new TurretDownedFx(), new TurretDownedFx() };
        private readonly CircleCollider2D[] _downBlockers = new CircleCollider2D[PlayerCount];
        private bool _localLastGripping;

        private TurretMoveSettings _settings;
        private SwarmSystem _swarm;
        private float _refreshTimer;
        private int _coreCount;

        // 落下演出など、別の係が砲台を動かしている間（TurretMoveHooks.IsHeld）。
        private bool _held;

        // 自分の砲台
        private int _localPlayer = -1;
        private Vector2 _inputVelocity;
        private Vector2 _pushVelocity;
        private Vector2 _rawPush;
        private Vector2 _measuredVelocity;

        // カメラ
        private Vector2 _cameraOffset;
        private Vector2 _cameraOffsetSpeed;
        private Vector2 _lookAhead;
        private Vector2 _lookAheadSpeed;
        private float _zoom;
        private float _zoomSpeed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<TurretMoveDirector>() != null)
            {
                return;
            }

            // DontSave は付けない（付けると、エディタで Play を止めても消えずに残ってしまう）。DontDestroyOnLoad だけなら Play を止めると消える。
            var go = new GameObject("[実験] WASD 移動");
            DontDestroyOnLoad(go);
            go.AddComponent<TurretMoveDirector>();
        }

        private void OnEnable()
        {
            _settings = TurretMoveSettings.Load();
            FortressCameraRig.FollowSource = this;
            for (var p = 0; p < PlayerCount; p++)
            {
                _downViews[p] ??= new TurretDownView();
            }
        }

        private void OnDisable()
        {
            if (ReferenceEquals(FortressCameraRig.FollowSource, this))
            {
                FortressCameraRig.FollowSource = null;
            }

            Subscribe(null);
            _net.Shutdown();
            _ring.Dispose();
            _down.Reset(_turrets);
            foreach (var view in _downViews)
            {
                view?.Dispose();
            }

            _downOverlay.Dispose();
            for (var p = 0; p < PlayerCount; p++)
            {
                _fades[p].Apply(_turrets[p], false, 0f, 1f, 1f);
                TurretMoveHooks.SetDowned(p, false);
                _downedFx[p].Dispose();
                if (_downBlockers[p] != null)
                {
                    Destroy(_downBlockers[p].gameObject);
                    _downBlockers[p] = null;
                }
            }
        }

        private void Update()
        {
            var dt = Time.deltaTime;
            DetectMatch();

            var wasHeld = _held;
            _held = TurretMoveHooks.IsHeld;
            if (wasHeld && !_held)
            {
                // 落下演出が終わった。床も一緒に落ちていたので、着地した後の場所で床の範囲を取り直す。
                _refreshTimer = 0f;
                _net.Clear();
            }

            RefreshTurrets(Time.unscaledDeltaTime);
            UpdateCoreBlockers();
            if (_swarm == null)
            {
                return;
            }

            _localPlayer = FortressSeatMover.LocalPlayer();
            _net.Tick(Time.unscaledDeltaTime, _settings, _turrets, _localPlayer, _held, _down);

            TickDown(dt);
            TickLocal(dt);
            TickRemotes(dt);
        }

        // ───────── 菌にやられる・復活する ─────────

        private void TickDown(float dt)
        {
            if (_net.IsClient)
            {
                // Client: Host が決めた調子を映すだけ（砲台の撃つ・撃たないも Host から届く）。
                for (var p = 0; p < PlayerCount; p++)
                {
                    if (_net.TryGetCondition(p, out var condition, out var dirt, out var revive))
                    {
                        _down.ApplyRemote(p, condition, dirt, revive);
                    }
                }
            }
            else
            {
                // 落下演出・席替えの間は汚れが溜まらない（砲台を別の係が動かしているので）。
                _down.TickAuthority(dt, _turrets, _settings, _held || FortressSeats.IsMoving, FortressWaves.Phase == WavePhase.Break);
            }

            _down.PlayTransitionEffects(_turrets, _settings);

            for (var p = 0; p < PlayerCount; p++)
            {
                _downViews[p]?.Update(transform, _turrets[p], _down.States[p], _settings.bodyRadius, p);
            }

            // スキル欄などが「倒れているか」を見られるように知らせる（全PCで同じ値）。倒れている砲台はビームを止める。
            for (var p = 0; p < PlayerCount; p++)
            {
                var downed = _settings.downEnabled && _down.IsDowned(p);
                TurretMoveHooks.SetDowned(p, downed);
                UpdateDownBlocker(p, downed);
                _downedFx[p].Update(transform, _turrets[p], downed, _settings.bodyRadius, Time.unscaledDeltaTime);
            }

            // 自分の画面にだけ: 右上の体力バーと、倒れている間の抜け出し方の案内。
            var localGripping = ReadLocalGripping(out var keyName);
            var pressed = localGripping && !_localLastGripping;
            _localLastGripping = localGripping;
            var hasLocal = TurretOf(_localPlayer) != null;
            var localState = hasLocal ? _down.States[_localPlayer] : null;
            var localDowned = hasLocal && localState.Condition == TurretCondition.Downed;
            _downOverlay.Update(
                hasLocal && _settings.downEnabled && _settings.showHealthBar,
                hasLocal ? localState.Health01 : 1f,
                localDowned,
                localDowned ? localState.Revive01 : 0f,
                Mathf.Max(0, _localPlayer),
                keyName,
                pressed,
                Time.unscaledDeltaTime);
        }

        // 倒れた人の砲台は、ほかの人の画面で色あせて見せる（キャラの絵は毎フレーム色を入れ直すので、全員の描き終わった後に重ねる）。
        private void LateUpdate()
        {
            for (var p = 0; p < PlayerCount; p++)
            {
                var faded = _settings != null && _settings.downEnabled && p != _localPlayer && _down.IsDowned(p);
                _fades[p].Apply(
                    _turrets[p],
                    faded,
                    _settings != null ? _settings.downedDesaturate : 0f,
                    _settings != null ? _settings.downedDarken : 1f,
                    _settings != null ? _settings.downedAlpha : 1f);
            }
        }

        // 倒れている砲台に、トリガーではない丸い当たりを付ける（障害物と同じく、ビームはそこで止まる。群衆の敵へのダメージも止まった所まで）。
        // 砲台の子なので、移動の当たり判定（砲台の当たりは無視する）には引っかからない。全PCが自分で付ける（倒れているかは Host から届く）。
        private void UpdateDownBlocker(int player, bool downed)
        {
            var turret = _turrets[player];
            var blocker = _downBlockers[player];
            if (blocker != null && (turret == null || blocker.transform.parent != turret.transform))
            {
                Destroy(blocker.gameObject);
                blocker = null;
                _downBlockers[player] = null;
            }

            if (!downed || turret == null)
            {
                if (blocker != null && blocker.enabled)
                {
                    blocker.enabled = false;
                }

                return;
            }

            if (blocker == null)
            {
                var go = new GameObject(TurretMoveHooks.DownedBlockerName);
                go.transform.SetParent(turret.transform, false);
                blocker = go.AddComponent<CircleCollider2D>();
                _downBlockers[player] = blocker;
            }

            // 当たりの半径は「体の半径」（砲台の拡大率を打ち消す）。
            var scale = turret.transform.lossyScale;
            var s = Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
            blocker.radius = _settings.bodyRadius / s;
            blocker.enabled = true;
        }

        // この PC の握力（案内を跳ねさせるのに使うだけ。数えるのは Host）。通信中はこの PC のセンサー 0（キーボードなら Q）、オフラインは自分の番号のセンサー。
        private bool ReadLocalGripping(out string keyName)
        {
            var sensor = _net.IsActive ? 0 : Mathf.Clamp(_localPlayer, 0, PlayerCount - 1);
            var keys = global::MS2026.GripInputBridge.Transports.SimulatedGripTransport.DefaultKeyBindings;
            keyName = sensor < keys.Length ? keys[sensor].ToString() : "Q";
            return global::MS2026.GripInputBridge.GripInputBridge.Provider.IsGripping(sensor);
        }

        // ───────── 自分の砲台 ─────────

        private void TickLocal(float dt)
        {
            var turret = TurretOf(_localPlayer);
            if (turret == null || dt <= 0f)
            {
                _measuredVelocity = Vector2.zero;
                _ring.Update(transform, false, default, 0f, 0f, 0f, 0, Time.unscaledDeltaTime);
                return;
            }

            var t = turret.transform;
            var from = (Vector2)t.position;
            var home = HomeOfPlayer(_localPlayer, from);
            var range = _settings.rangeRadius;
            var radius = _settings.bodyRadius;
            var useFloor = _settings.area == TurretMoveArea.WholeFloor && !_region.IsEmpty;
            // 枠の内側では、体の半径に加えて「枠からさらに内側」の幅も空ける。
            var margin = radius + (_region.Frames.Count > 0 ? _settings.frameInset : 0f);

            if (!CanMove())
            {
                // 動けない間は勢いを捨てる（席替え・歯みがきタイムの後に、前の勢いで滑り出さないように）。
                _inputVelocity = Vector2.zero;
                _pushVelocity = Vector2.zero;
                _rawPush = Vector2.zero;
                _measuredVelocity = Vector2.zero;
                _ring.Update(transform, false, home, range, t.position.z, 0f, _localPlayer, Time.unscaledDeltaTime);
                return;
            }

            var desired = ReadInput() * _settings.moveSpeed;
            _inputVelocity = TurretMoveMath.StepVelocity(_inputVelocity, desired, _settings.acceleration, _settings.deceleration, dt);
            _inputVelocity = useFloor
                ? _region.Soften(_inputVelocity, from, margin, _settings.rangeSoftEdge)
                : TurretMoveMath.SoftenAtEdge(_inputVelocity, from, home, range, _settings.rangeSoftEdge);

            var pushTarget = _settings.pushedByEnemies
                ? TurretMoveMath.PushToVelocity(_rawPush, _settings.enemyPushStrength, _settings.maxEnemyPushSpeed)
                : Vector2.zero;
            _pushVelocity = Vector2.Lerp(_pushVelocity, pushTarget, TurretMoveMath.Blend(_settings.enemyPushResponse, dt));

            var delta = (_inputVelocity + _pushVelocity) * dt;
            var to = TurretMoveCollision.Move(from, delta, radius, _settings.obstacleLayers);
            LogBlocker();
            to = PushOutOfCores(to, radius);
            to = useFloor ? _region.Clamp(to, margin) : TurretMoveMath.ClampToRange(to, home, range);
            // 範囲の端で押し戻したせいで壁にめり込んだら、もう一度外へ出す。
            to = TurretMoveCollision.Depenetrate(to, radius, _settings.obstacleLayers);

            t.position = new Vector3(to.x, to.y, t.position.z);
            _measuredVelocity = (to - from) / dt;

            // 壁に当たって動けなかった分の勢いは捨てる（壁から離れるときに、溜まった勢いで飛び出さないように）。
            if (_inputVelocity.sqrMagnitude > 1e-4f)
            {
                var along = Vector2.Dot(_measuredVelocity - _pushVelocity, _inputVelocity.normalized);
                if (along < _inputVelocity.magnitude)
                {
                    _inputVelocity = _inputVelocity.normalized * Mathf.Max(0f, along);
                }
            }

            // 輪は「席のまわり」のときだけ（床全域なら床の端そのものが見えている）。
            var distToEdge = range - (to - home).magnitude;
            var closeness = Mathf.Clamp01(1f - distToEdge / Mathf.Max(0.1f, _settings.ringShowDistance));
            var showRing = !useFloor && _settings.showRangeRing && _settings.movementEnabled;
            _ring.Update(transform, showRing, home, range, t.position.z, closeness, _localPlayer, Time.unscaledDeltaTime);
        }

        private bool CanMove()
        {
            return _settings.movementEnabled && !LaserTurret.InputLocked && !FortressSeats.IsMoving && !_held && !_down.IsDowned(_localPlayer);
        }

        // 動ける範囲: ステージの枠の内側（無ければ床、床も無ければ敵の通り道の範囲 NavigationField）。
        private void RefreshFloor()
        {
            if (TurretMoveHooks.TryGetFloorArea(_settings.areaFrameName, _region))
            {
                return;
            }

            var field = FindFirstObjectByType<NavigationField>();
            if (field != null && field.areaSize.x > 0f && field.areaSize.y > 0f)
            {
                _region.Rects.Add(new Rect(field.areaCenter - field.areaSize * 0.5f, field.areaSize));
            }
        }

        // エディタだけ: 何に止められたかを Console に出す（同じ物に当たり続けている間は1回だけ）。
        private void LogBlocker()
        {
#if UNITY_EDITOR
            var blocker = TurretMoveCollision.LastBlocker;
            if (blocker == _lastLoggedBlocker)
            {
                return;
            }

            _lastLoggedBlocker = blocker;
            if (blocker != null && _settings.logBlockingObstacles)
            {
                Debug.Log($"[実験 WASD 移動] 「{PathOf(blocker.transform)}」に当たって止まりました（{blocker.GetType().Name}{(blocker.isTrigger ? "・トリガー" : "")}）。", blocker);
            }
#endif
        }

#if UNITY_EDITOR
        private Collider2D _lastLoggedBlocker;

        private static string PathOf(Transform t)
        {
            var path = t.name;
            for (var p = t.parent; p != null; p = p.parent)
            {
                path = p.name + "/" + path;
            }

            return path;
        }
#endif

        private Vector2 ReadInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var y = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            var input = new Vector2(x, y);
            if (input.sqrMagnitude < 1e-6f)
            {
                return Vector2.zero;
            }

            input = Vector2.ClampMagnitude(input, 1f);
            if (!_settings.screenRelative)
            {
                return input;
            }

            // 画面の上下左右 → ワールドの向き（画面を回した視点でも W が画面の上）。
            var up = (Vector2)FortressSeatMover.ScreenUp();
            var right = new Vector2(up.y, -up.x);
            return Vector2.ClampMagnitude(right * input.x + up * input.y, 1f);
        }

        private Vector2 PushOutOfCores(Vector2 position, float radius)
        {
            for (var i = 0; i < _coreCount; i++)
            {
                var core = _cores[i];
                if (core != null && core.isActiveAndEnabled)
                {
                    position = TurretMoveMath.PushOutOfCircle(position, radius, core.transform.position, _coreRadii[i]);
                }
            }

            return position;
        }

        // ───────── 歯がビームを止める ─────────

        // 歯の位置に、トリガーではない丸い当たりを置く。レーザーは「トリガーではない当たり」で止まるので、壁と同じく歯で止まる。
        // 歯は ILaserTarget ではないので傷つかない。群衆の敵へのダメージも、ビームが止まった所までになる（歯の向こうには届かない）。
        private void UpdateCoreBlockers()
        {
            for (var i = 0; i < _coreBlockers.Length; i++)
            {
                var core = i < _coreCount ? _cores[i] : null;
                var active = core != null && core.isActiveAndEnabled && _settings.coreBlocksLasers;
                var blocker = _coreBlockers[i];
                if (!active)
                {
                    if (blocker != null && blocker.gameObject.activeSelf)
                    {
                        blocker.gameObject.SetActive(false);
                    }

                    continue;
                }

                if (blocker == null)
                {
                    var go = new GameObject("[実験] 歯のビーム止め");
                    go.transform.SetParent(transform, false);
                    blocker = go.AddComponent<CircleCollider2D>();
                    _coreBlockers[i] = blocker;
                }

                var t = blocker.transform;
                var position = core.transform.position;
                if (t.position != position)
                {
                    t.position = position;
                }

                if (!Mathf.Approximately(blocker.radius, _coreRadii[i]))
                {
                    blocker.radius = _coreRadii[i];
                }

                if (!blocker.gameObject.activeSelf)
                {
                    blocker.gameObject.SetActive(true);
                }
            }
        }

        // 歯のモデルが見えていれば、その見た目の半径 × 倍率。無ければ設定の値。
        private float ResolveCoreRadius(CoreCrystalController core)
        {
            var tooth = core.GetComponentInChildren<CoreToothModel>();
            if (tooth != null && tooth.IsShowing)
            {
                var has = false;
                var bounds = new Bounds();
                foreach (var renderer in tooth.GetComponentsInChildren<Renderer>())
                {
                    if (renderer is SpriteRenderer || renderer is ParticleSystemRenderer || renderer is LineRenderer)
                    {
                        continue;
                    }

                    if (has)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                    else
                    {
                        bounds = renderer.bounds;
                        has = true;
                    }
                }

                if (has)
                {
                    return Mathf.Max(0.1f, Mathf.Max(bounds.extents.x, bounds.extents.y) * _settings.coreLaserBlockScale);
                }
            }

            return _settings.coreBlockRadius;
        }

        // 敵の計算が止まっている瞬間に、自分の砲台に重なっている敵を数える（次のフレームの移動で使う）。
        // 敵の計算が止まっている瞬間に、砲台に重なっている菌を調べる（次のフレームで使う）。
        // ・自分の砲台: 押される向き（どのPCでも、自分の画面の菌の位置から）
        // ・4人とも: 菌との重なり（汚れの溜まり方。Host・オフラインだけ。倒れている・守られている間は調べない＝菌は素通り）
        private void OnSwarmReadable()
        {
            _rawPush = Vector2.zero;
            if (_swarm == null)
            {
                return;
            }

            var authority = !_net.IsClient;
            for (var p = 0; p < PlayerCount; p++)
            {
                var turret = _turrets[p];
                var needContact = authority && turret != null && _settings.downEnabled && _down.States[p].Condition == TurretCondition.Normal;
                var needPush = p == _localPlayer && turret != null && _settings.pushedByEnemies && CanMove();
                if (!needContact && !needPush)
                {
                    _down.Contact[p] = 0f;
                    continue;
                }

                var overlap = _swarm.ComputeCrowdContact(turret.transform.position, _settings.bodyRadius, out var push);
                _down.Contact[p] = needContact ? overlap : 0f;
                if (needPush)
                {
                    _rawPush = push;
                }
            }
        }

        // ───────── 他の人の砲台（LAN） ─────────

        private void TickRemotes(float dt)
        {
            // 席替え・落下演出の間は、各 PC がその係で砲台を動かすので触らない。
            if (!_net.IsActive || FortressSeats.IsMoving || _held)
            {
                return;
            }

            var blend = TurretMoveMath.Blend(_settings.remoteSharpness, dt);
            for (var p = 0; p < PlayerCount; p++)
            {
                var turret = _turrets[p];
                if (p == _localPlayer || turret == null || !_net.TryGetTarget(p, out var target))
                {
                    continue;
                }

                var t = turret.transform;
                var current = (Vector2)t.position;
                var next = (target - current).sqrMagnitude > _settings.remoteSnapDistance * _settings.remoteSnapDistance
                    ? target
                    : Vector2.Lerp(current, target, blend);
                t.position = new Vector3(next.x, next.y, t.position.z);
            }
        }

        // ───────── カメラ ─────────

        public CameraViewSettings Apply(int viewer, CameraViewSettings view, float deltaTime)
        {
            if (_settings == null)
            {
                return view;
            }

            if (_held)
            {
                // 落下演出の間は、追従を足さずに元の視点のまま（砲台を画面の外から落としているので、追うと画面ごと動いてしまう）。
                // 追従の溜まりも捨てて、終わった後は元の視点から始める。
                ResetCameraFollow();
                return view;
            }

            var targetOffset = Vector2.zero;
            var targetLookAhead = Vector2.zero;
            var speed01 = 0f;
            var turret = ViewerIndex.IsPlayer(viewer) ? TurretOf(viewer) : null;
            if (turret != null && _settings.movementEnabled && !FortressSeats.IsMoving)
            {
                // 席替えの間は追わない（席替えの視点の移り変わりはリグが受け持つ）。着地すると砲台は席にいるので、ずれは 0 に戻る。
                var seatHome = HomeOfSeat(FortressSeats.CameraSeatOf(viewer), turret.transform.position);
                targetOffset = ((Vector2)turret.transform.position - seatHome) * _settings.cameraFollow;

                var velocity = viewer == _localPlayer ? _measuredVelocity : Vector2.zero;
                targetLookAhead = Vector2.ClampMagnitude(velocity * _settings.cameraLookAheadSeconds, _settings.cameraMaxLookAhead);
                speed01 = _settings.moveSpeed > 0f ? Mathf.Clamp01(velocity.magnitude / _settings.moveSpeed) : 0f;
            }

            if (deltaTime > 0f)
            {
                _cameraOffset = Vector2.SmoothDamp(_cameraOffset, targetOffset, ref _cameraOffsetSpeed, _settings.cameraSmoothTime, Mathf.Infinity, deltaTime);
                _lookAhead = Vector2.SmoothDamp(_lookAhead, targetLookAhead, ref _lookAheadSpeed, _settings.cameraLookAheadSmoothTime, Mathf.Infinity, deltaTime);
                _zoom = Mathf.SmoothDamp(_zoom, speed01 * _settings.cameraZoomOutWhileMoving, ref _zoomSpeed, _settings.cameraZoomSmoothTime, Mathf.Infinity, deltaTime);
            }

            view.center += _cameraOffset + _lookAhead;
            if (_zoom > 1e-4f)
            {
                view = view.WithVisibleHalfHeight(view.VisibleHalfHeight * (1f + _zoom));
            }

            return view;
        }

        // ───────── 席・砲台・試合 ─────────

        private Vector2 HomeOfPlayer(int player, Vector2 fallback)
        {
            return HomeOfSeat(FortressSeats.SeatOf(player), fallback);
        }

        // ステージが知らせた席の場所。無ければ（古い Game だけのシーン）試合の始めの砲台の場所（まだ席替えしていないので、席 s = プレイヤー s）。
        private Vector2 HomeOfSeat(int seat, Vector2 fallback)
        {
            if (FortressSeats.TryGetRegisteredSeats(out var seats, out _) && seat >= 0 && seat < seats.Length)
            {
                return seats[seat];
            }

            return seat >= 0 && seat < PlayerCount && _hasCapturedHome[seat] ? _capturedHomes[seat] : fallback;
        }

        // 群衆の計算が新しくなった（ゲームのシーンを読み込んだ）＝新しい試合。最初からやり直す。
        private void DetectMatch()
        {
            var swarm = SwarmSystem.Current;
            if (ReferenceEquals(swarm, _swarm))
            {
                return;
            }

            Subscribe(swarm);
            _down.Reset(_turrets);
            for (var p = 0; p < PlayerCount; p++)
            {
                _fades[p].Apply(_turrets[p], false, 0f, 1f, 1f);
                TurretMoveHooks.SetDowned(p, false);
                UpdateDownBlocker(p, false);
                _downedFx[p].Update(transform, null, false, 0f, 0f);
            }
            for (var p = 0; p < PlayerCount; p++)
            {
                _turrets[p] = null;
                _hasCapturedHome[p] = false;
            }

            _inputVelocity = Vector2.zero;
            _pushVelocity = Vector2.zero;
            _rawPush = Vector2.zero;
            _measuredVelocity = Vector2.zero;
            _region.Clear();
            ResetCameraFollow();
            _net.Clear();
            _refreshTimer = 0f;
        }

        private void ResetCameraFollow()
        {
            _cameraOffset = Vector2.zero;
            _cameraOffsetSpeed = Vector2.zero;
            _lookAhead = Vector2.zero;
            _lookAheadSpeed = Vector2.zero;
            _zoom = 0f;
            _zoomSpeed = 0f;
        }

        private void Subscribe(SwarmSystem swarm)
        {
            if (_swarm != null)
            {
                _swarm.StorageReadable -= OnSwarmReadable;
            }

            _swarm = swarm;
            if (_swarm != null)
            {
                _swarm.StorageReadable += OnSwarmReadable;
            }
        }

        private void RefreshTurrets(float dt)
        {
            // 砲台・コアを探すのは決まった間隔ごと（毎フレーム探すと重い）。新しい試合では DetectMatch がすぐ探させる。
            _refreshTimer -= dt;
            if (_refreshTimer > 0f)
            {
                return;
            }

            _refreshTimer = TurretRefreshInterval;
            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                var p = turret.playerIndex;
                if (p < 0 || p >= PlayerCount || (_turrets[p] != null && _turrets[p] != turret))
                {
                    continue;
                }

                _turrets[p] = turret;
                if (!_hasCapturedHome[p] && !FortressSeats.IsMoving && FortressSeats.Shift == 0)
                {
                    _capturedHomes[p] = turret.transform.position;
                    _hasCapturedHome[p] = true;
                }
            }

            // 落下演出の間は床も画面の外へ出ているので取らない（終わったらすぐ取り直す）。
            if (!_held)
            {
                RefreshFloor();
            }

            _coreCount = 0;
            foreach (var core in FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None))
            {
                if (_coreCount < _cores.Length)
                {
                    _coreRadii[_coreCount] = ResolveCoreRadius(core);
                    _cores[_coreCount++] = core;
                }
            }
        }

        private LaserTurret TurretOf(int player)
        {
            return player >= 0 && player < PlayerCount ? _turrets[player] : null;
        }
    }
}
