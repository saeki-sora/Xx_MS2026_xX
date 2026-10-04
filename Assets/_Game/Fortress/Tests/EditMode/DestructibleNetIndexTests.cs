using MS2026.Fortress.Net;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class DestructibleNetIndexTests
    {
        [Test]
        public void SameLayout_SameHash()
        {
            var a = DestructibleNetIndex.ComputeLayoutHash(new[] { "Obstacles#0/Crate#0", "Obstacles#0/Crate#1" });
            var b = DestructibleNetIndex.ComputeLayoutHash(new[] { "Obstacles#0/Crate#0", "Obstacles#0/Crate#1" });

            Assert.AreEqual(a, b);
        }

        [Test]
        public void DifferentOrderOrContent_DifferentHash()
        {
            var original = DestructibleNetIndex.ComputeLayoutHash(new[] { "A#0", "B#0" });

            Assert.AreNotEqual(original, DestructibleNetIndex.ComputeLayoutHash(new[] { "B#0", "A#0" }));
            Assert.AreNotEqual(original, DestructibleNetIndex.ComputeLayoutHash(new[] { "A#0" }));
            Assert.AreNotEqual(original, DestructibleNetIndex.ComputeLayoutHash(new[] { "A#0B#0" }));
        }
    }
}
