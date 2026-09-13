using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Visuals
{
    public sealed class RiverTemporalFeature : ScriptableRendererFeature
    {
        public static int RenderedFrames { get; private set; }
        private WaterMotionPass pass;
        public override void Create() => pass = new WaterMotionPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (data.cameraData.cameraType != CameraType.Game || data.cameraData.antialiasing != AntialiasingMode.TemporalAntiAliasing) return;
            pass.ConfigureInput(ScriptableRenderPassInput.Motion);
            renderer.EnqueuePass(pass);
        }
        private sealed class WaterMotionPass : ScriptableRenderPass
        {
            private sealed class Data { public RendererListHandle list; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.motionVectorColor.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();
                var draw = RenderingUtils.CreateDrawingSettings(new ShaderTagId("RiverMotionVectors"), rendering, camera, lights, SortingCriteria.CommonTransparent);
                draw.perObjectData |= PerObjectData.MotionVectors;
                var filter = new FilteringSettings(RenderQueueRange.transparent, 1 << 4);
                var list = graph.CreateRendererList(new RendererListParams(rendering.cullResults, draw, filter));
                using (var builder = graph.AddRasterRenderPass<Data>("River water motion vectors", out var data))
                {
                    data.list = list;
                    builder.UseRendererList(list);
                    builder.SetRenderAttachment(resources.motionVectorColor, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.SetRenderFunc(static (Data d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.DrawRendererList(d.list);
                        RenderedFrames++;
                    });
                }
            }
        }
    }
}
