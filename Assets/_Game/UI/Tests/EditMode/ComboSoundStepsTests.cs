using MS2026.UI.Game;
using NUnit.Framework;

namespace MS2026.UI.Tests.EditMode
{
    /// <summary>コンボの音を鳴らす区切り（10・50・100、その後は 100 ごと。サウンドスタジオ N10）。</summary>
    public sealed class ComboSoundStepsTests
    {
        [TestCase(9, 10, ExpectedResult = true)]
        [TestCase(10, 11, ExpectedResult = false)]
        [TestCase(48, 52, ExpectedResult = true)]
        [TestCase(99, 100, ExpectedResult = true)]
        [TestCase(150, 199, ExpectedResult = false)]
        [TestCase(199, 200, ExpectedResult = true)]
        [TestCase(5, 0, ExpectedResult = false)]
        [TestCase(0, 9, ExpectedResult = false)]
        public bool Reached(int before, int after)
        {
            return ComboSoundSteps.Reached(before, after);
        }
    }
}
