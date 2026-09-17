using MS2026.GripInputBridge.Data;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.GripInputBridge.Tests.EditMode
{
    [TestFixture]
    public class GripCalibrationProfileTests
    {
        [Test]
        public void Normalize_BelowDeadZone_ReturnsZero()
        {
            var profile = GripCalibrationProfile.CreateDefault(0);
            profile.deadZone = 0.05f;

            Assert.AreEqual(0f, profile.Normalize(0.05f));
            Assert.AreEqual(0f, profile.Normalize(0.02f));
        }

        [Test]
        public void Normalize_LinearDefault_MapsRawDirectlyAboveDeadZone()
        {
            var profile = GripCalibrationProfile.CreateDefault(0);

            Assert.AreEqual(0.5f, profile.Normalize(0.5f), 1e-5f);
            Assert.AreEqual(1f, profile.Normalize(1f), 1e-5f);
        }

        [Test]
        public void Normalize_UsesMinMaxRange_ForPersonalCalibration()
        {
            // 大人(最大握力0.8)と子供(最大握力0.3)の相対値化を想定したテスト。
            var adult = GripCalibrationProfile.CreateDefault(0);
            adult.minRawValue = 0f;
            adult.maxRawValue = 0.8f;

            var child = GripCalibrationProfile.CreateDefault(1);
            child.minRawValue = 0f;
            child.maxRawValue = 0.3f;

            // 大人が0.4(自分の最大の半分)握ったとき、子供は0.15(自分の最大の半分)握れば同じ正規化値になるはず。
            Assert.AreEqual(adult.Normalize(0.4f), child.Normalize(0.15f), 1e-4f);
        }

        [Test]
        public void Normalize_ClampsAboveMax()
        {
            var profile = GripCalibrationProfile.CreateDefault(0);
            profile.maxRawValue = 0.5f;

            Assert.AreEqual(1f, profile.Normalize(0.9f), 1e-5f);
        }

        [Test]
        public void Normalize_ZeroRange_DoesNotThrow_AndReturnsZero()
        {
            var profile = GripCalibrationProfile.CreateDefault(0);
            profile.minRawValue = 0.5f;
            profile.maxRawValue = 0.5f; // 較正未実施(max==min)の異常値を想定

            Assert.AreEqual(0f, profile.Normalize(0.5f));
            Assert.AreEqual(0f, profile.Normalize(0.9f));
        }

        [Test]
        public void Normalize_AppliesResponseCurve()
        {
            var profile = GripCalibrationProfile.CreateDefault(0);
            profile.deadZone = 0f;
            // 中間(0.5)を0.9に強調するカーブ(「渾身」域を出しやすくする調整の例)。
            profile.responseCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.5f, 0.9f),
                new Keyframe(1f, 1f));

            Assert.Greater(profile.Normalize(0.5f), 0.8f);
        }
    }
}
