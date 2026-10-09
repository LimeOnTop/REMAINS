using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Remains.Editor.BuildKit;

namespace Remains.Editor
{
    /// <summary>
    /// Terrain, the village below the castle, fields, fences, forest, rocks and lighting.
    /// </summary>
    public static class WorldBuilder
    {
        private static readonly Vector2[] s_Houses =
        {
            new Vector2(-38f, -34f), new Vector2(-52f, -48f), new Vector2(-32f, -58f), new Vector2(36f, -38f), new Vector2(50f, -54f)
        };

        public static void BuildTerrain(Transform environment)
        {
            var mesh = SaveMesh(LowPolyMeshes.Terrain("Terrain", 280f, 90, Height));
            var terrain = new GameObject("Terrain") { layer = GroundLayer };
            terrain.transform.SetParent(environment);
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterial = M("Grass");
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;

            // Dirt road from the camp up to the gate.
            for (int i = 0; i < 30; i++)
            {
                float z = Mathf.Lerp(-80f, -18f, i / 29f);
                var patch = Box(environment, "Road", OnGround(0f, z, 0.03f), new Vector3(6f, 0.06f, 2.6f), M("Dirt"), false,
                    Quaternion.Euler(Mathf.Atan2(Height(0f, z + 1f) - Height(0f, z - 1f), 2f) * -Mathf.Rad2Deg, 0f, 0f));
                patch.layer = GroundLayer;
            }
        }

        public static void BuildVillage(Transform environment, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements, System.Random random)
        {
            var village = Group(environment, "Village", Vector3.zero);
            for (int i = 0; i < s_Houses.Length; i++)
            {
                BuildHouse(village, i, s_Houses[i], prefabs, placements, random);
            }

            // Fields with furrows.
            foreach (var field in new[] { new Rect(-70f, -30f, 18f, 12f), new Rect(52f, -30f, 16f, 14f), new Rect(-60f, -75f, 14f, 10f) })
            {
                for (float z = field.yMin; z < field.yMax; z += 1.4f)
                {
                    float x = field.center.x;
                    var row = Box(village, "Furrow", OnGround(x, z, 0.12f), new Vector3(field.width, 0.25f, 0.8f), M(z % 2.8f < 1.4f ? "Field" : "Hay"), false);
                    row.layer = GroundLayer;
                }
            }

            // Haystacks and a village well.
            var stackMesh = SaveMesh(LowPolyMeshes.Cone("Haystack", 7, 1.6f, 2.6f));
            foreach (var spot in new[] { new Vector2(-44f, -40f), new Vector2(42f, -46f), new Vector2(-26f, -48f) })
            {
                MeshObject(village, "Haystack", stackMesh, M("Hay"), OnGround(spot.x, spot.y, -0.1f), Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0), Vector3.one, true);
            }
        }

