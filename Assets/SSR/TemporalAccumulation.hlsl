#ifndef URP_TEMPORAL_ACCUMULATION_HLSL
#define URP_TEMPORAL_ACCUMULATION_HLSL

half2 GetVelocityWithOffset(float2 uv, half2 depthOffsetUv)
{
	return -SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_LinearClamp, uv + _MotionVectorTexture_TexelSize.xy * depthOffsetUv).xy;
}

void AdjustBestDepthOffset(inout half bestDepth, inout half bestX, inout half bestY, float2 uv, half currX, half currY)
{
	// Half precision should be fine, as we are only concerned about choosing the better value along sharp edges, so it is acceptable to have banding on continuous surfaces
	half depth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_PointClamp, uv.xy + _BlitTexture_TexelSize.xy * half2(currX, currY)).r;

#if UNITY_REVERSED_Z
	depth = 1.0 - depth;
#endif

	bool isBest = depth < bestDepth;
	bestDepth = isBest ? depth : bestDepth;
	bestX = isBest ? currX : bestX;
	bestY = isBest ? currY : bestY;
}

half3 SampleColorPoint(float2 uv, float2 texelOffset)
{
	return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_PointClamp, uv + _BlitTexture_TexelSize.xy * texelOffset, 0).xyz;
}

void AdjustColorBox(inout half3 boxMin, inout half3 boxMax, inout half3 moment1, inout half3 moment2, float2 uv, half currX, half currY)
{
	half3 color = SampleColorPoint(uv, float2(currX, currY));
	boxMin = min(color, boxMin);
	boxMax = max(color, boxMax);
	moment1 += color;
	moment2 += color * color;
}

// From Playdead's TAA (half version of HDRP)
half3 ClipToAABBCenter(half3 history, half3 minimum, half3 maximum)
{
	half3 center = 0.5 * (maximum + minimum);
	half3 extents = 0.5 * (maximum - minimum);

	half3 offset = history - center;
	half3 v_unit = offset.xyz / extents.xyz;
	half3 absUnit = abs(v_unit);
	half maxUnit = Max3(absUnit.x, absUnit.y, absUnit.z);
	if (maxUnit > 1.0)
		return center + (offset / maxUnit);
	else
		return history;
}

#endif