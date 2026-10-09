using System.Collections.Generic;
using System.IO;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Remains.Editor
{
    /// <summary>
    /// Shared helpers for the content builders: paths, terrain height, materials and primitive construction.
    /// </summary>
    public static class BuildKit
    {
        public const string Root = "Assets/Remains";
        public const string MaterialsDir = Root + "/Materials";
        public const string MeshesDir = Root + "/Meshes";
        public const string PrefabsDir = Root + "/Prefabs";
        public const string ItemsDir = PrefabsDir + "/Items";
        public const string CameraDir = PrefabsDir + "/Camera";
        public const string DataDir = Root + "/Data";
        public const string KitsDir = DataDir + "/Kits";
        public const string TexturesDir = Root + "/Textures";
        public const string ScenePath = Root + "/Scenes/Remains_Castle.unity";

        public const int GroundLayer = 8;
        public const int DebrisLayer = 9;

        public const float PlateauHeight = 8f;
        public const float PlateauRadius = 25f;
        public const float CastleHalfSize = 16f;
        public static readonly Vector3 CampCenter = new Vector3(0f, 0f, -100f);
        public const float CampRadius = 34f;

        // Battle map area baked onto the planning tables (world XZ rectangle).
        public static readonly Rect MapArea = new Rect(-90f, -130f, 180f, 180f);

        #region Terrain

        /// <summary>Plateau for the castle, gentle west slope, steep north, a ramp road to the gate, flat camp.</summary>
        public static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float dirX = r > 0.01f ? x / r : 0f;
            float dirZ = r > 0.01f ? z / r : 0f;

            float outer = 52f + 16f * Mathf.Max(0f, -dirX) - 14f * Mathf.Max(0f, dirZ);
            float t = Mathf.Clamp01((r - PlateauRadius) / (outer - PlateauRadius));
            float hill = PlateauHeight * (1f - t * t * (3f - 2f * t));

            float noise = 0f;
            if (r > PlateauRadius + 1.5f)
            {
                noise = (Mathf.PerlinNoise(x * 0.07f + 13f, z * 0.07f + 7f) - 0.5f) * 1.0f;
                noise += Mathf.Clamp01((r - 85f) / 30f) * Mathf.PerlinNoise(x * 0.025f, z * 0.025f) * 7f;
            }

            // Straight ramp road from the gate down towards the camp.
            float roadWidth = Mathf.Clamp01((Mathf.Abs(x) - 5f) / 6f);
            if (z < -PlateauRadius + 2f && z > -72f)
            {
                float roadT = Mathf.InverseLerp(-PlateauRadius + 2f, -72f, z);
                float road = Mathf.Lerp(PlateauHeight, 0f, roadT);
                hill = Mathf.Lerp(road, hill, roadWidth);
                noise *= roadWidth;
            }

            float campDistance = Vector2.Distance(new Vector2(x, z), new Vector2(CampCenter.x, CampCenter.z));
            float campFlatten = Mathf.Clamp01((campDistance - CampRadius) / 16f);
            return hill + noise * campFlatten;
        }

        public static Vector3 OnGround(float x, float z, float yOffset = 0f) => new Vector3(x, Height(x, z) + yOffset, z);

        #endregion

        #region Assets

        public static void EnsureFolders()
        {
            foreach (var dir in new[] { MaterialsDir, MeshesDir, ItemsDir, CameraDir, DataDir, KitsDir, TexturesDir, Root + "/Scenes" })
            {
                if (!AssetDatabase.IsValidFolder(dir)) Directory.CreateDirectory(dir);
            }
            AssetDatabase.Refresh();
        }

        public static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
            var layers = tagManager.FindProperty("layers");
            layers.GetArrayElementAtIndex(GroundLayer).stringValue = "Ground";
            layers.GetArrayElementAtIndex(DebrisLayer).stringValue = "Debris";
            tagManager.ApplyModifiedProperties();
        }

        public static Mesh SaveMesh(Mesh mesh)
        {
            string path = $"{MeshesDir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Mesh s_Cube;
        public static Mesh Cube => s_Cube != null ? s_Cube : (s_Cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx"));

        #endregion

        #region Materials

        private static readonly Dictionary<string, Material> s_Materials = new Dictionary<string, Material>();

        public static Material M(string name)
        {
            if (s_Materials.TryGetValue(name, out var material) && material != null) return material;
            material = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/{name}.mat");
            s_Materials[name] = material;
            return material;
        }

        public static Material Mat(string name, string hex, float smoothness = 0.05f, float metallic = 0f, bool doubleSided = false, float emission = 0f)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            ColorUtility.TryParseHtmlString(hex, out var color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Cull", doubleSided ? 0f : 2f);
            material.doubleSidedGI = doubleSided;
            if (emission > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            s_Materials[name] = material;
            return material;
        }

        public static Material MatTransparent(string name, Color color)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Cull", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            s_Materials[name] = material;
            return material;
        }

        public static Material MatUnlitTexture(string name, Texture texture)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            s_Materials[name] = material;
            return material;
        }

        public static void CreateMaterials()
        {
            s_Materials.Clear();
            Mat("Grass", "#6FA35A");
            Mat("GrassDark", "#5C8F4A");
            Mat("Field", "#C9B45A");
            Mat("Dirt", "#8A7250");
            Mat("Cobble", "#8F8A80");
            Mat("Brick1", "#9A9489");
            Mat("Brick2", "#8C867B");
            Mat("Brick3", "#A69F92");
            Mat("BrickDark", "#6F6A62");
            Mat("RuinStone", "#8C8F99");
            Mat("Wood", "#9C6B3F");
            Mat("WoodDark", "#6B4A2E");
            Mat("WoodLight", "#B88A55");
            Mat("Stone", "#9EA1A8");
            Mat("Metal", "#A7AEB5", 0.45f, 0.6f);
            Mat("MetalDark", "#4A4F55", 0.35f, 0.7f);
            Mat("Iron", "#3A3D42", 0.4f, 0.8f);
            Mat("Sand", "#C9B27C");
            Mat("Hay", "#D9BE5C");
            Mat("BarrelRed", "#B8412E");
            Mat("Band", "#3A3A3A");
            Mat("Plaster", "#E6DCC5");
            Mat("RoofRed", "#A8452F");
            Mat("RoofBlue", "#3E5E9E");
            Mat("RoofSlate", "#5A6070");
            Mat("Leaves", "#4E8A3E");
            Mat("LeavesLight", "#6AA84F");
            Mat("Rock", "#7D7F86");
            Mat("Water", "#4F87B8", 0.8f);
            Mat("Skin", "#E0B08A");
            Mat("Beard", "#5A3A22");
            Mat("Apron", "#5E4632");
            Mat("Shirt", "#8E3B2E");
            Mat("Rope", "#C2A878");
            Mat("Gold", "#D4A640", 0.5f, 0.8f);
            Mat("DefenderBanner", "#3B7BD9", 0.05f, 0f, true);
            Mat("AttackerBanner", "#E8792B", 0.05f, 0f, true);
            Mat("ClothOrange", "#D9702A", 0.05f, 0f, true);
            Mat("ClothCream", "#EFE3C6", 0.05f, 0f, true);
            Mat("ClothRed", "#A8322A", 0.05f, 0f, true);
            Mat("Fire", "#FF8A2A", 0f, 0f, false, 4f);
            Mat("Spark", "#FFD27A", 0f, 0f, false, 6f);
            Mat("Coal", "#FF4A1A", 0f, 0f, false, 2.5f);
            Mat("WindowDark", "#2A2A33");
            Mat("Dust", "#B7AE9E");
            Mat("RoyalPurple", "#6A2C8C", 0.25f);
            Mat("RoyalLight", "#8E4FB5", 0.25f);
            Mat("Lace", "#F4F1EA");
            Mat("HairGold", "#C9A04A");
            Mat("Ermine", "#F7F7F2");
            Mat("Lips", "#B8303A");
            Mat("Eyes", "#1E2A44");
            Mat("GemRed", "#D22E3A", 0.8f, 0f, false, 1.5f);
            Mat("GemBlue", "#2E5ED2", 0.8f, 0f, false, 1.5f);
            Mat("GemGreen", "#2EA85A", 0.8f, 0f, false, 1.5f);
            Mat("GoldBright", "#F2C94C", 0.7f, 0.9f, false, 0.4f);
            MatTransparent("AttackerBarrier", new Color(0.91f, 0.47f, 0.17f, 0.14f));
        }

        #endregion

        #region Construction

        public static GameObject Box(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material, bool collider = true, Quaternion? rotation = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = Cube;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            if (collider) go.AddComponent<BoxCollider>();
            return go;
        }

        public static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 scale, bool meshCollider = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            if (meshCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        public static Transform Group(Transform parent, string name, Vector3 position, Quaternion? rotation = null)
        {
            var go = new GameObject(name).transform;
            go.SetParent(parent, false);
            go.localPosition = position;
            go.localRotation = rotation ?? Quaternion.identity;
            return go;
        }

        public static WindCloth Cloth(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, float width, float height, Material material, WindCloth.PinnedEdge edge, float amplitude, int columns = 8, int rows = 4, float frequency = 2.2f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var cloth = go.AddComponent<WindCloth>();
            cloth.Configure(width, height, columns, rows, edge, amplitude, frequency);
            return cloth;
        }

        /// <summary>Makes an in-scene network object owned by the session owner.</summary>
        public static NetworkObject SceneNetworkObject(GameObject go)
        {
            var networkObject = go.GetComponent<NetworkObject>() ?? go.AddComponent<NetworkObject>();
            var so = new SerializedObject(networkObject);
            so.FindProperty("Ownership").intValue = (int)NetworkObject.OwnershipStatus.SessionOwner;
            so.ApplyModifiedPropertiesWithoutUndo();
            return networkObject;
        }

        public static void AddPointLight(Transform parent, Vector3 localPosition, Color color, float range, float intensity)
        {
            var go = new GameObject("Light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            go.AddComponent<FlickerLight>();
        }

        #endregion
    }
}
