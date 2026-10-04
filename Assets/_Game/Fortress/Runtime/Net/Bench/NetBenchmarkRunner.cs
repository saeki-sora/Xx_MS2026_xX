using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEngine;

namespace MS2026.Fortress.Net
{
    /// <summary>
    /// 負荷計測(ベンチマーク)の自動実行。起動引数(<see cref="NetBenchmarkArgs"/>)があるときだけ自動で作られる。
    /// ・改善策の切り替え(起動引数での上書き)を、各部品が使い始める前(シーン読み込み直後、Startより前)に反映する。
    /// ・Host/オフラインでは、条件(Clientの人数など)が揃ってから指定秒後に群衆を一斉投入する。
    /// ・1秒ごとに計測値を「[Bench] 」で始まる1行でログへ書く(HostとClientのログを時刻で突き合わせられるよう、時計の時刻も書く)。
    /// ・指定秒で自動終了する(ログを確定させ、batから続けて次の計測を回せるようにするため)。
    /// </summary>
    public sealed class NetBenchmarkRunner : MonoBehaviour
    {
        private NetBenchmarkArgs _args;
        private bool _burstDone;
        private float _readySince = -1f;

        // 1秒ごとの集計
        private float _windowStart;
        private int _frames;
        private float _frameMsSum;
        private float _frameMsMax;
        private int _gcAtWindowStart;
        private long _sentAtWindowStart;
        private long _receivedAtWindowStart;
        private long _eventsSentAtWindowStart;
        private long _eventsReceivedAtWindowStart;
        private long _snapshotsSentAtWindowStart;
        private long _snapshotsReceivedAtWindowStart;

        // Client: 新しい写真が届いた瞬間の先読みの外れ(SwarmStats.replica*)の集計。
        private int _measured;
        private float _errorSum;
        private float _errorMax;
        private int _snaps;
        private long _allocBytes;
        private long _allocCount;
        private readonly StringBuilder _line = new StringBuilder(512);

        // そのフレームにC#で確保したメモリ量と回数(GCの元)。Unityのプロファイラの計測値を読むだけなので、計測自体の負荷はほぼ無い。
        private ProfilerRecorder _gcAllocated;
        private ProfilerRecorder _gcAllocations;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var args = NetBenchmarkArgs.Parse(Environment.GetCommandLineArgs());
            ApplyOverrides(args);
            if (!args.AnyBenchmark)
            {
                return;
            }

            var go = new GameObject(nameof(NetBenchmarkRunner));
            DontDestroyOnLoad(go);
            go.AddComponent<NetBenchmarkRunner>()._args = args;
        }

        // 改善策の切り替え。各部品のStart(接続開始・群衆の追加)より前に反映する必要がある。
        private static void ApplyOverrides(NetBenchmarkArgs args)
        {
            var swarm = FindFirstObjectByType<SwarmSystem>();
            if (swarm != null && args.SpawnsPerFrame.HasValue)
            {
                swarm.SpawnsPerFrameOverride = Mathf.Max(0, args.SpawnsPerFrame.Value);
            }

            var hub = FindFirstObjectByType<SwarmNetworkHub>();
            if (hub != null)
            {
                if (args.SnapshotRate.HasValue)
                {
                    hub.snapshotRate = Mathf.Max(5f, args.SnapshotRate.Value);
                }

                if (args.SnapshotLeadSeconds.HasValue)
                {
                    hub.snapshotLeadSeconds = Mathf.Max(0f, args.SnapshotLeadSeconds.Value);
                }

                if (args.AdaptiveSnapshotQuality.HasValue)
                {
                    hub.adaptiveSnapshotQuality = args.AdaptiveSnapshotQuality.Value;
                }

                if (args.SnapshotPrecisionShift.HasValue)
                {
                    hub.snapshotPrecisionShift = Mathf.Clamp(args.SnapshotPrecisionShift.Value, 0, 4);
                }
            }

            var bootstrap = FindFirstObjectByType<FortressNetworkBootstrap>();
            if (bootstrap != null && args.PacketQueueSize.HasValue)
            {
                bootstrap.packetQueueSize = Mathf.Max(1, args.PacketQueueSize.Value);
            }

            if (args.StopWaves)
            {
                foreach (var director in FindObjectsByType<EnemySpawnDirector>(FindObjectsSortMode.None))
                {
                    director.autoStartOnPlay = false;
                    director.StopWave();
                }
            }
        }

