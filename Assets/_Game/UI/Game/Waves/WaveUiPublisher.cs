using MS2026.Fortress;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ウェーブの今の様子（<see cref="FortressWaves"/>）を画面の値（UiValues）へ書く。<see cref="FortressUiValues"/> が毎フレーム呼ぶ。
    /// 値が変わらなければ何も起きないので軽い。値の名前は <see cref="FortressUiKeys"/> の wave.*。
    /// </summary>
    internal static class WaveUiPublisher
    {
        private static int _lastSyncCount;
        private static float _syncShownAt = float.NegativeInfinity;

        public static void Publish()
        {
            PublishBrushing(FortressWaves.BreakRunner as BrushingRunner);
            var active = FortressWaves.IsActive;
            UiValues.Set(FortressUiKeys.WaveActive, active);
            if (!active)
            {
                UiValues.Set(FortressUiKeys.WaveAnnouncing, false);
                UiValues.Set(FortressUiKeys.WaveCountdown, 0);
                UiValues.Set(FortressUiKeys.WaveGo, false);
                UiValues.Set(FortressUiKeys.WaveClearShowing, false);
                UiValues.Set(FortressUiKeys.WaveBreak, false);
                UiValues.Set(FortressUiKeys.WaveNearGoal, false);
                UiValues.Set(FortressUiKeys.WaveTimeWarning, false);
                UiValues.Set(FortressUiKeys.WaveTimeUp, false);
                UiValues.Set(FortressUiKeys.WaveFirstHint, false);
                UiValues.Set(FortressUiKeys.WaveShowBanner, false);
                UiValues.Set(FortressUiKeys.WaveShowNewEnemies, false);
                UiValues.Set(FortressUiKeys.WaveShowRouteHint, false);
                return;
            }

            var phase = FortressWaves.Phase;
            var number = FortressWaves.WaveNumber;
            var count = FortressWaves.WaveCount;
            var isFinal = FortressWaves.IsFinalWave;
            var target = FortressWaves.TargetKills;
            var kills = FortressWaves.KillsInWave;
            var plan = FortressWaves.Plan;
            var wave = FortressWaves.CurrentWave;
            var timing = WaveDisplayTiming.Current;

            UiValues.Set(FortressUiKeys.WaveNumber, number);
            UiValues.Set(FortressUiKeys.WaveCount, count);
            UiValues.Set(FortressUiKeys.WaveNumberText, $"{number}/{count}");
            UiValues.Set(FortressUiKeys.WaveIsFinal, isFinal);
            UiValues.Set(FortressUiKeys.WavePhase, (int)phase);
            UiValues.Set(FortressUiKeys.WaveKills, kills);
            UiValues.Set(FortressUiKeys.WaveTarget, target);
            UiValues.Set(FortressUiKeys.WaveRemaining, FortressWaves.KillsRemaining);
            UiValues.Set(FortressUiKeys.WaveProgress01, target > 0 ? (float)kills / target : 0f);
            UiValues.Set(FortressUiKeys.WaveNearGoal, WaveHudLogic.NearGoal(phase, FortressWaves.KillsRemaining, timing.nearGoalKills));

            var hasLimit = FortressWaves.TimeLeft >= 0f;
            UiValues.Set(FortressUiKeys.WaveHasTimeLimit, hasLimit);
            UiValues.Set(FortressUiKeys.WaveTimeLeft, FortressWaves.TimeLeft);
            UiValues.Set(FortressUiKeys.WaveTimeText, WaveHudLogic.Clock(FortressWaves.TimeLeft));
            UiValues.Set(FortressUiKeys.WaveTimeWarning, WaveHudLogic.TimeWarning(FortressWaves.ClockRunning, FortressWaves.TimeLeft, timing.timeWarningSeconds));

            var announcing = phase == WavePhase.Announce;
            UiValues.Set(FortressUiKeys.WaveAnnouncing, announcing);
            UiValues.Set(FortressUiKeys.WaveAnnounceTitle, WaveHudLogic.AnnounceTitle(number, isFinal, wave?.label));
            UiValues.Set(FortressUiKeys.WaveAnnounceGoal, WaveHudLogic.AnnounceGoal(target));
            UiValues.Set(FortressUiKeys.WaveAnnounceText, timing.showAnnounceText ? wave?.announceText ?? "" : "");
            UiValues.Set(FortressUiKeys.WaveShowBanner, WaveDisplayTiming.ShowInAnnounce(phase, FortressWaves.PhaseElapsed, timing.bannerDelay));
            UiValues.Set(FortressUiKeys.WaveShowNewEnemies,
                timing.showNewEnemyCards && WaveDisplayTiming.ShowInAnnounce(phase, FortressWaves.PhaseElapsed, timing.newEnemyDelay));
            UiValues.Set(FortressUiKeys.WaveShowRouteHint,
                timing.showRouteHint && WaveDisplayTiming.ShowInAnnounce(phase, FortressWaves.PhaseElapsed, timing.routeDelay));
            UiValues.Set(FortressUiKeys.WaveCountdown, WaveHudLogic.Countdown(phase, FortressWaves.PhaseRemaining, plan != null ? plan.countdownSeconds : 0f));
            UiValues.Set(FortressUiKeys.WaveGo, WaveHudLogic.ShowGo(phase, FortressWaves.PhaseElapsed, timing.goSeconds));
            UiValues.Set(FortressUiKeys.WaveClearShowing, phase == WavePhase.Retreat);
            UiValues.Set(FortressUiKeys.WaveClearText, WaveHudLogic.ClearText(number, isFinal));
            UiValues.Set(FortressUiKeys.WaveBreak, phase == WavePhase.Break);
            UiValues.Set(FortressUiKeys.WaveTimeUp, phase == WavePhase.Finished && FortressWaves.EndKind == WaveEndKind.TimeUp);
            UiValues.Set(FortressUiKeys.WaveFirstHint, timing.showGripHint && WaveDisplayTiming.IsHintPhase(phase));
        }

        /// <summary>コアのコーティング（歯みがきのごほうび）。<see cref="FortressUiValues"/> がコアの値と一緒に呼ぶ。</summary>
        public static void PublishCoating(CoreCrystalController core)
        {
            var coating = core != null ? core.Coating : 0f;
            UiValues.Set(FortressUiKeys.CoreCoating, coating);
            UiValues.Set(FortressUiKeys.CoreCoating01, core != null ? coating / UnityEngine.Mathf.Max(1f, core.maxHealth) : 0f);
            UiValues.Set(FortressUiKeys.CoreHasCoating, coating > 0.05f);
        }

        private static void PublishBrushing(BrushingRunner runner)
        {
            var active = runner != null;
            UiValues.Set(FortressUiKeys.BrushActive, active);
            if (!active)
            {
                _lastSyncCount = 0;
                UiValues.Set(FortressUiKeys.BrushIntro, false);
                UiValues.Set(FortressUiKeys.BrushBrushing, false);
                UiValues.Set(FortressUiKeys.BrushResult, false);
                UiValues.Set(FortressUiKeys.BrushSyncShowing, false);
                return;
            }

            var stage = runner.Stage;
            UiValues.Set(FortressUiKeys.BrushStage, (int)stage);
            UiValues.Set(FortressUiKeys.BrushIntro, stage == BrushingStage.Intro);
            UiValues.Set(FortressUiKeys.BrushBrushing, stage == BrushingStage.Brushing);
            UiValues.Set(FortressUiKeys.BrushResult, stage == BrushingStage.Result);
            UiValues.Set(FortressUiKeys.BrushClean01, runner.Clean01);
            UiValues.Set(FortressUiKeys.BrushTimeLeft, UnityEngine.Mathf.Ceil(runner.BrushTimeLeft));
            UiValues.Set(FortressUiKeys.BrushPulses, runner.LocalPulses);
            for (var p = 0; p < FortressWaves.PlayerCount; p++)
            {
                UiValues.Set(FortressUiKeys.BrushShare(p), runner.Share01(p));
                UiValues.Set(FortressUiKeys.BrushIn(p), runner.Participates(p));
            }

            var sync = runner.SyncCount;
            if (sync > _lastSyncCount)
            {
                _syncShownAt = UnityEngine.Time.unscaledTime;
            }

            _lastSyncCount = sync;
            UiValues.Set(FortressUiKeys.BrushSyncCount, sync);
            UiValues.Set(FortressUiKeys.BrushSyncShowing, UnityEngine.Time.unscaledTime - _syncShownAt < WaveDisplayTiming.Current.syncShowSeconds);
            UiValues.Set(FortressUiKeys.BrushResultText, WaveHudLogic.BrushResultText(runner.HealAmount, runner.CoatingAmount, runner.Clean01));
            UiValues.Set(FortressUiKeys.BrushResultTitle, WaveHudLogic.BrushResultTitle(runner.Clean01));
            UiValues.Set(FortressUiKeys.BrushRemaining, runner.Remaining);
            UiValues.Set(FortressUiKeys.BrushTime01, runner.BrushDuration > 0f ? runner.BrushTimeLeft / runner.BrushDuration : 0f);
            UiValues.Set(FortressUiKeys.BrushPreviewText, WaveHudLogic.BrushPreviewText(runner.PreviewHeal, runner.PreviewCoating));
            UiValues.Set(FortressUiKeys.BrushFullText, WaveHudLogic.BrushFullText(runner.FullReward));
        }
    }
}
