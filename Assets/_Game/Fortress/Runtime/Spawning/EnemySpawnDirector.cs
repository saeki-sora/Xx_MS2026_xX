using System.Collections.Generic;
using System.Linq;
using MS2026.Fortress.Net;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// EnemyWaveConfigを再生し、指定した位置・数・タイミングで敵を生成する。
    /// 「敵の沸く位置や数、タイミング」を全てデータ(EnemyWaveConfig)側に追い出しているため、
    /// このクラス自体を編集しなくても敵配置を自由に調整できる。
    /// 時間の進め方は <see cref="WaveSpawnClock"/>（1フレームに何体でも出せる・くり返しが重ならない）。
    /// </summary>
    public sealed class EnemySpawnDirector : MonoBehaviour
    {
        [Tooltip("ウェーブの時間割（1本をくり返す、今までの遊び方）。下の「ウェーブの計画」があればそちらを使い、これは使わない。")]
        public EnemyWaveConfig wave;

        [Tooltip("ウェーブの計画（WAVE 1〜5・目標撃破数・制限時間）。入れると、ウェーブの係が計画どおりに湧かせる（Play したらすぐ再生する、は使わない）。空なら上の時間割1本。")]
        public WavePlan wavePlan;

        [Tooltip("未設定なら子オブジェクトからEnemySpawnPointを自動収集する。")]
        public EnemySpawnPoint[] spawnPoints;

        [Tooltip("EnemyTypeDefinition.visualPrefabが未設定な場合に使う土台プレファブ。未設定なら空のGameObjectを生成する。")]
        public GameObject fallbackEnemyPrefab;

        [Tooltip("Play開始時に自動でウェーブを再生するか。オフの場合はInspectorや要塞デザイナーの「ウェーブ開始」ボタンで手動再生する。")]
        public bool autoStartOnPlay = true;

        /// <summary>湧かせている途中か（くり返しの待ち時間の間も true）。</summary>
        public bool IsPlaying => _clock.IsRunning;

        /// <summary>今の周が始まってからの秒数（要塞デザイナーのタイムラインの赤い線）。</summary>
        public float ElapsedTime => _clock.CycleTime;

        /// <summary>今の再生で出した合計の数。</summary>
        public int SpawnedCount => _clock.TotalSpawned;

        private readonly WaveSpawnClock _clock = new WaveSpawnClock();
        private readonly List<EnemySpawnEntry> _due = new List<EnemySpawnEntry>();
        private readonly HashSet<string> _missingPointsLogged = new HashSet<string>();
        private Dictionary<string, EnemySpawnPoint> _spawnPointsById;
        private bool _swarmMissingLogged;
        private bool _noTypeLogged;

        private void Awake()
        {
            RefreshSpawnPoints();
        }

        private void Start()
        {
            // ウェーブの計画があれば、湧かせ始めるのはウェーブの係（FortressWaves）。
            if (autoStartOnPlay && wavePlan == null)
            {
                StartWave();
            }
        }

        public void RefreshSpawnPoints()
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                spawnPoints = GetComponentsInChildren<EnemySpawnPoint>();
            }

            _spawnPointsById = spawnPoints
                .Where(sp => sp != null)
                .GroupBy(sp => sp.ResolvedId)
                .ToDictionary(g => g.Key, g => g.First());
        }

        /// <summary>湧き位置の名前から、その位置（無ければ false）。</summary>
        public bool TryGetSpawnPoint(string id, out EnemySpawnPoint point)
        {
            if (_spawnPointsById == null)
            {
                RefreshSpawnPoints();
            }

            point = null;
            return id != null && _spawnPointsById != null && _spawnPointsById.TryGetValue(id, out point) && point != null;
        }

        /// <summary>
        /// このステージで使う全部の湧きの行（時間割 <see cref="wave"/>。計画があれば全部のウェーブの行も）。
        /// 敵の種類のネット番号・経路のチェック・テストなど「どの敵が出るか」を知りたい所は、必ずここから読む
        /// （1本の時間割だけを見ると、後のウェーブにしか出ない種類が漏れる）。
        /// </summary>
        public IEnumerable<EnemySpawnEntry> AllEntries()
        {
            if (wavePlan != null)
            {
                foreach (var entry in wavePlan.AllEntries())
                {
                    yield return entry;
                }
            }

            if (wave != null && wave.spawnEntries != null)
            {
                foreach (var entry in wave.spawnEntries)
                {
                    if (entry != null)
                    {
                        yield return entry;
                    }
                }
            }
        }

        /// <summary>このステージで出る全部の敵の種類（重なりなし、null なし）。</summary>
        public IEnumerable<EnemyTypeDefinition> AllEnemyTypes() =>
            AllEntries().Select(e => e.enemyType).Where(t => t != null).Distinct();

        /// <summary>ウェーブの再生を最初から開始する。</summary>
        public void StartWave()
        {
            StartWaveFrom(0f);
        }

        /// <summary>
        /// ウェーブを startTime 秒の時点から再生する（それより前に湧き始める行は飛ばす）。
        /// 要塞デザイナーの「この行から試す」用。0 なら最初から。
        /// </summary>
        public void StartWaveFrom(float startTime)
        {
            if (wave == null)
            {
                Debug.LogWarning("[EnemySpawnDirector] waveが設定されていません。", this);
                return;
            }

            PlayEntries(wave.spawnEntries, wave.loop, wave.loopInterval, startTime);
        }

        /// <summary>
        /// 好きな行の並びを再生する（ウェーブの計画の係が、ウェーブごとの行を渡す）。
        /// loop なら、最後の1体が出てから loopGap 秒後に最初からくり返す。
        /// </summary>
        public void PlayEntries(IReadOnlyList<EnemySpawnEntry> entries, bool loop, float loopGap, float startTime = 0f)
        {
            if (_spawnPointsById == null)
            {
                RefreshSpawnPoints();
            }

            _clock.Begin(entries, loop, loopGap, startTime);
        }

        public void StopWave()
        {
            _clock.Stop();
        }

        private void Update()
        {
            if (!_clock.IsRunning)
            {
                return;
            }

            _due.Clear();
            _clock.Advance(Time.deltaTime, _due);
            foreach (var entry in _due)
            {
                SpawnOne(entry);
            }
        }

        private void SpawnOne(EnemySpawnEntry entry)
        {
            if (!TryGetSpawnPoint(entry.spawnPointId, out var spawnPoint))
            {
                // 同じ名前の警告は1回だけ（1体ごとに出すと Console が埋まる）。
                if (_missingPointsLogged.Add(entry.spawnPointId ?? ""))
                {
                    Debug.LogWarning($"[EnemySpawnDirector] spawnPointId '{entry.spawnPointId}' が見つかりません（この名前の行は湧きません）。", this);
                }

                return;
            }

            if (entry.enemyType == null)
            {
                if (!_noTypeLogged)
                {
                    _noTypeLogged = true;
                    Debug.LogWarning("[EnemySpawnDirector] enemyTypeが未設定のエントリがあります。", this);
                }

                return;
            }

            if (entry.enemyType.simulationMode == EnemySimulationMode.Swarm)
            {
                var swarm = SwarmSystem.Current;
                if (swarm != null)
                {
                    swarm.Spawn(entry.enemyType, spawnPoint.transform.position);
                    return;
                }

                if (!_swarmMissingLogged)
                {
                    _swarmMissingLogged = true;
                    Debug.LogWarning(
                        "[EnemySpawnDirector] 敵の種類がSwarmモードですが、シーンにSwarmSystemがありません。" +
                        "要塞デザイナーの「群衆」タブで作成してください。今回は個別オブジェクトとして湧かせます。", this);
                }
            }

            // ネット対戦のClientでは、Actorの敵はHostが湧かせて届けるので自分では湧かせない。
            if (!FortressNet.HasSimulationAuthority)
            {
                return;
            }

            EnemyActorFactory.Create(entry.enemyType, spawnPoint.transform.position, fallbackEnemyPrefab);
        }

        /// <summary>
        /// 指定した種類をActorとして1体湧かせる(テスト・デバッグ用)。群衆モードの種類でもActorになる。
        /// 湧く場所は湧き位置のどれか(無ければこのオブジェクトの位置)。ネット対戦のClientでは何もしない(nullを返す)。
        /// </summary>
        public EnemyController SpawnActorForTest(EnemyTypeDefinition type)
        {
            if (type == null || !FortressNet.HasSimulationAuthority)
            {
                return null;
            }

            if (_spawnPointsById == null)
            {
                RefreshSpawnPoints();
            }

            var position = transform.position;
            if (_spawnPointsById != null && _spawnPointsById.Count > 0)
            {
                var points = _spawnPointsById.Values.ToArray();
                position = points[Random.Range(0, points.Length)].transform.position;
            }

            return EnemyActorFactory.Create(type, position, fallbackEnemyPrefab);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            foreach (var sp in GetComponentsInChildren<EnemySpawnPoint>())
            {
                if (sp != null)
                {
                    Gizmos.DrawWireCube(sp.transform.position, Vector3.one * 0.5f);
                }
            }
        }
    }
}
