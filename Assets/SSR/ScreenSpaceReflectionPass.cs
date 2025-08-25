using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

public class ScreenSpaceReflectionPass : ScriptableRenderPass
{
	#region Definitions

	private struct TextureHandles
	{
		public TextureHandle hitUvHandle;
		public TextureHandle reflectHandle;
		public TextureHandle historyHandle;
	}

	private enum PassStage
	{
		One,
		Two,
		Three,
	}

	private class PassData
	{
		public PassStage stage;

		public TextureHandle source;
		public TextureHandle target;

		public Material material;
		public int materialPassIndex;

		public TextureHandle hitUvHandle;
		public TextureHandle reflectHandle;
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

	private RTHandle historyHandle;
	private readonly Material ssrMaterial;
	private const ScreenSpaceReflectionURP.Resolution resolution = ScreenSpaceReflectionURP.Resolution.Half;
	public bool isMotionValid; // URP SceneView doesn't update motion vectors unless in play mode.

	#endregion

	#region Initialization

	public ScreenSpaceReflectionPass(Material material)
	{
		profilingSampler = new ProfilingSampler("Screen Space Reflection");
		ssrMaterial = material;
	}

	#endregion

	#region Scriptable Render Pass Methods

	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
		UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		TextureHandles textureHandles = CreateRenderGraphTextures(renderGraph, renderingData, cameraData, resourceData);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Screen Space Hit Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.One;
			passData.source = resourceData.cameraDepthTexture;
			passData.target = textureHandles.hitUvHandle;
			passData.material = ssrMaterial;
			passData.materialPassIndex = 0;
			passData.historyHandle = textureHandles.historyHandle;

			builder.SetRenderAttachment(textureHandles.hitUvHandle, 0);
			builder.UseTexture(resourceData.cameraDepthTexture);
			builder.UseGlobalTexture(Shader.PropertyToID("_GBuffer2"));

			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Resolve Color Pass", out PassData passData, profilingSampler))
		{
			passData.stage = PassStage.Two;
			passData.source = resourceData.cameraColor;
			passData.target = textureHandles.reflectHandle;
			passData.material = ssrMaterial;
			passData.materialPassIndex = 1;
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

		// Blit to Screen (required by denoiser)
		renderGraph.AddCopyPass(textureHandles.reflectHandle, resourceData.cameraColor, "Blit to Screen (Denoiser req)");

		if (/*isMotionValid &&*/ VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>().accumFactor.value != 0.0f)
		{
			using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Temporal Denoise", out PassData passData, profilingSampler))
			{
				passData.stage = PassStage.Three;
				passData.source = textureHandles.reflectHandle;
				passData.target = resourceData.cameraColor;
				passData.material = ssrMaterial;
				passData.materialPassIndex = 2;
				passData.historyHandle = textureHandles.historyHandle;

				builder.SetRenderAttachment(resourceData.cameraColor, 0);
				builder.UseTexture(textureHandles.hitUvHandle);
				builder.UseTexture(textureHandles.historyHandle);
				builder.UseTexture(textureHandles.reflectHandle);
				builder.UseTexture(resourceData.cameraDepthTexture);
				builder.UseTexture(resourceData.motionVectorColor);
				builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
			}

			//using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("copy history", out PassData passData, profilingSampler))
			//{
			//	//builder.AllowGlobalStateModification(true);

			//	passData.stage = PassStage.Four;
			//	passData.source = resourceData.cameraColor;
			//	passData.target = textureHandles.historyHandle;
			//	passData.material = ssrMaterial;

			//	builder.SetRenderAttachment(textureHandles.historyHandle, 0, AccessFlags.WriteAll);
			//	builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));

			//	//builder.SetGlobalTextureAfterPass(textureHandles.historyHandle, Shader.PropertyToID("_ScreenSpaceReflectionHistoryTexture"));
			//}

			//// We need to Load & Store the history texture, or it will not be stored on
			//// some platforms.
			//cmd.SetRenderTarget(
			//historyHandle,
			//RenderBufferLoadAction.Load,
			//RenderBufferStoreAction.Store,
			//historyHandle,
			//RenderBufferLoadAction.DontCare,
			//RenderBufferStoreAction.DontCare);

			//// Update History
			renderGraph.AddCopyPass(resourceData.cameraColor, textureHandles.historyHandle, "Update history");
		}
	}

	#endregion

	#region Methods

	public void AddRenderPass()
	{
		ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
		renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
	}

	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		PassStage stage = passData.stage;

		if (stage == PassStage.One)
		{
			passData.material.SetTexture(Shader.PropertyToID("_ScreenSpaceReflectionHistoryTexture"), passData.historyHandle);
			UpdateMaterialProperties(passData.material);
		}
		else if (stage == PassStage.Two)
		{
			passData.material.SetTexture("_ScreenSpaceReflectionHitTexture", passData.hitUvHandle);
		}
		else
		{
		}

		Blitter.BlitTexture(context.cmd, passData.source, Vector2.one, passData.material, passData.materialPassIndex);
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

	private static void UpdateMaterialProperties(Material ssrMaterial)
	{
		ScreenSpaceReflection ssrVolume = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>();

		ssrMaterial.SetFloat("_FrameCount", Time.renderedFrameCount % 64);

		if (ssrVolume.quality.value == ScreenSpaceReflection.Quality.Low)
		{
			ssrMaterial.SetFloat(StepSizeId, 0.4f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.33f);
			ssrMaterial.SetFloat(MaxStepId, 16);
		}
		else if (ssrVolume.quality.value == ScreenSpaceReflection.Quality.Medium)
		{
			ssrMaterial.SetFloat(StepSizeId, 0.3f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.33f);
			ssrMaterial.SetFloat(MaxStepId, 32);
		}
		else if (ssrVolume.quality.value == ScreenSpaceReflection.Quality.High)
		{
			ssrMaterial.SetFloat(StepSizeId, 0.2f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.33f);
			ssrMaterial.SetFloat(MaxStepId, 64);
		}
		else
		{
			ssrMaterial.SetFloat(StepSizeId, 0.2f);
			ssrMaterial.SetFloat(StepSizeMultiplierId, 1.1f);
			ssrMaterial.SetFloat(MaxStepId, ssrVolume.maxStep.value);
		}
		ssrMaterial.SetFloat(MinSmoothnessId, ssrVolume.minSmoothness.value);
		ssrMaterial.SetFloat(FadeSmoothnessId, ssrVolume.fadeSmoothness.value <= ssrVolume.minSmoothness.value ? ssrVolume.minSmoothness.value + 0.01f : ssrVolume.fadeSmoothness.value);
		ssrMaterial.SetFloat(EdgeFadeId, ssrVolume.edgeFade.value);
		ssrMaterial.SetFloat(ThicknessId, ssrVolume.thickness.value);

		ssrMaterial.SetFloat(DownSampleId, (float)resolution * 0.25f);
		ssrMaterial.SetFloat(AccumFactorId, ssrVolume.accumFactor.value);
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
