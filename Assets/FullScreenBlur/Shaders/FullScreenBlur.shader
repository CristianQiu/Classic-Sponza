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

                float4 center = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv) * 4.0;

                float4 topRight = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, offset.y));
                float4 bottomRight = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, -offset.y));
                float4 bottomLeft = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y));
                float4 topLeft = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, offset.y));

                return (center + topRight + bottomRight + bottomLeft + topLeft) * 0.125;
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

                float2 lowResTexelSize = _BlitTexture_TexelSize.xy;
                float2 offset = lowResTexelSize * _Intensity;
                float2 twiceOffset = offset * 2.0;

                float4 up = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, twiceOffset.y));
                float4 right = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(twiceOffset.x, 0.0));
                float4 bottom = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(0.0, -twiceOffset.y));
                float4 left = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-twiceOffset.x, 0.0));

                float4 topRight = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, offset.y));
                float4 bottomRight = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(offset.x, -offset.y));
                float4 bottomLeft = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, -offset.y));
                float4 topLeft = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv + float2(-offset.x, offset.y));

                float4 cross = up + right + bottom + left;
                float4 diagonal = topRight + bottomRight + bottomLeft + topLeft;

                return (cross + (diagonal * 2.0)) * 0.083333;
            }

            ENDHLSL
        }
    }

    Fallback Off
}