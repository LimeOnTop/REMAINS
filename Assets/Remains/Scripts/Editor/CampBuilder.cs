using System.Collections.Generic;
using UnityEngine;
using static Remains.Editor.BuildKit;

namespace Remains.Editor
{
    /// <summary>
    /// The attackers' siege camp: three large pavilion tents (waving fabric, battle-map table, armorer's workshop),
    /// cannons with cannonball piles, rams, ladders, campfires, banners and a palisade.
    /// </summary>
    public static class CampBuilder
    {
        private const float TentRadius = 6.5f;
        private const float WallHeight = 2.8f;
        private const float RoofHeight = 4.6f;
        private const int Sides = 8;

        private static readonly string[] s_ArmorerNames = { "Оружейник Бранд", "Оружейница Марта", "Оружейник Гюнтер" };

        public static List<Renderer> Build(Transform environment, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements, System.Random random)
        {
            var camp = Group(environment, "SiegeCamp", Vector3.zero);
            var mapSurfaces = new List<Renderer>();

            var tentPositions = new[] { new Vector3(-17f, 0f, -104f), new Vector3(0f, 0f, -110f), new Vector3(17f, 0f, -104f) };
            for (int i = 0; i < tentPositions.Length; i++)
            {
                Vector3 position = OnGround(tentPositions[i].x, tentPositions[i].z);
                // Entrances face the castle.
                Vector3 toCastle = -position;
                toCastle.y = 0f;
                var tent = BuildTent(camp, $"Tent_{i}", position, Quaternion.LookRotation(toCastle), s_ArmorerNames[i], i, mapSurfaces, prefabs, placements);
            }

            BuildSiegePark(camp, prefabs, placements, random);
            BuildCampDressing(camp, prefabs, placements, random);
            return mapSurfaces;
        }

        #region Tent

        private static Transform BuildTent(Transform parent, string name, Vector3 position, Quaternion rotation, string armorerName, int index,
            List<Renderer> mapSurfaces, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements)
        {
            var tent = Group(parent, name, position, rotation);
            var stripes = index == 1 ? M("ClothRed") : M("ClothOrange");

            // Floor boards and rug.
            var floor = Box(tent, "Floor", new Vector3(0f, 0.05f, 0f), new Vector3(TentRadius * 1.8f, 0.1f, TentRadius * 1.8f), M("WoodDark"));
            floor.layer = GroundLayer;
            Box(tent, "Rug", new Vector3(0f, 0.11f, 0.5f), new Vector3(6f, 0.02f, 4.5f), M("ClothRed"), false);

            // Walls: eight fabric panels hanging from the eaves; the one facing +Z is the entrance.
            int entrance = EntranceIndex();
            for (int i = 0; i < Sides; i++)
            {
                Vector3 a = Corner(i), b = Corner(i + 1);
                Vector3 edge = (b - a).normalized;
                Quaternion panelRotation = Quaternion.LookRotation(Vector3.Cross(edge, Vector3.up));
                float width = Vector3.Distance(a, b);

                if (i == entrance)
                {
                    // Tied-back door flaps.
                    Cloth(tent, "FlapL", a + Vector3.up * WallHeight, panelRotation * Quaternion.Euler(0f, -60f, 0f), width * 0.3f, WallHeight, stripes, WindCloth.PinnedEdge.Top, 0.18f, 3, 6, 2.6f);
                    Cloth(tent, "FlapR", b + Vector3.up * WallHeight, panelRotation * Quaternion.Euler(0f, 180f + 60f, 0f), width * 0.3f, WallHeight, stripes, WindCloth.PinnedEdge.Top, 0.18f, 3, 6, 2.6f);
                    continue;
                }

                Cloth(tent, $"Wall_{i}", a + Vector3.up * WallHeight, panelRotation, width, WallHeight, i % 2 == 0 ? M("ClothCream") : stripes, WindCloth.PinnedEdge.Top, 0.07f, 6, 4, 1.6f);

                var wallCollider = new GameObject("WallCollider");
                wallCollider.transform.SetParent(tent, false);
                wallCollider.transform.localPosition = (a + b) * 0.5f + Vector3.up * WallHeight * 0.5f;
                wallCollider.transform.localRotation = panelRotation;
                wallCollider.AddComponent<BoxCollider>().size = new Vector3(width, WallHeight, 0.15f);
            }

            // Striped conical roof, eave valance and the central pole with a pennant.
            var roofMesh = SaveMesh(LowPolyMeshes.StripedCone("TentRoof", Sides, TentRadius * 1.08f, RoofHeight));
            var roof = MeshObject(tent, "Roof", roofMesh, stripes, new Vector3(0f, WallHeight, 0f), Quaternion.Euler(0f, 360f / Sides / 2f * 0f, 0f), Vector3.one);
            roof.GetComponent<MeshRenderer>().sharedMaterials = new[] { stripes, M("ClothCream") };
            for (int i = 0; i < Sides; i++)
            {
                Vector3 a = Corner(i) * 1.08f, b = Corner(i + 1) * 1.08f;
                Quaternion valanceRotation = Quaternion.LookRotation(Vector3.Cross((b - a).normalized, Vector3.up));
                Cloth(tent, "Valance", a + Vector3.up * (WallHeight + 0.02f), valanceRotation, Vector3.Distance(a, b), 0.55f, i % 2 == 0 ? stripes : M("ClothCream"), WindCloth.PinnedEdge.Top, 0.12f, 6, 2, 2.4f);
            }
            var poleMesh = SaveMesh(LowPolyMeshes.Prism("TentPole", 6, 0.15f, WallHeight + RoofHeight + 1.6f));
            MeshObject(tent, "Pole", poleMesh, M("WoodDark"), new Vector3(0f, (WallHeight + RoofHeight + 1.6f) * 0.5f, 0f), Quaternion.identity, Vector3.one);
            Box(tent, "Finial", new Vector3(0f, WallHeight + RoofHeight + 1.65f, 0f), new Vector3(0.25f, 0.25f, 0.25f), M("Gold"), false, Quaternion.Euler(45f, 0f, 45f));
            Cloth(tent, "Pennant", new Vector3(0.1f, WallHeight + RoofHeight + 1.4f, 0f), Quaternion.identity, 2.2f, 0.8f, M("AttackerBanner"), WindCloth.PinnedEdge.Left, 0.3f, 10, 3, 3.2f);
            AddPointLight(tent, new Vector3(0f, 3.2f, 0f), new Color(1f, 0.78f, 0.5f), 11f, 1.3f);

            // Battle-map table in the centre.
            BuildMapTable(tent, mapSurfaces);

            // Workshop at the back with the armorer.
            BuildWorkshop(tent, armorerName, prefabs, placements);
            return tent;
        }

