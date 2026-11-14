using System;
using MS2026.Fortress;
using MS2026.Fortress.Cameras;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU の係（起動時に自動で作られる、見えないオブジェクト。シーンの編集は要らない）。
    /// ・どの席を CPU にするかを 0.5 秒ごとに決め直し（<see cref="CpuSeatRules"/>）、CPU の手を <see cref="PlayerPilots"/> に入れる。
    /// ・CPU は結果を決める PC（Host・オフライン）だけで動く。ほかのPCから人が入った席は、人に任せて CPU は止まる。
    /// ・ゲームの画面には何も出さない（人と区別しない）。
    /// ・砲台が作り直されたら（シーンを読み直した = 新しい試合）、その席の CPU も作り直す（学んだことは試合ごとに 0 に戻る）。
    /// </summary>
    [AddComponentMenu("")]
    [DefaultExecutionOrder(-30)] // 砲台（LaserTurret -10）・移動（-20）より先に手を動かす
    public sealed class CpuDirector : MonoBehaviour
    {
        private const int PlayerCount = CpuSeatRules.PlayerCount;
        private const float RefreshInterval = 0.5f;

        private readonly CpuPlayer[] _players = new CpuPlayer[PlayerCount];
        private readonly LaserTurret[] _turrets = new LaserTurret[PlayerCount];
        private readonly LaserTurret[] _playerTurrets = new LaserTurret[PlayerCount]; // CPU を作ったときの砲台
        private readonly LaserTurret[] _lastTurrets = new LaserTurret[PlayerCount];
        private readonly CpuSwarmSampler _sampler = new CpuSwarmSampler();

        private CpuSettings _settings;
        private float _refreshTimer;
        private int _randomBase;
        private int _matchCount;
        private int _loggedMask = -1;

        /// <summary>今の係（無ければ null）。</summary>
        public static CpuDirector Current { get; private set; }

        /// <summary>CPU の席が変わったとき。</summary>
        public static event Action SeatsChanged;

        /// <summary>CPU の席（bit p = P(p+1)）。</summary>
        public int CpuMask { get; private set; }

        public bool IsCpu(int player) => player >= 0 && player < PlayerCount && (CpuMask & (1 << player)) != 0;

        /// <summary>プレイヤー player の CPU（CPU でなければ null）。</summary>
        public CpuPlayer GetPlayer(int player) => player >= 0 && player < PlayerCount ? _players[player] : null;

        /// <summary>今の強さ（起動オプション → 設定）。</summary>
        public CpuLevel Level => CpuLaunchOptions.Current.Level ?? (_settings != null ? _settings.level : CpuLevel.Normal);

        /// <summary>CPU 全員で使う、群衆の敵の位置の写し。</summary>
        public CpuSwarmSampler Sampler => _sampler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Current = null;
            SeatsChanged = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindFirstObjectByType<CpuDirector>() != null)
            {
                return;
            }

            // DontSave は付けない（付けるとエディタで Play を止めても残る）。DontDestroyOnLoad だけなら Play を止めると消える。
            var go = new GameObject("[CPU] Director");
            DontDestroyOnLoad(go);
            go.AddComponent<CpuDirector>();
        }

        private void OnEnable()
        {
            Current = this;
            _settings = CpuSettings.Load();
            _randomBase = Environment.TickCount;
            _refreshTimer = 0f;
        }

        private void OnDisable()
        {
            StopAll();
            _sampler.Dispose();
            if (Current == this)
            {
                Current = null;
            }
        }

        private void Update()
        {
            _refreshTimer -= Time.unscaledDeltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = RefreshInterval;
                Refresh();
            }

            // 群衆の写しは CPU がいる間だけ取る（写すのは次の、群衆の配列を読める瞬間）。
            if (CpuMask != 0)
            {
                _sampler.Interval = _settings != null ? _settings.senseInterval : 0.1f;
                _sampler.Track();
            }
            else
            {
                _sampler.Dispose();
            }

            var dt = Time.deltaTime;
            for (var p = 0; p < PlayerCount; p++)
            {
                var cpu = _players[p];
                if (cpu == null)
                {
                    continue;
                }

                try
                {
                    cpu.Tick(_turrets[p], _sampler, dt);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        // ───────── 席を決め直す ─────────

        private void Refresh()
        {
            RefreshTurrets();

            var launch = CpuLaunchOptions.Current;
            var inputs = new CpuSeatInputs
            {
                Enabled = _settings != null && _settings.cpuEnabled,
                HasAuthority = FortressNet.HasSimulationAuthority,
                Networked = FortressNet.IsNetworked,
                LocalPlayer = LocalPlayer(),
                RemoteHumanMask = RemoteHumanMask(),
                TurretMask = TurretMask(),
                FillOtherSeatsOffline = _settings == null || _settings.fillOtherSeatsOffline,
                Mode = launch.SeatMode,
                ListMask = launch.SeatMask
            };

            Apply(CpuSeatRules.ComputeCpuMask(inputs));
        }

        private void Apply(int mask)
        {
            var changed = false;
            for (var p = 0; p < PlayerCount; p++)
            {
                var want = (mask & (1 << p)) != 0;
                var cpu = _players[p];

                // 砲台が作り直された（新しい試合）なら、CPU も作り直す。
                if (cpu != null && (!want || _playerTurrets[p] != _turrets[p]))
                {
                    StopPlayer(p);
                    cpu = null;
                    changed = true;
                }

                if (want && cpu == null)
                {
                    StartPlayer(p);
                    changed = true;
                }
            }

            CpuMask = mask;
            if (changed)
            {
                LogSeats();
                SeatsChanged?.Invoke();
            }
        }

        private void StartPlayer(int player)
        {
            var seed = CpuLaunchOptions.Current.Seed ?? (_settings != null ? _settings.randomSeed : 0);
            var skill = _settings != null ? _settings.skill : CpuSkillValues.Default;
            var cpu = new CpuPlayer(player, Level, CpuSeatRules.SeedFor(seed, player, _matchCount, _randomBase),
                _settings != null ? _settings.thinkInterval : 0.15f, skill);
            _players[player] = cpu;
            _playerTurrets[player] = _turrets[player];
            PlayerPilots.Set(player, cpu.Hands);
        }

        private void StopPlayer(int player)
        {
            var cpu = _players[player];
            if (cpu == null)
            {
                return;
            }

            _players[player] = null;
            if (PlayerPilots.TryGet(player, out var pilot) && ReferenceEquals(pilot, cpu.Hands))
            {
                PlayerPilots.Clear(player);
            }

            try
            {
                cpu.Stop();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void StopAll()
        {
            var had = CpuMask != 0;
            for (var p = 0; p < PlayerCount; p++)
            {
                StopPlayer(p);
            }

            CpuMask = 0;
            if (had)
            {
                SeatsChanged?.Invoke();
            }
        }

        private void LogSeats()
        {
            if ((_settings != null && !_settings.logSeatChanges) || _loggedMask == CpuMask)
            {
                return;
            }

            _loggedMask = CpuMask;
            Debug.Log(CpuMask != 0
                ? $"[CPU] CPU の席: {CpuSeatRules.Describe(CpuMask)}（{CpuLevels.Label(Level)}）。行動 {CpuPlugins.BehaviorCount} 種類" +
                  (CpuPlugins.HasMovePlanner ? "・移動あり" : "・移動なし")
                : "[CPU] CPU の席: なし");
        }

        // ───────── ゲームの様子を読む ─────────

        private void RefreshTurrets()
        {
            Array.Clear(_turrets, 0, _turrets.Length);
            foreach (var turret in FindObjectsByType<LaserTurret>(FindObjectsSortMode.None))
            {
                var p = turret.playerIndex;
                if (p >= 0 && p < PlayerCount && _turrets[p] == null)
                {
                    _turrets[p] = turret;
                }
            }

            // 新しい砲台が出てきたら（シーンを読み直した = 新しい試合）、試合の数を進める（乱数の種が変わる）。
            var newMatch = false;
            for (var p = 0; p < PlayerCount; p++)
            {
                if (_turrets[p] != null && _turrets[p] != _lastTurrets[p])
                {
                    newMatch = true;
                }

                _lastTurrets[p] = _turrets[p];
            }

            if (newMatch)
            {
                _matchCount++;
            }
        }

        private int TurretMask()
        {
            var mask = 0;
            for (var p = 0; p < PlayerCount; p++)
            {
                if (_turrets[p] != null && !_turrets[p].IsReplica)
                {
                    mask |= 1 << p;
                }
            }

            return mask;
        }

        // このPCの人のプレイヤー番号（通信中は自分の番号、オフラインはカメラで見ている人、それも無ければ P1）。
        private static int LocalPlayer()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap != null && bootstrap.LocalPlayerIndex >= 0 && bootstrap.LocalPlayerIndex < PlayerCount)
            {
                return bootstrap.LocalPlayerIndex;
            }

            var rig = FortressCameraRig.Active;
            return rig != null && ViewerIndex.IsPlayer(rig.CurrentViewer) ? rig.CurrentViewer : 0;
        }

        private static int RemoteHumanMask()
        {
            var bootstrap = FortressNetworkBootstrap.Instance;
            if (bootstrap == null || !FortressNet.IsNetworked)
            {
                return 0;
            }

            var mask = 0;
            foreach (var pair in bootstrap.ClientIdToPlayerIndex)
            {
                if (pair.Value >= 0 && pair.Value < PlayerCount)
                {
                    mask |= 1 << pair.Value;
                }
            }

            // 自分（Host）の番号は CpuSeatRules が除く。
            return mask;
        }
    }
}
