Shader "Hidden/CustomChromaticAberration"
{
    SubShader
    {
        Tags
        { 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "CustomChromaticAberration"

            ZTest Always
            ZWrite Off
            Cull Off
            Blend One Zero, Zero One
            //Blend SrcAlpha OneMinusSrcAlpha, Zero One

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            
            #pragma vertex Vert
            #pragma fragment Frag

            float _ChromaticIntensity;
            //SAMPLER(sampler_BlitTexture);

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                float2 offset = uv - float2(0.5, 0.5);
                
                // dont use "vignette", equally distort all the image
                //offset = float2(0.7071, 0.7071);

                // clamp offset to avoid distorting more and more towards corners/edges
                offset = min(abs(offset), float2(0.1, 0.1)) * sign(offset);

                // calculate uvs to sample
                float2 uvR = uv;
                float2 uvG = uv - offset * _ChromaticIntensity; 
                float2 uvB = uv - (2.0 * offset) * _ChromaticIntensity;

                // just sample 3 times
                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvR);
                float r = color.r;
                float g = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).g;
                float b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uvB).b;

                //return float4(1.0, 1.0, 1.0, 1.0);

                //return float4(0.0, 0.0, 0.0, 0.5);
                return color;

                return float4(r,g,b, color.a);

                return float4(r, g, b, color.a);
            }

            ENDHLSL
        }
    }

    Fallback Off
}