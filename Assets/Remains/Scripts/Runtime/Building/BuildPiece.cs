using System;
using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Remains
{
    public enum BuildMaterial : byte
    {
        Wood = 0,
        Stone = 1,
        Metal = 2,
        Sandbag = 3
    }

    /// <summary>
    /// Destructible structure with material-dependent resistances (Docs/GDD.md, sections 5.3 and 7.3).
    /// Health lives on the owner; hits from any client are routed there through <see cref="HitProcessor"/>.
    /// </summary>
    public class BuildPiece : HitProcessor
    {
        /// <summary>Raised locally when any piece takes damage (piece, damage). Used by the HUD hit marker.</summary>
        public static event Action<BuildPiece, float> AnyPieceDamaged;

        [SerializeField] private BuildMaterial material = BuildMaterial.Wood;
        [SerializeField] private float maxHealth = 250f;

        [Header("Destruction")]
        [SerializeField] private int debrisCount = 4;
        [SerializeField] private float debrisLifetime = 5f;
        [Tooltip("Powder barrel: explodes when destroyed.")]
        [SerializeField] private bool explodeOnDestroy;
        [SerializeField] private float explosionDamage = 120f;
        [SerializeField] private float explosionRadius = 4f;

        private readonly NetworkVariable<float> m_Health = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private bool m_Broken;

        public BuildMaterial Material => material;
        public float Health => m_Health.Value;
        public float MaxHealth => maxHealth;
        public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(m_Health.Value / maxHealth) : 0f;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (HasAuthority && m_Health.Value <= 0f)
            {
                m_Health.Value = maxHealth;
            }
            m_Health.OnValueChanged += HandleHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            m_Health.OnValueChanged -= HandleHealthChanged;
            base.OnNetworkDespawn();
        }

        private void HandleHealthChanged(float previous, float current)
        {
            if (current < previous)
            {
                AnyPieceDamaged?.Invoke(this, previous - current);
            }
        }

        /// <summary>
        /// Multiplier applied to incoming damage: weapon effectiveness against structures x material resistance.
        /// </summary>
        public static float GetDamageMultiplier(BuildMaterial material, DamageType type)
        {
            switch (type)
            {
                case DamageType.Bullet:
                    // Small arms barely scratch walls: x0.3 overall, further reduced by hard materials.
                    return 0.3f * (material == BuildMaterial.Stone ? 0.3f : material == BuildMaterial.Metal ? 0.25f : material == BuildMaterial.Sandbag ? 0.5f : 1f);
                case DamageType.Explosion:
                    return 2f * (material == BuildMaterial.Metal ? 1.2f : material == BuildMaterial.Sandbag ? 0.5f : 1f);
                case DamageType.Melee:
                    return 3f * (material == BuildMaterial.Stone ? 0.7f : material == BuildMaterial.Metal ? 0.5f : 1f);
                case DamageType.Impact:
                    return material == BuildMaterial.Metal ? 1.5f : 1f;
                default:
                    return 1f;
            }
        }

        protected override void HandleHit(HitInfo info)
        {
            ApplyDamageAuthority(info.amount, info.damageType, info.attackerId);
        }

        /// <summary>Damages the piece from any client (e.g. battering ram impacts).</summary>
        public void ApplyDamage(float amount, DamageType type, ulong attackerId)
        {
            if (!IsSpawned) return;
            if (HasAuthority)
            {
                ApplyDamageAuthority(amount, type, attackerId);
            }
            else
            {
                ApplyDamageRpc(amount, type, attackerId);
            }
        }

        [Rpc(SendTo.Authority)]
        private void ApplyDamageRpc(float amount, DamageType type, ulong attackerId)
        {
            ApplyDamageAuthority(amount, type, attackerId);
        }

        /// <summary>Restores health from any client (defenders' repair).</summary>
        public void Repair(float amount)
        {
            if (!IsSpawned || m_Broken || amount <= 0f) return;
            if (HasAuthority) RepairAuthority(amount);
            else RepairRpc(amount);
        }

        [Rpc(SendTo.Authority)]
        private void RepairRpc(float amount) => RepairAuthority(amount);

        private void RepairAuthority(float amount)
        {
            if (m_Broken) return;
            m_Health.Value = Mathf.Min(maxHealth, m_Health.Value + amount);
        }

        private void ApplyDamageAuthority(float amount, DamageType type, ulong attackerId)
        {
            if (m_Broken || amount <= 0f) return;

            var match = MatchManager.Instance;
            if (match != null && !match.StructuresVulnerable) return;

            float damage = amount * GetDamageMultiplier(material, type);
            m_Health.Value = Mathf.Max(0f, m_Health.Value - damage);

            if (m_Health.Value <= 0f)
            {
                Break(attackerId);
            }
        }

        private void Break(ulong attackerId)
        {
            m_Broken = true;
            BreakEffectsRpc(transform.position, transform.rotation);

            if (explodeOnDestroy)
            {
                Explode(attackerId);
            }

            NetworkObject.Despawn();
        }

        private void Explode(ulong attackerId)
        {
            var hits = Physics.OverlapSphere(transform.position, explosionRadius, ~0, QueryTriggerInteraction.Ignore);
            var processed = new System.Collections.Generic.HashSet<IHittable>();
            foreach (var hit in hits)
            {
                var hittable = hit.GetComponentInParent<IHittable>();
                if (hittable == null || ReferenceEquals(hittable, this) || !processed.Add(hittable)) continue;

                float distance = Vector3.Distance(transform.position, hit.ClosestPoint(transform.position));
                float falloff = 1f - Mathf.Clamp01(distance / explosionRadius);
                hittable.OnHit(new HitInfo
                {
                    amount = explosionDamage * falloff,
                    hitPoint = hit.ClosestPoint(transform.position),
                    hitNormal = (hit.transform.position - transform.position).normalized,
                    // Barrels are neutral traps: attribute to nobody so friendly-fire rules don't filter them.
                    attackerId = ulong.MaxValue,
                    impactForce = Vector3.one * explosionDamage * falloff,
                    damageType = DamageType.Explosion
                });
            }
        }

        [Rpc(SendTo.Everyone)]
        private void BreakEffectsRpc(Vector3 position, Quaternion rotation)
        {
            SpawnDebris(position, rotation);
        }

        private void SpawnDebris(Vector3 position, Quaternion rotation)
        {
            var sourceRenderer = GetComponentInChildren<MeshRenderer>();
            var sourceMaterial = sourceRenderer != null ? sourceRenderer.sharedMaterial : null;
            var bounds = sourceRenderer != null ? sourceRenderer.bounds : new Bounds(position, Vector3.one);
            float chunk = Mathf.Clamp(bounds.size.magnitude * 0.22f, 0.15f, 0.7f);

            for (int i = 0; i < debrisCount; i++)
            {
                var debris = GameObject.CreatePrimitive(i % 2 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                debris.name = "Debris";
                if (i % 2 == 1)
                {
                    // Low-poly look: squash spheres into rough rocks.
                    debris.transform.localScale = new Vector3(chunk, chunk * 0.6f, chunk * 0.8f);
                }
                else
                {
                    debris.transform.localScale = new Vector3(chunk, chunk * Random.Range(0.4f, 1f), chunk * Random.Range(0.5f, 1.2f));
                }

                Vector3 offset = new Vector3(Random.Range(-bounds.extents.x, bounds.extents.x), Random.Range(-bounds.extents.y, bounds.extents.y), Random.Range(-bounds.extents.z, bounds.extents.z));
                debris.transform.SetPositionAndRotation(bounds.center + offset, Random.rotation);
                if (sourceMaterial != null) debris.GetComponent<Renderer>().sharedMaterial = sourceMaterial;

                // Debris only collides with the world, never with players or live pieces' carriers.
                var body = debris.AddComponent<Rigidbody>();
                body.mass = 2f;
                body.linearVelocity = (offset.normalized + Vector3.up) * Random.Range(2f, 5f);
                body.angularVelocity = Random.insideUnitSphere * 6f;
                debris.layer = 2; // Ignore Raycast: not hittable, not grabbable.

                Destroy(debris, debrisLifetime + Random.Range(0f, 1f));
            }
        }
    }
}
