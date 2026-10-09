using System.Collections.Generic;
using UnityEngine;

namespace Remains
{
    [System.Serializable]
    public struct PropPlacement
    {
        public Unity.Netcode.NetworkObject prefab;
        public Vector3 position;
        public Quaternion rotation;
    }

    /// <summary>
    /// Scene description of the battlefield: fortress centre, attacker base, lobby area and spawn points.
    /// </summary>
    public class RemainsMap : MonoBehaviour
    {
        public static RemainsMap Instance { get; private set; }

        [Header("Fortress")]
        [Tooltip("Centre of the build zone and the defenders' banner.")]
        public Transform fortressCenter;
        [Tooltip("Radius around the fortress centre where defenders spawn.")]
        public float defenderSpawnRadius = 10f;

        [Header("Attackers")]
        public Transform attackerBase;
        [Tooltip("Attackers spawn inside this radius and cannot leave it during the build phase.")]
        public float attackerBaseRadius = 14f;

        [Header("Lobby")]
        public Transform lobbyArea;
        public float lobbyRadius = 8f;

        [Header("Round objects")]
        [Tooltip("Every interactive object placed on the map; respawned fresh at the start of each round.")]
        public List<PropPlacement> placements = new List<PropPlacement>();

        [Header("Ground")]
        public LayerMask groundMask = ~0;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public Vector3 FortressPosition => fortressCenter != null ? fortressCenter.position : Vector3.zero;

        public Vector3 GetSpawnPoint(Team team, MatchPhase phase)
        {
            if (phase == MatchPhase.Lobby && lobbyArea != null)
            {
                return RandomPointOnGround(lobbyArea.position, lobbyRadius);
            }

            switch (team)
            {
                case Team.Defenders:
                    return RandomPointOnGround(FortressPosition, defenderSpawnRadius);
                case Team.Attackers:
                    return RandomPointOnGround(attackerBase != null ? attackerBase.position : Vector3.zero, attackerBaseRadius * 0.7f);
                default:
                    return RandomPointOnGround(lobbyArea != null ? lobbyArea.position : Vector3.zero, lobbyRadius);
            }
        }

        public Quaternion GetSpawnRotation(Vector3 position)
        {
            Vector3 toCenter = FortressPosition - position;
            toCenter.y = 0f;
            return toCenter.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toCenter) : Quaternion.identity;
        }

        public bool IsInBuildZone(Vector3 position, float radius)
        {
            Vector3 delta = position - FortressPosition;
            delta.y = 0f;
            return delta.magnitude <= radius;
        }

        private Vector3 RandomPointOnGround(Vector3 center, float radius)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                Vector3 probe = center + new Vector3(offset.x, 60f, offset.y);
                if (Physics.Raycast(probe, Vector3.down, out var hit, 120f, groundMask, QueryTriggerInteraction.Ignore))
                {
                    // Avoid spawning inside obstacles (posts, rocks, walls, materials, rams).
                    Vector3 feet = hit.point + Vector3.up * 0.1f;
                    bool blocked = Physics.CheckCapsule(feet + Vector3.up * 0.5f, feet + Vector3.up * 1.6f, 0.5f, ~groundMask, QueryTriggerInteraction.Ignore);
                    if (!blocked)
                    {
                        return feet;
                    }
                }
            }
            return center + Vector3.up * 1f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (fortressCenter != null)
            {
                Gizmos.color = TeamInfo.DefenderColor;
                Gizmos.DrawWireSphere(fortressCenter.position, defenderSpawnRadius);
            }
            if (attackerBase != null)
            {
                Gizmos.color = TeamInfo.AttackerColor;
                Gizmos.DrawWireSphere(attackerBase.position, attackerBaseRadius);
            }
        }
#endif
    }
}
