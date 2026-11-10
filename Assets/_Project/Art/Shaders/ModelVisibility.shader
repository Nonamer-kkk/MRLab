Shader "Hidden/MRLab/ModelVisibility"
{
    Properties { _ObjectId("ID bytes (linear vector)",Vector)=(0,0,0,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            ZWrite On Cull Back
            HLSLPROGRAM
            #pragma vertex VertID
            #pragma fragment FragID
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _ObjectId;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; };
            float4 VertID(Attributes input):SV_POSITION { return TransformObjectToHClip(input.positionOS.xyz); }
            float4 FragID():SV_Target { return _ObjectId; }
            ENDHLSL
        }
    }
}
