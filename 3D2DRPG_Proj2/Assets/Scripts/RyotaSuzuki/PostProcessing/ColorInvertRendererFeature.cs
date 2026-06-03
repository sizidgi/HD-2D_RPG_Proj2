using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// ColorInvertVolume を適用する URP Renderer Feature。
/// URP 14 の FullScreenPass と同様にカラーバッファをコピーしてから描画する。
/// </summary>
public sealed class ColorInvertRendererFeature : ScriptableRendererFeature
{
    [SerializeField] private Shader shader;

    private Material _material;
    private ColorInvertPass _pass;

    public override void Create()
    {
        _pass = new ColorInvertPass("ColorInvert");

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

        var volume = VolumeManager.instance.stack.GetComponent<ColorInvertVolume>();
        if (volume == null || !volume.IsActive())
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
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly MaterialPropertyBlock s_PropertyBlock = new MaterialPropertyBlock();

        private Material _material;
        private ScriptableRenderer _renderer;
        private RTHandle _copiedColor;

        public ColorInvertPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);
        }

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

            var volume = VolumeManager.instance.stack.GetComponent<ColorInvertVolume>();
            if (volume == null || !volume.IsActive())
            {
                return;
            }

            ref var cameraData = ref renderingData.cameraData;
            var cmd = CommandBufferPool.Get("ColorInvert");

            using (new ProfilingScope(cmd, profilingSampler))
            {
                _material.SetFloat(IntensityId, volume.intensity.value);

                var source = cameraData.renderer.cameraColorTargetHandle;

                CoreUtils.SetRenderTarget(cmd, _copiedColor);
                Blitter.BlitTexture(cmd, source, new Vector4(1f, 1f, 0f, 0f), 0f, false);

                CoreUtils.SetRenderTarget(cmd, source);

                s_PropertyBlock.Clear();
                s_PropertyBlock.SetTexture(BlitTextureId, _copiedColor);
                s_PropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                cmd.DrawProcedural(Matrix4x4.identity, _material, 0, MeshTopology.Triangles, 3, 1, s_PropertyBlock);
            }

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
