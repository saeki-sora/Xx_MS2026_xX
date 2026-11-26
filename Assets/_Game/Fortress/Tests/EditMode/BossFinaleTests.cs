using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    /// <summary>ボスのとどめの計算（<see cref="BossFinaleMath"/>。2026-11-25 見本 E）。</summary>
    public sealed class BossFinaleTests
    {
        private static BossDefinition Def()
        {
            var def = ScriptableObject.CreateInstance<BossDefinition>();
            def.gaugeMaxSeconds = 8f;
            def.gaugeFullGripSeconds = 2.5f;
            def.finaleHitStopSeconds = 0.35f;
            def.finaleSlowSeconds = 0.9f;
            def.finaleSlowScale = 0.25f;
            def.finaleReturnSeconds = 0.7f;
            def.finaleHoldSeconds = 1.4f;
            return def;
        }

        [Test]
        public void GaugeFillsByItselfAtMaxSecondsAndFastWhenGrippingHard()
        {
            Assert.AreEqual(1f / 8f, BossFinaleMath.GaugeRate(0f, 8f, 2.5f), 1e-5f, "握らなくても満タンになる（失敗しない）");
            Assert.AreEqual(1f / 2.5f, BossFinaleMath.GaugeRate(1f, 8f, 2.5f), 1e-5f, "いちばん強く握ると速い");
            Assert.Greater(BossFinaleMath.GaugeRate(0.5f, 8f, 2.5f), BossFinaleMath.GaugeRate(0.2f, 8f, 2.5f), "強く握るほど速い");
        }

        [Test]
        public void StagesFollowTheDecidedOrder()
        {
            var def = Def();
            var hit = BossDefinition.MergeToHitSeconds;
            Assert.AreEqual(BossFinaleStage.Gauges, BossFinaleMath.StageAt(-1f, def));
            Assert.AreEqual(BossFinaleStage.Merge, BossFinaleMath.StageAt(0.1f, def));
            Assert.AreEqual(BossFinaleStage.Stop, BossFinaleMath.StageAt(hit + 0.1f, def));
            Assert.AreEqual(BossFinaleStage.Slow, BossFinaleMath.StageAt(hit + 0.5f, def));
            Assert.AreEqual(BossFinaleStage.Return, BossFinaleMath.StageAt(hit + 0.35f + 0.9f + 0.1f, def));
            Assert.AreEqual(BossFinaleStage.Hold, BossFinaleMath.StageAt(hit + 0.35f + 0.9f + 0.7f + 0.1f, def));
            Assert.AreEqual(BossFinaleStage.Done, BossFinaleMath.StageAt(def.FinaleShowSeconds + 0.01f, def));
        }

        [Test]
        public void TimeStopsAtTheHitThenSlowsThenComesBack()
        {
            var def = Def();
            var hit = BossDefinition.MergeToHitSeconds;
            Assert.AreEqual(1f, BossFinaleMath.TimeScaleAt(0.5f, def), 1e-5f, "合体の間はふつう");
            Assert.AreEqual(0f, BossFinaleMath.TimeScaleAt(hit + 0.1f, def), 1e-5f, "当たった瞬間は止め");
            Assert.AreEqual(0.25f, BossFinaleMath.TimeScaleAt(hit + 0.6f, def), 1e-5f, "その後スロー");
            Assert.AreEqual(1f, BossFinaleMath.TimeScaleAt(hit + 0.35f + 0.9f + 0.31f, def), 1e-5f, "なめらかに元へ");
        }

        [Test]
        public void FlashAlternatesWhiteAndDarkAndRespectsStrength()
        {
            Assert.Greater(BossFinaleMath.FlashAt(0.02f, 1f), 0f, "最初は白");
            Assert.Less(BossFinaleMath.FlashAt(0.09f, 1f), 0f, "次に暗");
            Assert.Greater(BossFinaleMath.FlashAt(0.15f, 1f), 0f, "また白");
            Assert.AreEqual(0f, BossFinaleMath.FlashAt(0.5f, 1f), 1e-5f, "すぐ消える");
            Assert.AreEqual(0f, BossFinaleMath.FlashAt(0.02f, 0f), 1e-5f, "強さ 0 なら光らない");
            Assert.AreEqual(0.5f * BossFinaleMath.FlashAt(0.02f, 1f), BossFinaleMath.FlashAt(0.02f, 0.5f), 1e-5f);
        }

        [Test]
        public void ZoomStartsBeforeTheHit()
        {
            Assert.Less(BossFinaleMath.ZoomInStart, BossDefinition.MergeToHitSeconds, "ビームが伸びている間から、ゆっくり寄り始める");
            Assert.Greater(BossFinaleMath.ZoomInStart + BossFinaleMath.ZoomInSeconds, BossDefinition.MergeToHitSeconds, "当たった後に寄り終わる");
        }
    }
}
