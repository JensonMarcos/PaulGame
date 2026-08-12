Shader "Hidden/PaulGame/SphericalProjection"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Spherical Projection"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // xy: horizontal/vertical half-FOV in radians
            // z: uniform crop-to-fit scale
            float4 _SphericalProjectionParams;

            // x: effect strength
            // yz: X/Y axis enabled
            float4 _SphericalProjectionControls;

            half4 SampleBicubic(float2 uv, float2 minUv, float2 maxUv)
            {
                float2 samplePosition = uv * _BlitTexture_TexelSize.zw;
                float2 centerPosition = floor(samplePosition - 0.5) + 0.5;
                float2 f = samplePosition - centerPosition;
                float2 f2 = f * f;
                float2 f3 = f2 * f;

                // Catmull-Rom weights, reduced from 16 point samples to five
                // bilinear samples using the central 2x2 texels as one tap.
                float2 w0 = -0.5 * f3 + f2 - 0.5 * f;
                float2 w1 = 1.5 * f3 - 2.5 * f2 + 1.0;
                float2 w2 = -1.5 * f3 + 2.0 * f2 + 0.5 * f;
                float2 w3 = 0.5 * f3 - 0.5 * f2;
                float2 w12 = w1 + w2;

                float2 uv0 = (centerPosition - 1.0) * _BlitTexture_TexelSize.xy;
                float2 uv3 = (centerPosition + 2.0) * _BlitTexture_TexelSize.xy;
                float2 uv12 =
                    (centerPosition + w2 / w12) * _BlitTexture_TexelSize.xy;

                half4 s0 = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    clamp(float2(uv12.x, uv0.y), minUv, maxUv));
                half4 s1 = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    clamp(float2(uv0.x, uv12.y), minUv, maxUv));
                half4 s2 = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    clamp(uv12, minUv, maxUv));
                half4 s3 = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    clamp(float2(uv3.x, uv12.y), minUv, maxUv));
                half4 s4 = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    clamp(float2(uv12.x, uv3.y), minUv, maxUv));

                float weight0 = w12.x * w0.y;
                float weight1 = w0.x * w12.y;
                float weight2 = w12.x * w12.y;
                float weight3 = w3.x * w12.y;
                float weight4 = w12.x * w3.y;
                float weightSum =
                    weight0 + weight1 + weight2 + weight3 + weight4;

                return (
                    s0 * weight0
                    + s1 * weight1
                    + s2 * weight2
                    + s3 * weight3
                    + s4 * weight4)
                    / weightSum;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenPosition = input.texcoord * 2.0 - 1.0;
                float2 angularPosition =
                    screenPosition * _SphericalProjectionParams.xy
                    * _SphericalProjectionParams.z;

                // Equidistant azimuthal projection:
                // radius on screen is proportional to the ray's angle from the
                // camera's optical axis. Reproject that spherical ray into the
                // perspective image Unity rendered.
                float theta = length(angularPosition);
                float safeTheta = min(theta, HALF_PI - 1e-4);
                float radialScale = theta > 1e-5 ? tan(safeTheta) / theta : 1.0;
                float2 sphericalPerspectivePosition =
                    angularPosition * radialScale
                    / tan(_SphericalProjectionParams.xy);
                float2 axisStrength =
                    _SphericalProjectionControls.x * _SphericalProjectionControls.yz;
                float2 perspectivePosition =
                    lerp(screenPosition, sphericalPerspectivePosition, axisStrength);

                float isInFront = 1.0 - step(HALF_PI - 1e-4, theta);
                float isInBounds =
                    step(abs(perspectivePosition.x), 1.0)
                    * step(abs(perspectivePosition.y), 1.0);
                float isValid = isInFront * isInBounds;

                float2 sourceUv = perspectivePosition * 0.5 + 0.5;
                float2 halfTexel = 0.5 * _BlitTexture_TexelSize.xy;
                float2 minSourceUv = halfTexel;
                float2 maxSourceUv = max(_RTHandleScale.xy - halfTexel, halfTexel);
                sourceUv = saturate(sourceUv) * _RTHandleScale.xy;
                half4 color =
                    SampleBicubic(sourceUv, minSourceUv, maxSourceUv);

                return lerp(half4(0.0, 0.0, 0.0, 1.0), color, isValid);
            }
            ENDHLSL
        }
    }
}
