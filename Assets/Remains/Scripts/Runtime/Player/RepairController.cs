using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// Defenders' repair (Docs/GDD.md, section 5): hold F while looking at a breach to relay bricks one block
    /// at a time, or at a damaged gate/barricade to restore its health.
    /// </summary>
    public class RepairController : NetworkBehaviour, IPlayerAddon
    {
        [SerializeField] private float reach = 4f;
        [SerializeField] private float secondsPerBlock = 1.2f;
        [SerializeField] private float piecesRepairPerSecond = 60f;

        private CorePlayerManager m_Player;
        private Camera m_Camera;
        private float m_Progress;
        private WallSection m_TargetWall;
        private int m_TargetBlock = -1;
        private float m_PendingPieceRepair;

        public void Initialize(CorePlayerManager playerManager) => m_Player = playerManager;

        public void OnPlayerSpawn()
        {
            if (!IsOwner) enabled = false;
        }

        public void OnPlayerDespawn()
        {
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            m_Progress = 0f;
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || LoadoutMenu.IsOpen) return;
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Camera == null || m_Player.PlayerState == null || !m_Player.PlayerState.IsActive) return;
            if (GrabController.Local != null && GrabController.Local.Held != null) return;

            var kit = PlayerLoadout.Local != null ? PlayerLoadout.Local.Kit : null;
            var match = MatchManager.Instance;
            bool allowed = kit != null && kit.canRepair && match != null && match.Phase != MatchPhase.Results;
            if (!allowed) return;

            var keyboard = Keyboard.current;
            bool holding = keyboard != null && keyboard.fKey.isPressed;
            var ray = new Ray(m_Camera.transform.position, m_Camera.transform.forward);
            float speed = kit.repairSpeed;

            // Damaged gate or barricade first.
            if (Physics.Raycast(ray, out var hit, reach + 1.5f, ~(1 << BrickDebris.DebrisLayer), QueryTriggerInteraction.Ignore))
            {
                var piece = hit.collider.GetComponentInParent<BuildPiece>();
                if (piece != null && piece.IsSpawned && piece.GetComponent<BatteringRam>() == null && piece.Health < piece.MaxHealth - 0.5f)
                {
                    var grabbable = piece.GetComponent<Grabbable>();
                    string name = grabbable != null ? grabbable.DisplayName : "Ворота";
                    HudHint.Set($"F (держать) — чинить · {name} {Mathf.CeilToInt(piece.Health)}/{Mathf.CeilToInt(piece.MaxHealth)}", piece.HealthFraction, 3);
                    if (holding)
                    {
                        m_PendingPieceRepair += piecesRepairPerSecond * speed * Time.deltaTime;
                        if (m_PendingPieceRepair >= 15f)
                        {
                            piece.Repair(m_PendingPieceRepair);
                            m_PendingPieceRepair = 0f;
                        }
                    }
                    return;
                }
            }

            FindBreach(ray);
            if (m_TargetWall == null)
            {
                m_Progress = 0f;
                return;
            }

            HudHint.Set("F (держать) — заложить пролом кирпичом", m_Progress, 3);
            if (!holding)
            {
                m_Progress = 0f;
                return;
            }

            m_Progress += Time.deltaTime * speed / secondsPerBlock;
            if (m_Progress >= 1f)
            {
                m_Progress = 0f;
                m_TargetWall.RequestRepair(m_TargetBlock);
            }
        }

        private void FindBreach(Ray ray)
        {
            WallSection bestWall = null;
            int bestBlock = -1;
            float bestDistance = reach;

            foreach (var wall in WallSection.All)
            {
                if (Vector3.Distance(wall.transform.position, ray.origin) > 20f) continue;
                if (wall.FindRepairTarget(ray, bestDistance, out int block, out float distance) && distance <= bestDistance)
                {
                    bestDistance = distance;
                    bestWall = wall;
                    bestBlock = block;
                }
            }

            if (bestWall != m_TargetWall || bestBlock != m_TargetBlock) m_Progress = 0f;
            m_TargetWall = bestWall;
            m_TargetBlock = bestBlock;
        }
    }
}
