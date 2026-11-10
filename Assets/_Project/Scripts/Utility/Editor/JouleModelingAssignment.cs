using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MRLab.Joule.Editor
{
    /// <summary>Assignment 2: source sampling reference export and screened-Poisson/Blender mesh integration.</summary>
    public static class JouleModelingAssignment
    {
        private const string MainScene = "Assets/_Project/Scenes/PC/01_PC_Main.unity";
        private const string SourceDirectory = "Assets/_Project/Art/Models/GeneratedLab";
        private const string OptimizedDirectory = "Assets/_Project/Art/Models/Assignment02/Optimized";
        private const string BaseDirectory = "SourceAssets/Assignment02";
        private const string SourceVersion = "MRLab primitive assembly 2.0.0 (sampling reference)";
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        [Serializable] public sealed class MeshRecord
        {
            public string name, sourceAssetPath;
            public float[] vertices, normals, uv;
            public int[] triangles;
        }
        [Serializable] public sealed class PrimitiveRecord
        {
            public string name, primitiveType;
            public float[] size;
        }
        [Serializable] public sealed class PartRecord
        {
            public string name, hierarchyPath, meshName;
            public string[] materialNames;
            public float[] matrix;
            public bool active;
        }
        [Serializable] public sealed class ObjectRecord
        {
            public string id, name, hierarchyPath, objFile;
            public float[] worldMatrix;
            public bool active;
            public PartRecord[] parts;
        }
        [Serializable] public sealed class MaterialRecord
        {
            public string name;
            public float[] color;
            public float metallic, smoothness;
        }
        [Serializable] public sealed class GeometryDocument
        {
            public string version, generatedUtc, coordinateSystem, inputSha256;
            public MeshRecord[] meshRecords;
            public PrimitiveRecord[] primitiveRecords;
            public ObjectRecord[] objects;
            public MaterialRecord[] materials;
        }
        [Serializable] public sealed class ValidationReport
        {
            public string utc, algorithmVersion, status, algorithmGeometrySha256, softwareGeometrySha256;
            public int objects, meshInstances, uniqueMeshes, baselineTriangles, optimizedTriangles;
            public bool structurePreserved, allMeshesMapped, noMissingScripts;
        }
        [Serializable] public sealed class ExportReport
        {
            public string utc, method, unityVersion, algorithmVersion, geometrySha256, status;
            public bool unityExecuted;
            public int objects, meshInstances, uniqueMeshes, materials, uniqueTriangles, instanceTriangles;
        }

        [MenuItem("MRLab/Assignment 2/1 Export Sampling Reference and Inputs")]
        public static void ExportBaseline()
        {
            Scene scene = OpenSavedScene();
            Transform[] roots = TargetRoots(scene);
            Require(roots.Length > 0, "No assignment modeling targets found in the saved PC scene.");
            var meshes = new SortedDictionary<string, Mesh>();
            var materials = new SortedDictionary<string, Material>();
            var objects = new List<ObjectRecord>();
            for (int index = 0; index < roots.Length; index++)
            {
                Transform root = roots[index];
                var parts = new List<PartRecord>();
                foreach (MeshFilter filter in TargetFilters(root))
                {
                    Mesh mesh = OriginalMesh(filter.sharedMesh);
                    meshes[mesh.name] = mesh;
                    Renderer renderer = filter.GetComponent<Renderer>();
                    Material[] slots = renderer != null ? renderer.sharedMaterials : new Material[0];
                    foreach (Material material in slots)
                        if (material != null) materials[material.name] = material;
                    parts.Add(new PartRecord
                    {
                        name = filter.name, hierarchyPath = HierarchyPath(filter.transform), meshName = mesh.name,
                        materialNames = slots.Select(m => m != null ? m.name : "Default").ToArray(),
                        matrix = Flatten(root.worldToLocalMatrix * filter.transform.localToWorldMatrix),
                        active = filter.gameObject.activeInHierarchy && (renderer == null || renderer.enabled)
                    });
                }
                if (parts.Count == 0) continue;
                string id = (objects.Count + 1).ToString("D2", Invariant) + "_" + SafeName(root.name);
                objects.Add(new ObjectRecord
                {
                    id = id, name = root.name, hierarchyPath = HierarchyPath(root), objFile = id + ".obj",
                    worldMatrix = Flatten(root.localToWorldMatrix), active = root.gameObject.activeInHierarchy,
                    parts = parts.ToArray()
                });
            }
            var inputs = new GeometryDocument
            {
                version = SourceVersion, generatedUtc = DateTime.UtcNow.ToString("o", Invariant),
                coordinateSystem = "Unity local mesh coordinates; metres; row-major matrices. OBJ reflects X and reverses winding.",
                primitiveRecords = meshes.Values.Select(PrimitiveFromMesh).ToArray(),
                objects = objects.ToArray(), materials = materials.Values.Select(MaterialData).ToArray(),
                meshRecords = new MeshRecord[0]
            };
            string inputDirectory = Path.Combine(BaseDirectory, "Input");
            Directory.CreateDirectory(inputDirectory);
            WriteJson(Path.Combine(inputDirectory, "parameters.json"), inputs);
            File.Copy("Assets/_Project/Scripts/Core/LabVisualFactory.cs", Path.Combine(inputDirectory, "LabVisualFactory.cs"), true);
            File.Copy("Assets/_Project/Scripts/Experiment/WirePathGeometry.cs", Path.Combine(inputDirectory, "WirePathGeometry.cs"), true);
            RegenerateBaselineMeshes();
            Debug.Log("ASSIGNMENT02_BASELINE: " + objects.Count + " objects; " + meshes.Count + " unique meshes. PC scene preserved.");
        }

        [MenuItem("MRLab/Assignment 2/Replay Sampling Reference from Saved Inputs")]
        public static void RegenerateBaselineMeshes()
        {
            GeometryDocument document = ReadDocument(Path.Combine(BaseDirectory, "Input", "parameters.json"));
            Require(document.primitiveRecords != null && document.primitiveRecords.Length > 0, "No primitive parameters in algorithm input.");
            var records = new List<MeshRecord>();
            foreach (PrimitiveRecord parameter in document.primitiveRecords)
            {
                PrimitiveType type;
                Require(Enum.TryParse(parameter.primitiveType, out type), "Unsupported primitive type: " + parameter.primitiveType);
                Require(parameter.size != null && parameter.size.Length == 3 && parameter.size.All(v => Finite(v) && v > 0f), "Invalid dimensions for " + parameter.name);
                GameObject primitive = GameObject.CreatePrimitive(type);
                Mesh mesh = UnityEngine.Object.Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
                try
                {
                    mesh.name = parameter.name;
                    Vector3 size = new Vector3(parameter.size[0], parameter.size[1], parameter.size[2]);
                    Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                    for (int i = 0; i < vertices.Length; i++) vertices[i] = Vector3.Scale(vertices[i], size);
                    for (int i = 0; i < normals.Length; i++)
                        normals[i] = new Vector3(normals[i].x / size.x, normals[i].y / size.y, normals[i].z / size.z).normalized;
                    mesh.vertices = vertices; mesh.normals = normals; mesh.RecalculateBounds();
                    records.Add(MeshData(mesh, SourceDirectory + "/" + parameter.name + ".asset"));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(mesh);
                    UnityEngine.Object.DestroyImmediate(primitive);
                }
            }
            document.meshRecords = records.ToArray();
            document.generatedUtc = DateTime.UtcNow.ToString("o", Invariant);
            string directory = Path.Combine(BaseDirectory, "Input", "Reference");
            Directory.CreateDirectory(directory);
            WriteJson(Path.Combine(directory, "geometry.json"), document);
            File.Copy(Path.Combine(directory, "geometry.json"), Path.Combine(BaseDirectory, "Input", "baseline_geometry.json"), true);
            ExportObj(document, directory);
            WriteExportReport(document, directory);
            Debug.Log("ASSIGNMENT02_REPLAY: " + records.Count + " meshes regenerated from saved primitive inputs.");
        }

        private static void WriteExportReport(GeometryDocument document, string directory)
        {
            string path = Path.Combine(directory, "export_report.json");
            if (File.Exists(path) && File.ReadAllText(path).Contains("offline saved-project export"))
                File.Copy(path, Path.Combine(directory, "export_report.offline.json"), true);
            string hash;
            using (SHA256 sha256 = SHA256.Create())
                hash = BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(Path.Combine(directory, "geometry.json")))).Replace("-", "").ToLowerInvariant();
            var records = document.meshRecords.ToDictionary(record => record.name);
            WriteJson(path, new ExportReport
            {
                utc = document.generatedUtc, method = "Unity Editor primitive parameter replay and OBJ export",
                unityVersion = Application.unityVersion, algorithmVersion = document.version,
                geometrySha256 = hash, status = "exported", unityExecuted = true,
                objects = document.objects.Length, meshInstances = document.objects.Sum(item => item.parts.Length),
                uniqueMeshes = document.meshRecords.Length, materials = document.materials.Length,
                uniqueTriangles = document.meshRecords.Sum(record => record.triangles.Length / 3),
                instanceTriangles = document.objects.Sum(item => item.parts.Sum(part => records[part.meshName].triangles.Length / 3))
            });
        }

        [MenuItem("MRLab/Assignment 2/2 Apply Blender Optimized Meshes")]
        public static void ApplyOptimized()
        {
            Scene scene = OpenSavedScene();
            GeometryDocument baseline = ReadDocument(Path.Combine(BaseDirectory, "Algorithm", "geometry.json"));
            GeometryDocument software = ReadDocument(Path.Combine(BaseDirectory, "Software", "geometry.json"));
            ValidateProvenance(baseline, software);
            Require(software.meshRecords != null && software.meshRecords.Length > 0, "Software mesh output is empty.");
            var records = software.meshRecords.ToDictionary(r => r.name);
            var expected = new HashSet<string>(baseline.meshRecords.Select(r => r.name));
            Require(expected.SetEquals(records.Keys), "Blender mesh names differ from the complete algorithm mesh checklist.");
            string structureBefore = SceneStructure(scene);
            EnsureAssetFolder(OptimizedDirectory);
            var replacements = new Dictionary<string, Mesh>();
            foreach (MeshRecord record in software.meshRecords)
            {
                ValidateGeometry(record);
                MeshRecord original = baseline.meshRecords.First(r => r.name == record.name);
                ValidateBounds(original, record);
                string path = OptimizedDirectory + "/" + record.name + ".asset";
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                bool existing = mesh != null;
                if (!existing) mesh = new Mesh();
                mesh.Clear(); mesh.name = record.name;
                mesh.indexFormat = record.vertices.Length / 3 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.vertices = Vectors(record.vertices); mesh.normals = Vectors(record.normals);
                if (record.uv != null && record.uv.Length == record.vertices.Length / 3 * 2) mesh.uv = Uvs(record.uv);
                mesh.triangles = record.triangles; mesh.RecalculateBounds();
                if (mesh.uv.Length == mesh.vertexCount) mesh.RecalculateTangents();
                if (existing) EditorUtility.SetDirty(mesh); else AssetDatabase.CreateAsset(mesh, path);
                replacements.Add(record.name, mesh);
            }
            AssetDatabase.SaveAssets();
            var targetNames = new HashSet<string>(baseline.objects.Select(o => o.name));
            int prefabMappings = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!targetNames.Contains(Path.GetFileNameWithoutExtension(path))) continue;
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    int mapped = Remap(prefab.GetComponentsInChildren<MeshFilter>(true), replacements, false);
                    if (mapped > 0) { PrefabUtility.SaveAsPrefabAsset(prefab, path); prefabMappings += mapped; }
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
            MeshFilter[] filters = TargetRoots(scene).SelectMany(TargetFilters).Distinct().ToArray();
            int count = Remap(filters, replacements, true);
            Require(count == filters.Length, "An assignment mesh instance was not mapped to optimized geometry.");
            Require(SceneStructure(scene) == structureBefore, "Model integration changed transforms, components, materials, colliders or script references.");
            foreach (MeshFilter filter in filters)
                Require(AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(OptimizedDirectory + "/", StringComparison.Ordinal), "Mesh integration incomplete at " + HierarchyPath(filter.transform));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainScene);
            AssetDatabase.SaveAssets();
            WriteValidation(scene, baseline, software, true);
            Debug.Log("ASSIGNMENT02_APPLIED: " + count + " scene mesh instances; " + prefabMappings + " prefab mesh instances. Structure and behavior references preserved.");
        }

        [MenuItem("MRLab/Assignment 2/3 Validate and Build PC")]
        public static void ValidateAndBuild()
        {
            Scene scene = OpenSavedScene();
            GeometryDocument baseline = ReadDocument(Path.Combine(BaseDirectory, "Algorithm", "geometry.json"));
            GeometryDocument software = ReadDocument(Path.Combine(BaseDirectory, "Software", "geometry.json"));
            ValidateProvenance(baseline, software);
            ValidationReport applied = JsonUtility.FromJson<ValidationReport>(File.ReadAllText("Logs/Assignment02Integration.json"));
            Require(applied != null && applied.structurePreserved &&
                applied.algorithmGeometrySha256 == FileSha256(Path.Combine(BaseDirectory, "Algorithm", "geometry.json")) &&
                applied.softwareGeometrySha256 == FileSha256(Path.Combine(BaseDirectory, "Software", "geometry.json")),
                "Run ApplyOptimized for the current Poisson and Blender outputs before building.");
            foreach (MeshRecord record in software.meshRecords) ValidateGeometry(record);
            MeshFilter[] filters = TargetRoots(scene).SelectMany(TargetFilters).Distinct().ToArray();
            Require(filters.Length == baseline.objects.Sum(o => o.parts.Length), "Model instance count differs from the exported checklist.");
            foreach (MeshFilter filter in filters)
                Require(filter.sharedMesh != null && AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(OptimizedDirectory + "/", StringComparison.Ordinal), "Unmapped optimized mesh: " + HierarchyPath(filter.transform));
            Require(NoMissingScripts(scene), "Missing MonoBehaviour scripts in saved scene.");
            JouleModelTests.Run();
            JouleSceneBuilder.Build();
            WriteValidation(scene, baseline, software, true);
            Debug.Log("ASSIGNMENT02_VALIDATED_AND_BUILT");
        }

        private static int Remap(IEnumerable<MeshFilter> filters, Dictionary<string, Mesh> replacements, bool scene)
        {
            int count = 0;
            foreach (MeshFilter filter in filters)
            {
                Mesh mesh;
                if (filter.sharedMesh == null || !replacements.TryGetValue(filter.sharedMesh.name, out mesh)) continue;
                filter.sharedMesh = mesh; EditorUtility.SetDirty(filter);
                if (scene && PrefabUtility.IsPartOfPrefabInstance(filter)) PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                count++;
            }
            return count;
        }

        private static void WriteValidation(Scene scene, GeometryDocument baseline, GeometryDocument software, bool structure)
        {
            var report = new ValidationReport
            {
                utc = DateTime.UtcNow.ToString("o", Invariant), algorithmVersion = baseline.version, status = "passed",
                algorithmGeometrySha256 = FileSha256(Path.Combine(BaseDirectory, "Algorithm", "geometry.json")),
                softwareGeometrySha256 = FileSha256(Path.Combine(BaseDirectory, "Software", "geometry.json")),
                objects = baseline.objects.Length, meshInstances = baseline.objects.Sum(o => o.parts.Length),
                uniqueMeshes = software.meshRecords.Length,
                baselineTriangles = baseline.meshRecords.Sum(m => m.triangles.Length / 3),
                optimizedTriangles = software.meshRecords.Sum(m => m.triangles.Length / 3),
                structurePreserved = structure, allMeshesMapped = true, noMissingScripts = NoMissingScripts(scene)
            };
            Directory.CreateDirectory("Logs"); WriteJson("Logs/Assignment02Integration.json", report);
        }

        private static Scene OpenSavedScene()
        {
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Exit Play mode before modeling operations.");
            Scene active = SceneManager.GetActiveScene();
            if (active.path == MainScene)
            {
                Require(!active.isDirty, "Save the current scene before exporting or applying modeling assets.");
                return active;
            }
            Require(!active.isDirty, "Save the current scene before opening the PC scene.");
            return EditorSceneManager.OpenScene(MainScene, OpenSceneMode.Single);
        }

        private static string FileSha256(string path)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        }

        private static void ValidateProvenance(GeometryDocument baseline, GeometryDocument software)
        {
            Require(baseline.version != null && baseline.version.StartsWith("Screened Poisson Surface Reconstruction", StringComparison.Ordinal),
                "Algorithm geometry must be generated by poisson_reconstruct.py.");
            Require(software.inputSha256 == FileSha256(Path.Combine(BaseDirectory, "Algorithm", "geometry.json")),
                "Blender output was not generated from the current Poisson geometry.");
        }

        private static Transform[] TargetRoots(Scene scene)
        {
            var result = new List<Transform>();
            Transform environment = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "_Environment")?.transform;
            Transform experiment = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "_Experiment")?.transform;
            Transform bench = environment != null ? environment.Find("LaboratoryBench") : null;
            if (bench != null) result.Add(bench);
            Transform rig = experiment != null ? experiment.Find("Devices/ExperimentBench") : null;
            if (rig != null) foreach (Transform child in rig) if (TargetFilters(child).Any()) result.Add(child);
            Transform interactables = experiment != null ? experiment.Find("Interactables") : null;
            if (interactables != null) foreach (Transform child in interactables) if (TargetFilters(child).Any()) result.Add(child);
            return result.ToArray();
        }

        private static IEnumerable<MeshFilter> TargetFilters(Transform root)
        {
            return root.GetComponentsInChildren<MeshFilter>(true).Where(f => f.sharedMesh != null && f.sharedMesh.name.StartsWith("Lab_", StringComparison.Ordinal));
        }

        private static Mesh OriginalMesh(Mesh current)
        {
            Mesh original = AssetDatabase.LoadAssetAtPath<Mesh>(SourceDirectory + "/" + current.name + ".asset");
            Require(original != null, "Original algorithm primitive asset unavailable: " + current.name);
            return original;
        }

        private static PrimitiveRecord PrimitiveFromMesh(Mesh mesh)
        {
            string[] tokens = mesh.name.Split('_');
            Require(tokens.Length == 5 && tokens[0] == "Lab", "Unrecognized procedural mesh key: " + mesh.name);
            return new PrimitiveRecord
            {
                name = mesh.name, primitiveType = tokens[1],
                size = tokens.Skip(2).Select(t => float.Parse(t.Replace("p", ".").Replace("m", "-"), Invariant)).ToArray()
            };
        }

        private static MaterialRecord MaterialData(Material material)
        {
            Color color = material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
            return new MaterialRecord
            {
                name = material.name, color = new[] { color.r, color.g, color.b, color.a },
                metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0,
                smoothness = material.HasProperty("_Smoothness") ? material.GetFloat("_Smoothness") : 0
            };
        }

        private static MeshRecord MeshData(Mesh mesh, string source)
        {
            return new MeshRecord
            {
                name = mesh.name, sourceAssetPath = source,
                vertices = mesh.vertices.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(),
                normals = mesh.normals.SelectMany(v => new[] { v.x, v.y, v.z }).ToArray(),
                uv = mesh.uv.SelectMany(v => new[] { v.x, v.y }).ToArray(), triangles = mesh.triangles
            };
        }

        private static void ExportObj(GeometryDocument document, string directory)
        {
            var records = document.meshRecords.ToDictionary(r => r.name);
            var mtl = new StringBuilder("# MRLab shared modeling materials\n");
            foreach (MaterialRecord material in document.materials)
            {
                mtl.Append("newmtl ").Append(SafeName(material.name)).Append('\n');
                mtl.AppendFormat(Invariant, "Kd {0:R} {1:R} {2:R}\nd {3:R}\nNs {4:R}\n\n", material.color[0], material.color[1], material.color[2], material.color[3], material.smoothness * 100f);
            }
            foreach (ObjectRecord item in document.objects)
            {
                var obj = new StringBuilder("# Generated from preserved algorithm inputs. Units: metres.\nmtllib " + item.id + ".mtl\n");
                int offset = 1;
                foreach (PartRecord part in item.parts)
                {
                    MeshRecord record = records[part.meshName];
                    Vector3[] vertices = Vectors(record.vertices), normals = Vectors(record.normals);
                    Vector2[] uvs = record.uv != null && record.uv.Length == vertices.Length * 2 ? Uvs(record.uv) : new Vector2[vertices.Length];
                    Matrix4x4 matrix = Matrix(part.matrix), normalMatrix = matrix.inverse.transpose;
                    obj.Append("o ").Append(SafeName(part.hierarchyPath)).Append('\n');
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 v = matrix.MultiplyPoint3x4(vertices[i]);
                        obj.AppendFormat(Invariant, "v {0:R} {1:R} {2:R}\n", -v.x, v.y, v.z);
                    }
                    foreach (Vector2 uv in uvs) obj.AppendFormat(Invariant, "vt {0:R} {1:R}\n", uv.x, uv.y);
                    foreach (Vector3 normal in normals)
                    {
                        Vector3 n = normalMatrix.MultiplyVector(normal).normalized;
                        obj.AppendFormat(Invariant, "vn {0:R} {1:R} {2:R}\n", -n.x, n.y, n.z);
                    }
                    obj.Append("usemtl ").Append(SafeName(part.materialNames.Length > 0 ? part.materialNames[0] : "Default")).Append('\n');
                    for (int i = 0; i < record.triangles.Length; i += 3)
                    {
                        int a = record.triangles[i] + offset, b = record.triangles[i + 2] + offset, c = record.triangles[i + 1] + offset;
                        obj.AppendFormat(Invariant, "f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\n", a, b, c);
                    }
                    offset += vertices.Length;
                }
                File.WriteAllText(Path.Combine(directory, item.objFile), obj.ToString(), new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(directory, item.id + ".mtl"), mtl.ToString(), new UTF8Encoding(false));
            }
        }

        private static string SceneStructure(Scene scene)
        {
            var text = new StringBuilder();
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    text.Append(HierarchyPath(transform)).Append(':').Append(transform.gameObject.activeSelf).Append(':')
                        .Append(JsonUtility.ToJson(transform.localPosition)).Append(':').Append(JsonUtility.ToJson(transform.localRotation)).Append(':').Append(JsonUtility.ToJson(transform.localScale)).Append('\n');
                    Component[] components = transform.GetComponents<Component>();
                    for (int componentIndex = 0; componentIndex < components.Length; componentIndex++)
                    {
                        Component component = components[componentIndex];
                        if (component == null) { text.Append("MISSING\n"); continue; }
                        text.Append(componentIndex).Append(':').Append(component.GetType().FullName).Append('\n');
                        var serialized = new SerializedObject(component);
                        SerializedProperty property = serialized.GetIterator();
                        bool enterChildren = true;
                        while (property.Next(enterChildren))
                        {
                            enterChildren = true;
                            // Prefab bookkeeping may be rebuilt when its mesh reference is saved.
                            // The actual scene references below use asset IDs or hierarchy paths.
                            if (property.propertyPath == "m_CorrespondingSourceObject" || property.propertyPath == "m_PrefabInstance" || property.propertyPath == "m_PrefabAsset")
                            {
                                enterChildren = false;
                                continue;
                            }
                            if (component is MeshFilter && property.propertyPath == "m_Mesh")
                            {
                                enterChildren = false;
                                continue;
                            }
                            AppendStableProperty(text, property);
                        }
                    }
                }
            return text.ToString();
        }

        private static void AppendStableProperty(StringBuilder text, SerializedProperty property)
        {
            text.Append(property.propertyPath).Append('=').Append(property.propertyType).Append(':');
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: text.Append(property.longValue); break;
                case SerializedPropertyType.Boolean: text.Append(property.boolValue); break;
                case SerializedPropertyType.Float: text.Append(property.doubleValue.ToString("R", Invariant)); break;
                case SerializedPropertyType.String: text.Append(property.stringValue); break;
                case SerializedPropertyType.Color:
                    Color color = property.colorValue;
                    text.Append(Float(color.r)).Append(',').Append(Float(color.g)).Append(',').Append(Float(color.b)).Append(',').Append(Float(color.a));
                    break;
                case SerializedPropertyType.ObjectReference: text.Append(StableObjectReference(property.objectReferenceValue)); break;
                case SerializedPropertyType.LayerMask: text.Append(property.intValue); break;
                case SerializedPropertyType.Enum: text.Append(property.enumValueIndex); break;
                case SerializedPropertyType.Vector2:
                    Vector2 vector2 = property.vector2Value;
                    text.Append(Float(vector2.x)).Append(',').Append(Float(vector2.y));
                    break;
                case SerializedPropertyType.Vector3:
                    Vector3 vector3 = property.vector3Value;
                    text.Append(Float(vector3.x)).Append(',').Append(Float(vector3.y)).Append(',').Append(Float(vector3.z));
                    break;
                case SerializedPropertyType.Vector4:
                    Vector4 vector4 = property.vector4Value;
                    text.Append(Float(vector4.x)).Append(',').Append(Float(vector4.y)).Append(',').Append(Float(vector4.z)).Append(',').Append(Float(vector4.w));
                    break;
                case SerializedPropertyType.Rect:
                    Rect rect = property.rectValue;
                    text.Append(Float(rect.x)).Append(',').Append(Float(rect.y)).Append(',').Append(Float(rect.width)).Append(',').Append(Float(rect.height));
                    break;
                case SerializedPropertyType.ArraySize: text.Append(property.intValue); break;
                case SerializedPropertyType.Character: text.Append(property.intValue); break;
                case SerializedPropertyType.AnimationCurve:
                    AnimationCurve curve = property.animationCurveValue;
                    text.Append(curve.preWrapMode).Append(':').Append(curve.postWrapMode);
                    foreach (Keyframe key in curve.keys)
                        text.Append(';').Append(Float(key.time)).Append(',').Append(Float(key.value)).Append(',').Append(Float(key.inTangent)).Append(',').Append(Float(key.outTangent))
                            .Append(',').Append(Float(key.inWeight)).Append(',').Append(Float(key.outWeight)).Append(',').Append(key.weightedMode);
                    break;
                case SerializedPropertyType.Gradient:
                    Gradient gradient = property.gradientValue;
                    text.Append(gradient.mode);
                    foreach (GradientColorKey key in gradient.colorKeys)
                        text.Append(';').Append(Float(key.time)).Append(',').Append(Float(key.color.r)).Append(',').Append(Float(key.color.g)).Append(',').Append(Float(key.color.b)).Append(',').Append(Float(key.color.a));
                    foreach (GradientAlphaKey key in gradient.alphaKeys)
                        text.Append(';').Append(Float(key.time)).Append(',').Append(Float(key.alpha));
                    break;
                case SerializedPropertyType.Bounds:
                    Bounds bounds = property.boundsValue;
                    text.Append(Float(bounds.center.x)).Append(',').Append(Float(bounds.center.y)).Append(',').Append(Float(bounds.center.z)).Append(';')
                        .Append(Float(bounds.size.x)).Append(',').Append(Float(bounds.size.y)).Append(',').Append(Float(bounds.size.z));
                    break;
                case SerializedPropertyType.Quaternion:
                    Quaternion quaternion = property.quaternionValue;
                    text.Append(Float(quaternion.x)).Append(',').Append(Float(quaternion.y)).Append(',').Append(Float(quaternion.z)).Append(',').Append(Float(quaternion.w));
                    break;
                case SerializedPropertyType.ExposedReference: text.Append(StableObjectReference(property.exposedReferenceValue)); break;
                case SerializedPropertyType.Vector2Int:
                    Vector2Int vector2Int = property.vector2IntValue;
                    text.Append(vector2Int.x).Append(',').Append(vector2Int.y);
                    break;
                case SerializedPropertyType.Vector3Int:
                    Vector3Int vector3Int = property.vector3IntValue;
                    text.Append(vector3Int.x).Append(',').Append(vector3Int.y).Append(',').Append(vector3Int.z);
                    break;
                case SerializedPropertyType.RectInt:
                    RectInt rectInt = property.rectIntValue;
                    text.Append(rectInt.x).Append(',').Append(rectInt.y).Append(',').Append(rectInt.width).Append(',').Append(rectInt.height);
                    break;
                case SerializedPropertyType.BoundsInt:
                    BoundsInt boundsInt = property.boundsIntValue;
                    text.Append(boundsInt.position.x).Append(',').Append(boundsInt.position.y).Append(',').Append(boundsInt.position.z).Append(';')
                        .Append(boundsInt.size.x).Append(',').Append(boundsInt.size.y).Append(',').Append(boundsInt.size.z);
                    break;
                case SerializedPropertyType.ManagedReference: text.Append(property.managedReferenceFullTypename); break;
                case SerializedPropertyType.FixedBufferSize: text.Append(property.fixedBufferSize); break;
                case SerializedPropertyType.Hash128: text.Append(property.hash128Value); break;
            }
            text.Append('\n');
        }

        private static string StableObjectReference(UnityEngine.Object value)
        {
            if (value == null) return "null";
            string assetPath = AssetDatabase.GetAssetPath(value);
            string guid;
            long localId;
            if (!string.IsNullOrEmpty(assetPath) && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out localId))
                return "asset:" + guid + ":" + localId.ToString(Invariant);

            GameObject gameObject = value as GameObject;
            Component component = value as Component;
            if (component != null) gameObject = component.gameObject;
            if (gameObject != null)
            {
                if (component == null) return "scene:GameObject:" + HierarchyPath(gameObject.transform);
                Component[] sameType = component.GetComponents(component.GetType());
                int ordinal = Array.IndexOf(sameType, component);
                return "scene:" + HierarchyPath(component.transform) + ":" + component.GetType().FullName + ":" + ordinal;
            }
            return "object:" + value.GetType().FullName + ":" + value.name;
        }

        private static string Float(float value)
        {
            return value.ToString("R", Invariant);
        }

        private static bool NoMissingScripts(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .All(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0);
        }

        private static void ValidateGeometry(MeshRecord record)
        {
            Require(record != null && !string.IsNullOrEmpty(record.name), "Unnamed optimized mesh.");
            Require(record.vertices != null && record.vertices.Length > 0 && record.vertices.Length % 3 == 0 && record.vertices.All(Finite), "Invalid vertices: " + record.name);
            Require(record.normals != null && record.normals.Length == record.vertices.Length && record.normals.All(Finite), "Invalid normals: " + record.name);
            Require(record.triangles != null && record.triangles.Length > 0 && record.triangles.Length % 3 == 0, "Invalid triangles: " + record.name);
            int count = record.vertices.Length / 3;
            Require(record.triangles.All(i => i >= 0 && i < count), "Triangle index outside mesh: " + record.name);
            Vector3[] vertices = Vectors(record.vertices), normals = Vectors(record.normals);
            Require(normals.All(n => n.sqrMagnitude > .25f && n.sqrMagnitude < 2f), "Degenerate normals: " + record.name);
            for (int i = 0; i < record.triangles.Length; i += 3)
            {
                Vector3 a = vertices[record.triangles[i]], b = vertices[record.triangles[i + 1]], c = vertices[record.triangles[i + 2]];
                Require(Vector3.Cross(b - a, c - a).sqrMagnitude > 1e-24f, "Degenerate face: " + record.name);
            }
        }

        private static void ValidateBounds(MeshRecord original, MeshRecord edited)
        {
            Vector3[] source = Vectors(original.vertices), target = Vectors(edited.vertices);
            var sourceBounds = new Bounds(source[0], Vector3.zero);
            var targetBounds = new Bounds(target[0], Vector3.zero);
            foreach (Vector3 vertex in source) sourceBounds.Encapsulate(vertex);
            foreach (Vector3 vertex in target) targetBounds.Encapsulate(vertex);
            float tolerance = Mathf.Max(.002f, sourceBounds.size.magnitude * .03f);
            Require(Vector3.Distance(sourceBounds.center, targetBounds.center) <= tolerance &&
                Vector3.Distance(sourceBounds.size, targetBounds.size) <= tolerance,
                "Optimized mesh changed metre scale, axes or pivot: " + edited.name);
        }

        private static Vector3[] Vectors(float[] values)
        {
            var result = new Vector3[values.Length / 3];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3(values[i * 3], values[i * 3 + 1], values[i * 3 + 2]);
            return result;
        }
        private static Vector2[] Uvs(float[] values)
        {
            var result = new Vector2[values.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector2(values[i * 2], values[i * 2 + 1]);
            return result;
        }
        private static float[] Flatten(Matrix4x4 matrix)
        {
            var result = new float[16];
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) result[row * 4 + column] = matrix[row, column];
            return result;
        }
        private static Matrix4x4 Matrix(float[] values)
        {
            var matrix = Matrix4x4.identity;
            for (int row = 0; row < 4; row++) for (int column = 0; column < 4; column++) matrix[row, column] = values[row * 4 + column];
            return matrix;
        }
        private static string HierarchyPath(Transform transform)
        {
            var segments = new List<string>();
            while (transform != null) { segments.Add(transform.name + "[" + transform.GetSiblingIndex() + "]"); transform = transform.parent; }
            segments.Reverse(); return string.Join("/", segments);
        }
        private static string SafeName(string value)
        {
            return new string(value.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
        }
        private static void EnsureAssetFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/')); EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        private static GeometryDocument ReadDocument(string path)
        {
            Require(File.Exists(path), "Required modeling artifact missing: " + path);
            GeometryDocument document = JsonUtility.FromJson<GeometryDocument>(File.ReadAllText(path));
            Require(document != null, "Invalid modeling document: " + path); return document;
        }
        private static void WriteJson(string path, object data)
        {
            File.WriteAllText(path, JsonUtility.ToJson(data, true), new UTF8Encoding(false));
        }
        private static bool Finite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("ASSIGNMENT02: " + message);
        }
    }
}
