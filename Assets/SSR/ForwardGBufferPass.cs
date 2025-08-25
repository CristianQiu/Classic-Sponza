using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RendererUtils;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Pass to render GBuffers in forward or forward+ rendering paths.
/// </summary>
public sealed class ForwardGBufferPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Holds the texture handles used by the render pass.
	/// </summary>
	private struct TextureHandles
	{
		public TextureHandle gBuffer0;
		public TextureHandle gBuffer1;
		public TextureHandle gBuffer2;
		public TextureHandle gBufferDepth;
	}

	/// <summary>
	/// Holds the data used by the render pass.
	/// </summary>
	private class PassData
	{
		public RendererListHandle rendererListHandle;
	}

	#endregion

	#region Private Attributes

	private static readonly ShaderTagId GBufferShaderTagId = new ShaderTagId("UniversalGBuffer");

	private static readonly int GBuffer0Id = Shader.PropertyToID("_GBuffer0");
	private static readonly int GBuffer1Id = Shader.PropertyToID("_GBuffer1");
	private static readonly int GBuffer2Id = Shader.PropertyToID("_GBuffer2");
	//private static readonly int GBufferDepthId = Shader.PropertyToID("_GBufferDepth");

	#endregion

	#region Initialization Methods

	/// <summary>
	/// Constructor.
	/// </summary>
	public ForwardGBufferPass()
	{
		profilingSampler = new ProfilingSampler("Forward GBuffer");
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

		TextureHandles textureHandles = CreateRenderGraphTextures(renderGraph, renderingData, resourceData);

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass("Forward GBuffer", out PassData passData, profilingSampler))
		{
			builder.AllowPassCulling(false);
			builder.AllowGlobalStateModification(true);

			builder.SetRenderAttachment(textureHandles.gBuffer0, 0);
			builder.SetRenderAttachment(textureHandles.gBuffer1, 1);
			builder.SetRenderAttachment(textureHandles.gBuffer2, 2);
			builder.SetRenderAttachmentDepth(textureHandles.gBufferDepth);

			passData.rendererListHandle = CreateRendererList(renderGraph, renderingData, cameraData, resourceData);
			builder.UseRendererList(passData.rendererListHandle);

			builder.SetGlobalTextureAfterPass(textureHandles.gBuffer0, GBuffer0Id);
			builder.SetGlobalTextureAfterPass(textureHandles.gBuffer1, GBuffer1Id);
			builder.SetGlobalTextureAfterPass(textureHandles.gBuffer2, GBuffer2Id);
			//builder.SetGlobalTextureAfterPass(textureHandles.gBufferDepth, GBufferDepthId);

			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}
	}

	#endregion

	#region Methods

	/// <summary>
	/// Creates and returns all the necessary render graph texture handles.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="renderingData"></param>
	/// <param name="resourceData"></param>
	/// <returns></returns>
	private TextureHandles CreateRenderGraphTextures(RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalResourceData resourceData)
	{
		TextureDesc gBufferDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
		gBufferDesc.depthBufferBits = 0;
		gBufferDesc.msaaSamples = MSAASamples.None;
		gBufferDesc.clearBuffer = true;
		gBufferDesc.clearColor = Color.clear;

		TextureHandles textureHandles = new TextureHandles();

		// Albedo.rgb + MaterialFlags.a.
		gBufferDesc.name = "_GBuffer0";
		gBufferDesc.format = GetGBufferFormat(0);
		textureHandles.gBuffer0 = renderGraph.CreateTexture(gBufferDesc);

		// Specular.rgb + Occlusion.a.
		gBufferDesc.name = "_GBuffer1";
		gBufferDesc.format = GetGBufferFormat(1);
		textureHandles.gBuffer1 = renderGraph.CreateTexture(gBufferDesc);

		// NormalWS.rgb + Smoothness.a.
		gBufferDesc.name = "_GBuffer2";
		gBufferDesc.format = GetGBufferFormat(2);
		textureHandles.gBuffer2 = renderGraph.CreateTexture(gBufferDesc);

		// GBuffer depth.
		//TextureDesc gBufferDepthDesc = renderGraph.GetTextureDesc(resourceData.cameraDepth);
		//gBufferDepthDesc.name = "_GBufferDepth";
		//gBufferDepthDesc.clearBuffer = true;
		//gBufferDepthDesc.msaaSamples = MSAASamples.None;
		//textureHandles.gBufferDepth = renderGraph.CreateTexture(gBufferDepthDesc);

		// Assume we can reuse the camera depth to render GBuffer at all times.
		textureHandles.gBufferDepth = resourceData.cameraDepth;

		return textureHandles;

		// Note: there's plenty extra code before rendergraph which I commented just as reminders, where rthandles try to be reused instead of reallocating.
		// I actually dont know if rendergraph is dealing with this and avoiding allocating new stuff when there's an existing resource of the same characteristics requested
		// also theres some extra code trying to ensure some textures params match what we want, renderGraph.GetTextureDesc is now safer than old cameraTargetDescriptor
		//RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
		//desc.depthBufferBits = 0;
		//desc.stencilFormat = GraphicsFormat.None;
		//desc.msaaSamples = 1;
		//RenderingUtils.ReAllocateIfNeeded(ref gBuffer0, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_GBuffer0");

		// If _CameraNormalsTexture exists, lacking smoothness information, set the target to it instead of creating a new RT.
		//if (normalsTextureFieldInfo.GetValue(renderingData.cameraData.renderer) is not RTHandle normalsTextureHandle || renderingData.cameraData.cameraType == CameraType.SceneView) // There're a problem (wrong render target) of reusing normals texture in scene view.
		//{
		//	// NormalWS.rgb + Smoothness.a
		//	desc.graphicsFormat = GetGBufferFormat(2);
		//	RenderingUtils.ReAllocateIfNeeded(ref gBuffer2, desc, FilterMode.Point, TextureWrapMode.Clamp, name: "_GBuffer2");
		//	//gBuffers = new RTHandle[] { gBuffer0, gBuffer1, gBuffer2 };
		//}

		// [OpenGL] Reusing the depth buffer seems to cause black glitching artifacts, so clear
		// the existing depth.
		//bool isOpenGL = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3) || (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore); // GLES 2 is removed.
		//if (isOpenGL || renderingData.cameraData.renderer.cameraDepthTargetHandle.isMSAAEnabled)
		//	ConfigureClear(ClearFlag.Color | ClearFlag.Depth, Color.black);
		//else
		//	// We have to also clear previous color so that the "background" will remain empty
		//	// (black) when moving the camera.
		//	ConfigureClear(ClearFlag.Color, Color.clear);
	}

	/// <summary>
	/// Gets the graphics format for the GBuffer index. From URP Package/Runtime/DeferredLights.cs.
	/// </summary>
	/// <param name="index"></param>
	/// <returns></returns>
	private GraphicsFormat GetGBufferFormat(int index)
	{
		GraphicsFormat format = GraphicsFormat.None;

		switch (index)
		{
			case 0:
				// sRGB albedo, materialFlags.
				format = QualitySettings.activeColorSpace == ColorSpace.Linear ? GraphicsFormat.R8G8B8A8_SRGB : GraphicsFormat.R8G8B8A8_UNorm;
				break;
			case 1:
				// sRGB specular, occlusion.
				format = GraphicsFormat.R8G8B8A8_UNorm;
				break;
			case 2:
				// Normal, normal, normal, packedSmoothness. NormalWS range is -1.0 to 1.0, so we
				// need a signed render texture.
				if (SystemInfo.IsFormatSupported(GraphicsFormat.R8G8B8A8_SNorm, GraphicsFormatUsage.Render))
					format = GraphicsFormat.R8G8B8A8_SNorm;
				else
					format = GraphicsFormat.R16G16B16A16_SFloat;
				break;
		}

		return format;
	}

	/// <summary>
	/// Creates the renderer list handle to render opaque objects into the GBuffer.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="renderingData"></param>
	/// <param name="cameraData"></param>
	/// <param name="resourceData"></param>
	/// <returns></returns>
	private RendererListHandle CreateRendererList(RenderGraph renderGraph, UniversalRenderingData renderingData, UniversalCameraData cameraData, UniversalResourceData resourceData)
	{
		RendererListDesc rendererListDesc = new RendererListDesc(GBufferShaderTagId, renderingData.cullResults, cameraData.camera);
		rendererListDesc.stateBlock = GetRenderStateBlock(renderGraph, cameraData, resourceData);
		rendererListDesc.sortingCriteria = cameraData.defaultOpaqueSortFlags;
		rendererListDesc.renderQueueRange = RenderQueueRange.opaque;

		// TODO: Use rendering layer masks to exclude stuff from being reflected.
		//rendererListDesc.layerMask = ~0;
		//rendererListDesc.renderingLayerMask = renderingLayerMask;
		//rendererListDesc.rendererConfiguration = PerObjectData.None;
		//rendererListDesc.excludeObjectMotionVectors = false;

		return renderGraph.CreateRendererList(rendererListDesc);
	}

	/// <summary>
	/// Gets the render state block to create the renderer list handle.
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="cameraData"></param>
	/// <param name="resourceData"></param>
	/// <returns></returns>
	private RenderStateBlock GetRenderStateBlock(RenderGraph renderGraph, UniversalCameraData cameraData, UniversalResourceData resourceData)
	{
		// Depth Priming.
		RenderStateBlock renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);

		// Reduce GBuffer overdraw using the depth from opaque pass, excluding OpenGL platforms.
		TextureDesc depthDesc = renderGraph.GetTextureDesc(resourceData.cameraDepth);
		bool noMsaa = depthDesc.msaaSamples == MSAASamples.None;

		if ((cameraData.renderType == CameraRenderType.Base || depthDesc.clearBuffer) && noMsaa && !IsOpenGL())
		{
			renderStateBlock.depthState = new DepthState(false, CompareFunction.Equal);
			renderStateBlock.mask |= RenderStateMask.Depth;
		}
		else if (renderStateBlock.depthState.compareFunction == CompareFunction.Equal)
		{
			// TODO: This code path has not been tested.
			renderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);
			renderStateBlock.mask |= RenderStateMask.Depth;
		}

		return renderStateBlock;
	}

	/// <summary>
	/// Gets whether the graphics device type is OpenGL. Does not consider GLES2.
	/// </summary>
	/// <returns></returns>
	private bool IsOpenGL()
	{
		return SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore;
	}

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		context.cmd.DrawRendererList(passData.rendererListHandle);
	}

	#endregion

	#region DEPRECATED

	private RTHandle gBuffer0;
	private RTHandle gBuffer1;
	private RTHandle gBuffer2;
	private RTHandle gBufferDepth;

	private RTHandle[] gBuffers;

	public void Dispose()
	{
		gBuffer0?.Release();
		gBuffer1?.Release();
		gBuffer2?.Release();

		gBufferDepth?.Release();
	}

	//public override void FrameCleanup(CommandBuffer cmd)
	//{
	//	cmd?.ReleaseTemporaryRT(Shader.PropertyToID(gBuffer0?.name));
	//	cmd?.ReleaseTemporaryRT(Shader.PropertyToID(gBuffer1?.name));
	//	cmd?.ReleaseTemporaryRT(Shader.PropertyToID(gBuffer2?.name));
	//	if (gBufferDepth != null)
	//		cmd.ReleaseTemporaryRT(Shader.PropertyToID(gBufferDepth.name));
	//}

	//public override void OnCameraCleanup(CommandBuffer cmd)
	//{
	//	gBuffer0 = null;
	//	gBuffer1 = null;
	//	gBuffer2 = null;
	//	gBufferDepth = null;
	//}

	//public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
	//{
	//	RenderTextureDescriptor desc = renderingData.cameraData.cameraTargetDescriptor;
	//	desc.depthBufferBits = 0; // Color and depth cannot be combined in RTHandles
	//	desc.stencilFormat = GraphicsFormat.None;
	//	desc.msaaSamples = 1; // Do not enable MSAA for GBuffers.

	// // Albedo.rgb + MaterialFlags.a desc.graphicsFormat = GetGBufferFormat(0);
	// RenderingUtils.ReAllocateIfNeeded(ref gBuffer0, desc, FilterMode.Point,
	// TextureWrapMode.Clamp, name: "_GBuffer0"); cmd.SetGlobalTexture("_GBuffer0", gBuffer0);

	// // Specular.rgb + Occlusion.a desc.graphicsFormat = GetGBufferFormat(1);
	// RenderingUtils.ReAllocateIfNeeded(ref gBuffer1, desc, FilterMode.Point,
	// TextureWrapMode.Clamp, name: "_GBuffer1"); cmd.SetGlobalTexture("_GBuffer1", gBuffer1);

	// // If "_CameraNormalsTexture" exists (lacking smoothness info), set the target to it //
	// instead of creating a new RT. if
	// (normalsTextureFieldInfo.GetValue(renderingData.cameraData.renderer) is not RTHandle
	// normalsTextureHandle || renderingData.cameraData.cameraType == CameraType.SceneView) //
	// There're a problem (wrong render target) of reusing normals texture in scene view. { //
	// NormalWS.rgb + Smoothness.a desc.graphicsFormat = GetGBufferFormat(2);
	// RenderingUtils.ReAllocateIfNeeded(ref gBuffer2, desc, FilterMode.Point,
	// TextureWrapMode.Clamp, name: "_GBuffer2"); cmd.SetGlobalTexture("_GBuffer2", gBuffer2);
	// gBuffers = new RTHandle[] { gBuffer0, gBuffer1, gBuffer2 }; } else {
	// cmd.SetGlobalTexture("_GBuffer2", normalsTextureHandle); gBuffers = new RTHandle[] {
	// gBuffer0, gBuffer1, normalsTextureHandle }; }

	// if (renderingData.cameraData.renderer.cameraDepthTargetHandle.isMSAAEnabled) {
	// RenderTextureDescriptor depthDesc = renderingData.cameraData.cameraTargetDescriptor;
	// depthDesc.msaaSamples = 1; RenderingUtils.ReAllocateIfNeeded(ref gBufferDepth, depthDesc,
	// FilterMode.Point, TextureWrapMode.Clamp, name: "_GBuffersDepthTexture");
	// ConfigureTarget(gBuffers, gBufferDepth); } else ConfigureTarget(gBuffers, renderingData.cameraData.renderer.cameraDepthTargetHandle);

	//	// [OpenGL] Reusing the depth buffer seems to cause black glitching artifacts, so clear
	//	// the existing depth.
	//	bool isOpenGL = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3) || (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore); // GLES 2 is removed.
	//	if (isOpenGL || renderingData.cameraData.renderer.cameraDepthTargetHandle.isMSAAEnabled)
	//		ConfigureClear(ClearFlag.Color | ClearFlag.Depth, Color.black);
	//	else
	//		// We have to also clear previous color so that the "background" will remain empty
	//		// (black) when moving the camera.
	//		ConfigureClear(ClearFlag.Color, Color.clear);
	//}

	//public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
	//{
	//	SortingCriteria sortingCriteria = renderingData.cameraData.defaultOpaqueSortFlags;

	// RenderStateBlock renderStateBlock = new(RenderStateMask.Nothing); bool isOpenGL =
	// (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3) ||
	// (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore); // GLES 2 is removed.

	// // Reduce GBuffer overdraw using the depth from opaque pass. (excluding OpenGL platforms) if
	// (!isOpenGL && (renderingData.cameraData.renderType == CameraRenderType.Base ||
	// renderingData.cameraData.clearDepth) &&
	// !renderingData.cameraData.renderer.cameraDepthTargetHandle.isMSAAEnabled) {
	// renderStateBlock.depthState = new DepthState(false, CompareFunction.Equal);
	// renderStateBlock.mask |= RenderStateMask.Depth; } else if
	// (renderStateBlock.depthState.compareFunction == CompareFunction.Equal) {
	// renderStateBlock.depthState = new DepthState(true, CompareFunction.LessEqual);
	// renderStateBlock.mask |= RenderStateMask.Depth; }

	// CommandBuffer cmd = CommandBufferPool.Get(); using (new ProfilingScope(cmd,
	// profilingSampler)) { RendererListDesc rendererListDesc = new(new
	// ShaderTagId("UniversalGBuffer"), renderingData.cullResults, renderingData.cameraData.camera);
	// rendererListDesc.stateBlock = renderStateBlock; rendererListDesc.sortingCriteria =
	// sortingCriteria; rendererListDesc.renderQueueRange = RenderQueueRange.opaque; RendererList
	// rendererList = context.CreateRendererList(rendererListDesc);

	//		cmd.DrawRendererList(rendererList);
	//	}
	//	context.ExecuteCommandBuffer(cmd);
	//	cmd.Clear();
	//	CommandBufferPool.Release(cmd);
	//}

	#endregion
}