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
            AssertFits<EnemyNetPosition>();
            AssertFits<DestructibleNetPosition>();
        }

        [Test]
        public void SwarmEvent_Is14Bytes()
        {
            // 写真の「新しく出た敵」「消えた敵」1件の大きさ(通信量の見積もりの前提)。
            Assert.AreEqual(14, UnsafeUtility.SizeOf<SwarmNetEvent>());
        }

        private static void AssertFits<T>() where T : struct
        {
            var count = NetMessageLimits.UnreliableItemsPerMessage<T>();
            Assert.Greater(count, 0, typeof(T).Name);
            Assert.LessOrEqual(count * UnsafeUtility.SizeOf<T>() + NetMessageLimits.HeaderAllowanceBytes, NetMessageLimits.UnreliableMaxBytes, typeof(T).Name);
        }
    }
}
