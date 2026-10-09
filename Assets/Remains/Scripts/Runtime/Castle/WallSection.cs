using System.Collections.Generic;
using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// A castle wall section made of brick blocks laid on a grid (Docs/GDD.md, section 5).
    /// Cannonballs, rams, charges and grenades knock blocks out; blocks that lose every path to the ground
    /// (or to the towers on either side) collapse, so breaches big enough to climb through appear.
    /// Destroyed blocks are replicated as a 128-bit mask owned by the session owner; defenders repair them.
    /// Block layout: index = row * columns + column, local centre = ((c + 0.5) * w, (r + 0.5) * h, 0).
    /// </summary>
    public class WallSection : HitProcessor
    {
        public const int MaxBlocks = 128;
        private static readonly List<WallSection> s_All = new List<WallSection>();
        public static IReadOnlyList<WallSection> All => s_All;

        [SerializeField] private int columns = 6;
        [SerializeField] private int rows = 10;
        [SerializeField] private Vector3 blockSize = new Vector3(1f, 0.75f, 1.6f);
        [Tooltip("Blocks in index order; null entries are empty grid cells (gaps between merlons).")]
        [SerializeField] private GameObject[] blocks;
        [SerializeField] private float blockHealth = 160f;
        [Tooltip("Blocks in the first/last column are held by the neighbouring towers.")]
        [SerializeField] private bool anchoredLeft = true;
        [SerializeField] private bool anchoredRight = true;
        [SerializeField] private Material dustMaterial;

        private readonly NetworkVariable<ulong> m_MaskLow = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ulong> m_MaskHigh = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float[] m_Health;
        private bool[] m_Destroyed;
        private bool[] m_Visible;
        private Vector3 m_LastImpact;
        private readonly Queue<int> m_Queue = new Queue<int>();
        private bool[] m_Supported;

        public int Columns => columns;
        public int Rows => rows;
        public Vector3 BlockSize => blockSize;
        public int BlockCount => blocks != null ? blocks.Length : 0;

        #region Lifecycle

        private void Awake()
        {
            int count = BlockCount;
            m_Health = new float[count];
            m_Destroyed = new bool[count];
            m_Supported = new bool[count];
            m_Visible = new bool[count];
            for (int i = 0; i < count; i++)
            {
                m_Health[i] = blockHealth;
                m_Visible[i] = blocks[i] != null;
            }
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            s_All.Add(this);
            m_MaskLow.OnValueChanged += HandleMaskChanged;
            m_MaskHigh.OnValueChanged += HandleMaskChanged;
            ApplyMask(false);
        }

        public override void OnNetworkDespawn()
        {
            s_All.Remove(this);
            m_MaskLow.OnValueChanged -= HandleMaskChanged;
            m_MaskHigh.OnValueChanged -= HandleMaskChanged;
            base.OnNetworkDespawn();
        }

        #endregion

        #region Queries

        public bool Exists(int index) => blocks != null && index >= 0 && index < blocks.Length && blocks[index] != null;

        public bool IsDestroyed(int index)
        {
            ulong mask = index < 64 ? m_MaskLow.Value : m_MaskHigh.Value;
            return (mask & (1UL << (index % 64))) != 0;
        }

        public Vector3 LocalCenter(int index)
        {
            int r = index / columns, c = index % columns;
            return new Vector3((c + 0.5f) * blockSize.x, (r + 0.5f) * blockSize.y, 0f);
        }

        public Vector3 WorldCenter(int index) => transform.TransformPoint(LocalCenter(index));

        /// <summary>
        /// Finds the destroyed block a repairer is pointing at: intersects the ray with the section's volume
        /// and picks the closest repairable hole within reach.
        /// </summary>
        public bool FindRepairTarget(Ray ray, float maxDistance, out int index, out float distance)
        {
            index = -1;
            distance = 0f;

            Vector3 origin = transform.InverseTransformPoint(ray.origin);
            Vector3 direction = transform.InverseTransformDirection(ray.direction);
            Vector3 min = new Vector3(0f, 0f, -blockSize.z * 0.5f);
            Vector3 max = new Vector3(columns * blockSize.x, rows * blockSize.y, blockSize.z * 0.5f);
            if (!IntersectBox(origin, direction, min, max, out float enter) || enter > maxDistance) return false;

            Vector3 hit = origin + direction * enter;
            float best = 1.4f;
            for (int i = 0; i < BlockCount; i++)
            {
                if (!Exists(i) || !IsDestroyed(i) || !HasSupportIfRestored(i)) continue;
                Vector3 center = LocalCenter(i);
                float d = Vector2.Distance(new Vector2(center.x, center.y), new Vector2(hit.x, hit.y));
                if (d < best)
                {
                    best = d;
                    index = i;
                }
            }

            distance = enter;
            return index >= 0;
        }

        private static bool IntersectBox(Vector3 origin, Vector3 direction, Vector3 min, Vector3 max, out float enter)
        {
            float tMin = 0f, tMax = float.MaxValue;
            for (int axis = 0; axis < 3; axis++)
            {
                if (Mathf.Abs(direction[axis]) < 1e-6f)
                {
                    if (origin[axis] < min[axis] || origin[axis] > max[axis])
                    {
                        enter = 0f;
                        return false;
                    }
                    continue;
                }
                float t1 = (min[axis] - origin[axis]) / direction[axis];
                float t2 = (max[axis] - origin[axis]) / direction[axis];
                if (t1 > t2) (t1, t2) = (t2, t1);
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
                if (tMin > tMax)
                {
                    enter = 0f;
                    return false;
                }
            }
            enter = tMin;
            return true;
        }

        private bool HasSupportIfRestored(int index)
        {
            int r = index / columns, c = index % columns;
            if (r == 0) return true;
            if ((c == 0 && anchoredLeft) || (c == columns - 1 && anchoredRight)) return true;
            return IsIntact(r - 1, c) || IsIntact(r, c - 1) || IsIntact(r, c + 1);
        }

        private bool IsIntact(int r, int c)
        {
            if (r < 0 || r >= rows || c < 0 || c >= columns) return false;
            int i = r * columns + c;
            return Exists(i) && !IsDestroyed(i);
        }

        #endregion

        #region Damage (authority)

        protected override void HandleHit(HitInfo info)
        {
            float radius, multiplier;
            switch (info.damageType)
            {
                case DamageType.Explosion: radius = 1.3f; multiplier = 1f; break;
                case DamageType.Impact: radius = 1.0f; multiplier = 1f; break;
                case DamageType.Melee: radius = 0.5f; multiplier = 0.6f; break;
                default: radius = 0.3f; multiplier = 0.05f; break;
            }
            ApplyBlastAuthority(info.hitPoint, info.amount * multiplier, radius);
        }

        /// <summary>Damages blocks around a world point from any client (cannonballs, rams, charges).</summary>
        public void ApplyBlast(Vector3 worldPoint, float damage, float radius)
        {
            if (!IsSpawned) return;
            if (HasAuthority) ApplyBlastAuthority(worldPoint, damage, radius);
            else BlastRpc(worldPoint, damage, radius);
        }

        [Rpc(SendTo.Authority)]
        private void BlastRpc(Vector3 worldPoint, float damage, float radius)
        {
            ApplyBlastAuthority(worldPoint, damage, radius);
        }

        private void ApplyBlastAuthority(Vector3 worldPoint, float damage, float radius)
        {
            var match = MatchManager.Instance;
            if (match != null && !match.StructuresVulnerable) return;
            if (damage <= 0f) return;

            SyncDestroyedFromMask();
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            Vector3 half = blockSize * 0.5f;
            bool changed = false;
            int nearest = -1;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < BlockCount; i++)
            {
                if (!Exists(i) || m_Destroyed[i]) continue;

                Vector3 center = LocalCenter(i);
                Vector3 offset = local - center;
                Vector3 closest = new Vector3(Mathf.Clamp(offset.x, -half.x, half.x), Mathf.Clamp(offset.y, -half.y, half.y), Mathf.Clamp(offset.z, -half.z, half.z));
                float distance = (offset - closest).magnitude;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = i;
                }

                if (distance > radius) continue;
                float falloff = 1f - 0.6f * (distance / Mathf.Max(0.01f, radius));
                changed |= DamageBlock(i, damage * falloff);
            }

            // Small-radius hits always land on the block that was actually struck.
            if (nearest >= 0 && nearestDistance > radius && nearestDistance < 0.6f)
            {
                changed |= DamageBlock(nearest, damage);
            }

            if (!changed) return;

            m_LastImpact = worldPoint;
            CollapseUnsupported();
            WriteMask();
            ImpactPointRpc(worldPoint);
        }

        private bool DamageBlock(int index, float damage)
        {
            m_Health[index] -= damage;
            if (m_Health[index] > 0f) return false;
            m_Destroyed[index] = true;
            return true;
        }

        [Rpc(SendTo.NotAuthority)]
        private void ImpactPointRpc(Vector3 point)
        {
            // Remembered so debris flies away from the impact on every client.
            m_LastImpact = point;
        }

        /// <summary>Blocks connected (through intact neighbours) to the ground row or an anchored edge stay up.</summary>
        private void CollapseUnsupported()
        {
            int count = BlockCount;
            for (int i = 0; i < count; i++) m_Supported[i] = false;
            m_Queue.Clear();

            for (int i = 0; i < count; i++)
            {
                if (!Exists(i) || m_Destroyed[i]) continue;
                int r = i / columns, c = i % columns;
                if (r == 0 || (c == 0 && anchoredLeft) || (c == columns - 1 && anchoredRight))
                {
                    m_Supported[i] = true;
                    m_Queue.Enqueue(i);
                }
            }

            while (m_Queue.Count > 0)
            {
                int i = m_Queue.Dequeue();
                int r = i / columns, c = i % columns;
                Visit(r + 1, c);
                Visit(r - 1, c);
                Visit(r, c + 1);
                Visit(r, c - 1);
            }

            for (int i = 0; i < count; i++)
            {
                if (Exists(i) && !m_Destroyed[i] && !m_Supported[i])
                {
                    m_Destroyed[i] = true;
                    m_Health[i] = 0f;
                }
            }
        }

        private void Visit(int r, int c)
        {
            if (r < 0 || r >= rows || c < 0 || c >= columns) return;
            int i = r * columns + c;
            if (!Exists(i) || m_Destroyed[i] || m_Supported[i]) return;
            m_Supported[i] = true;
            m_Queue.Enqueue(i);
        }

        private void SyncDestroyedFromMask()
        {
            for (int i = 0; i < BlockCount; i++)
            {
                bool destroyed = IsDestroyed(i);
                if (m_Destroyed[i] && !destroyed) m_Health[i] = blockHealth;
                m_Destroyed[i] = destroyed;
                if (destroyed) m_Health[i] = 0f;
            }
        }

        private void WriteMask()
        {
            ulong low = 0, high = 0;
            for (int i = 0; i < BlockCount && i < MaxBlocks; i++)
            {
                if (!m_Destroyed[i]) continue;
                if (i < 64) low |= 1UL << i;
                else high |= 1UL << (i - 64);
            }
            if (m_MaskLow.Value != low) m_MaskLow.Value = low;
            if (m_MaskHigh.Value != high) m_MaskHigh.Value = high;
        }

        #endregion

        #region Repair and reset

        /// <summary>Asks the authority to rebuild a destroyed block (defenders' repair).</summary>
        public void RequestRepair(int index)
        {
            if (!IsSpawned) return;
            RepairRpc(index);
        }

        [Rpc(SendTo.Authority)]
        private void RepairRpc(int index)
        {
            if (!Exists(index) || !IsDestroyed(index) || !HasSupportIfRestored(index)) return;

            // Don't rebuild a block inside a player or a barricade.
            Vector3 center = WorldCenter(index);
            Vector3 halfExtents = Vector3.Scale(blockSize * 0.45f, transform.lossyScale);
            int mask = ~((1 << BrickDebris.DebrisLayer) | (1 << 2));
            var overlaps = Physics.OverlapBox(center, halfExtents, transform.rotation, mask, QueryTriggerInteraction.Ignore);
            foreach (var overlap in overlaps)
            {
                if (overlap.transform.IsChildOf(transform)) continue;
                return;
            }

            SyncDestroyedFromMask();
            m_Destroyed[index] = false;
            m_Health[index] = blockHealth;
            WriteMask();
        }

        /// <summary>Authority: restores the whole section (new round).</summary>
        public void ResetAll()
        {
            if (!HasAuthority) return;
            for (int i = 0; i < BlockCount; i++)
            {
                m_Destroyed[i] = false;
                m_Health[i] = blockHealth;
            }
            WriteMask();
        }

        #endregion

        #region Visuals

        private void HandleMaskChanged(ulong previous, ulong current) => ApplyMask(true);

        private void ApplyMask(bool effects)
        {
            int newlyDestroyed = 0;
            for (int i = 0; i < BlockCount; i++)
            {
                if (!Exists(i)) continue;
                bool visible = !IsDestroyed(i);
                if (m_Visible[i] == visible) continue;
                m_Visible[i] = visible;

                var block = blocks[i];
                if (effects && !visible)
                {
                    var renderer = block.GetComponent<Renderer>();
                    var material = renderer != null ? renderer.sharedMaterial : null;
                    Vector3 size = Vector3.Scale(blockSize, transform.lossyScale);
                    BrickDebris.Spawn(block.transform.position, transform.rotation, size, material, newlyDestroyed < 24 ? 4 : 2, m_LastImpact);
                    if (dustMaterial != null && newlyDestroyed % 3 == 0) BrickDebris.Dust(block.transform.position, 1.2f, dustMaterial, 3);
                    newlyDestroyed++;
                }
                else if (effects && visible && dustMaterial != null)
                {
                    BrickDebris.Dust(block.transform.position, 0.6f, dustMaterial, 2);
                }
                block.SetActive(visible);
            }
        }

        #endregion

#if UNITY_EDITOR
        /// <summary>Editor: assigns the generated block objects.</summary>
        public void EditorSetup(int columnCount, int rowCount, Vector3 size, GameObject[] blockObjects, float health, bool left, bool right, Material dust)
        {
            columns = columnCount;
            rows = rowCount;
            blockSize = size;
            blocks = blockObjects;
            blockHealth = health;
            anchoredLeft = left;
            anchoredRight = right;
            dustMaterial = dust;
        }
#endif
    }
}
