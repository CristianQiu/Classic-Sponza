using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Screen Space Reflection URP")]
[Tooltip("Add this Renderer Feature to support screen space reflection in URP Volume.")]
public class ScreenSpaceReflectionURP : ScriptableRendererFeature
{
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

	[Header("Setup")]
	[Tooltip("The post-processing material of screen space reflection.")]
	public Material material;

	[Header("PBR Accumulation")]
	[Tooltip("Enable this to denoise SSR at anytime in SceneView. This is disabled by default because URP SceneView only updates motion vectors in play mode.")]
	public bool sceneView = false;

	private const string ssrShaderName = "Hidden/Lighting/ScreenSpaceReflection";
	private ScreenSpaceReflectionPass screenSpaceReflectionPass;
	private ForwardGBufferPass forwardGBufferPass;

	// Pirnt message only once when using the rendering debugger.
	private bool isLogPrinted = false;

	// Render GBuffers in Forward path.
	private static readonly FieldInfo renderingModeFieldInfo = typeof(UniversalRenderer).GetField("m_RenderingMode", BindingFlags.NonPublic | BindingFlags.Instance);
	private static readonly FieldInfo normalsTextureFieldInfo = typeof(UniversalRenderer).GetField("m_NormalsTexture", BindingFlags.NonPublic | BindingFlags.Instance);

	public Material SSRMaterial
	{
		get { return material; }
		set { material = (value.shader == Shader.Find(ssrShaderName)) ? value : material; }
	}

	public override void Create()
	{
		if (material != null)
		{
			if (material.shader != Shader.Find(ssrShaderName))
			{
				Debug.LogErrorFormat("Screen Space Reflection URP: Material shader should be {0}.", ssrShaderName);
				return;
			}
		}
		else
			return;

		if (screenSpaceReflectionPass == null)
		{
			screenSpaceReflectionPass = new(material);
			screenSpaceReflectionPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents + 1;
		}

		if (forwardGBufferPass == null)
		{
			forwardGBufferPass = new();
			forwardGBufferPass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents; // Depth Priming
		}
	}

	protected override void Dispose(bool disposing)
	{
		screenSpaceReflectionPass?.Dispose();
		forwardGBufferPass?.Dispose();
	}

	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		if (material == null)
		{
			Debug.LogErrorFormat("Screen Space Reflection URP: Post-processing material is empty.");
			return;
		}

		var renderingMode = (RenderingMode)renderingModeFieldInfo.GetValue(renderer as UniversalRenderer);
		bool isUsingDeferred = (renderingMode != RenderingMode.Forward) && (renderingMode != RenderingMode.ForwardPlus); // URP may have Deferred+ in the future.

		// URP forces Forward path on OpenGL platforms.
		bool isOpenGL = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3) || (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore); // GLES 2 is removed.

		var stack = VolumeManager.instance.stack;
		ScreenSpaceReflection ssrVolume = stack.GetComponent<ScreenSpaceReflection>();
		bool isActive = ssrVolume != null && ssrVolume.IsActive();
		bool isDebugger = DebugManager.instance.isAnyDebugUIActive;

		bool isMotionValid = true;
#if UNITY_EDITOR
		// Motion Vectors of URP SceneView don't get updated each frame when not entering play mode.
		// (Might be fixed when supporting scene view anti-aliasing) Change the method to
		// multi-frame accumulation (offline mode) if SceneView is not in play mode.
		isMotionValid = sceneView || UnityEditor.EditorApplication.isPlaying || renderingData.cameraData.camera.cameraType != CameraType.SceneView;
#endif

		if (renderingData.cameraData.camera.cameraType != CameraType.Preview && isActive && (!isDebugger /*|| renderingDebugger*/))
		{
			if (!isUsingDeferred || isOpenGL) 
				renderer.EnqueuePass(forwardGBufferPass);

			screenSpaceReflectionPass.isMotionValid = isMotionValid;
//#if UNITY_2023_2_OR_NEWER
			// [PBR Accumulation] Looks like there's a bug with the queue of URP's final blit pass
			// when enabling FXAA in 2023.2 (alpha & beta). We will move the queue of SSR pass
			// forward in that case. The next step is to integrate with SRP render graph, and
			// probably there will be more injection points available in URP, which makes PBR
			// Accumulation more useful.
			//screenSpaceReflectionPass.renderPassEvent = ssrVolume.accumFactor.value == 0.0f ? RenderPassEvent.BeforeRenderingPostProcessing : (renderingData.cameraData.camera.cameraType != CameraType.SceneView && renderingData.cameraData.camera.GetComponent<UniversalAdditionalCameraData>().antialiasing == AntialiasingMode.FastApproximateAntialiasing) ? RenderPassEvent.AfterRenderingPostProcessing - 1 : RenderPassEvent.AfterRenderingPostProcessing;
//#endif
			screenSpaceReflectionPass.AddRenderPass();
			renderer.EnqueuePass(screenSpaceReflectionPass);
			isLogPrinted = false;
		}
		else if (isDebugger && isLogPrinted == false)
		{
			Debug.Log("Screen Space Reflection URP: Disable effect to avoid affecting rendering debugging.");
			isLogPrinted = true;
		}
	}
}