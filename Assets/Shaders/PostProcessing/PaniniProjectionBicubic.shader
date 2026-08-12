Shader "Hidden/PaulGame/PaniniProjectionFiltered"
{
    HLSLINCLUDE
        #pragma multi_compile_local _GENERIC _UNIT_DISTANCE
        #pragma multi_compile _ _PANINI_FILTER_BICUBIC _PANINI_FILTER_NEAREST

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/DynamicScalingClamping.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float4 _Params;

        float2 Panini_UnitDistance(float2 viewPosition)
        {
            const float distance = 1.0;
            const float viewDistance = 2.0;
            const float viewDistanceSquared = 4.0;

            float viewHypotenuse =
                sqrt(viewPosition.x * viewPosition.x + viewDistanceSquared);
            float cylinderHypotenuse =
                viewHypotenuse
                - (viewPosition.x * viewPosition.x) / viewHypotenuse;
            float cylinderHypotenuseFraction =
                cylinderHypotenuse / viewHypotenuse;
            float cylinderDistance =
                viewDistance * cylinderHypotenuseFraction;
            float2 cylinderPosition =
                viewPosition * cylinderHypotenuseFraction;

            return cylinderPosition / (cylinderDistance - distance);
        }

        float2 Panini_Generic(float2 viewPosition, float distance)
        {
            float viewDistance = 1.0 + distance;
            float viewHypotenuseSquared =
                viewPosition.x * viewPosition.x
                + viewDistance * viewDistance;
            float intersectionD = viewPosition.x * distance;
            float intersectionDiscriminant =
                viewHypotenuseSquared - intersectionD * intersectionD;
            float cylinderDistanceMinusD =
                (-intersectionD * viewPosition.x
                    + viewDistance * sqrt(intersectionDiscriminant))
                / viewHypotenuseSquared;
            float cylinderDistance = cylinderDistanceMinusD + distance;
            float2 cylinderPosition =
                viewPosition * (cylinderDistance / viewDistance);

            return cylinderPosition / (cylinderDistance - distance);
        }

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

            float2 uv0 =
                (centerPosition - 1.0) * _BlitTexture_TexelSize.xy;
            float2 uv3 =
                (centerPosition + 2.0) * _BlitTexture_TexelSize.xy;
            float2 uv12 =
                (centerPosition + w2 / w12) * _BlitTexture_TexelSize.xy;

            half4 sample0 = SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                clamp(float2(uv12.x, uv0.y), minUv, maxUv));
            half4 sample1 = SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                clamp(float2(uv0.x, uv12.y), minUv, maxUv));
            half4 sample2 = SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                clamp(uv12, minUv, maxUv));
            half4 sample3 = SAMPLE_TEXTURE2D_X(
                _BlitTexture,
                sampler_LinearClamp,
                clamp(float2(uv3.x, uv12.y), minUv, maxUv));
            half4 sample4 = SAMPLE_TEXTURE2D_X(
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
                sample0 * weight0
                + sample1 * weight1
                + sample2 * weight2
                + sample3 * weight3
                + sample4 * weight4)
                / weightSum;
        }

        half4 FragPaniniProjection(Varyings input) : SV_Target
        {
            UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            float2 viewPosition =
                (2.0 * input.texcoord - 1.0) * _Params.xy * _Params.w;

            #if _GENERIC
                float2 projectedPosition =
                    Panini_Generic(viewPosition, _Params.z);
            #else
                float2 projectedPosition =
                    Panini_UnitDistance(viewPosition);
            #endif

            float2 projectedNdc = projectedPosition / _Params.xy;

            #if defined(_PANINI_FILTER_BICUBIC)
                float2 sourceUv = saturate(projectedNdc * 0.5 + 0.5);
                float2 halfTexel = 0.5 * _BlitTexture_TexelSize.xy;
                float2 minSourceUv = halfTexel;
                float2 maxSourceUv =
                    max(_RTHandleScale.xy - halfTexel, halfTexel);
                sourceUv *= _RTHandleScale.xy;

                return SampleBicubic(
                    sourceUv,
                    minSourceUv,
                    maxSourceUv);
            #elif defined(_PANINI_FILTER_NEAREST)
                float2 sourceUv = ClampAndScaleUVForPoint(
                    saturate(projectedNdc * 0.5 + 0.5));

                return SAMPLE_TEXTURE2D_X_LOD(
                    _BlitTexture,
                    sampler_PointClamp,
                    sourceUv,
                    0);
            #else
                float2 sourceUv = ClampAndScaleUVForBilinear(
                    projectedNdc * 0.5 + 0.5);

                return SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    sourceUv);
            #endif
        }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        LOD 100
        ZTest Always
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Panini Projection Filtered"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment FragPaniniProjection
            ENDHLSL
        }
    }
}
