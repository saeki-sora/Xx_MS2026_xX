using NUnit.Framework;
using UnityEngine;

namespace MS2026.Cpu.Tests.EditMode
{
    public sealed class CpuSectorMathTests
    {
        [Test]
        public void SectorOf_WrapsAngles()
        {
            Assert.AreEqual(0, CpuSectorMath.SectorOf(0f, 60));
            Assert.AreEqual(0, CpuSectorMath.SectorOf(5.9f, 60));
            Assert.AreEqual(1, CpuSectorMath.SectorOf(6.1f, 60));
            Assert.AreEqual(59, CpuSectorMath.SectorOf(-1f, 60));
            Assert.AreEqual(59, CpuSectorMath.SectorOf(359.99f, 60));
            Assert.AreEqual(0, CpuSectorMath.SectorOf(720f, 60));
        }

        [Test]
        public void SumAhead_CounterClockwiseLooksAtLargerAngles()
        {
            var w = new float[60];
            w[0] = 1f;  // 0〜6 度（今の向き）
            w[2] = 5f;  // 12〜18 度（今 3 度なら、反時計回りに 9 度先で入る）
            w[58] = 7f; // 348〜354 度（時計回りなら先）
            Assert.AreEqual(1f, CpuSectorMath.SumAhead(w, 3f, +1f, 0f, 5f), 1e-5f);
            Assert.AreEqual(6f, CpuSectorMath.SumAhead(w, 3f, +1f, 0f, 9f), 1e-5f);
            Assert.AreEqual(5f, CpuSectorMath.MaxAhead(w, 3f, +1f, 0f, 9f), 1e-5f);
        }

        [Test]
        public void SumAhead_ClockwiseLooksAtSmallerAngles()
        {
            var w = new float[60];
            w[0] = 1f;
            w[2] = 5f;
            w[58] = 7f;
            // 時計回り: 3 度 → 0 度で扇 59 に入り（3 度先）、354 度で扇 58 に入る（9 度先）。
            Assert.AreEqual(8f, CpuSectorMath.SumAhead(w, 3f, -1f, 0f, 9f), 1e-5f);
            Assert.AreEqual(1f, CpuSectorMath.SumAhead(w, 3f, -1f, 0f, 8f), 1e-5f);
        }

        [Test]
        public void SumAhead_NotRotating_OnlyCurrent()
        {
            var w = new float[60];
            w[0] = 2f;
            w[1] = 9f;
            Assert.AreEqual(2f, CpuSectorMath.SumAhead(w, 1f, 0f, 0f, 90f), 1e-5f);
        }

        [Test]
        public void Accumulate_RespectsRangeViewAndObstacles()
        {
            var w = new float[4]; // 90 度ずつ
            var obstacles = new[] { 3f, 100f, 100f, 100f };
            var view = new CpuView(Vector2.zero, new Vector2(10f, 10f), Vector2.up);

            Assert.IsTrue(CpuSectorMath.Accumulate(w, obstacles, Vector2.zero, 20f, true, view, new Vector2(2f, 0.5f), 1f));
            Assert.IsFalse(CpuSectorMath.Accumulate(w, obstacles, Vector2.zero, 20f, true, view, new Vector2(5f, 0.5f), 1f)); // 壁の向こう
            Assert.IsFalse(CpuSectorMath.Accumulate(w, obstacles, Vector2.zero, 20f, true, view, new Vector2(-5f, 15f), 1f)); // 画面の外
            Assert.IsFalse(CpuSectorMath.Accumulate(w, obstacles, Vector2.zero, 4f, false, view, new Vector2(-5f, 0.1f), 1f)); // 射程の外
            Assert.IsTrue(CpuSectorMath.Accumulate(w, obstacles, Vector2.zero, 20f, false, view, new Vector2(-5f, 15f), 2f)); // 範囲を使わなければ見える
            Assert.AreEqual(1f, w[0]);
            Assert.AreEqual(2f, w[1]);
        }
    }

