using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace ShipSimulator.Visuals
{
    [ExecuteAlways]
    public sealed class RiverPlanarReflection : MonoBehaviour
    {
        [SerializeField, Range(256, 1024)] private int resolution = 768;
        private Camera reflectionCamera;
        private RenderTexture reflection;
        private static bool rendering;
        private static readonly int TextureId = Shader.PropertyToID("_RiverPlanarReflection");
        private static readonly int MatrixId = Shader.PropertyToID("_RiverReflectionVP");
        private static readonly int EnabledId = Shader.PropertyToID("_RiverReflectionAvailable");

        private void OnEnable() => RenderPipelineManager.beginCameraRendering += Render;

        private void Render(ScriptableRenderContext context, Camera source)
        {
            if (rendering || source == null || source.cameraType == CameraType.Reflection || source.cameraType == CameraType.Preview) return;
            if (source.transform.position.y < transform.position.y + 0.1f)
            {
                Shader.SetGlobalFloat(EnabledId, 0);
                return;
            }
            EnsureResources();
            reflectionCamera.CopyFrom(source);
            reflectionCamera.enabled = false;
            reflectionCamera.cameraType = CameraType.Reflection;
            reflectionCamera.cullingMask = source.cullingMask & ~(1 << 4) & ~(1 << 5);
            reflectionCamera.targetTexture = reflection;
            reflectionCamera.allowMSAA = false;
            reflectionCamera.farClipPlane = Mathf.Min(source.farClipPlane, 1400);
            var data = reflectionCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            data.requiresColorOption = CameraOverrideOption.Off;
            data.requiresDepthOption = CameraOverrideOption.Off;
            float height = transform.position.y;
            Matrix4x4 mirror = Matrix4x4.identity;
            mirror.m11 = -1;
            mirror.m13 = 2 * height;
            Vector3 position = source.transform.position;
            reflectionCamera.transform.position = new Vector3(position.x, 2 * height - position.y, position.z);
            reflectionCamera.transform.rotation = Quaternion.LookRotation(Vector3.Reflect(source.transform.forward, Vector3.up), Vector3.Reflect(source.transform.up, Vector3.up));
            reflectionCamera.worldToCameraMatrix = source.worldToCameraMatrix * mirror;
            Matrix4x4 view = reflectionCamera.worldToCameraMatrix;
            Vector3 point = view.MultiplyPoint(new Vector3(0, height + 0.08f, 0));
            Vector3 normal = view.MultiplyVector(Vector3.up).normalized;
            Vector4 clipPlane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(point, normal));
            reflectionCamera.projectionMatrix = source.CalculateObliqueMatrix(clipPlane);
            bool inverted = GL.invertCulling;
            try
            {
                rendering = true;
                GL.invertCulling = !inverted;
                RenderPipeline.SubmitRenderRequest(reflectionCamera,
                    new UniversalRenderPipeline.SingleCameraRequest { destination = reflection });
                Shader.SetGlobalTexture(TextureId, reflection);
                Shader.SetGlobalMatrix(MatrixId, GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true) * view);
                Shader.SetGlobalFloat(EnabledId, 1);
            }
            finally
            {
                GL.invertCulling = inverted;
                rendering = false;
            }
        }

        private void EnsureResources()
        {
            if (reflectionCamera == null)
            {
                var cameraObject = new GameObject("River reflection camera") { hideFlags = HideFlags.HideAndDontSave };
                reflectionCamera = cameraObject.AddComponent<Camera>();
                reflectionCamera.enabled = false;
            }
            if (reflection != null && reflection.width == resolution) return;
            if (reflection != null) Release(reflection);
            reflection = new RenderTexture(resolution, resolution, 16, RenderTextureFormat.ARGBHalf)
            {
                name = "River planar reflection", hideFlags = HideFlags.HideAndDontSave,
                useMipMap = true, autoGenerateMips = true, filterMode = FilterMode.Trilinear
            };
            reflection.Create();
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Render;
            Shader.SetGlobalFloat(EnabledId, 0);
            if (reflectionCamera != null) Release(reflectionCamera.gameObject);
            if (reflection != null) Release(reflection);
            reflectionCamera = null;
            reflection = null;
        }

        private static void Release(Object resource)
        {
            if (Application.isPlaying) Destroy(resource);
            else DestroyImmediate(resource);
        }
    }
}
