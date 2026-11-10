#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Networking;

namespace MRLab.Hunyuan3D
{
    public sealed class Hunyuan3DGeneratorWindow : EditorWindow
    {
        private string imagePath = string.Empty;
        private string modelName = "HunyuanGeneratedModel";
        private string serviceUrl = "http://127.0.0.1:8080/generate";
        private string status = "选择一张外部图片，然后点击生成。";
        private UnityWebRequest request;
        private string outputPath;

        [MenuItem("MRLab/Hunyuan3D/从图片生成模型")]
        public static void Open() => GetWindow<Hunyuan3DGeneratorWindow>("Hunyuan3D 生成");

        public static void ValidateImportedAsset()
        {
            const string path = "Assets/_Project/Art/Models/HunyuanGenerated/AI_FireExtinguisher.obj";
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Hunyuan OBJ was not imported: " + path);
            MeshFilter filter = asset.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                throw new InvalidOperationException("Hunyuan OBJ has no usable MeshFilter: " + path);
            Debug.Log("HUNYUAN_IMPORT_VALIDATED: " + path + "; vertices=" + filter.sharedMesh.vertexCount + "; triangles=" + (filter.sharedMesh.triangles.Length / 3));
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "图片会发送到本机 Hunyuan3D 推理服务。服务只使用图片，不读取旧 Unity 网格。生成的 OBJ 会导入当前工程并放到当前场景原点。",
                MessageType.Info);
            EditorGUILayout.LabelField("输入图片", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.TextField(imagePath);
                if (GUILayout.Button("选择", GUILayout.Width(70)))
                    imagePath = EditorUtility.OpenFilePanel("选择物体图片", "", "png,jpg,jpeg,webp");
            }
            modelName = EditorGUILayout.TextField("模型名称", modelName);
            serviceUrl = EditorGUILayout.TextField("服务地址", serviceUrl);
            EditorGUILayout.Space(6);
            if (request != null && !request.isDone)
                EditorGUILayout.HelpBox(status, MessageType.Info);
            else if (!string.IsNullOrEmpty(status))
                EditorGUILayout.HelpBox(status, status.StartsWith("生成失败") ? MessageType.Error : MessageType.None);
            using (new EditorGUI.DisabledScope(request != null && !request.isDone))
            {
                if (GUILayout.Button("生成并导入到当前场景", GUILayout.Height(32)))
                    Generate();
            }
        }

        private void Generate()
        {
            if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            {
                status = "生成失败：请先选择存在的图片。";
                return;
            }
            if (string.IsNullOrWhiteSpace(modelName)) modelName = "HunyuanGeneratedModel";
            modelName = Sanitize(modelName);
            byte[] bytes = File.ReadAllBytes(imagePath);
            string mime = imagePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("image", bytes, Path.GetFileName(imagePath), mime),
                new MultipartFormDataSection("seed", "1234"),
                new MultipartFormDataSection("steps", "5"),
                new MultipartFormDataSection("octree_resolution", "128")
            };
            request = UnityWebRequest.Post(serviceUrl, form);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 900;
            outputPath = "Assets/_Project/Art/Models/HunyuanGenerated/" + modelName + ".obj";
            status = "正在等待 Hunyuan3D 推理……首次运行会下载模型权重。";
            EditorApplication.update += PollRequest;
            request.SendWebRequest();
        }

        private void PollRequest()
        {
            Repaint();
            if (request == null || !request.isDone) return;
            EditorApplication.update -= PollRequest;
            if (request.result != UnityWebRequest.Result.Success)
            {
                status = "生成失败：" + request.error + "\n" + request.downloadHandler.text;
                request.Dispose(); request = null; Repaint(); return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            File.WriteAllBytes(outputPath, request.downloadHandler.data);
            request.Dispose(); request = null;
            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(outputPath);
            if (prefab == null)
            {
                status = "生成成功，但 Unity 没有识别 OBJ：" + outputPath;
                Repaint(); return;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = modelName;
            instance.transform.position = Vector3.zero;
            Undo.RegisterCreatedObjectUndo(instance, "Place Hunyuan3D model");
            Selection.activeGameObject = instance;
            EditorSceneManager.MarkSceneDirty(instance.scene);
            status = "生成成功：" + outputPath + "\n已放置到当前场景原点，可直接操作。";
            Repaint();
        }

        private static string Sanitize(string value)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) value = value.Replace(c.ToString(), "_");
            return value.Trim();
        }

        private void OnDisable()
        {
            if (request != null && !request.isDone) request.Abort();
            EditorApplication.update -= PollRequest;
        }
    }
}
#endif