    public sealed class CpuShootMathTests
    {
        private static CpuSkillValues Skill()
        {
            var s = CpuSkillValues.Default;
            s.startThreshold = 3f;
            s.keepThreshold = 1f;
            s.denseCount = 10f;
            s.minGrip = 0.4f;
            s.leadSeconds = 0.5f;
            s.heatCoolStart01 = 0.6f;
            s.heatPanic01 = 0.9f;
            return s;
        }

        private static CpuTurretInfoBuilder Turret() => new CpuTurretInfoBuilder();

        [Test]
        public void StartsWhenCrowdIsJustAhead()
        {
            var w = new float[60];
            w[3] = 8f; // 18〜24 度
            // 反時計回り 40 度/秒、先読み 0.5 秒 = 20 度先まで見る。今 2 度なら 16 度先で入る → 握る。
            var d = CpuShootMath.Decide(w, Turret().Aim(2f).Ccw(40f).Build(), Skill());
            Assert.IsTrue(d.Fire);
            Assert.Greater(d.Grip, 0.4f);

            // 先読みが短いと、まだ握らない。
            var s = Skill();
            s.leadSeconds = 0.1f;
            Assert.IsFalse(CpuShootMath.Decide(w, Turret().Aim(2f).Ccw(40f).Build(), s).Fire);
        }

        [Test]
        public void DoesNotStartForCrowdBehind()
        {
            var w = new float[60];
            w[58] = 20f; // 時計回りの側にいる
            Assert.IsFalse(CpuShootMath.Decide(w, Turret().Aim(2f).Ccw(40f).Build(), Skill()).Fire);
        }

        [Test]
        public void KeepsFiringWithFewerEnemies()
        {
            var w = new float[60];
            w[0] = 1.5f; // 始めるには少ないが、続けるには足りる
            Assert.IsFalse(CpuShootMath.Decide(w, Turret().Aim(2f).Ccw(40f).Build(), Skill()).Fire);
            Assert.IsTrue(CpuShootMath.Decide(w, Turret().Aim(2f).Ccw(40f).Firing().Build(), Skill()).Fire);
        }

        [Test]
        public void GripGrowsWithCrowdAndShrinksWithHeat()
        {
            var sparse = new float[60];
            sparse[0] = 3f;
            var dense = new float[60];
            dense[0] = 30f;
            var skill = Skill();
            var weak = CpuShootMath.Decide(sparse, Turret().Aim(2f).Ccw(40f).Build(), skill).Grip;
            var strong = CpuShootMath.Decide(dense, Turret().Aim(2f).Ccw(40f).Build(), skill).Grip;
            var hot = CpuShootMath.Decide(dense, Turret().Aim(2f).Ccw(40f).Heat(0.85f).Build(), skill).Grip;
            Assert.Greater(strong, weak);
            Assert.AreEqual(1f, strong, 1e-5f);
            Assert.Less(hot, strong);
            Assert.GreaterOrEqual(hot, CpuHands.GrippingThreshold);
        }

        [Test]
        public void CannotFire_NoDecision()
        {
            var w = new float[60];
            w[0] = 30f;
            Assert.IsFalse(CpuShootMath.Decide(w, Turret().Aim(2f).Ccw(40f).Blocked().Build(), Skill()).Fire);
        }

        [Test]
        public void ChargeKeepsGripHigh()
        {
            var w = new float[60];
            w[0] = 3f;
            var d = CpuShootMath.Decide(w, Turret().Aim(2f).Ccw(40f).Charge(0.95f).Build(), Skill());
            Assert.IsTrue(d.Fire);
            Assert.GreaterOrEqual(d.Grip, 0.95f);
        }

