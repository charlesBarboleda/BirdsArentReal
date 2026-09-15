using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class SecurityCameraRenderPass : ScriptableRenderPass
{
    const string PassName = "Security Camera Effect";

    static readonly int IntensityID =
        Shader.PropertyToID("_Intensity");

    readonly Material _material;

    float _intensity;

    public SecurityCameraRenderPass(Material material)
    {
        _material = material;

        renderPassEvent =
            RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public void SetIntensity(float intensity)
    {
        _intensity = intensity;
    }

    public override void RecordRenderGraph(
     RenderGraph renderGraph,
     ContextContainer frameData)
    {
        UniversalResourceData resourceData =
            frameData.Get<UniversalResourceData>();

        if (resourceData.isActiveTargetBackBuffer)
            return;

        TextureHandle source =
            resourceData.activeColorTexture;

        TextureDesc destinationDesc =
            renderGraph.GetTextureDesc(source);

        destinationDesc.name =
            "SecurityCameraEffect";

        destinationDesc.clearBuffer = false;
        destinationDesc.depthBufferBits = 0;

        TextureHandle destination =
            renderGraph.CreateTexture(destinationDesc);

        RenderGraphUtils.BlitMaterialParameters parameters =
            new(
                source,
                destination,
                _material,
                0);

        parameters.material.SetFloat(
            IntensityID,
            _intensity);

        renderGraph.AddBlitPass(
            parameters,
            PassName);

        resourceData.cameraColor = destination;
    }
}