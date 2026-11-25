using System.Collections.Generic;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.Fortress;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.SoundStudio.Tests
{
    /// <summary>数え方と、設定ファイルの欄の書き換え。</summary>
    public sealed class SoundStudioEditTests
    {
        private TestSoundHolder _holder;

        [SetUp]
        public void SetUp()
        {
            _holder = ScriptableObject.CreateInstance<TestSoundHolder>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_holder);
        }

        private SoundSlot SlotOf(string path)
        {
            var list = new List<SoundSlot>();
            SoundSlotScanner.AddSlotsOf(_holder, SoundSlotPlace.Asset, null, list);
            return list.Find(s => s.PropertyPath == path);
        }

        [Test]
        public void ArrayElements_GetNumberedLabels()
        {
            Assert.That(SlotOf("list.Array.data[1]").Label, Does.EndWith("（2）"));
            Assert.That(SlotOf("a").Label, Does.Not.EndWith("）"));
        }

        [Test]
        public void SwitchedOffFeature_IsInactiveAndNotCountedMissing()
        {
            var bomb = SlotOf("bomb.onExplode");
            Assert.That(bomb.Active, Is.False, "爆弾ではない（explodeOnDeath が OFF）なら、使っていない欄");
            _holder.bomb.explodeOnDeath = true;
            Assert.That(SlotOf("bomb.onExplode").Active, Is.True);
            Assert.That(SoundStudioContext.CountMissing(new[] { bomb }), Is.EqualTo(0));
        }

        [Test]
        public void ElementLabel_UsesElementName()
        {
            Assert.That(SlotOf("named.Array.data[0].voice").Label, Does.EndWith("（赤）"));
            Assert.That(SlotOf("named.Array.data[1].voice").Label, Does.EndWith("（2）"));
        }

        [Test]
        public void SameSlots_FindsSameFieldOnOtherHolders_OnlyActive()
        {
            SoundSlot Make(string asset, string field, bool active = true, SoundSlotKind kind = SoundSlotKind.Effect) => new SoundSlot
            {
                Kind = kind, DeclaringType = typeof(TestSoundHolder), FieldName = field, AssetPath = asset, PropertyPath = field, Active = active,
            };

            var a = Make("A", "a");
            var b = Make("B", "a");
            var otherField = Make("C", "hidden");
            var inactive = Make("D", "a", active: false);
            var bgm = Make("E", "a", kind: SoundSlotKind.Bgm);
            var same = SoundStudioContext.SameSlots(a, new[] { a, b, otherField, inactive, bgm });
            Assert.That(same, Is.EqualTo(new[] { b }));
        }

        [Test]
        public void SameSlots_NoneForCandidate()
        {
            var c = new SoundSlot { Kind = SoundSlotKind.Candidate };
            Assert.That(SoundStudioContext.SameSlots(c, new[] { c }), Is.Empty);
        }

        [Test]
        public void CountMissing_IgnoresCandidates()
        {
            var slots = new[]
            {
                new SoundSlot { Kind = SoundSlotKind.Candidate },
                new SoundSlot { Kind = SoundSlotKind.Effect, HasSound = true },
            };
            Assert.That(SoundStudioContext.CountMissing(slots), Is.EqualTo(0));
        }

        [Test]
        public void CountMissing_CountsEmptyEffectAndBgm()
        {
            var slots = new[]
            {
                new SoundSlot { Kind = SoundSlotKind.Effect, HasSound = false },
                new SoundSlot { Kind = SoundSlotKind.Bgm, HasSound = false },
                new SoundSlot { Kind = SoundSlotKind.Effect, HasSound = true },
            };
            Assert.That(SoundStudioContext.CountMissing(slots), Is.EqualTo(2));
        }

        [Test]
        public void SetVolumeScale_ChangesAssetAndSnapshot()
        {
            var slot = SlotOf("nested.inner");
            Assert.That(SoundSlotWriter.SetVolumeScale(slot, 0.5f), Is.EqualTo(SoundWriteResult.Saved));
            Assert.That(_holder.nested.inner.volumeScale, Is.EqualTo(0.5f));
            Assert.That(((FortressEffect)slot.Value).volumeScale, Is.EqualTo(0.5f));
        }

        [Test]
        public void SetVolumeScale_ClampsToZeroOne()
        {
            var slot = SlotOf("a");
            SoundSlotWriter.SetVolumeScale(slot, 3f);
            Assert.That(_holder.a.volumeScale, Is.EqualTo(1f));
        }

        [Test]
        public void SetSe_SetsIdAndMarksVoiceFromCatalog()
        {
            var slot = SlotOf("list.Array.data[1]");
            slot.Voice = true;
            SoundSlotWriter.SetSe(slot, new AssetId<SeMarker>(1234, AssetType.Se));
            Assert.That(_holder.list[1].se.Value, Is.EqualTo(1234UL));
            Assert.That(_holder.list[1].isVoice, Is.True);
            Assert.That(slot.HasSound, Is.True);
        }

        [Test]
        public void SetBgm_SetsId()
        {
            var slot = SlotOf("music");
            SoundSlotWriter.SetBgm(slot, new AssetId<BgmMarker>(99, AssetType.Bgm));
            Assert.That(_holder.music.Value, Is.EqualTo(99UL));
        }

        [Test]
        public void ResetPlace_RestoresDefaults()
        {
            var slot = SlotOf("a");
            SoundSlotWriter.SetVolumeScale(slot, 0.2f);
            SoundSlotWriter.SetAudience(slot, SoundAudience.Everyone);
            SoundSlotWriter.ResetPlace(slot);
            Assert.That(_holder.a.volumeScale, Is.EqualTo(1f));
            Assert.That(_holder.a.audience, Is.EqualTo(SoundAudience.Auto));
        }

        [Test]
        public void IsChanged_TrueOnlyWhenNotDefault()
        {
            var slot = SlotOf("a");
            Assert.That(SoundSlotWriter.IsChanged(slot), Is.False);
            SoundSlotWriter.SetVolumeScale(slot, 0.7f);
            Assert.That(SoundSlotWriter.IsChanged(slot), Is.True);
        }
    }
}