        [Test]
        public void CoolScore_RisesWithHeat_PanicBeatsShooting()
        {
            var s = Skill();
            Assert.AreEqual(0f, CpuShootMath.CoolScore(0.5f, false, s));
            var mid = CpuShootMath.CoolScore(0.75f, false, s);
            Assert.Greater(mid, 0f);
            Assert.Less(mid, CpuShootMath.ShootBaseScore + CpuShootMath.ShootValueScore);
            Assert.Greater(CpuShootMath.CoolScore(0.95f, false, s), CpuShootMath.ShootBaseScore + CpuShootMath.ShootValueScore);

            // 危ないから冷まし始めたら、冷まし始めの 8 割まで下がるまで続ける。
            Assert.AreEqual(CpuShootMath.PanicScore, CpuShootMath.CoolScore(0.55f, true, s));
            Assert.AreEqual(0f, CpuShootMath.CoolScore(0.4f, true, s));
        }

        [Test]
        public void CoolScore_NeverPanicsWhenPanicAboveOne()
        {
            var s = Skill();
            s.heatPanic01 = 1.1f;
            Assert.Less(CpuShootMath.CoolScore(0.99f, false, s), CpuShootMath.PanicScore);
        }

        [Test]
        public void SparseTargetLosesToCooling_DenseTargetWins()
        {
            var s = Skill();
            var cool = CpuShootMath.CoolScore(0.8f, false, s);
            var sparse = new float[60];
            sparse[0] = 3f;
            var dense = new float[60];
            dense[0] = 10f;
            var t = Turret().Aim(2f).Ccw(40f).Heat(0.8f).Build();
            Assert.Less(CpuShootMath.ShootScore(CpuShootMath.Decide(sparse, t, s)), cool);
            Assert.Greater(CpuShootMath.ShootScore(CpuShootMath.Decide(dense, t, s)), cool);
        }
    }

    public sealed class CpuHandsHumanTests
    {
        [Test]
        public void CannotToggleFasterThanMinInterval()
        {
            var hands = new CpuHands(0) { MinToggleSeconds = 0.2f };
            hands.Hold(1f);
            hands.Tick(0.01f);
            Assert.IsTrue(hands.IsGripping);

            hands.Release();
            hands.Tick(0.05f); // まだ離せない
            Assert.IsTrue(hands.IsGripping);
            hands.Tick(0.2f);
            Assert.IsFalse(hands.IsGripping);
        }

        [Test]
        public void JitterStaysWithinBounds()
        {
            var hands = new CpuHands(0, seed: 5) { Jitter = 0.1f };
            hands.Hold(0.6f);
            for (var i = 0; i < 500; i++)
            {
                hands.Tick(0.016f);
                Assert.That(hands.Grip01, Is.InRange(0.5f - 1e-4f, 0.7f + 1e-4f));
            }
        }

        [Test]
        public void SameSeedSameJitter()
        {
            var a = new CpuHands(0, seed: 9) { Jitter = 0.1f };
            var b = new CpuHands(0, seed: 9) { Jitter = 0.1f };
            a.Hold(0.6f);
            b.Hold(0.6f);
            for (var i = 0; i < 50; i++)
            {
                a.Tick(0.016f);
                b.Tick(0.016f);
            }

            Assert.AreEqual(a.Grip01, b.Grip01);
        }
    }

    /// <summary>テスト用に砲台の様子を作る。</summary>
    internal sealed class CpuTurretInfoBuilder
    {
        private float _aim;
        private float _sign;
        private float _speed;
        private bool _firing;
        private float _heat;
        private bool _blocked;
        private float _charge = -1f;

        public CpuTurretInfoBuilder Aim(float degrees) { _aim = degrees; return this; }
        public CpuTurretInfoBuilder Ccw(float speed) { _sign = 1f; _speed = speed; return this; }
        public CpuTurretInfoBuilder Firing() { _firing = true; return this; }
        public CpuTurretInfoBuilder Heat(float heat01) { _heat = heat01; return this; }
        public CpuTurretInfoBuilder Blocked() { _blocked = true; return this; }
        public CpuTurretInfoBuilder Charge(float grip) { _charge = grip; return this; }

        public CpuTurretInfo Build() => CpuTurretInfo.ForTest(!_blocked, _firing, false, _charge >= 0f, Mathf.Max(0f, _charge),
            _heat, _aim, _sign, _speed);
    }
}
