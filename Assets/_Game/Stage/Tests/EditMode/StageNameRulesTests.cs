using MS2026.Stage.EditorTools;
using NUnit.Framework;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StageNameRulesTests
    {
        [TestCase("Floor_Main", StagePropRole.Floor)]
        [TestCase("sink_plate", StagePropRole.Floor)]
        [TestCase("床", StagePropRole.Floor)]
        [TestCase("WaterPuddle", StagePropRole.SlowZone)]
        [TestCase("泡_01", StagePropRole.SlowZone)]
        [TestCase("GlassCup", StagePropRole.DestructibleWall)]
        [TestCase("deco_flower", StagePropRole.Decoration)]
        [TestCase("Shampoo", StagePropRole.Wall)]
        [TestCase("ToothBrush", StagePropRole.Wall)]
        public void GuessRole(string name, StagePropRole expected)
        {
            Assert.AreEqual(expected, StageNameRules.GuessRole(name));
        }

        [Test]
        public void ParseMarker_CoreTurretSpawn()
        {
            Assert.AreEqual(StageMarkerKind.Core, StageNameRules.ParseMarker("MARK_Core", out _));
            Assert.AreEqual(StageMarkerKind.Turret, StageNameRules.ParseMarker("MARK_Turret3", out var turret));
            Assert.AreEqual(2, turret);
            Assert.AreEqual(StageMarkerKind.Turret, StageNameRules.ParseMarker("mark_p1", out var p1));
            Assert.AreEqual(0, p1);
            Assert.AreEqual(StageMarkerKind.Spawn, StageNameRules.ParseMarker("MARK_Spawn_North", out _));
        }

        [Test]
        public void ParseMarker_RejectsOthers()
        {
            Assert.AreEqual(StageMarkerKind.None, StageNameRules.ParseMarker("Shampoo", out _));
            Assert.AreEqual(StageMarkerKind.None, StageNameRules.ParseMarker("MARK_Turret5", out _));
            Assert.AreEqual(StageMarkerKind.None, StageNameRules.ParseMarker("MARK_Something", out _));
        }
    }
}
