using System;
using MS2026.GripInputBridge.Data;
using MS2026.GripInputBridge.Processing;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class OneEuroFilterTests
    {
        private double _now;

        private double Clock()
        {
            return _now;
        }

        private OneEuroFilter CreateDefault()
        {
            return new OneEuroFilter(1.5f, 2f, 1f, Clock);
        }

        [SetUp]
        public void SetUp()
        {
            _now = 10d;
        }

        [Test]
        public void FirstSample_ReturnsThatSampleAsIs()
        {
            var filter = CreateDefault();

            Assert.AreEqual(0.7f, filter.Filter(0.7f), 1e-5f);
        }

        [Test]
        public void ConstantInput_StaysConstant()
        {
            var filter = CreateDefault();
            var result = 0f;
            for (var i = 0; i < 120; i++)
            {
                result = filter.Filter(0.4f);
                _now += 1d / 60d;
            }

            Assert.AreEqual(0.4f, result, 1e-5f);
        }

        [Test]
        public void SameTime_ReturnsPreviousValue()
        {
            var filter = CreateDefault();
            filter.Filter(0.2f);

            // 時間が進んでいないので、速さが計算できない。前の値のまま。
            Assert.AreEqual(0.2f, filter.Filter(0.9f), 1e-5f);
        }

        [Test]
        public void SmallJitterWhileStill_IsMostlyRemoved()
        {
            var filter = CreateDefault();
            var maxDeviation = 0f;
            for (var i = 0; i < 120; i++)
            {
                var raw = 0.5f + (i % 2 == 0 ? -0.02f : 0.02f);
                var result = filter.Filter(raw);
                if (i >= 60)
                {
                    maxDeviation = Mathf.Max(maxDeviation, Mathf.Abs(result - 0.5f));
                }

                _now += 1d / 60d;
            }

            // 生の値は ±0.02 震えている。1€を通すと 0.005 未満に収まる。
            Assert.Less(maxDeviation, 0.005f);
        }

        [Test]
        public void FastSqueeze_ReachesTargetSoonerThanEma()
        {
            var oneEuroSeconds = SecondsToReach90Percent(CreateDefault(), 60);
            var emaSeconds = SecondsToReach90Percent(new ExponentialMovingAverageFilter(0.3f), 60);

            Assert.Less(oneEuroSeconds, emaSeconds);
        }

        [Test]
        public void FrameRateDifference_ChangesResponseLessThanEma()
        {
            var oneEuroSpread = Math.Abs(
                SecondsToReach90Percent(CreateDefault(), 60) - SecondsToReach90Percent(CreateDefault(), 144));
            var emaSpread = Math.Abs(
                SecondsToReach90Percent(new ExponentialMovingAverageFilter(0.3f), 60) -
                SecondsToReach90Percent(new ExponentialMovingAverageFilter(0.3f), 144));

            // EMA は1サンプルごとに効くので、144fps と 60fps で反応の速さが大きく違う。1€は経過時間で効くので差が小さい。
            Assert.Less(oneEuroSpread, emaSpread * 0.5d);
        }

        [Test]
        public void Reset_ForgetsPreviousValue()
        {
            var filter = CreateDefault();
            filter.Filter(0.1f);
            _now += 0.1d;

            filter.Reset();

            Assert.AreEqual(0.9f, filter.Filter(0.9f), 1e-5f);
        }

        [Test]
        public void MedianThenOneEuro_RejectsSingleSpike()
        {
            var filter = new MedianThenOneEuroFilter(3, 1.5f, 2f, 1f, Clock);
            filter.Filter(0.2f);
            _now += 1d / 60d;
            filter.Filter(0.2f);
            _now += 1d / 60d;

            var result = filter.Filter(0.9f); // 単発スパイク

            Assert.AreEqual(0.2f, result, 1e-5f);
        }

        [Test]
        public void InvalidParameters_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuroFilter(0f, 2f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuroFilter(1.5f, -1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new OneEuroFilter(1.5f, 2f, 0f));
        }

        [TestCase(GripFilterType.OneEuro, typeof(OneEuroFilter))]
        [TestCase(GripFilterType.MedianThenOneEuro, typeof(MedianThenOneEuroFilter))]
        [TestCase(GripFilterType.None, typeof(PassthroughFilter))]
        public void Factory_CreatesFilterForNewTypes(GripFilterType type, Type expected)
        {
            var settings = ScriptableObject.CreateInstance<GripFilterSettings>();
            try
            {
                settings.filterType = type;

                Assert.IsInstanceOf(expected, GripFilterFactory.Create(settings, Clock));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        // 0 から 1 へぐっと握ったとき、値が 0.9 に届くまでの秒数。
        private double SecondsToReach90Percent(IGripSignalFilter filter, int framesPerSecond)
        {
            _now = 10d;
            var dt = 1d / framesPerSecond;
            filter.Filter(0f);
            for (var frame = 1; frame <= framesPerSecond * 2; frame++)
            {
                _now += dt;
                if (filter.Filter(1f) >= 0.9f)
                {
                    return frame * dt;
                }
            }

            return double.PositiveInfinity;
        }
    }
}
