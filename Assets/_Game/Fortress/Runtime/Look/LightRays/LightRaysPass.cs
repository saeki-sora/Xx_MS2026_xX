using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace MS2026.Fortress
{
    /// <summary>
    /// 斜めの光の筋をかける描画の1工程（光のにじみ等の後処理の前。画面のふちの靄があれば、その後）。
    /// <see cref="LightRaysLook"/> がゲームのカメラにだけ毎フレーム差し込む。レンダラーの設定ファイルは触らない。
    /// </summary>
    internal sealed class LightRaysPass : ScriptableRenderPass
    {
        private static readonly int ColorId = Shader.PropertyToID("_RayColor");
        private static readonly int ShapeId = Shader.PropertyToID("_RayShape");
        private static readonly int MotionId = Shader.PropertyToID("_RayMotion");

        private readonly Material _material;

        public LightRaysPass(Material material)
        {
            _material = material;
            // 靄（BeforeRenderingPostProcessing）の後に重ねて、暗い靄の中にも光が差し込んで見えるようにする。
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 1;
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("Fortress Light Rays");
        }

        public void Setup(LightRaysSettings settings, Camera camera)
        {
            var c = settings.color;
            _material.SetVector(ColorId, new Vector4(c.r, c.g, c.b, settings.intensity));
            _material.SetVector(ShapeId, new Vector4(settings.angle * Mathf.Deg2Rad, settings.density, settings.sharpness, settings.reach));
            _material.SetVector(MotionId, new Vector4(settings.driftSpeed, settings.shimmer, camera.aspect, 0f));
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer)
            {
                return;
            }

            var source = resourceData.activeColorTexture;
            var description = renderGraph.GetTextureDesc(source);
            description.name = "Fortress Light Rays";
            description.clearBuffer = false;
            var destination = renderGraph.CreateTexture(description);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0), "Fortress Light Rays");
            resourceData.cameraColor = destination;
        }
    }
}
