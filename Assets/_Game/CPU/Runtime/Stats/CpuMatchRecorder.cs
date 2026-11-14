using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MS2026.Fortress;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MS2026.Cpu
{
    /// <summary>
    /// 試合とウェーブを区切って、CPU の記録（<see cref="CpuStats"/>）を閉じ、結果の表（CSV）に書く係。<see cref="CpuDirector"/> が持つ。
    /// ・区切り: ウェーブが始まった（<see cref="FortressWaves.WaveStarted"/>）→ クリア（<see cref="FortressWaves.WaveCleared"/>）／試合が終わった（<see cref="FortressMatch.Ended"/>）
    /// ・1行 = 1ウェーブ × 1人（人の席は撃破数だけ）。試合が終わると「試合の合計」の行も足す
    /// ・表は自動プレイ1回（または Play 1回）につき1ファイル。試合が終わるたびに書き直す（途中で止めても、そこまでは残る）
    /// ・置き場所: エディタはプロジェクトの Logs/CPU/、ビルドは persistentDataPath/CPU/
    /// 結果を決める PC（Host・オフライン）だけで動く。
    /// </summary>
    public sealed class CpuMatchRecorder : IDisposable
    {
        private const int PlayerCount = CpuSeatRules.PlayerCount;

        public static readonly string[] Header =
        {
            "記録した時刻", "試合", "ステージ", "強さ", "乱数の種", "ウェーブ", "区切り", "プレイヤー", "操作", "撃破", "秒数", "コアHP%"
        };

        private readonly Func<int, CpuPlayer> _getCpu;
        private readonly List<CpuResultRow> _rows = new List<CpuResultRow>();
        private readonly int[] _waveKillBase = new int[PlayerCount];
        private readonly CpuCounters[] _matchTotals = { new CpuCounters(), new CpuCounters(), new CpuCounters(), new CpuCounters() };
        private readonly bool[] _wasCpu = new bool[PlayerCount];

        private bool _subscribed;
        private bool _inMatch;
        private bool _inWave;
        private int _waveIndex = -1;
        private float _waveStart;
        private float _matchStart;
        private string _stage = string.Empty;
        private string _path;

        public CpuMatchRecorder(Func<int, CpuPlayer> getCpu)
        {
            _getCpu = getCpu;
        }

        /// <summary>表に書くか（書かなくても区切り・記録の閉じ方は同じ。学びは記録を使うので）。</summary>
        public bool WriteCsv { get; set; }

        /// <summary>強さ・乱数の種（表に書くだけ）。</summary>
        public CpuLevel Level { get; set; }

        public int Seed { get; set; }

        /// <summary>この Play（自動プレイ）で何試合目か（1 から。試合の外は今までの数）。</summary>
        public int MatchNumber { get; private set; }

        /// <summary>試合中か。</summary>
        public bool InMatch => _inMatch;

        /// <summary>今の試合のゲームの時間での秒数。</summary>
        public float MatchSeconds => _inMatch ? Time.time - _matchStart : 0f;

        /// <summary>書いた表のファイル（まだ書いていなければ null）。</summary>
        public string FilePath => _path;

        /// <summary>試合が終わって記録を閉じたとき（自動プレイが次の試合へ進むのに使う）。引数は結果（打ち切りなら null）。</summary>
        public event Action<MatchResult?> MatchClosed;

        public void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            _subscribed = true;
            FortressWaves.WaveStarted += OnWaveStarted;
            FortressWaves.WaveCleared += OnWaveCleared;
            FortressMatch.Ended += OnMatchEnded;
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                FortressWaves.WaveStarted -= OnWaveStarted;
                FortressWaves.WaveCleared -= OnWaveCleared;
                FortressMatch.Ended -= OnMatchEnded;
                _subscribed = false;
            }

            Flush();
        }

        /// <summary>毎フレーム（試合の始まりを見つける）。</summary>
        public void Tick()
        {
            if (FortressMatch.InMatch && !FortressMatch.HasEnded && !_inMatch)
            {
                BeginMatch();
            }
            else if (!FortressMatch.InMatch && _inMatch)
            {
                // 試合の外に出た（結果が出ないまま、シーンを抜けた）。
                CloseMatch(null, "抜けた");
            }
        }

        /// <summary>試合を打ち切る（自動プレイで、決めた時間を過ぎても終わらないとき）。</summary>
        public void Abort(string reason)
        {
            if (_inMatch)
            {
                CloseMatch(null, reason);
            }
        }

        /// <summary>CPU が新しく席に着いたとき（その人の記録を今のウェーブから始める）。</summary>
        public void OnCpuStarted(CpuPlayer cpu)
        {
            if (cpu != null && _inWave)
            {
                cpu.Context.Stats.BeginWave(_waveIndex);
            }
        }

        private void BeginMatch()
        {
            _inMatch = true;
            MatchNumber++;
            _matchStart = Time.time;
            _stage = SceneManager.GetActiveScene().name;
            for (var p = 0; p < PlayerCount; p++)
            {
                _matchTotals[p].Clear();
                _waveKillBase[p] = EnemyKillCounter.ByPlayer(p);
                _wasCpu[p] = false;
            }

            // ウェーブの計画が無い試合は、試合全体を1つの区切りにする。
            if (!FortressWaves.IsActive)
            {
                BeginWave(-1);
            }
        }

        private void OnWaveStarted(int index)
        {
            if (!_inMatch)
            {
                BeginMatch();
            }

            if (_inWave)
            {
                CloseWave(false);
            }

            BeginWave(index);
        }

        private void OnWaveCleared(int index)
        {
            if (_inWave)
            {
                CloseWave(true);
            }
        }

        private void OnMatchEnded(MatchResult result)
        {
            if (_inMatch)
            {
                CloseMatch(result, null);
            }
        }

        private void BeginWave(int index)
        {
            _inWave = true;
            _waveIndex = index;
            _waveStart = Time.time;
            for (var p = 0; p < PlayerCount; p++)
            {
                _waveKillBase[p] = EnemyKillCounter.ByPlayer(p);
                _getCpu(p)?.Context.Stats.BeginWave(index);
            }
        }

        private void CloseWave(bool cleared)
        {
            _inWave = false;
            var seconds = Time.time - _waveStart;
            var core = CoreHealthPercent();
            for (var p = 0; p < PlayerCount; p++)
            {
                var kills = EnemyKillCounter.ByPlayer(p) - _waveKillBase[p];
                var cpu = _getCpu(p);
                var row = NewRow(_waveIndex >= 0 ? (_waveIndex + 1).ToString() : "なし", cleared ? "クリア" : "途中", p, cpu != null,
                    kills, seconds, core);
                if (cpu != null)
                {
                    _wasCpu[p] = true;
                    var stats = cpu.Context.Stats.EndWave(cleared);
                    row.Counters.CopyFrom(stats.Counters);
                    CpuCsv.Accumulate(_matchTotals[p], stats.Counters);
                }

                _rows.Add(row);
            }
        }

        private void CloseMatch(MatchResult? result, string reason)
        {
            if (_inWave)
            {
                CloseWave(false);
            }

            _inMatch = false;
            var seconds = Time.time - _matchStart;
            var core = result.HasValue ? result.Value.coreHealth01 * 100f : CoreHealthPercent();
            var label = reason ?? Outcome(result);
            var reached = result.HasValue ? result.Value.reachedWave.ToString() : (_waveIndex >= 0 ? (_waveIndex + 1).ToString() : "なし");
            for (var p = 0; p < PlayerCount; p++)
            {
                var kills = result.HasValue && result.Value.kills != null && p < result.Value.kills.Length
                    ? result.Value.kills[p]
                    : EnemyKillCounter.ByPlayer(p);
                var row = NewRow(reached, "試合の合計:" + label, p, _wasCpu[p] || _getCpu(p) != null, kills, seconds, core);
                row.Counters.CopyFrom(_matchTotals[p]);
                _rows.Add(row);
            }

            Flush();
            MatchClosed?.Invoke(result);
        }

        private CpuResultRow NewRow(string wave, string part, int player, bool isCpu, int kills, float seconds, float corePercent)
        {
            var row = new CpuResultRow();
            row.Fixed.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            row.Fixed.Add(MatchNumber.ToString());
            row.Fixed.Add(_stage);
            row.Fixed.Add(CpuLevels.Label(Level));
            row.Fixed.Add(Seed.ToString());
            row.Fixed.Add(wave);
            row.Fixed.Add(part);
            row.Fixed.Add("P" + (player + 1));
            row.Fixed.Add(isCpu ? "CPU" : "人");
            row.Fixed.Add(kills.ToString());
            row.Fixed.Add(CpuCsv.Number(seconds));
            row.Fixed.Add(corePercent >= 0f ? CpuCsv.Number(corePercent) : string.Empty);
            return row;
        }

        private static string Outcome(MatchResult? result)
        {
            if (!result.HasValue)
            {
                return "不明";
            }

            var outcome = result.Value.outcome == MatchOutcome.Clear ? "クリア"
                : result.Value.outcome == MatchOutcome.GameOver ? "ゲームオーバー"
                : result.Value.outcome.ToString();
            switch (result.Value.reason)
            {
                case MatchEndReason.CoreDestroyed: return outcome + "（コアが壊れた）";
                case MatchEndReason.TimeUp: return outcome + "（時間切れ）";
                default: return outcome;
            }
        }

        private static float CoreHealthPercent()
        {
            var core = UnityEngine.Object.FindFirstObjectByType<CoreCrystalController>();
            return core != null && core.maxHealth > 0f ? core.CurrentHealth / core.maxHealth * 100f : -1f;
        }

        private void Flush()
        {
            if (!WriteCsv || _rows.Count == 0)
            {
                return;
            }

            try
            {
                if (_path == null)
                {
                    var folder = Application.isEditor
                        ? Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Logs", "CPU")
                        : Path.Combine(Application.persistentDataPath, "CPU");
                    Directory.CreateDirectory(folder);
                    _path = Path.Combine(folder, $"cpu_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
                    Debug.Log($"[CPU] 結果の表に書きます: {_path}");
                }

                File.WriteAllText(_path, CpuCsv.Build(Header, _rows), new UTF8Encoding(true));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CPU] 結果の表を書けませんでした: {e.Message}");
            }
        }
    }
}
