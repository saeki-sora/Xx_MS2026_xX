using MS2026.Fortress.Net;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class NetMessageLimitsTests
    {
        // 取りこぼしてよい送信で配列を送る物は、全てNGOの上限(1296バイト)に収まる数に分けていること。
        [Test]
        public void UnreliablePayloads_FitWithinNgoLimit()
        {
            AssertFits<SwarmNetCorrection>();
            AssertFits<EnemyNetPosition>();
            AssertFits<DestructibleNetPosition>();
        }

        [Test]
        public void SwarmCorrection_IsSixBytes()
        {
            // 通信量の見積もり(3万体で約2.9Mbps)の前提。
            Assert.AreEqual(6, UnsafeUtility.SizeOf<SwarmNetCorrection>());
        }

        private static void AssertFits<T>() where T : struct
        {
            var count = NetMessageLimits.UnreliableItemsPerMessage<T>();
            Assert.Greater(count, 0, typeof(T).Name);
            Assert.LessOrEqual(count * UnsafeUtility.SizeOf<T>() + NetMessageLimits.HeaderAllowanceBytes, NetMessageLimits.UnreliableMaxBytes, typeof(T).Name);
        }
    }
}
