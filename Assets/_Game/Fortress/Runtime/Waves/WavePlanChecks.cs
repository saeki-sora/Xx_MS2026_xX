using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    public enum WavePlanIssueLevel
    {
        Info = 0,
        Warning = 1,

        /// <summary>このままだと遊べない（ウェーブが終わらないなど）。</summary>
        Error = 2
    }

    /// <summary>計画の気になる所1件。</summary>
    public readonly struct WavePlanIssue
    {
        public WavePlanIssue(int wave, WavePlanIssueLevel level, string message)
        {
            Wave = wave;
            Level = level;
            Message = message;
        }

        /// <summary>どのウェーブか（0 から。計画全体なら −1）。</summary>
        public int Wave { get; }

        public WavePlanIssueLevel Level { get; }

        public string Message { get; }
    }

    /// <summary>
    /// ウェーブの計画の見積もりと点検（Unity に頼らない計算。テストあり）。要塞デザイナーの表示に使う。
    /// </summary>
    public static class WavePlanChecks
    {
        /// <summary>くり返しなしのとき、出る数が目標のこの倍率より少ないと注意を出す（コアに着いた敵は撃破数に入らないため）。</summary>
        public const float SafeMargin = 1.15f;

        /// <summary>1周で出る数。</summary>
        public static int CycleEnemies(WaveDefinition wave) => wave == null ? 0 : EnemyWaveStats.TotalEnemies(wave.spawnEntries);

        /// <summary>1周の長さ（最後の1体が出る時刻）。</summary>
        public static float CycleSeconds(WaveDefinition wave) => wave == null ? 0f : EnemyWaveStats.LastSpawnTime(wave.spawnEntries);

        /// <summary>1秒あたりに出るおよその数（くり返しの待ちも入れる）。出し方が一瞬なら 1周の数そのもの。</summary>
        public static float EnemiesPerSecond(WaveDefinition wave)
        {
            var total = CycleEnemies(wave);
            if (total <= 0)
            {
                return 0f;
            }

            var period = CycleSeconds(wave) + (wave.repeatUntilCleared ? wave.repeatGapSeconds : 0f);
            return period > 0.01f ? total / period : total;
        }

        /// <summary>
        /// 目標の数が出そろうまでのおよその秒数（全部倒せたとしての最短）。くり返しなしで足りなければ −1。
        /// </summary>
        public static float SecondsUntilTargetSpawned(WaveDefinition wave)
        {
            if (wave == null || wave.spawnEntries == null)
            {
                return -1f;
            }

            var total = CycleEnemies(wave);
            var target = Mathf.Max(1, wave.targetKills);
            if (total <= 0 || (!wave.repeatUntilCleared && total < target))
            {
                return -1f;
            }

            var cycles = (target - 1) / total; // 何周を丸ごと使うか
            var remaining = target - cycles * total;
            var period = Mathf.Max(WaveSpawnClock.MinLoopPeriod, CycleSeconds(wave) + wave.repeatGapSeconds);
            return cycles * period + TimeOfNth(wave.spawnEntries, remaining);
        }

        // 1周の中で n 体目が出る時刻。
        private static float TimeOfNth(IReadOnlyList<EnemySpawnEntry> entries, int n)
        {
            var times = new List<float>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                var interval = EnemyWaveStats.Interval(entry);
                for (var i = 0; i < entry.count; i++)
                {
                    times.Add(EnemyWaveStats.StartTime(entry) + i * interval);
                }
            }

            if (times.Count == 0)
            {
                return 0f;
            }

            times.Sort();
            return times[Mathf.Clamp(n - 1, 0, times.Count - 1)];
        }

        /// <summary>計画を点検する。knownSpawnIds を渡すと、シーンに無い湧き位置も調べる。</summary>
        public static List<WavePlanIssue> Check(WavePlan plan, ICollection<string> knownSpawnIds = null)
        {
            var issues = new List<WavePlanIssue>();
            if (plan == null)
            {
                return issues;
            }

            issues.AddRange(CheckWaves(plan.waves, knownSpawnIds));
            if (plan.timeLimitSeconds > 0f && plan.clockRunsDuringBreaks)
            {
                var fixedTime = FixedSeconds(plan);
                if (fixedTime >= plan.timeLimitSeconds * 0.8f)
                {
                    issues.Add(new WavePlanIssue(-1, WavePlanIssueLevel.Warning,
                        $"発表・撤退・合間だけで {fixedTime:0}秒あり、制限時間 {plan.timeLimitSeconds:0}秒のほとんどを使います（戦う時間が残りません）。"));
                }
            }

            return issues;
        }

        /// <summary>ウェーブの並びを点検する（計画全体の設定は見ない）。</summary>
        public static List<WavePlanIssue> CheckWaves(IReadOnlyList<WaveDefinition> waves, ICollection<string> knownSpawnIds = null)
        {
            var issues = new List<WavePlanIssue>();
            if (waves == null || waves.Count == 0)
            {
                issues.Add(new WavePlanIssue(-1, WavePlanIssueLevel.Error, "ウェーブが1つもありません。"));
                return issues;
            }

            for (var i = 0; i < waves.Count; i++)
            {
                CheckWave(waves, i, knownSpawnIds, issues);
            }

            return issues;
        }

        /// <summary>戦っている時間以外の、決まった長さの合計（発表・撤退の最短・合間）。</summary>
        public static float FixedSeconds(WavePlan plan)
        {
            if (plan == null)
            {
                return 0f;
            }

            var total = plan.announceSeconds * plan.Count;
            for (var i = 0; i < plan.Count - 1; i++)
            {
                var wave = plan.waves[i];
                total += plan.clearBannerSeconds;
                total += wave?.intervalEvent != null ? wave.intervalEvent.DurationSeconds : wave?.restSeconds ?? 0f;
            }

            return total;
        }

        private static void CheckWave(IReadOnlyList<WaveDefinition> waves, int index, ICollection<string> knownSpawnIds, List<WavePlanIssue> issues)
        {
            var wave = waves[index];
            if (wave == null)
            {
                issues.Add(new WavePlanIssue(index, WavePlanIssueLevel.Error, "中身がありません。"));
                return;
            }

            var entries = wave.spawnEntries;
            var total = CycleEnemies(wave);
            var target = Mathf.Max(1, wave.targetKills);
            if (entries == null || entries.Count == 0 || total == 0)
            {
                issues.Add(new WavePlanIssue(index, WavePlanIssueLevel.Error, "湧きの行がありません。このウェーブは終わりません。"));
                return;
            }

            if (!wave.repeatUntilCleared)
            {
                if (total < target)
                {
                    issues.Add(new WavePlanIssue(index, WavePlanIssueLevel.Error,
                        $"出る数 {total}体 が目標 {target}体 より少ないので、このウェーブは終わりません。「目標までくり返す」を ON にするか、数を増やしてください。"));
                }
                else if (total < target * SafeMargin)
                {
                    issues.Add(new WavePlanIssue(index, WavePlanIssueLevel.Warning,
                        $"出る数 {total}体 が目標 {target}体 ぎりぎりです。コアに着いた敵は撃破数に入らないので、終わらないことがあります。"));
                }
            }

            var noType = 0;
            var missing = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (entry == null)
                {
                    continue;
                }

                if (entry.enemyType == null)
                {
                    noType++;
                }

                if (knownSpawnIds != null && (string.IsNullOrEmpty(entry.spawnPointId) || !knownSpawnIds.Contains(entry.spawnPointId)))
                {
                    missing.Add(string.IsNullOrEmpty(entry.spawnPointId) ? "（未設定）" : entry.spawnPointId);
                }
            }

            if (noType > 0)
            {
                issues.Add(new WavePlanIssue(index, WavePlanIssueLevel.Warning, $"敵の種類が未設定の行が {noType}行 あります（湧きません）。"));
            }

            if (missing.Count > 0)
            {
                issues.Add(new WavePlanIssue(index, WavePlanIssueLevel.Warning, $"シーンに無い湧き位置を使っています: {string.Join(", ", missing)}（その行は湧きません）。"));
            }

            var last = index == waves.Count - 1;
            if (last && wave.intervalEvent != null)
            {
                issues.Add(new WavePlanIssue(index, WavePlanIssueLevel.Info, "最終ウェーブの後の合間のイベントは使われません（目標を倒したらクリア）。"));
            }
        }
    }
}
