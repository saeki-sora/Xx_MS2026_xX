using NUnit.Framework;

namespace MS2026.Fortress.Tests.EditMode
{
    /// <summary>サウンドスタジオ: 「どのPCで鳴らすか」と「最終の音量」の決まり。</summary>
    public sealed class FortressSoundRulesTests
    {
        // 1行 = (鳴らすPC, 持ち主, 自分, Hostか, 持ち主が人か) → 鳴らすか
        [TestCase(SoundAudience.Everyone, 2, 0, false, true, ExpectedResult = true)]
        [TestCase(SoundAudience.OwnerOnly, 2, 0, false, true, ExpectedResult = false)]
        [TestCase(SoundAudience.OwnerOnly, 0, 0, false, true, ExpectedResult = true)]
        [TestCase(SoundAudience.OwnerOnly, 2, -1, false, true, ExpectedResult = true)] // ネット無し・1人
        [TestCase(SoundAudience.OwnerOnly, -1, 0, false, true, ExpectedResult = true)] // 持ち主が分からない
        [TestCase(SoundAudience.OwnerOnly, 2, 0, true, false, ExpectedResult = true)] // CPU の席 → Host で鳴らす
        [TestCase(SoundAudience.OwnerOnly, 2, 1, false, false, ExpectedResult = false)] // CPU の席 → Client では鳴らさない
        [TestCase(SoundAudience.OwnerOnly, 2, 0, true, true, ExpectedResult = false)] // Host でも、人がいる別の席の音は鳴らさない
        [TestCase(SoundAudience.Auto, 2, 0, false, true, ExpectedResult = false)] // 番号あり → その人だけ
        [TestCase(SoundAudience.Auto, -1, 0, false, true, ExpectedResult = true)] // 番号なし → 全員
        public bool ShouldPlay_Cases(SoundAudience audience, int owner, int local, bool isHost, bool ownerIsHuman)
        {
            return FortressSoundRules.ShouldPlay(audience, owner, local, isHost, ownerIsHuman);
        }

        [Test]
        public void FinalVolume_Multiplies()
        {
            Assert.That(FortressSoundRules.FinalVolume(0.8f, 0.5f, 0.5f), Is.EqualTo(0.2f).Within(1e-5f));
        }

        [Test]
        public void FinalVolume_ClampsToOne()
        {
            Assert.That(FortressSoundRules.FinalVolume(1f, 1f, 2f), Is.EqualTo(1f));
        }

        [Test]
        public void FinalVolume_ClampsNegative()
        {
            Assert.That(FortressSoundRules.FinalVolume(1f, -1f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void NewEffect_DefaultsKeepCurrentBehaviour()
        {
            var effect = new FortressEffect();
            Assert.That(effect.audience, Is.EqualTo(SoundAudience.Auto));
            Assert.That(effect.volumeScale, Is.EqualTo(1f));
            Assert.That(effect.isVoice, Is.False);
        }
    }
}
