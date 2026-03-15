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

		public TextureHandle t1;
		public TextureHandle t2;
		public TextureHandle t3;

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

		//CreateRenderGraphTextures(renderGraph, cameraData, out TextureHandle blitTextureHandle);

		//using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("FullScreen Horizontal Blur Pass", out PassData passData, profilingSampler))
		//{
		//	passData.stage = PassStage.HorizontalBlur;
		//	passData.source = resourceData.cameraColor;
		//	passData.target = blitTextureHandle;
		//	passData.material = fullScreenBlurMaterial;
		//	passData.materialPassIndex = 0;

		//	builder.SetRenderAttachment(blitTextureHandle, 0, AccessFlags.WriteAll);
		//	builder.UseTexture(resourceData.cameraColor);
		//	builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		//}

		//using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("FullScreen Vertical Blur Pass", out PassData passData, profilingSampler))
		//{
		//	passData.stage = PassStage.VerticalBlur;
		//	passData.source = blitTextureHandle;
		//	passData.target = resourceData.cameraColor;
		//	passData.material = fullScreenBlurMaterial;
		//	passData.materialPassIndex = 1;

		//	builder.SetRenderAttachment(resourceData.cameraColor, 0, AccessFlags.WriteAll);
		//	builder.UseTexture(blitTextureHandle);
		//	builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		//}
		using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("unsafe blur", out PassData passData, profilingSampler))
		{
			TextureDesc desc = renderGraph.GetTextureDesc(resourceData.activeColorTexture);
			desc.width /= 2;
			desc.height /= 2;
			TextureHandle t1 = builder.CreateTransientTexture(desc);

			desc.width /= 2;
			desc.height /= 2;
			TextureHandle t2 = builder.CreateTransientTexture(desc);

			desc.width /= 2;
			desc.height /= 2;
			TextureHandle t3 = builder.CreateTransientTexture(desc);

			passData.t1 = t1;
			passData.t2 = t2;
			passData.t3 = t3;
			passData.material = fullScreenBlurMaterial;

			passData.source = resourceData.activeColorTexture;
			passData.target = t1;

			builder.SetRenderAttachment(t1, 0);
			builder.UseTexture(passData.source, AccessFlags.ReadWrite);
			builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecuteUnsafePass(data, context));
		}
	}

	private void ExecuteUnsafePass(PassData data, UnsafeGraphContext context)
	{
		CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
		//Blitter.BlitCameraTexture(unsafeCmd, passData.source, passData.target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, passData.material, passData.materialPassIndex);

		Material fullScreenBlurMaterial = data.material;
		FullScreenBlurVolumeComponent fullScreenBlurVolume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();

		float blurRadius = fullScreenBlurVolume.blurRadius.value;
		fullScreenBlurMaterial.SetFloat(KernelRadiusId, blurRadius);

		int Pass = 2;

		Blitter.BlitTexture(unsafeCmd, data.source, data.t1, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
		Blitter.BlitCameraTexture(unsafeCmd, data.t1, data.t2, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);

		//Blitter.BlitCameraTexture(unsafeCmd, data.source, data.target, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
		//Blitter.BlitTexture(unsafeCmd, data.target, data.t2, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
		//Blitter.BlitTexture(unsafeCmd, data.t2, data.t3, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
		Pass = 3;

		Blitter.BlitCameraTexture(unsafeCmd, data.t2, data.t1, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
		Blitter.BlitCameraTexture(unsafeCmd, data.t1, data.source, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);

		//Blitter.BlitTexture(unsafeCmd, data.t3, data.t2, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
		//Blitter.BlitTexture(unsafeCmd, data.t2, data.t1, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
		//Blitter.BlitTexture(unsafeCmd, data.t1, data.source, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, data.material, Pass);
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

		blitTextureHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, cameraTargetDescriptor, "_FullScreenBlur", false);
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		//FullScreenBlurVolumeComponent fullScreenBlurVolume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();

		//if (passData.stage == PassStage.HorizontalBlur)
		//{
		//	Material fullScreenBlurMaterial = passData.material;

		// int maxBlurRadius = fullScreenBlurVolume.blurRadius.value; int blurRadius =
		// (int)Mathf.Lerp(2.0f, (float)maxBlurRadius, (float)fullScreenBlurVolume.progress.value);

		//	fullScreenBlurMaterial.SetInt(KernelRadiusId, blurRadius);
		//	fullScreenBlurMaterial.SetFloat(BlurStandardDeviationId, (float)blurRadius * 0.5f);
		//}

		//Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
	}

	#endregion
}