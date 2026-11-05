using System;
using System.Collections.Generic;
using System.Text;
using MS2026.Fortress;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Spectator
{
    /// <summary>
    /// 観客ページの係（起動時に自動で作られる、見えないオブジェクト。シーンに置く物は無い）。
    /// ・Web サーバーを動かす（ビルドでは常に。エディタではメニューでオンにしたときだけ）
    /// ・1 秒に 4 回、試合の様子とステージの地図を写してサーバーに渡す
    /// ・観客が送った分を取り出し、この PC が Host（かオフライン）なら印つきで菌を出す
    /// ・印つきの菌を追いかけ、群れの中心・広がり・残りの数を 1 秒に 5 回、全員の PC に配る（名前札と光の輪）
    /// ・試合ごとの上限（ウェーブの菌の 15% 上乗せまで）と、ボスのたまり（15 回で 1 体、1 試合 2 体まで。上限が 1 試合およそ 20 回分なので、1 試合に 1 回は見られる数）
    /// M2 でも Client の PC からは送れない（M3 で Host へ取り次ぐ）。
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SpectatorHost : MonoBehaviour
    {
        private const float BoardInterval = 0.25f;
        private const float ScanInterval = 0.2f;
        private const int BossFallbackGerms = 40;
        private const string PageResource = "Spectator/SpectatorPage";

        private static SpectatorHost _instance;

        private readonly SpectatorStage _stage = new SpectatorStage();
        private readonly SpectatorGroupBook _book = new SpectatorGroupBook();
        private readonly SpectatorBudget _budget = new SpectatorBudget();
        private readonly List<SpectatorGroupState> _groups = new List<SpectatorGroupState>();
        private SpectatorApi _api;
        private SpectatorHttpServer _server;
        private SwarmSystem _swarm;
        private int _port;
        private float _boardTimer;
        private float _scanTimer;
        private bool _scanDue;
        private string _matchKey = "";
        private int _spawnBaseline;

        /// <summary>動いている係（無ければ null）。</summary>
        public static SpectatorHost Instance => _instance;

        public int Port => _port;

        public int SpectatorCount => _api != null ? _api.Roster.Count : 0;

        private static double Now => Time.realtimeSinceStartupAsDouble;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var options = SpectatorConfig.Parse(Environment.GetCommandLineArgs());
            if (options.Disabled || _instance != null)
            {
                return;
            }

            if (Application.isEditor && PlayerPrefs.GetInt(SpectatorConfig.EditorEnabledKey, 0) != 1)
            {
                return;
            }

            // DontSave は付けない（付けると Play を止めても消えずに残る）。
            var go = new GameObject("[Spectator] Host") { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            var host = go.AddComponent<SpectatorHost>();
            host._port = options.Port;
        }

        private void Awake()
        {
            _instance = this;
        }

        private void Start()
        {
            _api = new SpectatorApi(new SpectatorRoster(SpectatorConfig.MaxSpectators, SpectatorConfig.CooldownSeconds));
            var page = Resources.Load<TextAsset>(PageResource);
            if (page != null)
            {
                _api.SetPage(page.bytes);
            }
            else
            {
                Debug.LogWarning($"[Spectator] スマホのページ（Resources/{PageResource}.html）が見つかりません。");
            }

            _server = new SpectatorHttpServer(_api.Handle, SpectatorConfig.MaxConnections);
            var started = _server.Start(_port, out var error);

            // エディタでは、番号がふさがっていたら隣の番号を試す（ビルドは印刷した QR と同じ番号のまま）。
            for (var i = 1; !started && Application.isEditor && i <= 5; i++)
            {
                started = _server.Start(_port + i, out _);
                if (started)
                {
                    Debug.LogWarning($"[Spectator] 番号 {_port} がふさがっていたので {_port + i} で開きます（{error}）。");
                    _port += i;
                }
            }

            if (!started)
            {
                Debug.LogWarning($"[Spectator] 観客ページを開けませんでした（番号 {_port}）: {error}。ほかのアプリが同じ番号を使っていないか確かめてください。起動オプション {SpectatorConfig.PortFlag} で番号を変えられます。");
                _server = null;
                enabled = false;
                return;
            }

            Debug.Log($"[Spectator] 観客ページを開きました: {SpectatorAddresses.Urls(_port)}");
            _stage.Refresh();
            UpdateBoard();
        }

        private void Update()
        {
            if (_api == null)
            {
                return;
            }

            _stage.Refresh();
            Hook(SwarmSystem.Current);
            CheckNewMatch();

            while (_api.TryDequeue(out var order))
            {
                Carry(order);
            }

            _boardTimer -= Time.unscaledDeltaTime;
            if (_boardTimer <= 0f)
            {
                _boardTimer = BoardInterval;
                UpdateBoard();
            }

            _scanTimer -= Time.unscaledDeltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = ScanInterval;
                _scanDue = true;
            }
        }

        // ───────── 試合の区切り ─────────

        private void CheckNewMatch()
        {
            var key = FortressMatch.InMatch ? _stage.Key : "";
            if (key == _matchKey)
            {
                return;
            }

            _matchKey = key;
            _book.Clear();
            _budget.Reset();
            _spawnBaseline = _swarm != null ? _swarm.Stats.totalSpawned : 0;
            _groups.Clear();
            SpectatorGroupFeed.Publish(_groups);
        }

        // ───────── 菌を出す ─────────

        private void Carry(SpectatorOrder order)
        {
            if (order.StageKey != _stage.Key || !FortressNet.HasSimulationAuthority)
            {
                // ステージが変わった・Host でなくなった。待ち時間を返して、もう一度送れるようにする。
                _api.Roster.Refund(order.SpectatorId);
                return;
            }

            var tag = _book.NewTag();
            _book.AddGroup(tag, order.Number, order.ColorIndex, false, Now);
            var spawned = _stage.Spawn(order.ExitIndex, SpectatorConfig.GermsPerSend, tag);
            if (spawned <= 0)
            {
                _api.Roster.Refund(order.SpectatorId);
                return;
            }

            _budget.AddSpectatorSpawned(spawned);
            _book.RecordSend(order.Number, spawned);
            Debug.Log($"[Spectator] {order.Name} が出口 {_stage.ExitId(order.ExitIndex)} から {_stage.GermName} を {spawned} 体送り込んだ（ボスまで {_budget.Pool + 1}/{_budget.PoolMax}）");

            if (_budget.AddToPool())
            {
                SpawnBoss();
            }
        }

        private void SpawnBoss()
        {
            var exit = UnityEngine.Random.Range(0, _stage.ExitCount);
            var tag = _book.NewTag();
            _book.AddGroup(tag, 0, 0, true, Now);
            var spawned = _stage.SpawnBoss(exit, BossFallbackGerms, tag);
            if (spawned <= 0)
            {
                _budget.RefundBoss();
                return;
            }

            _budget.AddSpectatorSpawned(_stage.HasDistinctBoss ? SpectatorConfig.GermsPerSend : spawned);
            Debug.Log($"[Spectator] みんなのボス菌が出口 {_stage.ExitId(exit)} から出撃（この試合 {_budget.BossesSpawned}/{_budget.MaxBosses} 体目）");
        }

        // ───────── 群衆の係とのつなぎ ─────────

        private void Hook(SwarmSystem swarm)
        {
            if (ReferenceEquals(_swarm, swarm))
            {
                return;
            }

            Unhook();
            _swarm = swarm;
            if (_swarm == null)
            {
                return;
            }

            _swarm.TaggedSpawnAdded += OnTaggedSpawnAdded;
            _swarm.AgentsRemoved += OnAgentsRemoved;
            _swarm.StorageReadable += OnStorageReadable;
            _spawnBaseline = _swarm.Stats.totalSpawned;
        }

        private void Unhook()
        {
            if (ReferenceEquals(_swarm, null))
            {
                return;
            }

            _swarm.TaggedSpawnAdded -= OnTaggedSpawnAdded;
            _swarm.AgentsRemoved -= OnAgentsRemoved;
            _swarm.StorageReadable -= OnStorageReadable;
            _swarm = null;
        }

        private void OnTaggedSpawnAdded(int tag, int netId) => _book.OnAssigned(tag, netId);

        private void OnAgentsRemoved(IReadOnlyList<SwarmSystem.RemovalRecord> removed)
        {
            if (_book.TrackedAgents == 0)
            {
                return;
            }

            for (var i = 0; i < removed.Count; i++)
            {
                var reason = removed[i].Reason == EnemyRemovalReason.Died ? SpectatorRemoval.Killed
                    : removed[i].Reason == EnemyRemovalReason.ReachedCore ? SpectatorRemoval.ReachedCore
                    : SpectatorRemoval.Other;
                _book.OnRemoved(removed[i].Id, reason);
            }
        }

        // 配列を読める瞬間に、印つきの菌の位置を数えて配る（1 秒に 5 回）。
        private void OnStorageReadable()
        {
            if (!_scanDue || !FortressNet.HasSimulationAuthority)
            {
                return;
            }

            _scanDue = false;
            _book.BeginScan();
            if (_book.TrackedAgents > 0 && _swarm != null)
            {
                var storage = _swarm.Storage;
                for (var i = 0; i < storage.Count; i++)
                {
                    var p = storage.pos[i];
                    _book.Accumulate(storage.netId[i], p.x, p.y);
                }
            }

            _book.EndScan(Now);
            _book.Snapshot(_groups, SpectatorGroupFeed.MaxGroups);
            SpectatorGroupFeed.Publish(_groups);
            SpectatorGroupFeed.Send(_groups);
        }

        // ───────── スマホに見せる様子 ─────────

        private void UpdateBoard()
        {
            if (_swarm != null)
            {
                _budget.SetWaveSpawned(_swarm.Stats.totalSpawned - _spawnBaseline - _budget.SpectatorSpawned);
            }

            var phase = ReadPhase(out var message);
            var sb = new StringBuilder(512);
            sb.Append('{')
                .Key("phase").Str(phase).Append(',')
                .Key("msg").Str(message).Append(',')
                .Key("stage").Str(_stage.Key).Append(',')
                .Key("timeLeft").Num(FortressWaves.TimeLeft).Append(',')
                .Key("wave").Num(FortressWaves.IsActive ? FortressWaves.WaveNumber : 0).Append(',')
                .Key("waveCount").Num(FortressWaves.WaveCount).Append(',')
                .Key("germs").Num(SpectatorConfig.GermsPerSend).Append(',')
                .Key("pool").Num(_budget.Pool).Append(',')
                .Key("poolMax").Num(_budget.PoolMax).Append(',')
                .Key("bossLeft").Num(_budget.BossesLeft).Append(',')
                .Key("bossDone").Num(_budget.BossesSpawned).Append(',')
                .Key("exits");
            _stage.AppendExitsJson(sb);
            sb.Append('}');

            _api.Board = new SpectatorBoard
            {
                Phase = phase,
                StageKey = _stage.Key,
                ExitCount = _stage.ExitCount,
                Allowance = _budget.Allowance,
                SharedJson = sb.ToString()
            };
        }

        /// <summary>いま送れるか（play）と、スマホに出す一言。</summary>
        private string ReadPhase(out string message)
        {
            if (!FortressMatch.InMatch || !_stage.IsReady)
            {
                message = "次の試合を待っています";
                return SpectatorPhase.Wait;
            }

            if (FortressMatch.HasEnded)
            {
                message = "試合が終わりました。次の試合を待っています";
                return SpectatorPhase.End;
            }

            if (FortressNet.IsNetworked && !FortressNet.HasSimulationAuthority)
            {
                message = "このPCからはまだ送れません（作業中）";
                return SpectatorPhase.Wait;
            }

            if (!FortressWaves.IsActive)
            {
                message = "出口をタップして菌を送ろう";
                return SpectatorPhase.Play;
            }

            switch (FortressWaves.Phase)
            {
                case WavePhase.Combat:
                    message = "出口をタップして菌を送ろう";
                    return SpectatorPhase.Play;
                case WavePhase.Announce:
                    message = $"WAVE {FortressWaves.WaveNumber} がはじまります";
                    return SpectatorPhase.Wait;
                case WavePhase.Retreat:
                    message = "WAVE CLEAR！ 次のウェーブを待っています";
                    return SpectatorPhase.Wait;
                case WavePhase.Break:
                    message = "歯みがきタイム中。次のウェーブを待っています";
                    return SpectatorPhase.Wait;
                case WavePhase.Finished:
                    message = "試合が終わりました。次の試合を待っています";
                    return SpectatorPhase.End;
                default:
                    message = "まもなく試合がはじまります";
                    return SpectatorPhase.Wait;
            }
        }

        private void OnDestroy()
        {
            Unhook();
            _server?.Dispose();
            _server = null;
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            _server?.Dispose();
            _server = null;
        }
    }
}
