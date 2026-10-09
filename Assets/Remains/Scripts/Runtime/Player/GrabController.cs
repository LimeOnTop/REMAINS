using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// Player addon for carrying objects by hand (Docs/GDD.md, section 5):
    /// E — pick up / put down, LMB — throw, RMB (hold) — nail in place / barricade, R/Q — rotate, wheel — distance.
    /// Runs only for the owning client; the carried object's physics runs on this client after it takes ownership.
    /// </summary>
    public class GrabController : NetworkBehaviour, IPlayerAddon
    {
        public static GrabController Local { get; private set; }

        [SerializeField] private float grabRange = 3.5f;
        [SerializeField] private float defaultHoldDistance = 2.2f;
        [SerializeField] private float minHoldDistance = 1.3f;
        [SerializeField] private float maxHoldDistance = 4f;
        [SerializeField] private float throwSpeed = 11f;
        [SerializeField] private float unfixHoldTime = 1f;
        [SerializeField] private float ownershipTimeout = 0.75f;

        private CorePlayerManager m_Player;
        private CharacterController m_CharacterController;
        private Camera m_Camera;
        private readonly RaycastHit[] m_Hits = new RaycastHit[16];

        private Grabbable m_Focused;
        private Grabbable m_Held;
        private Grabbable m_Pending;
        private float m_PendingTimer;
        private Quaternion m_LocalRotation;
        private float m_HoldDistance;
        private float m_FixProgress;
        private float m_UnfixProgress;
        private float m_BaseMoveSpeed;
        private float m_BaseSprintSpeed;

        public Grabbable Focused => m_Focused;
        public Grabbable Held => m_Held;
        /// <summary>0..1 progress of nailing the held object (RMB).</summary>
        public float FixProgress => m_FixProgress;
        /// <summary>0..1 progress of detaching the focused nailed object (hold E).</summary>
        public float UnfixProgress => m_UnfixProgress;
        public bool CanFixHeld => m_Held != null && CanFix(m_Held);

        #region IPlayerAddon

        public void Initialize(CorePlayerManager playerManager)
        {
            m_Player = playerManager;
            m_CharacterController = GetComponent<CharacterController>();
        }

        public void OnPlayerSpawn()
        {
            if (!IsOwner)
            {
                enabled = false;
                return;
            }

            Local = this;
            m_Camera = Camera.main;
            if (m_Player.CoreMovement != null)
            {
                m_BaseMoveSpeed = m_Player.CoreMovement.moveSpeed;
                m_BaseSprintSpeed = m_Player.CoreMovement.sprintSpeed;
            }
            RefreshSpeed();
        }

        public void OnPlayerDespawn()
        {
            if (!IsOwner) return;
            Drop(Vector3.zero);
            LocalPlayerLocks.ReleaseAll(this);
            if (Local == this) Local = null;
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (IsOwner && newState == PlayerLifeState.Eliminated)
            {
                Drop(Vector3.zero);
                m_Pending = null;
            }
        }

        #endregion

        #region Rules

        private static Team LocalTeam => TeamMember.Local != null ? TeamMember.Local.Team : Team.None;
        private static MatchPhase Phase => MatchManager.Instance != null ? MatchManager.Instance.Phase : MatchPhase.Lobby;
        private static LoadoutKit LocalKit => PlayerLoadout.Local != null ? PlayerLoadout.Local.Kit : null;

        private static bool CanGrab(Grabbable item)
        {
            if (item == null || !item.IsSpawned || item.IsHeld) return false;

            var team = LocalTeam;
            var phase = Phase;
            if (phase == MatchPhase.Results) return false;

            if (item.IsFixed)
            {
                // Planted ladders can be knocked down by anyone; armed charges can't be touched;
                // barricades can only be taken apart by the defenders.
                if (item.FixRule == FixRule.Anyone) return true;
                if (item.FixRule == FixRule.Sappers) return false;
                return phase == MatchPhase.Lobby || (team == Team.Defenders && item.FixedBy == Team.Defenders);
            }

            // Before the assault attackers stay in their camp and may only take siege supplies.
            if (phase == MatchPhase.Build && team == Team.Attackers) return item.IsSiegeTool;
            return true;
        }

        private static bool NeedsUnfixHold(Grabbable item) => item.IsFixed && item.FixRule == FixRule.DefendersInBuildZone;

        private static bool CanFix(Grabbable item)
        {
            var phase = Phase;
            if (phase == MatchPhase.Results) return false;

            switch (item.FixRule)
            {
                case FixRule.Anyone:
                    return true;
                case FixRule.Sappers:
                    var kit = LocalKit;
                    return phase == MatchPhase.Lobby || (phase == MatchPhase.Assault && kit != null && kit.canArmCharges);
                case FixRule.DefendersInBuildZone:
                    var map = RemainsMap.Instance;
                    var match = MatchManager.Instance;
                    bool inZone = map == null || match == null || map.IsInBuildZone(item.transform.position, match.Config.buildZoneRadius);
                    // Defenders barricade before and during the assault (breaches must be plugged).
                    bool allowed = phase == MatchPhase.Lobby || LocalTeam == Team.Defenders;
                    return inZone && allowed;
                default:
                    return false;
            }
        }

        #endregion

        #region Public actions

        /// <summary>Tries to pick up <paramref name="item"/> (checks team/phase rules). Returns false if not allowed.</summary>
        public bool TryGrab(Grabbable item)
        {
            if (m_Held != null || m_Pending != null || !CanGrab(item)) return false;
            BeginGrab(item);
            return true;
        }

        /// <summary>Puts the carried object down.</summary>
        public void PutDown() => Drop(Vector3.zero);

        /// <summary>Throws the carried object where the camera looks.</summary>
        public void Throw()
        {
            if (m_Held == null || m_Camera == null) return;
            float massFactor = Mathf.Clamp(20f / Mathf.Max(1f, m_Held.Mass), 0.35f, 1.2f);
            Drop(m_Camera.transform.forward * throwSpeed * massFactor + Vector3.up * 1.5f);
        }

        /// <summary>Nails the carried object immediately if the rules allow it.</summary>
        public bool FixHeld()
        {
            if (m_Held == null || !CanFix(m_Held)) return false;
            var item = m_Held;
            ReleaseLocalState();
            item.Fix(LocalTeam);
            return true;
        }

        /// <summary>Rotates the carried object by 45° around the vertical (yaw) or horizontal (pitch) axis.</summary>
        public void RotateHeld(bool pitch)
        {
            m_LocalRotation = Quaternion.AngleAxis(45f, pitch ? Vector3.right : Vector3.up) * m_LocalRotation;
        }

        /// <summary>Re-applies movement speed from the kit and the carried weight.</summary>
        public void RefreshSpeed()
        {
            var movement = m_Player != null ? m_Player.CoreMovement : null;
            if (movement == null || m_BaseMoveSpeed <= 0f) return;

            var kit = LocalKit;
            float multiplier = kit != null ? kit.speedMultiplier : 1f;
            if (m_Held != null && (kit == null || !kit.noCarryPenalty))
            {
                multiplier *= m_Held.CarrySpeedMultiplier;
            }

            movement.moveSpeed = m_BaseMoveSpeed * multiplier;
            movement.sprintSpeed = m_BaseSprintSpeed * multiplier;
        }

        #endregion

        private void Update()
        {
            if (!IsSpawned || !IsOwner || LoadoutMenu.IsOpen) return;
            if (m_Camera == null) m_Camera = Camera.main;
            if (m_Camera == null) return;

            bool alive = m_Player.PlayerState != null && m_Player.PlayerState.IsActive;
            if (!alive)
            {
                m_Focused = null;
                return;
            }

            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool interactPressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            bool interactHeld = keyboard != null && keyboard.eKey.isPressed;

            UpdatePending();

            if (m_Held != null)
            {
                UpdateHeld(keyboard, mouse, interactPressed);
                return;
            }

            m_Focused = FindFocused();
            if (m_Focused == null || m_Pending != null)
            {
                m_UnfixProgress = 0f;
                return;
            }

            PublishFocusHint();

            if (NeedsUnfixHold(m_Focused))
            {
                if (interactHeld && CanGrab(m_Focused))
                {
                    m_UnfixProgress += Time.deltaTime / unfixHoldTime;
                    if (m_UnfixProgress >= 1f)
                    {
                        m_UnfixProgress = 0f;
                        BeginGrab(m_Focused);
                    }
                }
                else
                {
                    m_UnfixProgress = 0f;
                }
            }
            else if (interactPressed && CanGrab(m_Focused))
            {
                BeginGrab(m_Focused);
            }
        }

        private void PublishFocusHint()
        {
            var focused = m_Focused;
            string health = focused.Piece != null ? $" · {Mathf.CeilToInt(focused.Piece.Health)}/{Mathf.CeilToInt(focused.Piece.MaxHealth)}" : string.Empty;

            if (focused.IsHeld)
            {
                HudHint.Set($"{focused.DisplayName} (занято)");
            }
            else if (!CanGrab(focused))
            {
                HudHint.Set($"{focused.DisplayName}{health}");
            }
            else if (NeedsUnfixHold(focused))
            {
                HudHint.Set($"{focused.DisplayName}{health} · закреплено (держать E — снять)", m_UnfixProgress);
            }
            else
            {
                HudHint.Set($"E — взять · {focused.DisplayName}{health}");
            }
        }

        private Grabbable FindFocused()
        {
            var ray = new Ray(m_Camera.transform.position, m_Camera.transform.forward);
            int count = Physics.RaycastNonAlloc(ray, m_Hits, grabRange + 2f, ~0, QueryTriggerInteraction.Ignore);

            Grabbable best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var hit = m_Hits[i];
                if (hit.collider.transform.IsChildOf(transform)) continue;
                if (hit.distance >= bestDistance) continue;

                // Anything solid closer than the candidate blocks it.
                bestDistance = hit.distance;
                best = hit.collider.GetComponentInParent<Grabbable>();
            }

            // The camera sits behind the player's centre, so allow a little extra reach.
            if (best != null && Vector3.Distance(transform.position + Vector3.up, best.transform.position) > grabRange + 1.5f) return null;
            return best;
        }

        private void BeginGrab(Grabbable item)
        {
            if (item.IsOwner)
            {
                CompleteGrab(item);
                return;
            }

            if (item.NetworkObject.IsOwnershipLocked) return;

            m_Pending = item;
            m_PendingTimer = ownershipTimeout;
            item.NetworkObject.ChangeOwnership(NetworkManager.LocalClientId);
        }

        private void UpdatePending()
        {
            if (m_Pending == null) return;

            if (!m_Pending.IsSpawned)
            {
                m_Pending = null;
                return;
            }

            if (m_Pending.IsOwner)
            {
                var item = m_Pending;
                m_Pending = null;
                CompleteGrab(item);
                return;
            }

            m_PendingTimer -= Time.deltaTime;
            if (m_PendingTimer <= 0f) m_Pending = null;
        }

        private void CompleteGrab(Grabbable item)
        {
            if (!item.TryBeginHold(NetworkManager.LocalClientId)) return;

            m_Held = item;
            m_FixProgress = 0f;

            Vector3 cameraPosition = m_Camera.transform.position;
            float distance = Vector3.Distance(cameraPosition, item.transform.position);
            m_HoldDistance = Mathf.Clamp(Mathf.Max(distance, defaultHoldDistance), minHoldDistance, maxHoldDistance);

            // Keep the object's current orientation relative to where the player is facing.
            Quaternion yaw = Quaternion.Euler(0f, m_Camera.transform.eulerAngles.y, 0f);
            m_LocalRotation = Quaternion.Inverse(yaw) * item.transform.rotation;

            SetCarrying(item, true);
        }

        private void UpdateHeld(Keyboard keyboard, Mouse mouse, bool interactPressed)
        {
            // Lost it (destroyed, consumed by a cannon, stolen after a disconnect...).
            if (!m_Held.IsSpawned || !m_Held.IsOwner || m_Held.HeldBy != NetworkManager.LocalClientId)
            {
                ReleaseLocalState();
                return;
            }

            if (keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) RotateHeld(false);
                if (keyboard.qKey.wasPressedThisFrame) RotateHeld(true);
            }

            if (mouse != null)
            {
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    m_HoldDistance = Mathf.Clamp(m_HoldDistance + Mathf.Sign(scroll) * 0.25f, minHoldDistance, maxHoldDistance);
                }
            }

            Transform cameraTransform = m_Camera.transform;
            Quaternion yaw = Quaternion.Euler(0f, cameraTransform.eulerAngles.y, 0f);
            m_Held.SetHoldTarget(cameraTransform.position + cameraTransform.forward * m_HoldDistance, yaw * m_LocalRotation);

            bool canFix = CanFix(m_Held);
            string fixVerb = m_Held.FixRule == FixRule.Sappers ? "заминировать" : m_Held.FixRule == FixRule.Anyone ? "установить" : "закрепить";
            HudHint.Set(canFix
                    ? $"{m_Held.DisplayName}: ПКМ (держать) — {fixVerb} · ЛКМ — бросить · E — положить · R/Q — повернуть"
                    : $"{m_Held.DisplayName}: ЛКМ — бросить · E — положить · R/Q — повернуть",
                m_FixProgress, 10);

            if (interactPressed)
            {
                Drop(Vector3.zero);
                return;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Throw();
                return;
            }

            bool fixHeld = mouse != null && mouse.rightButton.isPressed;
            if (fixHeld && canFix)
            {
                float duration = MatchManager.Instance != null ? MatchManager.Instance.Config.fixDuration : 0.6f;
                m_FixProgress += Time.deltaTime / Mathf.Max(0.05f, duration);
                if (m_FixProgress >= 1f)
                {
                    FixHeld();
                }
            }
            else
            {
                m_FixProgress = 0f;
            }
        }

        private void Drop(Vector3 velocity)
        {
            if (m_Held == null) return;

            var item = m_Held;
            ReleaseLocalState();
            if (item.IsSpawned && item.IsOwner)
            {
                item.EndHold(velocity);
            }
        }

        private void ReleaseLocalState()
        {
            var item = m_Held;
            m_Held = null;
            m_FixProgress = 0f;
            if (item != null) SetCarrying(item, false);
        }

        private void SetCarrying(Grabbable item, bool carrying)
        {
            LocalPlayerLocks.SetWeapon(this, carrying);

            if (m_CharacterController != null && item != null && item.Colliders != null)
            {
                foreach (var itemCollider in item.Colliders)
                {
                    if (itemCollider != null) Physics.IgnoreCollision(m_CharacterController, itemCollider, carrying);
                }
            }

            RefreshSpeed();
        }
    }
}
