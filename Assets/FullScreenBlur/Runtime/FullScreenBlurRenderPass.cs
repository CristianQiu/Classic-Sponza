using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The full screen blur render pass.
/// </summary>
public sealed class FullScreenBlurRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Holds the data needed by the execution of the render pass.
	/// </summary>
	private class PassData
	{
		public TextureHandle source;

		public Material material;
		public int materialPassIndex;

		public TextureHandle halfRes;
		public TextureHandle quarterRes;
	}

	#endregion

	#region Private Attributes

	private const float DoublePassProgressThreshold = 0.5f;
	private const float BlurIntensity = 5.0f;
	private const float BlurSizeScalingReferenceHeight = 1440.0f;

	private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

	private Material material;

	#endregion

	#region Initialization Methods

	public FullScreenBlurRenderPass(Material material) : base()
	{
		profilingSampler = new ProfilingSampler("Full Screen Blur");
		renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
		requiresIntermediateTexture = false;

		this.material = material;
	}

	#endregion

	#region Scriptable Render Pass Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="frameData"></param>
	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("Full Screen Blur", out PassData passData, profilingSampler))
		{
			CreateRenderGraphTextures(renderGraph, resourceData, builder, out TextureHandle halfRes, out TextureHandle quarterRes);

			passData.source = resourceData.activeColorTexture;
			passData.material = material;
			passData.halfRes = halfRes;
			passData.quarterRes = quarterRes;

			builder.SetRenderAttachment(halfRes, 0);
			builder.UseTexture(passData.source, AccessFlags.ReadWrite);
			builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecuteUnsafePass(data, context));
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph texture handles.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="resourceData"></param>
	/// <param name="halfRes"></param>
	/// <param name="quarterRes"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalResourceData resourceData, IUnsafeRenderGraphBuilder builder, out TextureHandle halfRes, out TextureHandle quarterRes)
	{
		TextureDesc descriptor = renderGraph.GetTextureDesc(resourceData.activeColorTexture);

		descriptor.width = Mathf.RoundToInt((float)descriptor.width / 2.0f);
		descriptor.height = Mathf.RoundToInt((float)descriptor.height / 2.0f);
		halfRes = builder.CreateTransientTexture(descriptor);

		descriptor.width = Mathf.RoundToInt((float)descriptor.width / 2.0f);
		descriptor.height = Mathf.RoundToInt((float)descriptor.height / 2.0f);
		quarterRes = builder.CreateTransientTexture(descriptor);
	}

	/// <summary>
	/// Updates the material parameters according to the volume settings.
	/// </summary>
	/// <param name="material"></param>
	private static void UpdateMaterialParameters(Material material)
	{
		FullScreenBlurVolumeComponent volume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();

		float progress = volume.intensity.value;
		float blurIntensity = BlurIntensity;

		if (progress <= DoublePassProgressThreshold)
		{
			blurIntensity = Mathf.Lerp(0.0f, BlurIntensity * 0.75f, volume.intensity.value * 4.0f);
		}
		else
		{
			float t = Mathf.InverseLerp(DoublePassProgressThreshold, 1.0f, progress);
			float newProgress = Mathf.Lerp(0.0f, 1.0f, t);

			blurIntensity = Mathf.Lerp(BlurIntensity * 0.175f, BlurIntensity, newProgress);
		}

		float factor = Screen.height / BlurSizeScalingReferenceHeight;

		// An increase of x4 pixels equals to a multiplier of 2.5 to blur intensity.
		factor = Mathf.InverseLerp(0.0f, 4.0f, factor);
		factor = Mathf.Lerp(0.0f, 2.5f, factor);

		material.SetFloat(IntensityId, blurIntensity * factor);
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecuteUnsafePass(PassData passData, UnsafeGraphContext context)
	{
		Material material = passData.material;

		UpdateMaterialParameters(material);

		CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

		FullScreenBlurVolumeComponent volume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();

		Blitter.BlitTexture(cmd, passData.source, Vector2.one, material, 0);

		if (volume.intensity.value <= DoublePassProgressThreshold)
		{
			Blitter.BlitCameraTexture(cmd, passData.halfRes, passData.source, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, 1);
			return;
		}

		Blitter.BlitCameraTexture(cmd, passData.halfRes, passData.quarterRes, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, 0);

		Blitter.BlitCameraTexture(cmd, passData.quarterRes, passData.halfRes, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, 1);
		Blitter.BlitCameraTexture(cmd, passData.halfRes, passData.source, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, 1);
	}

	#endregion
}