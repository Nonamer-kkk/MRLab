using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MRLab.Joule.Rendering;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace MRLab.Joule.Editor
{
    public static class Assignment03HighQuality
    {
        const string OriginalScene = "Assets/_Project/Scenes/PC/01_PC_Main.unity";
        const string ScenePath = "Assets/_Project/Scenes/PC/03_Rendering_Submission.unity";
        const string AssetRoot = "Assets/_Project/Art/Assignment03";
        const string Settings = "Assets/_Project/Settings/Assignment03";
        const string Output = "Docs/Assignment03";
        const string Prefix = "24371119容易为";
        const int Width = 3840, Height = 2160;
        static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        static LectureBloomFeature bloom;
        static UniversalRenderPipelineAsset pipeline;
        static UniversalRendererData rendererData;
        static readonly List<ScriptableRendererFeature> extraFeatures = new List<ScriptableRendererFeature>();
        static Dictionary<string, Material> materials;

        [Serializable] public class ModelVisibility { public string model; public int pixels; public bool visible; }
        [Serializable] public class ViewAudit
        {
            public string view; public int models, visibleModels; public float visiblePercent;
            public int meshParts, visibleMeshParts; public float meshPartPercent;
            public Vector3 cameraPosition, cameraTarget; public float fieldOfView;
            public ModelVisibility[] perModel;
        }
        [Serializable] public class Report
        {
            public string generatedUtc, unity, urp, gpu, scene, sourceScene, sourceSceneSha256, aiModelSha256;
            public int width, height, cascades, shadowAtlas, gaussianTaps, bloomLevels, customBloomPassExecutions;
            public float shadowDistance, threshold, knee, intensity, scatter;
            public Vector3 cascadeSplits;
            public string visibilityMethod, scope, csmImplementation, bloomImplementation;
            public ViewAudit[] views;
        }

        [MenuItem("MRLab/Assignment 3/Build High Quality Scene and Render")]
        public static void BuildAndRender()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BackupDraft();
            foreach (string directory in new[] { AssetRoot, AssetRoot + "/Materials", AssetRoot + "/Textures", Settings, Output, Output + "/Evidence" })
                Directory.CreateDirectory(directory);
            AssetDatabase.Refresh();
            string sourceHash = Hash(OriginalScene);
            RenderPipelineAsset oldDefault = GraphicsSettings.defaultRenderPipeline, oldQuality = QualitySettings.renderPipeline;
            try
            {
                BuildPipelineAssets();
                GraphicsSettings.defaultRenderPipeline = pipeline;
                QualitySettings.renderPipeline = pipeline;
                Scene scene = EditorSceneManager.OpenScene(OriginalScene, OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (MonoBehaviour component in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (!(component is TMP_Text) && !(component is Volume)) component.enabled = false;
                foreach (var name in new[] { "_UI", "_Interaction", "_Debug" })
                {
                    GameObject item = GameObject.Find(name); if (item != null) item.SetActive(false);
                }
                foreach (var effect in Object.FindObjectsOfType<ParticleSystem>())
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                // Inactive fault-only meshes are retained but are not counted as displayed models.
                var auxiliary = new GameObject("Assignment03_Rendering");
                var camera = Object.FindObjectOfType<Camera>();
                camera.rect = new Rect(0, 0, 1, 1); camera.aspect = 16f / 9f;
                camera.nearClipPlane = .05f; camera.farClipPlane = 35; camera.allowHDR = true; camera.allowMSAA = true;
                var cameraData = camera.GetUniversalAdditionalCameraData();
                cameraData.renderPostProcessing = true; cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
                cameraData.dithering = true;
                cameraData.SetRenderer(0);
                FixLabels(scene);
                AddGeneratedExtinguisher(auxiliary.transform);
                var targets = ModelingTargets(scene);
                SetCamera(camera, 0);
                RenderWhiteModel(camera, targets);
                CreateSurfaceAssets(scene);
                ConfigureLighting(scene, auxiliary.transform);
                ConfigureVolume(scene);
                AddReflectionProbe(auxiliary.transform);
                SetCamera(camera, 0);
                var scope = auxiliary.AddComponent<Assignment03PipelineScope>();
                scope.enabled = false; scope.pipeline = pipeline; scope.enabled = true;
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();

                var audits = new List<ViewAudit>();
                for (int index = 0; index < 2; index++)
                {
                    SetCamera(camera, index);
                    Object.DestroyImmediate(Render(camera, Path.Combine(Output, Prefix + "-05-渲染结果0" + (index + 1) + ".png")));
                    audits.Add(AuditVisibility(camera, targets, index));
                }
                SetCamera(camera, 0);
                RenderAblations(camera);
                SetCamera(camera, 0);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                var report = new Report
                {
                    generatedUtc = DateTime.UtcNow.ToString("o"), unity = Application.unityVersion, urp = "14.0.12",
                    gpu = SystemInfo.graphicsDeviceName, scene = ScenePath, sourceScene = OriginalScene, sourceSceneSha256 = sourceHash,
                    aiModelSha256 = Hash("Assets/_Project/Art/Models/HunyuanGenerated/AI_FireExtinguisher.obj"),
                    width = Width, height = Height, cascades = 4, shadowAtlas = 4096, shadowDistance = pipeline.shadowDistance,
                    cascadeSplits = pipeline.cascade4Split, gaussianTaps = 9, bloomLevels = LectureBloomFeature.LastPyramidLevels,
                    threshold = bloom.threshold, knee = bloom.knee, intensity = bloom.intensity, scatter = bloom.scatter,
                    customBloomPassExecutions = LectureBloomFeature.ExecutedPasses,
                    visibilityMethod = "Opaque per-mesh RGB ID render with real depth test; visible if >=8 pixels at 3840x2160. Original transparent acrylic is opaque in ID audit (conservative). Labels/particles/cameras/lights excluded. Active source models are never removed for audit.",
                    scope = "Assignment 2 40-item modeling checklist (inactive fault-only items excluded), AI OBJ, plus the complete visible laboratory room assembly. Also reports independent mesh-part fraction.",
                    csmImplementation = "URP 14 MainLightShadowCasterPass: 4 cascades, 2x2 depth atlas, split culling spheres, URP PCF soft-shadow sampling. Configured integration, not claimed as handwritten CSM.",
                    bloomImplementation = "Custom LectureBloomFeature + LectureBloom.shader: soft knee prefilter, 9-tap H/V Gaussian, six downsample levels, additive reconstruction before ACES. URP built-in Bloom disabled.",
                    views = audits.ToArray()
                };
                File.WriteAllText(Output + "/Evidence/RenderingAudit.json", JsonUtility.ToJson(report, true));
                WriteDescription(report);
                if (Hash(OriginalScene) != sourceHash) throw new InvalidOperationException("Original PC scene changed during export.");
                if (audits.Any(a => a.visiblePercent < 75)) throw new InvalidOperationException("Model visibility is below 75%; adjust camera and re-render.");
                if (report.customBloomPassExecutions < 1) throw new InvalidOperationException("Custom Bloom pass did not execute.");
                Debug.Log("ASSIGNMENT03_HIGH_QUALITY_SUCCESS " + JsonUtility.ToJson(report));
            }
            finally
            {
                GraphicsSettings.defaultRenderPipeline = oldDefault;
                QualitySettings.renderPipeline = oldQuality;
                AssetDatabase.SaveAssets();
            }
        }

        static void BuildPipelineAssets()
        {
            rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(Settings + "/SubmissionRenderer.asset");
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, Settings + "/SubmissionRenderer.asset");
            }
            ResourceReloader.TryReloadAllNullIn(rendererData, "Packages/com.unity.render-pipelines.universal");
            rendererData.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
            bloom = rendererData.rendererFeatures.OfType<LectureBloomFeature>().FirstOrDefault();
            if (bloom == null)
            {
                bloom = ScriptableObject.CreateInstance<LectureBloomFeature>(); bloom.name = "Lecture04_CustomGaussianBloom";
                AssetDatabase.AddObjectToAsset(bloom, rendererData); rendererData.rendererFeatures.Add(bloom);
            }
            bloom.shader = Shader.Find("Hidden/MRLab/LectureBloom");
            bloom.threshold = 1.15f; bloom.knee = .5f; bloom.intensity = .32f; bloom.scatter = .65f; bloom.iterations = 6; bloom.debugMode = 0;
            bloom.SetActive(true); bloom.Create();
            // A supporting contact-shadow effect; the claimed lecture algorithms remain CSM and custom Bloom.
            extraFeatures.Clear();
            var ao = rendererData.rendererFeatures.FirstOrDefault(f => f.GetType().Name == "ScreenSpaceAmbientOcclusion");
            if (ao == null)
            {
                var type = typeof(UniversalRendererData).Assembly.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
                if (type != null)
                {
                    ao = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type); ao.name = "Supporting_ContactOcclusion";
                    AssetDatabase.AddObjectToAsset(ao, rendererData); rendererData.rendererFeatures.Insert(0, ao);
                }
            }
            if (ao != null)
            {
                var settings = new SerializedObject(ao); var p = settings.FindProperty("m_Settings");
                p.FindPropertyRelative("AOMethod").enumValueIndex = 1;
                p.FindPropertyRelative("Intensity").floatValue = .65f;
                p.FindPropertyRelative("Radius").floatValue = .12f;
                p.FindPropertyRelative("DirectLightingStrength").floatValue = .1f;
                p.FindPropertyRelative("Downsample").boolValue = false;
                settings.ApplyModifiedPropertiesWithoutUndo(); ao.SetActive(true); ao.Create(); extraFeatures.Add(ao);
            }
            var serializedRenderer = new SerializedObject(rendererData);
            var map = serializedRenderer.FindProperty("m_RendererFeatureMap");
            map.arraySize = rendererData.rendererFeatures.Count;
            for (int i = 0; i < map.arraySize; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rendererData.rendererFeatures[i], out string guid, out long id);
                map.GetArrayElementAtIndex(i).longValue = id;
            }
            serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
            pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Settings + "/SubmissionPipeline.asset");
            if (pipeline == null)
            {
                pipeline = Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Project/Settings/PC_UniversalRenderPipeline.asset"));
                pipeline.name = "SubmissionPipeline"; AssetDatabase.CreateAsset(pipeline, Settings + "/SubmissionPipeline.asset");
            }
            var serialized = new SerializedObject(pipeline);
            var renderers = serialized.FindProperty("m_RendererDataList"); renderers.arraySize = 1;
            renderers.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            serialized.FindProperty("m_AdditionalLightShadowsSupported").boolValue = true;
            serialized.FindProperty("m_ReflectionProbeBlending").boolValue = true;
            serialized.FindProperty("m_ReflectionProbeBoxProjection").boolValue = true;
            serialized.FindProperty("m_SoftShadowQuality").intValue = 3;
            serialized.FindProperty("m_MainLightShadowmapResolution").intValue = 4096;
            serialized.FindProperty("m_Cascade4Split").vector3Value = new Vector3(.22f,.42f,.68f);
            serialized.FindProperty("m_SoftShadowsSupported").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            pipeline.supportsHDR = true; pipeline.msaaSampleCount = 4; pipeline.renderScale = 1;
            pipeline.supportsCameraDepthTexture = true; pipeline.supportsCameraOpaqueTexture = true;
            pipeline.shadowDistance = 15;
            pipeline.shadowCascadeCount = 4;
            pipeline.cascadeBorder = .12f; pipeline.shadowDepthBias = .25f; pipeline.shadowNormalBias = .2f;
            pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
            EditorUtility.SetDirty(rendererData); EditorUtility.SetDirty(bloom); EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        static void FixLabels(Scene scene)
        {
            foreach (var text in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)))
            {
                // This label overlapped the supply because its transform was a runtime annotation.
                if (text.name == "SelectedFaultLabel" || text.text.Contains("红外测温") || text.name == "HotspotLabel")
                    text.gameObject.SetActive(false);
            }
        }

        static void AddGeneratedExtinguisher(Transform parent)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/HunyuanGenerated/AI_FireExtinguisher.obj");
            if (source == null) throw new FileNotFoundException("Assignment 2 AI OBJ missing.");
            var holder = new GameObject("Assignment02_Hunyuan_FireExtinguisher"); holder.transform.SetParent(parent);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(source); model.transform.SetParent(holder.transform, false);
            Bounds bounds = model.GetComponentsInChildren<Renderer>().First().bounds;
            foreach (var r in model.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            float scale = .55f / bounds.size.y;
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            holder.transform.position = new Vector3(-2.1f, .025f, .25f);
            holder.transform.rotation = Quaternion.Euler(0, 10, 0);
            var mat = NewLit("AI_Extinguisher_Enamel", new Color(.48f,.018f,.014f), .12f, .38f);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;
            Debug.Log("ASSIGNMENT03_AI_MODEL " + bounds + " scale=" + scale);
        }

        static Transform[] ModelingTargets(Scene scene)
        {
            var result = new List<Transform>();
            var env = scene.GetRootGameObjects().First(g => g.name == "_Environment").transform;
            result.Add(env.Find("LaboratoryBench")); result.Add(env.Find("LaboratoryRoom"));
            var experiment = scene.GetRootGameObjects().First(g => g.name == "_Experiment").transform;
            var rig = experiment.Find("Devices/ExperimentBench");
            foreach (Transform t in rig) if (t.GetComponentsInChildren<MeshFilter>(true).Any(f => f.sharedMesh != null && f.sharedMesh.name.StartsWith("Lab_"))) result.Add(t);
            foreach (Transform t in experiment.Find("Interactables")) if (t.GetComponentsInChildren<MeshFilter>(true).Length > 0) result.Add(t);
            result.Add(GameObject.Find("Assignment02_Hunyuan_FireExtinguisher").transform);
            return result.Where(t => t != null && t.gameObject.activeInHierarchy && t.GetComponentsInChildren<MeshRenderer>().Any(r => r.enabled && r.GetComponent<TMP_Text>() == null)).ToArray();
        }

        static Material NewLit(string name, Color color, float metallic, float smoothness)
        {
            string path = AssetRoot + "/Materials/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",smoothness);
            EditorUtility.SetDirty(mat); return mat;
        }

        static void CreateSurfaceAssets(Scene scene)
        {
            materials = new Dictionary<string, Material>();
            var normal = SurfaceTexture("MicroNormal", 2); var floor = SurfaceTexture("FloorTerrazzo",0); var paint = SurfaceTexture("PaintGrain",1);
            foreach (var r in scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if (r.GetComponent<TMP_Text>() != null || r.GetComponent<MeshFilter>() == null) continue;
                var slots = r.sharedMaterials;
                for (int i=0; i<slots.Length; i++)
                {
                    var old = slots[i]; if(old == null || !old.shader.name.Contains("Universal Render Pipeline/Lit")) continue;
                    if(!materials.TryGetValue(old.name, out var mat))
                    {
                        string path=AssetRoot+"/Materials/"+old.name+"_Render.mat";
                        mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(mat==null){mat=Object.Instantiate(old);mat.name=old.name+"_Render";AssetDatabase.CreateAsset(mat,path);} else mat.CopyPropertiesFromMaterial(old);
                        if(!old.name.Contains("Acrylic") && !old.name.Contains("Window") && !old.name.Contains("Screen") && !old.name.Contains("Glass") && !old.name.Contains("LED"))
                        {
                            mat.SetTexture("_BaseMap",old.name.Contains("Floor")?floor:paint);
                            mat.SetTexture("_BumpMap",normal);mat.EnableKeyword("_NORMALMAP");mat.SetFloat("_BumpScale",.13f);
                        }
                        if(old.name.Contains("Floor")){mat.SetColor("_BaseColor",new Color(.27f,.33f,.34f));mat.SetFloat("_Smoothness",.33f);mat.SetTextureScale("_BaseMap",new Vector2(8,8));}
                        if(old.name.Contains("Wall")){mat.SetColor("_BaseColor",new Color(.72f,.74f,.7f));mat.SetFloat("_Smoothness",.13f);}
                        if(old.name.Contains("Steel")){mat.SetColor("_BaseColor",new Color(.57f,.62f,.63f));mat.SetFloat("_Metallic",.82f);mat.SetFloat("_Smoothness",.46f);}
                        if(old.name.Contains("CeilingLight")){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.7f,.88f,1f)*6);}
                        if(old.name.Contains("Screen")){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.02f,.65f,.44f)*1.6f);}
                        if(old.name.Contains("Window")){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.37f,.62f,.8f)*1.6f);}
                        if(old.name.Contains("Acrylic")){mat.SetColor("_BaseColor",new Color(.45f,.7f,.73f,.1f));mat.SetFloat("_Smoothness",.85f);}
                        EditorUtility.SetDirty(mat); materials[old.name]=mat;
                    }
                    slots[i]=mat;
                }
                r.sharedMaterials=slots;
                r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
            }
        }

        static Texture2D SurfaceTexture(string name, int kind)
        {
            string path=AssetRoot+"/Textures/"+name+".png";
            var pixels=new Color[512*512];
            for(int y=0;y<512;y++)for(int x=0;x<512;x++)
            {
                float n=Mathf.PerlinNoise(x*.13f+7,y*.13f+11);
                float fine=Mathf.PerlinNoise(x*.75f,y*.75f);
                if(kind==2){float a=Mathf.Sin(x*.31f)*.008f,b=Mathf.Cos(y*.37f)*.008f;pixels[y*512+x]=new Color(.5f+a,.5f+b,1,1);}
                else {float value=kind==0?.77f+.16f*n+.04f*fine:.94f+.045f*n;pixels[y*512+x]=new Color(value,value,value,1);}
            }
            var texture=new Texture2D(512,512,TextureFormat.RGB24,false);texture.SetPixels(pixels);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=kind==2?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=kind!=2;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void ConfigureLighting(Scene scene,Transform parent)
        {
            foreach(var light in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)))light.enabled=false;
            var key=AddLight(parent,"Sun_CSM_Key",LightType.Directional,new Vector3(0,3,0),new Color(1,.87f,.7f),1.65f,10);
            key.transform.rotation=Quaternion.Euler(58,-24,0);key.shadows=LightShadows.Soft;key.shadowStrength=.72f;
            key.shadowBias=.03f;key.shadowNormalBias=.15f;
            var fill=AddLight(parent,"Cool_Window_Fill",LightType.Directional,new Vector3(-3,2,0),new Color(.62f,.78f,1f),.42f,10);
            fill.transform.rotation=Quaternion.Euler(30,118,0);
            var task=AddLight(parent,"Ceiling_Task_Light",LightType.Point,new Vector3(-.2f,2.7f,.15f),new Color(.75f,.88f,1),5.5f,5);
            var rim=AddLight(parent,"Rear_Warm_Light",LightType.Point,new Vector3(-1.3f,2.55f,1.85f),new Color(1,.85f,.65f),3,4);
            task.shadows=LightShadows.Soft;rim.shadows=LightShadows.None;
            RenderSettings.ambientMode=AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.34f,.42f,.49f);RenderSettings.ambientEquatorColor=new Color(.2f,.25f,.28f);RenderSettings.ambientGroundColor=new Color(.12f,.14f,.15f);
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;RenderSettings.fogColor=new Color(.35f,.43f,.47f);
            // A wall extension closes the former black void without changing any Assignment 2 meshes.
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="RightWall_EnvironmentExtension";wall.transform.SetParent(parent);
            wall.transform.position=new Vector3(4.25f,1.65f,.6f);wall.transform.localScale=new Vector3(.16f,3.3f,5);
            wall.GetComponent<Renderer>().sharedMaterial=materials["Lab_Wall_MAT"];
            var ceiling=GameObject.CreatePrimitive(PrimitiveType.Cube);ceiling.name="Ceiling_EnvironmentExtension";ceiling.transform.SetParent(parent);
            ceiling.transform.position=new Vector3(0,3.4f,-.4f);ceiling.transform.localScale=new Vector3(8.6f,.12f,8);
            ceiling.GetComponent<Renderer>().sharedMaterial=materials["Lab_Wall_MAT"];
            var seamMaterial=NewLit("Floor_Grout",new Color(.15f,.19f,.20f),0,.08f);
            foreach(var r in Object.FindObjectsOfType<MeshRenderer>())if(r.name.StartsWith("FloorSeam"))
            {r.shadowCastingMode=ShadowCastingMode.Off;r.sharedMaterial=seamMaterial;}
        }
        static Light AddLight(Transform parent,string name,LightType type,Vector3 position,Color color,float intensity,float range)
        {
            var item=new GameObject(name);item.transform.SetParent(parent);item.transform.position=position;
            var light=item.AddComponent<Light>();light.type=type;light.color=color;light.intensity=intensity;light.range=range;
            light.GetUniversalAdditionalLightData().usePipelineSettings=true;return light;
        }
        static void ConfigureVolume(Scene scene)
        {
            foreach(var volume in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Volume>(true)))
            {
                var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Settings+"/SubmissionVolume.asset");
                if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Settings+"/SubmissionVolume.asset");}
                if(!profile.TryGet(out Tonemapping tone)){tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.ACES);
                if(!profile.TryGet(out ColorAdjustments color)){color=profile.Add<ColorAdjustments>(true);AssetDatabase.AddObjectToAsset(color,profile);}color.postExposure.Override(.35f);color.contrast.Override(5);color.saturation.Override(-3);
                if(profile.TryGet(out Bloom builtIn))builtIn.active=false;
                volume.sharedProfile=profile;volume.isGlobal=true;volume.weight=1;EditorUtility.SetDirty(profile);
            }
        }
        static void AddReflectionProbe(Transform parent)
        {
            var item=new GameObject("Room_ReflectionProbe");item.transform.SetParent(parent);item.transform.position=new Vector3(0,1.3f,.6f);
            var probe=item.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Custom;probe.resolution=256;probe.size=new Vector3(8,4,8);probe.boxProjection=true;probe.intensity=.8f;
            probe.backgroundColor=new Color(.25f,.32f,.36f);probe.clearFlags=ReflectionProbeClearFlags.SolidColor;
            string path=AssetRoot+"/RoomReflection.exr";
            if(!Lightmapping.BakeReflectionProbe(probe,path))throw new InvalidOperationException("Reflection probe bake failed.");
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>(path);
        }

        static void SetCamera(Camera camera,int index)
        {
            Vector3 pos=index==0?new Vector3(-2.85f,2.75f,-3.95f):new Vector3(2.9f,2.6f,-3.8f);
            Vector3 target=index==0?new Vector3(.05f,1.15f,.6f):new Vector3(-.1f,1.05f,.6f);
            camera.transform.SetPositionAndRotation(pos,Quaternion.LookRotation(target-pos));camera.fieldOfView=46;
        }
        static Texture2D Render(Camera camera,string path, bool linear=false)
        {
            // RenderRequest inherits its HDR format and sample count from the destination.
            // Keep highlights above 1 through Bloom/ACES, then encode the resolved final image to sRGB.
            var texture=new RenderTexture(Width,Height,24,linear?RenderTextureFormat.ARGB32:RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear)
                {antiAliasing=linear?1:4};texture.Create();
            RenderTexture readback=null;
            var previous=RenderTexture.active;
            try
            {
                // Warm-up frame initializes volume blending, SSAO and pipeline resources after toggles.
                for(int i=0;i<2;i++)RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=texture});
                if(!linear)
                {
                    readback=new RenderTexture(Width,Height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                    readback.Create();Graphics.Blit(texture,readback);
                }
                RenderTexture.active=linear?texture:readback;
                var image=new Texture2D(Width,Height,TextureFormat.RGB24,false,linear);image.ReadPixels(new Rect(0,0,Width,Height),0,0);image.Apply(false);
                File.WriteAllBytes(path,image.EncodeToPNG());Debug.Log("ASSIGNMENT03_IMAGE "+path);return image;
            }
            finally
            {
                RenderTexture.active=previous;texture.Release();Object.DestroyImmediate(texture);
                if(readback!=null){readback.Release();Object.DestroyImmediate(readback);}
            }
        }
        static void RenderWhiteModel(Camera camera,Transform[] targets)
        {
            var renderers=Object.FindObjectsOfType<MeshRenderer>().Where(r=>r.GetComponent<TMP_Text>()==null).ToArray();
            var saved=renderers.Select(r=>r.sharedMaterials).ToArray();var white=new Material(Shader.Find("Universal Render Pipeline/Lit"));white.SetColor("_BaseColor",new Color(.65f,.65f,.65f));white.SetFloat("_Smoothness",.12f);
            bool active=bloom.isActive;bloom.SetActive(false);foreach(var f in extraFeatures)f.SetActive(false);
            try{for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=saved[i].Select(m=>white).ToArray();Object.DestroyImmediate(Render(camera,Output+"/"+Prefix+"-05-原始场景.png"));}
            finally{for(int i=0;i<renderers.Length;i++)renderers[i].sharedMaterials=saved[i];Object.DestroyImmediate(white);bloom.SetActive(active);foreach(var f in extraFeatures)f.SetActive(true);}
        }

        static ViewAudit AuditVisibility(Camera camera,Transform[] targets,int index)
        {
            var all=Object.FindObjectsOfType<MeshRenderer>().Where(r=>r.enabled && r.GetComponent<MeshFilter>()!=null && r.GetComponent<TMP_Text>()==null).ToArray();
            var saved=all.Select(r=>r.sharedMaterials).ToArray();var mats=new List<Material>();var ids=new Dictionary<MeshRenderer,int>();
            var texts=Object.FindObjectsOfType<TMP_Text>().Select(t=>t.GetComponent<Renderer>()).Where(r=>r!=null).ToArray();
            var data=camera.GetUniversalAdditionalCameraData();bool post=data.renderPostProcessing, fog=RenderSettings.fog, msaa=camera.allowMSAA;
            var aa=data.antialiasing;var clear=camera.backgroundColor;bool active=bloom.isActive;
            try
            {
                data.renderPostProcessing=false;data.antialiasing=AntialiasingMode.None;camera.allowMSAA=false;camera.backgroundColor=Color.black;RenderSettings.fog=false;
                bloom.SetActive(false);foreach(var f in extraFeatures)f.SetActive(false);foreach(var r in texts)r.enabled=false;
                for(int i=0;i<all.Length;i++)
                {
                    int id=i+1;ids[all[i]]=id;var mat=new Material(Shader.Find("Hidden/MRLab/ModelVisibility"));
                    mat.SetVector("_ObjectId",new Vector4((id&255)/255f,((id>>8)&255)/255f,0,1));mats.Add(mat);
                    all[i].sharedMaterials=saved[i].Select(m=>mat).ToArray();
                }
                var image=Render(camera,Output+"/Evidence/View0"+(index+1)+"_DepthTestedIDs.png",true);
                int[] counts=new int[all.Length+1];foreach(var pixel in image.GetPixels32()){int id=pixel.r+(pixel.g<<8);if(id>0 && id<counts.Length)counts[id]++;}Object.DestroyImmediate(image);
                var modelRecords=targets.Select(t=>new ModelVisibility{model=HierarchyPath(t),pixels=t.GetComponentsInChildren<MeshRenderer>().Where(r=>ids.ContainsKey(r)).Sum(r=>counts[ids[r]])}).ToArray();
                foreach(var r in modelRecords)r.visible=r.pixels>=8;
                int visible=modelRecords.Count(m=>m.visible),parts=counts.Skip(1).Count(n=>n>=8);
                var audit=new ViewAudit{view="View0"+(index+1),models=modelRecords.Length,visibleModels=visible,visiblePercent=100f*visible/modelRecords.Length,
                    meshParts=all.Length,visibleMeshParts=parts,meshPartPercent=100f*parts/all.Length,cameraPosition=camera.transform.position,
                    cameraTarget=index==0?new Vector3(.05f,1.15f,.6f):new Vector3(-.1f,1.05f,.6f),fieldOfView=camera.fieldOfView,perModel=modelRecords};
                Debug.Log("ASSIGNMENT03_VISIBILITY "+JsonUtility.ToJson(audit));return audit;
            }
            finally
            {
                for(int i=0;i<all.Length;i++)all[i].sharedMaterials=saved[i];foreach(var mat in mats)Object.DestroyImmediate(mat);
                foreach(var r in texts)r.enabled=true;data.renderPostProcessing=post;data.antialiasing=aa;camera.allowMSAA=msaa;camera.backgroundColor=clear;RenderSettings.fog=fog;
                bloom.SetActive(active);foreach(var f in extraFeatures)f.SetActive(true);
            }
        }
        static void RenderAblations(Camera camera)
        {
            bloom.SetActive(false);Object.DestroyImmediate(Render(camera,Output+"/Evidence/01_Bloom_Off.png"));
            bloom.SetActive(true);bloom.debugMode=1;Object.DestroyImmediate(Render(camera,Output+"/Evidence/02_Bloom_Only.png"));
            bloom.debugMode=2;Object.DestroyImmediate(Render(camera,Output+"/Evidence/03_Bright_Prefilter.png"));
            bloom.debugMode=3;Object.DestroyImmediate(Render(camera,Output+"/Evidence/04_CSM_DepthAtlas.png"));
            bloom.debugMode=4;Object.DestroyImmediate(Render(camera,Output+"/Evidence/05_CSM_CascadeCoverage.png"));
            bloom.debugMode=0;pipeline.shadowCascadeCount=1;SetSoftShadows(false);
            Object.DestroyImmediate(Render(camera,Output+"/Evidence/06_Single_Hard_Shadow.png"));
            pipeline.shadowCascadeCount=4;SetSoftShadows(true);
            Object.DestroyImmediate(Render(camera,Output+"/Evidence/07_Final_CSM4_PCF_Bloom.png"));
            EditorUtility.SetDirty(pipeline);EditorUtility.SetDirty(bloom);
        }
        static string HierarchyPath(Transform transform){return transform.parent==null?transform.name:HierarchyPath(transform.parent)+"/"+transform.name;}
        static void SetSoftShadows(bool enabled){var so=new SerializedObject(pipeline);so.FindProperty("m_SoftShadowsSupported").boolValue=enabled;so.ApplyModifiedPropertiesWithoutUndo();}
        static string Hash(string path){using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-","").ToLowerInvariant();}
        static void BackupDraft()
        {
            if(!Directory.Exists(Output))return;
            string path="SourceAssets/Assignment03/PreviousDraft_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(path);
            foreach(string file in Directory.GetFiles(Output))File.Copy(file,Path.Combine(path,Path.GetFileName(file)));
            string zip="Docs/"+Prefix+"-00-第3次小作业.zip";if(File.Exists(zip))File.Copy(zip,Path.Combine(path,Path.GetFileName(zip)));
        }
        static void WriteDescription(Report r)
        {
            string text="小作业3：三维渲染——焦耳定律实验室\n容易为 / 24371119\n\n"+
                "基础：第二次作业的优化网格和 Hunyuan3D 灭火器 OBJ。复用完整 PC 实验室；专用场景为 "+ScenePath+"。原 PC 场景哈希已验证未改变。\n"+
                "软件：Unity "+r.unity+"，URP 14.0.12，Linear，HDR，4x MSAA + SMAA High，ACES。两视角与白模均为 3840×2160（真实 Unity 渲染，无图像生成/放大）。\n\n"+
                "一、课件高级算法1：级联阴影贴图 CSM + PCF（Lecture04 第42–59页）\n"+
                "将相机视域分为4段，按各段剔除球计算光源空间矩阵，将深度渲染到2×2图集。着色时根据世界位置选择级联，转换至对应tile，对深度比较进行PCF过滤，减少近景阴影锯齿。\n"+
                "实现边界：使用并配置 URP 14 MainLightShadowCasterPass 的 CSM/PCF；未声称该底层由本人重写。可查看 Packages/com.unity.render-pipelines.universal/Runtime/Passes/MainLightShadowCasterPass.cs 与 ShaderLibrary/Shadows.hlsl。\n"+
                "参数：4096×4096图集；4 cascades；split=(0.22,0.42,0.68)，maxDistance=15m；cascadeBorder=0.12；soft shadows high；pipeline depth/normal bias=0.25/0.20。\n\n"+
                "二、课件高级算法2：自主编写的 SRP Gaussian Bloom（第60–85页）\n"+
                "自定义 ScriptableRendererFeature 在 BeforeRenderingPostProcessing 注入。先取 max(R,G,B) 做 soft-knee 阈值提取；用九抽头水平高斯降采样、九抽头垂直高斯模糊构建六层纹理金字塔；从小到大上采样相加，最后乘intensity叠加到HDR原图，再做ACES。\n"+
                "高斯权重与第71/73页一致：[0.01621622,0.05405405,0.12162162,0.19459459,0.22702703,0.19459459,0.12162162,0.05405405,0.01621622]。\n"+
                "参数：线性亮度threshold="+r.threshold.ToString(Invariant)+"；knee="+r.knee.ToString(Invariant)+"；intensity="+r.intensity.ToString(Invariant)+"；scatter="+r.scatter.ToString(Invariant)+"；6层；RGBA16F中间纹理。scatter用于抑制多层直接相加的过曝。URP内置Bloom禁用，避免双重泛光。\n"+
                "源码：Assets/_Project/Scripts/Rendering/LectureBloomFeature.cs 与 Assets/_Project/Art/Shaders/LectureBloom.shader。支持亮部、Bloom-only、阴影图集和级联覆盖调试。\n\n"+
                "三、常见功能（至少3种，均实际启用）\n"+
                "1. 表面贴图：固定程序生成的地面颗粒、涂层微纹理和法线贴图；PNG与材质随工程保存，不依赖外链。\n"+
                "2. 多光源：暖方向主光1.65、冷方向补光0.42、顶灯点光5.5、后方点光3.0。主光与顶灯有阴影。\n"+
                "3. 雾：Exp2，density=0.012，轻度空气透视。\n"+
                "4. 反射探针：256px六面体，盒投影，intensity=0.8，保存为工程EXR，金属及亚克力使用。\n"+
                "5. ACES、SMAA High、辅助SSAO（强度0.65，半径0.12m），增强接触层次并保留细节。\n\n"+
                "四、视角与展示率\n"+string.Join("\n",r.views.Select(v=>v.view+"："+v.visibleModels+"/"+v.models+"个模型可见（"+v.visiblePercent.ToString("F2",Invariant)+"%），网格部件="+v.visibleMeshParts+"/"+v.meshParts+"。"))+"\n"+
                "模型统计沿用第二次作业40项清单，以未激活的故障状态物体为排除项；加入生成OBJ和房间组。ID离屏渲染用真实深度测试，至少8像素才记可见，透明保护板在检查时按不透明处理。逐项结果和方法见 Evidence/RenderingAudit.json；不把视锥相交冒充无阻挡可见。\n\n"+
                "五、证据\n原始场景图为相同模型的灰材质白模，关闭自定义Bloom/SSAO。\nEvidence含Bloom开关、亮部提取、Bloom-only、CSM图集、级联覆盖、单级硬阴影与四级PCF结果，以及两视角遮挡ID图。\n";
            File.WriteAllText(Output+"/"+Prefix+"-04-渲染描述.txt",text,new System.Text.UTF8Encoding(true));
        }
    }
}