        private static int EntranceIndex()
        {
            // Corner i sits at angle i * 45°; the panel between corners 1 and 2 faces +Z (90°).
            return 1;
        }

        private static Vector3 Corner(int i)
        {
            float angle = (i + 0.5f) / Sides * Mathf.PI * 2f;
            return new Vector3(Mathf.Cos(angle) * TentRadius, 0f, Mathf.Sin(angle) * TentRadius);
        }

        private static void BuildMapTable(Transform tent, List<Renderer> mapSurfaces)
        {
            var table = Group(tent, "MapTable", new Vector3(0f, 0.1f, 1.2f));
            foreach (var x in new[] { -1.4f, 1.4f })
                foreach (var z in new[] { -1.4f, 1.4f })
                    Box(table, "Leg", new Vector3(x, 0.45f, z), new Vector3(0.15f, 0.9f, 0.15f), M("WoodDark"));
            Box(table, "Frame", new Vector3(0f, 0.92f, 0f), new Vector3(3.4f, 0.08f, 3.4f), M("WoodDark"));
            var surface = Box(table, "MapSurface", new Vector3(0f, 0.97f, 0f), new Vector3(3.1f, 0.03f, 3.1f), M("BattleMap"));
            mapSurfaces.Add(surface.GetComponent<Renderer>());

            // Figurines and a candle on the rim.
            Box(table, "Figure", new Vector3(1.6f, 1.05f, 1.5f), new Vector3(0.1f, 0.18f, 0.1f), M("AttackerBanner"), false);
            Box(table, "Figure", new Vector3(1.6f, 1.05f, 1.3f), new Vector3(0.1f, 0.18f, 0.1f), M("DefenderBanner"), false);
            Box(table, "Candle", new Vector3(-1.62f, 1.05f, -1.55f), new Vector3(0.08f, 0.2f, 0.08f), M("ClothCream"), false);
            Box(table, "CandleFlame", new Vector3(-1.62f, 1.18f, -1.55f), new Vector3(0.05f, 0.07f, 0.05f), M("Fire"), false);
        }

