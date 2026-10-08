using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class FootprintBuilderTests
    {
        private static FootprintSettings Exact => new FootprintSettings
        {
            blockHeight = 1f,
            padding = 0f,
            closeGaps = 0f,
            resolution = 0.05f,
            simplify = 0.02f,
            fillHoles = true,
            minArea = 0.001f
        };

        /// <summary>床に置いた箱（XYが床、高さは-Z）の三角形。</summary>
        private static void Box(Vector3 min, Vector3 max, List<Vector3> v, List<int> t)
        {
            var o = v.Count;
            for (var i = 0; i < 8; i++)
            {
                v.Add(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z));
            }

            int[] faces =
            {
                0, 1, 3, 0, 3, 2, 4, 6, 7, 4, 7, 5, 0, 4, 5, 0, 5, 1,
                2, 3, 7, 2, 7, 6, 0, 2, 6, 0, 6, 4, 1, 5, 7, 1, 7, 3
            };
            foreach (var f in faces)
            {
                t.Add(o + f);
            }
        }

        [Test]
        public void BoxOnFloor_GivesItsRectangle()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Box(new Vector3(-1f, -0.5f, -0.5f), new Vector3(1f, 0.5f, 0f), v, t);

            var paths = FootprintBuilder.Build(v, t, Exact, out var report);

            Assert.AreEqual(1, paths.Count);
            Assert.AreEqual(2f, report.Area, 0.25f, "2×1 の箱は面積およそ2");
            var bounds = BoundsOf(paths[0]);
            Assert.AreEqual(-1f, bounds.xMin, 0.1f);
            Assert.AreEqual(1f, bounds.xMax, 0.1f);
            Assert.AreEqual(-0.5f, bounds.yMin, 0.1f);
            Assert.AreEqual(0.5f, bounds.yMax, 0.1f);
            Assert.Greater(FootprintContour.SignedArea(paths[0]), 0f, "外周は反時計回り");
        }

        [Test]
        public void FloatingPartAboveBlockHeight_IsIgnored()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            // 高さ 2〜3 に浮いた板（下をくぐれる）
            Box(new Vector3(-1f, -1f, -3f), new Vector3(1f, 1f, -2f), v, t);

            var paths = FootprintBuilder.Build(v, t, Exact, out _);

            Assert.AreEqual(0, paths.Count);
        }

        [Test]
        public void HollowTube_IsFilledWhenFillHolesIsOn()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            // 底も天井も無い四角い筒（4枚の壁）。真上から見ると輪だけになる。
            Box(new Vector3(-1f, -1f, -0.5f), new Vector3(-0.9f, 1f, 0f), v, t);
            Box(new Vector3(0.9f, -1f, -0.5f), new Vector3(1f, 1f, 0f), v, t);
            Box(new Vector3(-1f, -1f, -0.5f), new Vector3(1f, -0.9f, 0f), v, t);
            Box(new Vector3(-1f, 0.9f, -0.5f), new Vector3(1f, 1f, 0f), v, t);

            var filled = FootprintBuilder.Build(v, t, Exact, out var filledReport);
            var settings = Exact;
            settings.fillHoles = false;
            var hollow = FootprintBuilder.Build(v, t, settings, out var hollowReport);

            Assert.AreEqual(1, filled.Count, "穴埋めONなら外周1つだけ");
            Assert.AreEqual(4f, filledReport.Area, 0.4f);
            Assert.AreEqual(2, hollow.Count, "穴埋めOFFなら外周＋穴");
            Assert.Less(hollowReport.Area, 1.5f, "穴の分だけ面積が減る（穴は負の面積）");
        }

        [Test]
        public void Padding_GrowsTheShape()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Box(new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, 0.5f, 0f), v, t);
            var settings = Exact;
            settings.padding = 0.25f;

            var paths = FootprintBuilder.Build(v, t, settings, out _);

            var bounds = BoundsOf(paths[0]);
            Assert.AreEqual(-0.75f, bounds.xMin, 0.1f);
            Assert.AreEqual(0.75f, bounds.xMax, 0.1f);
        }

        [Test]
        public void ThinVerticalWall_StillBlocks()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            // 厚み0の縦の板（真上から見ると線）
            v.Add(new Vector3(-1f, 0f, 0f));
            v.Add(new Vector3(1f, 0f, 0f));
            v.Add(new Vector3(1f, 0f, -0.5f));
            v.Add(new Vector3(-1f, 0f, -0.5f));
            t.AddRange(new[] { 0, 1, 2, 0, 2, 3 });

            var paths = FootprintBuilder.Build(v, t, Exact, out var report);

            Assert.AreEqual(1, paths.Count);
            Assert.Greater(report.CellsFilled, 30);
        }

        [Test]
        public void TwoSeparateBoxes_GiveTwoShapes_AndCloseGapsMergesThem()
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            Box(new Vector3(-1f, -0.5f, -0.5f), new Vector3(-0.05f, 0.5f, 0f), v, t);
            Box(new Vector3(0.05f, -0.5f, -0.5f), new Vector3(1f, 0.5f, 0f), v, t);

            var apart = FootprintBuilder.Build(v, t, Exact, out _);
            var settings = Exact;
            settings.closeGaps = 0.3f;
            var merged = FootprintBuilder.Build(v, t, settings, out _);

            Assert.AreEqual(2, apart.Count, "0.1の隙間は残る");
            Assert.AreEqual(1, merged.Count, "隙間埋め0.3でつながる");
        }

        [Test]
        public void Simplify_KeepsSquareCorners()
        {
            var loop = new List<Vector2>();
            for (var i = 0; i < 10; i++) loop.Add(new Vector2(i * 0.1f, 0f));
            for (var i = 0; i < 10; i++) loop.Add(new Vector2(1f, i * 0.1f));
            for (var i = 0; i < 10; i++) loop.Add(new Vector2(1f - i * 0.1f, 1f));
            for (var i = 0; i < 10; i++) loop.Add(new Vector2(0f, 1f - i * 0.1f));

            var simplified = FootprintContour.Simplify(loop.ToArray(), 0.01f);

            Assert.AreEqual(4, simplified.Length);
        }

        private static Rect BoundsOf(Vector2[] path)
        {
            var min = path[0];
            var max = path[0];
            foreach (var p in path)
            {
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
