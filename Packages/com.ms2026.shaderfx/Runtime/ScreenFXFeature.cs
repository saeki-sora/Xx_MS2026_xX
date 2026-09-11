using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MS2026.ShaderFX
{
    public sealed class ScreenFXFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/ShaderFX/Screen";

        private Material material;
        private Material maskMaterial;
        private ScreenFXPass pass;

        public override void Create()
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[ShaderFX] Shader '{ShaderName}' not found. ScreenFXFeature will be inactive.");
                return;
            }

            material = CoreUtils.CreateEngineMaterial(shader);
            maskMaterial = CoreUtils.CreateEngineMaterial(shader);
            pass = new ScreenFXPass(material, maskMaterial)
            {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents,
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material == null || pass == null) return;

            var settings = EffectDirector.HasInstance ? EffectDirector.Instance.ScreenSettings : null;
            if (settings == null || !settings.HasAnyEffect) return;

            pass.Settings = settings;
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(maskMaterial);
        }
    }
}
