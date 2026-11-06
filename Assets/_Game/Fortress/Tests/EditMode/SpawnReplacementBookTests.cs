using MS2026.Fortress;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public class SpawnReplacementBookTests
    {
        private sealed class Kind
        {
        }

        [Test]
        public void ConsumesOnlyTheSameKind_AndCountsDown()
        {
            var germ = new Kind();
            var bomb = new Kind();
            var book = new SpawnReplacementBook<Kind>();
            book.Add(germ, 2);

            Assert.AreEqual(2, book.Pending);
            Assert.IsFalse(book.TryConsume(bomb), "ほかの種類は打ち消さない");
            Assert.IsTrue(book.TryConsume(germ));
            Assert.IsTrue(book.TryConsume(germ));
            Assert.IsFalse(book.TryConsume(germ), "使い切ったらふつうに出す");
            Assert.AreEqual(0, book.Pending);
            Assert.AreEqual(2, book.Replaced);
            Assert.AreEqual(0, book.PendingOf(germ));
        }

        [Test]
        public void IgnoresNullAndNonPositive_AndClearResets()
        {
            var germ = new Kind();
            var book = new SpawnReplacementBook<Kind>();
            book.Add(null, 5);
            book.Add(germ, 0);
            book.Add(germ, -3);
            Assert.AreEqual(0, book.Pending);
            Assert.IsFalse(book.TryConsume(null));

            book.Add(germ, 4);
            book.TryConsume(germ);
            book.Clear();
            Assert.AreEqual(0, book.Pending);
            Assert.AreEqual(0, book.Replaced);
            Assert.IsFalse(book.TryConsume(germ));
        }
    }
}
