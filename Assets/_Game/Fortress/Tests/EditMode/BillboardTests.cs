using System.Collections.Generic;
using MS2026.Fortress.Billboards;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Fortress.Tests.EditMode
{
    public sealed class BillboardTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _created)
            {
                if (obj != null)
                {
                    Object.DestroyImmediate(obj);
                }
            }

            _created.Clear();
        }

        private SpriteRenderer CreateRenderer(Material original)
        {
            var go = new GameObject("BillboardTestSprite");
            _created.Add(go);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sharedMaterial = original;
            return renderer;
        }

        private Material CreateMaterial(string name)
        {
            var material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Hidden/InternalErrorShader")) { name = name };
            _created.Add(material);
            return material;
        }

        [Test]
        public void 差し替えた絵は全て元のマテリアルに戻せる()
        {
            var original = CreateMaterial("Original");
            var billboard = CreateMaterial("Billboard");
            var a = CreateRenderer(original);
            var b = CreateRenderer(original);
            var swapper = new BillboardMaterialSwapper();

            swapper.Sync(new List<SpriteRenderer> { a, b }, billboard);
            Assert.AreEqual(billboard, a.sharedMaterial);
            Assert.AreEqual(2, swapper.Count);

            swapper.RestoreAll();
            Assert.AreEqual(original, a.sharedMaterial);
            Assert.AreEqual(original, b.sharedMaterial);
            Assert.AreEqual(0, swapper.Count);
        }

        [Test]
        public void 対象から外れた絵はすぐ元に戻る()
        {
            var original = CreateMaterial("Original");
            var billboard = CreateMaterial("Billboard");
            var a = CreateRenderer(original);
            var b = CreateRenderer(original);
            var swapper = new BillboardMaterialSwapper();

            swapper.Sync(new List<SpriteRenderer> { a, b }, billboard);
            swapper.Sync(new List<SpriteRenderer> { b }, billboard);

            Assert.AreEqual(original, a.sharedMaterial);
            Assert.AreEqual(billboard, b.sharedMaterial);
        }

        [Test]
        public void 他の仕組みが付け替えた絵からは手を引き取り合わない()
        {
            var original = CreateMaterial("Original");
            var billboard = CreateMaterial("Billboard");
            var effect = CreateMaterial("Effect");
            var a = CreateRenderer(original);
            var swapper = new BillboardMaterialSwapper();

            swapper.Sync(new List<SpriteRenderer> { a }, billboard);
            a.sharedMaterial = effect;
            swapper.Refresh(billboard);
            swapper.Sync(new List<SpriteRenderer> { a }, billboard);

            Assert.AreEqual(effect, a.sharedMaterial);
            Assert.AreEqual(0, swapper.Count);

            swapper.RestoreAll();
            Assert.AreEqual(effect, a.sharedMaterial);
        }

        [Test]
        public void OFFの設定はどの対象も含まない()
        {
            var settings = new BillboardSettings { enabled = false, targets = BillboardTargets.All };
            Assert.IsFalse(settings.Includes(BillboardTargets.Core));

            settings.enabled = true;
            settings.targets = BillboardTargets.Core | BillboardTargets.Enemies;
            Assert.IsTrue(settings.Includes(BillboardTargets.Core));
            Assert.IsFalse(settings.Includes(BillboardTargets.TurretBody));
        }

        [Test]
        public void OFFの設定を適用するとシェーダーの値も平らに戻る()
        {
            var controller = new BillboardController(new IBillboardTargetSource[0]);
            try
            {
                controller.Apply(new BillboardSettings { enabled = true, standAmount = 0.7f }, true);
                controller.Apply(new BillboardSettings { enabled = false }, true);

                Assert.IsFalse(controller.IsActive);
                Assert.AreEqual(0f, Shader.GetGlobalFloat(BillboardShaderGlobals.StandId));
                Assert.AreEqual(0f, Shader.GetGlobalFloat(BillboardShaderGlobals.SwarmStandId));
            }
            finally
            {
                controller.Dispose();
            }
        }
    }
}
