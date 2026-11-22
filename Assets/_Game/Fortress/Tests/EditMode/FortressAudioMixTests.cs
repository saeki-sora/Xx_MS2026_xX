using DDrive.Foundation.Handle;
using DDrive.Runtime.Audio;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    /// <summary>サウンドスタジオ: 全体音量の読み方と、D-Drive の準備がないときに壊れないこと。</summary>
    public sealed class FortressAudioMixTests
    {
        private FortressSoundBalance _previous;

        [SetUp]
        public void SetUp()
        {
            _previous = FortressSoundBalance.Current;
        }

        [TearDown]
        public void TearDown()
        {
            FortressSoundBalance.Current = _previous;
        }

        [Test]
        public void ApplySeVolume_NoBootstrap_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => FortressAudioMix.ApplySeVolume(Handle<SeMarker>.Invalid, default, 0.5f, false));
        }

        [Test]
        public void Master_UsesVoiceVolumeForVoice()
        {
            var balance = ScriptableObject.CreateInstance<FortressSoundBalance>();
            balance.seVolume = 0.3f;
            balance.voiceVolume = 0.7f;
            FortressSoundBalance.Current = balance;
            Assert.That(FortressAudioMix.Master(true), Is.EqualTo(0.7f));
            Assert.That(FortressAudioMix.Master(false), Is.EqualTo(0.3f));
            Object.DestroyImmediate(balance);
        }

        [Test]
        public void Master_NoBalance_IsOne()
        {
            FortressSoundBalance.Current = null;
            Assert.That(FortressAudioMix.Master(false), Is.EqualTo(1f));
            Assert.That(FortressAudioMix.BgmMaster, Is.EqualTo(1f));
        }

        [Test]
        public void PlaySound_NotReady_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => FortressEffectPlayer.PlaySound(new FortressEffect(), Vector3.zero, 2));
        }

        [Test]
        public void PlaySound_NotReady_ReturnsInvalid()
        {
            Assert.That(FortressEffectPlayer.PlaySound(new FortressEffect(), Vector3.zero, 2), Is.EqualTo(Handle<SeMarker>.Invalid));
        }

        [Test]
        public void PlayBgm_NoBootstrap_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => FortressAudioMix.PlayBgm(default));
        }
    }
}
