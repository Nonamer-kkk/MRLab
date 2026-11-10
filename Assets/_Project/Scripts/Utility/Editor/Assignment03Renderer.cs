using UnityEditor;

namespace MRLab.Joule.Editor
{
    /// <summary>Compatibility entry point for the Assignment 3 exporter.</summary>
    public static class Assignment03Renderer
    {
        [MenuItem("MRLab/Assignment 3/Render Submission Images")]
        public static void RenderAll()
        {
            Assignment03HighQuality.BuildAndRender();
        }
    }
}
