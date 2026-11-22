using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace MS2026.SoundStudio.Tests
{
    /// <summary>名前の表（どの欄を、どのページに、何という名前で出すか）。</summary>
    public sealed class SoundCatalogTests
    {
        [TestCase("onStart", ExpectedResult = "onStart")]
        [TestCase("sounds.onReady", ExpectedResult = "onReady")]
        [TestCase("extraBgms.Array.data[3]", ExpectedResult = "extraBgms")]
        [TestCase("kinds.Array.data[1].onLand", ExpectedResult = "onLand")]
        public string FieldNameOf_LastField(string path)
        {
            return SoundCatalog.FieldNameOf(path);
        }

        [Test]
        public void Find_ByDeclaringTypeAndField()
        {
            var e = SoundCatalog.Entries[0];
            Assert.That(SoundCatalog.Find(e.DeclaringType, e.FieldName), Is.SameAs(e));
            Assert.That(SoundCatalog.Find("No.Such.Type", "x"), Is.Null);
        }

        [Test]
        public void Entries_AreUnique()
        {
            Assert.That(SoundCatalog.Entries.Select(x => x.DeclaringType + "|" + x.FieldName), Is.Unique);
        }

        [Test]
        public void Entries_FieldsExistOnTheirTypes()
        {
            foreach (var e in SoundCatalog.Entries)
            {
                var type = FindType(e.DeclaringType);
                Assert.That(type, Is.Not.Null, e.DeclaringType);
                var field = type.GetField(e.FieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null, e.DeclaringType + "." + e.FieldName);
            }
        }

        [Test]
        public void Entries_HaveJapaneseLabelAndGroup()
        {
            foreach (var e in SoundCatalog.Entries)
            {
                Assert.That(e.Label, Is.Not.Empty, e.FieldName);
                Assert.That(e.Group, Is.Not.Empty, e.FieldName);
                Assert.That(e.Page, Is.Not.EqualTo(SoundPageId.Start).And.Not.EqualTo(SoundPageId.Other), e.FieldName);
            }
        }

        [Test]
        public void Candidates_AreN1ToN20()
        {
            Assert.That(SoundCatalog.Candidates.Select(c => c.Number), Is.EqualTo(Enumerable.Range(1, 20).Select(i => "N" + i)));
        }

        [Test]
        public void Pages_InFlowOrder()
        {
            Assert.That((int)SoundPageId.Start, Is.LessThan((int)SoundPageId.Title));
            Assert.That((int)SoundPageId.Title, Is.LessThan((int)SoundPageId.Lobby));
            Assert.That((int)SoundPageId.Boss, Is.LessThan((int)SoundPageId.Result));
            Assert.That((int)SoundPageId.Result, Is.LessThan((int)SoundPageId.Other));
        }

        [Test]
        public void SeFolder_IsAsciiForEveryPage()
        {
            foreach (SoundPageId page in Enum.GetValues(typeof(SoundPageId)))
            {
                Assert.That(SoundPageIds.SeFolder(page), Does.Match("^[A-Za-z]+$"), page.ToString());
                Assert.That(SoundPageIds.Label(page), Is.Not.Empty, page.ToString());
            }
        }

        private static Type FindType(string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(fullName)).FirstOrDefault(t => t != null);
        }
    }
}
