using System;
using System.Collections.Generic;
using System.Linq;
using DDrive.Foundation.Identity;
using DDrive.Runtime.Audio;
using MS2026.Fortress;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.SoundStudio.Tests
{
    /// <summary>テスト用: いろいろな置き方の音の欄を持つ設定。</summary>
    public sealed class TestSoundHolder : ScriptableObject
    {
        [Serializable]
        public sealed class Nested
        {
            public FortressEffect inner = new FortressEffect();
        }

        public FortressEffect a = new FortressEffect();
        public FortressEffect[] list = { new FortressEffect(), new FortressEffect() };
        public Nested nested = new Nested();
        public List<Nested> nestedList = new List<Nested> { new Nested() };
        public AssetId<BgmMarker> music;
        [SerializeField] private FortressEffect hidden = new FortressEffect();
        [NonSerialized] public FortressEffect notSaved = new FortressEffect();
        public static FortressEffect StaticOne = new FortressEffect();
        public ScriptableObject otherAsset;

        public FortressEffect Hidden => hidden;
    }

    public sealed class SlotPathFinderTests
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
            UnityEngine.Object.DestroyImmediate(_holder);
        }

        [Test]
        public void FindSlots_FindsDirectArrayNestedListAndPrivate()
        {
            var paths = SlotPathFinder.FindSlots(_holder).Where(s => !s.IsBgm).Select(s => s.Path);
            Assert.That(paths, Is.EquivalentTo(new[]
            {
                "a", "list.Array.data[0]", "list.Array.data[1]", "nested.inner", "nestedList.Array.data[0].inner", "hidden",
            }));
        }

        [Test]
        public void FindSlots_FindsBgm()
        {
            Assert.That(SlotPathFinder.FindSlots(_holder).Where(s => s.IsBgm).Select(s => s.Path), Is.EqualTo(new[] { "music" }));
        }

        [Test]
        public void FindSlots_KnowsDeclaringTypeAndField()
        {
            var inner = SlotPathFinder.FindSlots(_holder).Single(s => s.Path == "nested.inner");
            Assert.That(inner.DeclaringType, Is.EqualTo(typeof(TestSoundHolder.Nested)));
            Assert.That(inner.Field.Name, Is.EqualTo("inner"));
        }

        [Test]
        public void FindSlots_ReturnsTheLiveObject()
        {
            var slot = SlotPathFinder.FindSlots(_holder).Single(s => s.Path == "list.Array.data[1]");
            Assert.That(slot.Value, Is.SameAs(_holder.list[1]));
        }

        [Test]
        public void MayHoldSlots_TrueForHolder_FalseForPlainType()
        {
            Assert.That(SlotPathFinder.MayHoldSlots(typeof(TestSoundHolder)), Is.True);
            Assert.That(SlotPathFinder.MayHoldSlots(typeof(Transform)), Is.False);
        }
    }
}
