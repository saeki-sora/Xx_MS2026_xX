using System.Collections.Generic;
using MS2026.Fortress;
using MS2026.Fortress.Billboards;
using MS2026.Fortress.Net;
using MS2026.Skills;
using UnityEngine;

namespace MS2026.HowToPlay
{
    /// <summary>
    /// 練習のシーン（PracticeTutorial.unity）の係（2026-11-13）。キャラ選択の上に重ねて読み込まれる。
    /// 中身は本物: 砲台（LaserTurret＋キャラのポッド）・レーザー・敵（群衆）・スマッシュボール・スキル。背景や置物はステージと同じでなくてよい。
    /// ・通信中: Host が通信経由で読み込み、本番と同じ同期の部品（[Fortress] NetSync）で4人が同じ砲台・敵・泡を見る
    ///   （砲台の握力・敵・泡は本番と同じく Host が決めて配る。この係は握力に口を出さない）
    /// ・通信していない（エディタで試すとき）: 自分の砲台はこのPCのセンサー（番号0。キーボードなら Q）、ほかの3人は届いた握力（<see cref="PracticeBoard"/>）で動かす。
    ///   ほかの人がスキルの段階に来たら、その人のスキルを使える状態にする
    /// ・練習の敵: 1人ずつ別（<see cref="PracticeLocalEnemies"/>。このPCの中だけ・自分の歯磨き粉でしか倒せない）。
    ///   調整の「敵を1人ずつ別にする」を OFF にすると、前と同じく本物の群衆を決めた出現場所から出し続ける（湧かせるのは Host だけ）
    /// ・割れたスマッシュボールは少し後に直す（Host だけ）
    /// ・泡は1人に1つ（<see cref="PracticeBallOwner"/>。シーンを作るメニューが砲台の横に置く）。持ち主が決めた課題に来たら、その人の泡が全員の画面に出る。
    ///   持ち主以外のビームが当たっても減らない（Host が減った分をすぐ戻す）
    /// ・カメラは自分の砲台が上に来る向き（本番と同じ）。練習の画面がこのカメラを左上の枠に映す
    /// ・練習の係（<see cref="PracticeTrainer.World"/>）に、自分の砲台・スキル・割ったことを渡す
    /// 練習用の調整（<see cref="HowToPlaySettings.sceneTuning"/>: オーバーヒートまでの秒数・冷める速さ・泡を動かさない・研修3まで泡を出さない など）は、
    /// このシーンの砲台・泡にだけ当てる（砲台の設定は複製して書き換えるので、本番の設定ファイルは変わらない）。
    /// </summary>
    public sealed class PracticeSceneDriver : MonoBehaviour, IPracticeWorld, ITurretGripSource
    {
        private const int Players = 4;

        [Tooltip("フィールドを映すカメラ（このシーンの物）。")]
        [SerializeField] private Camera viewCamera;

        [Tooltip("練習の敵を出す係。")]
        [SerializeField] private EnemySpawnDirector director;

        [Tooltip("練習の敵の出し方（出現場所・種類・数・間隔）。練習の間ずっと出す。")]
        [SerializeField] private List<EnemySpawnEntry> practiceEntries = new List<EnemySpawnEntry>();

        [Tooltip("カメラとフィールドの距離（メートル）。")]
        [SerializeField] private float cameraDistance = 16f;

        [Tooltip("カメラの縦の画角（度）。")]
        [SerializeField] private float cameraFieldOfView = 55f;

        [Tooltip("カメラの中心を、コアから自分の砲台の方へどれだけ寄せるか（0 = コア、1 = 砲台）。")]
        [Range(0f, 1f)]
        [SerializeField] private float centerTowardTurret = 0.15f;


