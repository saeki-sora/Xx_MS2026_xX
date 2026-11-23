using NUnit.Framework;

namespace MS2026.SoundStudio.Tests
{
    /// <summary>wav を落としたときの名前と置き場所。</summary>
    public sealed class SeImportNamingTests
    {
        private const string Root = "Assets/_Game/DDrive/SourceAssets";

        [TestCase("Start", "a.wav", ExpectedResult = "Start")]
        [TestCase("スタートを押した", "push_start.wav", ExpectedResult = "PushStart")]
        [TestCase("laser hit 2", "x.wav", ExpectedResult = "LaserHit2")]
        [TestCase("", "", ExpectedResult = "Sound")]
        [TestCase("ボタン", "ボタン.wav", ExpectedResult = "Sound")]
        [TestCase("", "9lives.wav", ExpectedResult = "S9lives")]
        public string SuggestName(string label, string file)
        {
            return SeImportNaming.SuggestName(label, file);
        }

        [Test]
        public void MakeFileName_StripsFolderPrefix()
        {
            Assert.That(SeImportNaming.MakeFileName(Root, SoundPageId.Title, "TitleStart", ".wav", false),
                Is.EqualTo("Assets/_Game/DDrive/SourceAssets/Se/Title/Start.wav"));
        }

        [Test]
        public void MakeFileName_KeepsNameWhenOnlyPrefix()
        {
            Assert.That(SeImportNaming.MakeFileName(Root, SoundPageId.Title, "Title", ".wav", false),
                Is.EqualTo("Assets/_Game/DDrive/SourceAssets/Se/Title/Title.wav"));
        }

        [Test]
        public void MakeFileName_BgmGoesToBgmFolder()
        {
            Assert.That(SeImportNaming.MakeFileName(Root, SoundPageId.Boss, "Battle", ".ogg", true),
                Is.EqualTo("Assets/_Game/DDrive/SourceAssets/Bgm/Boss/Battle.ogg"));
        }

        [Test]
        public void MakeFileName_SameNameTwice_SamePath()
        {
            Assert.That(SeImportNaming.MakeFileName(Root, SoundPageId.Match, "Hit", ".wav", false),
                Is.EqualTo(SeImportNaming.MakeFileName(Root, SoundPageId.Match, "Hit", ".wav", false)));
        }

        [TestCase("a.wav", ExpectedResult = true)]
        [TestCase("a.MP3", ExpectedResult = true)]
        [TestCase("a.ogg", ExpectedResult = true)]
        [TestCase("a.aif", ExpectedResult = true)]
        [TestCase("a.txt", ExpectedResult = false)]
        public bool IsSoundFile(string path)
        {
            return SeImportNaming.IsSoundFile(path);
        }
    }
}
