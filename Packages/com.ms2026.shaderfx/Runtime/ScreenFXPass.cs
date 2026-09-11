using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MS2026.ShaderFX
{
    internal sealed class ScreenFXPass : ScriptableRenderPass
    {
        private const int PassGrayscale = 0;
        private const int PassPosterize = 1;
        private const int PassPixelate = 2;
        private const int PassOutline = 3;
        private const int PassMask = 4;
        private const int PassCopy = 5;
        private const int PassShockwave = 6;
        private const int PassScreenFlash = 7;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int LevelsId = Shader.PropertyToID("_Levels");
        private static readonly int BlockCountId = Shader.PropertyToID("_BlockCount");
        private static readonly int UseMaskId = Shader.PropertyToID("_UseMask");
        private static readonly int MaskTextureId = Shader.PropertyToID("_MaskTexture");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
        private static readonly int DepthThresholdId = Shader.PropertyToID("_DepthThreshold");
        private static readonly int NormalThresholdId = Shader.PropertyToID("_NormalThreshold");
        private static readonly int ShockwaveCenterId = Shader.PropertyToID("_ShockwaveCenter");
        private static readonly int ShockwaveProgressId = Shader.PropertyToID("_ShockwaveProgress");
        private static readonly int ShockwaveStrengthId = Shader.PropertyToID("_ShockwaveStrength");
        private static readonly int ShockwaveWidthId = Shader.PropertyToID("_ShockwaveWidth");
        private static readonly int ScreenFlashColorId = Shader.PropertyToID("_ScreenFlashColor");
        private static readonly int ScreenFlashAmountId = Shader.PropertyToID("_ScreenFlashAmount");

        private readonly Material material;
        private readonly Material maskMaterial;

        public ScreenFXSettings Settings;

        public ScreenFXPass(Material material, Material maskMaterial)
        {
            this.material = material;
            this.maskMaterial = maskMaterial;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
        }

        private class BlitPassData
        {
            public TextureHandle source;
            public TextureHandle extraTexture;
            public Material material;
            public int passIndex;
        }

        private class MaskPassData
        {
            public RendererListHandle rendererList;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (Settings == null || !Settings.HasAnyEffect) return;

            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            var cameraData = frameData.Get<UniversalCameraData>();
            var source = resourceData.activeColorTexture;

            var descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            // Quality-driven downsampling (design doc §5「品質スケーリング」): every intermediate
            // blit in this chain runs at `workingDescriptor`'s (possibly reduced) resolution, then
            // a final "Copy" blit upscales back to the camera's actual resolution. Sampling depth/
            // normals (Outline) and the group mask (Pixelate) still works at a mismatched
            // resolution since every lookup uses normalized UVs, same as any half-res post effect.
            float scale = Mathf.Clamp(ShaderFXQuality.ScreenEffectResolutionScale, 0.1f, 1f);
            bool downsampled = scale < 0.999f;
            var workingDescriptor = descriptor;
            if (downsampled)
            {
                workingDescriptor.width = Mathf.Max(1, Mathf.RoundToInt(descriptor.width * scale));
                workingDescriptor.height = Mathf.Max(1, Mathf.RoundToInt(descriptor.height * scale));
            }

            TextureHandle maskHandle = TextureHandle.nullHandle;
            if (Settings.pixelateEnabled && Settings.pixelateRenderingLayerMask != 0)
            {
                maskHandle = RecordMaskPass(renderGraph, frameData, workingDescriptor);
            }

            if (Settings.grayscaleEnabled)
            {
                source = BlitPass(renderGraph, "ShaderFX Grayscale", source, workingDescriptor, PassGrayscale, () =>
                {
                    material.SetFloat(IntensityId, Settings.grayscaleIntensity);
                });
            }

            if (Settings.posterizeEnabled)
            {
                source = BlitPass(renderGraph, "ShaderFX Posterize", source, workingDescriptor, PassPosterize, () =>
                {
                    material.SetFloat(LevelsId, Settings.posterizeLevels);
                });
            }

            if (Settings.pixelateEnabled)
            {
                bool useMask = maskHandle.IsValid();
                source = BlitPass(renderGraph, "ShaderFX Pixelate", source, workingDescriptor, PassPixelate, () =>
                {
                    float blockSize = Mathf.Max(Settings.pixelateBlockSize, 1f);
                    float w = Mathf.Max(workingDescriptor.width / blockSize, 1f);
                    float h = Mathf.Max(workingDescriptor.height / blockSize, 1f);
                    material.SetVector(BlockCountId, new Vector4(w, h, 0f, 0f));
                    material.SetFloat(UseMaskId, useMask ? 1f : 0f);
                }, maskHandle);
            }

            if (Settings.outlineEnabled)
            {
                source = BlitPass(renderGraph, "ShaderFX Outline", source, workingDescriptor, PassOutline, () =>
                {
                    material.SetColor(OutlineColorId, Settings.outlineColor);
                    material.SetFloat(ThicknessId, Settings.outlineThickness);
                    material.SetFloat(DepthThresholdId, Settings.outlineDepthThreshold);
                    material.SetFloat(NormalThresholdId, Settings.outlineNormalThreshold);
                }, default, resourceData.cameraDepthTexture, resourceData.cameraNormalsTexture);
            }

            if (Settings.shockwaveEnabled)
            {
                source = BlitPass(renderGraph, "ShaderFX Shockwave", source, workingDescriptor, PassShockwave, () =>
                {
                    material.SetVector(ShockwaveCenterId, Settings.shockwaveCenter);
                    material.SetFloat(ShockwaveProgressId, Settings.shockwaveProgress);
                    material.SetFloat(ShockwaveStrengthId, Settings.shockwaveStrength);
                    material.SetFloat(ShockwaveWidthId, Settings.shockwaveWidth);
                });
            }

            if (downsampled)
            {
                source = BlitPass(renderGraph, "ShaderFX Screen FX Upscale", source, descriptor, PassCopy, null);
            }

            // ScreenFlash runs after the upscale (always at full camera resolution) since it's a
            // flat color overlay with no spatial detail to lose from downsampling — no reason to
            // pay for it twice by running it before AND relying on the upscale not softening it.
            if (Settings.screenFlashEnabled)
            {
                source = BlitPass(renderGraph, "ShaderFX Screen Flash", source, descriptor, PassScreenFlash, () =>
                {
                    material.SetColor(ScreenFlashColorId, Settings.screenFlashColor);
                    material.SetFloat(ScreenFlashAmountId, Settings.screenFlashAmount);
                });
            }

            resourceData.cameraColor = source;
        }

        private TextureHandle BlitPass(RenderGraph renderGraph, string passName, TextureHandle source,
            RenderTextureDescriptor baseDescriptor, int passIndex, System.Action configure,
            TextureHandle extraTexture = default, params TextureHandle[] readOnlyDependencies)
        {
            configure?.Invoke();

            var textureDesc = new TextureDesc(baseDescriptor) { name = passName, clearBuffer = false };
            var destination = renderGraph.CreateTexture(textureDesc);

            using (var builder = renderGraph.AddRasterRenderPass<BlitPassData>(passName, out var passData))
            {
                passData.source = source;
                passData.extraTexture = extraTexture;
                passData.material = material;
                passData.passIndex = passIndex;

                builder.UseTexture(source, AccessFlags.Read);
                if (extraTexture.IsValid())
                {
                    builder.UseTexture(extraTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true); // needed for SetGlobalTexture(_MaskTexture) below
                }
                if (readOnlyDependencies != null)
                {
                    // Not touched directly (already bound globally by URP's own depth/normals prepasses);
                    // declaring the read here just stops Render Graph from culling those prepasses as unused.
                    foreach (var dependency in readOnlyDependencies)
                    {
                        if (dependency.IsValid()) builder.UseTexture(dependency, AccessFlags.Read);
                    }
                }
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);

                builder.SetRenderFunc((BlitPassData data, RasterGraphContext ctx) =>
                {
                    if (data.extraTexture.IsValid())
                    {
                        ctx.cmd.SetGlobalTexture(MaskTextureId, data.extraTexture);
                    }
                    Blitter.BlitTexture(ctx.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), data.material, data.passIndex);
                });
            }

            return destination;
        }

        private TextureHandle RecordMaskPass(RenderGraph renderGraph, ContextContainer frameData, RenderTextureDescriptor baseDescriptor)
        {
            var renderingData = frameData.Get<UniversalRenderingData>();
            var cameraData = frameData.Get<UniversalCameraData>();

            var maskDescriptor = baseDescriptor;
            maskDescriptor.colorFormat = RenderTextureFormat.R8;
            maskDescriptor.depthBufferBits = 0;
            maskDescriptor.msaaSamples = 1;

            var maskTextureDesc = new TextureDesc(maskDescriptor)
            {
                name = "ShaderFX Group Mask",
                clearBuffer = true,
                clearColor = Color.clear,
            };
            var maskHandle = renderGraph.CreateTexture(maskTextureDesc);

            var sortingSettings = new SortingSettings(cameraData.camera) { criteria = SortingCriteria.CommonOpaque };
            var drawingSettings = new DrawingSettings(new ShaderTagId("UniversalForward"), sortingSettings)
            {
                overrideMaterial = maskMaterial,
                overrideMaterialPassIndex = PassMask,
            };
            var filteringSettings = new FilteringSettings(RenderQueueRange.opaque, -1, Settings.pixelateRenderingLayerMask);

            var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
            var rendererListHandle = renderGraph.CreateRendererList(rendererListParams);

            using (var builder = renderGraph.AddRasterRenderPass<MaskPassData>("ShaderFX Group Mask", out var passData))
            {
                passData.rendererList = rendererListHandle;
                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(maskHandle, 0, AccessFlags.Write);

                builder.SetRenderFunc((MaskPassData data, RasterGraphContext ctx) =>
                {
                    ctx.cmd.DrawRendererList(data.rendererList);
                });
            }

            return maskHandle;
        }
    }
}