        private readonly LaserTurret[] _turrets = new LaserTurret[Players];
        private readonly bool[] _granted = new bool[Players];
        // 誰もいない席の砲台（2026-11-23）: 砲台そのもの（キャラの絵が無く、黒い丸のように見える）は隠し、
        // 代わりに、誰も選んでいないキャラの砲台の絵を「ダミー」として出す（砲台は本物と同じく回る。撃たない）。
        // 0 = まだ決めていない、1 = 出す（人がいる）、-1 = 隠してダミーを出す。
        private readonly int[] _turretShown = new int[Players];
        private readonly SpriteRenderer[] _dummies = new SpriteRenderer[Players];
        private int _dummyKey = -1;
        private TurretPortraitTable _portraits;
        private readonly List<(DestructibleObstacle obstacle, float at)> _repairs = new List<(DestructibleObstacle, float)>();
        private PracticeBoard _board;
        private int _local;
        private bool _bound;
        private bool _brokeBall;
        private int _killBase;
        private float _time;
        private float _nextScan;
        private CoreCrystalController _core;
        private PracticeSceneTuning _tuning;
        private readonly Dictionary<LaserTuningConfig, LaserTuningConfig> _tuned = new Dictionary<LaserTuningConfig, LaserTuningConfig>();
        // 研修1の間の写し（熱がたまらない）。元の設定 → 写し。
        private readonly Dictionary<LaserTuningConfig, LaserTuningConfig> _noHeat = new Dictionary<LaserTuningConfig, LaserTuningConfig>();
        // 砲台ごとの元の設定（本番の設定ファイル。写しに替えたあとも覚えておく）。
        private readonly LaserTuningConfig[] _originalTuning = new LaserTuningConfig[Players];
        private bool _noOverheatInLesson1 = true;
        private readonly List<Ball> _balls = new List<Ball>();
        private PracticeLocalEnemies _localEnemies;

        private sealed class Ball
        {
            public SmashBallModule Module;
            public DestructibleObstacle Obstacle;
            public int Owner = -1;
            public Vector3 Scale;
            public bool Shown = true;
        }

        /// <summary>読み込まれている練習のシーンの係（無ければ null）。</summary>
        public static PracticeSceneDriver Current { get; private set; }

        public Camera ViewCamera => viewCamera;

        /// <summary>自分の砲台（まだ見つかっていなければ null）。研修2の熱のゲージの場所に使う。</summary>
        public LaserTurret LocalTurret => _bound ? _turrets[_local] : null;

        /// <summary>泡の数（持ち主の付いていない古いシーンの泡も含む）。</summary>
        public int BallCount => _balls.Count;

        /// <summary>
        /// i 番目の泡の位置と持ち主（持ち主が無ければ -1）。隠している・割れている泡は false。
        /// </summary>
        public bool TryGetBall(int i, out Vector3 position, out int owner)
        {
            position = default;
            owner = -1;
            if (i < 0 || i >= _balls.Count)
            {
                return false;
            }

            var b = _balls[i];
            if (b.Module == null || !b.Shown || !b.Module.isActiveAndEnabled || (b.Obstacle != null && b.Obstacle.IsDestroyed))
            {
                return false;
            }

            position = b.Module.transform.position;
            owner = b.Owner;
            return true;
        }

        /// <summary>練習の様子（全員の握力）と自分の席を渡す。これで動き出す。</summary>
        public void Bind(PracticeBoard board, int localSeat)
        {
            _board = board;
            _local = PracticeBoard.IsValid(localSeat) ? localSeat : 0;
            _bound = true;
            _brokeBall = false;
            _killBase = EnemyKillCounter.ByPlayer(_local);
            PracticeTrainer.World = this;
        }

        /// <summary>通信中か（通信中は本物の同期に任せる）。</summary>
        private static bool Online => FortressNet.IsNetworked;

        private void Awake()
        {
            Current = this;
            var settings = HowToPlaySettings.Load();
            _tuning = settings.sceneTuning ?? new PracticeSceneTuning();
            _noOverheatInLesson1 = settings.rules == null || settings.rules.noOverheatInLesson1;
        }

        private int _skillUses;

        private void OnEnable()
        {
            SmashBallModule.AnyBroken += OnBallBroken;
            PlayerSkillSlot.AnyStateChanged += OnSkillStateChanged;
        }

