using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// 湧きの行（時間割）を時間どおりに進める時計（Unity に頼らない計算。テストあり）。
    /// 「この時刻までに何体出ているはずか」で数えるので、間隔がフレームより短くても1フレームに何体でも出せる
    /// （以前は1体ごとに WaitForSeconds で待っていたため、60fps だと1秒に60体までしか出ず、書いた秒数より遅れた）。
    /// くり返すときは「最後の1体が出た時刻 + くり返すまでの秒数」から次の周を始める（以前は最後の行が湧き始めた時刻からで、周が重なっていた）。
    /// 行の中身は <see cref="Begin"/> のときに写す（Play 中に表を変えても、今の周は書き換わらない。次に始めたときから反映）。
    /// </summary>
    public sealed class WaveSpawnClock
    {
        /// <summary>1回の <see cref="Advance"/> で出す数の上限（壊れた設定で止まらないための安全柵）。</summary>
        public const int MaxSpawnsPerAdvance = 10000;

        /// <summary>くり返すときの1周の最短の長さ（秒）。0 秒の周を無限にくり返さないため。</summary>
        public const float MinLoopPeriod = 0.25f;

        private readonly List<EnemySpawnEntry> _entries = new List<EnemySpawnEntry>();
        private readonly List<int> _spawned = new List<int>();
        private float _cycleLength;
        private bool _loop;
        private float _loopGap;

        /// <summary>動いているか（止めたか、くり返さずに全部出し終えたら false）。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>今の周が始まってからの秒数（くり返しの待ち時間の間は、周の長さを越えて増える）。</summary>
        public float CycleTime { get; private set; }

        /// <summary>今が何周目か（0 から）。</summary>
        public int Cycle { get; private set; }

        /// <summary>今までに出した合計の数（全部の周）。</summary>
        public int TotalSpawned { get; private set; }

        /// <summary>1周の長さ（最後の1体が出る時刻）。</summary>
        public float CycleLength => _cycleLength;

        /// <summary>
        /// 行を写して、startTime 秒の時点から始める（startTime より前に湧き始める行は飛ばす）。
        /// </summary>
        public void Begin(IReadOnlyList<EnemySpawnEntry> entries, bool loop, float loopGap, float startTime = 0f)
        {
            _entries.Clear();
            _spawned.Clear();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry != null && entry.count > 0)
                    {
                        _entries.Add(entry.Clone());
                    }
                }
            }

            _cycleLength = EnemyWaveStats.LastSpawnTime(_entries);
            _loop = loop;
            _loopGap = Mathf.Max(0f, loopGap);
            startTime = Mathf.Max(0f, startTime);
            foreach (var entry in _entries)
            {
                // 途中から始めるときは、それより前に湧き始める行を「出し終えた」扱いにする（その周だけ）。
                _spawned.Add(entry.triggerTime < startTime - 1e-4f ? entry.count : 0);
            }

            CycleTime = startTime;
            Cycle = 0;
            TotalSpawned = 0;
            IsRunning = _entries.Count > 0;
        }

        public void Stop()
        {
            IsRunning = false;
        }

        /// <summary>
        /// dt 秒進めて、この間に出す行を output に1体1件で足す（同じ行が何回も入る）。足した数を返す。
        /// </summary>
        public int Advance(float dt, List<EnemySpawnEntry> output)
        {
            if (!IsRunning)
            {
                return 0;
            }

            CycleTime += Mathf.Max(0f, dt);
            var added = 0;
            while (IsRunning && added < MaxSpawnsPerAdvance)
            {
                added += EmitDue(output, MaxSpawnsPerAdvance - added);
                if (!AllSpawned())
                {
                    break;
                }

                if (!_loop)
                {
                    IsRunning = false;
                    break;
                }

                var period = Mathf.Max(MinLoopPeriod, _cycleLength + _loopGap);
                if (CycleTime < period)
                {
                    break;
                }

                // 次の周へ（余った時間は持ち越す。重いフレームで一気に進んでも数がずれない）。
                CycleTime -= period;
                Cycle++;
                for (var i = 0; i < _spawned.Count; i++)
                {
                    _spawned[i] = 0;
                }
            }

            TotalSpawned += added;
            return added;
        }

        private int EmitDue(List<EnemySpawnEntry> output, int budget)
        {
            var added = 0;
            for (var i = 0; i < _entries.Count && added < budget; i++)
            {
                var entry = _entries[i];
                var due = EnemyWaveStats.SpawnedByEntry(entry, CycleTime);
                while (_spawned[i] < due && added < budget)
                {
                    output?.Add(entry);
                    _spawned[i]++;
                    added++;
                }
            }

            return added;
        }

        private bool AllSpawned()
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_spawned[i] < _entries[i].count)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
