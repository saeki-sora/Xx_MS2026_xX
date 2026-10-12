using System.Collections.Generic;
using MS2026.UI.EditorTools;
using NUnit.Framework;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class UiPlayChangeListTests
    {
        private static UiPlayChange Change(string path, string value, string asset = "Assets/A.prefab", string name = "Title", int index = 0, string before = "")
        {
            var change = new UiPlayChange
            {
                propertyPath = path,
                value = value,
                after = value,
                before = before,
                target = new UiPlayTarget { destination = UiPlayDestination.Prefab, assetPath = asset, componentType = "T", componentIndex = 0 }
            };
            change.target.names.Add(name);
            change.target.indices.Add(index);
            return change;
        }

        [Test]
        public void Merge_SameFieldKeepsFirstBeforeAndOrderButTakesLatestValue()
        {
            var list = new UiPlayChangeList(new List<UiPlayChange>());
            Assert.IsTrue(list.Merge(Change("m_FontSize", "30", before: "24")));
            Assert.IsTrue(list.Merge(Change("m_Color.r", "1")));
            Assert.IsFalse(list.Merge(Change("m_FontSize", "40", before: "30")), "同じ欄は1行にまとめる");

            Assert.AreEqual(2, list.Count);
            Assert.AreEqual("m_FontSize", list.Items[0].propertyPath, "並びは最初のまま");
            Assert.AreEqual("40", list.Items[0].value, "値は最新");
            Assert.AreEqual("24", list.Items[0].before, "変える前の値は最初のまま");
        }

        [Test]
        public void Merge_KeepsUserChoice()
        {
            var list = new UiPlayChangeList(new List<UiPlayChange>());
            list.Merge(Change("m_FontSize", "30"));
            list.Items[0].keep = false;
            list.Merge(Change("m_FontSize", "31"));
            Assert.IsFalse(list.Items[0].keep, "一覧で外したチェックは、また変えても外れたまま");
        }

        [Test]
        public void Key_SeparatesSameNameSiblingsAndDestinations()
        {
            Assert.AreNotEqual(Change("m_Text", "a", index: 0).Key, Change("m_Text", "a", index: 1).Key);
            Assert.AreNotEqual(Change("m_Text", "a", asset: "Assets/A.prefab").Key, Change("m_Text", "a", asset: "Assets/B.prefab").Key);
            Assert.AreEqual(Change("m_Text", "a").Key, Change("m_Text", "b").Key);
        }

        [Test]
        public void ApplyOrder_PutsArraySizesFirst()
        {
            var order = UiPlayChangeList.ApplyOrder(new[]
            {
                Change("entries.Array.data[2].motion.delay", "0.1"),
                Change("entries.Array.size", "3"),
                Change("m_Name", "X")
            });

            Assert.AreEqual("entries.Array.size", order[0].propertyPath);
            Assert.AreEqual("entries.Array.data[2].motion.delay", order[1].propertyPath);
            Assert.AreEqual("m_Name", order[2].propertyPath);
        }

        [Test]
        public void GroupByDestination_KeepsFirstSeenOrder()
        {
            var groups = UiPlayChangeList.GroupByDestination(new[]
            {
                Change("a", "1", asset: "Assets/B.prefab"),
                Change("b", "1", asset: "Assets/A.prefab"),
                Change("c", "1", asset: "Assets/B.prefab")
            });

            Assert.AreEqual(2, groups.Count);
            Assert.AreEqual("Assets/B.prefab", groups[0][0].target.assetPath);
            Assert.AreEqual(2, groups[0].Count);
        }
    }

    public sealed class UiPlayPathTests
    {
        [Test]
        public void SameNameIndex_RoundTripsThroughFindPosition()
        {
            var siblings = new[] { "Button", "Label", "Button", "Button" };
            for (var i = 0; i < siblings.Length; i++)
            {
                var index = UiPlayPath.IndexAmongSameName(siblings, i);
                Assert.AreEqual(i, UiPlayPath.FindPosition(siblings, siblings[i], index));
            }

            Assert.AreEqual(2, UiPlayPath.IndexAmongSameName(siblings, 3));
        }

        [Test]
        public void FindPosition_ToleratesRuntimeObjectsWithOtherNames()
        {
            // Play 中に足された物（[Modal Blocker] など）が前にいても、同じ名前の何番目かで見つかる。
            var prefab = new[] { "Title", "Button", "Button" };
            var runtime = new[] { "[Modal Blocker]", "Title", "Button", "Button" };
            var index = UiPlayPath.IndexAmongSameName(runtime, 3);
            Assert.AreEqual(2, UiPlayPath.FindPosition(prefab, "Button", index));
            Assert.AreEqual(-1, UiPlayPath.FindPosition(prefab, "Row(Clone)", 0));
        }

        [Test]
        public void Describe_JoinsWithArrows()
        {
            Assert.AreEqual("ロビー › Panel › Title", UiPlayPath.Describe("ロビー", new[] { "Panel", "Title" }));
            Assert.AreEqual("A", UiPlayPath.Describe(null, new[] { "A" }));
        }
    }

    public sealed class UiPlayValueTextTests
    {
        [Test]
        public void ParsesUnityModificationText()
        {
            Assert.IsTrue(UiPlayValueText.TryBool("1", out var b) && b);
            Assert.IsTrue(UiPlayValueText.TryBool("false", out b) && !b);
            Assert.IsFalse(UiPlayValueText.TryBool("maybe", out _));
            Assert.IsTrue(UiPlayValueText.TryDouble("0.25", out var d) && d == 0.25);
            Assert.IsTrue(UiPlayValueText.TryDouble("-Infinity", out d) && double.IsNegativeInfinity(d));
            Assert.IsTrue(UiPlayValueText.TryLong("-3", out var l) && l == -3);
        }

        [Test]
        public void Same_ToleratesFloatFormatting()
        {
            Assert.IsTrue(UiPlayValueText.Same("0.1", "0.100000001"));
            Assert.IsTrue(UiPlayValueText.Same("abc", "abc"));
            Assert.IsFalse(UiPlayValueText.Same("0.1", "0.2"));
            Assert.IsFalse(UiPlayValueText.Same("abc", "abd"));
        }

        [Test]
        public void Short_TrimsLongAndMultilineText()
        {
            Assert.AreEqual("a⏎b", UiPlayValueText.Short("a\r\nb"));
            Assert.AreEqual(5, UiPlayValueText.Short("abcdefgh", 5).Length);
        }
    }
}
