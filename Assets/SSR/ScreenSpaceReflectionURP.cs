using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleRendererFeature("Screen Space Reflection URP")]
[Tooltip("Add this Renderer Feature to support screen space reflection in URP Volume.")]
public class ScreenSpaceReflectionURP : ScriptableRendererFeature
{
	#region Private Attributes

	// Render GBuffers in Forward path.
	private static readonly FieldInfo renderingModeFieldInfo = typeof(UniversalRenderer).GetField("m_RenderingMode", BindingFlags.NonPublic | BindingFlags.Instance);
	private static readonly FieldInfo normalsTextureFieldInfo = typeof(UniversalRenderer).GetField("m_NormalsTexture", BindingFlags.NonPublic | BindingFlags.Instance);

	[HideInInspector]
	[SerializeField] private Shader shader;

	private Material material;

	private ForwardGBufferPass forwardGBufferPass;
	private ScreenSpaceReflectionPass screenSpaceReflectionPass;

	#endregion

	#region Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public override void Create()
	{
		ValidateResources(true);
	
		forwardGBufferPass = new ForwardGBufferPass();
		screenSpaceReflectionPass = new ScreenSpaceReflectionPass(material);
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderer"></param>
	/// <param name="renderingData"></param>
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		bool isPostProcessEnabled = renderingData.postProcessingEnabled && renderingData.cameraData.postProcessEnabled;
		bool addPass = isPostProcessEnabled && ShouldAddRenderPass(renderingData.cameraData.cameraType);

		var renderingMode = (RenderingMode)renderingModeFieldInfo.GetValue(renderer as UniversalRenderer);
		bool isUsingDeferred = (renderingMode != RenderingMode.Forward) && (renderingMode != RenderingMode.ForwardPlus);
		if (isUsingDeferred)
			return;

		if (addPass && !isUsingDeferred)
		{
			// URP forces Forward path on OpenGL platforms.
			// TODO: if opengl or deferred, queue gbuffer
			//bool isOpenGL = (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3) || (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore);

			screenSpaceReflectionPass.ConfigurePass();

			renderer.EnqueuePass(forwardGBufferPass);
			renderer.EnqueuePass(screenSpaceReflectionPass);
		}
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="disposing"></param>
	protected override void Dispose(bool disposing)
	{
		screenSpaceReflectionPass?.Dispose();
		forwardGBufferPass?.Dispose();
	}

	/// <summary>
	/// Validates the resources used by the render pass.
	/// </summary>
	/// <param name="forceRefresh"></param>
	/// <returns></returns>
	private bool ValidateResources(bool forceRefresh)
	{
		if (forceRefresh)
		{
#if UNITY_EDITOR
			shader = Shader.Find("Hidden/Lighting/ScreenSpaceReflection");
#endif
			CoreUtils.Destroy(material);
			material = CoreUtils.CreateEngineMaterial(shader);
		}

		return shader != null && material != null;
	}

	/// <summary>
	/// Gets whether the render pass should be enqueued to the renderer.
	/// </summary>
	/// <param name="cameraType"></param>
	/// <returns></returns>
	private bool ShouldAddRenderPass(CameraType cameraType)
	{
		ScreenSpaceReflection volume = VolumeManager.instance.stack.GetComponent<ScreenSpaceReflection>();

		bool isCameraOk = cameraType != CameraType.Preview && cameraType != CameraType.Reflection && cameraType != CameraType.SceneView;
		bool areResourcesOk = ValidateResources(false);
		bool isRenderPassOk = forwardGBufferPass != null && screenSpaceReflectionPass != null;
		bool isFogVolumeOk = volume != null && volume.IsActive();

		return isActive && isCameraOk && areResourcesOk && isRenderPassOk && isFogVolumeOk;
	}

	#endregion
}