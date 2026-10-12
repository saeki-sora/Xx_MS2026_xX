using MS2026.Stage.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StageSymmetryMathTests
    {
        [Test]
        public void CopyAngles_SkipTheOriginal()
        {
            CollectionAssert.AreEqual(new[] { 180f }, StageSymmetryMath.CopyAngles(2));
            CollectionAssert.AreEqual(new[] { 90f, 180f, 270f }, StageSymmetryMath.CopyAngles(4));
            Assert.IsEmpty(StageSymmetryMath.CopyAngles(1));
        }

        [Test]
        public void RotateAround_TurnsCounterClockwiseAboutCenter()
        {
            var p = StageSymmetryMath.RotateAround(new Vector2(3f, 1f), new Vector2(1f, 1f), 90f);
            Assert.AreEqual(1f, p.x, 1e-4f);
            Assert.AreEqual(3f, p.y, 1e-4f);
            var opposite = StageSymmetryMath.RotateAround(new Vector2(2f, -1f), Vector2.zero, 180f);
            Assert.AreEqual(-2f, opposite.x, 1e-4f);
            Assert.AreEqual(1f, opposite.y, 1e-4f);
        }

        [Test]
        public void IsAtCenter_UsesTolerance()
        {
            Assert.IsTrue(StageSymmetryMath.IsAtCenter(new Vector2(0.01f, 0f), Vector2.zero));
            Assert.IsFalse(StageSymmetryMath.IsAtCenter(new Vector2(0.5f, 0f), Vector2.zero));
        }
    }
}