        private static void BuildWorkshop(Transform tent, string armorerName, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements)
        {
            var shop = Group(tent, "Workshop", new Vector3(0f, 0.1f, -3.6f));

            Box(shop, "Counter", new Vector3(0f, 0.5f, 1.1f), new Vector3(3.2f, 1f, 0.7f), M("Wood"));
            Box(shop, "CounterTop", new Vector3(0f, 1.03f, 1.1f), new Vector3(3.3f, 0.06f, 0.8f), M("WoodLight"), false);

            // Wares on the counter.
            Box(shop, "Helmet", new Vector3(-1f, 1.2f, 1.1f), new Vector3(0.35f, 0.3f, 0.35f), M("Metal"), false);
            Box(shop, "Shield", new Vector3(1f, 1.3f, 1.2f), new Vector3(0.6f, 0.5f, 0.06f), M("AttackerBanner"), false, Quaternion.Euler(-15f, 0f, 0f));
            Box(shop, "Bolts", new Vector3(0.1f, 1.1f, 1.05f), new Vector3(0.5f, 0.1f, 0.3f), M("Iron"), false);

            // Forge and anvil.
            Box(shop, "Forge", new Vector3(-2.3f, 0.55f, -0.6f), new Vector3(1.2f, 1.1f, 1.2f), M("BrickDark"));
            Box(shop, "Coals", new Vector3(-2.3f, 1.12f, -0.6f), new Vector3(0.8f, 0.06f, 0.8f), M("Coal"), false);
            AddPointLight(shop, new Vector3(-2.3f, 1.6f, -0.6f), new Color(1f, 0.5f, 0.2f), 6f, 1.8f);
            Box(shop, "AnvilBase", new Vector3(1.4f, 0.35f, -0.3f), new Vector3(0.35f, 0.7f, 0.45f), M("WoodDark"));
            var anvil = Box(shop, "Anvil", new Vector3(1.4f, 0.8f, -0.3f), new Vector3(0.8f, 0.22f, 0.36f), M("Iron"));
            var anvilTop = Group(shop, "AnvilTop", new Vector3(1.2f, 0.95f, -0.3f));

            // Weapon racks with rifles and hammers.
            foreach (var x in new[] { -3.2f, 3.2f })
            {
                var rack = Group(shop, "Rack", new Vector3(x, 0f, 0.3f), Quaternion.Euler(0f, x < 0 ? 90f : -90f, 0f));
                Box(rack, "Frame", new Vector3(0f, 0.9f, 0f), new Vector3(1.6f, 0.08f, 0.3f), M("WoodDark"));
                Box(rack, "Base", new Vector3(0f, 0.1f, 0f), new Vector3(1.6f, 0.2f, 0.4f), M("WoodDark"));
                for (int i = 0; i < 4; i++)
                {
                    Box(rack, "Rifle", new Vector3(-0.6f + i * 0.4f, 0.75f, 0.05f), new Vector3(0.07f, 1.3f, 0.1f), M("Iron"), false, Quaternion.Euler(0f, 0f, 8f));
                }
            }

            // The armorer (faces the entrance, i.e. local +Z).
            var npcRoot = Group(shop, "Armorer", new Vector3(0.3f, 0f, 0.2f));
            var body = Group(npcRoot, "Body", Vector3.zero);
            Box(body, "LegL", new Vector3(-0.15f, 0.45f, 0f), new Vector3(0.2f, 0.9f, 0.25f), M("WoodDark"), false);
            Box(body, "LegR", new Vector3(0.15f, 0.45f, 0f), new Vector3(0.2f, 0.9f, 0.25f), M("WoodDark"), false);
            Box(body, "Torso", new Vector3(0f, 1.25f, 0f), new Vector3(0.62f, 0.75f, 0.38f), M("Shirt"), false);
            Box(body, "Belly", new Vector3(0f, 1.15f, 0.1f), new Vector3(0.55f, 0.45f, 0.3f), M("Shirt"), false);
            Box(body, "Apron", new Vector3(0f, 1.0f, 0.24f), new Vector3(0.5f, 0.9f, 0.04f), M("Apron"), false);
            Box(body, "Head", new Vector3(0f, 1.82f, 0f), new Vector3(0.34f, 0.36f, 0.34f), M("Skin"), false);
            Box(body, "Beard", new Vector3(0f, 1.7f, 0.15f), new Vector3(0.3f, 0.26f, 0.1f), M("Beard"), false);
            Box(body, "Hair", new Vector3(0f, 2.0f, -0.02f), new Vector3(0.36f, 0.08f, 0.36f), M("Beard"), false);
            Box(body, "Nose", new Vector3(0f, 1.82f, 0.19f), new Vector3(0.07f, 0.08f, 0.06f), M("Skin"), false);
            Box(body, "ArmL", new Vector3(-0.4f, 1.25f, 0f), new Vector3(0.16f, 0.65f, 0.18f), M("Skin"), false);
            var arm = Group(body, "HammerArm", new Vector3(0.4f, 1.55f, 0f));
            Box(arm, "Arm", new Vector3(0f, -0.3f, 0f), new Vector3(0.16f, 0.62f, 0.18f), M("Skin"), false);
            Box(arm, "Handle", new Vector3(0f, -0.6f, 0.25f), new Vector3(0.05f, 0.05f, 0.55f), M("WoodDark"), false);
            Box(arm, "HammerHead", new Vector3(0f, -0.6f, 0.52f), new Vector3(0.12f, 0.22f, 0.12f), M("Iron"), false);
            var npcCollider = npcRoot.gameObject.AddComponent<CapsuleCollider>();
            npcCollider.center = new Vector3(0f, 1f, 0f);
            npcCollider.height = 2f;
            npcCollider.radius = 0.35f;

            var armorer = npcRoot.gameObject.AddComponent<ArmorerNpc>();
            armorer.EditorSetup(armorerName, body, arm, anvilTop, M("Spark"));

            // Crate of powder charges for sappers.
            Box(shop, "ChargeTable", new Vector3(2.4f, 0.45f, 1.9f), new Vector3(1.4f, 0.9f, 0.9f), M("WoodDark"));
            for (int i = 0; i < 3; i++)
            {
                CastleBuilder.Add(placements, prefabs.PowderCharge, shop.TransformPoint(new Vector3(2f + i * 0.4f, 1.15f, 1.9f)), shop.eulerAngles.y);
            }
        }

