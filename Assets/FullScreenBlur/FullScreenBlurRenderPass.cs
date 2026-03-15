using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The full screen blur render pass render pass.
/// </summary>
public sealed class FullScreenBlurRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Stages from the render pass.
	/// </summary>
	private enum PassStage
	{
		HorizontalBlur,
		VerticalBlur,
	}

	/// <summary>
	/// Holds the data needed by the execution of the full screen blur render pass subpasses.
	/// </summary>
	private class PassData
	{
		public PassStage stage;

		public TextureHandle target;
		public TextureHandle source;

		public Material material;
		public int materialPassIndex;

		public TextureHandle blitTextureHandle;
	}

	#endregion

	#region Private Attributes

	private static readonly int KernelRadiusId = Shader.PropertyToID("_BlurKernelRadius");
	private static readonly int BlurStandardDeviationId = Shader.PropertyToID("_BlurStandardDeviation");

	private Material fullScreenBlurMaterial;

	#endregion

	#region Initialization Methods

	public FullScreenBlurRenderPass(Material fullScreenBlurMaterial) : base()
	{
		profilingSampler = new ProfilingSampler("Full Screen Blur");
		renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
		requiresIntermediateTexture = false;

		this.fullScreenBlurMaterial = fullScreenBlurMaterial;
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
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		CreateRenderGraphTextures(renderGraph, cameraData, out TextureHandle blitTextureHandle);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("FullScreen Horizontal Blur Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.HorizontalBlur;
			passData.source = resourceData.cameraColor;
			passData.target = blitTextureHandle;
			passData.material = fullScreenBlurMaterial;
			passData.materialPassIndex = 0;

			builder.SetRenderAttachment(blitTextureHandle, 0, AccessFlags.WriteAll);
			builder.UseTexture(resourceData.cameraColor);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("FullScreen Vertical Blur Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.VerticalBlur;
			passData.source = blitTextureHandle;
			passData.target = resourceData.cameraColor;
			passData.material = fullScreenBlurMaterial;
			passData.materialPassIndex = 1;

			builder.SetRenderAttachment(resourceData.cameraColor, 0, AccessFlags.WriteAll);
			builder.UseTexture(blitTextureHandle);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph textures.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="cameraData"></param>
	/// <param name="blitTextureHandle"></param>
	private void CreateRenderGraphTextures(RenderGraph renderGraph, UniversalCameraData cameraData, out TextureHandle blitTextureHandle)
	{
		RenderTextureDescriptor cameraTargetDescriptor = cameraData.cameraTargetDescriptor;
		cameraTargetDescriptor.depthBufferBits = (int)DepthBits.None;

		blitTextureHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_FullScreenBlurTarget", false);
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		FullScreenBlurVolumeComponent fullScreenBlurVolume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();

		if (passData.stage == PassStage.HorizontalBlur)
		{
			Material fullScreenBlurMaterial = passData.material;

			int maxBlurRadius = fullScreenBlurVolume.blurRadius.value;
			int blurRadius = (int)Mathf.Lerp(2.0f, (float)maxBlurRadius, (float)fullScreenBlurVolume.progress.value);

			fullScreenBlurMaterial.SetInt(KernelRadiusId, blurRadius);
			fullScreenBlurMaterial.SetFloat(BlurStandardDeviationId, (float)blurRadius * 0.5f);
		}

		Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
	}

	#endregion
}