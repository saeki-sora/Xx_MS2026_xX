using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class AmbientMoteAtlasTests
    {
        [Test]
        public void TilesSplitTheTextureIntoFourColumns()
        {
            var rect = AmbientMoteAtlas.TileRect(AmbientMoteAtlas.Bubble);
            Assert.AreEqual(0.5f, rect.x, 1e-6f);
            Assert.AreEqual(0.25f, rect.z, 1e-6f);
            Assert.AreEqual(1f, rect.w, 1e-6f);
        }

        [Test]
        public void BubbleIsAHollowRing()
        {
            // 輪の上は濃く、真ん中はほとんど透明（塗りつぶした敵と見分けるため）。
            var centre = AmbientMoteAtlas.Tile / 2;
            Assert.Less(AmbientMoteAtlas.BubbleAlpha(centre, centre), 0.15f);
            Assert.Greater(AmbientMoteAtlas.BubbleAlpha(centre + 25, centre), 0.75f);
        }

        [Test]
        public void SteamFadesOutTowardTheEdge()
        {
            var centre = AmbientMoteAtlas.Tile / 2;
            Assert.Greater(AmbientMoteAtlas.SteamAlpha(centre, centre), 0.95f);
            Assert.AreEqual(0f, AmbientMoteAtlas.SteamAlpha(0, 0), 1e-4f);
        }
    }
}
