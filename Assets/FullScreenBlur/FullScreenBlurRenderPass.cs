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
		public TextureHandle eightRes;
	}

	#endregion

	#region Private Attributes

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
		//UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		using (IUnsafeRenderGraphBuilder builder = renderGraph.AddUnsafePass("Full Screen Blur", out PassData passData, profilingSampler))
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

			passData.halfRes = t1;
			passData.quarterRes = t2;
			passData.eightRes = t3;
			passData.material = material;

			passData.source = resourceData.activeColorTexture;

			builder.SetRenderAttachment(t1, 0);
			builder.UseTexture(passData.source, AccessFlags.ReadWrite);
			builder.SetRenderFunc((PassData data, UnsafeGraphContext context) => ExecuteUnsafePass(data, context));
		}
	}

	private static void ExecuteUnsafePass(PassData passData, UnsafeGraphContext context)
	{
		CommandBuffer unsafeCmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

		Material material = passData.material;
		UpdateMaterialParameters(passData.material);

		int Pass = 0;

		Blitter.BlitTexture(unsafeCmd, passData.source, Vector2.one, passData.material, Pass);
		Blitter.BlitCameraTexture(unsafeCmd, passData.halfRes, passData.quarterRes, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, Pass);

		Pass = 1;

		Blitter.BlitCameraTexture(unsafeCmd, passData.quarterRes, passData.halfRes, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, Pass);
		Blitter.BlitCameraTexture(unsafeCmd, passData.halfRes, passData.source, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store, material, Pass);
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
	/// Updates the material parameters according to the volume settings.
	/// </summary>
	/// <param name="passData"></param>
	private static void UpdateMaterialParameters(Material material)
	{
		FullScreenBlurVolumeComponent volume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();

		float blurRadius = volume.blurRadius.value;
		material.SetFloat(IntensityId, blurRadius);
	}

	#endregion
}