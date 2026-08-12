using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace PaulGame.Rendering
{
    public sealed class SphericalProjectionRendererFeature : ScriptableRendererFeature
    {
        private const string ShaderName = "Hidden/PaulGame/SphericalProjection";

        [SerializeField] private Shader shader;

        private Material material;
        private SphericalProjectionPass projectionPass;

        public override void Create()
        {
            CoreUtils.Destroy(material);

            if (shader == null)
                shader = Shader.Find(ShaderName);

            if (shader != null)
                material = CoreUtils.CreateEngineMaterial(shader);

            projectionPass = new SphericalProjectionPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            if (material == null || !cameraData.postProcessEnabled || cameraData.isSceneViewCamera)
                return;

            SphericalProjection settings =
                VolumeManager.instance.stack.GetComponent<SphericalProjection>();

            if (settings == null || !settings.IsActive())
                return;

            projectionPass.Setup(
                cameraData.camera.fieldOfView,
                cameraData.camera.aspect,
                settings.cropToFit.value,
                settings.strength.value,
                settings.xAxis.value,
                settings.yAxis.value);
            renderer.EnqueuePass(projectionPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material);
            material = null;
        }

        private sealed class SphericalProjectionPass : ScriptableRenderPass
        {
            private const string PassName = "Spherical Projection";
            private static readonly int ProjectionParamsId =
                Shader.PropertyToID("_SphericalProjectionParams");
            private static readonly int ProjectionControlsId =
                Shader.PropertyToID("_SphericalProjectionControls");

            private readonly Material material;
            private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
            private Vector4 projectionParams;
            private Vector4 projectionControls;

            public SphericalProjectionPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public void Setup(
                float verticalFieldOfView,
                float aspect,
                float cropToFit,
                float strength,
                bool xAxis,
                bool yAxis)
            {
                float halfVertical = verticalFieldOfView * Mathf.Deg2Rad * 0.5f;
                float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * aspect);
                float cornerAngle = Mathf.Sqrt(
                    halfHorizontal * halfHorizontal + halfVertical * halfVertical);

                // An equidistant projection maps angular distance from the optical axis
                // linearly to screen distance. Its projected rectangle does not have the
                // same outline as a perspective frustum, so calculate the exact uniform
                // scale that keeps the spherical rectangle inside the source image.
                float horizontalLimit =
                    Mathf.Tan(halfHorizontal) * cornerAngle / halfHorizontal;
                float verticalLimit =
                    Mathf.Tan(halfVertical) * cornerAngle / halfVertical;
                float fitAngle = Mathf.Atan(Mathf.Min(horizontalLimit, verticalLimit));
                float fitScale = fitAngle / cornerAngle;
                float projectionScale = Mathf.Lerp(1f, fitScale, cropToFit);

                projectionParams =
                    new Vector4(halfHorizontal, halfVertical, projectionScale, 0f);
                projectionControls =
                    new Vector4(strength, xAxis ? 1f : 0f, yAxis ? 1f : 0f, 0f);
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                if (resourceData.isActiveTargetBackBuffer)
                    return;

                TextureHandle source = resourceData.activeColorTexture;
                TextureDesc destinationDescriptor = renderGraph.GetTextureDesc(source);
                destinationDescriptor.name = "CameraColor-SphericalProjection";
                destinationDescriptor.clearBuffer = false;
                TextureHandle destination = renderGraph.CreateTexture(destinationDescriptor);

                properties.Clear();
                properties.SetVector(ProjectionParamsId, projectionParams);
                properties.SetVector(ProjectionControlsId, projectionControls);

                var parameters = new RenderGraphUtils.BlitMaterialParameters(
                    source,
                    destination,
                    material,
                    0,
                    properties,
                    RenderGraphUtils.FullScreenGeometryType.ProceduralTriangle);

                renderGraph.AddBlitPass(parameters, PassName);
                resourceData.cameraColor = destination;
            }
        }
    }
}
