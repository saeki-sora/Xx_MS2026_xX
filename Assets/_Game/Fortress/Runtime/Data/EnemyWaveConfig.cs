using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>「数」が2以上の行で、1体ずつの出し方をどう決めるか。</summary>
    public enum EnemySpawnTiming
    {
        /// <summary>1体ずつの間隔（秒）で決める（今までの決め方）。</summary>
        Interval = 0,

        /// <summary>全部を出し終えるまでの秒数で決める。間隔は「秒数 ÷ (数−1)」で自動。</summary>
        Duration = 1
    }

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

        [Tooltip("countが2以上のとき、1体ずつ生成する間隔(秒)。0なら同時に生成する。「秒数で決める」のときは使わない。")]
        [Min(0f)]
        public float intervalBetweenSpawns = 0.3f;

        [Tooltip("1体ずつの出し方を「間隔」で決めるか「全部を出し終えるまでの秒数」で決めるか。")]
        public EnemySpawnTiming timing = EnemySpawnTiming.Interval;

        [Tooltip("「秒数で決める」のとき、最初の1体から最後の1体までの秒数。間隔は自動（秒数 ÷ (数−1)）。0なら同時に生成する。")]
        [Min(0f)]
        public float durationSeconds;

        /// <summary>実際に使う1体ずつの間隔（秒）。「秒数で決める」なら秒数から計算する。</summary>
        public float EffectiveInterval => EnemyWaveStats.Interval(this);

        /// <summary>同じ中身の別の行（複製用）。</summary>
        public EnemySpawnEntry Clone() => (EnemySpawnEntry)MemberwiseClone();
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

        [Tooltip("最後の1体が出た後、最初からループして再生し続けるか。")]
        public bool loop;

        [Tooltip("ループする場合、最後の1体が出てから次の周までの待機時間(秒)。")]
        [Min(0f)]
        public float loopInterval = 5f;
    }
}