        private void OnDisable()
        {
            SmashBallModule.AnyBroken -= OnBallBroken;
            PlayerSkillSlot.AnyStateChanged -= OnSkillStateChanged;
            foreach (var b in _balls)
            {
                if (b.Obstacle != null)
                {
                    b.Obstacle.Damaged -= OnBallDamaged;
                }
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            if (ReferenceEquals(PracticeTrainer.World, this))
            {
                PracticeTrainer.World = null;
            }

            foreach (var turret in _turrets)
            {
                if (turret != null && ReferenceEquals(turret.GripSourceOverride, this))
                {
                    turret.GripSourceOverride = null;
                }
            }

            foreach (var copy in _noHeat.Values)
            {
                if (copy != null)
                {
                    Destroy(copy);
                }
            }

            foreach (var copy in _tuned.Values)
            {
                if (copy != null)
                {
                    Destroy(copy);
                }
            }
        }

        private void Start()
        {
            if (director != null)
            {
                director.autoStartOnPlay = false;
                if (_tuning.enemiesEnabled && _tuning.separateEnemies)
                {
                    // 敵は1人ずつ別: 本物の群衆は出さず、このPCの中だけで自分の敵を出す。
                    EnemyTypeDefinition type = null;
                    foreach (var entry in practiceEntries)
                    {
                        if (entry != null && entry.enemyType != null)
                        {
                            type = entry.enemyType;
                            break;
                        }
                    }

                    ScanTurrets();
                    _localEnemies = gameObject.AddComponent<PracticeLocalEnemies>();
                    _localEnemies.Setup(type, _tuning, _core != null ? _core.transform.position : Vector3.zero);
                }
                else if (_tuning.enemiesEnabled && practiceEntries.Count > 0)
                {
                    var entries = practiceEntries;
                    if (_tuning.enemySpawnIntervalSeconds > 0f)
                    {
                        entries = new List<EnemySpawnEntry>();
                        for (var i = 0; i < practiceEntries.Count; i++)
                        {
                            var copy = practiceEntries[i].Clone();
                            copy.intervalBetweenSpawns = _tuning.enemySpawnIntervalSeconds;
                            copy.triggerTime = 1f + (i * _tuning.enemySpawnIntervalSeconds / Mathf.Max(1, practiceEntries.Count));
                            entries.Add(copy);
                        }
                    }

                    director.PlayEntries(entries, false, 0f);
                }
            }

            // 泡（スマッシュボール）: 動かさない・持ち主が研修3に来るまで出さない・持ち主しか割れない。
            foreach (var module in FindObjectsByType<SmashBallModule>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (module == null || module.gameObject.scene != gameObject.scene)
                {
                    continue;
                }

                var owner = module.GetComponent<PracticeBallOwner>();
                var ball = new Ball
                {
                    Module = module,
                    Obstacle = module.GetComponent<DestructibleObstacle>(),
                    Owner = owner != null ? owner.playerIndex : -1,
                    Scale = module.transform.localScale
                };
                _balls.Add(ball);
                if (ball.Obstacle != null)
                {
                    ball.Obstacle.Damaged += OnBallDamaged;
                }

                var floater = module.GetComponent<SmashBallFloater>();
                if (floater != null && !_tuning.smashBallsMove)
                {
                    floater.enabled = false;
                }
            }

            UpdateBallVisibility();
        }

        /// <summary>持ち主のいない泡（古いシーン）は、だれか1人が決めた課題に来たら出す。</summary>
        private bool AnyoneReached(PracticeStep step)
        {
            if (_board == null)
            {
                return step <= PracticeStep.Grip;
            }

            for (var p = 0; p < Players; p++)
            {
                if (Reached(p, step))
                {
                    return true;
                }
            }

            return false;
        }

        private bool Reached(int seat, PracticeStep step)
        {
            if (step <= PracticeStep.Grip)
            {
                return _board == null || !PracticeBoard.IsValid(seat) || _board.seats[seat].present;
            }

            if (_board == null || !PracticeBoard.IsValid(seat))
            {
                return false;
            }

            var s = _board.seats[seat];
            return s.present && s.step >= step;
        }

        /// <summary>
        /// 泡を出す・隠す（持ち主ごと）。隠す間は小さくして当たらなくする（同期の部品の一覧から外れないよう、オブジェクトは消さない）。
        /// 割れない設定（無敵）は Host が決めて配る。全員のPCが同じ様子（届いた課題）から決めるので、どのPCでも同じ泡が出る。
        /// </summary>
        private void UpdateBallVisibility()
        {
            var from = _tuning.showSmashBallsFrom;
            foreach (var b in _balls)
            {
                if (b.Module == null)
                {
                    continue;
                }

                var show = b.Owner >= 0 ? Reached(b.Owner, from) : AnyoneReached(from);
                if (b.Shown == show)
                {
                    continue;
                }

                b.Shown = show;
                b.Module.transform.localScale = show ? b.Scale : Vector3.zero;
                if (b.Obstacle != null && FortressNet.HasSimulationAuthority)
                {
                    b.Obstacle.SetInvulnerable(!show);
                }
            }
        }

        /// <summary>持ち主以外のビームで減ったら、すぐ戻す（Host だけ。戻した耐久は同期の部品が全員に配る）。</summary>
        private void OnBallDamaged(DestructibleObstacle obstacle, float amount, DamageSource source)
        {
            if (obstacle == null || obstacle.IsReplica || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            var owner = -1;
            foreach (var b in _balls)
            {
                if (b.Obstacle == obstacle)
                {
                    owner = b.Owner;
                    break;
                }
            }

            if (owner < 0 || obstacle.LastAttacker.PlayerIndex == owner)
            {
                return;
            }

            // ここで耐久を戻すと、この後の「0 になったら壊れる」も起きない。
            obstacle.ApplyReplicatedHealth(Mathf.Min(obstacle.MaxHealth, obstacle.CurrentHealth + amount));
        }

        /// <summary>練習用に、砲台の設定を複製して熱・冷め方・回る速さを変える（本番の設定ファイルは変えない）。</summary>
        private void ApplyTuning(int seat, LaserTurret turret)
        {
            var current = turret.tuning;
            if (current == null)
            {
                return;
            }

            // 元の設定を覚える（すでにこちらの写しなら、覚えている元のまま）。
            if (!_tuned.ContainsValue(current) && !_noHeat.ContainsValue(current))
            {
                _originalTuning[seat] = current;
            }

            var original = _originalTuning[seat];
            if (original == null)
            {
                return;
            }

            var practice = PracticeCopy(original);

            // 研修1の間は熱がたまらない写し（オーバーヒートしない）。熱は結果を決めるPC（Host・オフライン）が計算するので、
            // 全員の様子（届いた課題）から、その人が研修1にいるかで決める。どのPCでも同じように替える。
            var target = InLesson1(seat) ? NoHeatCopy(original, practice) : practice;
            if (turret.tuning != target)
            {
                turret.tuning = target;
            }
        }

        private bool SeatPresent(int seat) => seat == _local || (_board != null && _board.seats[seat].present);

        /// <summary>
        /// 誰もいない席の砲台に、ダミーのキャラの絵を出す（人がいる席・砲台が見つかった席が変わったときだけ決め直す）。
        /// 絵は砲台のキャラの絵の一覧（スキルを配る係の turretPortraits）から、誰も選んでいないキャラを上から順に使う。
        /// 大きさ・置き方は本物の砲台と同じ（一覧の高さ・ずれ・向き。砲台と一緒に回る）。
        /// </summary>
        private void UpdateDummies()
        {
            var key = 0;
            for (var p = 0; p < Players; p++)
            {
                key |= (SeatPresent(p) ? 1 : 0) << p;
                key |= (_turrets[p] != null ? 1 : 0) << (p + 4);
            }

            if (key == _dummyKey)
            {
                return;
            }

            _dummyKey = key;
            if (_portraits == null)
            {
                foreach (var installer in FindObjectsByType<PlayerSkillInstaller>(FindObjectsSortMode.None))
                {
                    if (installer != null && installer.turretPortraits != null)
                    {
                        _portraits = installer.turretPortraits;
                        break;
                    }
                }
            }

            // ダミーのキャラは、画面右下のアイコンと同じ決まりで選ぶ（PracticeArtLibrary.DummyCharacterIds）。
            var ids = PracticeArtLibrary.DummyCharacterIds(SeatPresent);
            for (var p = 0; p < Players; p++)
            {
                var turret = _turrets[p];
                if (turret == null)
                {
                    continue;
                }

                SkillTurretLook look = null;
                if (!string.IsNullOrEmpty(ids[p]) && _portraits != null && _portraits.entries != null)
                {
                    foreach (var entry in _portraits.entries)
                    {
                        if (entry != null && entry.normalSprite != null && entry.characterId == ids[p])
                        {
                            look = entry;
                            break;
                        }
                    }
                }

                ShowDummy(p, turret, look);
            }
        }

        private void ShowDummy(int seat, LaserTurret turret, SkillTurretLook look)
        {
            var dummy = _dummies[seat];
            if (look == null)
            {
                if (dummy != null)
                {
                    dummy.enabled = false;
                }

                return;
            }

            if (dummy == null)
            {
                var go = new GameObject("[Practice] Dummy Turret");
                go.transform.SetParent(turret.transform, false);
                dummy = go.AddComponent<SpriteRenderer>();
                var own = turret.GetComponent<SpriteRenderer>();
                if (own != null)
                {
                    dummy.sharedMaterial = own.sharedMaterial;
                }

                dummy.sortingOrder = 10;
                _dummies[seat] = dummy;
            }

            // 本物の砲台の絵と同じ: 高さ look.height m、砲台からのずれ look.offset、砲台と一緒に回る。
            var sprite = look.normalSprite;
            dummy.sprite = sprite;
            dummy.color = look.tint;
            dummy.enabled = true;
            var parent = turret.transform.lossyScale;
            var sx = Mathf.Max(0.0001f, Mathf.Abs(parent.x));
            var sy = Mathf.Max(0.0001f, Mathf.Abs(parent.y));
            var spriteHeight = Mathf.Max(0.0001f, sprite.bounds.size.y);
            var k = look.height / spriteHeight;
            var t = dummy.transform;
            t.localPosition = new Vector3(look.offset.x / sx, look.offset.y / sy, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, look.rotationOffset);
            t.localScale = new Vector3(k / sx, k / sy, 1f);
        }

        /// <summary>砲台の見た目（絵・レーザーの線・粒）を出す・隠す。誰もいない席の砲台を隠すのに使う（動きはそのまま）。</summary>
        private void ShowTurret(int seat, LaserTurret turret, bool show)
        {
            var want = show ? 1 : -1;
            if (_turretShown[seat] == want)
            {
                return;
            }

            _turretShown[seat] = want;
            foreach (var r in turret.GetComponentsInChildren<Renderer>(true))
            {
                if (r != _dummies[seat])
                {
                    r.enabled = show;
                }
            }
        }

        private bool InLesson1(int seat)
        {
            if (!_noOverheatInLesson1 || _board == null || !PracticeBoard.IsValid(seat))
            {
                return false;
            }

            var s = _board.seats[seat];
            return s.present && PracticeSteps.Lesson(s.step) == 0;
        }

        private LaserTuningConfig NoHeatCopy(LaserTuningConfig original, LaserTuningConfig practice)
        {
            if (!_noHeat.TryGetValue(original, out var copy))
            {
                copy = Instantiate(practice);
                copy.name = original.name + " (練習・研修1)";
                copy.heatGainPerSecond = 0f;
                _noHeat[original] = copy;
            }

            return copy;
        }

        /// <summary>練習用の写し（熱・冷め方・回る速さを変えた物）。</summary>
        private LaserTuningConfig PracticeCopy(LaserTuningConfig original)
        {
            if (!_tuned.TryGetValue(original, out var copy))
            {
                copy = Instantiate(original);
                copy.name = original.name + " (練習)";
                var threshold = Mathf.Max(0.01f, copy.overheatThreshold);
                if (_tuning.secondsToOverheat > 0f)
                {
                    copy.heatGainPerSecond = threshold / _tuning.secondsToOverheat;
                }

                if (_tuning.secondsToCoolDown > 0f)
                {
                    copy.heatCoolingPerSecond = threshold / _tuning.secondsToCoolDown;
                }

                if (_tuning.overheatSilenceSeconds > 0f)
                {
                    copy.overheatSilenceDuration = _tuning.overheatSilenceSeconds;
                }

                copy.rotationSpeedDegPerSec *= _tuning.turnSpeedMultiplier;
                _tuned[original] = copy;
            }

            return copy;
        }

        private void Update()
        {
            _time += Time.deltaTime;
            ScanTurrets();
            if (!_bound)
            {
                return;
            }

            for (var p = 0; p < Players; p++)
            {
                var turret = _turrets[p];
                if (turret == null)
                {
                    continue;
                }

                ApplyTuning(p, turret);
                ShowTurret(p, turret, SeatPresent(p));
                if (p == _local && _localEnemies != null)
                {
                    _localEnemies.Tick(turret, viewCamera, Time.deltaTime);
                }

                if (Online)
                {
                    // 通信中は、握力も敵もスキルも本物の同期に任せる。
                    continue;
                }

                turret.GripSourceOverride = this;

                // ほかの人がスキルの段階に来たら、その人のスキルを使える状態にする（そのあと握ると本物のスキルが出る）。
                if (p != _local && _board != null)
                {
                    var seat = _board.seats[p];
                    var slot = PlayerSkillSlot.Find(p);
                    if (seat.present && seat.step == PracticeStep.Skill && !_granted[p] && slot != null && slot.State == PlayerSkillState.Empty)
                    {
                        slot.GrantCharge();
                        _granted[p] = true;
                    }
                }
            }

            // 誰もいない席の砲台にダミーのキャラの絵を出す。
            UpdateDummies();

            // 泡は、持ち主が決めた課題に来たら出す。
            UpdateBallVisibility();

            if (!FortressNet.HasSimulationAuthority)
            {
                _repairs.Clear();
                return;
            }

            for (var i = _repairs.Count - 1; i >= 0; i--)
            {
                var (obstacle, at) = _repairs[i];
                if (_time < at)
                {
                    continue;
                }

                if (obstacle != null && obstacle.IsDestroyed)
                {
                    obstacle.Regenerate();
                }

                _repairs.RemoveAt(i);
            }
        }

        private void LateUpdate()
        {
            PlaceCamera();
        }

        // ───────── 砲台 ─────────

        private void ScanTurrets()
        {
            if (Time.unscaledTime < _nextScan)
            {
                return;
            }

            _nextScan = Time.unscaledTime + 0.5f;
            foreach (var t in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                if (t != null && t.gameObject.scene == gameObject.scene && t.playerIndex >= 0 && t.playerIndex < Players)
                {
                    _turrets[t.playerIndex] = t;
                }
            }

            if (_core == null)
            {
                foreach (var c in FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None))
                {
                    if (c != null && c.gameObject.scene == gameObject.scene)
                    {
                        _core = c;
                        break;
                    }
                }
            }
        }

        /// <summary>砲台の握力: 自分の席はこのPCのセンサー（番号0）、ほかの人は届いた値。</summary>
        public void GetGrip(int playerIndex, out bool isGripping, out float grip01)
        {
            isGripping = false;
            grip01 = 0f;
            if (!_bound || Online)
            {
                return;
            }

            if (playerIndex == _local)
            {
                var provider = MS2026.GripInputBridge.GripInputBridge.Provider;
                if (provider != null)
                {
                    isGripping = provider.IsGripping(0);
                    grip01 = provider.GetGripValue(0);
                }

                return;
            }

            if (_board != null && PracticeBoard.IsValid(playerIndex))
            {
                var seat = _board.seats[playerIndex];
                isGripping = seat.present && seat.gripping;
                grip01 = isGripping ? seat.Grip01 : 0f;
            }
        }

        private void OnBallBroken(SmashBallBreakInfo info)
        {
            if (info.Obstacle == null || info.Obstacle.gameObject.scene != gameObject.scene)
            {
                return;
            }

            if (_bound && info.Attacker.PlayerIndex == _local)
            {
                _brokeBall = true;
            }

            if (FortressNet.HasSimulationAuthority)
            {
                _repairs.Add((info.Obstacle, _time + _tuning.smashBallRepairSeconds));
            }
        }

        // ───────── カメラ ─────────

        private void PlaceCamera()
        {
            if (viewCamera == null)
            {
                return;
            }

            var core = _core != null ? _core.transform.position : Vector3.zero;
            var turret = _bound ? _turrets[_local] : null;
            var dir = Vector2.up;
            var reach = 0f;
            if (turret != null)
            {
                var d = (Vector2)(turret.transform.position - core);
                if (d.sqrMagnitude > 0.01f)
                {
                    dir = d.normalized;
                    reach = d.magnitude;
                }
            }

            var center = (Vector2)core + (dir * reach * centerTowardTurret);
            viewCamera.orthographic = false;
            viewCamera.fieldOfView = cameraFieldOfView;
            viewCamera.transform.SetPositionAndRotation(
                new Vector3(center.x, center.y, core.z - cameraDistance),
                Quaternion.LookRotation(Vector3.forward, new Vector3(dir.x, dir.y, 0f)));
            EnemyScreenAlign.Apply(true, viewCamera);
        }

        // ───────── 練習の係へ（自分の様子） ─────────

        public bool IsReady => _bound && _turrets[_local] != null;

        public TurretState LocalTurretState => _turrets[_local] != null ? _turrets[_local].State : TurretState.Idle;

        public float LocalHeat01 => _turrets[_local] != null ? _turrets[_local].HeatRatio01 : 0f;

        public bool LocalHasSkill
        {
            get
            {
                var slot = PlayerSkillSlot.Find(_local);
                return slot != null && slot.State != PlayerSkillState.NoSkill;
            }
        }

        public bool LocalSkillReady
        {
            get
            {
                var slot = PlayerSkillSlot.Find(_local);
                return slot != null && slot.IsReady;
            }
        }

        public bool LocalSkillActive
        {
            get
            {
                var slot = PlayerSkillSlot.Find(_local);
                return slot != null && slot.IsActive;
            }
        }

        public bool LocalBrokeBall => _brokeBall;

        public int LocalSkillUses => _skillUses;

        /// <summary>自分のスキルが「発動中」になった瞬間を数える（黄の矢のように、同じフレームで終わるスキルも取りこぼさない）。</summary>
        private void OnSkillStateChanged(PlayerSkillSlot slot)
        {
            if (_bound && slot != null && slot.PlayerIndex == _local && slot.State == PlayerSkillState.Active && slot.gameObject.scene == gameObject.scene)
            {
                _skillUses++;
            }
        }

        /// <summary>練習が始まってから自分が倒した数（撃破数は Host が数えて全員に配る。練習のシーンを読み込んだときに 0 から数え直される）。</summary>
        public int LocalKills
        {
            get
            {
                if (_localEnemies != null)
                {
                    return _localEnemies.Kills;
                }

                var kills = EnemyKillCounter.ByPlayer(_local);
                if (kills < _killBase)
                {
                    // 数え直された（新しい群衆になった）。ここから数える。
                    _killBase = 0;
                }

                return Mathf.Max(0, kills - _killBase);
            }
        }
    }
}
