using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>制限時間が来たときの扱い（2026-10-24 ユーザー: 今はゲームオーバー、後で変える可能性が大きい）。</summary>
    public enum WaveTimeUpRule
    {
        /// <summary>ゲームオーバー（リザルトには「どのウェーブまで行けたか」も残る）。</summary>
        GameOver = 0,

        /// <summary>クリア扱いで終わる（「どのウェーブまで行けたか」を競う遊び方用）。</summary>
        ClearWithReachedWave = 1
    }

    /// <summary>1ウェーブ分の決まりと湧きの行。</summary>
    [Serializable]
    public sealed class WaveDefinition
    {
        [Tooltip("画面に出す名前。空なら「WAVE n」。")]
        public string label = "";

        [Tooltip("発表のときに添える一言（任意）。例: 「ボス出現！」")]
        public string announceText = "";

        [Tooltip("このウェーブで、この数だけ倒したら次へ進む（最後のウェーブならクリア）。")]
        [Min(1)]
        public int targetKills = 30;

        [Tooltip("湧きの行。ウェーブが始まってからの秒数で出す。")]
        public List<EnemySpawnEntry> spawnEntries = new List<EnemySpawnEntry>();

        [Tooltip("ON: 最後の1体を出し終えたら、目標に届くまで最初からくり返す。OFF: 1回だけ（出る数が目標より少ないと終わらないので注意）。")]
        public bool repeatUntilCleared = true;

        [Tooltip("くり返すとき、最後の1体が出てから次の周までの秒数。")]
        [Min(0f)]
        public float repeatGapSeconds = 3f;

        [Tooltip("このウェーブの後の合間に行うイベント（歯みがきタイムなど）。空なら「休むだけ」。最後のウェーブでは使わない。")]
        public WaveIntervalEvent intervalEvent;

        [Tooltip("合間のイベントが空のときに休む秒数（イベントがあるときは、イベントの長さ）。")]
        [Min(0f)]
        public float restSeconds = 3f;

        public WaveDefinition Clone()
        {
            var copy = (WaveDefinition)MemberwiseClone();
            copy.spawnEntries = new List<EnemySpawnEntry>();
            foreach (var entry in spawnEntries)
            {
                if (entry != null)
                {
                    copy.spawnEntries.Add(entry.Clone());
                }
            }

            return copy;
        }

        /// <summary>画面に出す名前（空なら「WAVE n」。n は 1 から）。</summary>
        public string DisplayLabel(int number) => string.IsNullOrWhiteSpace(label) ? $"WAVE {number}" : label;
    }

    /// <summary>
    /// ステージ1つ分の「ウェーブの計画」（2026-10-24）。ウェーブを順に進め、最後のウェーブの目標を倒したらクリア。
    /// 試合全体に制限時間があり、「時間内にどのウェーブまで行けるか」を遊ぶ。
    /// ステージのシーンの <see cref="EnemySpawnDirector.wavePlan"/> に入れると使われる（空なら今までどおり時間割1本＋撃破数でクリア）。
    /// 1ステージ1ファイル（別のステージと共有しない。ステージを写すときは計画も写す）。
    /// </summary>
    [CreateAssetMenu(menuName = "Fortress/Wave Plan", fileName = "New WavePlan")]
    public sealed class WavePlan : ScriptableObject
    {
        public const int DefaultWaveCount = 5;

        [TextArea(1, 4)]
        [Tooltip("メモ（ゲームでは使わない）。")]
        public string memo = "";

        [Tooltip("ウェーブの並び。最後の1本が最終ウェーブ。")]
        public List<WaveDefinition> waves = new List<WaveDefinition>();

        [Header("試合全体")]
        [Tooltip("試合全体の制限時間（秒）。0 なら時間切れなし。")]
        [Min(0f)]
        public float timeLimitSeconds = 300f;

        [Tooltip("制限時間が来たときの扱い。")]
        public WaveTimeUpRule timeUpRule = WaveTimeUpRule.GameOver;

        [Tooltip("ON: 発表・撤退・合間の間も時間が減る。OFF: 戦っている間だけ減る（合間の間に時間切れにならない）。")]
        public bool clockRunsDuringBreaks;

        [Header("始まり")]
        [Tooltip("全員の準備（読み込み・幕が開く）がそろってから、最初の発表までの秒数。")]
        [Min(0f)]
        public float startDelaySeconds = 0.8f;

        [Tooltip("全員の準備を待つ最長の秒数（遅いPCがいても、これを過ぎたら始める）。")]
        [Min(0f)]
        public float maxWaitForPlayersSeconds = 10f;

        [Header("発表（WAVE n！目標撃破数 m！）")]
        [Tooltip("発表の長さ（秒）。最後の「カウントダウンの秒数」は 3・2・1 を出す。この間に敵のルートを赤い線で見せる。")]
        [Min(0.5f)]
        public float announceSeconds = 4f;

        [Tooltip("発表の最後に 3・2・1 を出す秒数（0 なら出さない）。")]
        [Min(0f)]
        public float countdownSeconds = 2f;

        [Tooltip("ON: 発表の間、次のウェーブの敵のルートを赤い線で見せる。")]
        public bool showRoutes = true;

        [Tooltip("表示のタイミング（GO! の秒数・赤い線の出方・初めての人向けの表示など）。全部のステージで同じファイルを使ってよい。空なら既定の値。")]
        public WaveDisplaySettings display;

        [Header("ウェーブクリア → 撤退")]
        [Tooltip("「WAVE CLEAR!」を見せる最短の秒数（敵がすぐいなくなっても、この間は次へ進まない）。")]
        [Min(0f)]
        public float clearBannerSeconds = 1.6f;

        [Tooltip("残った敵が全員の画面から逃げ切るのを待つ最長の秒数。過ぎたら残りは消える。")]
        [Min(0.5f)]
        public float retreatMaxSeconds = 5f;

        [Tooltip("逃げるときの速さの倍率。")]
        [Min(0.1f)]
        public float retreatSpeedMultiplier = 2.4f;

        [Header("演出（D-Drive の番号札。各PCで鳴らす。空なら鳴らさない）")]
        [Tooltip("発表が始まったとき（最終ウェーブ以外）。")]
        public FortressEffect onAnnounce = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("最終ウェーブの発表が始まったとき（空なら上と同じ）。")]
        public FortressEffect onFinalAnnounce = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("3・2・1 の1つごと。")]
        public FortressEffect onCountdownTick = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("ウェーブが始まったとき（GO!）。")]
        public FortressEffect onWaveStart = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("目標を倒してウェーブをクリアしたとき。")]
        public FortressEffect onWaveClear = new FortressEffect { tintWithPlayerColor = false };

        [Tooltip("残り時間が少なくなったとき（10秒前から1秒ごと）。")]
        public FortressEffect onTimeWarning = new FortressEffect { tintWithPlayerColor = false };

        public int Count => waves?.Count ?? 0;

        public WaveDefinition Get(int index) => waves != null && index >= 0 && index < waves.Count ? waves[index] : null;

        /// <summary>最後のウェーブの番号（0 から）。</summary>
        public int FinalIndex => Count - 1;

        /// <summary>全部のウェーブの湧きの行。</summary>
        public IEnumerable<EnemySpawnEntry> AllEntries()
        {
            if (waves == null)
            {
                yield break;
            }

            foreach (var wave in waves)
            {
                if (wave?.spawnEntries == null)
                {
                    continue;
                }

                foreach (var entry in wave.spawnEntries)
                {
                    if (entry != null)
                    {
                        yield return entry;
                    }
                }
            }
        }

        /// <summary>全部のウェーブの目標の合計（＝最後まで行くのに倒す数）。</summary>
        public int TotalTargetKills()
        {
            var total = 0;
            for (var i = 0; i < Count; i++)
            {
                total += Mathf.Max(1, waves[i]?.targetKills ?? 1);
            }

            return total;
        }

        /// <summary>
        /// 中身の指紋（Host と Client で違う計画・違うビルドを使っていないかの確認用）。ウェーブの数・目標・制限時間から作る。
        /// </summary>
        public uint ComputeHash()
        {
            unchecked
            {
                var hash = 2166136261u;
                void Mix(int value)
                {
                    hash = (hash ^ (uint)value) * 16777619u;
                }

                Mix(Count);
                for (var i = 0; i < Count; i++)
                {
                    Mix(waves[i]?.targetKills ?? 0);
                    Mix(waves[i]?.spawnEntries?.Count ?? 0);
                }

                Mix(Mathf.RoundToInt(timeLimitSeconds * 10f));
                Mix((int)timeUpRule);
                return hash;
            }
        }

        private void Reset()
        {
            waves = new List<WaveDefinition>();
            for (var i = 0; i < DefaultWaveCount; i++)
            {
                waves.Add(new WaveDefinition { targetKills = 30 + i * 15 });
            }
        }
    }
}
