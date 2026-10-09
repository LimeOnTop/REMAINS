using System.Collections.Generic;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;
using Unity.Cinemachine;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;
using static Remains.Editor.BuildKit;

namespace Remains.Editor
{
    /// <summary>
    /// Network prefabs: grabbable barricade materials and props, siege engines, the castle gate,
    /// equipment kits and the first-person player variant.
    /// </summary>
    public static class PrefabBuilder
    {
        private const string SourcePlayer = "Assets/Shooter/Prefabs/[BB] ShooterPlayer.prefab";
        private const string SourceAimCamera = "Assets/Shooter/Prefabs/[BB] Aim.prefab";

        public class Prefabs
        {
            public NetworkObject Plank, Crate, Log, Sandbag, StoneBlock, MetalSheet, Hedgehog, PowderBarrel, Barrel,
                HayBale, Cart, Table, Bench, Ladder, Cannonball, PowderCharge, Gate, Ram, Cannon;
            public GameObject Player;
            public LoadoutCatalog Catalog;

            public IEnumerable<GameObject> All()
            {
                foreach (var prefab in new[] { Plank, Crate, Log, Sandbag, StoneBlock, MetalSheet, Hedgehog, PowderBarrel, Barrel, HayBale, Cart, Table, Bench, Ladder, Cannonball, PowderCharge, Gate, Ram, Cannon })
                {
                    if (prefab != null) yield return prefab.gameObject;
                }
                if (Player != null) yield return Player;
            }
        }

        public static Prefabs BuildAll()
        {
            var prefabs = new Prefabs();
            prefabs.Catalog = CreateKits();
            CreateItems(prefabs);
            prefabs.Player = CreatePlayerVariant(prefabs.Catalog);
            RegisterNetworkPrefabs(prefabs);
            return prefabs;
        }

        #region Kits

        private static LoadoutKit Kit(string file, string name, string purpose, string description, string hex, Team team, float share, params string[] weapons)
        {
            string path = $"{KitsDir}/{file}.asset";
            var kit = AssetDatabase.LoadAssetAtPath<LoadoutKit>(path);
            if (kit == null)
            {
                kit = ScriptableObject.CreateInstance<LoadoutKit>();
                AssetDatabase.CreateAsset(kit, path);
            }
            kit.kitName = name;
            kit.purpose = purpose;
            kit.description = description;
            ColorUtility.TryParseHtmlString(hex, out kit.color);
            kit.team = team;
            kit.maxShare = share;
            kit.weapons = new List<string>(weapons);
            kit.speedMultiplier = 1f;
            kit.damageTakenMultiplier = 1f;
            kit.explosiveDamageTakenMultiplier = 1f;
            kit.pushWeight = 1f;
            kit.noCarryPenalty = false;
            kit.canArmCharges = false;
            kit.cannonReloadMultiplier = 1f;
            kit.canRepair = false;
            kit.repairSpeed = 1f;
            return kit;
        }

