using MS2026.UI.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MS2026.UI.Tests.EditMode
{
    public sealed class ClearIntroRenderingTests
    {
        [TestCase("ClearFoamCover")]
        [TestCase("ClearBubbleGraphic")]
        [TestCase("GameOverRippleGraphic")]
        public void CustomGraphicsAlwaysHaveCanvasRenderer(string name)
        {
            var go = new GameObject("Graphic test", typeof(RectTransform));
            try
            {
                var type = typeof(MatchResultPresenter).Assembly.GetType("MS2026.UI.Game." + name, true);
                var graphic = go.AddComponent(type);
                Assert.That(graphic, Is.InstanceOf<MaskableGraphic>());
                Assert.That(go.GetComponent<CanvasRenderer>(), Is.Not.Null, name + " must be drawable by Unity UI");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ClearArtworkAndNoiseShaderAreIncluded()
        {
            var image = Resources.Load<Texture2D>("ClearIntro/clear");
            Assert.That(image, Is.Not.Null);
            Assert.That(image.width, Is.EqualTo(1920));
            Assert.That(image.height, Is.EqualTo(1080));
            Assert.That(Resources.Load<Shader>("ClearIntro/ClearLogoNoise"), Is.Not.Null);
        }

        [Test]
        public void CancelClearIntroHidesCanvasAndDropsSceneCallback()
        {
            var go = new GameObject("Cancel intro test", typeof(RectTransform), typeof(Canvas));
            try
            {
                var type = typeof(MatchResultPresenter).Assembly.GetType("MS2026.UI.Game.ClearMatchIntro", true);
                var intro = (MonoBehaviour)go.AddComponent(type);
                var callback = type.GetField("_onFinished", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var invoked = false;
                callback.SetValue(intro, new System.Action(() => invoked = true));
                type.GetMethod("Cancel").Invoke(intro, null);
                Assert.IsFalse(go.GetComponent<Canvas>().enabled);
                Assert.IsFalse(intro.enabled);
                Assert.IsNull(callback.GetValue(intro), "停止後にリザルトへ進むコールバックを残さない");
                Assert.IsFalse(invoked);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
