using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The full screen blur renderer feature.
/// </summary>
[Tooltip("Adds support to do a full screen blur.")]
[DisallowMultipleRendererFeature("Fullscreen Blur")]
public sealed class FullScreenBlurRendererFeature : ScriptableRendererFeature
{
	#region Private Attributes

	[HideInInspector]
	[SerializeField] private Shader shader;

	private Material material;

	private FullScreenBlurRenderPass renderPass;

	#endregion

	#region Scriptable Renderer Feature Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public override void Create()
	{
		ValidateResources(true);

		renderPass = new FullScreenBlurRenderPass(material);
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderer"></param>
	/// <param name="renderingData"></param>
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		bool isPostProcessEnabled = renderingData.postProcessingEnabled && renderingData.cameraData.postProcessEnabled;
		bool shouldAddRenderPass = isPostProcessEnabled && ShouldAddRenderPass(renderingData.cameraData.cameraType);

		if (shouldAddRenderPass)
			renderer.EnqueuePass(renderPass);
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="disposing"></param>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		CoreUtils.Destroy(material);
	}

	#endregion

	#region Methods

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
			shader = Shader.Find("Hidden/FullScreenBlur");
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
		FullScreenBlurVolumeComponent volume = VolumeManager.instance.stack.GetComponent<FullScreenBlurVolumeComponent>();

		bool isVolumeOk = volume != null && volume.IsActive();
		bool isRenderPassOk = renderPass != null;
		bool areResourcesOk = ValidateResources(false);
		bool isCameraOk = cameraType != CameraType.Preview && cameraType != CameraType.Reflection;

		return isActive && isVolumeOk && isRenderPassOk && areResourcesOk && isCameraOk;
	}

	#endregion
}