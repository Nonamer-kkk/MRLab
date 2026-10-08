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
        private const string TmpEssentialResourcesPath = "Assets/ThirdParty/TextMesh Pro";
        private const string TmpSettingsPath = TmpEssentialResourcesPath + "/Resources/TMP Settings.asset";

        [MenuItem("MRLab/Import TMP Essential Resources")]
        public static void ImportTmpEssentialResourcesBatch()
        {
            TMP_PackageResourceImporter.ImportResources(true, false, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            MoveTmpEssentialResourcesToThirdParty();
            CreateOrRepairChineseTmpFontAsset();
            ConfigureTmpSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("TMP Essential Resources imported under Assets/ThirdParty and configured for the project Chinese font.");
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
            CreateOrRepairChineseTmpFontAsset();
            ConfigureTmpSettings();
            CreatePcMainScene();
            ConfigureBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("MRLab project initialization completed.");
        }

        [MenuItem("MRLab/Validate Document Compliance")]
        public static void ValidateDocumentCompliance()
        {
            EnsureProjectFolders();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureTmpEssentialResourcesArePresent();
            CreateOrRepairChineseTmpFontAsset();
            ConfigureTmpSettings();
            ConfigureBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("MRLab document-compliance validation completed.");
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
            MoveTmpEssentialResourcesToThirdParty();

            if (File.Exists(TmpSettingsPath))
            {
                return;
            }

            throw new InvalidOperationException(
                "TMP Essential Resources are missing. Run ImportTmpEssentialResourcesBatch first.");
        }

        private static void MoveTmpEssentialResourcesToThirdParty()
        {
            const string defaultImportPath = "Assets/TextMesh Pro";

            if (!AssetDatabase.IsValidFolder(defaultImportPath))
            {
                return;
            }

            if (AssetDatabase.IsValidFolder(TmpEssentialResourcesPath))
            {
                throw new InvalidOperationException(
                    $"Both {defaultImportPath} and {TmpEssentialResourcesPath} exist. Merge them before continuing.");
            }

            string error = AssetDatabase.MoveAsset(defaultImportPath, TmpEssentialResourcesPath);
            if (!string.IsNullOrEmpty(error))
            {
                throw new InvalidOperationException($"Unable to move TMP Essential Resources: {error}");
            }
        }

        private static void CreateOrRepairChineseTmpFontAsset()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(ChineseFontPath);
            if (sourceFont == null)
            {
                throw new FileNotFoundException("Chinese font source was not imported.", ChineseFontPath);
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontAssetPath);
            if (fontAsset == null)
            {
                fontAsset = TMP_FontAsset.CreateFontAsset(
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

            Texture2D atlasTexture = fontAsset.atlasTextures != null && fontAsset.atlasTextures.Length > 0
                ? fontAsset.atlasTextures[0]
                : null;
            if (atlasTexture == null)
            {
                atlasTexture = new Texture2D(0, 0, TextureFormat.Alpha8, false)
                {
                    name = "NotoSansCJKsc-Regular Atlas"
                };
                fontAsset.atlasTextures = new[] { atlasTexture };
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }

            if (fontAsset.material == null)
            {
                Shader shader = Shader.Find("TextMeshPro/Distance Field");
                if (shader == null)
                {
                    throw new InvalidOperationException("The TextMeshPro distance-field shader is unavailable.");
                }

                var material = new Material(shader)
                {
                    name = "NotoSansCJKsc-Regular Atlas Material"
                };
                material.SetTexture(ShaderUtilities.ID_MainTex, atlasTexture);
                material.SetFloat(ShaderUtilities.ID_TextureWidth, fontAsset.atlasWidth);
                material.SetFloat(ShaderUtilities.ID_TextureHeight, fontAsset.atlasHeight);
                material.SetFloat(ShaderUtilities.ID_GradientScale, fontAsset.atlasPadding + 1);
                material.SetFloat(ShaderUtilities.ID_WeightNormal, fontAsset.normalStyle);
                material.SetFloat(ShaderUtilities.ID_WeightBold, fontAsset.boldStyle);
                fontAsset.material = material;
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            fontAsset.isMultiAtlasTexturesEnabled = true;
            EditorUtility.SetDirty(fontAsset);
        }

        private static void ConfigureTmpSettings()
        {
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            TMP_FontAsset chineseFontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(ChineseFontAssetPath);
            if (settings == null || chineseFontAsset == null)
            {
                throw new InvalidOperationException("TMP Settings or the project Chinese TMP font asset is missing.");
            }

            var serializedSettings = new SerializedObject(settings);
            SerializedProperty defaultFont = serializedSettings.FindProperty("m_defaultFontAsset");
            SerializedProperty fallbackFonts = serializedSettings.FindProperty("m_fallbackFontAssets");
            if (defaultFont == null || fallbackFonts == null)
            {
                throw new InvalidOperationException("TMP font settings properties were not found.");
            }

            defaultFont.objectReferenceValue = chineseFontAsset;
            fallbackFonts.arraySize = 1;
            fallbackFonts.GetArrayElementAtIndex(0).objectReferenceValue = chineseFontAsset;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
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
