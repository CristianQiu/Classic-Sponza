Shader "Hidden/FullScreenBlur"
{
    SubShader
    {
        Tags
        { 
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Downsample"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _Intensity;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
    
                float2 highResTexelSize = _BlitTexture_TexelSize.xy;
                float2 offset = highResTexelSize * _Intensity;

                float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 4.0;

                float4 topRight = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, offset.y)); // top-right
                float4 a = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, -offset.y)); // bottom-right
                float4 b = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y)); // bottom-left
                float4 c = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, offset.y)); // top-left

                return (color + topRight + a + b + c) * 0.125;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Upsample"
            
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag

            float _Intensity;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
    
                float2 halfpixel = _BlitTexture_TexelSize.xy /* * 0.5 */;
                float2 o = halfpixel * _Intensity;

                float4 color = 0;

                // Edge centers (1x weight each)
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x * 2.0, 0.0)); 
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( o.x * 2.0, 0.0)); 
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, -o.y * 2.0)); 
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0,  o.y * 2.0)); 

                // Diagonal corners (2x weight each)
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x,  o.y)) * 2.0; 
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( o.x,  o.y)) * 2.0; 
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-o.x, -o.y)) * 2.0; 
                color += SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2( o.x, -o.y)) * 2.0; 

                return color * 0.083333;
            }

            ENDHLSL
        }
    }

    Fallback Off
}