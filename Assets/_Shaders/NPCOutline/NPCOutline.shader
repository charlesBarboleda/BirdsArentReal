Shader "Custom/NPCOutline"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0.001, 0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "NPCOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)

                float4 _OutlineColor;
                float _OutlineWidth;

            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float3 positionOS =
                    input.positionOS.xyz +
                    normalize(input.normalOS) * _OutlineWidth;

                output.positionHCS =
                    TransformObjectToHClip(positionOS);

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }

            ENDHLSL
        }
    }

    Fallback Off
}
