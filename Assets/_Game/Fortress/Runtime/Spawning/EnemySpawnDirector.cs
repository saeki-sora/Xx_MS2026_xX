using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// EnemyWaveConfigを再生し、指定した位置・数・タイミングで敵を生成する。
    /// 「敵の沸く位置や数、タイミング」を全てデータ(EnemyWaveConfig)側に追い出しているため、
    /// このクラス自体を編集しなくても敵配置を自由に調整できる。
    /// </summary>
    public sealed class EnemySpawnDirector : MonoBehaviour
    {
        public EnemyWaveConfig wave;

        [Tooltip("未設定なら子オブジェクトからEnemySpawnPointを自動収集する。")]
        public EnemySpawnPoint[] spawnPoints;

        [Tooltip("EnemyTypeDefinition.visualPrefabが未設定な場合に使う土台プレファブ。未設定なら空のGameObjectを生成する。")]
        public GameObject fallbackEnemyPrefab;

        [Tooltip("Play開始時に自動でウェーブを再生するか。オフの場合はInspectorや要塞デザイナーの「ウェーブ開始」ボタンで手動再生する。")]
        public bool autoStartOnPlay = true;

        public bool IsPlaying { get; private set; }
        public float ElapsedTime { get; private set; }

        private readonly List<EnemySpawnEntry> _pendingEntries = new List<EnemySpawnEntry>();
        private Dictionary<string, EnemySpawnPoint> _spawnPointsById;
        private bool _swarmMissingLogged;

        private void Awake()
        {
            RefreshSpawnPoints();
        }

        private void Start()
        {
            if (autoStartOnPlay)
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

        /// <summary>ウェーブの再生を最初から開始する。</summary>
        public void StartWave()
        {
            if (wave == null)
            {
                Debug.LogWarning("[EnemySpawnDirector] waveが設定されていません。", this);
                return;
            }

            if (_spawnPointsById == null)
            {
                RefreshSpawnPoints();
            }

            _pendingEntries.Clear();
            _pendingEntries.AddRange(wave.spawnEntries.OrderBy(e => e.triggerTime));
            ElapsedTime = 0f;
            IsPlaying = true;
        }

        public void StopWave()
        {
            IsPlaying = false;
            CancelInvoke(nameof(StartWave));
            _pendingEntries.Clear();
        }

        private void Update()
        {
            if (!IsPlaying)
            {
                return;
            }

            ElapsedTime += Time.deltaTime;

            while (_pendingEntries.Count > 0 && _pendingEntries[0].triggerTime <= ElapsedTime)
            {
                var entry = _pendingEntries[0];
                _pendingEntries.RemoveAt(0);
                StartCoroutine(SpawnEntryRoutine(entry));
            }

            if (_pendingEntries.Count == 0)
            {
                IsPlaying = false;

                if (wave.loop)
                {
                    Invoke(nameof(StartWave), wave.loopInterval);
                }
            }
        }

        private IEnumerator SpawnEntryRoutine(EnemySpawnEntry entry)
        {
            for (var i = 0; i < entry.count; i++)
            {
                SpawnOne(entry);

                if (i < entry.count - 1 && entry.intervalBetweenSpawns > 0f)
                {
                    yield return new WaitForSeconds(entry.intervalBetweenSpawns);
                }
            }
        }

        private void SpawnOne(EnemySpawnEntry entry)
        {
            if (_spawnPointsById == null || !_spawnPointsById.TryGetValue(entry.spawnPointId, out var spawnPoint))
            {
                Debug.LogWarning($"[EnemySpawnDirector] spawnPointId '{entry.spawnPointId}' が見つかりません。", this);
                return;
            }

            if (entry.enemyType == null)
            {
                Debug.LogWarning("[EnemySpawnDirector] enemyTypeが未設定のエントリがあります。", this);
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

            var prefab = entry.enemyType.visualPrefab != null ? entry.enemyType.visualPrefab : fallbackEnemyPrefab;
            var instance = prefab != null
                ? Instantiate(prefab, spawnPoint.transform.position, Quaternion.identity)
                : new GameObject(entry.enemyType.displayName);

            instance.transform.position = spawnPoint.transform.position;

            var enemy = instance.GetComponent<EnemyController>();
            if (enemy == null)
            {
                enemy = instance.AddComponent<EnemyController>();
            }

            enemy.definition = entry.enemyType;
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
