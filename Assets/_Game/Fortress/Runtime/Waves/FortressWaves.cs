using System;
using System.Collections.Generic;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>
    /// ウェーブの今の様子（全員のPCで同じ値。2026-10-24）。決めるのは Host（オフラインでは自分）で、Client には Host から届く。
    /// HUD・ルート線・ステージの仕掛け・合間のイベントは、ここを読むか、お知らせ（static イベント）を受け取る。
    /// 係（<see cref="FortressWaveRunner"/>）は起動時に自動で作られる（シーンに置く物は無い）。
    /// 計画（<see cref="WavePlan"/>）はステージのシーンの <see cref="EnemySpawnDirector.wavePlan"/>。無ければ何もしない（<see cref="IsActive"/> が false）。
    /// </summary>
    public static class FortressWaves
    {
        /// <summary>Host → 全員: 今の様子（<see cref="FortressWaveRunner"/> の WriteState を参照）。</summary>
        public const string StateMessage = "MS2026.Fortress.Waves";

        /// <summary>Client → Host: 準備ができた（読み込み・幕が開いた）。</summary>
        public const string ReadyMessage = "MS2026.Fortress.Waves.Ready";

        /// <summary>Client → Host: 合間のイベントの入力の数（握った回数など。合間ごとの累計）。</summary>
        public const string InputMessage = "MS2026.Fortress.Waves.Input";

        public const int PlayerCount = 4;

        private static readonly List<Func<bool>> ReadyBlockers = new List<Func<bool>>();
        private static readonly int[] BreakInputs = new int[PlayerCount];

        /// <summary>ウェーブの計画が動いているか（計画のあるステージの試合中。終わった後も結果を見せる間は true）。</summary>
        public static bool IsActive => Phase != WavePhase.None;

        /// <summary>このPCが読み込んだステージの計画（ルート線などに使う。Client も自分のシーンの物）。</summary>
        public static WavePlan Plan { get; internal set; }

        public static WavePhase Phase { get; internal set; }

        public static WavePhase PreviousPhase { get; internal set; }

        /// <summary>今のウェーブの番号（0 から）。合間の間は今クリアしたウェーブ。</summary>
        public static int WaveIndex { get; internal set; }

        /// <summary>今のウェーブの番号（1 から。画面用）。</summary>
        public static int WaveNumber => WaveIndex + 1;

        public static int WaveCount { get; internal set; }

        public static bool IsFinalWave => WaveCount > 0 && WaveIndex >= WaveCount - 1;

        /// <summary>今のウェーブの定義（このPCの計画から。無ければ null）。</summary>
        public static WaveDefinition CurrentWave => Plan != null ? Plan.Get(WaveIndex) : null;

        /// <summary>次に発表されるウェーブの番号（合間の間は次、それ以外は今）。</summary>
        public static int UpcomingWaveIndex => Phase == WavePhase.Break || Phase == WavePhase.Retreat ? WaveIndex + 1 : WaveIndex;

        /// <summary>今のウェーブで倒した数。</summary>
        public static int KillsInWave { get; internal set; }

        /// <summary>今のウェーブの目標撃破数。</summary>
        public static int TargetKills { get; internal set; }

        public static int KillsRemaining => Mathf.Max(0, TargetKills - KillsInWave);

        /// <summary>今の段階の残り秒数（長さが決まっていない段階では 0）。各PCで数える。</summary>
        public static float PhaseRemaining { get; internal set; }

        /// <summary>今の段階の長さ（決まっていなければ 0）。</summary>
        public static float PhaseDuration { get; internal set; }

        /// <summary>今の段階に入ってからの秒数（このPCで数えた値）。</summary>
        public static float PhaseElapsed { get; internal set; }

        /// <summary>試合の残り時間（制限時間が無ければ −1）。各PCで数え、Host から届くたびに合わせ直す。</summary>
        public static float TimeLeft { get; internal set; } = -1f;

        /// <summary>試合の制限時間（無ければ 0）。</summary>
        public static float TimeLimit { get; internal set; }

        /// <summary>今、残り時間が減っているか。</summary>
        public static bool ClockRunning { get; internal set; }

        public static WaveEndKind EndKind { get; internal set; }

        /// <summary>試合にいるプレイヤー（bit p = P(p+1)。Host が決めて配る）。</summary>
        public static int PlayerMask { get; internal set; } = 1;

        /// <summary>合間のイベントの今の係（合間の外では null）。</summary>
        public static WaveIntervalRunner BreakRunner { get; internal set; }

        /// <summary>どこまで行けたか（1 から。全部クリアならウェーブの数）。リザルト用。</summary>
        public static int ReachedWave => EndKind == WaveEndKind.AllWavesCleared ? WaveCount : WaveNumber;

        /// <summary>
        /// true ならウェーブを進めない（負荷計測で使う。<see cref="FortressMatch.EndChecksSuspended"/> と同じ扱い）。
        /// </summary>
        public static bool Suspended { get; internal set; }

        /// <summary>段階が変わった（前, 今）。全員のPCで1回ずつ。</summary>
        public static event Action<WavePhase, WavePhase> PhaseChanged;

        /// <summary>ウェーブの発表が始まった（番号 0 から）。</summary>
        public static event Action<int> WaveAnnounced;

        /// <summary>ウェーブが始まった（GO!。番号 0 から）。</summary>
        public static event Action<int> WaveStarted;

        /// <summary>ウェーブの目標に届いた（番号 0 から）。最後のウェーブでも鳴る。</summary>
        public static event Action<int> WaveCleared;

        /// <summary>合間が始まった（今クリアしたウェーブの番号）。</summary>
        public static event Action<int> BreakStarted;

        /// <summary>合間が終わった（今クリアしたウェーブの番号）。</summary>
        public static event Action<int> BreakEnded;

        /// <summary>計画が終わった（全部クリア・時間切れ）。</summary>
        public static event Action<WaveEndKind> Ended;

        /// <summary>
        /// 「まだ準備ができていない」を知らせる関数を足す（幕が画面を覆っている間 true を返すなど）。
        /// どれか1つでも true の間は、このPCは準備ができていない扱い。起動時に1回だけ足す（消さない）。
        /// </summary>
        public static void AddReadyBlocker(Func<bool> isBlocking)
        {
            if (isBlocking != null && !ReadyBlockers.Contains(isBlocking))
            {
                ReadyBlockers.Add(isBlocking);
            }
        }

        /// <summary>このPCの準備ができているか（幕が開いているか）。</summary>
        public static bool IsLocallyReady
        {
            get
            {
                foreach (var blocker in ReadyBlockers)
                {
                    try
                    {
                        if (blocker())
                        {
                            return false;
                        }
                    }
                    catch (Exception)
                    {
                        // 壊れた関数があっても始められなくならないように、無視する。
                    }
                }

                return true;
            }
        }

        /// <summary>このPCのプレイヤーの、合間のイベントの入力の数（合間ごとの累計。握った回数など）。Host に届く。</summary>
        public static int LocalBreakInput { get; private set; }

        public static void SetLocalBreakInput(int count)
        {
            LocalBreakInput = Mathf.Max(0, count);
        }

        /// <summary>Host: プレイヤー p の、合間のイベントの入力の数（届いた最新）。</summary>
        public static int BreakInput(int player) => player >= 0 && player < PlayerCount ? BreakInputs[player] : 0;

        internal static void SetBreakInput(int player, int count)
        {
            if (player >= 0 && player < PlayerCount)
            {
                BreakInputs[player] = Mathf.Max(BreakInputs[player], count);
            }
        }

        internal static void ClearBreakInputs()
        {
            Array.Clear(BreakInputs, 0, BreakInputs.Length);
            LocalBreakInput = 0;
        }

        // ───────── テスト用（要塞デザイナー。Host／オフラインだけ効く） ─────────

        internal static Action DebugSkipHandler;
        internal static Action<int> DebugJumpHandler;

        /// <summary>テスト用: 今の段階をすぐ終わらせる（戦っている間なら、目標を倒したことにする）。</summary>
        public static void DebugSkip() => DebugSkipHandler?.Invoke();

        /// <summary>テスト用: index のウェーブの発表から始め直す。</summary>
        public static void DebugJumpTo(int index) => DebugJumpHandler?.Invoke(index);

        // ───────── 係から呼ぶ ─────────

        internal static void RaisePhaseChanged(WavePhase previous, WavePhase next)
        {
            PhaseChanged?.Invoke(previous, next);
            switch (next)
            {
                case WavePhase.Announce:
                    WaveAnnounced?.Invoke(WaveIndex);
                    break;
                case WavePhase.Combat:
                    WaveStarted?.Invoke(WaveIndex);
                    break;
                case WavePhase.Retreat:
                    WaveCleared?.Invoke(WaveIndex);
                    break;
                case WavePhase.Break:
                    BreakStarted?.Invoke(WaveIndex);
                    break;
                case WavePhase.Finished:
                    if (EndKind == WaveEndKind.AllWavesCleared)
                    {
                        WaveCleared?.Invoke(WaveIndex);
                    }

                    Ended?.Invoke(EndKind);
                    break;
            }

            if (previous == WavePhase.Break)
            {
                // 合間が終わった時点では、もう次のウェーブの番号になっている。
                BreakEnded?.Invoke(next == WavePhase.Announce ? WaveIndex - 1 : WaveIndex);
            }
        }

        internal static void ResetState()
        {
            Plan = null;
            Phase = WavePhase.None;
            PreviousPhase = WavePhase.None;
            WaveIndex = 0;
            WaveCount = 0;
            KillsInWave = 0;
            TargetKills = 0;
            PhaseRemaining = 0f;
            PhaseDuration = 0f;
            PhaseElapsed = 0f;
            TimeLeft = -1f;
            TimeLimit = 0f;
            ClockRunning = false;
            EndKind = WaveEndKind.None;
            PlayerMask = 1;
            BreakRunner = null;
            ClearBreakInputs();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            ResetState();
            Suspended = false;
            PhaseChanged = null;
            WaveAnnounced = null;
            WaveStarted = null;
            WaveCleared = null;
            BreakStarted = null;
            BreakEnded = null;
            Ended = null;
            DebugSkipHandler = null;
            DebugJumpHandler = null;
            // ReadyBlockers は起動時（AfterAssembliesLoaded）に足されるので消さない。
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRunner()
        {
            var go = new GameObject("[Fortress] Waves");
            go.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<FortressWaveRunner>();
        }
    }
}
