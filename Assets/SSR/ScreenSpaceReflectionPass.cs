using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pass to render SSR.
/// </summary>
public sealed class ScreenSpaceReflectionPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Holds the texture handles used by the render pass.
	/// </summary>
	private struct TextureHandles
	{
		public TextureHandle hitUvHandle;
		public TextureHandle reflectHandle;
		public TextureHandle historyHandle;
	}

	/// <summary>
	/// The subpasses this render pass is made of.
	/// </summary>
	private enum PassStage
	{
		hitUv,
		resolveColor,
		reprojection,
	}

	/// <summary>
	/// Holds the data used by the render pass.
	/// </summary>
	private class PassData
	{
		public PassStage stage;

		public TextureHandle source;
		public TextureHandle target;

		public Material material;
		public int materialPassIndex;

		public TextureHandle hitUvHandle;
		//public TextureHandle reflectHandle;
		public TextureHandle historyHandle;
	}

	#endregion

	#region Private Attributes

	private static readonly int MinSmoothnessId = Shader.PropertyToID("_MinSmoothness");
	private static readonly int FadeSmoothnessId = Shader.PropertyToID("_FadeSmoothness");
	private static readonly int EdgeFadeId = Shader.PropertyToID("_EdgeFade");
	private static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
	private static readonly int StepSizeId = Shader.PropertyToID("_StepSize");
	private static readonly int StepSizeMultiplierId = Shader.PropertyToID("_StepSizeMultiplier");
	private static readonly int MaxStepId = Shader.PropertyToID("_MaxStep");
	private static readonly int DownSampleId = Shader.PropertyToID("_DownSample");
	private static readonly int AccumFactorId = Shader.PropertyToID("_AccumulationFactor");

	private static readonly int SsrReflectionHitTextureId = Shader.PropertyToID("_ScreenSpaceReflectionHitTexture");
	private static readonly int SsrHistoryTextureId = Shader.PropertyToID("_ScreenSpaceReflectionHistoryTexture");

	private Material ssrMaterial;

	private int hitUvPassIndex;
	private int resolveColorPassIndex;
	private int reprojectionPassIndex;

	private RTHandle historyHandle;

	#endregion

	#region Initialization

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="material"></param>
	public ScreenSpaceReflectionPass(Material material)
	{
		profilingSampler = new ProfilingSampler("Screen Space Reflection");
		ssrMaterial = material;
		requiresIntermediateTexture = false;
		renderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 1;

		InitPassesIndices();
	}

	/// <summary>
	/// Initializes the passes indices from their name.
	/// </summary>
	private void InitPassesIndices()
	{
		hitUvPassIndex = ssrMaterial.FindPass("Screen Space Reflection Hit");
		resolveColorPassIndex = ssrMaterial.FindPass("Resolve Reflection");
		reprojectionPassIndex = ssrMaterial.FindPass("Temporal Denoise");
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
		UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		TextureHandles textureHandles = CreateRenderGraphTextures(renderGraph, renderingData, cameraData, resourceData);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("SSR Screen Space Hit Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.hitUv;
			passData.source = resourceData.cameraDepthTexture;
			passData.target = textureHandles.hitUvHandle;
			passData.material = ssrMaterial;
			passData.materialPassIndex = hitUvPassIndex;
			passData.historyHandle = textureHandles.historyHandle;

			builder.SetRenderAttachment(textureHandles.hitUvHandle, 0);
			builder.UseTexture(resourceData.cameraDepthTexture);
			builder.UseGlobalTexture(Shader.PropertyToID("_GBuffer2"));

			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("SSR Resolve Color Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.resolveColor;
			passData.source = resourceData.cameraColor;
			passData.target = textureHandles.reflectHandle;
			passData.material = ssrMaterial;
			passData.materialPassIndex = resolveColorPassIndex;
			passData.hitUvHandle = textureHandles.hitUvHandle;

			builder.SetRenderAttachment(textureHandles.reflectHandle, 0);
			builder.UseTexture(resourceData.cameraColor);
			builder.UseTexture(resourceData.cameraDepthTexture);
			builder.UseTexture(textureHandles.hitUvHandle);

			builder.UseGlobalTexture(Shader.PropertyToID("_GBuffer0"));
			builder.UseGlobalTexture(Shader.PropertyToID("_GBuffer1"));
			builder.UseGlobalTexture(Shader.PropertyToID("_GBuffer2"));

			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		renderGraph.AddCopyPass(textureHandles.reflectHandle, resourceData.cameraColor, "SSR Blit to Screen");

		if (GetSSRVolume().accumFactor.value != 0.0f)
		{
			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("SSR Temporal Denoise", out PassData passData, profilingSampler))
			{
				passData.stage = PassStage.reprojection;
				passData.source = textureHandles.reflectHandle;
				passData.target = resourceData.cameraColor;
				passData.material = ssrMaterial;
				passData.materialPassIndex = reprojectionPassIndex;
				passData.historyHandle = textureHandles.historyHandle;

				builder.SetRenderAttachment(resourceData.cameraColor, 0);
				builder.UseTexture(textureHandles.hitUvHandle);
				builder.UseTexture(textureHandles.historyHandle);
				builder.UseTexture(textureHandles.reflectHandle);
				builder.UseTexture(resourceData.cameraDepthTexture);
				builder.UseTexture(resourceData.motionVectorColor);
				builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
			}

			// Update the history.
			renderGraph.AddCopyPass(resourceData.cameraColor, textureHandles.historyHandle, "SSR Update history");
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Configures this pass before it is enqueued to the renderer.
	/// </summary>
	public void ConfigurePass()
	{
		ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);

		renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
	}

	/// <summary>
	/// Creates and returns all the necessary render graph textures.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="renderingData"></param>
	/// <param name="cameraData"></param>
	/// <param name="resourceData"></param>
	/// <returns></returns>
	private TextureHandles CreateRenderGraphTextures(RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalResourceData resourceData)
	{
		TextureHandles textureHandles = new TextureHandles();

		TextureDesc cameraDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
		cameraDesc.depthBufferBits = 0;
		cameraDesc.msaaSamples = MSAASamples.None;
		cameraDesc.filterMode = FilterMode.Point;
		cameraDesc.wrapMode = TextureWrapMode.Clamp;
		cameraDesc.useMipMap = false;

		ScreenSpaceReflection.Resolution resolution = GetSSRVolume().resolution.value;

		TextureDesc hitDesc = cameraDesc;
		hitDesc.width = (int)resolution * (int)(cameraDesc.width * 0.25f);
		hitDesc.height = (int)resolution * (int)(cameraDesc.height * 0.25f);
		// ARGBHALF - Store "hitUV.xy" + "fresnel.z"
		hitDesc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;

		hitDesc.name = "_ScreenSpaceReflectionHitTexture";
		textureHandles.hitUvHandle = renderGraph.CreateTexture(hitDesc);

		cameraDesc.name = "_ScreenSpaceReflectionColorTexture";
		textureHandles.reflectHandle = renderGraph.CreateTexture(cameraDesc);

		// TODO: This pattern should not be used https://discussions.unity.com/t/introduction-of-render-graph-in-the-universal-render-pipeline-urp/930355/602
		// but there is no fucking way around to go from TextureDesc to RenderTextureDescriptor because the history texture needs to be imported.
		// Unless manually done, which I consider even more risky.
		RenderTextureDescriptor historyDescriptor = cameraData.cameraTargetDescriptor;
		historyDescriptor.depthBufferBits = 0;
		historyDescriptor.msaaSamples = 1;
		historyDescriptor.useMipMap = false;
		RenderingUtils.ReAllocateHandleIfNeeded(ref historyHandle, historyDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceReflectionHistoryTexture");
		ImportResourceParams importHistoryParams = new ImportResourceParams();
		importHistoryParams.clearOnFirstUse = false;
		importHistoryParams.discardOnLastUse = false;
		importHistoryParams.clearColor = Color.clear;
		textureHandles.historyHandle = renderGraph.ImportTexture(historyHandle, importHistoryParams);

		return textureHandles;
	}

	/// <summary>
	/// Updates the material properties with the parameters from the volume.
	/// </summary>
	/// <param name="ssrMaterial"></param>
	private static void UpdateMaterialProperties(Material ssrMaterial)
	{
		ScreenSpaceReflection ssr = GetSSRVolume();

		if (ssr.quality.value == ScreenSpaceReflection.Quality.Low)
		{
			ssrMaterial.SetFloat(StepSizeId, 0.4f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.33f);
			ssrMaterial.SetFloat(MaxStepId, 16);
		}
		else if (ssr.quality.value == ScreenSpaceReflection.Quality.Medium)
		{
			ssrMaterial.SetFloat(StepSizeId, 0.3f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.33f);
			ssrMaterial.SetFloat(MaxStepId, 32);
		}
		else if (ssr.quality.value == ScreenSpaceReflection.Quality.High)
		{
			ssrMaterial.SetFloat(StepSizeId, 0.2f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.33f);
			ssrMaterial.SetFloat(MaxStepId, 64);
		}
		else
		{
			ssrMaterial.SetFloat(StepSizeId, 0.2f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.1f);
			ssrMaterial.SetFloat(MaxStepId, ssr.maxStep.value);
		}

		ssrMaterial.SetFloat(MinSmoothnessId, ssr.minSmoothness.value);
		ssrMaterial.SetFloat(FadeSmoothnessId, ssr.fadeSmoothness.value <= ssr.minSmoothness.value ? ssr.minSmoothness.value + 0.01f : ssr.fadeSmoothness.value);
		ssrMaterial.SetFloat(EdgeFadeId, ssr.edgeFade.value);
		ssrMaterial.SetFloat(ThicknessId, ssr.thickness.value);

		ssrMaterial.SetFloat(DownSampleId, (float)ssr.resolution.value * 0.25f);
		ssrMaterial.SetFloat(AccumFactorId, ssr.accumFactor.value);
	}

	/// <summary>
	/// Gets the SSR from the volume.
	/// </summary>
	/// <returns></returns>
	private static ScreenSpaceReflection GetSSRVolume()
	{
		return VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>();
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		PassStage stage = passData.stage;

		if (stage == PassStage.hitUv)
		{
			// Set history texture from last frame.
			passData.material.SetTexture(SsrHistoryTextureId, passData.historyHandle);
			UpdateMaterialProperties(passData.material);
		}
		else if (stage == PassStage.resolveColor)
		{
			passData.material.SetTexture(SsrReflectionHitTextureId, passData.hitUvHandle);
		}

		Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
	}

	#endregion

	#region DEPRECATED

	//private RTHandle sourceHandle;
	//private RTHandle reflectHandle;

	public void Dispose()
	{
		//sourceHandle?.Release();
		//reflectHandle?.Release();
		historyHandle?.Release();
	}

	//public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
	//{
	//	RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
	//	desc.depthBufferBits = 0;
	//	desc.msaaSamples = 1;
	//	desc.useMipMap = false;

	//	RenderTextureDescriptor descHit = desc;
	//	descHit.width = (int)resolution * (int)(desc.width * 0.25f);
	//	descHit.height = (int)resolution * (int)(desc.height * 0.25f);
	//	descHit.colorFormat = RenderTextureFormat.ARGBHalf; // Store "hitUV.xy" + "fresnel.z"
	//	RenderingUtils.ReAllocateIfNeeded(ref sourceHandle, descHit, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceReflectionHitTexture");
	//	cmd.SetGlobalTexture("_ScreenSpaceReflectionHitTexture", sourceHandle);

	//	RenderingUtils.ReAllocateIfNeeded(ref historyHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceReflectionHistoryTexture");
	//	cmd.SetGlobalTexture("_ScreenSpaceReflectionHistoryTexture", historyHandle);
	//	ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);

	//	RenderingUtils.ReAllocateIfNeeded(ref reflectHandle, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_ScreenSpaceReflectionColorTexture");

	//	ConfigureTarget(sourceHandle, sourceHandle);
	//}
	//public override void FrameCleanup(CommandBuffer cmd)
	//{
	//	if (sourceHandle != null)
	//		cmd.ReleaseTemporaryRT(Shader.PropertyToID(sourceHandle.name));
	//	if (reflectHandle != null)
	//		cmd.ReleaseTemporaryRT(Shader.PropertyToID(reflectHandle.name));
	//	if (historyHandle != null)
	//		cmd.ReleaseTemporaryRT(Shader.PropertyToID(historyHandle.name));
	//}

	//public override void OnCameraCleanup(CommandBuffer cmd)
	//{
	//	sourceHandle = null;
	//	reflectHandle = null;
	//	historyHandle = null;
	//}

	//public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
	//{
	//	CommandBuffer cmd = CommandBufferPool.Get();

	//	using (new ProfilingScope(cmd, new ProfilingSampler("Screen Space Reflection")))
	//	{
	//		UpdateMaterialProperties(ssrMaterial);

	//		// Blit() may not handle XR rendering correctly.
	//		// 5 passes, can we optimize?

	//		// Screen Space Hit
	//		Blitter.BlitCameraTexture(cmd, sourceHandle, sourceHandle, ssrMaterial, pass: 0);
	//		// Resolve Color
	//		Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, reflectHandle, ssrMaterial, pass: 1);
	//		// Blit to Screen (required by denoiser)
	//		Blitter.BlitCameraTexture(cmd, reflectHandle, renderingData.cameraData.renderer.cameraColorTargetHandle);

	//		// Temporal Denoise (alpha blend)
	//		if (isMotionValid && VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>().accumFactor.value != 0.0f)
	//		{
	//			Blitter.BlitCameraTexture(cmd, reflectHandle, renderingData.cameraData.renderer.cameraColorTargetHandle, ssrMaterial, pass: 2);

	//			// We need to Load & Store the history texture, or it will not be stored on
	//			// some platforms.
	//			cmd.SetRenderTarget(
	//			historyHandle,
	//			RenderBufferLoadAction.Load,
	//			RenderBufferStoreAction.Store,
	//			historyHandle,
	//			RenderBufferLoadAction.DontCare,
	//			RenderBufferStoreAction.DontCare);
	//			// Update History
	//			Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, historyHandle);
	//		}
	//		cmd.SetGlobalTexture("_ScreenSpaceReflectionHistoryTexture", historyHandle);
	//	}
	//	context.ExecuteCommandBuffer(cmd);
	//	cmd.Clear();
	//	CommandBufferPool.Release(cmd);
	//}

	#endregion
}
