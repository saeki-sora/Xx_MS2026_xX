using MS2026.Fortress.Net;
using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class FortressLaunchArgsTests
    {
        [Test]
        public void NoArgs_DoesNothing()
        {
            var args = FortressLaunchArgs.Parse(new[] { "MS2026.exe" });

            Assert.AreEqual(FortressLaunchArgs.StartMode.None, args.Start);
            Assert.IsNull(args.PlayerIndex);
            Assert.IsNull(args.Address);
            Assert.IsFalse(args.Tile);
        }

        [Test]
        public void ClientLaunch_IsParsed()
        {
            var args = FortressLaunchArgs.Parse(new[]
            {
                "MS2026.exe", "-screen-fullscreen", "0",
                "-fortress-start", "Client", "-fortress-player", "2", "-fortress-address", "192.168.0.10", "-fortress-tile"
            });

            Assert.AreEqual(FortressLaunchArgs.StartMode.Client, args.Start);
            Assert.AreEqual(2, args.PlayerIndex);
            Assert.AreEqual("192.168.0.10", args.Address);
            Assert.IsTrue(args.Tile);
        }

        [TestCase("4")]
        [TestCase("-1")]
        [TestCase("abc")]
        public void InvalidPlayer_IsIgnored(string value)
        {
            var args = FortressLaunchArgs.Parse(new[] { "-fortress-player", value });

            Assert.IsNull(args.PlayerIndex);
        }

        [Test]
        public void UnknownStartMode_IsNone()
        {
            var args = FortressLaunchArgs.Parse(new[] { "-fortress-start", "ui" });

            Assert.AreEqual(FortressLaunchArgs.StartMode.None, args.Start);
        }

        [Test]
        public void MissingValueAtEnd_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => FortressLaunchArgs.Parse(new[] { "-fortress-player" }));
        }
    }
}
