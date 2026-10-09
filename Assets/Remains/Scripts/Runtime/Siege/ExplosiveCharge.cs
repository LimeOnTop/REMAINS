using System.Collections.Generic;
using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Sapper's powder charge: once planted (RMB, sapper kit only) its fuse burns for a few seconds, then it blows
    /// a large breach in walls and damages everything around.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class ExplosiveCharge : NetworkBehaviour
    {
        [SerializeField] private float fuseSeconds = 5f;
        [SerializeField] private float wallDamage = 750f;
        [SerializeField] private float wallRadius = 2.1f;
        [SerializeField] private float splashDamage = 160f;
        [SerializeField] private float splashRadius = 4f;
        [SerializeField] private GameObject fuseIndicator;
        [SerializeField] private Material dustMaterial;

        private readonly NetworkVariable<double> m_DetonateAt = new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private Grabbable m_Grabbable;
        private bool m_Exploded;

        public bool IsArmed => m_DetonateAt.Value > 0;
        private double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0;

        private void Awake()
        {
            m_Grabbable = GetComponent<Grabbable>();
        }

        public override void OnNetworkSpawn()
        {
            m_Grabbable.StateChanged += HandleStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            m_Grabbable.StateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged()
        {
            if (HasAuthority && m_Grabbable.IsFixed && m_DetonateAt.Value <= 0)
            {
                m_DetonateAt.Value = Now + fuseSeconds;
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (fuseIndicator != null)
            {
                bool blink = IsArmed && Mathf.Repeat((float)(m_DetonateAt.Value - Now), 0.5f) > 0.25f;
                if (fuseIndicator.activeSelf != blink) fuseIndicator.SetActive(blink);
            }

            if (HasAuthority && IsArmed && !m_Exploded && Now >= m_DetonateAt.Value)
            {
                Explode();
            }
        }

        private void Explode()
        {
            m_Exploded = true;
            Vector3 position = transform.position;
            ExplosionEffectsRpc(position);

            var processed = new HashSet<Object>();
            foreach (var hit in Physics.OverlapSphere(position, splashRadius, ~(1 << BrickDebris.DebrisLayer), QueryTriggerInteraction.Ignore))
            {
                float distance = Vector3.Distance(position, hit.ClosestPoint(position));
                float falloff = 1f - Mathf.Clamp01(distance / splashRadius);

                var wall = hit.GetComponentInParent<WallSection>();
                if (wall != null)
                {
                    if (processed.Add(wall)) wall.ApplyBlast(position, wallDamage, wallRadius);
                    continue;
                }

                var piece = hit.GetComponentInParent<BuildPiece>();
                if (piece != null)
                {
                    if (piece.gameObject != gameObject && processed.Add(piece)) piece.ApplyDamage(splashDamage * 3f * falloff, DamageType.Explosion, OwnerClientId);
                    continue;
                }

                var hittable = hit.GetComponentInParent<IHittable>();
                if (hittable is Object hittableObject && processed.Add(hittableObject))
                {
                    hittable.OnHit(new HitInfo
                    {
                        amount = splashDamage * falloff,
                        hitPoint = hit.ClosestPoint(position),
                        hitNormal = (hit.transform.position - position).normalized,
                        attackerId = OwnerClientId,
                        impactForce = Vector3.one * splashDamage * falloff,
                        damageType = DamageType.Explosion
                    });
                }
            }

            NetworkObject.Despawn();
        }

        [Rpc(SendTo.Everyone)]
        private void ExplosionEffectsRpc(Vector3 position)
        {
            if (dustMaterial != null) BrickDebris.Dust(position, 3f, dustMaterial, 12);
            var camera = Camera.main;
            if (camera != null && Vector3.Distance(camera.transform.position, position) < 30f)
            {
                CoreDirector.RequestCameraShake()
                    .WithImpulseDefinition(Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                        Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseTypes.Dissipating, 0.5f)
                    .WithVelocity(0.15f)
                    .Execute();
            }
        }
    }
}
