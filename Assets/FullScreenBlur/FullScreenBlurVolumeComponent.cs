using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume component for the full screen blur.
/// </summary>
[DisplayInfo(name = "Full Screen Blur")]
[VolumeComponentMenu("Custom/Full Screen Blur")]
[VolumeRequiresRendererFeatures(typeof(FullScreenBlurRendererFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class FullScreenBlurVolumeComponent : VolumeComponent, IPostProcessComponent
{
	#region Public Attributes

	public ClampedFloatParameter progress = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
	public ClampedFloatParameter blurRadius = new ClampedFloatParameter(1.0f, 0.0f, 16.0f);

	#endregion

	#region IPostProcessComponent Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <returns></returns>
	public bool IsActive()
	{
		return progress.value > 0.0f;
	}

	#endregion
}