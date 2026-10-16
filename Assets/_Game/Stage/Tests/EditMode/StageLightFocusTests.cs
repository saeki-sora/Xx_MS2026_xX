using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StageLightFocusTests
    {
        [Test]
        public void GlossShift_OneMeansMaterialAsIs()
        {
            Assert.AreEqual(0f, StageLightingSettings.GlossShift(1f), 1e-6f);
            Assert.AreEqual(-1f, StageLightingSettings.GlossShift(0f), 1e-6f);
            Assert.AreEqual(1f, StageLightingSettings.GlossShift(2f), 1e-6f);
        }

        [Test]
        public void GlossShift_IsClamped()
        {
            Assert.AreEqual(1f, StageLightingSettings.GlossShift(5f), 1e-6f);
            Assert.AreEqual(-1f, StageLightingSettings.GlossShift(-3f), 1e-6f);
        }

        [Test]
        public void ApplyShaderGlobals_OffMeansNoChange()
        {
            var settings = new StageLightingSettings { apply = false, highlightFocus = 0.2f };
            settings.ApplyShaderGlobals();
            Assert.AreEqual(0f, Shader.GetGlobalFloat(StageShaderIds.GlossShift), 1e-6f);

            settings.apply = true;
            settings.ApplyShaderGlobals();
            Assert.AreEqual(-0.8f, Shader.GetGlobalFloat(StageShaderIds.GlossShift), 1e-5f);

            Shader.SetGlobalFloat(StageShaderIds.GlossShift, 0f);
        }
    }
}
