using MS2026.Fortress.Net;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class SwarmFluidTests
    {
        [Test]
        public void ExcessIsZeroUntilRestOccupancy()
        {
            Assert.AreEqual(0f, SwarmFluidMath.Excess(0.5f, 0.6f));
            Assert.AreEqual(0.3f, SwarmFluidMath.Excess(0.9f, 0.6f), 1e-5f);
        }

        [Test]
        public void PushGoesDownTheGradientAndIsClamped()
        {
            // 右ほど混んでいる → 左へ押す。
            var push = SwarmFluidMath.Push(new float2(1f, 0f), 0.35f, 0.6f, 1f, 1f, 1f);
            Assert.Less(push.x, 0f);
            Assert.AreEqual(0f, push.y, 1e-6f);

            var clamped = SwarmFluidMath.Push(new float2(100f, 0f), 2f, 0.6f, 1f, 1f, 0.05f);
            Assert.AreEqual(0.05f, math.length(clamped), 1e-5f);
        }

        [Test]
        public void HeavierAgentsAreMovedLess()
        {
            var light = SwarmFluidMath.Push(new float2(1f, 0f), 0.35f, 0.6f, 1f, 1f, 10f);
            var heavy = SwarmFluidMath.Push(new float2(1f, 0f), 0.35f, 0.6f, 1f, 4f, 10f);
            Assert.Greater(math.length(light), math.length(heavy));
        }

        [Test]
        public void SolverLaunchArgumentIsParsed()
        {
            Assert.AreEqual(SwarmCrowdSolver.Fluid, NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-solver", "fluid" }).CrowdSolver);
            Assert.AreEqual(SwarmCrowdSolver.PerAgent, NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-solver", "peragent" }).CrowdSolver);
            Assert.IsNull(NetBenchmarkArgs.Parse(new[] { "-fortress-swarm-solver", "???" }).CrowdSolver);
            Assert.IsNull(NetBenchmarkArgs.Parse(new string[0]).CrowdSolver);
        }

        [Test]
        public void OccupancyIsBodyAreaOverCellArea()
        {
            // マス2つ（0番に2体、1番に0体）。半径0.3の体2つ ÷ 面積1。
            var cellStart = new NativeArray<int>(new[] { 0, 2, 2 }, Allocator.TempJob);
            var cellItems = new NativeArray<int>(new[] { 0, 1 }, Allocator.TempJob);
            var typeIdx = new NativeArray<int>(new[] { 0, 0 }, Allocator.TempJob);
            var types = new NativeArray<SwarmTypeParams>(1, Allocator.TempJob);
            types[0] = new SwarmTypeParams { radius = 0.3f, mass = 1f };
            var occupancy = new NativeArray<float>(2, Allocator.TempJob);
            try
            {
                new SwarmOccupancyJob
                {
                    cellStart = cellStart,
                    cellItems = cellItems,
                    typeIdx = typeIdx,
                    types = types,
                    occupancy = occupancy,
                    invCellArea = 1f,
                    personalSpace = 1f
                }.Schedule(2, 1).Complete();

                Assert.AreEqual(2f * math.PI * 0.09f, occupancy[0], 1e-4f);
                Assert.AreEqual(0f, occupancy[1], 1e-6f);
            }
            finally
            {
                cellStart.Dispose();
                cellItems.Dispose();
                typeIdx.Dispose();
                types.Dispose();
                occupancy.Dispose();
            }
        }
    }
}
