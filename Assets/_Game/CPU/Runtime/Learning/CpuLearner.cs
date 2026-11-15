using System;
using System.Collections.Generic;
using MS2026.Fortress;

namespace MS2026.Cpu
{
    /// <summary>
    /// CPU 1人の学び。ウェーブが終わって記録が閉じたら（<see cref="CpuStats.WaveFinished"/>）、登録された分野を全部振り返り、
    /// 苦手な分野ほど大きく慣れ（<see cref="CpuFamiliarity"/>）を上げる（伸び方は <see cref="CpuLearningMath"/>）。
    /// 慣れが変わると、次のフレームで上手さが作り直される（<see cref="CpuPlayer.RefreshSkill"/>）。
    /// 振り返った「苦手さ」と「伸び」は、閉じたウェーブの記録に足す（結果の表の列「苦手最大:（分野）」「伸び:（分野）」）。
    /// 試合ごとに CPU を作り直すので、学んだことは試合をまたがない。
    /// </summary>
    public sealed class CpuLearner : IDisposable
    {
        public const string NeedPrefix = "苦手最大:";
        public const string GrowthPrefix = "伸び:";

        private readonly CpuContext _context;
        private readonly Func<CpuLearningTuning> _tuning;
        private readonly Func<int> _planWaveCount;
        private readonly Func<IEnumerable<CpuSkillArea>> _areas;
        private readonly Func<string, CpuWaveStats, float> _review;

        /// <param name="planWaveCount">ステージのウェーブの数（省略 = <see cref="FortressWaves.WaveCount"/>）。</param>
        /// <param name="areas">振り返る分野（省略 = 登録された分野 <see cref="CpuPlugins.SkillAreas"/>）。</param>
        /// <param name="review">分野の苦手さ（省略 = 登録された振り返り <see cref="CpuPlugins.ReviewArea"/>）。</param>
        public CpuLearner(CpuContext context, Func<CpuLearningTuning> tuning, Func<int> planWaveCount = null,
            Func<IEnumerable<CpuSkillArea>> areas = null, Func<string, CpuWaveStats, float> review = null)
        {
            _context = context;
            _tuning = tuning ?? (() => CpuLearningTuning.Default);
            _planWaveCount = planWaveCount ?? (() => FortressWaves.WaveCount);
            _areas = areas ?? (() => CpuPlugins.SkillAreas);
            _review = review ?? CpuPlugins.ReviewArea;
            _context.Stats.WaveFinished += OnWaveFinished;
        }

        /// <summary>今までに振り返った回数（ウェーブの数）。</summary>
        public int Reviews { get; private set; }

        public void Dispose()
        {
            _context.Stats.WaveFinished -= OnWaveFinished;
        }

        /// <summary>そのウェーブを振り返って慣れを上げる（ふつうはウェーブが閉じたときに自動で呼ばれる）。</summary>
        public void Review(CpuWaveStats wave)
        {
            var tuning = _tuning();
            if (!tuning.enabled || wave == null)
            {
                return;
            }

            var waveCount = CpuLearningMath.WaveCount(tuning, _planWaveCount());
            foreach (var area in _areas())
            {
                var need = _review(area.Id, wave);
                var growth = CpuLearningMath.Growth(need, waveCount, tuning);
                var before = _context.Familiarity.Get(area.Id);
                _context.Familiarity.Add(area.Id, growth);
                wave.Counters.Max(NeedPrefix + area.Label, need);
                wave.Counters.Set(GrowthPrefix + area.Label, _context.Familiarity.Get(area.Id) - before);
            }

            Reviews++;
        }

        private void OnWaveFinished(CpuWaveStats wave) => Review(wave);
    }
}
