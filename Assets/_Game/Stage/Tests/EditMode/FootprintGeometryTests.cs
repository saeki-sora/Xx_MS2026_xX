using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class FootprintGeometryTests
    {
        private static Vector2[] Square(float x, float y, float size) => new[]
        {
            new Vector2(x, y), new Vector2(x + size, y), new Vector2(x + size, y + size), new Vector2(x, y + size)
        };

        [Test]
        public void ClosestPoints_MeasuresTheGapBetweenTwoSquares()
        {
            var d = FootprintGeometry.ClosestPoints(Square(0f, 0f, 1f), Square(1.5f, 0.2f, 1f), out var a, out var b);

            Assert.AreEqual(0.5f, d, 1e-4f);
            Assert.AreEqual(1f, a.x, 1e-4f);
            Assert.AreEqual(1.5f, b.x, 1e-4f);
        }

        [Test]
        public void ClosestPoints_OverlappingIsZero()
        {
            var d = FootprintGeometry.ClosestPoints(Square(0f, 0f, 1f), Square(0.5f, 0.5f, 1f), out _, out _);

            Assert.AreEqual(0f, d, 1e-4f);
        }

        [Test]
        public void ClosestPoints_InsideIsZero()
        {
            var d = FootprintGeometry.ClosestPoints(Square(0f, 0f, 4f), Square(1f, 1f, 1f), out _, out _);

            Assert.AreEqual(0f, d, 1e-4f);
        }

        [Test]
        public void Contains_PointInsideAndOutside()
        {
            var square = Square(0f, 0f, 1f);

            Assert.IsTrue(FootprintGeometry.Contains(square, new Vector2(0.5f, 0.5f)));
            Assert.IsFalse(FootprintGeometry.Contains(square, new Vector2(1.5f, 0.5f)));
        }
    }
}
