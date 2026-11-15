using NUnit.Framework;
using UnityEngine;

namespace MS2026.CharacterSelect.Tests.EditMode
{
    /// <summary>空いている席に、選ばれなかったキャラを割り当てる決まり（CPU 用。2026-11-15）。</summary>
    public sealed class CharacterSelectionFillTests
    {
        private CharacterDefinition[] _pool;

        [SetUp]
        public void SetUp()
        {
            _pool = new CharacterDefinition[4];
            for (var i = 0; i < _pool.Length; i++)
            {
                _pool[i] = ScriptableObject.CreateInstance<CharacterDefinition>();
                _pool[i].id = "c" + i;
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var c in _pool)
            {
                Object.DestroyImmediate(c);
            }

            CharacterSelectionResult.Clear();
        }

        [Test]
        public void FillsEmptySeatsWithUnusedInRosterOrder()
        {
            var chosen = new[] { _pool[2], null, null, _pool[0] };
            var filled = CharacterSelectionResult.FillAbsent(chosen, _pool);
            Assert.AreSame(_pool[2], filled[0]);
            Assert.AreSame(_pool[1], filled[1]); // 0 は P4 が使っている → 1
            Assert.AreSame(_pool[3], filled[2]);
            Assert.AreSame(_pool[0], filled[3]);
            Assert.IsNull(chosen[1]); // 元の並びは変えない
        }

        [Test]
        public void NotEnoughCharacters_LeavesNull()
        {
            var filled = CharacterSelectionResult.FillAbsent(new CharacterDefinition[] { null, null, null }, new[] { _pool[0] });
            Assert.AreSame(_pool[0], filled[0]);
            Assert.IsNull(filled[1]);
            Assert.IsNull(filled[2]);
        }

        [Test]
        public void StoreAndClear_RaiseChanged()
        {
            var count = 0;
            void OnChanged() => count++;
            CharacterSelectionResult.Changed += OnChanged;
            try
            {
                CharacterSelectionResult.Store(new[] { _pool[0] });
                CharacterSelectionResult.Clear();
                Assert.AreEqual(2, count);
            }
            finally
            {
                CharacterSelectionResult.Changed -= OnChanged;
            }
        }
    }
}
