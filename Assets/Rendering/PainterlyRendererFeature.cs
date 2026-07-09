using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

// Applies a fullscreen painterly post material through URP RenderGraph.
public class PainterlyRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Material material;
    [SerializeField] private RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingPostProcessing;

    private PainterlyPass pass;

    class PainterlyPass : ScriptableRenderPass
    {
        private readonly Material mat;

        public PainterlyPass(Material material)
        {
            mat = material;
            requiresIntermediateTexture = true;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (mat == null) return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            if (resourceData.isActiveTargetBackBuffer) return;

            TextureHandle source = resourceData.activeColorTexture;

            TextureDesc desc = renderGraph.GetTextureDesc(source);
            desc.name = "PainterlyTarget";
            desc.clearBuffer = false;
            desc.depthBufferBits = 0;
            TextureHandle destination = renderGraph.CreateTexture(desc);

            RenderGraphUtils.BlitMaterialParameters para =
                new RenderGraphUtils.BlitMaterialParameters(source, destination, mat, 0);
            renderGraph.AddBlitPass(para, "PainterlyPost");

            resourceData.cameraColor = destination;
        }
    }

    public override void Create()
    {
        pass = new PainterlyPass(material) { renderPassEvent = injectionPoint };
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (material == null) return;
        renderer.EnqueuePass(pass);
    }
}
