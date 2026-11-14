using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Cpu.Tests.EditMode
{
    public sealed class CpuSkillMathTests
    {
        [Test]
        public void ConeValue_CoversWidthAndSweepInRotationDirection()
        {
            var w = new float[60];
            w[59] = 1f; // 354〜360 度（今の向きの少し後ろ）
            w[0] = 2f;  // 0〜6 度（今の向き）
            w[5] = 4f;  // 30〜36 度（回った先）
            w[20] = 8f; // 120〜126 度（遠すぎる）
            // 反時計回り、半分の幅 6 度、回る分 30 度 → 357〜393 度（＝ -3〜33 度）。
            Assert.AreEqual(7f, CpuSkillMath.ConeValue(w, 3f, +1f, 6f, 30f), 1e-4f);
            // 回る分が無ければ今の向きのまわりだけ。
            Assert.AreEqual(3f, CpuSkillMath.ConeValue(w, 3f, +1f, 6f, 0f), 1e-4f);
        }

        [Test]
        public void LineValue_CurrentPlusHalfNeighbours()
        {
            var w = new float[60];
            w[0] = 4f;
            w[1] = 2f;
            w[59] = 2f;
            Assert.AreEqual(6f, CpuSkillMath.LineValue(w, 3f), 1e-4f);
        }

        [Test]
        public void ShouldFire_ThresholdDropsWithWaiting()
        {
            // 待ち始めは 0.6 が要る。
            Assert.IsFalse(CpuSkillMath.ShouldFire(0.4f, 0f, 4f, 0.6f, 0.1f));
            Assert.IsTrue(CpuSkillMath.ShouldFire(0.7f, 0f, 4f, 0.6f, 0.1f));
            // 半分待つと 0.35 で撃つ。
            Assert.IsTrue(CpuSkillMath.ShouldFire(0.4f, 2f, 4f, 0.6f, 0.1f));
            // 待ちきっても min より低ければ撃たない。
            Assert.IsFalse(CpuSkillMath.ShouldFire(0.05f, 10f, 4f, 0.6f, 0.1f));
        }

        [Test]
        public void ShouldFire_NoPatience_FiresAtMin()
        {
            Assert.IsTrue(CpuSkillMath.ShouldFire(0.1f, 0f, 0f, 0.9f, 0.1f));
            Assert.IsFalse(CpuSkillMath.ShouldFire(0.05f, 0f, 0f, 0.9f, 0.1f));
        }

        [Test]
        public void ShouldFire_MinZero_FiresEvenWithNothingAfterWaiting()
        {
            Assert.IsFalse(CpuSkillMath.ShouldFire(0f, 1f, 4f, 0.6f, 0f));
            Assert.IsTrue(CpuSkillMath.ShouldFire(0f, 4f, 4f, 0.6f, 0f));
        }

        [Test]
        public void HoldSecondsFor_LinearBetweenMinAndMax()
        {
            Assert.AreEqual(0f, CpuSkillMath.HoldSecondsFor(3f, 3f, 12f, 1f), 1e-5f);
            Assert.AreEqual(0.5f, CpuSkillMath.HoldSecondsFor(7.5f, 3f, 12f, 1f), 1e-5f);
            Assert.AreEqual(1f, CpuSkillMath.HoldSecondsFor(20f, 3f, 12f, 1f), 1e-5f);
            Assert.AreEqual(0f, CpuSkillMath.HoldSecondsFor(7f, 3f, 3f, 1f), 1e-5f);
        }
    }

    public sealed class CpuSightTests
    {
        [Test]
        public void CountNear_SumsCellsAroundPoint()
        {
            var sight = new CpuSight(4, 4) { Origin = Vector2.zero, Range = 8f }; // 扇 90 度、輪 2 m
            // 扇 0（0〜90 度）の輪 1（2〜4 m）: 真ん中は 45 度・3 m。
            sight.Weights[0] = 5f;
            sight.RingWeights[0 * 4 + 1] = 5f;
            var center = sight.CellCenter(0, 1);
            Assert.AreEqual(3f, center.magnitude, 1e-4f);
            Assert.AreEqual(5f, sight.CountNear(center, 0.5f), 1e-4f);
            Assert.AreEqual(0f, sight.CountNear(-center, 0.5f), 1e-4f);
        }

        [Test]
        public void TryLocate_ReturnsSectorAndDistance()
        {
            var view = new CpuView(Vector2.zero, new Vector2(10f, 10f), Vector2.up);
            Assert.IsTrue(CpuSectorMath.TryLocate(4, null, Vector2.zero, 20f, true, view, new Vector2(0f, 3f), out var sector, out var distance));
            Assert.AreEqual(1, sector);
            Assert.AreEqual(3f, distance, 1e-4f);
            Assert.IsFalse(CpuSectorMath.TryLocate(4, new[] { 1f, 1f, 1f, 1f }, Vector2.zero, 20f, false, view, new Vector2(0f, 3f), out sector, out _));
            Assert.AreEqual(-1, sector);
        }
    }

    public sealed class CpuBallMathTests
    {
        private static CpuBallSight Ball(float angle, float distance, bool inLine = true) => new CpuBallSight
        {
            Angle = angle, Distance = distance, Radius = 0.5f, InLine = inLine, Health01 = 1f
        };

        [Test]
        public void OnTarget_WhenAimInsideBallWidth()
        {
            var aim = CpuBallMath.Pick(new List<CpuBallSight> { Ball(10f, 5f) }, 8f, +1f, 45f);
            Assert.IsTrue(aim.Found);
            Assert.IsTrue(aim.OnTarget);
        }

        [Test]
        public void TimeUntil_FollowsRotationDirection()
        {
            var balls = new List<CpuBallSight> { Ball(90f, 10f) };
            var ccw = CpuBallMath.Pick(balls, 0f, +1f, 45f);
            var cw = CpuBallMath.Pick(balls, 0f, -1f, 45f);
            Assert.IsTrue(ccw.Found && !ccw.OnTarget);
            Assert.Less(ccw.SecondsUntil, 2.1f);
            Assert.Greater(cw.SecondsUntil, 5.5f); // 時計回りだと 270 度近く回らないと着かない
        }

        [Test]
        public void BlockedBallsAndStillTurret_AreIgnored()
        {
            Assert.IsFalse(CpuBallMath.Pick(new List<CpuBallSight> { Ball(10f, 5f, inLine: false) }, 9f, +1f, 45f).Found);
            Assert.IsFalse(CpuBallMath.Pick(new List<CpuBallSight> { Ball(90f, 5f) }, 0f, 0f, 45f).Found);
        }
    }

    public sealed class CpuBrushRhythmTests
    {
        [Test]
        public void PressesAtSteadyRate()
        {
            var rhythm = new CpuBrushRhythm();
            var presses = 0;
            var was = false;
            for (var t = 0f; t < 4f; t += 0.01f)
            {
                var pressed = rhythm.Tick(t, 0.5f, 0.5f);
                if (pressed && !was)
                {
                    presses++;
                }

                was = pressed;
            }

            Assert.That(presses, Is.InRange(7, 9)); // 4 秒で 1 秒 2 回
        }

        [Test]
        public void Follow_PullsNextPressEarlier()
        {
            var rhythm = new CpuBrushRhythm();
            rhythm.Tick(0f, 1f, 0.3f);   // 0 秒で握る
            rhythm.Tick(0.4f, 1f, 0.3f); // 0.3 秒で離した
            Assert.IsFalse(rhythm.Pressed);

            rhythm.Follow(0.6f, 1f); // ほかの人に合わせて 0.6 秒に
            Assert.IsFalse(rhythm.Tick(0.55f, 1f, 0.3f));
            Assert.IsTrue(rhythm.Tick(0.61f, 1f, 0.3f));
        }

        [Test]
        public void Follow_IgnoredWhileStillTooSoon()
        {
            var rhythm = new CpuBrushRhythm();
            rhythm.Tick(0f, 1f, 0.3f);
            rhythm.Tick(0.31f, 1f, 0.3f); // 0.3 秒で離した
            rhythm.Follow(0.35f, 1f);     // 離してすぐ（0.3 + 0.2 秒より前）は合わせない
            Assert.IsFalse(rhythm.Tick(0.4f, 1f, 0.3f));
        }
    }
}