        private static void BuildHouse(Transform parent, int index, Vector2 spot, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements, System.Random random)
        {
            float ground = Mathf.Min(Height(spot.x - 3f, spot.y - 3f), Height(spot.x + 3f, spot.y + 3f), Height(spot.x + 3f, spot.y - 3f), Height(spot.x - 3f, spot.y + 3f));
            Vector3 toCastle = new Vector3(-spot.x, 0f, -spot.y);
            var house = Group(parent, $"House_{index}", new Vector3(spot.x, ground, spot.y), Quaternion.LookRotation(toCastle));

            const float w = 5f, d = 6f, h = 3f, t = 0.3f;
            Box(house, "Foundation", new Vector3(0, -0.6f, 0), new Vector3(w + 0.4f, 1.4f, d + 0.4f), M("Cobble"));
            Box(house, "WallBack", new Vector3(0, h / 2f, -d / 2f), new Vector3(w, h, t), M("Plaster"));
            Box(house, "WallL", new Vector3(-w / 2f, h / 2f, 0), new Vector3(t, h, d), M("Plaster"));
            Box(house, "WallR", new Vector3(w / 2f, h / 2f, 0), new Vector3(t, h, d), M("Plaster"));
            Box(house, "FrontL", new Vector3(-1.55f, h / 2f, d / 2f), new Vector3(1.9f, h, t), M("Plaster"));
            Box(house, "FrontR", new Vector3(1.55f, h / 2f, d / 2f), new Vector3(1.9f, h, t), M("Plaster"));
            Box(house, "FrontTop", new Vector3(0f, 2.6f, d / 2f), new Vector3(1.2f, 0.8f, t), M("Plaster"));

            // Timber framing.
            foreach (var x in new[] { -w / 2f, w / 2f })
                foreach (var z in new[] { -d / 2f, d / 2f })
                    Box(house, "Beam", new Vector3(x, h / 2f, z), new Vector3(0.36f, h, 0.36f), M("WoodDark"), false);
            Box(house, "BeamTop", new Vector3(0, h, d / 2f + 0.02f), new Vector3(w + 0.3f, 0.25f, 0.34f), M("WoodDark"), false);
            Box(house, "BeamTop", new Vector3(0, h, -d / 2f - 0.02f), new Vector3(w + 0.3f, 0.25f, 0.34f), M("WoodDark"), false);
            Box(house, "BeamX", new Vector3(-1.55f, 1.5f, d / 2f + 0.17f), new Vector3(0.15f, 2.6f, 0.04f), M("WoodDark"), false, Quaternion.Euler(0, 0, 35));
            Box(house, "BeamX", new Vector3(1.55f, 1.5f, d / 2f + 0.17f), new Vector3(0.15f, 2.6f, 0.04f), M("WoodDark"), false, Quaternion.Euler(0, 0, -35));
            Box(house, "Window", new Vector3(-w / 2f - 0.16f, 1.7f, 0f), new Vector3(0.04f, 0.8f, 1f), M("WindowDark"), false);
            Box(house, "Window", new Vector3(w / 2f + 0.16f, 1.7f, 0f), new Vector3(0.04f, 0.8f, 1f), M("WindowDark"), false);

            // Gabled roof: two slabs.
            float slope = 38f;
            Box(house, "RoofL", new Vector3(-1.4f, h + 1.05f, 0f), new Vector3(3.6f, 0.22f, d + 0.8f), M("RoofRed"), true, Quaternion.Euler(0, 0, slope));
            Box(house, "RoofR", new Vector3(1.4f, h + 1.05f, 0f), new Vector3(3.6f, 0.22f, d + 0.8f), M("RoofRed"), true, Quaternion.Euler(0, 0, -slope));
            Box(house, "GableF", new Vector3(0f, h + 0.7f, d / 2f), new Vector3(2.4f, 1.3f, t), M("Plaster"), false);
            Box(house, "GableB", new Vector3(0f, h + 0.7f, -d / 2f), new Vector3(2.4f, 1.3f, t), M("Plaster"), false);
            Box(house, "Chimney", new Vector3(1.4f, h + 1.8f, -1.8f), new Vector3(0.6f, 1.6f, 0.6f), M("BrickDark"));

            // Door.
            var doorRoot = new GameObject("Door");
            doorRoot.transform.SetParent(house, false);
            doorRoot.transform.localPosition = new Vector3(0f, 0f, d / 2f);
            SceneNetworkObject(doorRoot);
            var hinge = Group(doorRoot.transform, "Hinge", new Vector3(-0.6f, 0f, 0f));
            Box(hinge, "Leaf", new Vector3(0.6f, 1.1f, 0f), new Vector3(1.18f, 2.2f, 0.1f), M("Wood"));
            doorRoot.AddComponent<InteractiveDoor>().EditorSetup(hinge, "Дверь дома", 100f);

            // Fence in front and some loose goods.
            for (int i = 0; i < 6; i++)
            {
                float x = -4.5f + i * 1.8f;
                if (Mathf.Abs(x) < 1f) continue;
                Box(house, "FencePost", new Vector3(x, 0.5f, d / 2f + 3f), new Vector3(0.14f, 1f, 0.14f), M("WoodDark"));
                Box(house, "FenceRail", new Vector3(x + 0.9f, 0.7f, d / 2f + 3f), new Vector3(1.8f, 0.08f, 0.06f), M("Wood"), false);
            }

            CastleBuilder.Add(placements, prefabs.Crate, house.TransformPoint(new Vector3(-w / 2f - 1f, 0.6f, 1f)), random.Next(0, 40));
            CastleBuilder.Add(placements, prefabs.Barrel, house.TransformPoint(new Vector3(w / 2f + 0.9f, 0.5f, 1.5f)), 0f);
            CastleBuilder.Add(placements, prefabs.Bench, house.TransformPoint(new Vector3(1.8f, 0.1f, d / 2f + 1f)), 0f);
            if (index % 2 == 0) CastleBuilder.Add(placements, prefabs.HayBale, house.TransformPoint(new Vector3(-w / 2f - 1f, 0.4f, -1.2f)), 0f);
            if (index == 1) CastleBuilder.Add(placements, prefabs.Cart, house.TransformPoint(new Vector3(w / 2f + 2.5f, 0.1f, -1f)), 90f);
        }

