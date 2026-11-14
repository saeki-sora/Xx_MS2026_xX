using System;
using System.Collections.Generic;

namespace MS2026.Cpu
{
    /// <summary>
    /// 名前つきの数の集まり（1ウェーブぶん）。キーは足した順に並ぶ（結果の表の列の順になる）。
    /// キーは日本語でよい（表の見出しになる）。単位はキーに書く（例「撃った秒」「オーバーヒート回」）。
    /// </summary>
    public sealed class CpuCounters
    {
        private readonly List<string> _keys = new List<string>();
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>();

        public IReadOnlyList<string> Keys => _keys;

        public float Get(string key) => key != null && _values.TryGetValue(key, out var value) ? value : 0f;

        public bool Has(string key) => key != null && _values.ContainsKey(key);

        public void Add(string key, float amount)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            Touch(key);
            _values[key] += amount;
        }

        public void Set(string key, float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            Touch(key);
            _values[key] = value;
        }

        /// <summary>今までの値より大きければ入れる。</summary>
        public void Max(string key, float value)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var had = _values.TryGetValue(key, out var current);
            Touch(key);
            _values[key] = had ? Math.Max(current, value) : value;
        }

        public void Clear()
        {
            _keys.Clear();
            _values.Clear();
        }

        public void CopyFrom(CpuCounters other)
        {
            Clear();
            if (other == null)
            {
                return;
            }

            foreach (var key in other._keys)
            {
                Set(key, other._values[key]);
            }
        }

        private void Touch(string key)
        {
            if (!_values.ContainsKey(key))
            {
                _keys.Add(key);
                _values[key] = 0f;
            }
        }
    }

    /// <summary>1ウェーブぶんの CPU の記録。</summary>
    public sealed class CpuWaveStats
    {
        /// <summary>ウェーブの番号（0 から。ウェーブの無い試合は -1）。</summary>
        public int WaveIndex;

        /// <summary>このウェーブにいた秒数（ゲームの時間）。</summary>
        public float Seconds;

        /// <summary>ウェーブをクリアして終わったか（false = 試合が途中で終わった）。</summary>
        public bool Cleared;

        public readonly CpuCounters Counters = new CpuCounters();
    }

    /// <summary>
    /// CPU 1人の記録。ウェーブごとに区切る（区切るのは <see cref="CpuMatchRecorder"/>）。
    /// 行動・差し込みは <see cref="Add"/> などで好きな名前の数を足せる（結果の表に列が増える。学び（段7）もこれを読む）。
    /// </summary>
    public sealed class CpuStats
    {
        // 本体が数える名前（表の見出し）。
        public const string FiringSeconds = "撃った秒";
        public const string WhiffSeconds = "空振り秒";
        public const string Overheats = "オーバーヒート回";
        public const string MaxHeat = "熱の最大%";
        public const string BehaviorSecondsPrefix = "行動秒:";

        private readonly List<CpuWaveStats> _finished = new List<CpuWaveStats>();
        private CpuWaveStats _current = new CpuWaveStats { WaveIndex = -1 };

        /// <summary>今のウェーブの記録。</summary>
        public CpuWaveStats Current => _current;

        /// <summary>終わったウェーブの記録（古い順）。</summary>
        public IReadOnlyList<CpuWaveStats> Finished => _finished;

        /// <summary>ウェーブが終わって記録が閉じたとき（学び（段7）がここで振り返る）。</summary>
        public event Action<CpuWaveStats> WaveFinished;

        public void Add(string key, float amount) => _current.Counters.Add(key, amount);
        public void Set(string key, float value) => _current.Counters.Set(key, value);
        public void Max(string key, float value) => _current.Counters.Max(key, value);

        /// <summary>今のウェーブにいた秒数を進める（CpuPlayer が毎フレーム呼ぶ）。</summary>
        public void Tick(float deltaTime) => _current.Seconds += deltaTime;

        /// <summary>新しいウェーブの記録を始める（それまでの数は捨てる）。</summary>
        public void BeginWave(int waveIndex)
        {
            _current = new CpuWaveStats { WaveIndex = waveIndex };
        }

        /// <summary>今のウェーブの記録を閉じて返す（次の記録は同じ番号で続く）。</summary>
        public CpuWaveStats EndWave(bool cleared)
        {
            var done = _current;
            done.Cleared = cleared;
            _finished.Add(done);
            _current = new CpuWaveStats { WaveIndex = done.WaveIndex };
            WaveFinished?.Invoke(done);
            return done;
        }
    }
}
