using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Custom chromatic aberration feature.
/// </summary>
public class CustomChromaticAberrationFeature : ScriptableRendererFeature
{
	#region Private Attributes

	[HideInInspector]
	[SerializeField] private Shader shader;
	private Material material;

	private CustomChromaticAberrationRenderPass pass;

	#endregion

	#region Scriptable Renderer Feature Methods

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	public override void Create()
	{
		ValidateResources(true);

		pass = new CustomChromaticAberrationRenderPass(material);
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderer"></param>
	/// <param name="renderingData"></param>
	public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	{
		bool isPostProcessEnabled = renderingData.postProcessingEnabled && renderingData.cameraData.postProcessEnabled;
		bool shouldAddPass = isPostProcessEnabled && ShouldAddPass(renderingData.cameraData.cameraType);

		if (shouldAddPass)
			renderer.EnqueuePass(pass);
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
	/// Validates the resources used by the pass.
	/// </summary>
	/// <param name="forceRefresh"></param>
	/// <returns></returns>
	private bool ValidateResources(bool forceRefresh)
	{
		if (forceRefresh)
		{
#if UNITY_EDITOR
			shader = Shader.Find("Hidden/CustomChromaticAberration");
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
	private bool ShouldAddPass(CameraType cameraType)
	{
		CustomChromaticAberrationVolume volume = VolumeManager.instance.stack.GetComponent<CustomChromaticAberrationVolume>();

		bool isVolumeOk = volume != null && volume.IsActive();
		bool isCameraOk = cameraType != CameraType.Preview && cameraType != CameraType.Reflection;
		bool areResourcesOk = ValidateResources(false);

		return isActive && isVolumeOk && isCameraOk && areResourcesOk;
	}

	#endregion
}