        public static void BuildNature(Transform environment, System.Random random)
        {
            var nature = Group(environment, "Nature", Vector3.zero);

            var trunk = SaveMesh(LowPolyMeshes.Prism("TreeTrunk", 6, 0.22f, 2.4f));
            var crown = SaveMesh(LowPolyMeshes.Cone("TreeCrown", 7, 1.6f, 3.2f));
            var bush = SaveMesh(LowPolyMeshes.Rock("Bush", 11));
            var rocks = new List<Mesh>();
            for (int i = 0; i < 4; i++) rocks.Add(SaveMesh(LowPolyMeshes.Rock($"Rock_{i}", 100 + i)));

            int placed = 0;
            for (int attempt = 0; attempt < 900 && placed < 150; attempt++)
            {
                float x = (float)(random.NextDouble() * 260 - 130);
                float z = (float)(random.NextDouble() * 260 - 130);
                if (!IsFreeForNature(x, z, 3f)) continue;

                bool eastForest = x > 26f && z > -80f && z < 60f;
                bool northForest = z > 40f;
                if (!eastForest && !northForest && random.NextDouble() > 0.2) continue;

                var tree = Group(nature, "Tree", OnGround(x, z), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f));
                tree.localScale = Vector3.one * (0.8f + (float)random.NextDouble() * 0.8f);
                var trunkObject = MeshObject(tree, "Trunk", trunk, M("WoodDark"), new Vector3(0, 1.2f, 0), Quaternion.identity, Vector3.one);
                var capsule = trunkObject.AddComponent<CapsuleCollider>();
                capsule.radius = 0.25f;
                capsule.height = 2.4f;
                var leaves = random.NextDouble() < 0.5 ? M("Leaves") : M("LeavesLight");
                MeshObject(tree, "Crown", crown, leaves, new Vector3(0, 2f, 0), Quaternion.identity, Vector3.one);
                MeshObject(tree, "Crown", crown, leaves, new Vector3(0, 3.4f, 0), Quaternion.Euler(0, 25, 0), Vector3.one * 0.7f);
                placed++;
            }

            for (int i = 0; i < 70; i++)
            {
                float x = (float)(random.NextDouble() * 260 - 130);
                float z = (float)(random.NextDouble() * 260 - 130);
                if (!IsFreeForNature(x, z, 2f)) continue;
                float size = 0.8f + (float)random.NextDouble() * 2.4f;
                var rock = MeshObject(nature, "Rock", rocks[i % rocks.Count], M("Rock"), OnGround(x, z, size * 0.15f), Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f), Vector3.one * size);
                rock.AddComponent<MeshCollider>().sharedMesh = rocks[i % rocks.Count];
            }

            for (int i = 0; i < 90; i++)
            {
                float x = (float)(random.NextDouble() * 240 - 120);
                float z = (float)(random.NextDouble() * 240 - 120);
                if (!IsFreeForNature(x, z, 1f)) continue;
                float size = 0.6f + (float)random.NextDouble() * 0.8f;
                MeshObject(nature, "Bush", bush, random.NextDouble() < 0.5 ? M("Leaves") : M("GrassDark"), OnGround(x, z, size * 0.2f), Quaternion.Euler(0, (float)random.NextDouble() * 360f, 0), new Vector3(size * 1.3f, size, size * 1.3f));
            }
        }

        private static bool IsFreeForNature(float x, float z, float margin)
        {
            if (Mathf.Sqrt(x * x + z * z) < 32f + margin) return false;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(CampCenter.x, CampCenter.z)) < CampRadius + 6f + margin) return false;
            if (Mathf.Abs(x) < 9f + margin && z < -15f && z > -82f) return false;
            foreach (var house in s_Houses)
            {
                if (Vector2.Distance(house, new Vector2(x, z)) < 9f + margin) return false;
            }
            return true;
        }

        public static void BuildLighting()
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.intensity = 1.35f;
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42f, -30f, 0f);
            RenderSettings.sun = light;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.58f, 0.6f, 0.56f);
            RenderSettings.ambientGroundColor = new Color(0.32f, 0.29f, 0.25f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.74f, 0.82f, 0.88f);
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance = 260f;
        }
    }
}
