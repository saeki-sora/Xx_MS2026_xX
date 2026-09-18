using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>敵の湧き1件分。位置・数・タイミングを個別に調整できる。</summary>
    [Serializable]
    public sealed class EnemySpawnEntry
    {
        [Tooltip("ウェーブ開始からの経過秒数でこのエントリを発生させる。")]
        [Min(0f)]
        public float triggerTime;

        [SpawnPointId]
        [Tooltip("シーン内のEnemySpawnPointのID。どこから湧くかを指定する。")]
        public string spawnPointId;

        [Tooltip("湧かせる敵の種類。")]
        public EnemyTypeDefinition enemyType;

        [Tooltip("同時に何体湧かせるか。")]
        [Min(1)]
        public int count = 1;

        [Tooltip("countが2以上のとき、1体ずつ生成する間隔(秒)。0なら同時に生成する。")]
        [Min(0f)]
        public float intervalBetweenSpawns = 0.3f;
    }

    /// <summary>
    /// 1回の防衛ウェーブの敵配置データ。エントリの並び順は問わず、Directorが
    /// triggerTime順に自動で並べ替えて再生する。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Enemy Wave Config", fileName = "New EnemyWaveConfig")]
    public sealed class EnemyWaveConfig : ScriptableObject
    {
        public string waveName = "Wave 1";

        public List<EnemySpawnEntry> spawnEntries = new List<EnemySpawnEntry>();

        [Tooltip("最後のエントリの発生後、最初からループして再生し続けるか。")]
        public bool loop;

        [Tooltip("ループする場合、最後のエントリからの待機時間(秒)。")]
        [Min(0f)]
        public float loopInterval = 5f;
    }
}
