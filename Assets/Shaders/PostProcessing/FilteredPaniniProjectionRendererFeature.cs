using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace PaulGame.Rendering
{
    public sealed class FilteredPaniniProjectionRendererFeature :
        ScriptableRendererFeature
    {
        private FilterSetupPass filterSetupPass;

        public override void Create()
        {
            filterSetupPass = new FilterSetupPass();
        }

        public override void AddRenderPasses(
            ScriptableRenderer renderer,
            ref RenderingData renderingData)
        {
            ref CameraData cameraData = ref renderingData.cameraData;
            if (!cameraData.postProcessEnabled || cameraData.isSceneViewCamera)
                return;

            VolumeStack stack = VolumeManager.instance.stack;
            FilteredPaniniProjection settings =
                stack.GetComponent<FilteredPaniniProjection>();
            UnityEngine.Rendering.Universal.PaniniProjection builtInSettings =
                stack.GetComponent<
                    UnityEngine.Rendering.Universal.PaniniProjection>();

            if (settings == null || builtInSettings == null)
                return;

            bool isActive = settings.IsActive();
            builtInSettings.distance.value =
                isActive ? settings.distance.value : 0f;

            if (!isActive)
                return;

            builtInSettings.cropToFit.value = settings.cropToFit.value;
            filterSetupPass.Setup(settings.filtering.value);
            renderer.EnqueuePass(filterSetupPass);
        }

        private sealed class FilterSetupPass : ScriptableRenderPass
        {
            private const string PassName = "Configure Panini Filtering";
            private static readonly GlobalKeyword BicubicKeyword =
                GlobalKeyword.Create("_PANINI_FILTER_BICUBIC");
            private static readonly GlobalKeyword NearestKeyword =
                GlobalKeyword.Create("_PANINI_FILTER_NEAREST");

            private PaniniProjectionFilter filtering;

            public FilterSetupPass()
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            }

            public void Setup(PaniniProjectionFilter filter)
            {
                filtering = filter;
            }

            public override void RecordRenderGraph(
                RenderGraph renderGraph,
                ContextContainer frameData)
            {
                using IRasterRenderGraphBuilder builder =
                    renderGraph.AddRasterRenderPass<PassData>(
                        PassName,
                        out PassData passData);

                passData.filtering = filtering;
                builder.AllowGlobalStateModification(true);
                builder.AllowPassCulling(false);
                builder.SetRenderFunc(static (
                    PassData data,
                    RasterGraphContext context) =>
                {
                    context.cmd.SetKeyword(
                        BicubicKeyword,
                        data.filtering == PaniniProjectionFilter.Bicubic);
                    context.cmd.SetKeyword(
                        NearestKeyword,
                        data.filtering
                            == PaniniProjectionFilter.NearestNeighbor);
                });
            }

            private sealed class PassData
            {
                public PaniniProjectionFilter filtering;
            }
        }
    }
}
