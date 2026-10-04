using MS2026.Fortress.Net;
using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class SwarmCorrectionPriorityMathTests
    {
        private static readonly SwarmPriorityWeights Weights = new()
        {
            view = 3f,
            beam = 4f,
            core = 2f,
            baseError = 0.25f,
            beamMargin = 1.5f,
            coreRadius = 4f,
            minInterval = 0.05f,
            maxAge = 1f,
            maxPredictSeconds = 0.5f
        };

        private static readonly float4 View = new(-10f, -5f, 10f, 5f);

        [Test]
        public void Importance_AddsWeightsForViewBeamAndCore()
        {
            Assert.AreEqual(1f, SwarmCorrectionPriorityMath.Importance(new float2(20f, 0f), View, true, false, false, Weights));
            Assert.AreEqual(4f, SwarmCorrectionPriorityMath.Importance(new float2(0f, 0f), View, true, false, false, Weights));
            Assert.AreEqual(10f, SwarmCorrectionPriorityMath.Importance(new float2(0f, 0f), View, true, true, true, Weights));
        }

        [Test]
        public void Importance_IgnoresViewWhenUnknown()
        {
            Assert.AreEqual(1f, SwarmCorrectionPriorityMath.Importance(new float2(0f, 0f), View, false, false, false, Weights));
        }

        [Test]
        public void PredictionError_UsesLastSentVelocity()
        {
            var sent = new SwarmSentState { pos = new float2(0f, 0f), vel = new float2(2f, 0f), time = 0f };

            // 0.25秒後、予想は (0.5, 0)。実際が (0.5, 0) ならズレ0、(0.5, 1) なら1。
            Assert.AreEqual(0f, SwarmCorrectionPriorityMath.PredictionError(new float2(0.5f, 0f), sent, 0.25f, 0.5f), 1e-5f);
            Assert.AreEqual(1f, SwarmCorrectionPriorityMath.PredictionError(new float2(0.5f, 1f), sent, 0.25f, 0.5f), 1e-5f);
        }

        [Test]
        public void PredictionError_ClampsPredictionTime()
        {
            var sent = new SwarmSentState { pos = new float2(0f, 0f), vel = new float2(1f, 0f), time = 0f };

            // 10秒経っても0.5秒分しか進めない(予想は (0.5, 0))。
            Assert.AreEqual(0.5f, SwarmCorrectionPriorityMath.PredictionError(new float2(1f, 0f), sent, 10f, 0.5f), 1e-5f);
        }

        [Test]
        public void Score_RespectsMinIntervalAndMaxAge()
        {
            Assert.AreEqual(SwarmCorrectionPriorityMath.NotEligible, SwarmCorrectionPriorityMath.Score(100f, 0.01f, Weights));
            Assert.AreEqual(3f, SwarmCorrectionPriorityMath.Score(3f, 0.5f, Weights));

            // 長く送っていない敵は、どんな優先度よりも上で、古いほど上。
            var old = SwarmCorrectionPriorityMath.Score(0f, 1.5f, Weights);
            var older = SwarmCorrectionPriorityMath.Score(0f, 2f, Weights);
            Assert.Greater(old, 1e4f);
            Assert.Greater(older, old);
        }

        [Test]
        public void DistanceSqToSegment_ProjectsOntoSegment()
        {
            var a = new float2(0f, 0f);
            var b = new float2(10f, 0f);

            Assert.AreEqual(4f, SwarmCorrectionPriorityMath.DistanceSqToSegment(new float2(5f, 2f), a, b), 1e-5f);
            Assert.AreEqual(25f, SwarmCorrectionPriorityMath.DistanceSqToSegment(new float2(-3f, 4f), a, b), 1e-5f);
            Assert.AreEqual(1f, SwarmCorrectionPriorityMath.DistanceSqToSegment(new float2(0f, 1f), a, a), 1e-5f);
        }
    }

    public sealed class SwarmCorrectionBudgetControllerTests
    {
        private const float KB = 1024f;

        private static SwarmCorrectionBudgetController Create()
        {
            return new SwarmCorrectionBudgetController
            {
                MinBytesPerSecond = 100 * KB,
                NormalBytesPerSecond = 700 * KB,
                MaxBytesPerSecond = 1700 * KB,
                HighError = 1f,
                LowError = 0.3f,
                RelaxFractionPerSecond = 0.25f,
                CongestionRttMs = 25f,
                RttSmoothingSeconds = 0f,
                BackoffFactor = 0.5f,
                BackoffIntervalSeconds = 0.25f,
                RecoverFractionPerSecond = 0.2f
            };
        }

        [Test]
        public void NoReport_UsesNormalBudget()
        {
            Assert.AreEqual(700 * KB, Create().Update(0.016f, -1f, -1f), 1f);
        }

        [Test]
        public void LargeError_RaisesToMaximumImmediately()
        {
            Assert.AreEqual(1700 * KB, Create().Update(0.016f, 5f, 1f), 1f);
        }

        [Test]
        public void MediumError_Interpolates()
        {
            // 0.65 は 0.3〜1.0 の中間 → 700 と 1700 の中間。
            Assert.AreEqual(1200 * KB, Create().Update(0.016f, 0.65f, -1f), 1f);
        }

        [Test]
        public void SmallError_RelaxesSlowly()
        {
            var controller = Create();
            controller.Update(0.016f, 5f, -1f);

            // 1秒で (1700-700)*0.25 = 250KB 戻る。
            Assert.AreEqual(1450 * KB, controller.Update(1f, 0f, -1f), 1f);
            Assert.AreEqual(700 * KB, controller.Update(10f, 0f, -1f), 1f);
        }

        [Test]
        public void RttIncrease_BacksOffAndRecovers()
        {
            var controller = Create();
            controller.Update(0.016f, 5f, 2f); // 一番良いRTT=2ms、最大まで上げる

            // RTTが2+25msを超えた → 今の量の半分へ。
            var reduced = controller.Update(0.016f, 5f, 40f);
            Assert.AreEqual(850 * KB, reduced, 2f * KB);
            Assert.AreEqual(1, controller.Backoffs);

            // 間隔(0.25秒)以内は続けて下げない。
            controller.Update(0.1f, 5f, 40f);
            Assert.AreEqual(1, controller.Backoffs);

            // 0.25秒経ってもまだ混んでいれば、もう一段下げる。
            var reducedAgain = controller.Update(0.2f, 5f, 40f);
            Assert.Less(reducedAgain, reduced);
            Assert.AreEqual(2, controller.Backoffs);

            // RTTが戻れば、上限が少しずつ戻る(1秒で最大の2割)。
            var recovered = controller.Update(1f, 5f, 2f);
            Assert.AreEqual(reducedAgain + 1700 * KB * 0.2f, recovered, 2f * KB);
        }

        [Test]
        public void ShortRttSpike_IsSmoothedAndIgnored()
        {
            var controller = Create();
            controller.RttSmoothingSeconds = 0.5f;
            controller.Update(0.016f, -1f, 2f);

            // 1フレームだけ200ms(フレームの引っかかり等)。ならすと約8msなので下げない。
            controller.Update(0.016f, -1f, 200f);
            Assert.AreEqual(0, controller.Backoffs);

            // 1秒以上続けば下げる。
            for (var i = 0; i < 60; i++)
            {
                controller.Update(0.016f, -1f, 200f);
            }

            Assert.Greater(controller.Backoffs, 0);
        }

        [Test]
        public void Backoff_NeverGoesBelowMinimum()
        {
            var controller = Create();
            controller.Update(0.016f, -1f, 1f);
            for (var i = 0; i < 50; i++)
            {
                controller.Update(0.3f, -1f, 500f);
            }

            Assert.AreEqual(100 * KB, controller.CurrentBytesPerSecond, 1f);
        }
    }

    /// <summary>Burstのジョブ(NativeArrayを使う)なので、Unityのテストランナーでのみ動く。</summary>
    public sealed class SwarmCorrectionSelectJobTests
    {
        [Test]
        public void SelectKthLargest_FindsValue()
        {
            using var values = new NativeArray<float>(new[] { 5f, 1f, 9f, 3f, 7f, 7f, 2f }, Allocator.Temp);

            Assert.AreEqual(9f, SwarmCorrectionSelectJob.SelectKthLargest(values, values.Length, 1));
            using var copy2 = new NativeArray<float>(new[] { 5f, 1f, 9f, 3f, 7f, 7f, 2f }, Allocator.Temp);
            Assert.AreEqual(7f, SwarmCorrectionSelectJob.SelectKthLargest(copy2, copy2.Length, 3));
            using var copy3 = new NativeArray<float>(new[] { 5f, 1f, 9f, 3f, 7f, 7f, 2f }, Allocator.Temp);
            Assert.AreEqual(1f, SwarmCorrectionSelectJob.SelectKthLargest(copy3, copy3.Length, 7));
        }

        [Test]
        public void Select_TakesHighestScoresUpToBudget_AndResetsThem()
        {
            const int count = 6;
            var ids = new[] { 10, 11, 12, 13, 14, 15 };
            var positions = new float2[count];
            var priorities = new float[32];
            for (var i = 0; i < count; i++)
            {
                positions[i] = new float2(i, -i);
                priorities[ids[i]] = 9f;
            }

            using var netId = new NativeArray<int>(ids, Allocator.TempJob);
            using var pos = new NativeArray<float2>(positions, Allocator.TempJob);
            using var vel = new NativeArray<float2>(count, Allocator.TempJob);

            // 13 は選べない(-1)。12 と 14 が同点。
            using var scores = new NativeArray<float>(new[] { 1f, 5f, 3f, -1f, 3f, 0.5f }, Allocator.TempJob);
            using var scratch = new NativeArray<float>(count, Allocator.TempJob);
            using var sent = new NativeArray<SwarmSentState>(32, Allocator.TempJob);
            using var priority = new NativeArray<float>(priorities, Allocator.TempJob);
            using var output = new NativeList<SwarmNetCorrection>(8, Allocator.TempJob);
            using var outputV = new NativeList<SwarmNetCorrectionV>(8, Allocator.TempJob);

            new SwarmCorrectionSelectJob
            {
                netId = netId,
                pos = pos,
                vel = vel,
                scores = scores,
                scratch = scratch,
                sent = sent,
                priority = priority,
                output = output,
                outputWithVelocity = outputV,
                count = count,
                budget = 2,
                withVelocity = 0,
                now = 7f,
                positionScale = SwarmNetQuantize.PositionScale
            }.Run();

            Assert.AreEqual(2, output.Length);
            Assert.AreEqual(0, outputV.Length);
            Assert.AreEqual(11, output[0].Id);

            // 同点の2つ(12と14)からは、枠の分(1つ)だけ選ぶ。
            Assert.AreEqual(12, output[1].Id);
            Assert.AreEqual(0f, priority[11]);
            Assert.AreEqual(9f, priority[14]);
            Assert.AreEqual(7f, sent[11].time);
            Assert.AreEqual(new float2(1f, -1f), sent[11].pos);
        }

        [Test]
        public void Select_WithVelocity_WritesVelocityFormat()
        {
            using var netId = new NativeArray<int>(new[] { 3 }, Allocator.TempJob);
            using var pos = new NativeArray<float2>(new[] { new float2(1f, 2f) }, Allocator.TempJob);
            using var vel = new NativeArray<float2>(new[] { new float2(0.5f, -1.5f) }, Allocator.TempJob);
            using var scores = new NativeArray<float>(new[] { 1f }, Allocator.TempJob);
            using var scratch = new NativeArray<float>(1, Allocator.TempJob);
            using var sent = new NativeArray<SwarmSentState>(8, Allocator.TempJob);
            using var priority = new NativeArray<float>(8, Allocator.TempJob);
            using var output = new NativeList<SwarmNetCorrection>(8, Allocator.TempJob);
            using var outputV = new NativeList<SwarmNetCorrectionV>(8, Allocator.TempJob);

            new SwarmCorrectionSelectJob
            {
                netId = netId,
                pos = pos,
                vel = vel,
                scores = scores,
                scratch = scratch,
                sent = sent,
                priority = priority,
                output = output,
                outputWithVelocity = outputV,
                count = 1,
                budget = 10,
                withVelocity = 1,
                now = 1f,
                positionScale = SwarmNetQuantize.PositionScale
            }.Run();

            Assert.AreEqual(0, output.Length);
            Assert.AreEqual(1, outputV.Length);
            Assert.AreEqual(500, outputV[0].X);
            Assert.AreEqual(1000, outputV[0].Y);
            Assert.AreEqual(0.5f, math.f16tof32(outputV[0].Vx));
            Assert.AreEqual(-1.5f, math.f16tof32(outputV[0].Vy));
        }
    }
}