        #endregion

        #region Siege park and dressing

        private static void BuildSiegePark(Transform camp, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements, System.Random random)
        {
            // Cannons at the front of the camp, facing the castle, each with a pile of balls.
            foreach (var x in new[] { -13f, 0f, 13f })
            {
                float z = x == 0f ? -78f : -80f;
                Vector3 position = OnGround(x, z, 0.2f);
                Quaternion facing = Quaternion.LookRotation(new Vector3(-position.x, 0f, -position.z));
                placements.Add(new PropPlacement { prefab = prefabs.Cannon, position = position, rotation = facing });

                for (int i = 0; i < 5; i++)
                {
                    Vector3 ball = OnGround(x - 2.2f + (i % 3) * 0.45f, z - 1.5f - (i / 3) * 0.45f, 0.25f + (i / 3) * 0.3f);
                    CastleBuilder.Add(placements, prefabs.Cannonball, ball, 0f);
                }
                Box(camp, "BallFrame", OnGround(x - 1.75f, z - 1.7f, 0.05f), new Vector3(1.6f, 0.1f, 1.1f), M("WoodDark"), false);
            }

            // Ammunition depot.
            Box(camp, "DepotFrame", OnGround(-25f, -90f, 0.05f), new Vector3(2.4f, 0.1f, 2.4f), M("WoodDark"), false);
            for (int i = 0; i < 12; i++)
            {
                CastleBuilder.Add(placements, prefabs.Cannonball, OnGround(-26f + (i % 4) * 0.5f, -91f + (i / 4 % 3) * 0.5f, 0.3f), 0f);
            }

            // Rams.
            foreach (var x in new[] { -6f, 6f })
            {
                Vector3 position = OnGround(x, -86f, 0.3f);
                placements.Add(new PropPlacement { prefab = prefabs.Ram, position = position, rotation = Quaternion.LookRotation(new Vector3(-position.x * 0.2f, 0f, -position.z)) });
            }

            // Ladders lying by the palisade.
            for (int i = 0; i < 6; i++)
            {
                placements.Add(new PropPlacement { prefab = prefabs.Ladder, position = OnGround(24f + i * 0.9f, -90f, 0.25f), rotation = Quaternion.Euler(-90f, 0f, 0f) });
            }
        }

