Shader "Custom/SecurityCamera"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        ZWrite Off
        Cull Off
        ZTest Always

        Pass
        {
            Name "SecurityCamera"

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _Intensity;

            float RandomNoise(float2 uv)
            {
                return frac(
                    sin(
                        dot(
                            uv,
                            float2(12.9898, 78.233)
                        )
                    ) * 43758.5453
                );
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;

                float4 source =
                    SAMPLE_TEXTURE2D_X_LOD(
                        _BlitTexture,
                        sampler_LinearRepeat,
                        uv,
                        _BlitMipLevel);

                float3 color = source.rgb;

                // --------------------------------
                // 1. Desaturate
                // --------------------------------

                float grayscale =
                    dot(
                        color,
                        float3(0.299, 0.587, 0.114));

                color = lerp(
                    color,
                    grayscale.xxx,
                    0.65);

                // --------------------------------
                // 2. Slight green security-camera tint
                // --------------------------------

                float3 securityTint =
                    float3(
                        0.75,
                        1.0,
                        0.80);

                color *= securityTint;

                // --------------------------------
                // 3. Scanlines
                // --------------------------------

                float scanline =
                    sin(uv.y * 900.0) * 0.035;

                color -= scanline;

                // --------------------------------
                // 4. Film noise
                // --------------------------------

                float noise =
                    RandomNoise(
                        uv * _Time.y);

                noise =
                    (noise - 0.5) * 0.08;

                color += noise;

                // --------------------------------
                // 5. Vignette
                // --------------------------------

                float2 centeredUV =
                    uv - 0.5;

                float vignette =
                    1.0 -
                    dot(
                        centeredUV,
                        centeredUV) * 1.5;

                vignette =
                    saturate(vignette);

                color *= vignette;

                // --------------------------------
                // 6. Overall effect intensity
                // --------------------------------

                color =
                    lerp(
                        source.rgb,
                        color,
                        _Intensity);

                return float4(color, source.a);
            }

            ENDHLSL
        }
    }
}