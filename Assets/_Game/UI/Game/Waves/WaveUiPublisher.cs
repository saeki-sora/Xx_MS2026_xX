using MS2026.Fortress;

namespace MS2026.UI.Game
{
    /// <summary>
    /// ウェーブの今の様子（<see cref="FortressWaves"/>）を画面の値（UiValues）へ書く。<see cref="FortressUiValues"/> が毎フレーム呼ぶ。
    /// 値が変わらなければ何も起きないので軽い。値の名前は <see cref="FortressUiKeys"/> の wave.*。
    /// </summary>
    internal static class WaveUiPublisher
    {
        public static void Publish()
        {
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

            UiValues.Set(FortressUiKeys.WaveNumber, number);
            UiValues.Set(FortressUiKeys.WaveCount, count);
            UiValues.Set(FortressUiKeys.WaveNumberText, $"{number}/{count}");
            UiValues.Set(FortressUiKeys.WaveIsFinal, isFinal);
            UiValues.Set(FortressUiKeys.WavePhase, (int)phase);
            UiValues.Set(FortressUiKeys.WaveKills, kills);
            UiValues.Set(FortressUiKeys.WaveTarget, target);
            UiValues.Set(FortressUiKeys.WaveRemaining, FortressWaves.KillsRemaining);
            UiValues.Set(FortressUiKeys.WaveProgress01, target > 0 ? (float)kills / target : 0f);
            UiValues.Set(FortressUiKeys.WaveNearGoal, WaveHudLogic.NearGoal(phase, FortressWaves.KillsRemaining));

            var hasLimit = FortressWaves.TimeLeft >= 0f;
            UiValues.Set(FortressUiKeys.WaveHasTimeLimit, hasLimit);
            UiValues.Set(FortressUiKeys.WaveTimeLeft, FortressWaves.TimeLeft);
            UiValues.Set(FortressUiKeys.WaveTimeText, WaveHudLogic.Clock(FortressWaves.TimeLeft));
            UiValues.Set(FortressUiKeys.WaveTimeWarning, WaveHudLogic.TimeWarning(FortressWaves.ClockRunning, FortressWaves.TimeLeft));

            var announcing = phase == WavePhase.Announce;
            UiValues.Set(FortressUiKeys.WaveAnnouncing, announcing);
            UiValues.Set(FortressUiKeys.WaveAnnounceTitle, WaveHudLogic.AnnounceTitle(number, isFinal, wave?.label));
            UiValues.Set(FortressUiKeys.WaveAnnounceGoal, WaveHudLogic.AnnounceGoal(target));
            UiValues.Set(FortressUiKeys.WaveAnnounceText, wave?.announceText ?? "");
            UiValues.Set(FortressUiKeys.WaveCountdown, WaveHudLogic.Countdown(phase, FortressWaves.PhaseRemaining, plan != null ? plan.countdownSeconds : 0f));
            UiValues.Set(FortressUiKeys.WaveGo, WaveHudLogic.ShowGo(phase, FortressWaves.PhaseElapsed));
            UiValues.Set(FortressUiKeys.WaveClearShowing, phase == WavePhase.Retreat);
            UiValues.Set(FortressUiKeys.WaveClearText, WaveHudLogic.ClearText(number, isFinal));
            UiValues.Set(FortressUiKeys.WaveBreak, phase == WavePhase.Break);
            UiValues.Set(FortressUiKeys.WaveTimeUp, phase == WavePhase.Finished && FortressWaves.EndKind == WaveEndKind.TimeUp);
        }
    }
}
