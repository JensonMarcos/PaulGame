using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PaulGame.Rendering
{
    public enum PaniniProjectionFilter
    {
        Bilinear,
        Bicubic,
        NearestNeighbor
    }

    [Serializable]
    public sealed class PaniniProjectionFilterParameter :
        VolumeParameter<PaniniProjectionFilter>
    {
        public PaniniProjectionFilterParameter(
            PaniniProjectionFilter value,
            bool overrideState = false)
            : base(value, overrideState)
        {
        }
    }

    [Serializable]
    [VolumeComponentMenu("Post-processing/Filtered Panini Projection")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class FilteredPaniniProjection :
        VolumeComponent,
        IPostProcessComponent
    {
        [Tooltip("Controls the strength of the Panini projection.")]
        public ClampedFloatParameter distance =
            new ClampedFloatParameter(0f, 0f, 1f);

        [Tooltip("Crops the distortion to the edge of the screen.")]
        public ClampedFloatParameter cropToFit =
            new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Selects the texture filter used while reprojecting the image.")]
        public PaniniProjectionFilterParameter filtering =
            new PaniniProjectionFilterParameter(
                PaniniProjectionFilter.Bilinear);

        public bool IsActive() => active && distance.value > 0f;

        [Obsolete("Unused. #from(2023.1)")]
        public bool IsTileCompatible() => false;
    }
}
