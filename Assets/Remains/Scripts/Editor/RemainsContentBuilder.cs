using System.Collections.Generic;
using System.IO;
using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using static Remains.Editor.BuildKit;

namespace Remains.Editor
{
    /// <summary>
    /// Generates the whole playable content from code: materials, prefabs, kits, the castle map with the
    /// attackers' camp and the planning map texture. Menu: Remains / Build All Content. Safe to re-run.
    /// </summary>
    public static class RemainsContentBuilder
    {
        private const string SourceScene = "Assets/Shooter/TestScenes/[BB] Shooter.unity";
        private const string PanelSettingsPath = "Assets/Core/Settings/PanelSettings.asset";
        private const string OldScenePath = Root + "/Scenes/Remains_Ruins.unity";

        [MenuItem("Remains/Build All Content")]
        public static void BuildAll()
        {
            EnsureFolders();
            EnsureLayers();
            CreateMaterials();
            // Placeholder until the map is baked (tables reference the material).
            MatUnlitTexture("BattleMap", null);

            var config = CreateMatchConfig();
            var prefabs = PrefabBuilder.BuildAll();
            AssetDatabase.SaveAssets();

            BuildScene(config, prefabs);
            AssetDatabase.SaveAssets();
            Debug.Log("[Remains] Content built: " + ScenePath);
        }

        private static MatchConfig CreateMatchConfig()
        {
            string path = DataDir + "/MatchConfig.asset";
            var config = AssetDatabase.LoadAssetAtPath<MatchConfig>(path);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<MatchConfig>();
                AssetDatabase.CreateAsset(config, path);
            }
            config.buildZoneRadius = 30f;
            config.defenderLeashRadius = 32f;
            EditorUtility.SetDirty(config);
            return config;
        }

