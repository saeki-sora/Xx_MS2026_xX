using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StageLightingPresetsTests
    {
        [Test]
        public void Presets_HaveUniqueLabels_AndTurnLightOn()
        {
            var labels = new HashSet<string>();
            foreach (var preset in StageLightingPresets.All)
            {
                Assert.IsTrue(labels.Add(preset.Label), preset.Label);
                var settings = new StageLightingSettings { apply = false };
                preset.ApplyTo(settings);
                Assert.IsTrue(settings.apply, preset.Label);
                Assert.That(settings.sunElevation, Is.InRange(5f, 90f), preset.Label);
                Assert.That(settings.shadowStrength, Is.InRange(0f, 1f), preset.Label);
            }
        }

        [Test]
        public void Presets_KeepFog()
        {
            var settings = new StageLightingSettings { fog = true, fogStart = 3f, fogColor = Color.red };
            StageLightingPresets.Find("夜").ApplyTo(settings);
            Assert.IsTrue(settings.fog);
            Assert.AreEqual(3f, settings.fogStart);
            Assert.AreEqual(Color.red, settings.fogColor);
        }

        [Test]
        public void ResetToDefaults_RestoresEverything()
        {
            var settings = new StageLightingSettings { apply = false, sunIntensity = 0.14f, fog = true, sunDirection = 90f };
            StageLightingPresets.ResetToDefaults(settings);
            var defaults = new StageLightingSettings();
            Assert.AreEqual(JsonUtility.ToJson(defaults), JsonUtility.ToJson(settings));
        }
    }
}
