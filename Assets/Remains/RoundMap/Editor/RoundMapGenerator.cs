using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Remains.Editor
{
    /// <summary>
    /// Settings of the round island map. Edit them in the Remains/Round Map window and press Generate.
    /// </summary>
    [System.Serializable]
    public class RoundMapSettings
    {
        public int Seed = 12;
        [Tooltip("Terrain square side, metres")] public float TerrainSize = 640f;
        [Tooltip("Average radius of the playable plateau, metres")] public float PlateauRadius = 282f;
        [Tooltip("Plateau ground level, metres")] public float GroundHeight = 42f;
        [Tooltip("Height of the hills on the plateau, metres")] public float HillHeight = 7f;
        [Tooltip("Water level of the river, metres")] public float WaterLevel = 37f;
        [Tooltip("Lava level at the bottom of the cliff, metres")] public float LavaLevel = 6f;
        [Tooltip("Average radius of the river loop around the castle, metres")] public float RiverRadius = 128f;
        [Tooltip("Average radius of the castle wall ring, metres")] public float CastleRadius = 70f;
        [Tooltip("Number of castle corners (towers)")] public int CastleCorners = 9;
        [Tooltip("How far each castle corner may stray from the circle, metres")] public float CastleIrregularity = 13f;
        public int ForestTrees = 420;
        public int ScatteredTrees = 140;
        [Range(0, 16)] public int GrassDensity = 7;
        public int CliffRocks = 220;
    }

    /// <summary>
    /// Builds Remains_RoundMap: a round plateau on a rocky cliff above lava, a river loop around an
    /// irregular castle, a winding path to the gate, forests, grass and the attacker camps.
    /// Uses the map kit models from Assets/Remains/MapKit and the rock meshes from Assets/Remains/Meshes.
    /// </summary>
    public static class RoundMapGenerator
    {
        private const string k_Root = "Assets/Remains/RoundMap";
        private const string k_KitModels = "Assets/Remains/MapKit/Models/";
        private const string k_ScenePath = "Assets/Remains/Scenes/Remains_RoundMap.unity";
        private const int k_HeightRes = 513;
        private const int k_SplatRes = 512;
        private const float k_MaxHeight = 140f;

        private static RoundMapSettings s;
        private static System.Random s_Rng;
        private static List<Vector2> s_River;
        private static List<Vector2> s_Path;
        private static Vector2[] s_Castle;
        private static Vector2 s_GatePoint, s_GateDir;
        private static float[] s_RimNoise;

        private static float Rand() => (float)s_Rng.NextDouble();
        private static float Rand(float a, float b) => a + (b - a) * Rand();

        // ---------------------------------------------------------------- shape functions

        private static float RimRadius(float angle)
        {
            return s.PlateauRadius + 10f * Mathf.Sin(7f * angle + 1f) + 6f * Mathf.Sin(13f * angle + 0.3f) + 4f * Mathf.Sin(23f * angle + 2f);
        }

        private static float Fbm(float x, float z, int octaves, float scale)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += (Mathf.PerlinNoise(x * scale + 100f + s.Seed * 17.3f, z * scale + 300f) * 2f - 1f) * amp;
                norm += amp;
                amp *= 0.5f;
                scale *= 2.03f;
            }
            return sum / norm;
        }

        private static float Ridged(float x, float z, float scale)
        {
            float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * scale + 50f, z * scale + s.Seed) * 2f - 1f);
            return n * n;
        }

        private static float DistanceToPolyline(List<Vector2> line, Vector2 p, float limit)
        {
            float best = limit * limit;
            for (int i = 0; i < line.Count; i++)
            {
                float dx = line[i].x - p.x, dz = line[i].y - p.y;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        private static bool InsideCastle(Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = s_Castle.Length - 1; i < s_Castle.Length; j = i++)
            {
                var a = s_Castle[i]; var b = s_Castle[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside;
        }

        private static float DistanceToCastleEdge(Vector2 p)
        {
            float best = float.MaxValue;
            for (int i = 0; i < s_Castle.Length; i++)
            {
                var a = s_Castle[i]; var b = s_Castle[(i + 1) % s_Castle.Length];
                var ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return InsideCastle(p) ? -best : best;
        }

        private static void BuildLayout()
        {
            // Irregular castle outline: corners on a jittered circle.
            s_Castle = new Vector2[s.CastleCorners];
            float step = Mathf.PI * 2f / s.CastleCorners;
            float start = -Mathf.PI / 2f + step / 2f; // the south side is an edge, not a corner: the gate sits there
            for (int i = 0; i < s.CastleCorners; i++)
            {
                float a = start + i * step + Rand(-0.22f, 0.22f) * step;
                float r = s.CastleRadius + Rand(-s.CastleIrregularity, s.CastleIrregularity);
                s_Castle[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            }
            // Gate: middle of the edge that crosses the south direction.
            for (int i = 0; i < s_Castle.Length; i++)
            {
                var a = s_Castle[i]; var b = s_Castle[(i + 1) % s_Castle.Length];
                if (a.y < 0f && b.y < 0f && Mathf.Sign(a.x) != Mathf.Sign(b.x))
                {
                    s_GatePoint = (a + b) * 0.5f;
                    var edge = (b - a).normalized;
                    s_GateDir = new Vector2(edge.y, -edge.x); // outward normal
                    if (Vector2.Dot(s_GateDir, s_GatePoint) < 0f) s_GateDir = -s_GateDir;
                }
            }

            // River: a wobbly loop around the castle plus an inflow (north-west) and an outflow (north-east) to the cliff.
            s_River = new List<Vector2>();
            for (int i = 0; i < 420; i++)
            {
                float a = i / 420f * Mathf.PI * 2f;
                float r = s.RiverRadius + 12f * Mathf.Sin(3f * a + 0.7f) + 7f * Mathf.Sin(5f * a + 2f) + 4f * Mathf.Sin(9f * a);
                s_River.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
            foreach (var angle in new[] { 2.45f, 0.75f })
            {
                for (int i = 0; i <= 120; i++)
                {
                    float t = i / 120f;
                    float r = Mathf.Lerp(s.RiverRadius + 8f, RimRadius(angle) + 30f, t);
                    float a = angle + 0.18f * Mathf.Sin(t * 9f) * t;
                    s_River.Add(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }

            // Winding path from the southern rim to the gate.
            s_Path = new List<Vector2>();
            var from = new Vector2(10f, -RimRadius(-Mathf.PI / 2f) + 10f);
            var to = s_GatePoint + s_GateDir * 6f;
            for (int i = 0; i <= 260; i++)
            {
                float t = i / 260f;
                var p = Vector2.Lerp(from, to, t);
                var side = new Vector2(1f, 0f);
                p += side * (22f * Mathf.Sin(t * Mathf.PI * 2.6f + 0.6f) + 7f * Mathf.Sin(t * Mathf.PI * 7f)) * (1f - t * t);
                s_Path.Add(p);
            }
        }

        // ---------------------------------------------------------------- heights

        private static float Height(Vector2 p, float riverDist, float pathDist, out float cliff)
        {
            float r = p.magnitude;
            float angle = Mathf.Atan2(p.y, p.x);
            float rim = RimRadius(angle);

            float h = s.GroundHeight + s.HillHeight * Fbm(p.x, p.y, 4, 0.006f);
            h += 3f * Mathf.Max(0f, Fbm(p.x + 400f, p.y, 3, 0.02f)); // small lumps
            // Hills rise in the western forest.
            h += 6f * Mathf.Clamp01(-p.x / 200f) * (0.5f + 0.5f * Fbm(p.x, p.y + 800f, 3, 0.01f));
            // Castle mound: uneven, but raised above the river.
            float edge = DistanceToCastleEdge(p);
            float mound = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((edge + 5f) / 30f));
            h = Mathf.Lerp(h, s.GroundHeight + 4f + 2.5f * Fbm(p.x, p.y, 3, 0.03f), mound * 0.8f);
            h = Mathf.Max(h, s.WaterLevel + 1.2f);

            // River channel.
            float river = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((riverDist - 6f) / 16f));
            h = Mathf.Lerp(h, s.WaterLevel - 3.5f, river);
            // Path: a shallow worn track.
            float path = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((pathDist - 2.5f) / 3f));
            h -= 0.45f * path * (1f - river);

            // Cliff: falls from the rim to the bottom, with rocky ledges.
            float t = Mathf.Clamp01((r - rim + 6f) / 26f);
            cliff = t;
            float ledges = 9f * Ridged(p.x, p.y, 0.045f) + 5f * Ridged(p.x + 30f, p.y, 0.11f) + 2f * Ridged(p.x, p.y + 70f, 0.3f);
            float fall = Mathf.SmoothStep(0f, 1f, t);
            h = Mathf.Lerp(h, 0f, fall) + ledges * Mathf.Sin(Mathf.PI * t) * 1.3f;
            // Stepped rock ledges on the cliff face.
            float step = 6f + 3f * Mathf.PerlinNoise(p.x * 0.02f, p.y * 0.02f);
            float terraced = Mathf.Floor(h / step) * step + step * Mathf.Pow(Mathf.Repeat(h, step) / step, 3f);
            h = Mathf.Lerp(h, terraced, 0.7f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.2f)));
            // Rim lip: slightly raised edge before the drop.
            h += 1.5f * Mathf.Exp(-Mathf.Pow((r - rim + 8f) / 5f, 2f));
            return Mathf.Max(0f, h);
        }

        // ---------------------------------------------------------------- textures

        private static float TileNoise(float x, float y, float period, float scale, float seed)
        {
            float N(float a, float b) => Mathf.PerlinNoise(a * scale + seed, b * scale + seed * 0.37f);
            float u = x / period, v = y / period;
            return (1 - u) * (1 - v) * N(x, y) + u * (1 - v) * N(x - period, y) + (1 - u) * v * N(x, y - period) + u * v * N(x - period, y - period);
        }

        private static Texture2D SaveTexture(string name, int size, System.Func<float, float, Color> pixel, bool alpha = false, bool clamp = false)
        {
            var tex = new Texture2D(size, size, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = pixel(x, y);
            tex.SetPixels(pixels);
            tex.Apply();
            string path = $"{k_Root}/Textures/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.alphaIsTransparency = alpha;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static readonly Dictionary<int, Vector2[]> s_CellPoints = new Dictionary<int, Vector2[]>();

        private static float Cells(float x, float y, float period, int count, int seed, out float edge)
        {
            // Tileable Voronoi: distance to nearest and second nearest jittered point.
            if (!s_CellPoints.TryGetValue(seed, out var pts))
            {
                var rng = new System.Random(seed);
                pts = new Vector2[count];
                for (int i = 0; i < count; i++) pts[i] = new Vector2((float)rng.NextDouble() * period, (float)rng.NextDouble() * period);
                s_CellPoints[seed] = pts;
            }
            float d1 = float.MaxValue, d2 = float.MaxValue;
            foreach (var q in pts)
                for (int ox = -1; ox <= 1; ox++)
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        float dx = q.x + ox * period - x, dy = q.y + oy * period - y;
                        float d = dx * dx + dy * dy;
                        if (d < d1) { d2 = d1; d1 = d; } else if (d < d2) d2 = d;
                    }
            edge = Mathf.Sqrt(d2) - Mathf.Sqrt(d1);
            return Mathf.Sqrt(d1);
        }

        private static Dictionary<string, Texture2D> BuildTextures()
        {
            const int n = 512;
            var t = new Dictionary<string, Texture2D>();
            t["Grass"] = SaveTexture("Grass", n, (x, y) =>
            {
                float a = TileNoise(x, y, n, 0.02f, 1f), b = TileNoise(x, y, n, 0.09f, 7f), c = TileNoise(x, y, n, 0.35f, 3f);
                var col = Color.Lerp(new Color(0.30f, 0.50f, 0.17f), new Color(0.47f, 0.64f, 0.24f), a * 0.7f + b * 0.3f);
                return Color.Lerp(col, new Color(0.56f, 0.70f, 0.30f), Mathf.Clamp01((c - 0.62f) * 4f));
            });
            t["GrassDark"] = SaveTexture("GrassDark", n, (x, y) =>
            {
                float a = TileNoise(x, y, n, 0.025f, 61f), b = TileNoise(x, y, n, 0.12f, 9f);
                return Color.Lerp(new Color(0.20f, 0.38f, 0.13f), new Color(0.33f, 0.50f, 0.18f), a * 0.6f + b * 0.4f);
            });
            t["Dirt"] = SaveTexture("Dirt", n, (x, y) =>
            {
                float a = TileNoise(x, y, n, 0.03f, 11f), b = TileNoise(x, y, n, 0.25f, 5f);
                var col = Color.Lerp(new Color(0.45f, 0.33f, 0.21f), new Color(0.62f, 0.49f, 0.33f), a);
                return Color.Lerp(col, new Color(0.70f, 0.64f, 0.55f), Mathf.Clamp01((b - 0.7f) * 5f)); // pebbles
            });
            t["Rock"] = SaveTexture("Rock", n, (x, y) =>
            {
                float cell = Cells(x, y, n, 26, 9, out float edge);
                float a = TileNoise(x, y, n, 0.04f, 21f), b = TileNoise(x, y, n, 0.2f, 13f);
                var col = Color.Lerp(new Color(0.33f, 0.31f, 0.30f), new Color(0.56f, 0.53f, 0.49f), a * 0.6f + b * 0.4f);
                col *= 0.85f + 0.15f * Mathf.Clamp01(cell / 60f);
                return Color.Lerp(new Color(0.16f, 0.15f, 0.15f), col, Mathf.Clamp01(edge / 6f)); // dark cracks
            });
            t["Cobble"] = SaveTexture("Cobble", n, (x, y) =>
            {
                Cells(x, y, n, 90, 4, out float edge);
                float a = TileNoise(x, y, n, 0.08f, 31f);
                var col = Color.Lerp(new Color(0.48f, 0.46f, 0.43f), new Color(0.66f, 0.63f, 0.58f), a);
                return Color.Lerp(new Color(0.30f, 0.27f, 0.23f), col, Mathf.Clamp01(edge / 4f));
            });
            t["Mud"] = SaveTexture("Mud", n, (x, y) =>
            {
                float a = TileNoise(x, y, n, 0.04f, 41f), b = TileNoise(x, y, n, 0.3f, 2f);
                return Color.Lerp(new Color(0.24f, 0.22f, 0.14f), new Color(0.40f, 0.38f, 0.25f), a * 0.7f + b * 0.3f);
            });
            t["Lava"] = SaveTexture("Lava", n, (x, y) =>
            {
                Cells(x, y, n, 40, 77, out float edge);
                float a = TileNoise(x, y, n, 0.03f, 51f);
                float glow = Mathf.Clamp01(1f - edge / 9f) * (0.6f + 0.4f * a);
                var crust = Color.Lerp(new Color(0.08f, 0.05f, 0.04f), new Color(0.22f, 0.09f, 0.04f), a);
                return Color.Lerp(crust, Color.Lerp(new Color(1f, 0.35f, 0.02f), new Color(1f, 0.85f, 0.25f), glow * glow), glow);
            });
            // Grass clump billboard: tapered blades with alpha.
            t["GrassBlades"] = SaveTexture("GrassBlades", 256, (x, y) => GrassBladePixel(x / 256f, y / 256f), true, true);
            return t;
        }

        private static Color GrassBladePixel(float u, float v)
        {
            var rng = new System.Random(5);
            Color result = new Color(0, 0, 0, 0);
            for (int i = 0; i < 26; i++)
            {
                float baseX = 0.08f + (float)rng.NextDouble() * 0.84f;
                float height = 0.45f + (float)rng.NextDouble() * 0.53f;
                float lean = ((float)rng.NextDouble() - 0.5f) * 0.35f;
                float width = 0.025f + (float)rng.NextDouble() * 0.02f;
                if (v > height) continue;
                float t = v / height;
                float cx = baseX + lean * t * t;
                if (Mathf.Abs(u - cx) < width * (1f - t))
                {
                    var tip = new Color(0.62f, 0.78f, 0.32f);
                    var root = new Color(0.22f, 0.40f, 0.12f);
                    var c = Color.Lerp(root, tip, t);
                    c.a = 1f;
                    result = c;
                }
            }
            return result;
        }

        private static TerrainLayer Layer(string name, Texture2D tex, float tile, float smooth = 0f)
        {
            string path = $"{k_Root}/Layers/{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, path);
            }
            layer.diffuseTexture = tex;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = smooth;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Material SaveMaterial(string name, Material material)
        {
            string path = $"{k_Root}/Materials/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.CopyPropertiesFromMaterial(material);
                existing.shader = material.shader;
                existing.shaderKeywords = material.shaderKeywords;
                existing.globalIlluminationFlags = material.globalIlluminationFlags;
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---------------------------------------------------------------- tree prefabs

        private static GameObject TreePrefab(string model, float height, Material material)
        {
            string path = $"{k_Root}/Prefabs/{model}.prefab";
            var source = AssetDatabase.LoadAllAssetsAtPath(k_KitModels + model + ".glb");
            Mesh mesh = null;
            foreach (var o in source) if (o is Mesh m) { mesh = m; break; }

            // Terrain trees take the mesh from the prefab root, so bake the scale into a mesh copy.
            var baked = Object.Instantiate(mesh);
            var verts = baked.vertices;
            var b = mesh.bounds;
            float k = height / b.size.y;
            for (int i = 0; i < verts.Length; i++) verts[i] = new Vector3((verts[i].x - b.center.x) * k, (verts[i].y - b.min.y) * k, (verts[i].z - b.center.z) * k);
            baked.vertices = verts;
            baked.RecalculateBounds();
            baked = SaveMesh(baked, $"{k_Root}/Prefabs/{model}_mesh.asset");

            var go = new GameObject(model);
            go.AddComponent<MeshFilter>().sharedMesh = baked;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            var col = go.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, height * 0.5f, 0);
            col.radius = height * 0.06f;
            col.height = height;
            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static GameObject GrassPrefab(Texture2D blades)
        {
            // Three crossed quads with the blade texture: a grass clump rendered as an instanced detail mesh.
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>(); var normals = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                float a = i * Mathf.PI / 3f;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.5f;
                int b = verts.Count;
                verts.AddRange(new[] { -dir, dir, dir + Vector3.up, -dir + Vector3.up });
                uvs.AddRange(new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) });
                normals.AddRange(new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up }); // up-facing normals light the clump evenly
                tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
            }
            var mesh = new Mesh { name = "GrassClump" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            mesh = SaveMesh(mesh, $"{k_Root}/Prefabs/GrassClump_mesh.asset");

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetTexture("_BaseMap", blades);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0f);
            mat.SetFloat("_Smoothness", 0f);
            mat.enableInstancing = true;
            mat = SaveMaterial("Grass_Clump", mat);

            var go = new GameObject("GrassClump");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            string prefabPath = $"{k_Root}/Prefabs/GrassClump.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        }

        // ---------------------------------------------------------------- scene objects

        private static Transform s_SceneRoot;
        private static Terrain s_Terrain;
        private static Material s_KitMaterial;
        private static readonly Dictionary<string, Transform> s_Groups = new Dictionary<string, Transform>();

        private static Transform Group(string path)
        {
            if (s_Groups.TryGetValue(path, out var t)) return t;
            int idx = path.LastIndexOf('/');
            var parent = idx >= 0 ? Group(path.Substring(0, idx)) : s_SceneRoot;
            t = new GameObject(path.Substring(idx + 1)).transform;
            t.SetParent(parent, false);
            s_Groups[path] = t;
            return t;
        }

        private static float Ground(Vector2 p) => s_Terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + s_Terrain.transform.position.y;

        private static GameObject Place(string model, string group, Vector2 p, float yaw, Vector3 scale, float yOffset = 0f, float? y = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(k_KitModels + model + ".glb");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Group(group));
            go.transform.position = new Vector3(p.x, (y ?? Ground(p)) + yOffset, p.y);
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.sharedMaterial = s_KitMaterial;
                if (r.GetComponent<MeshCollider>() == null) r.gameObject.AddComponent<MeshCollider>();
            }
            go.isStatic = true;
            return go;
        }

        private static GameObject Place(string model, string group, Vector2 p, float yaw, float scale, float yOffset = 0f) =>
            Place(model, group, p, yaw, Vector3.one * scale, yOffset);

        private static float YawOf(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        private static void BuildCastle()
        {
            const float wallScaleOverlap = 1.12f;
            float LowGround(Vector2 a, Vector2 b) => Mathf.Min(Ground(a), Mathf.Min(Ground(b), Ground((a + b) * 0.5f)));

            int north = 0;
            for (int i = 1; i < s_Castle.Length; i++) if (s_Castle[i].y > s_Castle[north].y) north = i;

            for (int i = 0; i < s_Castle.Length; i++)
            {
                var a = s_Castle[i]; var b = s_Castle[(i + 1) % s_Castle.Length];
                var dir = (b - a).normalized;
                float len = (b - a).magnitude;
                int count = Mathf.Max(1, Mathf.RoundToInt(len / 20f));
                float seg = len / count;
                bool gateEdge = (a + b) * 0.5f == s_GatePoint;
                for (int k = 0; k < count; k++)
                {
                    var p0 = a + dir * (k * seg); var p1 = a + dir * ((k + 1) * seg);
                    var mid = (p0 + p1) * 0.5f;
                    if (gateEdge && Vector2.Distance(mid, s_GatePoint) < seg * 0.9f) continue; // gatehouse goes here
                    float yaw = YawOf(dir);
                    var wall = Place(count > 1 && k == count / 2 && i % 4 == 2 ? "Wall_Breach" : "Wall_Straight", "Castle/Walls", mid, yaw,
                        seg * wallScaleOverlap, 0f);
                    wall.transform.position = new Vector3(mid.x, LowGround(p0, p1) - 1.5f, mid.y);
                    if (wall.name.StartsWith("Wall_Breach")) wall.transform.localScale = Vector3.one * seg * 0.95f;
                }
            }
            for (int i = 0; i < s_Castle.Length; i++)
            {
                var c = s_Castle[i];
                var tower = Place(i == north ? "Tower_North_Signal" : "Tower_Corner", "Castle/Towers", c, Rand(0f, 360f), Rand(19f, 23f), -1.5f);
                tower.name = $"Tower_{i}";
            }

            // Gatehouse: towers on both sides, barbican, gate, portcullis and the bridge over the river.
            var along = new Vector2(-s_GateDir.y, s_GateDir.x);
            float gateYaw = YawOf(s_GateDir);
            Place("Tower_Gate", "Castle/Gate", s_GatePoint + along * 18f, gateYaw, 21f, -1.5f);
            Place("Tower_Gate", "Castle/Gate", s_GatePoint - along * 18f, gateYaw, 21f, -1.5f);
            Place("Barbican", "Castle/Gate", s_GatePoint, gateYaw, 18f, -1f);
            Place("Gate_Main", "Castle/Gate", s_GatePoint - s_GateDir * 9f, gateYaw, 24f, -0.5f);
            Place("Portcullis", "Castle/Gate", s_GatePoint + s_GateDir * 4f, gateYaw + 90f, 20f, -0.5f);
            Place("Torch_Wall", "Castle/Gate", s_GatePoint + s_GateDir * 9f + along * 6f, gateYaw, 3f, 2.5f);
            Place("Torch_Wall", "Castle/Gate", s_GatePoint + s_GateDir * 9f - along * 6f, gateYaw, 3f, 2.5f);

            // Courtyard: keep in the middle, square with a fountain towards the gate, buildings around.
            var centre = Vector2.zero;
            foreach (var c in s_Castle) centre += c;
            centre /= s_Castle.Length;
            var toGate = (s_GatePoint - centre).normalized;
            var side = new Vector2(-toGate.y, toGate.x);
            Vector2 L(float fwd, float right) => centre + toGate * fwd + side * right;
            float baseYaw = YawOf(toGate);

            Place("Keep", "Castle/Keep", L(-14f, 0f), baseYaw, 30f, -1f);
            Place("Fountain", "Castle/Square", L(18f, 0f), 0f, 14f, -0.3f);
            Place("Market_Stall", "Castle/Square", L(26f, -12f), baseYaw + 30f, 11f);
            Place("Market_Stall", "Castle/Square", L(26f, 12f), baseYaw - 30f, 11f);
            Place("Market_Stall", "Castle/Square", L(12f, -16f), baseYaw + 90f, 11f);
            Place("Cart_Hand", "Castle/Square", L(10f, 14f), baseYaw + 40f, 9f);
            Place("Well", "Castle/Square", L(16f, -26f), 0f, 9f);

            var buildings = new (string model, float fwd, float right, float yawOffset, float scale)[]
            {
                ("Chapel", -46f, 0f, 0f, 19f), ("Barracks", -36f, -32f, 30f, 19f), ("Stable", -36f, 32f, -30f, 19f),
                ("Storehouse", -6f, -40f, 90f, 17f), ("Forge", -6f, 40f, -90f, 17f), ("Anvil_Forge", 4f, 30f, -90f, 9f),
                ("Granary", 30f, -38f, 120f, 17f), ("Tavern", 34f, 30f, -150f, 17f), ("Workshop", 44f, -18f, 160f, 15f),
            };
            foreach (var bld in buildings)
                Place(bld.model, "Castle/Buildings", L(bld.fwd, bld.right), baseYaw + bld.yawOffset + 180f, bld.scale, -0.6f);

            // Defences on the wall walk.
            for (int i = 0; i < s_Castle.Length; i++)
            {
                var a = s_Castle[i]; var b = s_Castle[(i + 1) % s_Castle.Length];
                var mid = (a + b) * 0.5f;
                if (mid == s_GatePoint) continue;
                var outward = mid.normalized;
                string model = i % 3 == 0 ? "Ballista" : "Cannon";
                Place(model, "Castle/Defences", mid, YawOf(outward), 8f, 0f).transform.position =
                    new Vector3(mid.x, LowGround(a, b) - 1.5f + 0.62f * 22f, mid.y);
            }

            // Barrels and crates by the buildings.
            foreach (var bld in buildings)
            {
                var spot = L(bld.fwd + 9f, bld.right * 0.8f);
                for (int i = 0; i < 3; i++)
                    Place(i == 1 ? "Crate" : "Barrel", "Castle/Props", spot + new Vector2(Rand(-3f, 3f), Rand(-3f, 3f)), Rand(0f, 360f), i == 1 ? 1.4f : 1.3f);
            }
        }

        private static void BuildBridge()
        {
            // The path point closest to the river loop is the crossing.
            int best = 0; float bestD = float.MaxValue;
            for (int i = 0; i < s_Path.Count; i++)
            {
                float rd = DistanceToPolyline(s_River, s_Path[i], 60f);
                if (rd < bestD) { bestD = rd; best = i; }
            }
            var p = s_Path[best];
            var dir = (s_Path[Mathf.Min(best + 3, s_Path.Count - 1)] - s_Path[Mathf.Max(best - 3, 0)]).normalized;
            // Drawbridge model is long along X: turn it to the path and stretch it over the river.
            var bridge = Place("Drawbridge", "Castle/Gate", p, YawOf(dir) + 90f, new Vector3(52f, 14f, 30f), 0f, s.WaterLevel + 0.4f);
            bridge.name = "Bridge_River";
        }

        private static void BuildCamps()
        {
            var camps = new[] { new Vector2(55f, -228f), new Vector2(-228f, -40f), new Vector2(225f, -25f) };
            string[] names = { "Front_South", "Left_West", "Right_East" };
            for (int i = 0; i < camps.Length; i++)
            {
                var c = camps[i];
                var toCastle = (-c).normalized;
                var side = new Vector2(-toCastle.y, toCastle.x);
                float yaw = YawOf(toCastle);
                string g = "Camps/" + names[i];
                Vector2 P(float f, float r) => c + toCastle * f + side * r;
                Place("Tent_Attack", g, c, yaw, 17f, -0.5f);
                Place("Planning_Table", g, P(10f, -14f), yaw, 6f);
                Place("Anvil_Forge", g, P(8f, 15f), yaw + 90f, 8f);
                Place("Cart_Supply", g, P(-8f, -17f), yaw + 20f, 10f);
                Place("Crate", g, P(-10f, 15f), Rand(0f, 360f), 1.5f);
                Place("Crate", g, P(-11f, 17f), Rand(0f, 360f), 1.4f);
                Place("Barrel", g, P(-9f, 19f), 0f, 1.3f);
                Place("Haystack", g, P(-18f, -8f), 0f, 9f);
            }
            Place("Windmill", "Outskirts/East_Farm", new Vector2(190f, 95f), 250f, 22f, -0.5f);
            Place("Barn_Coop", "Outskirts/East_Farm", new Vector2(200f, -105f), 290f, 16f, -0.5f);
            foreach (var h in new[] { new Vector2(175f, -130f), new Vector2(215f, -80f), new Vector2(-70f, -190f), new Vector2(60f, -175f) })
                Place("Haystack", "Outskirts/Fields", h, Rand(0f, 360f), 10f);
            Place("Cart_Hand", "Outskirts/Fields", new Vector2(-30f, -205f), 70f, 9f);
        }

        private static void BuildRocks()
        {
            var meshes = new List<Mesh>();
            for (int i = 0; i < 4; i++)
            {
                var m = AssetDatabase.LoadAssetAtPath<Mesh>($"Assets/Remains/Meshes/Rock_{i}.asset");
                if (m != null) meshes.Add(m);
            }
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Remains/Materials/Rock.mat");
            if (meshes.Count == 0 || mat == null) return;

            void Rock(Vector2 p, float size, float sink)
            {
                var mesh = meshes[s_Rng.Next(meshes.Count)];
                var go = new GameObject("Rock");
                go.transform.SetParent(Group("Outskirts/Rocks"), false);
                float k = size / Mathf.Max(0.01f, mesh.bounds.size.magnitude);
                go.transform.position = new Vector3(p.x, Ground(p) - sink * size, p.y);
                go.transform.rotation = Quaternion.Euler(Rand(-25f, 25f), Rand(0f, 360f), Rand(-25f, 25f));
                go.transform.localScale = new Vector3(k * Rand(0.8f, 1.3f), k * Rand(0.6f, 1.2f), k * Rand(0.8f, 1.3f));
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.isStatic = true;
            }

            // Outcrops along the rim and on the cliff face.
            for (int i = 0; i < s.CliffRocks; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                float r = RimRadius(a) + Rand(-4f, 22f);
                Rock(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, Rand(10f, 28f), 0.5f);
            }
            // Boulders on the plateau and by the river.
            for (int i = 0; i < 30; i++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                float r = Rand(s.RiverRadius + 25f, RimRadius(a) - 20f);
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (DistanceToPolyline(s_Path, p, 30f) < 12f) continue;
                Rock(p, Rand(2f, 5f), 0.3f);
            }
        }

        // ---------------------------------------------------------------- main

        public static void Generate(RoundMapSettings settings)
        {
            s = settings;
            s_Rng = new System.Random(s.Seed);
            s_Groups.Clear();
            foreach (var dir in new[] { "", "/Textures", "/Layers", "/Materials", "/Prefabs" })
                Directory.CreateDirectory(k_Root + dir);
            AssetDatabase.Refresh();

            BuildLayout();
            var tex = BuildTextures();
            s_KitMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Remains/MapKit/Materials/MapKit_VertexColor.mat");
            // Prefabs first: creating assets later can reload the terrain data from disk and drop unsaved edits.
            var pine = TreePrefab("Tree_Pine", 11f, s_KitMaterial);
            var leafy = TreePrefab("Tree_Deciduous", 9f, s_KitMaterial);
            var grassClump = GrassPrefab(tex["GrassBlades"]);

            // ---- heights, splat, grass
            float size = s.TerrainSize, half = size * 0.5f;
            var heights = new float[k_HeightRes, k_HeightRes];
            var riverD = new float[k_HeightRes, k_HeightRes];
            var pathD = new float[k_HeightRes, k_HeightRes];
            var cliffT = new float[k_HeightRes, k_HeightRes];
            for (int z = 0; z < k_HeightRes; z++)
            {
                for (int x = 0; x < k_HeightRes; x++)
                {
                    var p = new Vector2(-half + x * size / (k_HeightRes - 1), -half + z * size / (k_HeightRes - 1));
                    riverD[z, x] = DistanceToPolyline(s_River, p, 60f);
                    pathD[z, x] = DistanceToPolyline(s_Path, p, 30f);
                    heights[z, x] = Height(p, riverD[z, x], pathD[z, x], out cliffT[z, x]) / k_MaxHeight;
                }
            }

            // Save the asset first: alphamaps and details set before CreateAsset are not kept.
            string dataPath = $"{k_Root}/RoundMap_TerrainData.asset";
            AssetDatabase.DeleteAsset(dataPath);
            var data = new TerrainData { heightmapResolution = k_HeightRes };
            AssetDatabase.CreateAsset(data, dataPath);
            data.size = new Vector3(size, k_MaxHeight, size);
            data.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            var layers = new[]
            {
                Layer("Grass", tex["Grass"], 9f), Layer("Dirt", tex["Dirt"], 6f), Layer("Rock", tex["Rock"], 14f),
                Layer("Cobble", tex["Cobble"], 5f), Layer("Mud", tex["Mud"], 7f, 0.3f), Layer("GrassDark", tex["GrassDark"], 11f),
            };
            data.terrainLayers = layers;
            data.alphamapResolution = k_SplatRes;
            var splat = new float[k_SplatRes, k_SplatRes, layers.Length];
            var grass = new int[k_SplatRes, k_SplatRes];
            for (int z = 0; z < k_SplatRes; z++)
            {
                for (int x = 0; x < k_SplatRes; x++)
                {
                    int hx = Mathf.Min(k_HeightRes - 1, Mathf.RoundToInt(x * (k_HeightRes - 1f) / (k_SplatRes - 1f)));
                    int hz = Mathf.Min(k_HeightRes - 1, Mathf.RoundToInt(z * (k_HeightRes - 1f) / (k_SplatRes - 1f)));
                    var p = new Vector2(-half + x * size / (k_SplatRes - 1), -half + z * size / (k_SplatRes - 1));
                    float slope = data.GetSteepness(x / (k_SplatRes - 1f), z / (k_SplatRes - 1f));
                    float h = heights[hz, hx] * k_MaxHeight;

                    float rock = Mathf.Clamp01((slope - 34f) / 12f);
                    rock = Mathf.Max(rock, Mathf.Clamp01(cliffT[hz, hx] * 3f));
                    float mud = Mathf.Clamp01((14f - riverD[hz, hx]) / 5f) * (1f - rock);
                    float dirt = Mathf.Clamp01((4.5f - pathD[hz, hx]) / 2f) * (1f - rock);
                    float edge = DistanceToCastleEdge(p);
                    float cobble = Mathf.Clamp01((-edge - 2f) / 6f) * (1f - rock);
                    float patches = Mathf.Clamp01((Fbm(p.x, p.y + 999f, 3, 0.03f) - 0.35f) * 3f) * 0.6f; // worn spots in the grass
                    float rest = Mathf.Max(0f, 1f - rock - mud - dirt - cobble);
                    float lush = Mathf.Clamp01((Mathf.PerlinNoise(p.x * 0.012f + 40f, p.y * 0.012f) - 0.45f) * 3f);
                    float grassW = rest * (1f - patches);
                    float darkW = grassW * lush;
                    grassW -= darkW;
                    float dirtW = dirt + rest * patches;
                    float sum = grassW + darkW + dirtW + rock + cobble + mud + 1e-5f;
                    splat[z, x, 0] = grassW / sum;
                    splat[z, x, 1] = dirtW / sum;
                    splat[z, x, 2] = rock / sum;
                    splat[z, x, 3] = cobble / sum;
                    splat[z, x, 4] = mud / sum;
                    splat[z, x, 5] = darkW / sum;

                    bool grassy = (grassW + darkW) / sum > 0.7f && h > s.WaterLevel + 0.6f && slope < 30f;
                    if (grassy)
                    {
                        float clump = Mathf.PerlinNoise(p.x * 0.07f + 7f, p.y * 0.07f);
                        grass[z, x] = Mathf.RoundToInt(s.GrassDensity * Mathf.Clamp01(clump * 1.6f - 0.25f));
                    }
                }
            }
            data.SetAlphamaps(0, 0, splat);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);

            data.SetDetailResolution(k_SplatRes, 16);
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode); // detail values are clumps per cell, not coverage
            data.detailPrototypes = new[]
            {
                new DetailPrototype
                {
                    prototype = grassClump, usePrototypeMesh = true, renderMode = DetailRenderMode.VertexLit, useInstancing = true,
                    healthyColor = new Color(0.85f, 0.95f, 0.75f), dryColor = new Color(0.95f, 0.9f, 0.6f),
                    minWidth = 0.8f, maxWidth = 1.5f, minHeight = 0.6f, maxHeight = 1.2f, noiseSpread = 0.3f,
                },
            };
            data.SetDetailLayer(0, 0, 0, grass);
            data.wavingGrassAmount = 0.25f;
            data.wavingGrassStrength = 0.4f;
            data.wavingGrassTint = new Color(0.8f, 0.85f, 0.6f);

            EditorUtility.SetDirty(data);

            // ---- scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            s_SceneRoot = new GameObject("Map_RoundCastle").transform;
            var terrainGo = Terrain.CreateTerrainGameObject(data);
            terrainGo.name = "Terrain";
            terrainGo.transform.SetParent(s_SceneRoot, false);
            terrainGo.transform.position = new Vector3(-half, 0f, -half);
            s_Terrain = terrainGo.GetComponent<Terrain>();
            var terrainLit = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Runtime/Materials/TerrainLit.mat");
            if (terrainLit != null) s_Terrain.materialTemplate = terrainLit;
            s_Terrain.detailObjectDistance = 120f;
            s_Terrain.treeBillboardDistance = 2000f;
            s_Terrain.treeDistance = 2000f;
            s_Terrain.basemapDistance = 400f;

            // ---- trees (terrain tree instances, so they can be painted by hand later)
            data.treePrototypes = new[] { new TreePrototype { prefab = pine }, new TreePrototype { prefab = leafy } };
            var trees = new List<TreeInstance>();
            bool FreeSpot(Vector2 p)
            {
                if (DistanceToPolyline(s_River, p, 30f) < 19f || DistanceToPolyline(s_Path, p, 20f) < 12f) return false;
                if (DistanceToCastleEdge(p) < 22f) return false;
                if (p.magnitude > RimRadius(Mathf.Atan2(p.y, p.x)) - 10f) return false;
                foreach (var camp in new[] { new Vector2(55f, -228f), new Vector2(-228f, -40f), new Vector2(225f, -25f), new Vector2(190f, 95f), new Vector2(200f, -105f) })
                    if (Vector2.Distance(p, camp) < 34f) return false;
                return true;
            }
            void AddTree(Vector2 p, int proto, float scale)
            {
                trees.Add(new TreeInstance
                {
                    position = new Vector3((p.x + half) / size, 0f, (p.y + half) / size),
                    prototypeIndex = proto, widthScale = scale, heightScale = scale * Rand(0.9f, 1.15f),
                    rotation = Rand(0f, Mathf.PI * 2f), color = Color.white, lightmapColor = Color.white,
                });
            }
            for (int i = 0, tries = 0; i < s.ForestTrees && tries < s.ForestTrees * 20; tries++)
            {
                // Western forest: wedge between north-west and south-west, denser in clumps.
                float a = Rand(2.3f, 4.1f);
                float r = Rand(s.RiverRadius + 20f, RimRadius(a) - 8f);
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (Mathf.PerlinNoise(p.x * 0.02f + 3f, p.y * 0.02f) < 0.38f || !FreeSpot(p)) continue;
                AddTree(p, Rand() < 0.72f ? 0 : 1, Rand(0.8f, 1.35f));
                i++;
            }
            for (int i = 0, tries = 0; i < s.ScatteredTrees && tries < s.ScatteredTrees * 30; tries++)
            {
                float a = Rand(0f, Mathf.PI * 2f);
                float r = Rand(s.RiverRadius + 20f, RimRadius(a) - 8f);
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (!FreeSpot(p) || Mathf.PerlinNoise(p.x * 0.03f, p.y * 0.03f + 5f) < 0.5f) continue;
                AddTree(p, Rand() < 0.35f ? 0 : 1, Rand(0.8f, 1.3f));
                i++;
            }
            data.SetTreeInstances(trees.ToArray(), true);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssetIfDirty(data);
            s_Terrain.GetComponent<TerrainCollider>().terrainData = data;

            // ---- water and lava
            var water = AssetDatabase.LoadAssetAtPath<Material>("Assets/Remains/MapKit/Materials/Water.mat");
            var disc = DiscMesh(s.PlateauRadius - 35f, 96);
            disc = SaveMesh(disc, $"{k_Root}/WaterDisc.asset");
            var waterGo = new GameObject("River_Water");
            waterGo.transform.SetParent(s_SceneRoot, false);
            waterGo.transform.position = new Vector3(0f, s.WaterLevel, 0f);
            waterGo.AddComponent<MeshFilter>().sharedMesh = disc;
            waterGo.AddComponent<MeshRenderer>().sharedMaterial = water;

            var lavaMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            lavaMat.SetTexture("_BaseMap", tex["Lava"]);
            lavaMat.SetTextureScale("_BaseMap", new Vector2(14f, 14f));
            lavaMat.SetTexture("_EmissionMap", tex["Lava"]);
            lavaMat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.1f) * 2.2f);
            lavaMat.EnableKeyword("_EMISSION");
            lavaMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            lavaMat.SetFloat("_Smoothness", 0.35f);
            lavaMat = SaveMaterial("Lava", lavaMat);
            var lava = GameObject.CreatePrimitive(PrimitiveType.Plane);
            lava.name = "Lava";
            lava.transform.SetParent(s_SceneRoot, false);
            lava.transform.position = new Vector3(0f, s.LavaLevel, 0f);
            lava.transform.localScale = new Vector3(160f, 1f, 160f);
            lava.GetComponent<MeshRenderer>().sharedMaterial = lavaMat;
            var lavaLight = new GameObject("Lava_Glow").AddComponent<Light>();
            lavaLight.transform.SetParent(s_SceneRoot, false);
            lavaLight.type = LightType.Directional;
            lavaLight.color = new Color(1f, 0.45f, 0.15f);
            lavaLight.intensity = 0.35f;
            lavaLight.transform.rotation = Quaternion.Euler(-60f, 0f, 0f); // from below: lights the cliff faces

            // ---- buildings and props
            BuildCastle();
            BuildBridge();
            BuildCamps();
            BuildRocks();

            // ---- light, fog and camera
            var sun = Object.FindAnyObjectByType<Light>();
            sun.transform.rotation = Quaternion.Euler(42f, -40f, 0f);
            sun.color = new Color(1f, 0.95f, 0.85f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.62f, 0.55f, 0.50f);
            RenderSettings.fogStartDistance = 350f;
            RenderSettings.fogEndDistance = 1200f;
            var cam = Camera.main;
            cam.farClipPlane = 3000f;
            cam.transform.position = new Vector3(260f, 260f, -420f);
            cam.transform.LookAt(new Vector3(0f, s.GroundHeight, -20f));

            EditorSceneManager.SaveScene(scene, k_ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Round map generated: {trees.Count} trees, scene {k_ScenePath}");
        }

        /// <summary>Overwrites the mesh asset in place so prefabs that already point at it keep the reference.</summary>
        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            mesh.name = Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }
            EditorUtility.CopySerialized(mesh, existing);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssetIfDirty(existing);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }

        private static Mesh DiscMesh(float radius, int segments)
        {
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            verts[0] = Vector3.zero;
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2f / segments;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % segments + 1;
                tris[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "WaterDisc", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>
    /// Remains → Round Map: edit the generator settings and rebuild the map.
    /// </summary>
    public class RoundMapWindow : EditorWindow
    {
        [SerializeField] private RoundMapSettings m_Settings = new RoundMapSettings();
        private SerializedObject m_Serialized;
        private Vector2 m_Scroll;

        [MenuItem("Remains/Round Map/Generator...")]
        private static void Open() => GetWindow<RoundMapWindow>("Round Map");

        [MenuItem("Remains/Round Map/Generate With Defaults")]
        private static void GenerateDefault() => RoundMapGenerator.Generate(new RoundMapSettings());

        private void OnGUI()
        {
            m_Serialized ??= new SerializedObject(this);
            m_Serialized.Update();
            m_Scroll = EditorGUILayout.BeginScrollView(m_Scroll);
            var it = m_Serialized.FindProperty(nameof(m_Settings));
            EditorGUILayout.PropertyField(it, new GUIContent("Settings"), true);
            m_Serialized.ApplyModifiedProperties();
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Generate rebuilds Assets/Remains/Scenes/Remains_RoundMap.unity from scratch: hand edits to that scene are lost. " +
                                    "Change the seed to get a different layout.", MessageType.Info);
            if (GUILayout.Button("Generate", GUILayout.Height(32)) &&
                EditorUtility.DisplayDialog("Round Map", "Rebuild the round map scene? Unsaved changes in the open scene are lost.", "Generate", "Cancel"))
            {
                RoundMapGenerator.Generate(m_Settings);
            }
            EditorGUILayout.EndScrollView();
        }
    }
}
