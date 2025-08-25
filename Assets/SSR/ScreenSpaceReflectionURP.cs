using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Screen Space Reflection URP")]
[Tooltip("Add this Renderer Feature to support screen space reflection in URP Volume.")]
public class ScreenSpaceReflectionURP : ScriptableRendererFeature
{
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
		bool isUsingDeferred = (renderingMode != RenderingMode.Forward) && (renderingMode != RenderingMode.ForwardPlus);
		if (isUsingDeferred)
			return;

		// URP forces Forward path on OpenGL platforms.
		bool isOpenGL = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3) || (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore); // GLES 2 is removed.

		var stack = VolumeManager.instance.stack;
		ScreenSpaceReflection ssrVolume = stack.GetComponent<ScreenSpaceReflection>();
		bool isActive = ssrVolume != null && ssrVolume.IsActive();
		bool isDebugger = DebugManager.instance.isAnyDebugUIActive;

		if (renderingData.cameraData.camera.cameraType != CameraType.Preview && isActive && (!isDebugger /*|| renderingDebugger*/))
		{
			if (!isUsingDeferred || isOpenGL) 
				renderer.EnqueuePass(forwardGBufferPass);

			screenSpaceReflectionPass.ConfigurePass();
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