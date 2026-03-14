using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Custom chromatic aberration volume component.
/// </summary>
[VolumeComponentMenu("Custom/CustomChromaticAberration")]
[VolumeRequiresRendererFeatures(typeof(CustomChromaticAberrationFeature))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
[DisplayInfo(name = "Custom Chromatic Aberration", order = 0)]
public class CustomChromaticAberrationVolume : VolumeComponent
{
	#region Public Attributes

	public ClampedFloatParameter intensity = new ClampedFloatParameter(0.0f, 0.0f, 1.0f);

	#endregion

	#region Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <returns></returns>
	public bool IsActive()
	{
		return intensity.value > 0.0f;
	}

	#endregion

}