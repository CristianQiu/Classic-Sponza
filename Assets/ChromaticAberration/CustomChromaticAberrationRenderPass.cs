using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Custom chromatic aberration render pass.
/// </summary>
public class CustomChromaticAberrationRenderPass : ScriptableRenderPass
{
	#region Definitions

	/// <summary>
	/// Holds the data needed by the execution of the render pass.
	/// </summary>
	private class PassData
	{
		public Material material;
		public int materialPassIndex;
	}

	#endregion

	#region Private Attributes

	private const string PassName = "Custom Chromatic Aberration";

	private static readonly int IntensityId = Shader.PropertyToID("_ChromaticIntensity");

	private Material material;

	#endregion

	#region Scriptable Render Pass Methods

	/// <summary>
	/// Constructor.
	/// </summary>
	/// <param name="material"></param>
	/// <param name="passEvent"></param>
	public CustomChromaticAberrationRenderPass(Material material) : base()
	{
		profilingSampler = new ProfilingSampler(PassName);
		renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
		requiresIntermediateTexture = false;
		this.material = material;
	}

	/// <summary>
	/// <inheritdoc/>
	/// </summary>
	/// <param name="renderGraph"></param>
	/// <param name="frameData"></param>
	public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	{
		UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

		using (IRasterRenderGraphBuilder builder = renderGraph.AddRasterRenderPass(PassName, out PassData passData, profilingSampler))
		{
			passData.material = material;
			passData.materialPassIndex = 0;

			builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.ReadWrite);
			//if (resourceData.afterPostProcessColor.IsValid())
			//	builder.UseTexture(resourceData.afterPostProcessColor);
			builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
		}
	}

	#endregion

	/// <summary>
	/// Executes the pass with the information from the pass data.
	/// </summary>
	/// <param name="passData"></param>
	/// <param name="context"></param>
	private static void ExecutePass(PassData passData, RasterGraphContext context)
	{
		// update the material properties according to the volume
		CustomChromaticAberrationVolume volume = VolumeManager.instance.stack.GetComponent<CustomChromaticAberrationVolume>();

		//UpdateMaterialParameters(passData.material);
		//passData.material.SetTexture()
		// 0.1 : scale down for better ux
		passData.material.SetFloat(IntensityId, volume.intensity.value * 0.1f);

		Blitter.BlitTexture(context.cmd, Vector2.one, passData.material, passData.materialPassIndex);
	}
}