using System;
using MS2026.GripInputBridge.Processing;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripEdgeDetectorTests
    {
        [Test]
        public void Update_CrossingOnThreshold_ReturnsTrue_AndSetsGripping()
        {
            var detector = new GripEdgeDetector(onThreshold: 0.12f, offThreshold: 0.08f);

            var changed = detector.Update(0.5f);

            Assert.IsTrue(changed);
            Assert.IsTrue(detector.IsGripping);
        }

        [Test]
        public void Update_StayingAboveOffThreshold_DoesNotRefire()
        {
            var detector = new GripEdgeDetector(onThreshold: 0.12f, offThreshold: 0.08f);
            detector.Update(0.5f);

            // ヒステリシスの"のりしろ"(0.08〜0.12)の中に留まっている間は状態が変わらないはず。
            var changed = detector.Update(0.10f);

            Assert.IsFalse(changed);
            Assert.IsTrue(detector.IsGripping);
        }

        [Test]
        public void Update_DroppingBelowOffThreshold_ReturnsTrue_AndClearsGripping()
        {
            var detector = new GripEdgeDetector(onThreshold: 0.12f, offThreshold: 0.08f);
            detector.Update(0.5f);

            var changed = detector.Update(0.05f);

            Assert.IsTrue(changed);
            Assert.IsFalse(detector.IsGripping);
        }

        [Test]
        public void Update_HysteresisPreventsChattering_AroundMidpoint()
        {
            // 単純な単一しきい値(例: 0.1)だったら反転してしまうような、境界付近の細かい上下動を想定。
            var detector = new GripEdgeDetector(onThreshold: 0.12f, offThreshold: 0.08f);

            detector.Update(0.5f); // 握った
            var changedAtMidpoint1 = detector.Update(0.10f);
            var changedAtMidpoint2 = detector.Update(0.09f);
            var changedAtMidpoint3 = detector.Update(0.11f);

            Assert.IsFalse(changedAtMidpoint1);
            Assert.IsFalse(changedAtMidpoint2);
            Assert.IsFalse(changedAtMidpoint3);
            Assert.IsTrue(detector.IsGripping);
        }

        [Test]
        public void Reset_ClearsGrippingState()
        {
            var detector = new GripEdgeDetector(onThreshold: 0.12f, offThreshold: 0.08f);
            detector.Update(0.5f);

            detector.Reset();

            Assert.IsFalse(detector.IsGripping);
        }

        [Test]
        public void Constructor_OnThresholdBelowOffThreshold_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => new GripEdgeDetector(onThreshold: 0.05f, offThreshold: 0.1f));
        }
    }
}
