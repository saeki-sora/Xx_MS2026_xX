using MS2026.GripInputBridge.Data;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripDeviceConfigTests
    {
        private GripDeviceConfig _config;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<GripDeviceConfig>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void ToGripRawValue_CenteredSensorOff_ReturnsInputUnchanged(float value)
        {
            _config.centeredSensor = false;

            Assert.AreEqual(value, _config.ToGripRawValue(value), 1e-5f);
        }

        // 49E等のリニアホールセンサー: 磁石なしで中心(0.5)、N極/S極で0と1の両方向に振れる。
        [TestCase(0.5f, 0f)]
        [TestCase(0.75f, 0.5f)]
        [TestCase(0.25f, 0.5f)]
        [TestCase(1f, 1f)]
        [TestCase(0f, 1f)]
        public void ToGripRawValue_CenteredSensor_ReturnsDistanceFromCenter(float value, float expected)
        {
            _config.centeredSensor = true;
            _config.sensorCenter01 = 0.5f;

            Assert.AreEqual(expected, _config.ToGripRawValue(value), 1e-5f);
        }

        [Test]
        public void ToGripRawValue_OffCenter_ScalesByLongerSide()
        {
            _config.centeredSensor = true;
            _config.sensorCenter01 = 0.6f;

            // 中心から遠い側(0まで0.6)を1とする。反対側(1まで0.4)は端でも1に届かない。
            Assert.AreEqual(1f, _config.ToGripRawValue(0f), 1e-5f);
            Assert.AreEqual(0.4f / 0.6f, _config.ToGripRawValue(1f), 1e-5f);
        }
    }
}
