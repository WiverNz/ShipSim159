using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Visuals
{
    public sealed class RiverTemporalFeature : ScriptableRendererFeature
    {
        private static int lastRendererFrame = -100;
        public static int RenderedFrames { get; private set; }
        // True while the active renderer carries this feature, so temporal AA has water motion.
        public static bool IsActiveRenderer => Time.frameCount - lastRendererFrame <= 2;
        // Set by verification tools to receive a copy of each frame's motion vectors.
        public static RenderTexture MotionCaptureTarget { get; set; }
        private WaterMotionPass pass;
        public override void Create() => pass = new WaterMotionPass { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (data.cameraData.cameraType != CameraType.Game) return;
            lastRendererFrame = Time.frameCount;
            if (data.cameraData.antialiasing != AntialiasingMode.TemporalAntiAliasing) return;
            pass.ConfigureInput(ScriptableRenderPassInput.Motion);
            renderer.EnqueuePass(pass);
        }
        private sealed class WaterMotionPass : ScriptableRenderPass
        {
            private RenderTexture captureTexture;
            private RTHandle captureHandle;
            private sealed class Data { public RendererListHandle list; }
            private sealed class CaptureData { public TextureHandle source; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.motionVectorColor.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var lights = frameData.Get<UniversalLightData>();
                // No PerObjectData.MotionVectors: that request keeps only renderers whose transform
                // moved, and the water mesh never moves; its shader animates the surface itself.
                var draw = RenderingUtils.CreateDrawingSettings(new ShaderTagId("RiverMotionVectors"), rendering, camera, lights, SortingCriteria.CommonTransparent);
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

                RenderTexture capture = MotionCaptureTarget;
                if (capture == null) return;
                if (captureTexture != capture)
                {
                    captureTexture = capture;
                    captureHandle = RTHandles.Alloc(capture);
                }
                TextureHandle destination = graph.ImportTexture(captureHandle);
                using (var builder = graph.AddRasterRenderPass<CaptureData>("River motion vector capture", out var captureData))
                {
                    captureData.source = resources.motionVectorColor;
                    builder.UseTexture(resources.motionVectorColor, AccessFlags.Read);
                    builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                    builder.AllowPassCulling(false);
                    builder.SetRenderFunc(static (CaptureData d, RasterGraphContext ctx) =>
                        Blitter.BlitTexture(ctx.cmd, d.source, new Vector4(1, 1, 0, 0), 0, false));
                }
            }
        }
    }
}
