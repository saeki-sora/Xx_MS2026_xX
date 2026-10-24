using MS2026.Fortress;
using UnityEngine;

namespace MS2026.UI.Game
{
    /// <summary>ウェーブの表示の決まり（Unity の画面に頼らない計算。テストあり）。</summary>
    public static class WaveHudLogic
    {
        /// <summary>「GO!」を出す秒数。</summary>
        public const float GoSeconds = 0.9f;

        /// <summary>「あと○体！」で盛り上げ始める残りの数。</summary>
        public const int NearGoalKills = 5;

        /// <summary>残り時間の警告を出し始める秒数。</summary>
        public const float WarningSeconds = 10f;

        /// <summary>残り秒数を「02:48」の形に（切り上げ。−なら「--:--」）。</summary>
        public static string Clock(float seconds)
        {
            if (seconds < 0f)
            {
                return "--:--";
            }

            var total = Mathf.CeilToInt(seconds - 1e-4f);
            total = Mathf.Max(0, total);
            return $"{total / 60:00}:{total % 60:00}";
        }

        /// <summary>発表の最後の 3・2・1（出さないときは 0）。</summary>
        public static int Countdown(WavePhase phase, float phaseRemaining, float countdownSeconds)
        {
            if (phase != WavePhase.Announce || countdownSeconds <= 0f || phaseRemaining <= 0f || phaseRemaining > countdownSeconds)
            {
                return 0;
            }

            return Mathf.CeilToInt(phaseRemaining - 1e-4f);
        }

        public static bool ShowGo(WavePhase phase, float phaseElapsed) => phase == WavePhase.Combat && phaseElapsed < GoSeconds;

        public static bool NearGoal(WavePhase phase, int remaining) => phase == WavePhase.Combat && remaining > 0 && remaining <= NearGoalKills;

        public static bool TimeWarning(bool clockRunning, float timeLeft) => clockRunning && timeLeft >= 0f && timeLeft <= WarningSeconds;

        /// <summary>発表の大きな文字（最終ウェーブで名前が空なら「FINAL WAVE」）。</summary>
        public static string AnnounceTitle(int number, bool isFinal, string label)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            return isFinal && number > 1 ? "FINAL WAVE" : $"WAVE {number}";
        }

        public static string AnnounceGoal(int target) => $"目標撃破数 {target}！";

        public static string ClearText(int number, bool isFinal) => isFinal ? "ALL CLEAR!" : $"WAVE {number} CLEAR!";

        /// <summary>歯みがきの結果の文字（「HP +12　コーティング +5」。どちらも 0 なら、きれい度で一言）。</summary>
        public static string BrushResultText(float heal, float coating, float clean01)
        {
            var healText = Mathf.RoundToInt(heal);
            var coatingText = Mathf.RoundToInt(coating);
            if (healText <= 0 && coatingText <= 0)
            {
                return clean01 > 0f ? "もう少しみがこう！" : "みがけなかった…";
            }

            var parts = new System.Collections.Generic.List<string>();
            if (healText > 0)
            {
                parts.Add($"HP +{healText}");
            }

            if (coatingText > 0)
            {
                parts.Add($"コーティング +{coatingText}");
            }

            return string.Join("　", parts);
        }

        /// <summary>リザルトの一言（終わった理由と、行けたウェーブ）。</summary>
        public static string ResultSubtitle(MatchEndReason reason, int reachedWave, int waveCount)
        {
            switch (reason)
            {
                case MatchEndReason.TimeUp:
                    return waveCount > 0 ? $"時間切れ… WAVE {reachedWave} まで行けた！" : "時間切れ…";
                case MatchEndReason.CoreDestroyed:
                    return waveCount > 0 ? $"コアが壊されてしまった…（WAVE {reachedWave}）" : "コアが壊されてしまった…";
                case MatchEndReason.AllWavesCleared:
                    return $"全 {waveCount} ウェーブ クリア！";
                default:
                    return "";
            }
        }
    }
}
