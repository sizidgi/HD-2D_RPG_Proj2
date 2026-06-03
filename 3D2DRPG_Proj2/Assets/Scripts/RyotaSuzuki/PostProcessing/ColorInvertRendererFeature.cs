using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ColorInvertVolume を適用する URP Renderer Feature。
/// </summary>
public sealed class ColorInvertRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    private Material _material;
    private ColorInvertPass _pass;

    public override void Create()
    {
        _pass = new ColorInvertPass();

        if (shader == null)
        {
            shader = Shader.Find("Hidden/RyotaSuzuki/ColorInvert");
        }

        if (shader != null)
        {
            _material = CoreUtils.CreateEngineMaterial(shader);
        }

        _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        _pass.ConfigureInput(ScriptableRenderPassInput.Color);
        _pass.SetMaterial(_material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_pass == null || _material == null)
        {
            return;
        }

        if (UniversalRenderer.IsOffscreenDepthTexture(in renderingData.cameraData)
            || renderingData.cameraData.cameraType == CameraType.Preview
            || renderingData.cameraData.cameraType == CameraType.Reflection)
        {
            return;
        }

        if (!ColorInvertVolume.ShouldApply())
        {
            return;
        }

        _pass.Setup(renderer);
        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
        CoreUtils.Destroy(_material);
    }

    private sealed class ColorInvertPass : ScriptableRenderPass
    {
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        private Material _material;
        private ScriptableRenderer _renderer;
        private RTHandle _copiedColor;

        public void SetMaterial(Material material)
        {
            _material = material;
        }

        public void Setup(ScriptableRenderer renderer)
        {
            _renderer = renderer;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            ResetTarget();

            if (_material == null)
            {
                return;
            }

            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.msaaSamples = 1;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _copiedColor, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_ColorInvertCopy");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null || _renderer == null || _copiedColor == null)
            {
                return;
            }

            if (!ColorInvertVolume.ShouldApply())
            {
                return;
            }

            var cmd = CommandBufferPool.Get("ColorInvert");
            var source = renderingData.cameraData.renderer.cameraColorTargetHandle;

            _material.SetFloat(IntensityId, ColorInvertVolume.GetEffectiveIntensity());
            Blitter.BlitCameraTexture(cmd, source, _copiedColor, _material, 0);
            Blitter.BlitCameraTexture(cmd, _copiedColor, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _copiedColor?.Release();
            _copiedColor = null;
        }
    }
}
