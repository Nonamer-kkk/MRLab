using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MRLab.Joule.Editor
{
    /// <summary>Creates the editable laboratory scene and reusable project-owned prefabs.</summary>
    public static class JouleSceneBuilder
    {
        private const string MainScene="Assets/_Project/Scenes/PC/01_PC_Main.unity";
        private const string FontPath="Assets/_Project/Fonts/NotoSansCJKsc-Regular SDF.asset";

        [MenuItem("MRLab/Joule Lab/Generate PC Laboratory")]
        public static void Generate()
        {
            MRLab.Editor.ProjectBootstrap.ValidateDocumentCompliance();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            if(font==null)throw new InvalidOperationException("Project Chinese font missing.");
            BakeChineseGlyphs(font);
            AssetDatabase.SaveAssets();
            Transform systems=Root("_Systems"), environment=Root("_Environment"),experimentRoot=Root("_Experiment");
            var manager=Child(systems,"ExperimentManager").gameObject.AddComponent<JouleExperimentManager>();
            Child(systems,"UIManager");
            Child(systems,"AudioManager").gameObject.AddComponent<JouleLabAudio>();
            var inputManager=Child(systems,"InputManager");
            inputManager.gameObject.AddComponent<EventSystem>();
            inputManager.gameObject.AddComponent<InputSystemUIInputModule>();
            var reset=Child(systems,"ResetManager").gameObject.AddComponent<JouleResetRelay>();reset.Experiment=manager;
            Transform devices=Child(experimentRoot,"Devices");Child(experimentRoot,"Materials");
            Transform interactables=Child(experimentRoot,"Interactables"),effects=Child(experimentRoot,"Effects");
            var visuals=LabVisualFactory.Create(environment,devices,interactables,effects,font);visuals.Bind(manager);
            Transform interaction=Root("_Interaction"),pc=Child(interaction,"PC"),cameraRoot=Root("_Camera");
            Camera camera=Child(cameraRoot,"Main Camera").gameObject.AddComponent<Camera>();camera.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.20f,.24f);
            camera.fieldOfView=39;camera.nearClipPlane=.05f;camera.farClipPlane=40;
            camera.rect=new Rect(.1825f,.3178f,.625f,.4444f);camera.allowHDR=true;camera.allowMSAA=true;
            camera.gameObject.AddComponent<AudioListener>();
            var cameraData=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=true;
            cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var controls=pc.gameObject.AddComponent<JoulePCInput>();controls.Bind(manager,camera);
            var sun=Child(environment,"KeyLight").gameObject.AddComponent<Light>();sun.type=LightType.Directional;
            sun.color=new Color(1f,.94f,.84f);sun.intensity=1.45f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(48,-25,0);
            var fill=Child(environment,"WindowFill").gameObject.AddComponent<Light>();fill.type=LightType.Directional;
            fill.intensity=.45f;fill.color=new Color(.62f,.83f,1f);fill.transform.rotation=Quaternion.Euler(26,116,0);fill.shadows=LightShadows.None;
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.70f,.77f);
            RenderSettings.ambientEquatorColor=new Color(.40f,.48f,.52f);RenderSettings.ambientGroundColor=new Color(.19f,.22f,.25f);
            RenderSettings.fog=false;
            var volume=Child(environment,"LabColorGrading").gameObject.AddComponent<Volume>();volume.isGlobal=true;
            const string profilePath="Assets/_Project/Settings/LabVolumeProfile.asset";
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
            Tonemapping tone; if(!profile.TryGet(out tone)){tone=profile.Add<Tonemapping>(true);AssetDatabase.AddObjectToAsset(tone,profile);}
            tone.mode.Override(TonemappingMode.ACES);
            Bloom bloom;if(!profile.TryGet(out bloom)){bloom=profile.Add<Bloom>(true);AssetDatabase.AddObjectToAsset(bloom,profile);}
            bloom.intensity.Override(.18f);bloom.threshold.Override(1.2f);volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
            Transform uiRoot=Root("_UI");
            GameObject ui=new GameObject("LaboratoryInterface",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster),typeof(JouleLabUI));
            ui.transform.SetParent(uiRoot,false);ui.GetComponent<JouleLabUI>().Font=font;
            Prefab(ui,"UI");
            Transform debug=Root("_Debug");debug.gameObject.SetActive(false);
            // Save independent devices and effects so they are reusable by future MR/CAVE scenes.
            foreach(Transform child in environment.Cast<Transform>().ToArray())
                if(child.name=="LaboratoryRoom"||child.name=="LaboratoryBench")Prefab(child.gameObject,"Environment");
            foreach(Transform child in interactables.Cast<Transform>().ToArray())Prefab(child.gameObject,"Interactables");
            foreach(Transform child in effects.Cast<Transform>().ToArray())Prefab(child.gameObject,"Effects");
            Transform rig=devices.Find("ExperimentBench");
            if(rig!=null)foreach(Transform child in rig.Cast<Transform>().ToArray())
                if(child.name=="DCPowerSupply"||child.name=="LoadResistor"||child.name=="TemperatureSensor"||child.name=="TestWireHolder")Prefab(child.gameObject,"Devices");

            PlayerSettings.companyName="MRLab";PlayerSettings.productName="JouleLab_E10";PlayerSettings.bundleVersion="1.1.0";
            PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone,1);
            PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=900;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;PlayerSettings.runInBackground=true;
            QualitySettings.vSyncCount=1;
            var pipeline=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if(pipeline!=null){pipeline.msaaSampleCount=4;pipeline.shadowDistance=15;pipeline.supportsCameraDepthTexture=true;EditorUtility.SetDirty(pipeline);}
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(MainScene,true)};
            EditorSceneManager.SaveScene(scene,MainScene);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Debug.Log("JOULE_SCENE_GENERATED: "+MainScene);
        }

        [MenuItem("MRLab/Joule Lab/Build PC")]
        public static void Build()
        {
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);BakeChineseGlyphs(font);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds/PC");
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{MainScene},locationPathName="Builds/PC/MRLab_E10_24371119.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            var s=report.summary;Debug.Log($"JOULE_BUILD: {s.result}; errors={s.totalErrors}; warnings={s.totalWarnings}; bytes={s.totalSize}; duration={s.totalTime}");
            if(s.result!=BuildResult.Succeeded)throw new InvalidOperationException("PC build failed: "+s.result);
        }
        public static void GenerateTestAndBuild()
        {
            Generate();JouleModelTests.Run();Build();
        }
        private static void BakeChineseGlyphs(TMP_FontAsset font)
        {
            if(font==null)throw new InvalidOperationException("Project Chinese font missing.");
            // A single persisted atlas avoids TMP 3.0 multi-atlas cache initialization
            // on a fresh batch-mode domain. Preserve the asset GUID and material.
            font.atlasPopulationMode=AtlasPopulationMode.Dynamic;
            font.isMultiAtlasTexturesEnabled=false;
            var serialized=new SerializedObject(font);
            serialized.FindProperty("m_AtlasWidth").intValue=4096;
            serialized.FindProperty("m_AtlasHeight").intValue=4096;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var atlasTexture=font.atlasTexture;
            if(atlasTexture==null)throw new InvalidOperationException("Chinese font atlas missing.");
            font.ClearFontAssetData();
            font.material.SetTexture(ShaderUtilities.ID_MainTex,atlasTexture);
            font.material.SetFloat(ShaderUtilities.ID_TextureWidth,4096);
            font.material.SetFloat(ShaderUtilities.ID_TextureHeight,4096);
            string all=string.Join("",Directory.GetFiles("Assets/_Project/Scripts","*.cs",SearchOption.AllDirectories).Select(File.ReadAllText));
            // TMP also requests ellipsis and a nonbreaking space internally.
            string chars=new string((all+"\u2026\u00a0").Where(c=>!char.IsControl(c)).Distinct().ToArray());
            string missing;font.TryAddCharacters(chars,out missing);
            if(!string.IsNullOrEmpty(missing))throw new InvalidOperationException("JOULE_FONT_MISSING_CHARACTERS: "+missing);
            font.atlasPopulationMode=AtlasPopulationMode.Static;
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach(var atlas in font.atlasTextures)if(atlas!=null)EditorUtility.SetDirty(atlas);
            Debug.Log("JOULE_FONT_BAKED: "+font.characterTable.Count+" characters; static 4096 atlas; project-owned Noto font.");
        }
        private static void Prefab(GameObject go,string category)
        {
            string dir="Assets/_Project/Prefabs/"+category;Directory.CreateDirectory(dir);AssetDatabase.Refresh();
            PrefabUtility.SaveAsPrefabAssetAndConnect(go,dir+"/"+go.name+".prefab",InteractionMode.AutomatedAction);
        }
        private static Transform Root(string name){return new GameObject(name).transform;}
        private static Transform Child(Transform parent,string name){var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}
    }
}
