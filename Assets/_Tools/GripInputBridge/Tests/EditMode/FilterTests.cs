using MS2026.GripInputBridge.Processing;
using NUnit.Framework;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class FilterTests
    {
        [Test]
        public void MovingAverageFilter_AveragesWithinWindow()
        {
            var filter = new MovingAverageFilter(3);

            Assert.AreEqual(1f, filter.Filter(1f), 1e-5f);
            Assert.AreEqual(1.5f, filter.Filter(2f), 1e-5f);
            Assert.AreEqual(2f, filter.Filter(3f), 1e-5f); // (1+2+3)/3
            Assert.AreEqual(3f, filter.Filter(4f), 1e-5f); // 直近3件(2,3,4)の平均
        }

        [Test]
        public void MovingAverageFilter_Reset_ClearsHistory()
        {
            var filter = new MovingAverageFilter(3);
            filter.Filter(1f);
            filter.Filter(1f);

            filter.Reset();

            Assert.AreEqual(5f, filter.Filter(5f), 1e-5f);
        }

        [Test]
        public void ExponentialMovingAverageFilter_FirstSample_ReturnsThatSampleAsIs()
        {
            var filter = new ExponentialMovingAverageFilter(0.3f);

            Assert.AreEqual(0.8f, filter.Filter(0.8f), 1e-5f);
        }

        [Test]
        public void ExponentialMovingAverageFilter_SmoothsTowardNewValue()
        {
            var filter = new ExponentialMovingAverageFilter(0.5f);
            filter.Filter(0f);

            var result = filter.Filter(1f);

            Assert.AreEqual(0.5f, result, 1e-5f); // 0 + 0.5*(1-0)
        }

        [Test]
        public void MedianFilter_RejectsSingleSpike()
        {
            var filter = new MedianFilter(3);
            filter.Filter(0.2f);
            filter.Filter(0.2f);

            var result = filter.Filter(0.9f); // 単発スパイク

            Assert.AreEqual(0.2f, result, 1e-5f); // 中央値は0.2のまま
        }

        [Test]
        public void MedianThenEmaFilter_RejectsSpike_MoreThanRawValue()
        {
            var filter = new MedianThenEmaFilter(medianWindowSize: 3, emaAlpha: 0.3f);
            filter.Filter(0.2f);
            filter.Filter(0.2f);

            var result = filter.Filter(0.9f);

            Assert.Less(result, 0.9f);
        }

        [Test]
        public void GripFilterFactory_NullSettings_ReturnsPassthrough()
        {
            var filter = GripFilterFactory.Create(null);

            Assert.AreEqual(0.42f, filter.Filter(0.42f), 1e-5f);
        }
    }
}
