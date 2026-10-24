using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class WaveRouteShapesTests
    {
        [Test]
        public void SimplifyDropsPointsOnAStraightLine()
        {
            var points = new List<Vector2>();
            for (var i = 0; i <= 10; i++)
            {
                points.Add(new Vector2(i, 0.01f * (i % 2)));
            }

            points.Add(new Vector2(10f, 5f));
            var result = WaveRouteShapes.Simplify(points, 0.1f);
            Assert.AreEqual(3, result.Count, "始点・曲がり角・終点だけ");
            Assert.AreEqual(new Vector2(10f, 5f), result[2]);
        }

        [Test]
        public void SmoothKeepsTheEnds()
        {
            var points = new List<Vector2> { Vector2.zero, new Vector2(4f, 0f), new Vector2(4f, 4f) };
            var smooth = WaveRouteShapes.Smooth(points, 2);
            Assert.AreEqual(Vector2.zero, smooth[0]);
            Assert.AreEqual(new Vector2(4f, 4f), smooth[smooth.Count - 1]);
            Assert.Greater(smooth.Count, points.Count);
        }

        [Test]
        public void PointAtWalksAlongTheLine()
        {
            var points = new List<Vector2> { Vector2.zero, new Vector2(2f, 0f), new Vector2(2f, 3f) };
            Assert.AreEqual(5f, WaveRouteShapes.Length(points), 1e-4f);
            var p = WaveRouteShapes.PointAt(points, 3f, out var dir);
            Assert.AreEqual(new Vector2(2f, 1f), p);
            Assert.AreEqual(Vector2.up, dir);
            Assert.AreEqual(new Vector2(2f, 3f), WaveRouteShapes.PointAt(points, 99f, out _));
        }

        [Test]
        public void MoreEnemiesMakeAThickerLine()
        {
            var few = WaveRouteShapes.WidthFor(5, 0.3f, 0.9f);
            var many = WaveRouteShapes.WidthFor(100, 0.3f, 0.9f);
            Assert.AreEqual(0.3f, few, 1e-4f);
            Assert.AreEqual(0.9f, many, 1e-4f);
            Assert.Greater(WaveRouteShapes.WidthFor(30, 0.3f, 0.9f), few);
        }
    }
}
