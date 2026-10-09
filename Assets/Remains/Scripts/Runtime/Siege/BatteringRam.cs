using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Covered battering ram (Docs/GDD.md, section 7.2). Hitting the gate, a barricade or a castle wall fast enough
    /// deals damage proportional to the speed; walls lose the blocks around the ram's head.
    /// </summary>
    public class BatteringRam : PushableSiege
    {
        [Header("Impact")]
        [SerializeField] private float minImpactSpeed = 1.3f;
        [SerializeField] private float damagePerSpeed = 220f;
        [SerializeField] private float wallBlastRadius = 1.1f;
        [SerializeField] private float impactCooldown = 1.5f;
        [SerializeField] private Material dustMaterial;

        private float m_LastImpactTime = -10f;

        public override string DisplayName => "Таран";

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsSpawned || !HasAuthority) return;
            if (Time.time - m_LastImpactTime < impactCooldown) return;

            float impactSpeed = Mathf.Max(ForwardSpeed, Vector3.Dot(-collision.relativeVelocity, transform.forward));
            if (impactSpeed < minImpactSpeed) return;

            Vector3 point = collision.GetContact(0).point;
            float damage = damagePerSpeed * impactSpeed;
            bool hitSomething = false;

            var wall = collision.collider.GetComponentInParent<WallSection>();
            if (wall != null)
            {
                wall.ApplyBlast(point, damage, wallBlastRadius);
                hitSomething = true;
            }
            else
            {
                var piece = collision.collider.GetComponentInParent<BuildPiece>();
                if (piece != null && piece.gameObject != gameObject)
                {
                    piece.ApplyDamage(damage, DamageType.Impact, LastPusher);
                    hitSomething = true;
                }
            }

            if (!hitSomething) return;

            m_LastImpactTime = Time.time;
            ImpactEffectsRpc(point);

            // Recoil after the hit.
            ForwardSpeed = -0.8f;
        }

        [Rpc(SendTo.Everyone)]
        private void ImpactEffectsRpc(Vector3 point)
        {
            ShakeIfNear(point, 18f, 0.08f);
            if (dustMaterial != null) BrickDebris.Dust(point, 1.2f, dustMaterial, 5);
        }
    }
}
