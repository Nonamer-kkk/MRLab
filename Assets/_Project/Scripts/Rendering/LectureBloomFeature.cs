using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MRLab.Joule.Rendering
{
    /// <summary>Lecture04 pp.65–84: soft threshold, separable nine-tap Gaussian pyramid and additive reconstruction.</summary>
    public sealed class LectureBloomFeature : ScriptableRendererFeature
    {
        public Shader shader;
        [Range(1, 8)] public int iterations = 6;
        [Min(0)] public float threshold = 1.15f;
        [Range(0, 1)] public float knee = .5f;
        [Min(0)] public float intensity = .32f;
        [Range(0, 1)] public float scatter = .65f;
        [Tooltip("0 composite; 1 bloom only; 2 prefilter; 3 shadow atlas; 4 cascade coverage")]
        public int debugMode;
        public static int ExecutedPasses { get; private set; }
        public static int LastPyramidLevels { get; private set; }
        Material material;
        BloomPass pass;

        public override void Create()
        {
            pass?.Release();
            CoreUtils.Destroy(material);
            if (shader == null) shader = Shader.Find("Hidden/MRLab/LectureBloom");
            if (shader != null) material = CoreUtils.CreateEngineMaterial(shader);
            pass = new BloomPass(this, material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (material != null && data.cameraData.cameraType == CameraType.Game)
                renderer.EnqueuePass(pass);
        }
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData data)
        {
            if (data.cameraData.cameraType == CameraType.Game) pass.source = renderer.cameraColorTargetHandle;
        }
        protected override void Dispose(bool disposing)
        {
            pass?.Release();
            CoreUtils.Destroy(material);
        }

        sealed class BloomPass : ScriptableRenderPass
        {
            readonly LectureBloomFeature owner;
            readonly Material material;
            public RTHandle source;
            RTHandle prefilter, composite;
            readonly RTHandle[] horizontal = new RTHandle[8], down = new RTHandle[8], up = new RTHandle[8];
            int levels;
            public BloomPass(LectureBloomFeature owner, Material material)
            {
                this.owner = owner; this.material = material;
                ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
            }
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data)
            {
                var desc = data.cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0; desc.msaaSamples = 1;
                desc.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
                desc.useMipMap = desc.autoGenerateMips = false;
                RenderingUtils.ReAllocateIfNeeded(ref composite, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "LectureBloom_Composite");
                desc.width = Mathf.Max(1, desc.width / 2); desc.height = Mathf.Max(1, desc.height / 2);
                RenderingUtils.ReAllocateIfNeeded(ref prefilter, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "LectureBloom_Prefilter");
                levels = Mathf.Clamp(owner.iterations, 1, 8);
                for (int i = 0; i < levels; i++)
                {
                    desc.width = Mathf.Max(1, desc.width / 2); desc.height = Mathf.Max(1, desc.height / 2);
                    RenderingUtils.ReAllocateIfNeeded(ref horizontal[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "LectureBloom_H" + i);
                    RenderingUtils.ReAllocateIfNeeded(ref down[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "LectureBloom_V" + i);
                    RenderingUtils.ReAllocateIfNeeded(ref up[i], desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "LectureBloom_Up" + i);
                }
            }
            static void Draw(CommandBuffer cmd, RTHandle from, RTHandle to, Material mat, int shaderPass)
            {
                cmd.SetGlobalVector("_LectureTexelSize", new Vector4(1f / from.rt.width, 1f / from.rt.height, from.rt.width, from.rt.height));
                Blitter.BlitCameraTexture(cmd, from, to, mat, shaderPass);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData data)
            {
                if (source == null || material == null) return;
                CommandBuffer cmd = CommandBufferPool.Get("Lecture04 Gaussian pyramid Bloom");
                float t = owner.threshold; float k = t * owner.knee;
                cmd.SetGlobalVector("_LectureThreshold", new Vector4(t, t - k, 2 * k, .25f / (k + .00001f)));
                cmd.SetGlobalFloat("_LectureScatter", owner.scatter);
                cmd.SetGlobalFloat("_LectureIntensity", owner.intensity);
                Draw(cmd, source, prefilter, material, 0);
                RTHandle current = prefilter;
                for (int i = 0; i < levels; i++)
                {
                    Draw(cmd, current, horizontal[i], material, 1);
                    Draw(cmd, horizontal[i], down[i], material, 2);
                    current = down[i];
                }
                for (int i = levels - 2; i >= 0; i--)
                {
                    cmd.SetGlobalTexture("_LectureLow", current.nameID);
                    Draw(cmd, down[i], up[i], material, 3);
                    current = up[i];
                }
                cmd.SetGlobalTexture("_LectureLow", current.nameID);
                cmd.SetGlobalInt("_LectureDebug", owner.debugMode);
                if (owner.debugMode == 2) Blitter.BlitCameraTexture(cmd, prefilter, composite);
                else Draw(cmd, source, composite, material, 4);
                Blitter.BlitCameraTexture(cmd, composite, source);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
                ExecutedPasses++; LastPyramidLevels = levels;
            }
            public void Release()
            {
                prefilter?.Release(); composite?.Release();
                for (int i = 0; i < 8; i++) { horizontal[i]?.Release(); down[i]?.Release(); up[i]?.Release(); }
            }
        }
    }
}