        private void Start()
        {
            // 1行で読めるよう、ログの呼び出し元(スタックトレース)を付けない。
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            _gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _gcAllocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocation In Frame Count");

            if (_args.HideDDriveOverlay)
            {
                foreach (var overlay in FindObjectsByType<DDrive.Runtime.Net.NetDebugOverlay>(FindObjectsSortMode.None))
                {
                    overlay.Visible = false;
                }
            }

            LogAndDisableComponents();
            StartWindow();
            Log($"START args={string.Join(" ", Environment.GetCommandLineArgs(), 1, Environment.GetCommandLineArgs().Length - 1)}");
        }

        // 動いている部品の種類と数をログに出し(どれを止めて試すかの手がかり)、指定された部品を止める。
        private void LogAndDisableComponents()
        {
            var disable = new HashSet<string>(_args.DisableTypes ?? Array.Empty<string>());
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var disabled = new SortedDictionary<string, int>(StringComparer.Ordinal);

            foreach (var behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour == null || behaviour == this || !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                var type = behaviour.GetType();
                counts[type.Name] = counts.TryGetValue(type.Name, out var c) ? c + 1 : 1;

                var hasOnGui = type.GetMethod("OnGUI", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
                if (disable.Contains(type.Name) || (_args.NoOnGui && hasOnGui))
                {
                    behaviour.enabled = false;
                    disabled[type.Name] = disabled.TryGetValue(type.Name, out var d) ? d + 1 : 1;
                }
            }

            var line = new StringBuilder("COMPONENTS");
            foreach (var pair in counts)
            {
                line.Append(' ').Append(pair.Key).Append('x').Append(pair.Value);
            }

            Log(line.ToString());

            if (disabled.Count > 0)
            {
                line.Clear().Append("DISABLED");
                foreach (var pair in disabled)
                {
                    line.Append(' ').Append(pair.Key).Append('x').Append(pair.Value);
                }

                Log(line.ToString());
            }
        }

        private void OnDestroy()
        {
            _gcAllocated.Dispose();
            _gcAllocations.Dispose();
        }

        private void Update()
        {
            var frameMs = Time.unscaledDeltaTime * 1000f;
            if (_gcAllocated.Valid)
            {
                _allocBytes += _gcAllocated.LastValue;
            }

            if (_gcAllocations.Valid)
            {
                _allocCount += _gcAllocations.LastValue;
            }

            _frames++;
            _frameMsSum += frameMs;
            _frameMsMax = Mathf.Max(_frameMsMax, frameMs);

            var swarm = SwarmSystem.Current;
            if (swarm != null)
            {
                var stats = swarm.Stats;
                _measured += stats.replicaCorrections;
                _errorSum += stats.replicaErrorSum;
                _errorMax = Mathf.Max(_errorMax, stats.replicaErrorMax);
                _snaps += stats.replicaSnaps;
            }

            TryBurst(swarm);

            if (_args.LogEnabled && Time.unscaledTime - _windowStart >= 1f)
            {
                WriteWindow(swarm);
                StartWindow();
            }

            if (_args.QuitAfterSeconds > 0f && Time.realtimeSinceStartup >= _args.QuitAfterSeconds)
            {
                Log("QUIT");
                _args.QuitAfterSeconds = 0f;
                Application.Quit();
            }
        }

        private void TryBurst(SwarmSystem swarm)
        {
            if (_burstDone || _args.BurstCount <= 0 || swarm == null || swarm.IsReplica || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            if (!IsReadyForBurst())
            {
                _readySince = -1f;
                return;
            }

            if (_readySince < 0f)
            {
                _readySince = Time.unscaledTime;
                Log($"READY clients={RemoteClientCount()}");
            }

            if (Time.unscaledTime - _readySince < _args.BurstDelaySeconds)
            {
                return;
            }

            var type = FindSwarmType();
            if (type == null)
            {
                Log("BURST-FAILED 群衆の敵の種類が見つかりません");
                _burstDone = true;
                return;
            }

            var field = FindFirstObjectByType<NavigationField>();
            var center = field != null ? field.areaCenter : Vector2.zero;
            var spawned = swarm.SpawnBurst(type, center, _args.BurstRadius, _args.BurstCount);
            Log($"BURST n={spawned} type={type.name} radius={_args.BurstRadius.ToString(CultureInfo.InvariantCulture)}");
            _burstDone = true;
        }

        // Clientを待つ指定があれば、その人数がつながり、全員分の写真を送り終えてから。
        private bool IsReadyForBurst()
        {
            if (_args.ExpectedClients <= 0)
            {
                return true;
            }

            return FortressNet.IsNetworked
                   && RemoteClientCount() >= _args.ExpectedClients
                   && NetTrafficStats.SwarmFullStateTransfers == 0;
        }

        private static int RemoteClientCount()
        {
            var networkManager = Unity.Netcode.NetworkManager.Singleton;
            return networkManager != null && networkManager.IsServer ? networkManager.ConnectedClientsIds.Count - 1 : 0;
        }

        private static EnemyTypeDefinition FindSwarmType()
        {
            foreach (var director in FindObjectsByType<EnemySpawnDirector>(FindObjectsSortMode.None))
            {
                if (director.wave == null || director.wave.spawnEntries == null)
                {
                    continue;
                }

                foreach (var entry in director.wave.spawnEntries)
                {
                    if (entry != null && entry.enemyType != null && entry.enemyType.simulationMode == EnemySimulationMode.Swarm)
                    {
                        return entry.enemyType;
                    }
                }
            }

            return null;
        }

        private void StartWindow()
        {
            _windowStart = Time.unscaledTime;
            _frames = 0;
            _frameMsSum = 0f;
            _frameMsMax = 0f;
            _gcAtWindowStart = GC.CollectionCount(0);
            _sentAtWindowStart = NetTrafficStats.BytesSent;
            _receivedAtWindowStart = NetTrafficStats.BytesReceived;
            _eventsSentAtWindowStart = NetTrafficStats.SwarmEventsSent;
            _eventsReceivedAtWindowStart = NetTrafficStats.SwarmEventsReceived;
            _snapshotsSentAtWindowStart = NetTrafficStats.SwarmSnapshotsSent;
            _snapshotsReceivedAtWindowStart = NetTrafficStats.SwarmSnapshotsReceived;
            _measured = 0;
            _errorSum = 0f;
            _errorMax = 0f;
            _snaps = 0;
            _allocBytes = 0;
            _allocCount = 0;
        }

        private void WriteWindow(SwarmSystem swarm)
        {
            var seconds = Mathf.Max(0.001f, Time.unscaledTime - _windowStart);
            var inv = CultureInfo.InvariantCulture;
            _line.Clear();
            _line.Append("role=").Append(Role());
            _line.Append(" fps=").Append((_frames / seconds).ToString("0.0", inv));
            _line.Append(" avgMs=").Append((_frameMsSum / Mathf.Max(1, _frames)).ToString("0.0", inv));
            _line.Append(" maxMs=").Append(_frameMsMax.ToString("0.0", inv));
            _line.Append(" gc=").Append(GC.CollectionCount(0) - _gcAtWindowStart);
            _line.Append(" memMB=").Append((GC.GetTotalMemory(false) / (1024f * 1024f)).ToString("0", inv));
            _line.Append(" allocKBs=").Append((_allocBytes / 1024f / seconds).ToString("0.0", inv));
            _line.Append(" allocs=").Append((long)(_allocCount / seconds));

            if (swarm != null)
            {
                var s = swarm.Stats;
                _line.Append(" alive=").Append(s.alive);
                _line.Append(" pending=").Append(s.pendingSpawns);
                _line.Append(" simMs=").Append(s.simulationMs.ToString("0.00", inv));
                _line.Append(" jobMs=").Append(s.jobLatencyMs.ToString("0.0", inv));
                _line.Append(" renderMs=").Append(s.renderMs.ToString("0.00", inv));
                _line.Append(" navMs=").Append(s.navigationMs.ToString("0.00", inv));
                _line.Append(" maxCell=").Append(s.maxCellCount);
            }

            _line.Append(" sentKBs=").Append(((NetTrafficStats.BytesSent - _sentAtWindowStart) / 1024f / seconds).ToString("0.0", inv));
            _line.Append(" recvKBs=").Append(((NetTrafficStats.BytesReceived - _receivedAtWindowStart) / 1024f / seconds).ToString("0.0", inv));
            _line.Append(" evSent=").Append(NetTrafficStats.SwarmEventsSent - _eventsSentAtWindowStart);
            _line.Append(" evRecv=").Append(NetTrafficStats.SwarmEventsReceived - _eventsReceivedAtWindowStart);

            var snapshotsSent = NetTrafficStats.SwarmSnapshotsSent - _snapshotsSentAtWindowStart;
            var snapshotsReceived = NetTrafficStats.SwarmSnapshotsReceived - _snapshotsReceivedAtWindowStart;
            if (snapshotsSent > 0)
            {
                _line.Append(" snapSent=").Append(snapshotsSent);
                _line.Append(" snapKB=").Append((NetTrafficStats.SwarmSnapshotBytes / 1024f).ToString("0.0", inv));
                _line.Append(" bpa=").Append(NetTrafficStats.SwarmSnapshotBitsPerAgent.ToString("0.0", inv));
                _line.Append(" snapShift=").Append(NetTrafficStats.SwarmSnapshotShift);
                _line.Append(" qualitySteps=").Append(NetTrafficStats.SwarmSnapshotQualityStepUps);
            }

            if (NetTrafficStats.SwarmSnapshotLagMs >= 0f)
            {
                _line.Append(" lagMs=").Append(NetTrafficStats.SwarmSnapshotLagMs.ToString("0", inv));
            }

            if (snapshotsReceived > 0 || NetTrafficStats.SwarmSnapshotResyncs > 0)
            {
                _line.Append(" snapRecv=").Append(snapshotsReceived);
                _line.Append(" resyncs=").Append(NetTrafficStats.SwarmSnapshotResyncs);
                if (swarm != null)
                {
                    _line.Append(" leadMs=").Append((swarm.ReplicaExtrapolationSeconds * 1000f).ToString("0", inv));
                }
            }

            if (_measured > 0)
            {
                // Client: 新しい写真が届いた瞬間の先読みの外れ(平均・最大)と、滑らせずにその場で合わせた数。
                // 互換のため、測った数は auditN、平均は errAudit にも同じ値を書く(以前の計測スクリプトがこの名前で読む)。
                _line.Append(" errAvg=").Append((_errorSum / _measured).ToString("0.000", inv));
                _line.Append(" errMax=").Append(_errorMax.ToString("0.00", inv));
                _line.Append(" snaps=").Append(_snaps);
                _line.Append(" errAudit=").Append((_errorSum / _measured).ToString("0.000", inv));
                _line.Append(" auditN=").Append(_measured);
            }

            Log(_line.ToString());
        }

        private static string Role()
        {
            var networkManager = Unity.Netcode.NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
            {
                return "Offline";
            }

            return networkManager.IsServer ? "Host" : "Client";
        }

        private static void Log(string message)
        {
            var inv = CultureInfo.InvariantCulture;
            Debug.Log($"[Bench] clock={DateTime.Now.ToString("HH:mm:ss.fff", inv)} t={Time.realtimeSinceStartup.ToString("0.00", inv)} {message}");
        }
    }
}
