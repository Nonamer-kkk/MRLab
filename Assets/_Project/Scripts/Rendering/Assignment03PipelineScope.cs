using UnityEngine;
using UnityEngine.Rendering;

namespace MRLab.Joule.Rendering
{
    /// <summary>Selects the dedicated renderer while the submission scene is open; restores the previous PC pipeline on disable.</summary>
    [ExecuteAlways]
    public sealed class Assignment03PipelineScope : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        RenderPipelineAsset previousDefault, previousQuality;
        bool applied;
        void OnEnable()
        {
            if (pipeline == null || applied) return;
            previousDefault = GraphicsSettings.defaultRenderPipeline;
            previousQuality = QualitySettings.renderPipeline;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            applied = true;
        }
        void OnDisable()
        {
            if (!applied) return;
            if (GraphicsSettings.defaultRenderPipeline == pipeline) GraphicsSettings.defaultRenderPipeline = previousDefault;
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousQuality;
            applied = false;
        }
    }
}
