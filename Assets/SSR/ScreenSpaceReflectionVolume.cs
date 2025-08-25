using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[VolumeComponentMenu("Custom/Screen Space Reflection")]
[VolumeRequiresRendererFeatures(typeof(ScreenSpaceReflectionURP))]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class ScreenSpaceReflection : VolumeComponent, IPostProcessComponent
{
	[InspectorName("State (Opaque)"), Tooltip("When set to Enabled, URP processes SSR on opaque objects for Cameras in the influence of this effect's Volume.")]
	public SSRStateParameter state = new(value: State.Disabled, overrideState: true);
	[InspectorName("Render Resolution"), Tooltip("Render resolution for SSR.")]
	public SSRResolutionParameter resolution = new(value: Resolution.Half, overrideState: true);
	[InspectorName("Minimum Smoothness"), Tooltip("SSR ignores a pixel if its smoothness value is lower than this value.")]
	public ClampedFloatParameter minSmoothness = new(value: 0.33f, min: 0.0f, max: 1.0f);
	[InspectorName("Smoothness Fade Start"), Tooltip("Use the slider to set the smoothness value at which SSR reflections begin to fade out.")]
	public ClampedFloatParameter fadeSmoothness = new(value: 0.6f, min: 0.0f, max: 1.0f);
	[InspectorName("Screen Edge Fade Distance"), Tooltip("Fades out screen space reflection when it is near the screen boundaries.")]
	public ClampedFloatParameter edgeFade = new(value: 0.1f, min: 0.0f, max: 1.0f, overrideState: true);
	[InspectorName("Object Thickness"), Tooltip("The thickness of all scene objects. This is also the fallback thickness for automatic thickness mode.")]
	public ClampedFloatParameter thickness = new(value: 0.25f, min: 0.0f, max: 1.0f, overrideState: true);
	[Tooltip("The quality of ray marching. The custom mode provides the best quality.")]
	public SSRQualityParameter quality = new(value: Quality.Low);
	[InspectorName("Max Ray Steps"), Tooltip("The maximum ray steps for custom quality mode.")]
	public ClampedIntParameter maxStep = new(value: 16, min: 4, max: 128);
	[InspectorName("Accumulation Factor"), Tooltip("The speed of accumulation convergence for PBR Accumulation mode. Does not work properly with distortion post-processing effects due to URP limitations.")]
	public ClampedFloatParameter accumFactor = new(value: 0.75f, min: 0.0f, max: 1.0f);

	public bool IsActive()
	{
		return state.value == State.Enabled && minSmoothness.value < 1.0f && SystemInfo.supportedRenderTargetCount >= 3;
	}

	public enum State
	{
		[Tooltip("Disable URP screen space reflection.")]
		Disabled = 0,
		[Tooltip("Enable URP screen space reflection.")]
		Enabled = 1
	}

	public enum Resolution
	{
		[InspectorName("100%")]
		[Tooltip("Do ray marching at 100% resolution.")]
		Full = 4,

		[InspectorName("75%")]
		[Tooltip("Do ray marching at 75% resolution.")]
		ThreeQuarters = 3,

		[InspectorName("50%")]
		[Tooltip("Do ray marching at 50% resolution.")]
		Half = 2,

		[InspectorName("25%")]
		[Tooltip("Do ray marching at 25% resolution.")]
		Quarter = 1
	}

	public enum Quality
	{
		[Tooltip("Low quality mode with 16 ray steps.")]
		Low = 0,
		[Tooltip("Medium quality mode with 32 ray steps.")]
		Medium = 1,
		[Tooltip("High quality mode with 64 ray steps.")]
		High = 2,
		[Tooltip("Custom quality mode with 16 ray steps by default.")]
		Custom = 3
	}

	/// <summary>
	/// A <see cref="VolumeParameter"/> that holds a <see cref="State"/> value.
	/// </summary>
	[Serializable]
	public sealed class SSRStateParameter : VolumeParameter<State>
	{
		/// <summary>
		/// Creates a new <see cref="SSRStateParameter"/> instance.
		/// </summary>
		/// <param name="value">The initial value to store in the parameter.</param>
		/// <param name="overrideState">The initial override state for the parameter.</param>
		public SSRStateParameter(State value, bool overrideState = false) : base(value, overrideState) { }
	}

	/// <summary>
	/// A <see cref="VolumeParameter"/> that holds a <see cref="Resolution"/> value.
	/// </summary>
	[Serializable]
	public sealed class SSRResolutionParameter : VolumeParameter<Resolution>
	{
		/// <summary>
		/// Creates a new <see cref="SSRResolutionParameter"/> instance.
		/// </summary>
		/// <param name="value">The initial value to store in the parameter.</param>
		/// <param name="overrideState">The initial override state for the parameter.</param>
		public SSRResolutionParameter(Resolution value, bool overrideState = false) : base(value, overrideState) { }
	}

	/// <summary>
	/// A <see cref="VolumeParameter"/> that holds a <see cref="Quality"/> value.
	/// </summary>
	[Serializable]
	public sealed class SSRQualityParameter : VolumeParameter<Quality>
	{
		/// <summary>
		/// Creates a new <see cref="SSRQualityParameter"/> instance.
		/// </summary>
		/// <param name="value">The initial value to store in the parameter.</param>
		/// <param name="overrideState">The initial override state for the parameter.</param>
		public SSRQualityParameter(Quality value, bool overrideState = false) : base(value, overrideState) { }
	}
}