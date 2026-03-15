// Returns the gaussian weight given the standard deviation and 2D offset.
// See: https://en.wikipedia.org/wiki/Gaussian_blur
float Gaussian2D(float standardDeviation, float2 offset)
{
    float standardDeviationSq = standardDeviation * standardDeviation;
    float a = 1.0 / (TWO_PI * standardDeviation);
    
    float offsetXSq = offset.x * offset.x;
    float offsetYSq = offset.y * offset.y;
    float offsetSqSum = offsetXSq + offsetYSq;

    float b = exp(-(offsetSqSum / (2.0 * standardDeviationSq) ));
    
    return a * b;
}

void GaussianBlur_Shadergraph_float(float2 uv, float kernelRadius, float sigma, out float4 result)
{
    kernelRadius *= 0.5f;

    float2 textureToBlurTexelSizeXy = 1.2f;
    float3 res = float3(0.0, 0.0, 0.0);
    int kernelRadiusInt = (int)kernelRadius;

    UNITY_LOOP
    for (int i = -kernelRadiusInt; i <= kernelRadiusInt; ++i)
    {
        for (int j = -kernelRadiusInt; j <= kernelRadiusInt; ++j)
        {
            float2 uvOffset = float2(i * textureToBlurTexelSizeXy.x, j * textureToBlurTexelSizeXy.y);
            float2 uvSample = uv + uvOffset;
            float3 rgb = SHADERGRAPH_SAMPLE_SCENE_COLOR(uvSample).rgb;
            res += (rgb * Gaussian2D(sigma, uvOffset));
        }
    }

    //float alpha = SHADERGRAPH_SAMPLE_SCENE_COLOR(uv).a;

    result = float4(res, 1.0);
}