using MS2026.Stage.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class LightCopyTests
    {
        [Test]
        public void CopyLighting_CopiesEveryField_AndKeepsInstancesSeparate()
        {
            var from = new StageLightingSettings { apply = false, sunIntensity = 0.3f, sunDirection = 120f, fog = true, fogColor = Color.green };
            var to = new StageLightingSettings();

            LightCopyPanel.CopyLighting(from, to);

            Assert.AreEqual(JsonUtility.ToJson(from), JsonUtility.ToJson(to));
            Assert.AreNotSame(from, to);

            to.sunIntensity = 2f;
            Assert.AreEqual(0.3f, from.sunIntensity, "コピー後に片方を変えても、もう片方は変わらない");
        }
    }
}
