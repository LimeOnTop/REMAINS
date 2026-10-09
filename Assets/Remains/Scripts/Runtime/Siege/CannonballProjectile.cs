using System.Collections.Generic;
using Blocks.Gameplay.Core;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// A flying cannonball. Every client simulates its own copy of the shot; only the gunner's copy
    /// ("authoritative") applies damage, the others are purely visual.
    /// </summary>
    public class CannonballProjectile : MonoBehaviour
    {
        private const float WallDamage = 380f;
        private const float WallRadius = 1.5f;
        private const float DirectPieceDamage = 450f;
        private const float SplashDamage = 110f;
        private const float SplashRadius = 2.6f;

        private bool m_Authoritative;
        private ulong m_Shooter;
        private bool m_Impacted;
        private Material m_DustMaterial;
        private Rigidbody m_Body;

        public static void Launch(Vector3 origin, Vector3 velocity, bool authoritative, ulong shooter, Collider[] ignore, Material ballMaterial, Material dustMaterial)
        {
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Cannonball";
            ball.layer = 2;
            ball.transform.position = origin;
            ball.transform.localScale = Vector3.one * 0.36f;
            if (ballMaterial != null) ball.GetComponent<Renderer>().sharedMaterial = ballMaterial;

            var body = ball.AddComponent<Rigidbody>();
            body.mass = 12f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.linearVelocity = velocity;

            var ballCollider = ball.GetComponent<Collider>();
            if (ignore != null)
            {
                foreach (var other in ignore)
                {
                    if (other != null) Physics.IgnoreCollision(ballCollider, other, true);
                }
            }

            var projectile = ball.AddComponent<CannonballProjectile>();
            projectile.m_Authoritative = authoritative;
            projectile.m_Shooter = shooter;
            projectile.m_DustMaterial = dustMaterial;
            projectile.m_Body = body;
            Destroy(ball, 8f);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (m_Impacted) return;
            if (collision.relativeVelocity.magnitude < 6f) return;

            m_Impacted = true;
            Vector3 point = collision.GetContact(0).point;

            if (m_DustMaterial != null) BrickDebris.Dust(point, 2f, m_DustMaterial, 8);
            var camera = Camera.main;
            if (camera != null && Vector3.Distance(camera.transform.position, point) < 20f)
            {
                CoreDirector.RequestCameraShake()
                    .WithImpulseDefinition(Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseShapes.Explosion,
                        Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseTypes.Dissipating, 0.4f)
                    .WithVelocity(0.1f)
                    .Execute();
            }

            // The ball loses most of its energy and rolls on.
            if (m_Body != null) m_Body.linearVelocity *= 0.25f;

            if (m_Authoritative) ApplyDamage(collision.collider, point);
        }

        private void ApplyDamage(Collider direct, Vector3 point)
        {
            var directWall = direct.GetComponentInParent<WallSection>();
            var directPiece = direct.GetComponentInParent<BuildPiece>();

            if (directWall != null) directWall.ApplyBlast(point, WallDamage, WallRadius);
            if (directPiece != null) directPiece.ApplyDamage(DirectPieceDamage, DamageType.Impact, m_Shooter);

            var processed = new HashSet<Object>();
            if (directWall != null) processed.Add(directWall);
            if (directPiece != null) processed.Add(directPiece);

            foreach (var hit in Physics.OverlapSphere(point, SplashRadius, ~(1 << BrickDebris.DebrisLayer), QueryTriggerInteraction.Ignore))
            {
                float distance = Vector3.Distance(point, hit.ClosestPoint(point));
                float falloff = 1f - Mathf.Clamp01(distance / SplashRadius);

                var wall = hit.GetComponentInParent<WallSection>();
                if (wall != null)
                {
                    // Corner shots also chip the neighbouring section.
                    if (processed.Add(wall)) wall.ApplyBlast(point, WallDamage * 0.5f, WallRadius * 0.8f);
                    continue;
                }

                var piece = hit.GetComponentInParent<BuildPiece>();
                if (piece != null)
                {
                    if (processed.Add(piece)) piece.ApplyDamage(SplashDamage * 1.5f * falloff, DamageType.Explosion, m_Shooter);
                    continue;
                }

                var hittable = hit.GetComponentInParent<IHittable>();
                if (hittable is Object hittableObject && processed.Add(hittableObject))
                {
                    hittable.OnHit(new HitInfo
                    {
                        amount = SplashDamage * falloff,
                        hitPoint = hit.ClosestPoint(point),
                        hitNormal = (hit.transform.position - point).normalized,
                        attackerId = m_Shooter,
                        impactForce = Vector3.one * SplashDamage * falloff,
                        damageType = DamageType.Explosion
                    });
                }
            }
        }
    }
}
