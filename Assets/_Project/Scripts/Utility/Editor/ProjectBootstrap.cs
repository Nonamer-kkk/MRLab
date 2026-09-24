using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;

namespace MRLab.Editor
{
    public static class ProjectBootstrap
    {
        private const string MainScenePath = "Assets/_Project/Scenes/PC/01_PC_Main.unity";
        private const string SettingsDirectory = "Assets/_Project/Settings";
        private const string RendererPath = SettingsDirectory + "/PC_UniversalRenderer.asset";
        private const string PipelinePath = SettingsDirectory + "/PC_UniversalRenderPipeline.asset";
        private const string ChineseFontPath = "Assets/_Project/Fonts/NotoSansCJKsc-Regular.otf";
        private const string ChineseFontAssetPath = "Assets/_Project/Fonts/NotoSansCJKsc-Regular SDF.asset";

        [MenuItem("MRLab/Import TMP Essential Resources")]
        public static void ImportTmpEssentialResourcesBatch()
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("TMP Essential Resources import requested.");
        }

        [MenuItem("MRLab/Initialize Project")]
        public static void InitializeProject()
        {
            EnsureProjectFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetArchitecture(BuildTargetGroup.Standalone, 1);

            ConfigureUniversalRenderPipeline();
            EnsureTmpEssentialResourcesArePresent();
            CreateChineseTmpFontAsset();
            CreatePcMainScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("MRLab project initialization completed.");
        }

        [MenuItem("MRLab/Build Windows x86-64")]
        public static void BuildWindows()
        {
            ConfigureBuildSettings();
            Directory.CreateDirectory("Builds/PC");

            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = "Builds/PC/MRLab_E10_24371119.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            Debug.Log($"Windows build result: {summary.result}; size: {summary.totalSize}; errors: {summary.totalErrors}");

            if (summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException($"Windows build failed: {summary.result}");
            }
        }

        private static void EnsureProjectFolders()
        {
            string[] folders =
            {
                "Assets/_Project/Art/Animations",
                "Assets/_Project/Art/Materials",
                "Assets/_Project/Art/Models",
                "Assets/_Project/Art/Textures",
                "Assets/_Project/Art/VFX",
                "Assets/_Project/Audio",
                "Assets/_Project/Data",
                "Assets/_Project/Fonts",
                "Assets/_Project/Prefabs/Devices",
                "Assets/_Project/Prefabs/Environment",
                "Assets/_Project/Prefabs/Interactables",
                "Assets/_Project/Prefabs/Effects",
                "Assets/_Project/Prefabs/UI",
                "Assets/_Project/Scenes/PC",
                "Assets/_Project/Scenes/MR",
                "Assets/_Project/Scenes/CAVE",
                "Assets/_Project/Scenes/Test",
                "Assets/_Project/Scripts/Core",
                "Assets/_Project/Scripts/Experiment",
                "Assets/_Project/Scripts/Interaction/PC",
                "Assets/_Project/Scripts/Interaction/MR",
                "Assets/_Project/Scripts/Interaction/CAVE",
                "Assets/_Project/Scripts/UI",
                "Assets/_Project/Scripts/Utility/Editor",
                SettingsDirectory,
                "Assets/ThirdParty"
            };

            foreach (string folder in folders)
            {
                Directory.CreateDirectory(folder);
            }
        }

        private static void ConfigureUniversalRenderPipeline()
        {
            UniversalRendererData rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
            }

            UniversalRenderPipelineAsset pipelineAsset =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipelineAsset == null)
            {
                pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
                AssetDatabase.CreateAsset(pipelineAsset, PipelinePath);
            }

            var serializedPipeline = new SerializedObject(pipelineAsset);
            SerializedProperty rendererList = serializedPipeline.FindProperty("m_RendererDataList");
            if (rendererList == null)
            {
                throw new InvalidOperationException("URP renderer data list property was not found.");
            }

            rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = rendererData;
            SerializedProperty defaultRendererIndex = serializedPipeline.FindProperty("m_DefaultRendererIndex");
            if (defaultRendererIndex != null)
            {
                defaultRendererIndex.intValue = 0;
            }

            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipelineAsset);
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;
            QualitySettings.renderPipeline = pipelineAsset;
        }

        private static void EnsureTmpEssentialResourcesArePresent()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                return;
            }

            throw new InvalidOperationException(
                "TMP Essential Resources are missing. Run ImportTmpEssentialResourcesBatch first.");
        }

        private static void CreateChineseTmpFontAsset()
        {
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontAssetPath) != null)
            {
                return;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ChineseFontPath);
            if (sourceFont == null)
            {
                throw new FileNotFoundException("Chinese font source was not imported.", ChineseFontPath);
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                90,
                9,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);
            if (fontAsset == null)
            {
                throw new InvalidOperationException(
                    "TMP failed to create a Chinese font asset from NotoSansCJKsc-Regular.otf.");
            }

            fontAsset.name = "NotoSansCJKsc-Regular SDF";
            AssetDatabase.CreateAsset(fontAsset, ChineseFontAssetPath);
        }

        private static void CreatePcMainScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject systems = CreateRoot("_Systems");
            CreateChild(systems, "ExperimentManager");
            CreateChild(systems, "UIManager");
            CreateChild(systems, "AudioManager");
            CreateChild(systems, "InputManager");
            CreateChild(systems, "ResetManager");

            GameObject environment = CreateRoot("_Environment");
            GameObject directionalLight = CreateChild(environment, "Directional Light");
            Light light = directionalLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.shadows = LightShadows.Soft;
            directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject experiment = CreateRoot("_Experiment");
            CreateChild(experiment, "Devices");
            CreateChild(experiment, "Materials");
            CreateChild(experiment, "Interactables");
            CreateChild(experiment, "Effects");

            GameObject interaction = CreateRoot("_Interaction");
            CreateChild(interaction, "PC");

            CreateRoot("_UI");
            GameObject cameraRoot = CreateRoot("_Camera");
            GameObject cameraObject = CreateChild(cameraRoot, "Main Camera");
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();
            cameraObject.transform.position = new Vector3(0f, 1f, -10f);
            CreateRoot("_Debug");

            EditorSceneManager.SaveScene(scene, MainScenePath);
        }

        private static GameObject CreateRoot(string name)
        {
            return new GameObject(name);
        }

        private static GameObject CreateChild(GameObject parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent.transform, false);
            return child;
        }

        private static void ConfigureBuildSettings()
        {
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MainScenePath, true)
            };
        }
    }
}
