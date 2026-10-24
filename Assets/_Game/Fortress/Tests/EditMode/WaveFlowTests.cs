using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class WaveFlowTests
    {
        private static WaveFlowConfig Config(float timeLimit = 0f, bool clockInBreaks = false) => new WaveFlowConfig
        {
            targets = new[] { 10, 20, 30 },
            breakSeconds = new[] { 5f, 6f, 0f },
            breakWaitsForEvent = new[] { false, true, false },
            startDelaySeconds = 1f,
            maxWaitForPlayersSeconds = 10f,
            announceSeconds = 3f,
            clearBannerSeconds = 1f,
            retreatMaxSeconds = 4f,
            timeLimitSeconds = timeLimit,
            clockRunsDuringBreaks = clockInBreaks
        };

        private static WaveFlowInput Kills(int total, bool ready = true) => new WaveFlowInput { totalKills = total, playersReady = ready };

        // 戦いが始まるところまで進める。
        private static WaveFlow StartedFlow(WaveFlowConfig config, int kills = 0)
        {
            var flow = new WaveFlow(config);
            flow.Begin();
            flow.Tick(1f, Kills(kills));
            Assert.AreEqual(WavePhase.Announce, flow.Phase);
            var signal = flow.Tick(3f, Kills(kills));
            Assert.AreEqual(WavePhase.Combat, flow.Phase);
            Assert.IsTrue((signal & WaveFlowSignal.StartSpawning) != 0);
            return flow;
        }

        [Test]
        public void WaitsForPlayersThenAnnouncesThenFights()
        {
            var flow = new WaveFlow(Config());
            flow.Begin();
            Assert.AreEqual(WavePhase.WaitingForPlayers, flow.Phase);
            flow.Tick(5f, Kills(0, ready: false));
            Assert.AreEqual(WavePhase.WaitingForPlayers, flow.Phase, "準備ができていなければ待つ");
            flow.Tick(5f, Kills(0, ready: false));
            Assert.AreEqual(WavePhase.Announce, flow.Phase, "最長の待ち時間を過ぎたら始める");
            flow.Tick(3f, Kills(0));
            Assert.AreEqual(WavePhase.Combat, flow.Phase);
        }

        [Test]
        public void HintComesBeforeTheFirstAnnounceAndStopsTheClock()
        {
            var config = Config(timeLimit: 60f, clockInBreaks: true);
            config.hintSeconds = 3f;
            var flow = new WaveFlow(config);
            flow.Begin();
            flow.Tick(1f, Kills(0));
            Assert.AreEqual(WavePhase.Hint, flow.Phase, "全員そろったら、まず握り方のヒント");
            flow.Tick(2.9f, Kills(0));
            Assert.AreEqual(WavePhase.Hint, flow.Phase);
            Assert.AreEqual(60f, flow.TimeLeft, 1e-4f, "ヒントの間は時間が減らない");
            flow.Tick(0.2f, Kills(0));
            Assert.AreEqual(WavePhase.Announce, flow.Phase);
            Assert.AreEqual(0, flow.WaveIndex);
        }

        [Test]
        public void KillsCountFromTheStartOfTheWave()
        {
            var flow = StartedFlow(Config(), kills: 7);
            flow.Tick(0.1f, Kills(12));
            Assert.AreEqual(5, flow.KillsInWave, "発表の前に倒した7体は数えない");
            var signal = flow.Tick(0.1f, Kills(17));
            Assert.AreEqual(WavePhase.Retreat, flow.Phase);
            Assert.IsTrue((signal & WaveFlowSignal.WaveCleared) != 0);
            Assert.IsTrue((signal & WaveFlowSignal.StopSpawning) != 0);
            Assert.IsTrue((signal & WaveFlowSignal.BeginRetreat) != 0);
            Assert.AreEqual(10, flow.KillsInWave);
        }

        [Test]
        public void RetreatWaitsForTheBannerAndForEnemiesToLeave()
        {
            var flow = StartedFlow(Config());
            flow.Tick(0.1f, Kills(10));
            flow.Tick(0.5f, new WaveFlowInput { totalKills = 10, retreatingVisible = 0 });
            Assert.AreEqual(WavePhase.Retreat, flow.Phase, "クリアの表示は最短1秒");
            flow.Tick(0.6f, new WaveFlowInput { totalKills = 10, retreatingVisible = 3 });
            Assert.AreEqual(WavePhase.Retreat, flow.Phase, "まだ画面に敵がいる");
            var signal = flow.Tick(3f, new WaveFlowInput { totalKills = 10, retreatingVisible = 3 });
            Assert.AreEqual(WavePhase.Break, flow.Phase, "最長4秒で打ち切る");
            Assert.IsTrue((signal & WaveFlowSignal.FinishRetreat) != 0);
            Assert.IsTrue((signal & WaveFlowSignal.BeginBreak) != 0);
            Assert.AreEqual(0, flow.WaveIndex, "合間の間は、クリアしたウェーブの番号のまま");
        }

        [Test]
        public void BreakEndsByTimeOrByTheEvent()
        {
            var flow = StartedFlow(Config());
            flow.Tick(0.1f, Kills(10));
            flow.Tick(2f, Kills(10));
            Assert.AreEqual(WavePhase.Break, flow.Phase);
            flow.Tick(4.9f, new WaveFlowInput { totalKills = 10, breakFinished = true });
            Assert.AreEqual(WavePhase.Break, flow.Phase, "ウェーブ1の後はイベントを待たない（休むだけ）");
            var signal = flow.Tick(0.2f, Kills(10));
            Assert.AreEqual(WavePhase.Announce, flow.Phase);
            Assert.AreEqual(1, flow.WaveIndex);
            Assert.AreEqual(0, flow.KillsInWave, "次のウェーブの発表では、倒した数は 0 から");
            Assert.IsTrue((signal & WaveFlowSignal.EndBreak) != 0);

            flow.Tick(3f, Kills(10));
            flow.Tick(0.1f, Kills(30));
            flow.Tick(2f, Kills(30));
            Assert.AreEqual(WavePhase.Break, flow.Phase);
            flow.Tick(1f, new WaveFlowInput { totalKills = 30, breakFinished = true });
            Assert.AreEqual(WavePhase.Announce, flow.Phase, "ウェーブ2の後はイベントが終われば次へ");
            Assert.AreEqual(2, flow.WaveIndex);
        }

        [Test]
        public void FinalWaveTargetClearsWithoutRetreat()
        {
            var flow = StartedFlow(Config());
            flow.JumpTo(2);
            flow.Tick(3f, Kills(100));
            Assert.AreEqual(WavePhase.Combat, flow.Phase);
            var signal = flow.Tick(0.1f, Kills(130));
            Assert.AreEqual(WavePhase.Finished, flow.Phase);
            Assert.AreEqual(WaveEndKind.AllWavesCleared, flow.EndKind);
            Assert.IsTrue((signal & WaveFlowSignal.Ended) != 0);
            Assert.IsTrue((signal & WaveFlowSignal.BeginRetreat) == 0);
        }

        [Test]
        public void ClockRunsOnlyWhileFightingByDefault()
        {
            var flow = StartedFlow(Config(timeLimit: 10f));
            Assert.AreEqual(10f, flow.TimeLeft, 1e-4f, "待つ・発表の間は減らない");
            flow.Tick(4f, Kills(10));
            Assert.AreEqual(WavePhase.Retreat, flow.Phase);
            Assert.AreEqual(10f, flow.TimeLeft, 1e-4f, "目標に届いたフレームは減らない");
            flow.Tick(2f, Kills(10));
            flow.Tick(5f, Kills(10));
            Assert.AreEqual(10f, flow.TimeLeft, 1e-4f, "撤退・合間の間も減らない");
        }

        [Test]
        public void TimeUpEndsTheFlow()
        {
            var flow = StartedFlow(Config(timeLimit: 10f));
            flow.Tick(6f, Kills(5));
            var signal = flow.Tick(4.5f, Kills(5));
            Assert.AreEqual(WavePhase.Finished, flow.Phase);
            Assert.AreEqual(WaveEndKind.TimeUp, flow.EndKind);
            Assert.IsTrue((signal & WaveFlowSignal.StopSpawning) != 0);
            Assert.AreEqual(0f, flow.TimeLeft);
        }

        [Test]
        public void ClockCanRunDuringBreaks()
        {
            var flow = StartedFlow(Config(timeLimit: 10f, clockInBreaks: true));
            Assert.AreEqual(7f, flow.TimeLeft, 1e-4f, "発表の3秒も減る（待つ間は減らない）");
            flow.Tick(0.5f, Kills(10));
            Assert.AreEqual(WavePhase.Retreat, flow.Phase);
            flow.Tick(4.6f, Kills(10));
            Assert.AreEqual(WavePhase.Break, flow.Phase);
            Assert.AreEqual(2.4f, flow.TimeLeft, 1e-3f, "撤退の間も減る");
            flow.Tick(2.5f, Kills(10));
            Assert.AreEqual(WavePhase.Finished, flow.Phase, "合間の間でも時間切れになる");
            Assert.AreEqual(WaveEndKind.TimeUp, flow.EndKind);
        }

        [Test]
        public void ClearWinsOverTimeUpInTheSameFrame()
        {
            var flow = StartedFlow(Config(timeLimit: 1f));
            flow.Tick(2f, Kills(10));
            Assert.AreEqual(WavePhase.Retreat, flow.Phase);
        }

        [Test]
        public void SkipAdvancesEveryPhase()
        {
            var config = Config();
            config.hintSeconds = 2f;
            var flow = new WaveFlow(config);
            flow.Begin();
            flow.Skip(0);
            Assert.AreEqual(WavePhase.Hint, flow.Phase);
            flow.Skip(0);
            Assert.AreEqual(WavePhase.Announce, flow.Phase);
            flow.Skip(4);
            Assert.AreEqual(WavePhase.Combat, flow.Phase);
            Assert.AreEqual(4, flow.KillsAtWaveStart);
            flow.Skip(4);
            Assert.AreEqual(WavePhase.Retreat, flow.Phase);
            flow.Skip(4);
            Assert.AreEqual(WavePhase.Break, flow.Phase);
            flow.Skip(4);
            Assert.AreEqual(WavePhase.Announce, flow.Phase);
            Assert.AreEqual(1, flow.WaveIndex);
        }

        [Test]
        public void EmptyPlanEndsAtOnce()
        {
            var flow = new WaveFlow(new WaveFlowConfig());
            flow.Begin();
            Assert.AreEqual(WavePhase.Finished, flow.Phase);
        }
    }
}
