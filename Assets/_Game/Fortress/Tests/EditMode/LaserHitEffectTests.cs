using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class HitEffectBudgetTests
    {
        [Test]
        public void StartsFullThenRefillsPerSecond()
        {
            var budget = new HitEffectBudget(256) { PerSecond = 60f };
            Assert.AreEqual(60, budget.Available);

            budget.Consume(60);
            Assert.AreEqual(0, budget.Available);

            budget.Refill(0.5f);
            Assert.AreEqual(30, budget.Available);

            budget.Refill(10f);
            Assert.AreEqual(60, budget.Available, "1秒分より多くは貯まらない");
        }

        [Test]
        public void HardCapLimitsEvenWhenTokensRemain()
        {
            var budget = new HitEffectBudget(10) { PerSecond = 60f };
            Assert.AreEqual(10, budget.Available);
        }

        [Test]
        public void ZeroMeansUnlimitedUpToHardCap()
        {
            var budget = new HitEffectBudget(256) { PerSecond = 0f };
            budget.Consume(1000);
            Assert.AreEqual(256, budget.Available);
        }

        [Test]
        public void ChangingTheLimitRestartsFull()
        {
            var budget = new HitEffectBudget(256) { PerSecond = 60f };
            budget.Consume(60);
            budget.PerSecond = 120f;
            Assert.AreEqual(120, budget.Available);
        }
    }

    public sealed class SwarmLaserJobHitReportTests
    {
        private const int Capacity = 8;

        private NativeArray<float2> _pos;
        private NativeArray<float> _hp;
        private NativeArray<float> _flash;
        private NativeArray<int> _typeIdx;
        private NativeArray<SwarmTypeParams> _types;
        private NativeArray<SwarmBeam> _beams;
        private NativeArray<int> _cellStart;
        private NativeArray<int> _cellItems;
        private NativeArray<float> _hitT;
        private NativeArray<int> _hitIdx;
        private NativeArray<SwarmBeamHit> _newHits;
        private NativeArray<int> _newHitCount;

        [SetUp]
        public void SetUp()
        {
            _pos = new NativeArray<float2>(Capacity, Allocator.Persistent);
            _hp = new NativeArray<float>(Capacity, Allocator.Persistent);
            _flash = new NativeArray<float>(Capacity, Allocator.Persistent);
            _typeIdx = new NativeArray<int>(Capacity, Allocator.Persistent);
            _types = new NativeArray<SwarmTypeParams>(1, Allocator.Persistent);
            _types[0] = new SwarmTypeParams { radius = 0.2f };
            _beams = new NativeArray<SwarmBeam>(4, Allocator.Persistent);
            _cellStart = new NativeArray<int>(2, Allocator.Persistent);
            _cellItems = new NativeArray<int>(Capacity, Allocator.Persistent);
            _hitT = new NativeArray<float>(16, Allocator.Persistent);
            _hitIdx = new NativeArray<int>(16, Allocator.Persistent);
            _newHits = new NativeArray<SwarmBeamHit>(16, Allocator.Persistent);
            _newHitCount = new NativeArray<int>(1, Allocator.Persistent);
        }

        [TearDown]
        public void TearDown()
        {
            _pos.Dispose();
            _hp.Dispose();
            _flash.Dispose();
            _typeIdx.Dispose();
            _types.Dispose();
            _beams.Dispose();
            _cellStart.Dispose();
            _cellItems.Dispose();
            _hitT.Dispose();
            _hitIdx.Dispose();
            _newHits.Dispose();
            _newHitCount.Dispose();
        }

        /// <summary>全員を1つのセルに入れ、x軸上(0〜10)に並べる。</summary>
        private void PlaceEnemies(params float[] xs)
        {
            for (var i = 0; i < xs.Length; i++)
            {
                _pos[i] = new float2(xs[i], 0f);
                _hp[i] = 10f;
                _cellItems[i] = i;
            }

            _cellStart[0] = 0;
            _cellStart[1] = xs.Length;
        }

        private void Run(int beamCount, int maxNewHits, int beamOffset = 0)
        {
            new SwarmLaserJob
            {
                pos = _pos,
                hp = _hp,
                flash = _flash,
                typeIdx = _typeIdx,
                types = _types,
                beams = _beams,
                beamCount = beamCount,
                cellStart = _cellStart,
                cellItems = _cellItems,
                hashOrigin = new float2(-50f, -50f),
                hashInvCell = 0.01f,
                hashW = 1,
                hashH = 1,
                maxRadius = 0.2f,
                dt = 0.1f,
                hitT = _hitT,
                hitIdx = _hitIdx,
                newHits = _newHits,
                newHitCount = _newHitCount,
                maxNewHits = maxNewHits,
                beamOffset = beamOffset
            }.Run();
        }

        private static SwarmBeam Beam(int owner, int maxHits = 0)
        {
            return new SwarmBeam { origin = new float2(-1f, 0f), end = new float2(11f, 0f), halfWidth = 0.1f, damagePerSecond = 10f, maxHits = maxHits, owner = owner };
        }

        [Test]
        public void ReportsEnemiesWhoseFlashHadFaded()
        {
            PlaceEnemies(2f, 5f);
            _beams[0] = Beam(owner: 2);

            Run(1, 16);

            Assert.AreEqual(2, _newHitCount[0]);
            Assert.AreEqual(2, _newHits[0].owner);
            Assert.AreEqual(1f, _flash[0]);
            Assert.Less(_hp[0], 10f);
        }

        [Test]
        public void EnemiesStillFlashingAreDamagedButNotReported()
        {
            PlaceEnemies(2f, 5f);
            _flash[0] = 0.5f;
            _beams[0] = Beam(owner: 0);

            Run(1, 16);

            Assert.AreEqual(1, _newHitCount[0]);
            Assert.AreEqual(5f, _newHits[0].position.x);
            Assert.Less(_hp[0], 10f, "見た目の記録はしなくてもダメージは入る");
        }

        [Test]
        public void StopsReportingAtTheLimit()
        {
            PlaceEnemies(1f, 2f, 3f, 4f);
            _beams[0] = Beam(owner: 0);

            Run(1, 2);

            Assert.AreEqual(2, _newHitCount[0]);
            for (var i = 0; i < 4; i++)
            {
                Assert.AreEqual(1f, _flash[i], "上限を超えた分も当たり判定は普通に行う");
            }
        }

        [Test]
        public void TwoBeamsOnOneEnemyReportOnce()
        {
            PlaceEnemies(3f);
            _beams[0] = Beam(owner: 0);
            _beams[1] = Beam(owner: 1);

            Run(2, 16);

            Assert.AreEqual(1, _newHitCount[0]);
        }

        [Test]
        public void BeamOffsetChangesWhoGetsTheReport()
        {
            PlaceEnemies(3f);
            _beams[0] = Beam(owner: 0);
            _beams[1] = Beam(owner: 1);

            Run(2, 16, beamOffset: 1);

            Assert.AreEqual(1, _newHits[0].owner);
        }

        [Test]
        public void LimitedPierceReportsOnlyTheEnemiesItHits()
        {
            PlaceEnemies(6f, 2f, 4f);
            _beams[0] = Beam(owner: 3, maxHits: 1);

            Run(1, 16);

            Assert.AreEqual(1, _newHitCount[0]);
            Assert.AreEqual(2f, _newHits[0].position.x, "始点に一番近い敵だけ");
            Assert.AreEqual(0f, _flash[0]);
        }

        [Test]
        public void NoReportsWhenDisabled()
        {
            PlaceEnemies(3f);
            _beams[0] = Beam(owner: 0);

            Run(1, 0);

            Assert.AreEqual(0, _newHitCount[0]);
            Assert.AreEqual(1f, _flash[0]);
        }
    }
}
