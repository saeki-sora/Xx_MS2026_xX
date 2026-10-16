using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MS2026.Stage.Tests.EditMode
{
    public sealed class StageLookVolumeTests
    {
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void NeedsPostProcess_OnlyWhenToneOrExposureIsUsed()
        {
            Assert.IsFalse(StageLookVolume.NeedsPostProcess(new StageLightingSettings()));
            Assert.IsTrue(StageLookVolume.NeedsPostProcess(new StageLightingSettings { toneMapping = StageToneMapping.Natural }));
            Assert.IsTrue(StageLookVolume.NeedsPostProcess(new StageLightingSettings { exposure = -0.5f }));
        }

        [Test]
        public void Apply_CreatesUnsavedVolume_AndDisablesItWhenOff()
        {
            _go = new GameObject("StageRootTest");
            var root = _go.AddComponent<StageRoot>();
            var settings = new StageLightingSettings { apply = true, toneMapping = StageToneMapping.Cinematic, exposure = -0.5f };

            StageLookVolume.Apply(root, settings);

            var child = _go.transform.Find(StageLookVolume.ObjectName);
            Assert.IsNotNull(child);
            Assert.IsTrue((child.gameObject.hideFlags & HideFlags.DontSave) == HideFlags.DontSave);
            var volume = child.GetComponent<Volume>();
            Assert.IsTrue(volume.enabled);
            Assert.IsTrue(volume.sharedProfile.TryGet(out Tonemapping tonemapping));
            Assert.AreEqual(TonemappingMode.ACES, tonemapping.mode.value);
            Assert.IsTrue(volume.sharedProfile.TryGet(out ColorAdjustments color));
            Assert.AreEqual(-0.5f, color.postExposure.value, 1e-5f);

            settings.apply = false;
            StageLookVolume.Apply(root, settings);
            Assert.IsFalse(volume.enabled);

            Object.DestroyImmediate(volume.sharedProfile);
        }
    }
}
