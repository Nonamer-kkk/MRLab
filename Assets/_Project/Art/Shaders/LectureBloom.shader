Shader "Hidden/MRLab/LectureBloom"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
        TEXTURE2D_X(_LectureLow);
        float4 _LectureTexelSize, _LectureThreshold;
        float _LectureScatter, _LectureIntensity;
        int _LectureDebug;
        float3 Source(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb; }
        float3 Lower(float2 uv) { return SAMPLE_TEXTURE2D_X(_LectureLow, sampler_LinearClamp, uv).rgb; }
        float4 Prefilter(Varyings input) : SV_Target
        {
            float3 color = max(Source(input.texcoord), 0);
            float b = max(color.r, max(color.g, color.b));
            float soft = clamp(b - _LectureThreshold.y, 0, _LectureThreshold.z);
            soft = soft * soft * _LectureThreshold.w;
            float weight = max(soft, b - _LectureThreshold.x) / max(b, .00001);
            return float4(min(color * weight, 100), 1);
        }
        float3 Gaussian(float2 uv, float2 direction)
        {
            // Lecture04 pp.71,73: symmetric nine-tap separable approximation to a 9x9 Gaussian.
            const float weights[5] = { .22702703, .19459459, .12162162, .05405405, .01621622 };
            float3 result = Source(uv) * weights[0];
            [unroll] for (int i=1; i<=4; i++)
                result += (Source(uv + direction*i) + Source(uv - direction*i)) * weights[i];
            return result;
        }
        float4 Horizontal(Varyings input) : SV_Target { return float4(Gaussian(input.texcoord, float2(_LectureTexelSize.x*2,0)),1); }
        float4 Vertical(Varyings input) : SV_Target { return float4(Gaussian(input.texcoord, float2(0,_LectureTexelSize.y)),1); }
        float4 Upsample(Varyings input) : SV_Target { return float4(Source(input.texcoord) + Lower(input.texcoord)*_LectureScatter,1); }
        float4 Composite(Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;
            if (_LectureDebug == 1) return float4(Lower(uv)*_LectureIntensity,1);
            if (_LectureDebug == 3)
            {
                float depth = SAMPLE_TEXTURE2D_LOD(_MainLightShadowmapTexture, sampler_LinearClamp, uv, 0).r;
                return float4(depth.xxx,1);
            }
            if (_LectureDebug == 4)
            {
                float depth = SampleSceneDepth(uv);
                float3 world = ComputeWorldSpacePosition(uv,depth,UNITY_MATRIX_I_VP);
                uint index = ComputeCascadeIndex(world);
                float3 colors[5] = {float3(.8,.12,.12),float3(.12,.8,.15),float3(.12,.3,.95),float3(.85,.6,.1),float3(.1,.1,.1)};
                return float4(colors[min(index,4)],1);
            }
            return float4(Source(uv) + Lower(uv)*_LectureIntensity,1);
        }
        ENDHLSL
        Pass { Name "SoftKneePrefilter" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Prefilter
            ENDHLSL }
        Pass { Name "GaussianHorizontalDownsample" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Horizontal
            ENDHLSL }
        Pass { Name "GaussianVertical" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Vertical
            ENDHLSL }
        Pass { Name "PyramidReconstruction" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Upsample
            ENDHLSL }
        Pass { Name "HDRComposite" HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Composite
            ENDHLSL }
    }
}
