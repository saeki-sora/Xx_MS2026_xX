using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace MS2026.Fortress
{
    /// <summary>
    /// 画面のふちの靄をかける描画の1工程（透明な物まで描いた後・光のにじみ等の後処理の前）。
    /// <see cref="EdgeMistLook"/> がゲームのカメラにだけ毎フレーム差し込む。レンダラーの設定ファイルは触らない。
    /// </summary>
    internal sealed class EdgeMistPass : ScriptableRenderPass
    {
        private static readonly int MistColorId = Shader.PropertyToID("_MistColor");
        private static readonly int ShadeTintId = Shader.PropertyToID("_ShadeTint");
        private static readonly int BandColorId = Shader.PropertyToID("_BandColor");
        private static readonly int KeepId = Shader.PropertyToID("_Keep");
        private static readonly int BeamGlowId = Shader.PropertyToID("_BeamGlow");
        private static readonly int InvViewProjId = Shader.PropertyToID("_EdgeMistInvViewProj");

        private readonly Material _material;

        public EdgeMistPass(Material material)
        {
            _material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
            profilingSampler = new ProfilingSampler("Fortress Edge Mist");
        }

        /// <summary>このカメラで描く前に、設定とカメラの向きをマテリアルに入れる。</summary>
        public void Setup(EdgeMistSettings settings, Camera camera)
        {
            _material.SetColor(MistColorId, settings.mistColor);
            var tint = settings.shadeTint;
            _material.SetVector(ShadeTintId, new Vector4(tint.r, tint.g, tint.b, settings.darkness));
            var band = settings.edgeBandColor;
            _material.SetVector(BandColorId, new Vector4(band.r, band.g, band.b, settings.edgeBandStrength));
            _material.SetVector(KeepId, new Vector4(settings.keepThreshold, settings.keepSoftness, settings.keepLights, 0f));
            _material.SetVector(BeamGlowId, new Vector4(settings.beamGlow, settings.beamGlowRange, settings.beamReveal, 0f));
            // 画面の位置 → 地面の位置を求めるため（シェーダー側は OpenGL と同じ -1〜1 の向きで計算する）。
            var viewProjection = camera.projectionMatrix * camera.worldToCameraMatrix;
            _material.SetMatrix(InvViewProjId, viewProjection.inverse);
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
            description.name = "Fortress Edge Mist";
            description.clearBuffer = false;
            var destination = renderGraph.CreateTexture(description);

            renderGraph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source, destination, _material, 0), "Fortress Edge Mist");
            resourceData.cameraColor = destination;
        }
    }
}
