using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    /// <summary>ボスウェーブの流れ（<see cref="WaveFlow"/>）と配置の計算（<see cref="BossFormationMath"/>）。2026-11-15。</summary>
    public sealed class BossWaveTests
    {
        // WAVE 1 → WAVE 2 がボス。
        private static WaveFlowConfig Config(float timeLimit = 100f) => new WaveFlowConfig
        {
            targets = new[] { 10, 999 },
            breakSeconds = new[] { 5f, 0f },
            breakWaitsForEvent = new[] { false, false },
            bossWaves = new[] { false, true },
            startDelaySeconds = 1f,
            maxWaitForPlayersSeconds = 10f,
            announceSeconds = 3f,
            seatChangeSeconds = 2f,
            clearBannerSeconds = 1f,
            retreatMaxSeconds = 4f,
            timeLimitSeconds = timeLimit,
            bossFormationSeconds = 3f,
            bossWarningSeconds = 4f,
            bossFinaleMaxSeconds = 20f,
            bossReachedSeconds = 2f
        };

        private static WaveFlowInput Input(int kills, int partsLeft = -1, bool reached = false, bool finaleDone = false) => new WaveFlowInput
        {
            totalKills = kills,
            playersReady = true,
            bossPartsLeft = partsLeft,
            bossReachedCore = reached,
            bossFinaleDone = finaleDone
        };

        // WAVE 1 の目標を倒して、撤退が終わるところまで進める。
        private static WaveFlow ToBossFormation(WaveFlowConfig config)
        {
            var flow = new WaveFlow(config);
            flow.Begin();
            flow.Tick(1f, Input(0));
            flow.Tick(3f, Input(0));
            Assert.AreEqual(WavePhase.Combat, flow.Phase);
            flow.Tick(0.1f, Input(10));
            Assert.AreEqual(WavePhase.Retreat, flow.Phase);
            flow.Tick(1f, Input(10));
            return flow;
        }

        [Test]
        public void BeforeBossNoBrushingNoSeatChangeStraightToFormation()
        {
            var flow = ToBossFormation(Config());
            Assert.AreEqual(WavePhase.BossFormation, flow.Phase, "次がボスなら合間（歯みがき）も席替えもせず、すぐ並び替え");
            Assert.AreEqual(1, flow.WaveIndex);
            Assert.IsTrue(flow.IsBossWave);
        }

        [Test]
        public void FormationThenWarningThenCombatWithoutKillTarget()
        {
            var flow = ToBossFormation(Config());
            flow.Tick(3f, Input(10));
            Assert.AreEqual(WavePhase.BossWarning, flow.Phase);
            var signal = flow.Tick(4f, Input(10));
            Assert.AreEqual(WavePhase.BossCombat, flow.Phase);
            Assert.IsTrue((signal & WaveFlowSignal.StartSpawning) != 0);
            flow.Tick(1f, Input(5000, partsLeft: 4));
            Assert.AreEqual(WavePhase.BossCombat, flow.Phase, "ボス戦は撃破数では終わらない");
        }

        [Test]
        public void ClockStopsDuringBossPhases()
        {
            var flow = ToBossFormation(Config(timeLimit: 100f));
            var before = flow.TimeLeft;
            flow.Tick(3f, Input(10));
            flow.Tick(4f, Input(10));
            flow.Tick(30f, Input(10, partsLeft: 4));
            Assert.AreEqual(before, flow.TimeLeft, 1e-4f, "ボス戦の間は時間が止まる");
        }

        [Test]
        public void AllPartsBrokenGoesToFinaleThenClear()
        {
            var flow = ToBossFormation(Config());
            flow.Tick(3f, Input(10));
            flow.Tick(4f, Input(10));
            flow.Tick(0.1f, Input(10, partsLeft: 0));
            Assert.AreEqual(WavePhase.BossFinale, flow.Phase);
            flow.Tick(1f, Input(10, partsLeft: 0));
            Assert.AreEqual(WavePhase.BossFinale, flow.Phase, "最後の握りが終わるまで待つ");
            var signal = flow.Tick(0.1f, Input(10, partsLeft: 0, finaleDone: true));
            Assert.AreEqual(WavePhase.Finished, flow.Phase);
            Assert.AreEqual(WaveEndKind.AllWavesCleared, flow.EndKind);
            Assert.IsTrue((signal & WaveFlowSignal.Ended) != 0);
        }

        [Test]
        public void FinaleEndsByItselfAtMaxSeconds()
        {
            var flow = ToBossFormation(Config());
            flow.Tick(3f, Input(10));
            flow.Tick(4f, Input(10));
            flow.Tick(0.1f, Input(10, partsLeft: 0));
            flow.Tick(20f, Input(10, partsLeft: 0));
            Assert.AreEqual(WavePhase.Finished, flow.Phase, "握らなくても最長の秒数で必ず終わる（失敗しない）");
            Assert.AreEqual(WaveEndKind.AllWavesCleared, flow.EndKind);
        }

        [Test]
        public void ReachingTheCoreStillClearsAfterTheBite()
        {
            var flow = ToBossFormation(Config());
            flow.Tick(3f, Input(10));
            flow.Tick(4f, Input(10));
            flow.Tick(0.1f, Input(10, partsLeft: 2, reached: true));
            Assert.AreEqual(WavePhase.BossReached, flow.Phase);
            flow.Tick(2f, Input(10, partsLeft: 2, reached: true));
            Assert.AreEqual(WavePhase.Finished, flow.Phase, "コアが残っていればクリア");
            Assert.AreEqual(WaveEndKind.AllWavesCleared, flow.EndKind);
        }

        [Test]
        public void UnknownPartsDoNotEndTheFight()
        {
            var flow = ToBossFormation(Config());
            flow.Tick(3f, Input(10));
            flow.Tick(4f, Input(10));
            flow.Tick(5f, Input(10, partsLeft: -1));
            Assert.AreEqual(WavePhase.BossCombat, flow.Phase, "ボスがまだいない（−1）ときは終わらない");
        }

        [Test]
        public void JumpToBossWaveStartsWithFormationAndSkipWalksThrough()
        {
            var flow = new WaveFlow(Config());
            flow.Begin();
            flow.Tick(1f, Input(0));
            flow.JumpTo(1);
            Assert.AreEqual(WavePhase.BossFormation, flow.Phase);
            flow.Skip(0);
            Assert.AreEqual(WavePhase.BossWarning, flow.Phase);
            flow.Skip(0);
            Assert.AreEqual(WavePhase.BossCombat, flow.Phase);
            flow.Skip(0);
            Assert.AreEqual(WavePhase.BossFinale, flow.Phase);
            flow.Skip(0);
            Assert.AreEqual(WavePhase.Finished, flow.Phase);
        }

        [Test]
        public void NormalWavesStillUseBreakAndSeatChange()
        {
            var config = Config();
            config.bossWaves = new[] { false, false };
            var flow = ToBossFormation(config);
            Assert.AreEqual(WavePhase.Break, flow.Phase, "ボスでなければ今までどおり合間");
            flow.Tick(5f, Input(10));
            Assert.AreEqual(WavePhase.SeatChange, flow.Phase);
        }

        // ───────── 配置 ─────────

        [Test]
        public void LayoutPutsCoreLeftTurretsInAColumnAndBossRight()
        {
            var area = new Rect(-20f, -10f, 40f, 20f);
            var layout = BossFormationMath.Compute(area, new BossFormationShape(), 0.17f, 0.45f, 100f);
            Assert.IsTrue(layout.IsValid);
            Assert.Less(layout.core.x, layout.turrets[0].x, "コアは4人の後ろ（左）");
            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(layout.turrets[0].x, layout.turrets[i].x, 1e-4f, "縦一列");
            }

            Assert.Greater(layout.turrets[0].y, layout.turrets[3].y, "P1 が上");
            Assert.Greater(layout.bossStop.x, layout.turrets[0].x + layout.bossRadius, "ボスは右");
            Assert.Greater(layout.bossEntry.x, area.xMax, "画面の右の外から入ってくる");
            Assert.AreEqual(-90f, layout.turretAngle, 1e-4f, "砲台は右向き");
            Assert.Greater(layout.bossStop.x, layout.reachCenterX);
            Assert.LessOrEqual(layout.bossStop.y + layout.bobAmplitude + layout.bossRadius, area.yMax + 1e-3f, "ゆれても範囲からはみ出さない");
        }

        [Test]
        public void BossStopsWithinLaserRange()
        {
            var area = new Rect(-30f, -10f, 60f, 20f);
            var layout = BossFormationMath.Compute(area, new BossFormationShape(), 0.17f, 0.45f, 12f);
            var front = layout.bossStop.x - layout.bossRadius * 0.9f;
            Assert.LessOrEqual(front - layout.turrets[0].x, 12f + 1e-3f, "体の前の端はレーザーが届く距離の中");
        }

        [Test]
        public void ApproachSpeedTakesApproachSecondsFromStopToReach()
        {
            var layout = BossFormationMath.Compute(new Rect(-20f, -10f, 40f, 20f), new BossFormationShape(), 0.17f, 0.45f, 100f);
            var speed = BossFormationMath.ApproachSpeed(layout, 60f);
            Assert.AreEqual(layout.bossStop.x - layout.reachCenterX, speed * 60f, 1e-3f);
            Assert.AreEqual(0f, BossFormationMath.Approach01(layout, layout.bossStop.x), 1e-4f);
            Assert.AreEqual(1f, BossFormationMath.Approach01(layout, layout.reachCenterX), 1e-4f);
        }

        [Test]
        public void EscapeGoesToTheNearerEdgeOutsideTheArea()
        {
            var area = new Rect(-20f, -10f, 40f, 20f);
            var up = BossFormationMath.EscapePosition(new Vector2(3f, 2f), area, 1f, 1.5f);
            var down = BossFormationMath.EscapePosition(new Vector2(3f, -2f), area, 1f, 1.5f);
            Assert.Greater(up.y, area.yMax);
            Assert.Less(down.y, area.yMin);
            Assert.AreEqual(3f, up.x, 1e-4f);
        }
    }
}