        private static void BuildCampDressing(Transform camp, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements, System.Random random)
        {
            // Campfires with log seats.
            var stonesMesh = SaveMesh(LowPolyMeshes.Prism("FireRing", 8, 0.8f, 0.3f));
            foreach (var spot in new[] { new Vector2(-8f, -96f), new Vector2(8f, -96f), new Vector2(0f, -122f) })
            {
                var fire = Group(camp, "Campfire", OnGround(spot.x, spot.y));
                MeshObject(fire, "Ring", stonesMesh, M("Rock"), new Vector3(0, 0.15f, 0), Quaternion.identity, Vector3.one);
                Box(fire, "Flames", new Vector3(0, 0.45f, 0), new Vector3(0.5f, 0.6f, 0.5f), M("Fire"), false, Quaternion.Euler(0, 45, 0));
                Box(fire, "Flames", new Vector3(0, 0.35f, 0), new Vector3(0.6f, 0.4f, 0.6f), M("Coal"), false);
                AddPointLight(fire, new Vector3(0, 1.2f, 0), new Color(1f, 0.6f, 0.25f), 12f, 2.2f);
                Box(fire, "Seat", new Vector3(1.8f, 0.2f, 0), new Vector3(0.4f, 0.4f, 1.8f), M("WoodDark"));
                Box(fire, "Seat", new Vector3(-1.8f, 0.2f, 0), new Vector3(0.4f, 0.4f, 1.8f), M("WoodDark"));
                Box(fire, "Spit", new Vector3(0, 1f, 0), new Vector3(1.4f, 0.05f, 0.05f), M("Iron"), false);
            }

            // Banner poles around the camp.
            foreach (var spot in new[] { new Vector2(-30f, -84f), new Vector2(30f, -84f), new Vector2(-10f, -72f), new Vector2(10f, -72f) })
            {
                var pole = Group(camp, "Banner", OnGround(spot.x, spot.y));
                Box(pole, "Pole", new Vector3(0, 3f, 0), new Vector3(0.14f, 6f, 0.14f), M("WoodDark"));
                Box(pole, "Top", new Vector3(0, 6.1f, 0), new Vector3(0.2f, 0.2f, 0.2f), M("Gold"), false);
                Cloth(pole, "Flag", new Vector3(0.08f, 5.9f, 0f), Quaternion.Euler(0f, 90f, 0f), 2.4f, 1.4f, M("AttackerBanner"), WindCloth.PinnedEdge.Left, 0.3f, 10, 5, 2.8f);
            }

            // Palisade around the back and sides (open towards the castle).
            for (int i = 0; i < 90; i++)
            {
                float angle = Mathf.Lerp(200f, 340f, i / 89f) * Mathf.Deg2Rad;
                float radius = CampRadius + 2f;
                float x = CampCenter.x + Mathf.Cos(angle) * radius;
                float z = CampCenter.z + Mathf.Sin(angle) * radius;
                float height = 2.2f + (float)random.NextDouble() * 0.5f;
                Box(camp, "Stake", OnGround(x, z, height * 0.5f - 0.2f), new Vector3(0.35f, height, 0.35f), M("WoodDark"), true, Quaternion.Euler(0f, (float)random.NextDouble() * 40f, 0f));
            }

            // Supplies.
            for (int i = 0; i < 6; i++) CastleBuilder.Add(placements, prefabs.Crate, OnGround(-30f + (i % 3) * 1.1f, -100f + (i / 3) * 1.1f, 0.55f), random.Next(-10, 10));
            for (int i = 0; i < 4; i++) CastleBuilder.Add(placements, prefabs.Barrel, OnGround(28f + (i % 2) * 0.8f, -100f + (i / 2) * 0.8f, 0.5f), 0f);
            CastleBuilder.Add(placements, prefabs.Cart, OnGround(-22f, -112f, 0.05f), 60f);
            CastleBuilder.Add(placements, prefabs.HayBale, OnGround(22f, -114f, 0.35f), 20f);
            CastleBuilder.Add(placements, prefabs.HayBale, OnGround(23.3f, -114f, 0.35f), 20f);

            // Barrier dome shown until the assault starts.
            var barrierRoot = new GameObject("AttackerBarrier");
            barrierRoot.transform.SetParent(camp, false);
            barrierRoot.transform.position = OnGround(CampCenter.x, CampCenter.z);
            var dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = "Dome";
            Object.DestroyImmediate(dome.GetComponent<Collider>());
            dome.transform.SetParent(barrierRoot.transform, false);
            dome.transform.localScale = Vector3.one * CampRadius * 2f;
            var domeRenderer = dome.GetComponent<MeshRenderer>();
            domeRenderer.sharedMaterial = M("AttackerBarrier");
            domeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var visibility = barrierRoot.AddComponent<PhaseVisibility>();
            var v = new UnityEditor.SerializedObject(visibility);
            v.FindProperty("target").objectReferenceValue = dome;
            v.ApplyModifiedPropertiesWithoutUndo();
        }

        #endregion
    }
}
