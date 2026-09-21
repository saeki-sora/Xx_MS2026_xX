using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 数万体の敵を「配列 + Jobs/Burst + GPUインスタンシング」で動かす群衆システム。
    /// 敵の移動（経路のフロー・フィールド追従）・押し合い・レーザー被弾・コア到達・描画までを処理する。
    ///
    /// シミュレーションは非同期: Updateの最後にジョブをスケジュールし、次のUpdateの頭で完了を回収する。
    /// ジョブが動いている間、メインスレッドは他の処理（描画の準備など）を進められる。
    /// そのため、敵の追加・削除・レーザー登録はキューに積み、ジョブの合間にまとめて反映する。
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class SwarmSystem : MonoBehaviour
    {
        public const int HistoryLength = 180;

        public static readonly string[] StageNames =
        {
            "空間ハッシュ構築", "レーザー判定", "移動計算", "押し合い", "位置確定", "削除の詰め直し", "描画データ作成"
        };

        public static SwarmSystem Current { get; private set; }

        [Tooltip("群衆の設定アセット。未設定なら既定値で動く。")]
        public SwarmSettings settings;

        [Tooltip("画面左上に、敵の数・FPS・処理時間を表示する。")]
        public bool showHud = true;

        [Tooltip("ONにすると、各処理段階（ハッシュ構築・移動・押し合いなど）の時間と、密集度を個別に計測する。" +
                 "非同期をやめて段階ごとに完了を待つため、通常より遅くなる。原因調査用。")]
        public bool detailedProfiling;

        [Tooltip("フレーム時間がこの値(ms)を超えたら「スパイク」として内訳を記録する。")]
        [Min(1f)]
        public float spikeThresholdMs = 18f;

        /// <summary>Sceneビューの密度表示用に、各セルの敵数を毎フレームコピーして保持するか（エディタのツールが設定する）。</summary>
        [HideInInspector]
        public bool captureCellCounts;

        public SwarmAgentStorage Storage { get; private set; }
        public SwarmSpatialGrid Grid { get; private set; }
        public SwarmNavigationData Navigation { get; private set; }
        public SwarmStats Stats => _stats;
        public SwarmSettings ActiveSettings => _settings;
        public IReadOnlyList<EnemyTypeDefinition> RegisteredTypes => _types;
        public float[] FrameMsHistory { get; } = new float[HistoryLength];
        public float[] SimulationMsHistory { get; } = new float[HistoryLength];
        public int HistoryCursor { get; private set; }
        public float[] StageMs { get; } = new float[StageNames.Length];
        public IReadOnlyList<SwarmSpike> RecentSpikes => _spikes;

        // 密度表示用のスナップショット（captureCellCounts時のみ更新）
        public int[] CellCounts { get; private set; } = new int[1];
        public Vector2 CellGridOrigin { get; private set; }
        public float CellGridSize { get; private set; } = 1f;
        public int CellGridWidth { get; private set; } = 1;
        public int CellGridHeight { get; private set; } = 1;

        private struct PendingSpawn
        {
            public float2 position;
            public int type;
            public float hp;
            public float speed;
            public float animStart;
            public float2 face;
        }

        private SwarmSettings _settings;
        private SwarmStats _stats;

        private readonly List<EnemyTypeDefinition> _types = new List<EnemyTypeDefinition>();
        private readonly List<int> _typeProfileIndex = new List<int>();
        private readonly List<NavigationProfile> _profiles = new List<NavigationProfile>();
        private readonly List<CoreCrystalController> _cores = new List<CoreCrystalController>();
        private readonly List<PendingSpawn> _pending = new List<PendingSpawn>();
        private readonly SwarmBeam[] _beamQueue = new SwarmBeam[SwarmLimits.MaxBeams];
        private readonly List<SwarmSpike> _spikes = new List<SwarmSpike>();

        private readonly Stopwatch _phaseWatch = new Stopwatch();
        private readonly Stopwatch _stageWatch = new Stopwatch();
        private readonly Stopwatch _jobWatch = new Stopwatch();

        private NativeArray<SwarmTypeParams> _typeParams;
        private NativeArray<float2> _goals;
        private NativeArray<SwarmBeam> _beamsJob;
        private NativeArray<int> _counters;
        private NativeArray<float> _goalDamage;
        private NativeArray<int> _attached;
        private NativeArray<int> _killsByType;
        private NativeArray<SwarmInstance> _instances;
        private NativeArray<SwarmInstance> _tmpInstances;
        private NativeArray<int> _typeStart;
        private NativeArray<int> _keyStart;
        private NativeArray<int> _keyCursor;
        private NativeArray<int> _keyOf;
        private NativeArray<int> _keepList;
        private NativeArray<float> _yRange;
        private NativeArray<float> _hitT;
        private NativeArray<int> _hitIdx;

        private SwarmRenderer _renderer;
        private NavigationField _navField;
        private JobHandle _handle;
        private bool _inFlight;
        private bool _initialized;
        private bool _clearRequested;
        private bool _navigationDirty = true;
        private float _flightDt;
        private int _renderTotal;
        private int _beamCount;
        private int _goalCount;
        private float _refreshTimer;
        private double _stageLast;

        private float _lastNavMs;
        private float _lastSimMs;
        private float _lastRenderMs;
        private int _lastAlive;
        private int _lastMaxCell;
        private bool _lastNavRebuilt;
        private int _lastGcCount;

        private void Awake()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            Current = this;

            _settings = settings != null ? settings : CreateRuntimeSettings();
            var capacity = _settings.maxEnemies;

            Storage = new SwarmAgentStorage(capacity);
            Grid = new SwarmSpatialGrid(capacity);
            Navigation = new SwarmNavigationData();

            _typeParams = new NativeArray<SwarmTypeParams>(SwarmLimits.MaxTypes, Allocator.Persistent);
            _goals = new NativeArray<float2>(SwarmLimits.MaxGoals, Allocator.Persistent);
            _beamsJob = new NativeArray<SwarmBeam>(SwarmLimits.MaxBeams, Allocator.Persistent);
            _counters = new NativeArray<int>(4, Allocator.Persistent);
            _goalDamage = new NativeArray<float>(SwarmLimits.MaxGoals, Allocator.Persistent);
            _attached = new NativeArray<int>(SwarmLimits.MaxGoals, Allocator.Persistent);
            _killsByType = new NativeArray<int>(SwarmLimits.MaxTypes, Allocator.Persistent);
            _instances = new NativeArray<SwarmInstance>(capacity, Allocator.Persistent);
            _tmpInstances = new NativeArray<SwarmInstance>(capacity, Allocator.Persistent);
            _typeStart = new NativeArray<int>(SwarmLimits.MaxTypes + 1, Allocator.Persistent);
            _keyStart = new NativeArray<int>(SwarmLimits.MaxTypes * SwarmLimits.SortBuckets + 1, Allocator.Persistent);
            _keyCursor = new NativeArray<int>(SwarmLimits.MaxTypes * SwarmLimits.SortBuckets, Allocator.Persistent);
            _keyOf = new NativeArray<int>(capacity, Allocator.Persistent);
            _keepList = new NativeArray<int>(capacity, Allocator.Persistent);
            _yRange = new NativeArray<float>(2, Allocator.Persistent);
            _hitT = new NativeArray<float>(SwarmLimits.MaxPierceBuffer, Allocator.Persistent);
            _hitIdx = new NativeArray<int>(SwarmLimits.MaxPierceBuffer, Allocator.Persistent);

            _profiles.Add(null);
            _renderer = new SwarmRenderer(capacity);
            _stats.capacity = capacity;
        }

        private static SwarmSettings CreateRuntimeSettings()
        {
            var runtime = ScriptableObject.CreateInstance<SwarmSettings>();
            runtime.hideFlags = HideFlags.HideAndDontSave;
            return runtime;
        }

        // ---------------------------------------------------------------
        // 公開API（敵の追加・削除・レーザーはキューに積み、ジョブの合間に反映される）
        // ---------------------------------------------------------------

        /// <summary>敵を1体湧かせる。上限に達していれば false。</summary>
        public bool Spawn(EnemyTypeDefinition definition, Vector2 position)
        {
            Initialize();

            var type = RegisterType(definition);
            if (type < 0 || Storage.Count + _pending.Count >= Storage.Capacity)
            {
                _stats.rejectedSpawns++;
                return false;
            }

            var jitter = UnityEngine.Random.insideUnitCircle * _settings.spawnJitter;
            var angle = UnityEngine.Random.value * Mathf.PI * 2f;

            _pending.Add(new PendingSpawn
            {
                position = position + jitter,
                type = type,
                hp = definition.maxHealth,
                speed = 1f + UnityEngine.Random.Range(-1f, 1f) * definition.swarm.speedVariance,
                animStart = UnityEngine.Random.value * Mathf.Max(1, definition.swarm.frameCount),
                face = new float2(Mathf.Cos(angle), Mathf.Sin(angle))
            });

            _stats.totalSpawned++;
            return true;
        }

        /// <summary>中心の周りに円状にばらけて count 体を一斉に湧かせる。ストレステスト用。</summary>
        public int SpawnBurst(EnemyTypeDefinition definition, Vector2 center, float radius, int count)
        {
            var spawned = 0;
            for (var i = 0; i < count; i++)
            {
                if (!Spawn(definition, center + UnityEngine.Random.insideUnitCircle * radius))
                {
                    break;
                }

                spawned++;
            }

            return spawned;
        }

        public void ClearAll()
        {
            _pending.Clear();
            _clearRequested = true;
        }

        public void ClearSpikes()
        {
            _spikes.Clear();
        }

        /// <summary>そのフレームのレーザーを登録する。LaserBeamVisualが毎フレーム呼ぶ。maxHits=0で貫通数無制限。</summary>
        public void QueueBeam(Vector2 origin, Vector2 end, float halfWidth, float damagePerSecond, int maxHits)
        {
            if (_beamCount >= SwarmLimits.MaxBeams)
            {
                return;
            }

            _beamQueue[_beamCount++] = new SwarmBeam
            {
                origin = origin,
                end = end,
                halfWidth = halfWidth,
                damagePerSecond = damagePerSecond,
                maxHits = maxHits
            };
        }

        public int RegisterType(EnemyTypeDefinition definition)
        {
            var index = _types.IndexOf(definition);
            if (index >= 0)
            {
                return index;
            }

            if (_types.Count >= SwarmLimits.MaxTypes)
            {
                UnityEngine.Debug.LogWarning($"[Swarm] 敵の種類が上限({SwarmLimits.MaxTypes})を超えました: {definition.name}", this);
                return -1;
            }

            _types.Add(definition);
            _typeProfileIndex.Add(0);
            return _types.Count - 1;
        }

        // ---------------------------------------------------------------
        // 毎フレームの流れ
        //   1. 前フレームのジョブを回収  2. その結果を描画  3. 追加・削除を反映  4. 次のジョブをスケジュール
        // ---------------------------------------------------------------

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            var frameDelta = Time.unscaledDeltaTime;
            _stats.frameMs = Mathf.Lerp(_stats.frameMs, frameDelta * 1000f, 0.1f);
            DetectSpike(frameDelta * 1000f);

            _phaseWatch.Restart();
            CompleteSimulation();
            _phaseWatch.Stop();
            _lastSimMs = (float)_phaseWatch.Elapsed.TotalMilliseconds;
            _stats.simulationMs = Mathf.Lerp(_stats.simulationMs, _lastSimMs, 0.2f);

            _phaseWatch.Restart();
            _renderer.Render(_instances, _renderTotal, _typeStart, _types, _settings);
            _phaseWatch.Stop();
            _lastRenderMs = (float)_phaseWatch.Elapsed.TotalMilliseconds;
            _stats.renderMs = Mathf.Lerp(_stats.renderMs, _lastRenderMs, 0.2f);
            _stats.drawBatches = _renderer.LastBatchCount;

            ApplyPendingCommands();

            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 2f;
                RefreshCores();
            }

            UpdateGoalPositions();
            RefreshTypeParams();

            var versionBefore = Navigation.BuiltVersion;
            _phaseWatch.Restart();
            RefreshNavigation();
            _phaseWatch.Stop();
            _lastNavMs = (float)_phaseWatch.Elapsed.TotalMilliseconds;
            _lastNavRebuilt = Navigation.BuiltVersion != versionBefore;
            _stats.navigationMs = Mathf.Lerp(_stats.navigationMs, _lastNavMs, 0.2f);

            ScheduleSimulation();

            _beamCount = 0;
            _stats.alive = Storage.Count;
            _lastAlive = _stats.alive;
            RecordHistory();
        }

        /// <summary>追加・削除のキューを反映する。ジョブが動いていない、この間だけ配列を触れる。</summary>
        private void ApplyPendingCommands()
        {
            if (_clearRequested)
            {
                _clearRequested = false;
                Storage.Clear();
                _renderTotal = 0;
            }

            for (var i = 0; i < _pending.Count; i++)
            {
                var spawn = _pending[i];
                Storage.TryAdd(spawn.position, spawn.type, spawn.hp, spawn.speed, spawn.animStart, spawn.face);
            }

            _pending.Clear();
        }

        private void ScheduleSimulation()
        {
            var dt = Mathf.Min(Time.deltaTime, _settings.maxFrameDelta);
            var count = Storage.Count;
            if (count <= 0 || dt <= 1e-5f)
            {
                return;
            }

            for (var b = 0; b < _beamCount; b++)
            {
                _beamsJob[b] = _beamQueue[b];
            }

            _handle = BuildJobChain(count, dt);
            _flightDt = dt;
            _inFlight = true;
            _jobWatch.Restart();

            if (detailedProfiling)
            {
                // 段階ごとの計測は、その場で完了を待つ同期モードでのみ意味を持つ。
                CompleteSimulation();
            }
        }

        /// <summary>ジョブの完了を待ち、結果（配列の入れ替え・コアへのダメージ・統計）を反映する。</summary>
        private void CompleteSimulation()
        {
            if (!_inFlight)
            {
                return;
            }

            _handle.Complete();
            _inFlight = false;

            _stats.jobLatencyMs = Mathf.Lerp(_stats.jobLatencyMs, (float)_jobWatch.Elapsed.TotalMilliseconds, 0.2f);

            Storage.Count = _counters[0];
            Storage.Swap();
            _renderTotal = Storage.Count;

            ApplyCoreDamage(_flightDt);
            _stats.totalKilled += _counters[1];
            _stats.totalArrived += _counters[2];

            if (detailedProfiling)
            {
                MeasureDensity();
            }

            if (captureCellCounts)
            {
                CaptureCellCounts();
            }
        }

        private JobHandle BuildJobChain(int count, float dt)
        {
            var maxRadius = 0.1f;
            for (var t = 0; t < _types.Count; t++)
            {
                maxRadius = Mathf.Max(maxRadius, _typeParams[t].radius);
            }

            var densityRadius = _settings.densityRadius;
            var cellSize = Mathf.Max(2f * maxRadius * Mathf.Max(1f, _settings.personalSpace), densityRadius, 0.2f);

            float2 areaOrigin;
            float2 areaSize;
            if (Navigation.IsCreated && Navigation.BuiltVersion >= 0)
            {
                areaOrigin = Navigation.Origin;
                areaSize = Navigation.Size;
            }
            else
            {
                areaSize = new float2(_settings.fallbackAreaSize, _settings.fallbackAreaSize);
                areaOrigin = -areaSize * 0.5f;
            }

            Grid.Configure(areaOrigin, areaSize, cellSize);
            var hashInvCell = Grid.InvCellSize;

            _stageWatch.Restart();
            _stageLast = 0d;

            var handle = new SwarmCellIndexJob
            {
                pos = Storage.pos,
                cellOf = Grid.CellOf,
                origin = Grid.Origin,
                invCell = hashInvCell,
                width = Grid.Width,
                height = Grid.Height
            }.Schedule(count, 256);

            handle = new SwarmBuildGridJob
            {
                count = count,
                width = Grid.Width,
                height = Grid.Height,
                cellOf = Grid.CellOf,
                cellStart = Grid.CellStart,
                cursor = Grid.Cursor,
                cellItems = Grid.CellItems
            }.Schedule(handle);
            Mark(ref handle, 0);

            if (_beamCount > 0)
            {
                handle = new SwarmLaserJob
                {
                    pos = Storage.pos,
                    hp = Storage.hp,
                    flash = Storage.flash,
                    typeIdx = Storage.typeIdx,
                    types = _typeParams,
                    beams = _beamsJob,
                    beamCount = _beamCount,
                    cellStart = Grid.CellStart,
                    cellItems = Grid.CellItems,
                    hashOrigin = Grid.Origin,
                    hashInvCell = hashInvCell,
                    hashW = Grid.Width,
                    hashH = Grid.Height,
                    maxRadius = maxRadius,
                    dt = dt,
                    hitT = _hitT,
                    hitIdx = _hitIdx
                }.Schedule(handle);
                Mark(ref handle, 1);
            }

            handle = new SwarmSteerJob
            {
                pos = Storage.pos,
                vel = Storage.vel,
                predOut = Storage.predA,
                typeIdx = Storage.typeIdx,
                state = Storage.state,
                speedScale = Storage.speedScale,
                types = _typeParams,
                flowDirs = Navigation.FlowDirs,
                speedMul = Navigation.SpeedMultiplier,
                goals = _goals,
                goalCount = _goalCount,
                navOrigin = Navigation.Origin,
                navCell = Navigation.CellSize,
                navW = Navigation.Width,
                navH = Navigation.Height,
                navCellCount = Navigation.CellCount,
                cellStart = Grid.CellStart,
                cellItems = Grid.CellItems,
                hashOrigin = Grid.Origin,
                hashInvCell = hashInvCell,
                hashW = Grid.Width,
                hashH = Grid.Height,
                dt = dt,
                densityRadiusSq = densityRadius * densityRadius,
                densityReference = _settings.densityReferenceCount,
                densitySlowdown = _settings.densitySlowdown,
                minSpeedFactor = _settings.minSpeedFactor,
                maxNeighbors = _settings.maxNeighborsChecked
            }.Schedule(count, 128, handle);
            Mark(ref handle, 2);

            var input = Storage.predA;
            var output = Storage.predB;
            for (var iteration = 0; iteration < _settings.separationIterations; iteration++)
            {
                handle = new SwarmSeparationJob
                {
                    predIn = input,
                    predOut = output,
                    typeIdx = Storage.typeIdx,
                    state = Storage.state,
                    types = _typeParams,
                    cellStart = Grid.CellStart,
                    cellItems = Grid.CellItems,
                    hashOrigin = Grid.Origin,
                    hashInvCell = hashInvCell,
                    hashW = Grid.Width,
                    hashH = Grid.Height,
                    sdf = Navigation.Sdf,
                    sdfGradient = Navigation.SdfGradient,
                    navOrigin = Navigation.Origin,
                    navCell = Navigation.CellSize,
                    navW = Navigation.Width,
                    navH = Navigation.Height,
                    stiffness = _settings.separationStiffness,
                    personalSpace = _settings.personalSpace,
                    maxCorrectionRatio = _settings.maxCorrectionRatio,
                    wallStiffness = _settings.wallStiffness,
                    maxNeighbors = _settings.maxNeighborsChecked
                }.Schedule(count, 128, handle);

                var swap = input;
                input = output;
                output = swap;
            }

            Mark(ref handle, 3);

            handle = new SwarmFinalizeJob
            {
                pos = Storage.pos,
                vel = Storage.vel,
                facing = Storage.facing,
                animTime = Storage.animTime,
                flash = Storage.flash,
                goalOf = Storage.goalOf,
                pred = input,
                typeIdx = Storage.typeIdx,
                state = Storage.state,
                types = _typeParams,
                goals = _goals,
                goalCount = _goalCount,
                dt = dt,
                momentumTransfer = _settings.momentumTransfer,
                flashDecay = 1f / _settings.hitFlashDuration,
                arrivalRadius = _settings.arrivalRadius
            }.Schedule(count, 128, handle);
            Mark(ref handle, 4);

            handle = new SwarmCompactScanJob
            {
                order = Grid.CellItems,
                pos = Storage.pos,
                hp = Storage.hp,
                goalOf = Storage.goalOf,
                typeIdx = Storage.typeIdx,
                state = Storage.state,
                types = _typeParams,
                count = count,
                arrivalMode = (int)_settings.arrivalMode,
                keepList = _keepList,
                counters = _counters,
                goalDamage = _goalDamage,
                attached = _attached,
                killsByType = _killsByType,
                yRange = _yRange
            }.Schedule(handle);

            handle = new SwarmCompactCopyJob
            {
                keepList = _keepList,
                counters = _counters,
                pos = Storage.pos,
                vel = Storage.vel,
                facing = Storage.facing,
                hp = Storage.hp,
                animTime = Storage.animTime,
                flash = Storage.flash,
                speedScale = Storage.speedScale,
                typeIdx = Storage.typeIdx,
                state = Storage.state,
                goalOf = Storage.goalOf,
                posOut = Storage.posB,
                velOut = Storage.velB,
                facingOut = Storage.facingB,
                hpOut = Storage.hpB,
                animTimeOut = Storage.animTimeB,
                flashOut = Storage.flashB,
                speedScaleOut = Storage.speedScaleB,
                typeIdxOut = Storage.typeIdxB,
                stateOut = Storage.stateB,
                goalOfOut = Storage.goalOfB
            }.Schedule(count, 256, handle);
            Mark(ref handle, 5);

            // 描画データは、詰め直し先（〜B）の配列から作る（Swap前）。
            handle = new SwarmPrepInstanceJob
            {
                pos = Storage.posB,
                facing = Storage.facingB,
                animTime = Storage.animTimeB,
                flash = Storage.flashB,
                typeIdx = Storage.typeIdxB,
                types = _typeParams,
                counters = _counters,
                yRange = _yRange,
                ySort = _settings.ySort ? 1 : 0,
                tmpInstances = _tmpInstances,
                keyOf = _keyOf
            }.Schedule(count, 256, handle);

            handle = new SwarmPrepSortJob
            {
                counters = _counters,
                tmpInstances = _tmpInstances,
                keyOf = _keyOf,
                instances = _instances,
                typeStart = _typeStart,
                keyStart = _keyStart,
                keyCursor = _keyCursor
            }.Schedule(handle);
            Mark(ref handle, 6);

            return handle;
        }

        /// <summary>detailedProfiling時のみ、直前のジョブを完了させて、その段階の処理時間を記録する。</summary>
        private void Mark(ref JobHandle handle, int stage)
        {
            if (!detailedProfiling)
            {
                return;
            }

            handle.Complete();
            var now = _stageWatch.Elapsed.TotalMilliseconds;
            StageMs[stage] = Mathf.Lerp(StageMs[stage], (float)(now - _stageLast), 0.2f);
            _stageLast = now;
        }

        private void ApplyCoreDamage(float dt)
        {
            var goals = Mathf.Min(_cores.Count, _goalCount);
            for (var g = 0; g < goals; g++)
            {
                var damage = _goalDamage[g] + _attached[g] * _settings.lingerDamagePerSecond * dt;
                if (damage > 0f && _cores[g] != null)
                {
                    _cores[g].TakeDamage(damage);
                }
            }
        }

        // ---------------------------------------------------------------
        // 入力データの更新（ジョブが動いていない間にだけ呼ぶ）
        // ---------------------------------------------------------------

        private void RefreshTypeParams()
        {
            var profileCountBefore = _profiles.Count;

            for (var t = 0; t < _types.Count; t++)
            {
                var definition = _types[t];
                var swarm = definition.swarm;
                var profileIndex = IndexOfProfile(definition.navigationProfile);
                _typeProfileIndex[t] = profileIndex;

                _typeParams[t] = new SwarmTypeParams
                {
                    radius = swarm.radius,
                    mass = swarm.mass,
                    maxSpeed = definition.moveSpeed,
                    acceleration = swarm.acceleration,
                    turnSharpness = _settings.turnSharpness,
                    animFps = swarm.animationFps,
                    spriteSize = swarm.spriteSize,
                    damageToCore = definition.damageToCore,
                    profileIndex = profileIndex,
                    frameCount = Mathf.Max(1, swarm.frameCount),
                    directionCount = Mathf.Max(1, swarm.directionCount)
                };
            }

            if (_profiles.Count != profileCountBefore)
            {
                _navigationDirty = true;
            }
        }

        private int IndexOfProfile(NavigationProfile profile)
        {
            for (var i = 0; i < _profiles.Count; i++)
            {
                if (_profiles[i] == profile)
                {
                    return i;
                }
            }

            _profiles.Add(profile);
            return _profiles.Count - 1;
        }

        private void RefreshNavigation()
        {
            if (_navField == null)
            {
                _navField = FindFirstObjectByType<NavigationField>();
            }

            if (_navField != null)
            {
                if (_navigationDirty || Navigation.BuiltVersion != _navField.RebuildCount || !Navigation.IsCreated)
                {
                    Navigation.Rebuild(_navField, _profiles);
                    _navigationDirty = false;
                }

                return;
            }

            if (!Navigation.IsCreated || _navigationDirty)
            {
                Navigation.CreateFallback(float2.zero, _settings.fallbackAreaSize, _profiles.Count);
                _navigationDirty = false;
            }
        }

        private void RefreshCores()
        {
            _cores.Clear();
            _cores.AddRange(FindObjectsByType<CoreCrystalController>(FindObjectsSortMode.None));
            _goalCount = Mathf.Min(_cores.Count, SwarmLimits.MaxGoals);
        }

        private void UpdateGoalPositions()
        {
            for (var g = 0; g < _goalCount; g++)
            {
                if (_cores[g] != null)
                {
                    _goals[g] = (Vector2)_cores[g].transform.position;
                }
            }
        }

        // ---------------------------------------------------------------
        // 計測
        // ---------------------------------------------------------------

        /// <summary>空間ハッシュの各セルにいる敵の数から、最も混んだセルと、近傍チェック回数の概算を求める。</summary>
        private void MeasureDensity()
        {
            var cells = Grid.Width * Grid.Height;
            var max = 0;
            var estimate = 0f;
            for (var c = 0; c < cells; c++)
            {
                var n = Grid.CellStart[c + 1] - Grid.CellStart[c];
                if (n > max)
                {
                    max = n;
                }

                estimate += (float)n * n * 9f;
            }

            _lastMaxCell = max;
            _stats.maxCellCount = max;
            _stats.pairCheckEstimate = estimate;
        }

        private void CaptureCellCounts()
        {
            var cells = Grid.Width * Grid.Height;
            if (CellCounts.Length != cells)
            {
                CellCounts = new int[cells];
            }

            for (var c = 0; c < cells; c++)
            {
                CellCounts[c] = Grid.CellStart[c + 1] - Grid.CellStart[c];
            }

            CellGridOrigin = new Vector2(Grid.Origin.x, Grid.Origin.y);
            CellGridSize = Grid.CellSize;
            CellGridWidth = Grid.Width;
            CellGridHeight = Grid.Height;
        }

        private void DetectSpike(float frameMs)
        {
            var gc = System.GC.CollectionCount(0);
            _stats.gcCollections = gc;

            if (frameMs > spikeThresholdMs && Time.frameCount > 10)
            {
                if (_spikes.Count >= 12)
                {
                    _spikes.RemoveAt(0);
                }

                _spikes.Add(new SwarmSpike
                {
                    time = Time.unscaledTime,
                    frameMs = frameMs,
                    navigationMs = _lastNavMs,
                    simulationMs = _lastSimMs,
                    renderMs = _lastRenderMs,
                    alive = _lastAlive,
                    maxCellCount = _lastMaxCell,
                    navigationRebuilt = _lastNavRebuilt,
                    gcOccurred = gc != _lastGcCount
                });
            }

            _lastGcCount = gc;
        }

        private void RecordHistory()
        {
            FrameMsHistory[HistoryCursor] = Time.unscaledDeltaTime * 1000f;
            SimulationMsHistory[HistoryCursor] = _lastSimMs;
            HistoryCursor = (HistoryCursor + 1) % HistoryLength;
        }

        private void OnGUI()
        {
            if (showHud)
            {
                SwarmHud.Draw(this);
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }

            if (!_initialized)
            {
                return;
            }

            // 動いているジョブが配列を使っている間に破棄すると危険なので、必ず先に完了させる。
            if (_inFlight)
            {
                _handle.Complete();
                _inFlight = false;
            }

            _renderer?.Dispose();
            Storage?.Dispose();
            Grid?.Dispose();
            Navigation?.Dispose();

            DisposeArray(ref _typeParams);
            DisposeArray(ref _goals);
            DisposeArray(ref _beamsJob);
            DisposeArray(ref _counters);
            DisposeArray(ref _goalDamage);
            DisposeArray(ref _attached);
            DisposeArray(ref _killsByType);
            DisposeArray(ref _instances);
            DisposeArray(ref _tmpInstances);
            DisposeArray(ref _typeStart);
            DisposeArray(ref _keyStart);
            DisposeArray(ref _keyCursor);
            DisposeArray(ref _keyOf);
            DisposeArray(ref _keepList);
            DisposeArray(ref _yRange);
            DisposeArray(ref _hitT);
            DisposeArray(ref _hitIdx);
        }

        private static void DisposeArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
            {
                array.Dispose();
            }
        }
    }
}
