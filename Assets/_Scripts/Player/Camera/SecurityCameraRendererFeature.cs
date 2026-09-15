using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SecurityCameraRendererFeature : ScriptableRendererFeature
{
    public static SecurityCameraRendererFeature Instance { get; private set; }

    [SerializeField] Shader _shader;

    [Range(0f, 1f)]
    [SerializeField] float _intensity = 1f;

    Material _material;
    SecurityCameraRenderPass _renderPass;

    public override void Create()
    {
        Instance = this;

        SetActive(false);

        if (_shader == null)
            return;

        _material = CoreUtils.CreateEngineMaterial(_shader);
        _renderPass = new SecurityCameraRenderPass(_material);
    }

    public override void AddRenderPasses(
        ScriptableRenderer renderer,
        ref RenderingData renderingData)
    {
        if (_renderPass == null)
            return;

        if (renderingData.cameraData.cameraType != CameraType.Game)
            return;

        _renderPass.SetIntensity(_intensity);

        renderer.EnqueuePass(_renderPass);
    }

    protected override void Dispose(bool disposing)
    {
        if (Instance == this)
            Instance = null;

        CoreUtils.Destroy(_material);
    }
}