using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using static Remains.Editor.BuildKit;

namespace Remains.Editor
{
    /// <summary>
    /// The intact castle on the hill: destructible brick curtain walls between indestructible towers,
    /// a gatehouse with a wooden gate, wall-walk stairs, the keep and a busy courtyard full of barricade material.
    /// </summary>
    public static class CastleBuilder
    {
        private const int Columns = 6;
        private const int Rows = 10;
        private const int MerlonRows = 2;
        private static readonly Vector3 BlockSize = new Vector3(1f, 0.75f, 1.8f);
        private const float TowerRadius = 3.8f;
        private const float P = PlateauHeight;
        private const float H = CastleHalfSize;
        private const float WallTop = P + (Rows - MerlonRows) * 0.75f;

        public static void Build(Transform environment, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements, System.Random random)
        {
            var castle = Group(environment, "Castle", Vector3.zero);
            BuildWalls(castle, random);
            BuildTowers(castle);
            BuildGatehouse(castle, prefabs, placements);
            BuildStairs(castle);
            BuildKeep(castle, placements, prefabs);
            BuildCourtyard(castle, placements, prefabs, random);
        }

        #region Walls

        private static void BuildWalls(Transform castle, System.Random random)
        {
            var walls = Group(castle, "Walls", Vector3.zero);
            float[] starts = { -12f, -6f, 0f, 6f };

            foreach (var start in starts)
            {
                // North: local +x = world +x.
                CreateSection(walls, $"Wall_N_{start}", new Vector3(start, P, H), Quaternion.identity, 1f, random);
                // East / west: local +x = world +z.
                CreateSection(walls, $"Wall_E_{start}", new Vector3(H, P, start), Quaternion.Euler(0f, -90f, 0f), -1f, random);
                CreateSection(walls, $"Wall_W_{start}", new Vector3(-H, P, start), Quaternion.Euler(0f, -90f, 0f), 1f, random);
            }

            // South: the gatehouse occupies the middle.
            CreateSection(walls, "Wall_S_-12", new Vector3(-12f, P, -H), Quaternion.identity, -1f, random);
            CreateSection(walls, "Wall_S_6", new Vector3(6f, P, -H), Quaternion.identity, -1f, random);

            // Buttresses on the outer face at section seams (they also explain why seams hold the wall up).
            foreach (var x in new[] { -6f, 0f, 6f })
            {
                Box(walls, "Buttress", new Vector3(x, P + 3f, H + 1.3f), new Vector3(0.9f, 6f, 1f), M("BrickDark"));
                Box(walls, "Buttress", new Vector3(H + 1.3f, P + 3f, x), new Vector3(1f, 6f, 0.9f), M("BrickDark"));
                Box(walls, "Buttress", new Vector3(-H - 1.3f, P + 3f, x), new Vector3(1f, 6f, 0.9f), M("BrickDark"));
            }
        }

        /// <param name="outward">+1 if local +Z points out of the castle, -1 otherwise (merlons sit on the outer edge).</param>
        private static void CreateSection(Transform parent, string name, Vector3 origin, Quaternion rotation, float outward, System.Random random)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(origin, rotation);
            SceneNetworkObject(root);

            var blocks = new GameObject[Columns * Rows];
            var brickMaterials = new[] { M("Brick1"), M("Brick2"), M("Brick3") };
            for (int r = 0; r < Rows; r++)
            {
                bool merlonRow = r >= Rows - MerlonRows;
                for (int c = 0; c < Columns; c++)
                {
                    if (merlonRow && c % 2 == 1) continue;

                    int index = r * Columns + c;
                    var material = r == 0 ? M("BrickDark") : brickMaterials[random.Next(brickMaterials.Length)];
                    // Slight size jitter makes the courses read as masonry.
                    float jitter = 0.97f + (float)random.NextDouble() * 0.03f;
                    // Merlons are thin and sit on the outer edge, leaving a wall-walk behind them.
                    const float merlonDepth = 0.55f;
                    float z = merlonRow ? outward * (BlockSize.z - merlonDepth) * 0.5f : 0f;
                    var block = Box(root.transform, $"B_{r}_{c}",
                        new Vector3((c + 0.5f) * BlockSize.x, (r + 0.5f) * BlockSize.y, z),
                        new Vector3(BlockSize.x * jitter, BlockSize.y, merlonRow ? merlonDepth : BlockSize.z),
                        material);
                    blocks[index] = block;
                }
            }

            var wall = root.AddComponent<WallSection>();
            wall.EditorSetup(Columns, Rows, BlockSize, blocks, 160f, true, true, M("Dust"));
        }

        #endregion

        #region Towers, gate, stairs