        private static LoadoutCatalog CreateKits()
        {
            const string rifle = "Weapon_AssaultRifle", pistol = "Weapon_HandGun", shotgun = "Weapon_Shotgun", launcher = "Weapon_GrenadeLauncher";

            var assault = Kit("Kit_Assault", "Штурмовик", "Основная пехота: зачистка стен и дворов",
                "Надёжный автомат и пистолет. Без особых умений, зато без ограничений по числу. Прикрывает таран и канониров, первым лезет в пролом.",
                "#E8C170", Team.Attackers, 1f, rifle, pistol);

            var breacher = Kit("Kit_Breacher", "Таранщик", "Таран и ворота — его работа",
                "Дробовик для ближнего боя в проломе. Толстая стёганка: −25% урона. Толкает таран и пушку за двоих и носит тяжести без замедления.",
                "#D9702A", Team.Attackers, 0.3f, shotgun, pistol);
            breacher.damageTakenMultiplier = 0.75f;
            breacher.pushWeight = 2f;
            breacher.noCarryPenalty = true;
            breacher.speedMultiplier = 0.95f;

            var gunner = Kit("Kit_Gunner", "Канонир", "Обслуживает пушки и крушит стены",
                "Только пистолет — руки заняты ядрами. Перезаряжает пушку вдвое быстрее, носит ядра без замедления и помогает катить орудие.",
                "#B8412E", Team.Attackers, 0.2f, pistol);
            gunner.cannonReloadMultiplier = 0.5f;
            gunner.noCarryPenalty = true;
            gunner.pushWeight = 1.5f;

            var sapper = Kit("Kit_Sapper", "Сапёр", "Минирует стены пороховыми зарядами",
                "Единственный, кто умеет ставить пороховые заряды (ящики с зарядами — в шатрах). Взрыв выносит огромный кусок стены. Привычен к грохоту: −50% урона от взрывов.",
                "#8E6BBF", Team.Attackers, 0.15f, shotgun, pistol);
            sapper.canArmCharges = true;
            sapper.explosiveDamageTakenMultiplier = 0.5f;

            var grenadier = Kit("Kit_Grenadier", "Гренадёр", "Навесной огонь через стены",
                "Гранатомёт выбивает защитников со стен и из-за баррикад, пробивает деревянные заграждения. Тяжёлая сумка: −8% скорости.",
                "#6FA35A", Team.Attackers, 0.2f, launcher, pistol);
            grenadier.speedMultiplier = 0.92f;

            var scout = Kit("Kit_Scout", "Разведчик", "Фланги, лестницы и охота на стрелков",
                "Автомат и лёгкий доспех: +20% к скорости, но +15% урона. Ставит лестницы на дальних стенах и отвлекает гарнизон.",
                "#4FA3C7", Team.Attackers, 0.15f, rifle, pistol);
            scout.speedMultiplier = 1.2f;
            scout.damageTakenMultiplier = 1.15f;

            var garrison = Kit("Kit_Garrison", "Гарнизон", "Защитник замка",
                "Автомат, дробовик и пистолет. Чинит стены и ворота (F), баррикадирует проломы.",
                "#3B7BD9", Team.Defenders, 1f, rifle, shotgun, pistol);
            garrison.canRepair = true;

            foreach (var kit in new[] { assault, breacher, gunner, sapper, grenadier, scout, garrison }) EditorUtility.SetDirty(kit);

            string catalogPath = $"{DataDir}/LoadoutCatalog.asset";
            var catalog = AssetDatabase.LoadAssetAtPath<LoadoutCatalog>(catalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LoadoutCatalog>();
                AssetDatabase.CreateAsset(catalog, catalogPath);
            }
            catalog.attackerKits = new List<LoadoutKit> { assault, breacher, gunner, sapper, grenadier, scout };
            catalog.defenderKit = garrison;
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        #endregion

        #region Items

        private static GameObject NewNetworkRoot(string name, float mass, NetworkObject.OwnershipStatus ownership, bool rigidbody = true)
        {
            var root = new GameObject(name);
            var networkObject = root.AddComponent<NetworkObject>();
            var so = new SerializedObject(networkObject);
            so.FindProperty("Ownership").intValue = (int)ownership;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (rigidbody)
            {
                var body = root.AddComponent<Rigidbody>();
                body.mass = mass;
                body.linearDamping = 0.05f;
                body.angularDamping = 0.3f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

                var networkTransform = root.AddComponent<NetworkTransform>();
                networkTransform.SyncScaleX = false;
                networkTransform.SyncScaleY = false;
                networkTransform.SyncScaleZ = false;

                var networkRigidbody = root.AddComponent<NetworkRigidbody>();
                networkRigidbody.UseRigidBodyForMotion = true;
            }

            root.AddComponent<RoundObject>();
            return root;
        }

        private static GameObject NewItem(string name, string displayName, float mass, float health, BuildMaterial material, FixRule fixRule = FixRule.DefendersInBuildZone, bool siegeTool = false)
        {
            var root = NewNetworkRoot(name, mass, NetworkObject.OwnershipStatus.Distributable | NetworkObject.OwnershipStatus.Transferable);

            var grabbable = root.AddComponent<Grabbable>();
            var g = new SerializedObject(grabbable);
            g.FindProperty("displayName").stringValue = displayName;
            g.FindProperty("fixRule").enumValueIndex = (int)fixRule;
            g.FindProperty("isSiegeTool").boolValue = siegeTool;
            g.ApplyModifiedPropertiesWithoutUndo();

            var piece = root.AddComponent<BuildPiece>();
            var p = new SerializedObject(piece);
            p.FindProperty("material").enumValueIndex = (int)material;
            p.FindProperty("maxHealth").floatValue = health;
            p.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static NetworkObject Save(GameObject root)
        {
            string path = $"{ItemsDir}/{root.name}.prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab.GetComponent<NetworkObject>();
        }

        private static void CreateItems(Prefabs p)
        {
            var hexLog = SaveMesh(LowPolyMeshes.Prism("LogHex", 6, 0.22f, 4f));
            var barrelMesh = SaveMesh(LowPolyMeshes.Prism("BarrelOct", 8, 0.35f, 0.9f));
            var wheelMesh = SaveMesh(LowPolyMeshes.Prism("Wheel", 8, 0.45f, 0.18f));
            var smallWheel = SaveMesh(LowPolyMeshes.Prism("WheelSmall", 8, 0.4f, 0.14f));
            var ramLog = SaveMesh(LowPolyMeshes.Prism("RamLog", 6, 0.3f, 4.2f));
            var barrelTube = SaveMesh(LowPolyMeshes.Prism("CannonTube", 8, 0.26f, 2.4f));
            var ballMesh = SaveMesh(LowPolyMeshes.Rock("BallRock", 7));

            // Plank: light and quick, good for patching holes.
            var plank = NewItem("Item_Plank", "Доска", 10f, 150f, BuildMaterial.Wood);
            Box(plank.transform, "Visual", Vector3.zero, new Vector3(3f, 0.12f, 0.45f), M("Wood"));
            p.Plank = Save(plank);

            var crate = NewItem("Item_Crate", "Деревянный ящик", 20f, 250f, BuildMaterial.Wood);
            Box(crate.transform, "Visual", Vector3.zero, Vector3.one, M("Wood"));
            foreach (var y in new[] { -0.45f, 0.45f })
            {
                Box(crate.transform, "Frame", new Vector3(0, y, 0.51f), new Vector3(1.02f, 0.1f, 0.02f), M("WoodDark"), false);
                Box(crate.transform, "Frame", new Vector3(0, y, -0.51f), new Vector3(1.02f, 0.1f, 0.02f), M("WoodDark"), false);
                Box(crate.transform, "Frame", new Vector3(0.51f, y, 0), new Vector3(0.02f, 0.1f, 1.02f), M("WoodDark"), false);
                Box(crate.transform, "Frame", new Vector3(-0.51f, y, 0), new Vector3(0.02f, 0.1f, 1.02f), M("WoodDark"), false);
            }
            p.Crate = Save(crate);

            var log = NewItem("Item_Log", "Бревно", 40f, 400f, BuildMaterial.Wood);
            var logVisual = MeshObject(log.transform, "Visual", hexLog, M("WoodDark"), Vector3.zero, Quaternion.Euler(0, 0, 90), Vector3.one);
            var logCollider = logVisual.AddComponent<CapsuleCollider>();
            logCollider.direction = 1;
            logCollider.radius = 0.22f;
            logCollider.height = 4f;
            p.Log = Save(log);

            var sandbag = NewItem("Item_Sandbag", "Мешок с песком", 25f, 300f, BuildMaterial.Sandbag);
            Box(sandbag.transform, "Visual", Vector3.zero, new Vector3(1f, 0.3f, 0.5f), M("Sand"));
            Box(sandbag.transform, "Tie", new Vector3(0.44f, 0f, 0f), new Vector3(0.05f, 0.32f, 0.52f), M("Rope"), false);
            p.Sandbag = Save(sandbag);

            var stone = NewItem("Item_StoneBlock", "Каменный блок", 60f, 700f, BuildMaterial.Stone);
            Box(stone.transform, "Visual", Vector3.zero, new Vector3(1f, 1f, 0.5f), M("Stone"));
            p.StoneBlock = Save(stone);

            var sheet = NewItem("Item_MetalSheet", "Железный щит", 30f, 500f, BuildMaterial.Metal);
            Box(sheet.transform, "Visual", Vector3.zero, new Vector3(2f, 1.5f, 0.06f), M("Metal"));
            Box(sheet.transform, "Rivets", new Vector3(0f, 0.65f, 0.035f), new Vector3(1.9f, 0.06f, 0.01f), M("MetalDark"), false);
            Box(sheet.transform, "Rivets", new Vector3(0f, -0.65f, 0.035f), new Vector3(1.9f, 0.06f, 0.01f), M("MetalDark"), false);
            p.MetalSheet = Save(sheet);

            var hedgehog = NewItem("Item_Hedgehog", "Рогатка", 35f, 300f, BuildMaterial.Wood);
            Box(hedgehog.transform, "Beam", Vector3.zero, new Vector3(1.6f, 0.14f, 0.14f), M("WoodDark"), true, Quaternion.Euler(0, 0, 45));
            Box(hedgehog.transform, "Beam", Vector3.zero, new Vector3(1.6f, 0.14f, 0.14f), M("WoodDark"), true, Quaternion.Euler(0, 90, 45));
            Box(hedgehog.transform, "Beam", Vector3.zero, new Vector3(1.6f, 0.14f, 0.14f), M("WoodDark"), true, Quaternion.Euler(90, 0, 0));
            p.Hedgehog = Save(hedgehog);

            var powder = NewItem("Item_PowderBarrel", "Бочка с порохом", 30f, 50f, BuildMaterial.Wood);
            var powderPiece = new SerializedObject(powder.GetComponent<BuildPiece>());
            powderPiece.FindProperty("explodeOnDestroy").boolValue = true;
            powderPiece.ApplyModifiedPropertiesWithoutUndo();
            BarrelVisual(powder.transform, barrelMesh, M("BarrelRed"));
            p.PowderBarrel = Save(powder);

            var barrel = NewItem("Item_Barrel", "Бочка", 25f, 200f, BuildMaterial.Wood);
            BarrelVisual(barrel.transform, barrelMesh, M("WoodLight"));
            p.Barrel = Save(barrel);

            var hay = NewItem("Item_HayBale", "Тюк сена", 20f, 120f, BuildMaterial.Sandbag);
            Box(hay.transform, "Visual", Vector3.zero, new Vector3(1.2f, 0.6f, 0.7f), M("Hay"));
            Box(hay.transform, "Tie", new Vector3(0.3f, 0f, 0f), new Vector3(0.04f, 0.62f, 0.72f), M("Rope"), false);
            Box(hay.transform, "Tie", new Vector3(-0.3f, 0f, 0f), new Vector3(0.04f, 0.62f, 0.72f), M("Rope"), false);
            p.HayBale = Save(hay);

            var cart = NewItem("Item_Cart", "Телега", 90f, 600f, BuildMaterial.Wood);
            Box(cart.transform, "Bed", new Vector3(0, 0.75f, 0), new Vector3(1.6f, 0.12f, 2.6f), M("Wood"));
            Box(cart.transform, "SideL", new Vector3(-0.78f, 1.05f, 0), new Vector3(0.08f, 0.5f, 2.6f), M("WoodDark"));
            Box(cart.transform, "SideR", new Vector3(0.78f, 1.05f, 0), new Vector3(0.08f, 0.5f, 2.6f), M("WoodDark"));
            Box(cart.transform, "Back", new Vector3(0, 1.05f, -1.28f), new Vector3(1.6f, 0.5f, 0.08f), M("WoodDark"));
            Box(cart.transform, "Shaft", new Vector3(0, 0.6f, 2.0f), new Vector3(0.1f, 0.1f, 1.8f), M("WoodDark"), false);
            foreach (var x in new[] { -0.9f, 0.9f })
            {
                MeshObject(cart.transform, "Wheel", wheelMesh, M("WoodDark"), new Vector3(x, 0.45f, 0f), Quaternion.Euler(0, 0, 90), Vector3.one);
            }
            var cartCollider = cart.AddComponent<BoxCollider>();
            cartCollider.center = new Vector3(0, 0.45f, 0);
            cartCollider.size = new Vector3(1.9f, 0.9f, 2.4f);
            p.Cart = Save(cart);

            var table = NewItem("Item_Table", "Стол", 35f, 220f, BuildMaterial.Wood);
            Box(table.transform, "Top", new Vector3(0, 0.8f, 0), new Vector3(2f, 0.1f, 1f), M("WoodLight"));
            foreach (var x in new[] { -0.9f, 0.9f })
                foreach (var z in new[] { -0.4f, 0.4f })
                    Box(table.transform, "Leg", new Vector3(x, 0.38f, z), new Vector3(0.1f, 0.76f, 0.1f), M("WoodDark"));
            p.Table = Save(table);

            var bench = NewItem("Item_Bench", "Скамья", 18f, 150f, BuildMaterial.Wood);
            Box(bench.transform, "Seat", new Vector3(0, 0.45f, 0), new Vector3(1.8f, 0.08f, 0.35f), M("WoodLight"));
            Box(bench.transform, "Leg", new Vector3(-0.75f, 0.22f, 0), new Vector3(0.08f, 0.44f, 0.3f), M("WoodDark"));
            Box(bench.transform, "Leg", new Vector3(0.75f, 0.22f, 0), new Vector3(0.08f, 0.44f, 0.3f), M("WoodDark"));
            p.Bench = Save(bench);

            // Assault ladder (siege tool): pivot at its foot, length along +Y.
            var ladder = NewItem("Siege_Ladder", "Штурмовая лестница", 15f, 200f, BuildMaterial.Wood, FixRule.Anyone, true);
            Box(ladder.transform, "RailL", new Vector3(-0.3f, 3f, 0), new Vector3(0.08f, 6f, 0.08f), M("Wood"), false);
            Box(ladder.transform, "RailR", new Vector3(0.3f, 3f, 0), new Vector3(0.08f, 6f, 0.08f), M("Wood"), false);
            for (int i = 0; i < 15; i++) Box(ladder.transform, "Rung", new Vector3(0, 0.3f + i * 0.4f, 0), new Vector3(0.6f, 0.06f, 0.06f), M("WoodDark"), false);
            var ladderCollider = ladder.AddComponent<BoxCollider>();
            ladderCollider.center = new Vector3(0, 3f, 0);
            ladderCollider.size = new Vector3(0.7f, 6f, 0.1f);
            var ladderComponent = ladder.AddComponent<Ladder>();
            var l = new SerializedObject(ladderComponent);
            l.FindProperty("climbVolumeSize").vector3Value = new Vector3(1.2f, 6.4f, 1.6f);
            l.FindProperty("climbVolumeCenter").vector3Value = new Vector3(0f, 3f, 0f);
            l.ApplyModifiedPropertiesWithoutUndo();
            p.Ladder = Save(ladder);

            // Cannonball: carried to a cannon's muzzle to load it.
            var ball = NewItem("Siege_Cannonball", "Ядро", 12f, 5000f, BuildMaterial.Stone, FixRule.Never, true);
            var ballVisual = MeshObject(ball.transform, "Visual", ballMesh, M("Iron"), Vector3.zero, Quaternion.identity, Vector3.one * 0.5f);
            ballVisual.AddComponent<SphereCollider>().radius = 0.45f;
            ball.AddComponent<CannonballAmmo>();
            p.Cannonball = Save(ball);

            // Powder charge: sappers plant it on walls.
            var charge = NewItem("Siege_PowderCharge", "Пороховой заряд", 8f, 60f, BuildMaterial.Wood, FixRule.Sappers, true);
            Box(charge.transform, "Keg", Vector3.zero, new Vector3(0.5f, 0.45f, 0.35f), M("BarrelRed"));
            Box(charge.transform, "Strap", new Vector3(0, 0, 0), new Vector3(0.52f, 0.08f, 0.37f), M("Band"), false);
            var fuse = Box(charge.transform, "Fuse", new Vector3(0, 0.3f, 0), new Vector3(0.08f, 0.12f, 0.08f), M("Spark"), false);
            var explosive = charge.AddComponent<ExplosiveCharge>();
            var e = new SerializedObject(explosive);
            e.FindProperty("fuseIndicator").objectReferenceValue = fuse;
            e.FindProperty("dustMaterial").objectReferenceValue = M("Dust");
            e.ApplyModifiedPropertiesWithoutUndo();
            p.PowderCharge = Save(charge);

            p.Gate = CreateGate();
            p.Ram = CreateRam(wheelMesh, ramLog);
            p.Cannon = CreateCannon(smallWheel, barrelTube);
        }

        private static void BarrelVisual(Transform parent, Mesh barrelMesh, Material material)
        {
            var visual = MeshObject(parent, "Visual", barrelMesh, material, Vector3.zero, Quaternion.identity, Vector3.one);
            var collider = visual.AddComponent<CapsuleCollider>();
            collider.radius = 0.35f;
            collider.height = 0.9f;
            MeshObject(parent, "Band", barrelMesh, M("Band"), new Vector3(0, 0.3f, 0), Quaternion.identity, new Vector3(1.04f, 0.08f, 1.04f));
            MeshObject(parent, "Band", barrelMesh, M("Band"), new Vector3(0, -0.3f, 0), Quaternion.identity, new Vector3(1.04f, 0.08f, 1.04f));
        }

        private static NetworkObject CreateGate()
        {
            // Static, destructible double gate; respawned intact every round.
            var root = NewNetworkRoot("Castle_Gate", 0f, NetworkObject.OwnershipStatus.SessionOwner, false);
            foreach (var x in new[] { -1.5f, 1.5f })
            {
                Box(root.transform, "Leaf", new Vector3(x, 2.3f, 0f), new Vector3(2.95f, 4.6f, 0.35f), M("WoodDark"));
                for (int i = 0; i < 5; i++)
                {
                    Box(root.transform, "Plank", new Vector3(x - 1.2f + i * 0.6f, 2.3f, -0.19f), new Vector3(0.5f, 4.5f, 0.04f), M("Wood"), false);
                }
                foreach (var y in new[] { 0.9f, 3.6f })
                {
                    Box(root.transform, "Iron", new Vector3(x, y, -0.22f), new Vector3(2.9f, 0.18f, 0.05f), M("Iron"), false);
                }
            }
            var piece = root.AddComponent<BuildPiece>();
            var p = new SerializedObject(piece);
            p.FindProperty("material").enumValueIndex = (int)BuildMaterial.Wood;
            p.FindProperty("maxHealth").floatValue = 2600f;
            p.FindProperty("debrisCount").intValue = 14;
            p.ApplyModifiedPropertiesWithoutUndo();
            return Save(root);
        }

        private static NetworkObject CreateRam(Mesh wheelMesh, Mesh ramLog)
        {
            var root = NewNetworkRoot("Siege_BatteringRam", 800f, NetworkObject.OwnershipStatus.Distributable);
            var body = root.GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0f, 0.3f, 0f);
            body.angularDamping = 2f;

            var slippery = SlipperyMaterial();
            var chassis = Box(root.transform, "Chassis", new Vector3(0, 0.55f, 0), new Vector3(1.6f, 0.3f, 3.6f), M("WoodDark"));
            chassis.GetComponent<BoxCollider>().sharedMaterial = slippery;
            foreach (var x in new[] { -0.9f, 0.9f })
                foreach (var z in new[] { -1.2f, 1.2f })
                    MeshObject(root.transform, "Wheel", wheelMesh, M("WoodDark"), new Vector3(x, 0.45f, z), Quaternion.Euler(0, 0, 90), Vector3.one);
            AddSkid(root.transform, new Vector3(1.9f, 0.4f, 3.2f), slippery);

            foreach (var z in new[] { -1.3f, 1.1f })
            {
                Box(root.transform, "Post", new Vector3(-0.7f, 1.35f, z), new Vector3(0.14f, 1.3f, 0.14f), M("Wood"), false, Quaternion.Euler(0, 0, -12));
                Box(root.transform, "Post", new Vector3(0.7f, 1.35f, z), new Vector3(0.14f, 1.3f, 0.14f), M("Wood"), false, Quaternion.Euler(0, 0, 12));
                Box(root.transform, "Beam", new Vector3(0f, 1.95f, z), new Vector3(1.3f, 0.14f, 0.14f), M("Wood"), false);
            }
            var logVisual = MeshObject(root.transform, "RamLog", ramLog, M("WoodDark"), new Vector3(0, 1.3f, 0.6f), Quaternion.Euler(90, 0, 0), Vector3.one);
            var logCollider = logVisual.AddComponent<CapsuleCollider>();
            logCollider.direction = 1;
            logCollider.radius = 0.3f;
            logCollider.height = 4.2f;
            Box(root.transform, "Head", new Vector3(0, 1.3f, 2.75f), new Vector3(0.75f, 0.75f, 0.4f), M("MetalDark"));
            // Hide roof keeps pushers safe from arrows... and bricks.
            Box(root.transform, "Roof", new Vector3(0, 2.15f, -0.1f), new Vector3(1.9f, 0.08f, 3f), M("Wood"), false);
            BuildKit.Cloth(root.transform, "HideL", new Vector3(-0.95f, 2.1f, -1.6f), Quaternion.Euler(0, 90, 0), 3f, 0.7f, M("ClothCream"), WindCloth.PinnedEdge.Top, 0.05f, 6, 2);
            BuildKit.Cloth(root.transform, "HideR", new Vector3(0.95f, 2.1f, 1.4f), Quaternion.Euler(0, -90, 0), 3f, 0.7f, M("ClothCream"), WindCloth.PinnedEdge.Top, 0.05f, 6, 2);

            var piece = root.AddComponent<BuildPiece>();
            var p = new SerializedObject(piece);
            p.FindProperty("material").enumValueIndex = (int)BuildMaterial.Wood;
            p.FindProperty("maxHealth").floatValue = 1500f;
            p.FindProperty("debrisCount").intValue = 8;
            p.ApplyModifiedPropertiesWithoutUndo();

            var ram = root.AddComponent<BatteringRam>();
            var r = new SerializedObject(ram);
            r.FindProperty("dustMaterial").objectReferenceValue = M("Dust");
            r.ApplyModifiedPropertiesWithoutUndo();
            return Save(root);
        }

        private static NetworkObject CreateCannon(Mesh wheelMesh, Mesh tube)
        {
            var root = NewNetworkRoot("Siege_Cannon", 500f, NetworkObject.OwnershipStatus.Distributable | NetworkObject.OwnershipStatus.Transferable);
            var body = root.GetComponent<Rigidbody>();
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.centerOfMass = new Vector3(0f, 0.3f, 0f);
            body.angularDamping = 2f;

            var slippery = SlipperyMaterial();
            Box(root.transform, "Trail", new Vector3(0, 0.45f, -1.1f), new Vector3(0.5f, 0.2f, 2.2f), M("WoodDark"), false, Quaternion.Euler(-8, 0, 0));
            Box(root.transform, "CheekL", new Vector3(-0.4f, 0.75f, 0.1f), new Vector3(0.12f, 0.6f, 1.4f), M("WoodDark"), false);
            Box(root.transform, "CheekR", new Vector3(0.4f, 0.75f, 0.1f), new Vector3(0.12f, 0.6f, 1.4f), M("WoodDark"), false);
            Box(root.transform, "Axle", new Vector3(0, 0.45f, 0.2f), new Vector3(1.5f, 0.12f, 0.12f), M("Iron"), false);
            foreach (var x in new[] { -0.75f, 0.75f })
                MeshObject(root.transform, "Wheel", wheelMesh, M("WoodDark"), new Vector3(x, 0.45f, 0.2f), Quaternion.Euler(0, 0, 90), Vector3.one);
            AddSkid(root.transform, new Vector3(1.4f, 0.45f, 2.6f), slippery, new Vector3(0, 0.22f, -0.5f));

            var yaw = Group(root.transform, "YawPivot", new Vector3(0, 1.0f, 0.2f));
            var pitch = Group(yaw, "PitchPivot", Vector3.zero);
            MeshObject(pitch, "Barrel", tube, M("Iron"), new Vector3(0, 0, 0.6f), Quaternion.Euler(90, 0, 0), Vector3.one);
            MeshObject(pitch, "Muzzle", tube, M("Iron"), new Vector3(0, 0, 1.75f), Quaternion.Euler(90, 0, 0), new Vector3(1.25f, 0.06f, 1.25f));
            MeshObject(pitch, "Breech", tube, M("Iron"), new Vector3(0, 0, -0.55f), Quaternion.Euler(90, 0, 0), new Vector3(1.3f, 0.08f, 1.3f));
            var muzzle = Group(pitch, "MuzzlePoint", new Vector3(0, 0, 2.05f));
            var load = Group(root.transform, "LoadPoint", new Vector3(0, 1.1f, 2.1f));

            var cannon = root.AddComponent<SiegeCannon>();
            var c = new SerializedObject(cannon);
            c.FindProperty("yawPivot").objectReferenceValue = yaw;
            c.FindProperty("pitchPivot").objectReferenceValue = pitch;
            c.FindProperty("muzzle").objectReferenceValue = muzzle;
            c.FindProperty("loadPoint").objectReferenceValue = load;
            c.FindProperty("dustMaterial").objectReferenceValue = M("Dust");
            c.FindProperty("ballMaterial").objectReferenceValue = M("Iron");
            c.FindProperty("speedPerPusher").floatValue = 0.9f;
            c.FindProperty("maxSpeed").floatValue = 2.4f;
            c.ApplyModifiedPropertiesWithoutUndo();

            var piece = root.AddComponent<BuildPiece>();
            var p = new SerializedObject(piece);
            p.FindProperty("material").enumValueIndex = (int)BuildMaterial.Metal;
            p.FindProperty("maxHealth").floatValue = 900f;
            p.FindProperty("debrisCount").intValue = 6;
            p.ApplyModifiedPropertiesWithoutUndo();
            return Save(root);
        }

        private static PhysicsMaterial s_Slippery;

        private static PhysicsMaterial SlipperyMaterial()
        {
            if (s_Slippery != null) return s_Slippery;
            string path = $"{MaterialsDir}/SiegeChassis.asset";
            s_Slippery = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (s_Slippery == null)
            {
                s_Slippery = new PhysicsMaterial("SiegeChassis") { dynamicFriction = 0.05f, staticFriction = 0.05f, frictionCombine = PhysicsMaterialCombine.Minimum };
                AssetDatabase.CreateAsset(s_Slippery, path);
            }
            return s_Slippery;
        }

        private static void AddSkid(Transform parent, Vector3 size, PhysicsMaterial material, Vector3? center = null)
        {
            var skid = new GameObject("Skid");
            skid.transform.SetParent(parent, false);
            var collider = skid.AddComponent<BoxCollider>();
            collider.center = center ?? new Vector3(0, size.y * 0.5f, 0);
            collider.size = size;
            collider.sharedMaterial = material;
        }

        #endregion

        #region Player

        private static GameObject CreatePlayerVariant(LoadoutCatalog catalog)
        {
            var fp = CreateCameraMode("FP_Look", "FP", 72f);
            var fpAim = CreateCameraMode("FP_Aim", "FPAim", 72f);
            var fpZoom = CreateCameraMode("FP_Zoom", "FPZoom", 42f);

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePlayer);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "RemainsPlayer";

            foreach (var type in new[] { typeof(TeamMember), typeof(PlayerLoadout), typeof(GrabController), typeof(RepairController), typeof(FirstPersonBody) })
            {
                if (!instance.GetComponent(type)) instance.AddComponent(type);
            }

            var loadout = new SerializedObject(instance.GetComponent<PlayerLoadout>());
            loadout.FindProperty("catalog").objectReferenceValue = catalog;
            loadout.ApplyModifiedPropertiesWithoutUndo();

            var camera = new SerializedObject(instance.GetComponent<CoreCameraController>());
            var modes = camera.FindProperty("cameraModePrefabs");
            modes.arraySize = 3;
            modes.GetArrayElementAtIndex(0).objectReferenceValue = fp;
            modes.GetArrayElementAtIndex(1).objectReferenceValue = fpAim;
            modes.GetArrayElementAtIndex(2).objectReferenceValue = fpZoom;
            camera.FindProperty("initialModeName").stringValue = "FP";
            camera.ApplyModifiedPropertiesWithoutUndo();

            var shooter = new SerializedObject(instance.GetComponent<ShooterAddon>());
            shooter.FindProperty("thirdPersonFreeLookCameraName").stringValue = "FP";
            shooter.FindProperty("thirdPersonAimCameraName").stringValue = "FPAim";
            shooter.FindProperty("zoomCameraName").stringValue = "FPZoom";
            shooter.FindProperty("alwaysAim").boolValue = true;
            shooter.FindProperty("freeLookSensitivity").floatValue = 1f;
            shooter.FindProperty("zoomSensitivity").floatValue = 0.45f;
            shooter.ApplyModifiedPropertiesWithoutUndo();

            var movement = new SerializedObject(instance.GetComponent<CoreMovement>());
            movement.FindProperty("groundLayers").intValue = (1 << 0) | (1 << GroundLayer);
            movement.FindProperty("moveSpeed").floatValue = 4.5f;
            movement.FindProperty("sprintSpeed").floatValue = 7f;
            movement.ApplyModifiedPropertiesWithoutUndo();

            var prefab = PrefabUtility.SaveAsPrefabAsset(instance, PrefabsDir + "/RemainsPlayer.prefab");
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static CoreCameraMode CreateCameraMode(string assetName, string modeName, float fieldOfView)
        {
            string path = $"{CameraDir}/{assetName}.prefab";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CopyAsset(SourceAimCamera, path);

            var contents = PrefabUtility.LoadPrefabContents(path);
            contents.name = assetName;

            var mode = new SerializedObject(contents.GetComponent<CoreCameraMode>());
            mode.FindProperty("modeName").stringValue = modeName;
            mode.FindProperty("activePriority").intValue = 10;
            mode.FindProperty("playerRotationMode").enumValueIndex = (int)CoreMovement.CouplingMode.Coupled;
            mode.ApplyModifiedPropertiesWithoutUndo();

            var follow = contents.GetComponent<CinemachineThirdPersonFollow>();
            follow.ShoulderOffset = new Vector3(0f, 0.24f, 0.12f);
            follow.VerticalArmLength = 0f;
            follow.CameraDistance = 0f;
            follow.CameraSide = 0.5f;
            follow.Damping = Vector3.zero;
            follow.AvoidObstacles.Enabled = false;

            var cinemachineCamera = contents.GetComponent<CinemachineCamera>();
            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = fieldOfView;
            lens.NearClipPlane = 0.05f;
            cinemachineCamera.Lens = lens;

            PrefabUtility.SaveAsPrefabAsset(contents, path);
            PrefabUtility.UnloadPrefabContents(contents);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<CoreCameraMode>();
        }

        private static void RegisterNetworkPrefabs(Prefabs prefabs)
        {
            var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
            foreach (var prefab in prefabs.All())
            {
                if (!list.Contains(prefab)) list.Add(new NetworkPrefab { Prefab = prefab });
            }
            EditorUtility.SetDirty(list);
        }

        #endregion
    }
}
