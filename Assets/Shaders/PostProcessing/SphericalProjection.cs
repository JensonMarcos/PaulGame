using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PaulGame.Rendering
{
    [Serializable]
    [VolumeComponentMenu("Post-processing/Spherical Projection")]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public sealed class SphericalProjection : VolumeComponent, IPostProcessComponent
    {
        [Tooltip("Enables an equidistant spherical projection. The curvature is derived from the camera field of view.")]
        public BoolParameter enabled = new BoolParameter(false);

        [Tooltip("Blends between the original perspective image at 0 and the accurate spherical projection at 1.")]
        public ClampedFloatParameter strength = new ClampedFloatParameter(1f, 0f, 1f);

        [Tooltip("Applies the spherical projection along the X axis.")]
        public BoolParameter xAxis = new BoolParameter(true);

        [Tooltip("Applies the spherical projection along the Y axis.")]
        public BoolParameter yAxis = new BoolParameter(true);

        [Tooltip("Scales the spherical image to fill the rectangular frame. This changes framing, not the projection curve.")]
        public ClampedFloatParameter cropToFit = new ClampedFloatParameter(1f, 0f, 1f);

        public bool IsActive() =>
            active && enabled.value && strength.value > 0f && (xAxis.value || yAxis.value);

        [Obsolete("Unused. #from(2023.1)")]
        public bool IsTileCompatible() => false;
    }
}
