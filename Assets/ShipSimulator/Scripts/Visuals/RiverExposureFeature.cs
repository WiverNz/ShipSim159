using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Visuals
{
    public sealed class RiverExposureFeature : ScriptableRendererFeature
    {
        public ComputeShader meterShader;
        private MeterPass pass;
        public override void Create() => pass = new MeterPass { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!Application.isPlaying || meterShader == null || !SystemInfo.supportsAsyncGPUReadback ||
                !SystemInfo.supportsComputeShaders || renderingData.cameraData.camera != Camera.main || RiverLighting.Active == null) return;
            pass.shader = meterShader;
            renderer.EnqueuePass(pass);
        }
        private sealed class MeterPass : ScriptableRenderPass
        {
            public ComputeShader shader;
            private float nextSample;
            private bool pending;
            private sealed class Data
            {
                public ComputeShader shader;
                public TextureHandle source;
                public BufferHandle output;
                public Vector4 size;
                public RiverLighting owner;
                public MeterPass pass;
            }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                if (pending || Time.unscaledTime < nextSample) return;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var camera = frameData.Get<UniversalCameraData>();
                var buffer = graph.CreateBuffer(new BufferDesc(1024, sizeof(float)) { name = "River luminance samples" });
                using (var builder = graph.AddComputePass<Data>("River exposure meter", out var data))
                {
                    data.shader = shader;
                    data.source = resources.activeColorTexture;
                    data.output = buffer;
                    data.size = new Vector4(camera.cameraTargetDescriptor.width, camera.cameraTargetDescriptor.height, 0, 0);
                    builder.UseTexture(data.source, AccessFlags.Read);
                    builder.UseBuffer(buffer, AccessFlags.Write);
                    builder.SetRenderFunc(static (Data d, ComputeGraphContext ctx) =>
                    {
                        ctx.cmd.SetComputeTextureParam(d.shader, 0, "_Source", d.source);
                        ctx.cmd.SetComputeBufferParam(d.shader, 0, "_Luminance", d.output);
                        ctx.cmd.SetComputeVectorParam(d.shader, "_SourceSize", d.size);
                        ctx.cmd.DispatchCompute(d.shader, 0, 4, 4, 1);
                    });
                }
                using (var builder = graph.AddUnsafePass<Data>("River exposure readback", out var data))
                {
                    data.output = buffer;
                    data.owner = RiverLighting.Active;
                    data.pass = this;
                    builder.UseBuffer(buffer, AccessFlags.Read);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (Data d, UnsafeGraphContext ctx) =>
                    {
                        RiverLighting owner = d.owner;
                        MeterPass pass = d.pass;
                        ctx.cmd.RequestAsyncReadback(d.output, request =>
                        {
                            pass.pending = false;
                            if (!request.hasError && owner != null && owner.isActiveAndEnabled) owner.SubmitMeter(request.GetData<float>().ToArray());
                        });
                    });
                }
                pending = true;
                nextSample = Time.unscaledTime + 0.25f;
            }
        }
    }
}
