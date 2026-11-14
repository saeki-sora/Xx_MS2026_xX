using System;
using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>ウェーブの進み具合の段階。通信で byte として送るので、足すときは最後に足す。</summary>
    public enum WavePhase : byte
    {
        /// <summary>ウェーブの計画が動いていない（計画が無いステージ、試合の外）。</summary>
        None = 0,

        /// <summary>全員の準備（読み込み・幕が開く）を待っている。</summary>
        WaitingForPlayers = 1,

        /// <summary>「WAVE n！目標撃破数 m！」と敵のルートを見せている（敵はまだ来ない）。</summary>
        Announce = 2,

        /// <summary>戦っている（敵が湧く）。</summary>
        Combat = 3,

        /// <summary>目標を倒した。「WAVE CLEAR!」を出し、残りの敵が画面の外へ逃げる。</summary>
        Retreat = 4,

        /// <summary>ウェーブの合間（歯みがきタイムなど。敵は来ない）。</summary>
        Break = 5,

        /// <summary>全部のウェーブを終えた、または時間切れ。</summary>
        Finished = 6,

        /// <summary>試合の始まりの「YOU の矢印・HUD → 3・2・1」（WAVE 1 の発表の後、戦いの前。敵は来ない。2026-11-11 から発表の後に移した）。</summary>
        Hint = 7,

        /// <summary>合間の後、次のウェーブの発表の前の「席替え」（全員の砲台が隣の席へ跳び、YOU の矢印が出る。敵は来ない）。</summary>
        SeatChange = 8,

        /// <summary>試合の始まりの「コアを守れ！」（全員の準備ができた後、WAVE 1 の発表の前。HUD はまだ隠したまま。敵は来ない。2026-11-11）。</summary>
        Opening = 9
    }

    /// <summary>ウェーブの計画が終わった理由。</summary>
    public enum WaveEndKind : byte
    {
        None = 0,

        /// <summary>最後のウェーブの目標を倒した。</summary>
        AllWavesCleared = 1,

        /// <summary>試合の制限時間が来た。</summary>
        TimeUp = 2
    }

    /// <summary>1回の <see cref="WaveFlow.Tick"/> で起きたこと（いくつも同時に立つ）。</summary>
    [Flags]
    public enum WaveFlowSignal
    {
        None = 0,
        PhaseChanged = 1 << 0,

        /// <summary>今のウェーブの敵を湧かせ始める。</summary>
        StartSpawning = 1 << 1,

        /// <summary>湧かせるのを止める。</summary>
        StopSpawning = 1 << 2,

        /// <summary>残りの敵を逃がし始める。</summary>
        BeginRetreat = 1 << 3,

        /// <summary>逃げ終わった（または待ちきれない）。まだ残っている敵は消す。</summary>
        FinishRetreat = 1 << 4,

        BeginBreak = 1 << 5,
        EndBreak = 1 << 6,

        /// <summary>計画が終わった（<see cref="WaveFlow.EndKind"/>）。</summary>
        Ended = 1 << 7,

        /// <summary>今のウェーブの目標に届いた。</summary>
        WaveCleared = 1 << 8
    }

    /// <summary><see cref="WaveFlow"/> の決まり（計画から作る。テストでは手で作る）。</summary>
    public sealed class WaveFlowConfig
    {
        /// <summary>ウェーブごとの目標撃破数（並びの長さ＝ウェーブの数）。</summary>
        public int[] targets = Array.Empty<int>();

        /// <summary>ウェーブごとの「後の合間」の秒数（最後のウェーブの値は使わない）。</summary>
        public float[] breakSeconds = Array.Empty<float>();

        /// <summary>ウェーブごとに、合間の係が「終わった」と言うまで待つか（待つ場合も <see cref="breakSeconds"/> が上限）。</summary>
        public bool[] breakWaitsForEvent = Array.Empty<bool>();

        public float startDelaySeconds = 0.8f;
        public float maxWaitForPlayersSeconds = 10f;

        /// <summary>全員の準備ができた後、WAVE 1 の発表の前に「コアを守れ！」を出す秒数（0 なら無し）。</summary>
        public float openingSeconds;

        /// <summary>WAVE 1 の発表の後、YOU の矢印と HUD を出してから 3・2・1 を始めるまでの秒数（ヒントの段階の長さ = これ ＋ <see cref="countdownSeconds"/>）。</summary>
        public float hintSeconds;

        /// <summary>3・2・1 の秒数（WAVE 1 はヒントの段階の最後、WAVE 2 からは発表の最後に数える）。</summary>
        public float countdownSeconds;

        public float announceSeconds = 4f;

        /// <summary>合間の後、発表の前の「席替え」の秒数（0 なら無し。跳ぶ動きと YOU の矢印が終わるまで）。</summary>
        public float seatChangeSeconds;

        public float clearBannerSeconds = 1.6f;
        public float retreatMaxSeconds = 5f;

        /// <summary>試合全体の制限時間（0 以下 = なし）。</summary>
        public float timeLimitSeconds;

        public bool clockRunsDuringBreaks;

        public int WaveCount => targets?.Length ?? 0;

        public static WaveFlowConfig From(WavePlan plan)
        {
            var count = plan != null ? plan.Count : 0;
            var config = new WaveFlowConfig
            {
                targets = new int[count],
                breakSeconds = new float[count],
                breakWaitsForEvent = new bool[count]
            };
            if (plan == null)
            {
                return config;
            }

            for (var i = 0; i < count; i++)
            {
                var wave = plan.waves[i];
                config.targets[i] = Mathf.Max(1, wave?.targetKills ?? 1);
                var ev = wave?.intervalEvent;
                config.breakSeconds[i] = ev != null ? Mathf.Max(0f, ev.DurationSeconds) : Mathf.Max(0f, wave?.restSeconds ?? 0f);
                config.breakWaitsForEvent[i] = ev != null;
            }

            config.startDelaySeconds = plan.startDelaySeconds;
            var timing = plan.display != null && plan.display.timing != null ? plan.display.timing : WaveDisplayTiming.Default;
            config.openingSeconds = timing.openingSeconds;
            config.hintSeconds = timing.hintSeconds;
            config.countdownSeconds = plan.countdownSeconds;
            config.maxWaitForPlayersSeconds = plan.maxWaitForPlayersSeconds;
            config.announceSeconds = plan.announceSeconds;
            config.seatChangeSeconds = plan.seatRotation != null ? plan.seatRotation.WaveSeconds : 0f;
            config.clearBannerSeconds = plan.clearBannerSeconds;
            config.retreatMaxSeconds = plan.retreatMaxSeconds;
            config.timeLimitSeconds = plan.timeLimitSeconds;
            config.clockRunsDuringBreaks = plan.clockRunsDuringBreaks;
            return config;
        }
    }

    /// <summary><see cref="WaveFlow.Tick"/> に渡す、今の様子。</summary>
    public struct WaveFlowInput
    {
        /// <summary>試合全体で倒した数（<see cref="EnemyKillCounter.Total"/>）。</summary>
        public int totalKills;

        /// <summary>全員の準備ができたか。</summary>
        public bool playersReady;

        /// <summary>撤退中、まだだれかの画面に映っている敵の数。</summary>
        public int retreatingVisible;

        /// <summary>合間の係が「終わった」と言っているか。</summary>
        public bool breakFinished;
    }

    /// <summary>
    /// ウェーブの進め方（Unity に頼らない状態機械。テストあり）。決めるのは Host（オフラインでは自分）だけ。
    /// 待つ →（コアを守れ！）→ 発表 →（WAVE 1 だけ: YOU の矢印・HUD → 3・2・1）→ 戦う →（目標）→ 撤退 → 合間 →（席替え）→ 発表 …→ 最後のウェーブの目標でクリア。制限時間が来たら時間切れ。
    /// </summary>
    public sealed class WaveFlow
    {
        private readonly WaveFlowConfig _config;

        public WaveFlow(WaveFlowConfig config)
        {
            _config = config ?? new WaveFlowConfig();
            TimeLeft = _config.timeLimitSeconds > 0f ? _config.timeLimitSeconds : -1f;
        }

        public WaveFlowConfig Config => _config;

        public WavePhase Phase { get; private set; }

        public WavePhase PreviousPhase { get; private set; }

        /// <summary>今のウェーブの番号（0 から）。合間の間は、今クリアしたウェーブ。</summary>
        public int WaveIndex { get; private set; }

        /// <summary>今の段階に入ってからの秒数。</summary>
        public float PhaseTime { get; private set; }

        /// <summary>今の段階の長さ（決まっていなければ 0。戦っている間など）。</summary>
        public float PhaseDuration { get; private set; }

        /// <summary>今のウェーブが始まったときの、試合全体の撃破数。</summary>
        public int KillsAtWaveStart { get; private set; }

        /// <summary>今のウェーブで倒した数（目標で止まる）。</summary>
        public int KillsInWave { get; private set; }

        /// <summary>試合の残り時間（制限時間が無ければ −1）。</summary>
        public float TimeLeft { get; private set; }

        public WaveEndKind EndKind { get; private set; }

        public int WaveCount => _config.WaveCount;

        public bool IsFinalWave => WaveIndex >= WaveCount - 1;

        public int Target => WaveIndex >= 0 && WaveIndex < WaveCount ? _config.targets[WaveIndex] : 0;

        /// <summary>計画が終わったか（全部クリア、または時間切れ）。</summary>
        public bool HasEnded => Phase == WavePhase.Finished;

        /// <summary>始める（全員の準備を待つ段階へ）。</summary>
        public WaveFlowSignal Begin()
        {
            if (WaveCount == 0)
            {
                return Finish(WaveEndKind.AllWavesCleared, WaveFlowSignal.None);
            }

            WaveIndex = 0;
            return Enter(WavePhase.WaitingForPlayers, _config.maxWaitForPlayersSeconds, WaveFlowSignal.None);
        }

        public WaveFlowSignal Tick(float dt, in WaveFlowInput input)
        {
            dt = Mathf.Max(0f, dt);
            if (Phase == WavePhase.None || Phase == WavePhase.Finished)
            {
                return WaveFlowSignal.None;
            }

            PhaseTime += dt;
            var signal = WaveFlowSignal.None;
            if (Phase == WavePhase.Combat)
            {
                KillsInWave = Mathf.Clamp(input.totalKills - KillsAtWaveStart, 0, Target);
                if (KillsInWave >= Target)
                {
                    // 同じフレームに時間切れになっても、届いたほうを優先する。
                    return ClearWave(signal);
                }
            }

            if (TimeLeft >= 0f && (Phase == WavePhase.Combat || _config.clockRunsDuringBreaks) && !IsPreMatch(Phase))
            {
                TimeLeft = Mathf.Max(0f, TimeLeft - dt);
                if (TimeLeft <= 0f)
                {
                    return Finish(WaveEndKind.TimeUp, signal | WaveFlowSignal.StopSpawning);
                }
            }

            switch (Phase)
            {
                case WavePhase.WaitingForPlayers:
                    if ((input.playersReady && PhaseTime >= _config.startDelaySeconds) || PhaseTime >= _config.maxWaitForPlayersSeconds)
                    {
                        signal |= AfterWaiting();
                    }

                    break;
                case WavePhase.Opening:
                    if (PhaseTime >= _config.openingSeconds)
                    {
                        signal |= EnterAnnounce();
                    }

                    break;
                case WavePhase.Hint:
                    if (PhaseTime >= PhaseDuration)
                    {
                        signal |= StartCombat(input.totalKills);
                    }

                    break;
                case WavePhase.Announce:
                    if (PhaseTime >= _config.announceSeconds)
                    {
                        signal |= AfterAnnounce(input.totalKills);
                    }

                    break;
                case WavePhase.Retreat:
                    if (PhaseTime >= _config.clearBannerSeconds && (input.retreatingVisible <= 0 || PhaseTime >= _config.retreatMaxSeconds))
                    {
                        signal |= WaveFlowSignal.FinishRetreat;
                        signal |= Enter(WavePhase.Break, BreakSeconds(WaveIndex), WaveFlowSignal.BeginBreak);
                    }

                    break;
                case WavePhase.Break:
                    var waits = WaveIndex < _config.breakWaitsForEvent.Length && _config.breakWaitsForEvent[WaveIndex];
                    if (PhaseTime >= PhaseDuration || (waits && input.breakFinished))
                    {
                        signal |= EndBreak();
                    }

                    break;
                case WavePhase.SeatChange:
                    if (PhaseTime >= _config.seatChangeSeconds)
                    {
                        signal |= EnterAnnounce();
                    }

                    break;
            }

            return signal;
        }

        /// <summary>テスト用: 今の段階をすぐ終わらせる（戦っている間なら、目標を倒したことにする）。</summary>
        public WaveFlowSignal Skip(int totalKills)
        {
            switch (Phase)
            {
                case WavePhase.WaitingForPlayers:
                    return AfterWaiting();
                case WavePhase.Opening:
                    return EnterAnnounce();
                case WavePhase.Hint:
                    return StartCombat(totalKills);
                case WavePhase.Announce:
                    return AfterAnnounce(totalKills);
                case WavePhase.Combat:
                    KillsInWave = Target;
                    return ClearWave(WaveFlowSignal.None);
                case WavePhase.Retreat:
                    return WaveFlowSignal.FinishRetreat | Enter(WavePhase.Break, BreakSeconds(WaveIndex), WaveFlowSignal.BeginBreak);
                case WavePhase.Break:
                    return EndBreak();
                case WavePhase.SeatChange:
                    return EnterAnnounce();
                default:
                    return WaveFlowSignal.None;
            }
        }

        /// <summary>テスト用: index のウェーブの発表から始め直す（時間はそのまま）。</summary>
        public WaveFlowSignal JumpTo(int index)
        {
            if (WaveCount == 0)
            {
                return WaveFlowSignal.None;
            }

            var stop = Phase == WavePhase.Combat ? WaveFlowSignal.StopSpawning : WaveFlowSignal.None;
            var endBreak = Phase == WavePhase.Break ? WaveFlowSignal.EndBreak : WaveFlowSignal.None;
            var endRetreat = Phase == WavePhase.Retreat ? WaveFlowSignal.FinishRetreat : WaveFlowSignal.None;
            WaveIndex = Mathf.Clamp(index, 0, WaveCount - 1);
            EndKind = WaveEndKind.None;
            return stop | endBreak | endRetreat | EnterAnnounce();
        }

        /// <summary>試合の始まりの、まだ戦いの前の段階か（時間が減らない）。</summary>
        public static bool IsPreMatch(WavePhase phase) => phase == WavePhase.WaitingForPlayers || phase == WavePhase.Opening || phase == WavePhase.Hint;

        /// <summary>WAVE 1 の発表の後の「YOU の矢印・HUD → 3・2・1」の秒数（0 なら発表からすぐ戦い）。</summary>
        public float PreludeSeconds => Mathf.Max(0f, _config.hintSeconds) + Mathf.Max(0f, _config.countdownSeconds);

        // 全員そろったら、「コアを守れ！」（あれば）→ WAVE 1 の発表。
        private WaveFlowSignal AfterWaiting() =>
            _config.openingSeconds > 0f
                ? Enter(WavePhase.Opening, _config.openingSeconds, WaveFlowSignal.None)
                : EnterAnnounce();

        private WaveFlowSignal EnterAnnounce() => Enter(WavePhase.Announce, _config.announceSeconds, WaveFlowSignal.None);

        // 発表が終わったら戦い。WAVE 1 だけは、先に YOU の矢印と HUD を出し、最後に 3・2・1（ヒントの段階）。
        private WaveFlowSignal AfterAnnounce(int totalKills) =>
            WaveIndex == 0 && PreludeSeconds > 0f
                ? Enter(WavePhase.Hint, PreludeSeconds, WaveFlowSignal.None)
                : StartCombat(totalKills);

        // 合間が終わったら次のウェーブの番号にして、席替え（あれば）→ 発表。
        private WaveFlowSignal EndBreak()
        {
            WaveIndex++;
            var next = _config.seatChangeSeconds > 0f
                ? Enter(WavePhase.SeatChange, _config.seatChangeSeconds, WaveFlowSignal.None)
                : EnterAnnounce();
            return WaveFlowSignal.EndBreak | next;
        }

        private float BreakSeconds(int wave) =>
            wave >= 0 && wave < _config.breakSeconds.Length ? Mathf.Max(0f, _config.breakSeconds[wave]) : 0f;

        private WaveFlowSignal StartCombat(int totalKills)
        {
            KillsAtWaveStart = totalKills;
            KillsInWave = 0;
            return Enter(WavePhase.Combat, 0f, WaveFlowSignal.StartSpawning);
        }

        private WaveFlowSignal ClearWave(WaveFlowSignal signal)
        {
            signal |= WaveFlowSignal.WaveCleared | WaveFlowSignal.StopSpawning;
            if (IsFinalWave)
            {
                return Finish(WaveEndKind.AllWavesCleared, signal);
            }

            return Enter(WavePhase.Retreat, _config.retreatMaxSeconds, signal | WaveFlowSignal.BeginRetreat);
        }

        private WaveFlowSignal Finish(WaveEndKind kind, WaveFlowSignal signal)
        {
            EndKind = kind;
            return Enter(WavePhase.Finished, 0f, signal | WaveFlowSignal.Ended);
        }

        private WaveFlowSignal Enter(WavePhase phase, float duration, WaveFlowSignal signal)
        {
            if (phase == WavePhase.Announce)
            {
                KillsInWave = 0; // 次のウェーブの発表では「あと○体」を目標そのままにする（前のウェーブの数を持ち越さない）
            }

            PreviousPhase = Phase;
            Phase = phase;
            PhaseTime = 0f;
            PhaseDuration = Mathf.Max(0f, duration);
            return signal | WaveFlowSignal.PhaseChanged;
        }
    }
}