        private static void BuildScene(MatchConfig config, PrefabBuilder.Prefabs prefabs)
        {
            var copied = EditorSceneManager.OpenScene(SourceScene, OpenSceneMode.Single);
            EditorSceneManager.SaveScene(copied, ScenePath, true);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "MasterEnvironment_FPS" || root.name == "ProbeVolumePerSceneData") Object.DestroyImmediate(root);
            }

            // The copied template scene carries its own occlusion and lighting data, which would cull/light our map wrongly.
            StaticOcclusionCulling.Clear();
            Lightmapping.Clear();
            Lightmapping.ClearLightingDataAsset();

            // Assets created before the scene switch are unloaded; reload what we need by path.
            config = AssetDatabase.LoadAssetAtPath<MatchConfig>(DataDir + "/MatchConfig.asset");
            prefabs = ReloadPrefabs();

            var random = new System.Random(1337);
            var placements = new List<PropPlacement>();
            var environment = new GameObject("Environment").transform;

            WorldBuilder.BuildTerrain(environment);
            CastleBuilder.Build(environment, prefabs, placements, random);
            var mapSurfaces = CampBuilder.Build(environment, prefabs, placements, random);
            WorldBuilder.BuildVillage(environment, prefabs, placements, random);
            WorldBuilder.BuildNature(environment, random);
            WorldBuilder.BuildLighting();

            var map = CreateMap(placements);
            var lobbySpawns = CreateLobbySpawns(map.transform);

            var mapTexture = EnsureBattleMapTexture();
            var mapMaterial = MatUnlitTexture("BattleMap", mapTexture);
            foreach (var surface in mapSurfaces) surface.sharedMaterial = mapMaterial;
            CreateBattleMapBoard(mapTexture, mapSurfaces, mapMaterial);

            ConfigureNetworkAndGameManager(prefabs.Player, lobbySpawns);
            CreateMatchManager(config, map);
            CreateUi();
            new GameObject("VoiceChat").AddComponent<TeamVoiceChat>();

            MarkStatic(environment);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            UpdateBuildSettings();

            // URP can't render reliably in the same editor tick as the heavy scene rebuild: bake the map next tick.
            EditorApplication.delayCall += BakeBattleMapNow;
        }

        private static PrefabBuilder.Prefabs ReloadPrefabs()
        {
            NetworkObject Load(string name) => AssetDatabase.LoadAssetAtPath<GameObject>($"{ItemsDir}/{name}.prefab").GetComponent<NetworkObject>();
            return new PrefabBuilder.Prefabs
            {
                Plank = Load("Item_Plank"), Crate = Load("Item_Crate"), Log = Load("Item_Log"), Sandbag = Load("Item_Sandbag"),
                StoneBlock = Load("Item_StoneBlock"), MetalSheet = Load("Item_MetalSheet"), Hedgehog = Load("Item_Hedgehog"),
                PowderBarrel = Load("Item_PowderBarrel"), Barrel = Load("Item_Barrel"), HayBale = Load("Item_HayBale"),
                Cart = Load("Item_Cart"), Table = Load("Item_Table"), Bench = Load("Item_Bench"), Ladder = Load("Siege_Ladder"),
                Cannonball = Load("Siege_Cannonball"), PowderCharge = Load("Siege_PowderCharge"), Gate = Load("Castle_Gate"),
                Ram = Load("Siege_BatteringRam"), Cannon = Load("Siege_Cannon"),
                Player = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsDir + "/RemainsPlayer.prefab"),
                Catalog = AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(DataDir + "/LoadoutCatalog.asset")
            };
        }

        private static RemainsMap CreateMap(List<PropPlacement> placements)
        {
            var mapObject = new GameObject("Map");
            var map = mapObject.AddComponent<RemainsMap>();

            map.fortressCenter = Group(mapObject.transform, "CastleCenter", new Vector3(0f, PlateauHeight, 0f));
            map.defenderSpawnRadius = 10f;
            map.attackerBase = Group(mapObject.transform, "Camp", OnGround(CampCenter.x, CampCenter.z));
            map.attackerBaseRadius = CampRadius;
            map.lobbyArea = Group(mapObject.transform, "Lobby", new Vector3(0f, PlateauHeight, -7f));
            map.lobbyRadius = 4.5f;
            map.groundMask = 1 << GroundLayer;
            map.placements = placements;
            return map;
        }

        private static List<Transform> CreateLobbySpawns(Transform mapRoot)
        {
            var spawns = new List<Transform>();
            var root = Group(mapRoot, "LobbySpawns", Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                float angle = i / 8f * Mathf.PI * 2f;
                var spawn = Group(root, $"LobbySpawn_{i}", new Vector3(Mathf.Cos(angle) * 4f, PlateauHeight + 0.2f, -7f + Mathf.Sin(angle) * 3f));
                spawn.rotation = Quaternion.Euler(0f, i * 45f, 0f);
                spawns.Add(spawn);
            }
            return spawns;
        }

        #region Battle map

        private static string BattleMapPath => $"{TexturesDir}/BattleMap.png";

        /// <summary>Makes sure the map texture asset exists (plain parchment until it is baked).</summary>
        private static Texture2D EnsureBattleMapTexture()
        {
            if (!File.Exists(BattleMapPath))
            {
                var blank = new Texture2D(16, 16, TextureFormat.RGB24, false);
                var pixels = new Color32[16 * 16];
                for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(222, 204, 160, 255);
                blank.SetPixels32(pixels);
                File.WriteAllBytes(BattleMapPath, blank.EncodeToPNG());
                Object.DestroyImmediate(blank);
            }
            ImportBattleMap();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BattleMapPath);
        }

        private static void ImportBattleMap()
        {
            AssetDatabase.ImportAsset(BattleMapPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(BattleMapPath);
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        /// <summary>Renders the open map from above onto the planning-table texture.</summary>
        [MenuItem("Remains/Bake Battle Map")]
        public static void BakeBattleMapNow()
        {
            BakeBattleMap();
            Debug.Log("[Remains] Battle map baked: " + BattleMapPath);
        }

        private static Texture2D BakeBattleMap()
        {
            const int size = 1024;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;

            var cameraObject = new GameObject("MapBakeCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = MapArea.height * 0.5f;
            camera.aspect = 1f;
            camera.transform.SetPositionAndRotation(new Vector3(MapArea.center.x, 150f, MapArea.center.y), Quaternion.Euler(90f, 0f, 0f));
            camera.nearClipPlane = 1f;
            camera.farClipPlane = 300f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.8f, 0.75f, 0.6f);
            camera.useOcclusionCulling = false;

            // URP: render through a render request (Camera.Render mid-frame clashes with the editor's own rendering jobs).
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = target });

            RenderTexture.active = target;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            RenderTexture.active = null;
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(target);
            RenderSettings.fog = fog;

            // Parchment look: warm tint, grid and a border.
            var pixels = texture.GetPixels32();
            var parchment = new Color(0.93f, 0.85f, 0.68f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int i = y * size + x;
                    Color c = pixels[i];
                    float grey = c.grayscale;
                    Color tinted = Color.Lerp(new Color(grey, grey, grey), c, 0.55f) * parchment * 1.1f;
                    if (x % 64 == 0 || y % 64 == 0) tinted *= 0.8f;
                    if (x < 10 || y < 10 || x > size - 11 || y > size - 11) tinted = new Color(0.35f, 0.24f, 0.14f);
                    tinted.a = 1f;
                    pixels[i] = tinted;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            File.WriteAllBytes(BattleMapPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            ImportBattleMap();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(BattleMapPath);
        }

        private static void CreateBattleMapBoard(Texture2D mapTexture, List<Renderer> surfaces, Material material)
        {
            var go = new GameObject("BattleMapBoard");
            SceneNetworkObject(go);
            go.AddComponent<BattleMapBoard>().EditorSetup(mapTexture, surfaces, material);
        }

        #endregion

        #region Scene objects

        private static void ConfigureNetworkAndGameManager(GameObject player, List<Transform> lobbySpawns)
        {
            var networkManager = Object.FindAnyObjectByType<NetworkManager>();
            networkManager.NetworkConfig.PlayerPrefab = player;
            EditorUtility.SetDirty(networkManager);

            var gameManager = Object.FindAnyObjectByType<GameManager>();
            var gm = new SerializedObject(gameManager);
            gm.FindProperty("autoRespawn").boolValue = false;
            var spawnPoints = gm.FindProperty("spawnPoints");
            spawnPoints.arraySize = lobbySpawns.Count;
            for (int i = 0; i < lobbySpawns.Count; i++) spawnPoints.GetArrayElementAtIndex(i).objectReferenceValue = lobbySpawns[i];
            gm.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateMatchManager(MatchConfig config, RemainsMap map)
        {
            var go = new GameObject("MatchManager");
            SceneNetworkObject(go);
            var match = go.AddComponent<MatchManager>();
            var so = new SerializedObject(match);
            so.FindProperty("config").objectReferenceValue = config;
            so.FindProperty("map").objectReferenceValue = map;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateUi()
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);

            var hud = new GameObject("MatchHUD");
            var hudDocument = hud.AddComponent<UIDocument>();
            hudDocument.panelSettings = panelSettings;
            hudDocument.sortingOrder = 5;
            hud.AddComponent<MatchHUD>();

            var menu = new GameObject("LoadoutMenu");
            var menuDocument = menu.AddComponent<UIDocument>();
            menuDocument.panelSettings = panelSettings;
            menuDocument.sortingOrder = 20;
            menu.AddComponent<LoadoutMenu>();
        }

        private static void MarkStatic(Transform environment)
        {
            foreach (var renderer in environment.GetComponentsInChildren<Renderer>(true))
            {
                var go = renderer.gameObject;
                // Wall blocks toggle on destruction, cloth animates, doors swing, the NPC moves: keep those dynamic.
                if (go.GetComponentInParent<WallSection>() != null || go.GetComponent<WindCloth>() != null || go.GetComponentInParent<InteractiveDoor>() != null) continue;
                if (go.GetComponentInParent<ArmorerNpc>() != null || go.GetComponentInParent<PhaseVisibility>() != null) continue;
                if (go.GetComponentInParent<CaptureObjective>() != null) continue;
                if (go.name == "MapSurface") continue;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void UpdateBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (var existing in EditorBuildSettings.scenes)
            {
                if (existing.path != ScenePath && existing.path != OldScenePath) scenes.Add(existing);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        #endregion
    }
}
