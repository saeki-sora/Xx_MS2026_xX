using UnityEngine;

namespace MS2026.Fortress
{
    /// <summary>とどめの段階（<see cref="FortressBoss.FinaleStage"/>）。</summary>
    public enum BossFinaleStage
    {
        /// <summary>とどめの外。</summary>
        None = 0,

        /// <summary>「全員で握れ！」4色のゲージを溜めている。</summary>
        Gauges = 1,

        /// <summary>4本のレーザーが1点に集まり、太い1本がボスへ伸びている（カメラはゆっくりボスへ寄る）。</summary>
        Merge = 2,

        /// <summary>当たった瞬間の止め（ヒットストップ・光の点滅）。</summary>
        Stop = 3,

        /// <summary>スローで爆発している。</summary>
        Slow = 4,

        /// <summary>カメラが4人とコアを映す絵へ戻っている。</summary>
        Return = 5,

        /// <summary>4人とコアを映したまま見せている。</summary>
        Hold = 6,

        /// <summary>終わった（試合のクリアへ）。</summary>
        Done = 7
    }

    /// <summary>
    /// とどめの計算（Unity の物を使わない。テストあり。2026-11-25 見本 E）。
    /// ゲージ: 握らなくても <see cref="BossDefinition.gaugeMaxSeconds"/> で満タン、いちばん強く握ると <see cref="BossDefinition.gaugeFullGripSeconds"/> で満タン。
    /// 流れ（集まり始めてからの実時間）: 合体 → 止め → スロー → 4人へ戻る → 見せる → 終わり。
    /// </summary>
    public static class BossFinaleMath
    {
        /// <summary>握りの強さ grip01（0〜1）のときに、1秒で溜まるゲージの量（満タン = 1）。</summary>
        public static float GaugeRate(float grip01, float maxSeconds, float fullGripSeconds)
        {
            var slow = 1f / Mathf.Max(0.1f, maxSeconds);
            var fast = 1f / Mathf.Max(0.05f, Mathf.Min(fullGripSeconds, maxSeconds));
            return Mathf.Lerp(slow, fast, Mathf.Clamp01(grip01));
        }

        /// <summary>集まり始めてから t 秒の段階。</summary>
        public static BossFinaleStage StageAt(float t, BossDefinition def)
        {
            if (t < 0f)
            {
                return BossFinaleStage.Gauges;
            }

            var hit = BossDefinition.MergeToHitSeconds;
            if (t < hit)
            {
                return BossFinaleStage.Merge;
            }

            var stopEnd = hit + Mathf.Max(0f, def.finaleHitStopSeconds);
            if (t < stopEnd)
            {
                return BossFinaleStage.Stop;
            }

            var slowEnd = stopEnd + Mathf.Max(0f, def.finaleSlowSeconds);
            if (t < slowEnd)
            {
                return BossFinaleStage.Slow;
            }

            var returnEnd = slowEnd + Mathf.Max(0f, def.finaleReturnSeconds);
            if (t < returnEnd)
            {
                return BossFinaleStage.Return;
            }

            return t < returnEnd + Mathf.Max(0f, def.finaleHoldSeconds) ? BossFinaleStage.Hold : BossFinaleStage.Done;
        }

        /// <summary>集まり始めてから t 秒のゲームの速さ（止めは 0、スローは決めた速さ、その後 0.3 秒でなめらかに 1 へ）。</summary>
        public static float TimeScaleAt(float t, BossDefinition def)
        {
            var hit = BossDefinition.MergeToHitSeconds;
            var stopEnd = hit + Mathf.Max(0f, def.finaleHitStopSeconds);
            var slowEnd = stopEnd + Mathf.Max(0f, def.finaleSlowSeconds);
            if (t < hit)
            {
                return 1f;
            }

            if (t < stopEnd)
            {
                return 0f;
            }

            var slow = Mathf.Clamp(def.finaleSlowScale, 0.01f, 1f);
            if (t < slowEnd)
            {
                return slow;
            }

            return Mathf.Lerp(slow, 1f, Mathf.Clamp01((t - slowEnd) / 0.3f));
        }

        /// <summary>
        /// 当たってから t 秒の光（正 = 白、負 = 暗）。白→暗→白と短く点滅して消える（桜井さんの原則: 白一色でなく明暗を交互に、寄っているので弱め）。
        /// </summary>
        public static float FlashAt(float sinceHit, float strength)
        {
            if (sinceHit < 0f || sinceHit > 0.34f)
            {
                return 0f;
            }

            var v = sinceHit < 0.06f ? 1f : sinceHit < 0.12f ? -0.6f : sinceHit < 0.18f ? 0.7f : -0.4f * (1f - (sinceHit - 0.18f) / 0.16f);
            return v * Mathf.Clamp01(strength);
        }

        /// <summary>当たってから t 秒の、画面のまわりの暗さ（止めとスローの間だけ。戻り始めたら消える）。</summary>
        public static float DimAt(float sinceHit, BossDefinition def)
        {
            if (sinceHit < 0f)
            {
                return 0f;
            }

            var end = Mathf.Max(0f, def.finaleHitStopSeconds) + Mathf.Max(0f, def.finaleSlowSeconds);
            return sinceHit < end ? 0.45f : Mathf.Max(0f, 0.45f * (1f - (sinceHit - end) / 0.5f));
        }

        /// <summary>カメラがボスへ寄り始める時刻（集まり始めてから。当たる 0.6 秒前）。</summary>
        public static float ZoomInStart => BossDefinition.MergeToHitSeconds - 0.6f;

        /// <summary>カメラがボスへ寄る秒数（当たって 0.3 秒後まで）。</summary>
        public const float ZoomInSeconds = 0.9f;
    }
}