        private static void BuildTowers(Transform castle)
        {
            var towerMesh = SaveMesh(LowPolyMeshes.Prism("Tower", 8, TowerRadius, 12f));
            var towerBase = SaveMesh(LowPolyMeshes.Prism("TowerBase", 8, TowerRadius + 0.5f, 2f));
            var roof = SaveMesh(LowPolyMeshes.Cone("TowerRoof", 8, TowerRadius + 0.6f, 5f));

            int index = 0;
            foreach (var x in new[] { -H, H })
            {
                foreach (var z in new[] { -H, H })
                {
                    var tower = Group(castle, $"Tower_{index}", new Vector3(x, P, z));
                    MeshObject(tower, "Base", towerBase, M("BrickDark"), new Vector3(0, 0f, 0), Quaternion.identity, Vector3.one, true);
                    MeshObject(tower, "Shaft", towerMesh, M("Brick1"), new Vector3(0, 6f, 0), Quaternion.identity, Vector3.one, true);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = (i + 0.5f) / 8f * Mathf.PI * 2f;
                        Box(tower, "Merlon", new Vector3(Mathf.Cos(angle) * (TowerRadius - 0.3f), 12.5f, Mathf.Sin(angle) * (TowerRadius - 0.3f)),
                            new Vector3(1.1f, 1f, 0.6f), M("Brick2"), true, Quaternion.Euler(0f, -angle * Mathf.Rad2Deg + 90f, 0f));
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        float angle = (i * 120f + 45f) * Mathf.Deg2Rad;
                        Box(tower, "Slit", new Vector3(Mathf.Cos(angle) * TowerRadius, 7f, Mathf.Sin(angle) * TowerRadius), new Vector3(0.25f, 1.4f, 0.25f), M("WindowDark"), false);
                    }

                    if (index == 3)
                    {
                        MeshObject(tower, "Roof", roof, M("RoofBlue"), new Vector3(0, 12f, 0), Quaternion.identity, Vector3.one);
                        Box(tower, "Pole", new Vector3(0, 18f, 0), new Vector3(0.12f, 3f, 0.12f), M("WoodDark"), false);
                        Cloth(tower, "Pennant", new Vector3(0.06f, 19.3f, 0f), Quaternion.identity, 2.4f, 1f, M("DefenderBanner"), WindCloth.PinnedEdge.Left, 0.35f, 10, 4, 3f);
                    }
                    else
                    {
                        Box(tower, "Pole", new Vector3(0, 13.5f, 0), new Vector3(0.12f, 3f, 0.12f), M("WoodDark"), false);
                        Cloth(tower, "Flag", new Vector3(0.06f, 14.9f, 0f), Quaternion.identity, 1.8f, 1.1f, M("DefenderBanner"), WindCloth.PinnedEdge.Left, 0.3f, 8, 4, 2.8f);
                    }
                    index++;
                }
            }
        }

        private static void BuildGatehouse(Transform castle, PrefabBuilder.Prefabs prefabs, List<PropPlacement> placements)
        {
            var gatehouse = Group(castle, "Gatehouse", new Vector3(0f, P, -H));
            foreach (var x in new[] { -4.5f, 4.5f })
            {
                Box(gatehouse, "GateTower", new Vector3(x, 5.5f, 0f), new Vector3(3f, 11f, 3.4f), M("Brick1"));
                for (int i = 0; i < 3; i++)
                {
                    Box(gatehouse, "Merlon", new Vector3(x - 1f + i, 11.45f, -1.45f), new Vector3(0.6f, 0.9f, 0.5f), M("Brick2"));
                }
            }
            // Arch lintel over the gate passage.
            Box(gatehouse, "Lintel", new Vector3(0f, 7.9f, 0f), new Vector3(6f, 6.2f, 3.4f), M("Brick3"));
            Box(gatehouse, "Portcullis", new Vector3(0f, 5.2f, -1.2f), new Vector3(5.8f, 0.6f, 0.15f), M("Iron"), false);
            Box(gatehouse, "Crest", new Vector3(0f, 8.5f, -1.75f), new Vector3(1.4f, 1.6f, 0.1f), M("DefenderBanner"), false);
            Cloth(gatehouse, "BannerL", new Vector3(-5.2f, 10.5f, -1.8f), Quaternion.identity, 1.4f, 3.4f, M("DefenderBanner"), WindCloth.PinnedEdge.Top, 0.12f, 4, 8, 1.6f);
            Cloth(gatehouse, "BannerR", new Vector3(3.8f, 10.5f, -1.8f), Quaternion.identity, 1.4f, 3.4f, M("DefenderBanner"), WindCloth.PinnedEdge.Top, 0.12f, 4, 8, 1.6f);

            // The gate itself is a round object so it comes back intact every round.
            placements.Add(new PropPlacement { prefab = prefabs.Gate, position = new Vector3(0f, P, -H - 0.6f), rotation = Quaternion.identity });

            // Torches by the gate.
            foreach (var x in new[] { -3.3f, 3.3f })
            {
                Box(gatehouse, "Torch", new Vector3(x, 3.2f, -1.9f), new Vector3(0.12f, 0.7f, 0.12f), M("WoodDark"), false);
                Box(gatehouse, "Flame", new Vector3(x, 3.7f, -1.9f), new Vector3(0.22f, 0.3f, 0.22f), M("Fire"), false);
                AddPointLight(gatehouse, new Vector3(x, 3.9f, -2.3f), new Color(1f, 0.62f, 0.3f), 9f, 1.6f);
            }
        }

        private static void BuildStairs(Transform castle)
        {
            // Stone stairs up to the wall-walk along the inside of the west and east walls.
            const int steps = 20;
            float rise = (WallTop - P) / steps;
            foreach (var side in new[] { -1f, 1f })
            {
                var stairs = Group(castle, side < 0 ? "Stairs_W" : "Stairs_E", Vector3.zero);
                float x = side * (H - BlockSize.z * 0.5f - 0.8f);
                for (int i = 0; i < steps; i++)
                {
                    float top = (i + 1) * rise;
                    float z = side < 0 ? 9f - i * 0.5f : -9f + i * 0.5f;
                    var step = Box(stairs, "Step", new Vector3(x, P + top * 0.5f, z), new Vector3(1.5f, top, 0.5f), M("Cobble"));
                    step.layer = GroundLayer;
                }
            }
        }

        #endregion

        #region Keep and courtyard

        private static void BuildKeep(Transform castle, List<PropPlacement> placements, PrefabBuilder.Prefabs prefabs)
        {
            var keep = Group(castle, "Keep", new Vector3(0f, P, 7f));
            const float width = 10f, depth = 8f, height = 9f, thickness = 0.6f;

            Box(keep, "WallN", new Vector3(0, height / 2f, depth / 2f), new Vector3(width, height, thickness), M("Brick2"));
            Box(keep, "WallE", new Vector3(width / 2f, height / 2f, 0), new Vector3(thickness, height, depth), M("Brick1"));
            Box(keep, "WallW", new Vector3(-width / 2f, height / 2f, 0), new Vector3(thickness, height, depth), M("Brick1"));
            Box(keep, "WallS_L", new Vector3(-2.95f, height / 2f, -depth / 2f), new Vector3(4.1f, height, thickness), M("Brick3"));
            Box(keep, "WallS_R", new Vector3(2.95f, height / 2f, -depth / 2f), new Vector3(4.1f, height, thickness), M("Brick3"));
            Box(keep, "WallS_Top", new Vector3(0f, (height + 2.7f) / 2f, -depth / 2f), new Vector3(1.8f, height - 2.7f, thickness), M("Brick3"));
            Box(keep, "Ceiling", new Vector3(0, height, 0), new Vector3(width + 0.4f, 0.4f, depth + 0.4f), M("BrickDark"));
            var roofMesh = SaveMesh(LowPolyMeshes.Cone("KeepRoof", 4, 7.4f, 5f));
            MeshObject(keep, "Roof", roofMesh, M("RoofSlate"), new Vector3(0, height + 0.2f, 0), Quaternion.Euler(0, 45, 0), new Vector3(1f, 1f, 0.82f));

            foreach (var x in new[] { -3f, 3f })
            {
                Box(keep, "Window", new Vector3(x, 5.5f, -depth / 2f - 0.31f), new Vector3(0.9f, 1.4f, 0.05f), M("WindowDark"), false);
                Box(keep, "Window", new Vector3(x, 5.5f, depth / 2f + 0.31f), new Vector3(0.9f, 1.4f, 0.05f), M("WindowDark"), false);
            }
            Cloth(keep, "KeepBanner", new Vector3(-0.7f, 8.6f, -depth / 2f - 0.35f), Quaternion.identity, 1.4f, 3.2f, M("DefenderBanner"), WindCloth.PinnedEdge.Top, 0.1f, 4, 8, 1.4f);

            // Door.
            var doorRoot = new GameObject("KeepDoor");
            doorRoot.transform.SetParent(keep, false);
            doorRoot.transform.localPosition = new Vector3(0f, 0f, -depth / 2f);
            SceneNetworkObject(doorRoot);
            var hinge = Group(doorRoot.transform, "Hinge", new Vector3(-0.9f, 0f, 0f));
            Box(hinge, "Leaf", new Vector3(0.9f, 1.35f, 0f), new Vector3(1.78f, 2.7f, 0.12f), M("WoodDark"));
            Box(hinge, "Iron", new Vector3(0.9f, 0.6f, -0.07f), new Vector3(1.7f, 0.12f, 0.02f), M("Iron"), false);
            Box(hinge, "Iron", new Vector3(0.9f, 2.1f, -0.07f), new Vector3(1.7f, 0.12f, 0.02f), M("Iron"), false);
            doorRoot.AddComponent<InteractiveDoor>().EditorSetup(hinge, "Дверь донжона", -105f);

            // Interior: the throne room with the key objective.
            Box(keep, "WeaponRack", new Vector3(4.3f, 1f, -1.5f), new Vector3(0.3f, 2f, 2.4f), M("WoodDark"));
            Box(keep, "Hearth", new Vector3(-4.2f, 0.6f, -1.5f), new Vector3(1f, 1.2f, 2f), M("BrickDark"));
            Box(keep, "HearthFire", new Vector3(-3.9f, 0.5f, -1.5f), new Vector3(0.5f, 0.4f, 1.2f), M("Coal"), false);
            AddPointLight(keep, new Vector3(-3f, 1.5f, -1.5f), new Color(1f, 0.6f, 0.3f), 8f, 1.4f);
            BuildThroneRoom(keep);

            // Loose furniture near the door: material to barricade the throne room.
            Add(placements, prefabs.Barrel, keep.TransformPoint(new Vector3(3.8f, 0.5f, -2.9f)), 0f);
            Add(placements, prefabs.Crate, keep.TransformPoint(new Vector3(-3.7f, 0.55f, -3f)), 10f);
            Add(placements, prefabs.Bench, keep.TransformPoint(new Vector3(3.2f, 0.05f, 0.8f)), 90f);
            Add(placements, prefabs.Table, keep.TransformPoint(new Vector3(-3.3f, 0.05f, 1.2f)), 90f);
        }

        #region Throne room and objective

        private static void BuildThroneRoom(Transform keep)
        {
            // Dais, carpet runner, banners and candelabras.
            Box(keep, "Dais", new Vector3(0f, 0.15f, 2.3f), new Vector3(4.4f, 0.3f, 3f), M("Cobble"));
            Box(keep, "DaisStep", new Vector3(0f, 0.08f, 0.6f), new Vector3(3f, 0.16f, 0.5f), M("Cobble"));
            Box(keep, "Runner", new Vector3(0f, 0.02f, -1.6f), new Vector3(1.4f, 0.03f, 4.6f), M("ClothRed"), false);
            Box(keep, "DaisCarpet", new Vector3(0f, 0.31f, 2.3f), new Vector3(3.4f, 0.02f, 2.4f), M("ClothRed"), false);
            foreach (var x in new[] { -2.6f, 2.6f })
            {
                Box(keep, "BannerRod", new Vector3(x, 5.2f, 3.6f), new Vector3(1.4f, 0.06f, 0.06f), M("Gold"), false);
                Box(keep, "RoyalBanner", new Vector3(x, 3.9f, 3.65f), new Vector3(1.2f, 2.6f, 0.03f), M("RoyalPurple"), false);
                Box(keep, "BannerCrest", new Vector3(x, 4.2f, 3.62f), new Vector3(0.5f, 0.5f, 0.02f), M("Gold"), false, Quaternion.Euler(0, 0, 45));

                var candelabra = Group(keep, "Candelabra", new Vector3(x * 0.85f, 0.3f, 0.9f));
                Box(candelabra, "Stand", new Vector3(0, 0.7f, 0), new Vector3(0.08f, 1.4f, 0.08f), M("Gold"));
                Box(candelabra, "Arms", new Vector3(0, 1.4f, 0), new Vector3(0.6f, 0.05f, 0.05f), M("Gold"), false);
                foreach (var dx in new[] { -0.28f, 0f, 0.28f })
                {
                    Box(candelabra, "Candle", new Vector3(dx, 1.52f, 0), new Vector3(0.05f, 0.18f, 0.05f), M("Lace"), false);
                    Box(candelabra, "Flame", new Vector3(dx, 1.65f, 0), new Vector3(0.04f, 0.07f, 0.04f), M("Fire"), false);
                }
                AddPointLight(candelabra, new Vector3(0, 1.8f, 0), new Color(1f, 0.75f, 0.45f), 6f, 1.1f);
            }

            // Throne.
            var throne = Group(keep, "Throne", new Vector3(0f, 0.3f, 3.2f));
            Box(throne, "Seat", new Vector3(0, 0.3f, 0), new Vector3(1.1f, 0.6f, 0.8f), M("WoodDark"));
            Box(throne, "Cushion", new Vector3(0, 0.63f, 0.02f), new Vector3(0.9f, 0.08f, 0.7f), M("ClothRed"), false);
            Box(throne, "Back", new Vector3(0, 1.3f, 0.35f), new Vector3(1.1f, 2.1f, 0.15f), M("WoodDark"));
            Box(throne, "BackCloth", new Vector3(0, 1.3f, 0.26f), new Vector3(0.8f, 1.7f, 0.03f), M("ClothRed"), false);
            Box(throne, "Top", new Vector3(0, 2.45f, 0.35f), new Vector3(0.5f, 0.3f, 0.12f), M("Gold"), false, Quaternion.Euler(0, 0, 45));
            foreach (var x in new[] { -0.55f, 0.55f })
            {
                Box(throne, "ArmRest", new Vector3(x, 0.8f, 0f), new Vector3(0.12f, 0.12f, 0.8f), M("Gold"), false);
                Box(throne, "Finial", new Vector3(x, 2.35f, 0.35f), new Vector3(0.14f, 0.14f, 0.14f), M("Gold"), false);
            }

            // The objective itself: both looks are built, the round decides which one is shown.
            var root = new GameObject("KeyObjective");
            root.transform.SetParent(keep, false);
            root.transform.localPosition = new Vector3(0f, 0.3f, 2.2f);
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            SceneNetworkObject(root);

            var queen = BuildQueen(root.transform);
            var treasure = BuildTreasure(root.transform, out var lid);

            var lightObject = new GameObject("Glow");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 2f, 0.8f);
            var glow = lightObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.range = 7f;
            glow.intensity = 2.2f;
            glow.color = new Color(1f, 0.85f, 0.5f);
            glow.shadows = LightShadows.None;

            root.AddComponent<CaptureObjective>().EditorSetup(queen, treasure, lid, glow, 3.6f);
        }

        private static GameObject BuildQueen(Transform parent)
        {
            // Local space: the queen faces +Z (the parent is turned towards the door).
            var queen = Group(parent, "Queen", Vector3.zero);
            var body = Group(queen, "Body", Vector3.zero);

            var skirt = SaveMesh(LowPolyMeshes.Cone("QueenSkirt", 10, 0.78f, 1.3f));
            var ruffle = SaveMesh(LowPolyMeshes.Cone("QueenRuffle", 10, 0.62f, 0.95f));
            var ring = SaveMesh(LowPolyMeshes.Prism("QueenRing", 10, 0.8f, 0.08f));
            var waist = SaveMesh(LowPolyMeshes.Prism("QueenWaist", 8, 0.19f, 0.26f));
            var ruff = SaveMesh(LowPolyMeshes.Prism("QueenRuff", 12, 0.22f, 0.06f));
            var puff = SaveMesh(LowPolyMeshes.Rock("QueenPuff", 21));
            var crownRing = SaveMesh(LowPolyMeshes.Prism("CrownRing", 8, 0.12f, 0.07f));
            var crownPoint = SaveMesh(LowPolyMeshes.Cone("CrownPoint", 4, 0.035f, 0.11f));
            var orb = SaveMesh(LowPolyMeshes.Rock("ScepterOrb", 5));

            // Layered ball gown.
            MeshObject(body, "Skirt", skirt, M("RoyalPurple"), new Vector3(0, 0.02f, 0), Quaternion.identity, Vector3.one);
            MeshObject(body, "Hem", ring, M("Gold"), new Vector3(0, 0.06f, 0), Quaternion.identity, Vector3.one);
            MeshObject(body, "Ruffle", ruffle, M("RoyalLight"), new Vector3(0, 0.38f, 0), Quaternion.Euler(0, 18, 0), Vector3.one);
            MeshObject(body, "RuffleTrim", ring, M("Lace"), new Vector3(0, 0.4f, 0), Quaternion.Euler(0, 18, 0), new Vector3(0.78f, 0.6f, 0.78f));
            Box(body, "FrontPanel", new Vector3(0, 0.55f, 0.36f), new Vector3(0.28f, 0.9f, 0.02f), M("Gold"), false, Quaternion.Euler(-28f, 0, 0));
            MeshObject(body, "Waist", waist, M("RoyalPurple"), new Vector3(0, 1.1f, 0), Quaternion.identity, Vector3.one);
            Box(body, "Bodice", new Vector3(0, 1.36f, 0), new Vector3(0.38f, 0.4f, 0.25f), M("RoyalPurple"), false);
            Box(body, "Stomacher", new Vector3(0, 1.33f, 0.13f), new Vector3(0.12f, 0.38f, 0.02f), M("Gold"), false);
            Box(body, "Brooch", new Vector3(0, 1.48f, 0.145f), new Vector3(0.06f, 0.06f, 0.02f), M("GemRed"), false);
            MeshObject(body, "Ruff", ruff, M("Lace"), new Vector3(0, 1.58f, 0), Quaternion.identity, Vector3.one);
            Box(body, "Neck", new Vector3(0, 1.64f, 0), new Vector3(0.08f, 0.1f, 0.08f), M("Skin"), false);
            MeshObject(body, "PuffL", puff, M("RoyalLight"), new Vector3(-0.25f, 1.49f, 0), Quaternion.identity, Vector3.one * 0.24f);
            MeshObject(body, "PuffR", puff, M("RoyalLight"), new Vector3(0.25f, 1.49f, 0), Quaternion.Euler(0, 90, 0), Vector3.one * 0.24f);

            // Ermine-trimmed cape.
            Box(body, "Cape", new Vector3(0, 0.85f, -0.27f), new Vector3(0.75f, 1.45f, 0.04f), M("ClothRed"), false, Quaternion.Euler(8f, 0, 0));
            Box(body, "CapeTrim", new Vector3(0, 1.55f, -0.18f), new Vector3(0.62f, 0.1f, 0.12f), M("Ermine"), false);

            // Arms: the right one holds a sceptre, the left a fan.
            var armL = Group(body, "ArmL", new Vector3(-0.26f, 1.47f, 0f));
            Box(armL, "Sleeve", new Vector3(0, -0.2f, 0), new Vector3(0.09f, 0.36f, 0.09f), M("Lace"), false);
            Box(armL, "Hand", new Vector3(0, -0.42f, 0), new Vector3(0.07f, 0.09f, 0.06f), M("Skin"), false);
            Box(armL, "Fan", new Vector3(0, -0.5f, 0.07f), new Vector3(0.22f, 0.16f, 0.01f), M("ClothRed"), false, Quaternion.Euler(0, 0, 20));
            var armR = Group(body, "ArmR", new Vector3(0.26f, 1.47f, 0f));
            Box(armR, "Sleeve", new Vector3(0, -0.2f, 0), new Vector3(0.09f, 0.36f, 0.09f), M("Lace"), false);
            Box(armR, "Hand", new Vector3(0, -0.42f, 0), new Vector3(0.07f, 0.09f, 0.06f), M("Skin"), false);
            Box(armR, "Sceptre", new Vector3(0, -0.36f, 0.08f), new Vector3(0.025f, 0.6f, 0.025f), M("Gold"), false);
            MeshObject(armR, "Orb", orb, M("GemBlue"), new Vector3(0, -0.04f, 0.08f), Quaternion.identity, Vector3.one * 0.09f);

            // Head with hair, face and crown.
            var head = Group(body, "Head", new Vector3(0, 1.68f, 0f));
            Box(head, "Face", new Vector3(0, 0.13f, 0), new Vector3(0.21f, 0.25f, 0.2f), M("Skin"), false);
            Box(head, "EyeL", new Vector3(-0.05f, 0.16f, 0.101f), new Vector3(0.03f, 0.025f, 0.01f), M("Eyes"), false);
            Box(head, "EyeR", new Vector3(0.05f, 0.16f, 0.101f), new Vector3(0.03f, 0.025f, 0.01f), M("Eyes"), false);
            Box(head, "Lips", new Vector3(0, 0.06f, 0.101f), new Vector3(0.06f, 0.02f, 0.01f), M("Lips"), false);
            Box(head, "HairTop", new Vector3(0, 0.25f, -0.01f), new Vector3(0.24f, 0.08f, 0.23f), M("HairGold"), false);
            Box(head, "HairBack", new Vector3(0, 0.1f, -0.1f), new Vector3(0.24f, 0.3f, 0.06f), M("HairGold"), false);
            Box(head, "Bun", new Vector3(0, 0.22f, -0.15f), new Vector3(0.12f, 0.12f, 0.1f), M("HairGold"), false, Quaternion.Euler(45, 0, 0));
            MeshObject(head, "CrownRing", crownRing, M("GoldBright"), new Vector3(0, 0.31f, 0), Quaternion.identity, Vector3.one);
            for (int i = 0; i < 5; i++)
            {
                float angle = i / 5f * Mathf.PI * 2f + Mathf.PI / 2f;
                var tip = new Vector3(Mathf.Cos(angle) * 0.11f, 0.34f, Mathf.Sin(angle) * 0.11f);
                MeshObject(head, "CrownPoint", crownPoint, M("GoldBright"), tip, Quaternion.identity, Vector3.one);
                Box(head, "Gem", tip + new Vector3(0, 0.11f, 0), new Vector3(0.025f, 0.025f, 0.025f), i % 2 == 0 ? M("GemRed") : M("GemBlue"), false, Quaternion.Euler(45, 45, 0));
            }
            Box(head, "CrownJewel", new Vector3(0, 0.31f, 0.12f), new Vector3(0.04f, 0.04f, 0.02f), M("GemGreen"), false, Quaternion.Euler(0, 0, 45));

            var body3 = new GameObject("Collider");
            body3.transform.SetParent(queen, false);
            var capsule = body3.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 0.95f, 0);
            capsule.height = 1.9f;
            capsule.radius = 0.5f;

            queen.gameObject.AddComponent<QueenAnimator>().EditorSetup(body, head, armL, armR);
            return queen.gameObject;
        }

        private static GameObject BuildTreasure(Transform parent, out Transform lid)
        {
            var treasure = Group(parent, "Treasure", Vector3.zero);
            var coinMesh = SaveMesh(LowPolyMeshes.Prism("Coin", 8, 0.07f, 0.015f));
            var pileMesh = SaveMesh(LowPolyMeshes.Cone("GoldPile", 8, 0.45f, 0.3f));
            var sackMesh = SaveMesh(LowPolyMeshes.Rock("GoldSack", 33));
            var goblet = SaveMesh(LowPolyMeshes.Prism("Goblet", 6, 0.06f, 0.2f));

            // The great chest.
            Box(treasure, "Chest", new Vector3(0, 0.35f, 0), new Vector3(1.3f, 0.7f, 0.85f), M("WoodDark"));
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                Box(treasure, "Band", new Vector3(x, 0.35f, 0), new Vector3(0.08f, 0.72f, 0.87f), M("Gold"), false);
            }
            Box(treasure, "Rim", new Vector3(0, 0.69f, 0), new Vector3(1.32f, 0.04f, 0.87f), M("Gold"), false);
            Box(treasure, "Lock", new Vector3(0, 0.55f, 0.435f), new Vector3(0.16f, 0.2f, 0.03f), M("GoldBright"), false);
            MeshObject(treasure, "GoldInside", pileMesh, M("GoldBright"), new Vector3(0, 0.62f, 0), Quaternion.identity, new Vector3(1.3f, 0.6f, 0.85f));
            Box(treasure, "GemInside", new Vector3(0.2f, 0.72f, 0.1f), new Vector3(0.08f, 0.08f, 0.08f), M("GemRed"), false, Quaternion.Euler(45, 30, 0));
            Box(treasure, "GemInside", new Vector3(-0.25f, 0.7f, -0.1f), new Vector3(0.07f, 0.07f, 0.07f), M("GemGreen"), false, Quaternion.Euler(30, 45, 0));

            var lidPivot = Group(treasure, "LidPivot", new Vector3(0f, 0.7f, -0.425f));
            Box(lidPivot, "Lid", new Vector3(0, 0.12f, 0.425f), new Vector3(1.34f, 0.24f, 0.87f), M("WoodDark"), false);
            Box(lidPivot, "LidBand", new Vector3(0, 0.25f, 0.425f), new Vector3(1.36f, 0.03f, 0.2f), M("Gold"), false);
            foreach (var x in new[] { -0.45f, 0.45f })
            {
                Box(lidPivot, "LidStrap", new Vector3(x, 0.12f, 0.425f), new Vector3(0.08f, 0.26f, 0.89f), M("Gold"), false);
            }
            lid = lidPivot;

            // Spilled riches around.
            var random = new System.Random(7);
            MeshObject(treasure, "Pile", pileMesh, M("GoldBright"), new Vector3(-1.1f, 0f, 0.4f), Quaternion.identity, Vector3.one);
            MeshObject(treasure, "Pile", pileMesh, M("GoldBright"), new Vector3(1.05f, 0f, 0.5f), Quaternion.Euler(0, 20, 0), Vector3.one * 0.8f);
            for (int i = 0; i < 26; i++)
            {
                var position = new Vector3((float)random.NextDouble() * 3f - 1.5f, 0.01f, (float)random.NextDouble() * 1.6f - 0.3f);
                MeshObject(treasure, "Coin", coinMesh, M("GoldBright"), position, Quaternion.Euler((float)random.NextDouble() * 20f, (float)random.NextDouble() * 360f, 0), Vector3.one);
            }
            MeshObject(treasure, "Sack", sackMesh, M("Sand"), new Vector3(1.2f, 0.25f, -0.4f), Quaternion.identity, new Vector3(0.55f, 0.6f, 0.55f));
            Box(treasure, "SackTie", new Vector3(1.2f, 0.52f, -0.4f), new Vector3(0.12f, 0.05f, 0.12f), M("Rope"), false);
            MeshObject(treasure, "Goblet", goblet, M("GoldBright"), new Vector3(-0.8f, 0.1f, 0.85f), Quaternion.identity, Vector3.one);
            MeshObject(treasure, "Goblet", goblet, M("GoldBright"), new Vector3(0.7f, 0.1f, 1f), Quaternion.Euler(0, 0, 80), Vector3.one);
            // A spare crown on a cushion.
            Box(treasure, "Cushion", new Vector3(-1.2f, 0.12f, -0.4f), new Vector3(0.45f, 0.18f, 0.45f), M("ClothRed"));
            var crownRing = SaveMesh(LowPolyMeshes.Prism("CrownRing", 8, 0.12f, 0.07f));
            MeshObject(treasure, "Crown", crownRing, M("GoldBright"), new Vector3(-1.2f, 0.26f, -0.4f), Quaternion.identity, Vector3.one * 1.3f);
            // Smaller closed chests.
            Box(treasure, "SmallChest", new Vector3(-1.3f, 0.2f, -1.1f), new Vector3(0.6f, 0.4f, 0.4f), M("Wood"), true, Quaternion.Euler(0, 15, 0));
            Box(treasure, "SmallChest", new Vector3(1.35f, 0.2f, -1.1f), new Vector3(0.6f, 0.4f, 0.4f), M("Wood"), true, Quaternion.Euler(0, -10, 0));
            return treasure.gameObject;
        }

        #endregion

        private static void BuildCourtyard(Transform castle, List<PropPlacement> placements, PrefabBuilder.Prefabs prefabs, System.Random random)
        {
            var yard = Group(castle, "Courtyard", new Vector3(0f, P, 0f));

            // Cobbled path from the gate to the keep.
            for (int i = 0; i < 7; i++)
            {
                var stone = Box(yard, "Cobble", new Vector3(0f, 0.02f, -13f + i * 2.2f), new Vector3(3f, 0.05f, 2.1f), M("Cobble"), false);
                stone.layer = GroundLayer;
            }

            // Well.
            var ringMesh = SaveMesh(LowPolyMeshes.Prism("WellRing", 8, 1.1f, 0.9f));
            var waterMesh = SaveMesh(LowPolyMeshes.Prism("WellWater", 8, 0.85f, 0.05f));
            var well = Group(yard, "Well", new Vector3(-6f, 0f, -4f));
            MeshObject(well, "Ring", ringMesh, M("Cobble"), new Vector3(0, 0.45f, 0), Quaternion.identity, Vector3.one, true);
            MeshObject(well, "Water", waterMesh, M("Water"), new Vector3(0, 0.75f, 0), Quaternion.identity, Vector3.one);
            Box(well, "PostL", new Vector3(-0.9f, 1.4f, 0), new Vector3(0.14f, 1.9f, 0.14f), M("WoodDark"));
            Box(well, "PostR", new Vector3(0.9f, 1.4f, 0), new Vector3(0.14f, 1.9f, 0.14f), M("WoodDark"));
            Box(well, "Roof", new Vector3(0, 2.45f, 0), new Vector3(2.3f, 0.12f, 1.6f), M("RoofRed"), false, Quaternion.Euler(0, 0, 0));
            Box(well, "Bucket", new Vector3(0.2f, 1.2f, 0), new Vector3(0.3f, 0.3f, 0.3f), M("Wood"), false);

            // Stables along the west wall.
            var stables = Group(yard, "Stables", new Vector3(-10.3f, 0f, 3f));
            foreach (var z in new[] { -3f, 0f, 3f })
            {
                Box(stables, "Post", new Vector3(1.5f, 1.5f, z), new Vector3(0.2f, 3f, 0.2f), M("WoodDark"));
            }
            Box(stables, "Roof", new Vector3(0f, 3.1f, 0f), new Vector3(4f, 0.15f, 7f), M("RoofRed"), true, Quaternion.Euler(0, 0, -14));
            Box(stables, "Trough", new Vector3(0.5f, 0.35f, -1.5f), new Vector3(0.6f, 0.5f, 2f), M("WoodDark"));
            Box(stables, "HayPile", new Vector3(-0.8f, 0.4f, 1.8f), new Vector3(1.6f, 0.8f, 2f), M("Hay"));
            Box(stables, "Stall", new Vector3(-0.2f, 0.6f, 0f), new Vector3(2f, 1.2f, 0.12f), M("Wood"));

            // Blacksmith by the east wall.
            var smithy = Group(yard, "Blacksmith", new Vector3(11.5f, 0f, -5f));
            Box(smithy, "Forge", new Vector3(1.2f, 0.6f, 0f), new Vector3(1.6f, 1.2f, 1.6f), M("BrickDark"));
            Box(smithy, "Coals", new Vector3(1.2f, 1.22f, 0f), new Vector3(1.1f, 0.08f, 1.1f), M("Coal"), false);
            Box(smithy, "Chimney", new Vector3(1.6f, 2.4f, 0.4f), new Vector3(0.8f, 2.6f, 0.8f), M("BrickDark"));
            Box(smithy, "AnvilBase", new Vector3(-0.6f, 0.35f, 0f), new Vector3(0.4f, 0.7f, 0.5f), M("WoodDark"));
            Box(smithy, "Anvil", new Vector3(-0.6f, 0.8f, 0f), new Vector3(0.9f, 0.25f, 0.4f), M("Iron"));
            Box(smithy, "Awning", new Vector3(0.2f, 2.9f, 0f), new Vector3(3.2f, 0.1f, 3f), M("Wood"), false, Quaternion.Euler(0, 0, 10));
            AddPointLight(smithy, new Vector3(1.2f, 1.8f, 0f), new Color(1f, 0.55f, 0.25f), 8f, 2f);

            // Market stall with a striped awning.
            var stall = Group(yard, "Stall", new Vector3(8f, 0f, -11.5f));
            foreach (var x in new[] { -1.2f, 1.2f })
                foreach (var z in new[] { -0.8f, 0.8f })
                    Box(stall, "Post", new Vector3(x, 1.1f, z), new Vector3(0.1f, 2.2f, 0.1f), M("WoodDark"));
            Box(stall, "Counter", new Vector3(0f, 0.8f, -0.6f), new Vector3(2.4f, 0.1f, 0.6f), M("WoodLight"));
            Cloth(stall, "Awning", new Vector3(-1.3f, 2.25f, -0.95f), Quaternion.Euler(-70f, 0f, 0f), 2.6f, 1.9f, M("ClothRed"), WindCloth.PinnedEdge.Top, 0.05f, 6, 3);

            // Training dummies.
            foreach (var x in new[] { -9f, -7f })
            {
                var dummy = Group(yard, "Dummy", new Vector3(x, 0f, -11f));
                Box(dummy, "Post", new Vector3(0, 0.9f, 0), new Vector3(0.12f, 1.8f, 0.12f), M("WoodDark"));
                Box(dummy, "Arms", new Vector3(0, 1.35f, 0), new Vector3(1f, 0.1f, 0.1f), M("WoodDark"));
                Box(dummy, "Sack", new Vector3(0, 1.2f, 0), new Vector3(0.45f, 0.7f, 0.35f), M("Sand"));
            }

            // Wall torches along the inner faces.
            foreach (var position in new[] { new Vector3(-8f, 3f, H - 1.2f), new Vector3(8f, 3f, H - 1.2f), new Vector3(H - 1.2f, 3f, 6f), new Vector3(-H + 1.2f, 3f, -6f) })
            {
                Box(yard, "Torch", position, new Vector3(0.12f, 0.7f, 0.12f), M("WoodDark"), false);
                Box(yard, "Flame", position + Vector3.up * 0.45f, new Vector3(0.22f, 0.3f, 0.22f), M("Fire"), false);
                AddPointLight(yard, position + Vector3.up * 0.8f, new Color(1f, 0.62f, 0.3f), 9f, 1.4f);
            }

            // --- Interactive barricade material (round objects) ---
            Vector3 Y(float x, float z, float y = 0.5f) => new Vector3(x, P + y, z);

            // Staging area behind the gate.
            for (int i = 0; i < 6; i++) Add(placements, prefabs.Crate, Y(-5f + i * 1.3f, -10.5f, 0.55f), random.Next(-8, 8));
            for (int i = 0; i < 3; i++) Add(placements, prefabs.Crate, Y(-4.4f + i * 1.3f, -10.5f, 1.6f), random.Next(-8, 8));
            for (int i = 0; i < 6; i++) Add(placements, prefabs.Plank, Y(-11f, -12.5f, 0.15f + i * 0.14f), 90f);
            for (int i = 0; i < 8; i++) Add(placements, prefabs.Sandbag, Y(3f + (i % 4) * 1.05f, -13.2f, 0.2f + (i / 4) * 0.32f), 0f);
            Add(placements, prefabs.Cart, Y(-9f, -6f, 0.05f), 30f);
            Add(placements, prefabs.Cart, Y(4f, -6.5f, 0.05f), -20f);
            for (int i = 0; i < 3; i++) Add(placements, prefabs.Hedgehog, Y(-3f + i * 2f, -13.5f, 0.6f), random.Next(0, 90));

            // Storage by the stables.
            for (int i = 0; i < 6; i++) Add(placements, prefabs.Barrel, Y(-12.8f + (i % 3) * 0.8f, -3.5f - (i / 3) * 0.8f, 0.5f), 0f);
            for (int i = 0; i < 5; i++) Add(placements, prefabs.HayBale, Y(-10.5f, 8f + i * 0.8f, 0.35f), 90f);
            for (int i = 0; i < 4; i++) Add(placements, prefabs.Log, Y(-6f, 12.5f, 0.25f + i * 0.45f), 0f);

            // Blacksmith supplies.
            for (int i = 0; i < 3; i++) Add(placements, prefabs.MetalSheet, Y(12.8f, -1.5f + i * 0.2f, 0.8f), 90f);
            for (int i = 0; i < 6; i++) Add(placements, prefabs.StoneBlock, Y(8f + (i % 3) * 1.1f, 11.5f, 0.55f + (i / 3) * 1.02f), 0f);
            for (int i = 0; i < 3; i++) Add(placements, prefabs.PowderBarrel, Y(12.8f, 11f + i * 0.8f, 0.5f), 0f);
            for (int i = 0; i < 4; i++) Add(placements, prefabs.Bench, Y(-2f + i * 1.3f, -2f, 0.1f), 90f);

            // Cover on the wall-walk.
            const float walk = 0.35f;
            foreach (var position in new[] { new Vector3(-9f, WallTop + 0.5f, H - walk), new Vector3(9f, WallTop + 0.5f, H - walk), new Vector3(H - walk, WallTop + 0.5f, -3f), new Vector3(-H + walk, WallTop + 0.5f, 3f) })
            {
                Add(placements, prefabs.Crate, position, 0f);
            }
            for (int i = 0; i < 4; i++) Add(placements, prefabs.Sandbag, new Vector3(-10f + i * 1.1f, WallTop + 0.2f, -H + walk), 0f);
            for (int i = 0; i < 4; i++) Add(placements, prefabs.Sandbag, new Vector3(7f + i * 1.1f, WallTop + 0.2f, -H + walk), 0f);
        }

        public static void Add(List<PropPlacement> placements, NetworkObject prefab, Vector3 position, float yaw)
        {
            placements.Add(new PropPlacement { prefab = prefab, position = position, rotation = Quaternion.Euler(0f, yaw, 0f) });
        }

        #endregion
    }
}
