using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Volume component for the full screen blur.
/// </summary>
[VolumeComponentMenu("Custom/FullScreenBlur")]
[VolumeRequiresRendererFeatures(typeof(FullScreenBlurRendererFeature))]
[DisplayInfo(name = "Fullscreen Blur")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class FullScreenBlurVolumeComponent : VolumeComponent, IPostProcessComponent
{
	#region Public Attributes

	public ClampedFloatParameter progress = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);
	public ClampedIntParameter blurRadius = new ClampedIntParameter(8, 2, 32);

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