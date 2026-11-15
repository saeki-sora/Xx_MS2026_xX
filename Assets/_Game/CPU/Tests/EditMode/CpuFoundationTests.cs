using System.Collections.Generic;
using MS2026.Fortress;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Cpu.Tests.EditMode
{
    public sealed class CpuLaunchOptionsTests
    {
        [Test]
        public void NoArgs_IsDefault()
        {
            var o = CpuLaunchOptions.Parse(new[] { "game.exe" });
            Assert.AreEqual(CpuSeatMode.Default, o.SeatMode);
            Assert.IsFalse(o.Level.HasValue);
            Assert.IsFalse(o.Seed.HasValue);
            Assert.IsFalse(o.HasAny);
        }

        [Test]
        public void SeatList_UsesPlayerNumbersFromOne()
        {
            var o = CpuLaunchOptions.Parse(new[] { "game.exe", "-cpu", "2,4" });
            Assert.AreEqual(CpuSeatMode.List, o.SeatMode);
            Assert.AreEqual((1 << 1) | (1 << 3), o.SeatMask);
        }

        [Test]
        public void SeatList_AcceptsPPrefixAndIgnoresOutOfRange()
        {
            var o = CpuLaunchOptions.Parse(new[] { "-cpu", "p1,P3,9" });
            Assert.AreEqual(CpuSeatMode.List, o.SeatMode);
            Assert.AreEqual((1 << 0) | (1 << 2), o.SeatMask);
        }

        [Test]
        public void AllAndNone()
        {
            Assert.AreEqual(CpuSeatMode.All, CpuLaunchOptions.Parse(new[] { "-cpu", "all" }).SeatMode);
            Assert.AreEqual(CpuSeatMode.All, CpuLaunchOptions.Parse(new[] { "-cpu-all" }).SeatMode);
            Assert.AreEqual(CpuSeatMode.None, CpuLaunchOptions.Parse(new[] { "-cpu", "none" }).SeatMode);
        }

        [Test]
        public void MissingValue_DoesNotEatNextFlag()
        {
            var o = CpuLaunchOptions.Parse(new[] { "-cpu", "-cpu-level", "hard" });
            Assert.AreEqual(CpuSeatMode.Default, o.SeatMode);
            Assert.AreEqual(CpuLevel.Hard, o.Level);
        }

        [Test]
        public void LevelAndSeed()
        {
            var o = CpuLaunchOptions.Parse(new[] { "-cpu-level", "easy", "-cpu-seed", "123" });
            Assert.AreEqual(CpuLevel.Easy, o.Level);
            Assert.AreEqual(123, o.Seed);
        }

        [TestCase("1", CpuLevel.Easy)]
        [TestCase("NORMAL", CpuLevel.Normal)]
        [TestCase("つよい", CpuLevel.Hard)]
        public void LevelNames(string text, CpuLevel expected)
        {
            Assert.IsTrue(CpuLevels.TryParse(text, out var level));
            Assert.AreEqual(expected, level);
        }

        [Test]
        public void UnknownLevel_IsIgnored()
        {
            Assert.IsFalse(CpuLaunchOptions.Parse(new[] { "-cpu-level", "nightmare" }).Level.HasValue);
        }
    }

    public sealed class CpuSeatRulesTests
    {
        private static CpuSeatInputs Offline(int local = 0) => new CpuSeatInputs
        {
            Enabled = true,
            HasAuthority = true,
            Networked = false,
            LocalPlayer = local,
            TurretMask = CpuSeatRules.AllMask,
            FillOtherSeatsOffline = true
        };

        [Test]
        public void Offline_FillsEveryoneButMe()
        {
            Assert.AreEqual(0b1110, CpuSeatRules.ComputeCpuMask(Offline()));
            Assert.AreEqual(0b1011, CpuSeatRules.ComputeCpuMask(Offline(local: 2)));
        }

        [Test]
        public void Offline_FillOff_NoCpu()
        {
            var inputs = Offline();
            inputs.FillOtherSeatsOffline = false;
            Assert.AreEqual(0, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void Disabled_OrClient_NoCpu()
        {
            var inputs = Offline();
            inputs.Mode = CpuSeatMode.All;
            inputs.Enabled = false;
            Assert.AreEqual(0, CpuSeatRules.ComputeCpuMask(inputs));

            inputs.Enabled = true;
            inputs.HasAuthority = false;
            Assert.AreEqual(0, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void All_IncludesMyOwnSeat()
        {
            var inputs = Offline();
            inputs.Mode = CpuSeatMode.All;
            Assert.AreEqual(CpuSeatRules.AllMask, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void Networked_FillOff_HasNoCpu()
        {
            var inputs = Offline();
            inputs.Networked = true;
            inputs.FillEmptySeatsOnline = false;
            Assert.AreEqual(0, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void Networked_FillsSeatsWithoutHumans_AndHandsOverWhenSomeoneJoins()
        {
            var inputs = Offline();
            inputs.Networked = true;
            inputs.FillEmptySeatsOnline = true;
            inputs.RemoteHumanMask = 0b0100; // P3 に人
            Assert.AreEqual(0b1010, CpuSeatRules.ComputeCpuMask(inputs)); // P1（Host）と P3 以外

            inputs.RemoteHumanMask = 0b0110; // P2 にも人が来た → P2 の CPU は止まる
            Assert.AreEqual(0b1000, CpuSeatRules.ComputeCpuMask(inputs));

            inputs.RemoteHumanMask = 0b0010; // P3 の人が抜けた → P3 は CPU が引き継ぐ
            Assert.AreEqual(0b1100, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void Networked_RemoteHumansWin()
        {
            var inputs = Offline();
            inputs.Networked = true;
            inputs.Mode = CpuSeatMode.List;
            inputs.ListMask = 0b1110;
            inputs.RemoteHumanMask = 0b0101; // P1（自分。数えない）と P3 に人
            Assert.AreEqual(0b1010, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void MissingTurrets_AreNotCpu()
        {
            var inputs = Offline();
            inputs.TurretMask = 0b0011;
            Assert.AreEqual(0b0010, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void None_Wins()
        {
            var inputs = Offline();
            inputs.Mode = CpuSeatMode.None;
            Assert.AreEqual(0, CpuSeatRules.ComputeCpuMask(inputs));
        }

        [Test]
        public void Describe()
        {
            Assert.AreEqual("なし", CpuSeatRules.Describe(0));
            Assert.AreEqual("P2, P4", CpuSeatRules.Describe(0b1010));
        }

        [Test]
        public void Seed_SameInputsSameSeed_DifferentSeatsDiffer()
        {
            Assert.AreEqual(CpuSeatRules.SeedFor(42, 1, 3, 999), CpuSeatRules.SeedFor(42, 1, 3, 777));
            Assert.AreNotEqual(CpuSeatRules.SeedFor(42, 1, 3, 0), CpuSeatRules.SeedFor(42, 2, 3, 0));
            Assert.AreNotEqual(CpuSeatRules.SeedFor(42, 1, 3, 0), CpuSeatRules.SeedFor(42, 1, 4, 0));
        }
    }

    public sealed class CpuChoiceTests
    {
        [Test]
        public void PicksHighest()
        {
            Assert.AreEqual(1, CpuChoice.Pick(new[] { 0.2f, 0.9f, 0.5f }, -1, 0f));
        }

        [Test]
        public void NothingPositive_PicksNone()
        {
            Assert.AreEqual(-1, CpuChoice.Pick(new[] { 0f, -1f, float.NaN }, -1, 0.5f));
            Assert.AreEqual(-1, CpuChoice.Pick(new float[0], -1, 0.5f));
        }

        [Test]
        public void Stickiness_KeepsCurrentWhenClose()
        {
            Assert.AreEqual(0, CpuChoice.Pick(new[] { 0.5f, 0.55f }, 0, 0.1f));
            Assert.AreEqual(1, CpuChoice.Pick(new[] { 0.5f, 0.7f }, 0, 0.1f));
        }

        [Test]
        public void Stickiness_DoesNotRescueZeroScore()
        {
            Assert.AreEqual(1, CpuChoice.Pick(new[] { 0f, 0.05f }, 0, 1f));
        }

        [Test]
        public void Tie_FirstRegisteredWins()
        {
            Assert.AreEqual(0, CpuChoice.Pick(new[] { 0.5f, 0.5f }, -1, 0f));
        }
    }

    public sealed class CpuBrainTests
    {
        private sealed class FakeBehavior : ICpuBehavior
        {
            public float Value;
            public float HoldGrip;
            public int Enters;
            public int Exits;
            public int Ticks;

            public FakeBehavior(string id) => Id = id;

            public string Id { get; }
            public float Score(CpuContext context) => Value;
            public void Enter(CpuContext context) => Enters++;
            public void Exit(CpuContext context) => Exits++;

            public void Tick(CpuContext context, float deltaTime)
            {
                Ticks++;
                context.Hands.Hold(HoldGrip);
            }
        }

        private static CpuContext Context(CpuHands hands) => new CpuContext(1, CpuLevel.Normal, 7, hands);

        [Test]
        public void NoBehaviors_ReleasesHands()
        {
            var hands = new CpuHands(1);
            hands.Hold(1f);
            var brain = new CpuBrain(new List<ICpuBehavior>());
            brain.Tick(Context(hands), 0.016f);
            hands.Tick(0.016f);
            Assert.AreEqual(0f, hands.Grip01);
            Assert.IsNull(brain.Current);
        }

        [Test]
        public void SwitchesAndCallsEnterExit()
        {
            var hands = new CpuHands(1);
            var a = new FakeBehavior("a") { Value = 1f, HoldGrip = 0.3f };
            var b = new FakeBehavior("b") { Value = 0.5f, HoldGrip = 0.9f };
            var brain = new CpuBrain(new List<ICpuBehavior> { a, b }) { ThinkInterval = 0.1f, Stickiness = 0f };
            var context = Context(hands);

            brain.Tick(context, 0.016f);
            Assert.AreSame(a, brain.Current);
            Assert.AreEqual(1, a.Enters);
            Assert.AreEqual(0.3f, hands.Target, 1e-5f);

            a.Value = 0f;
            brain.Tick(context, 0.2f); // 考え直す
            Assert.AreSame(b, brain.Current);
            Assert.AreEqual(1, a.Exits);
            Assert.AreEqual(1, b.Enters);
            Assert.AreEqual(0.9f, hands.Target, 1e-5f);

            brain.Stop(context);
            Assert.IsNull(brain.Current);
            Assert.AreEqual(1, b.Exits);
            Assert.AreEqual(0f, hands.Grip01);
        }

        [Test]
        public void ThinksOnlyEveryInterval()
        {
            var hands = new CpuHands(1);
            var a = new FakeBehavior("a") { Value = 1f };
            var b = new FakeBehavior("b") { Value = 0f };
            var brain = new CpuBrain(new List<ICpuBehavior> { a, b }) { ThinkInterval = 0.5f, Stickiness = 0f };
            var context = Context(hands);

            brain.Tick(context, 0.01f);
            b.Value = 5f;
            brain.Tick(context, 0.1f); // まだ考え直さない
            Assert.AreSame(a, brain.Current);
            brain.Tick(context, 0.5f);
            Assert.AreSame(b, brain.Current);
        }
    }

    public sealed class CpuHandsTests
    {
        [Test]
        public void MovesTowardTargetAtLimitedSpeed()
        {
            var hands = new CpuHands(0) { GripChangePerSecond = 2f };
            hands.Hold(1f);
            hands.Tick(0.25f);
            Assert.AreEqual(0.5f, hands.Grip01, 1e-5f);
            Assert.IsTrue(hands.IsGripping);
            hands.Tick(1f);
            Assert.AreEqual(1f, hands.Grip01, 1e-5f);
        }

        [Test]
        public void WeakGripIsNotGripping()
        {
            var hands = new CpuHands(0);
            hands.Hold(0.05f);
            hands.Tick(0.1f);
            Assert.IsFalse(hands.IsGripping);
        }

        [Test]
        public void ReportsThroughGripSource()
        {
            var hands = new CpuHands(2);
            hands.Hold(0.8f);
            hands.Tick(0.1f);
            ((ITurretGripSource)hands).GetGrip(2, out var gripping, out var grip);
            Assert.IsTrue(gripping);
            Assert.AreEqual(0.8f, grip, 1e-5f);
        }
    }

    public sealed class PlayerPilotsTests
    {
        private sealed class Fixed : ITurretGripSource
        {
            public float Value;

            public void GetGrip(int playerIndex, out bool isGripping, out float grip01)
            {
                grip01 = Value;
                isGripping = Value > 0.1f;
            }
        }

        [TearDown]
        public void TearDown() => PlayerPilots.ClearAll();

        [Test]
        public void SetClearAndMask()
        {
            var changes = 0;
            void OnChanged() => changes++;
            PlayerPilots.Changed += OnChanged;
            try
            {
                var pilot = new Fixed { Value = 1.5f };
                PlayerPilots.Set(2, pilot);
                PlayerPilots.Set(2, pilot); // 同じなら知らせない
                Assert.AreEqual(1 << 2, PlayerPilots.Mask);
                Assert.IsTrue(PlayerPilots.IsPiloted(2));
                Assert.IsTrue(PlayerPilots.TryReadGrip(2, out var gripping, out var grip));
                Assert.IsTrue(gripping);
                Assert.AreEqual(1f, grip, 1e-5f); // 0〜1 に収める

                Assert.IsFalse(PlayerPilots.TryReadGrip(1, out _, out _));
                PlayerPilots.Clear(2);
                Assert.AreEqual(0, PlayerPilots.Mask);
                Assert.AreEqual(2, changes);
            }
            finally
            {
                PlayerPilots.Changed -= OnChanged;
            }
        }

        [Test]
        public void OutOfRange_IsIgnored()
        {
            PlayerPilots.Set(-1, new Fixed());
            PlayerPilots.Set(4, new Fixed());
            Assert.AreEqual(0, PlayerPilots.Mask);
        }

        [Test]
        public void TurretReadsPilotFirst()
        {
            var go = new GameObject("turret");
            try
            {
                var turret = go.AddComponent<LaserTurret>();
                turret.playerIndex = 3;
                turret.GripSourceOverride = new Fixed { Value = 0.2f };
                PlayerPilots.Set(3, new Fixed { Value = 0.7f });
                turret.ReadGrip(out var gripping, out var grip);
                Assert.IsTrue(gripping);
                Assert.AreEqual(0.7f, grip, 1e-5f);

                PlayerPilots.Clear(3);
                turret.ReadGrip(out _, out grip);
                Assert.AreEqual(0.2f, grip, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }

    public sealed class CpuViewTests
    {
        [Test]
        public void ContainsFollowsRotation()
        {
            var view = new CpuView(Vector2.zero, new Vector2(4f, 1f), Vector2.right); // 画面の上 = ワールドの右
            Assert.IsTrue(view.Contains(new Vector2(0.9f, 0f)));
            Assert.IsFalse(view.Contains(new Vector2(2f, 0f)));
            Assert.IsTrue(view.Contains(new Vector2(0f, -3.5f)));
            Assert.IsTrue(view.Contains(new Vector2(2f, 0f), margin: 1.5f));
        }
    }
}
