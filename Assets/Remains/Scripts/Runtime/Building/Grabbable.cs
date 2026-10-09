using System;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

namespace Remains
{
    public enum FixRule : byte
    {
        /// <summary>Can't be nailed in place.</summary>
        Never = 0,
        /// <summary>Defenders nail it inside the build zone during the build phase (walls, crates...).</summary>
        DefendersInBuildZone = 1,
        /// <summary>Anyone can plant it anywhere (ladders).</summary>
        Anyone = 2,
        /// <summary>Only players whose kit can arm charges may plant it (powder charges).</summary>
        Sappers = 3
    }

    /// <summary>
    /// A physical object players can pick up, carry, rotate, throw and nail in place.
    /// Carrying uses distributed-authority ownership: the carrier takes ownership and simulates the body,
    /// everybody else sees the synchronized transform. See Docs/GDD.md, section 5.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Grabbable : NetworkBehaviour
    {
        public const ulong NoHolder = ulong.MaxValue;

        [SerializeField] private string displayName = "Предмет";
        [SerializeField] private FixRule fixRule = FixRule.DefendersInBuildZone;
        [Tooltip("Siege tools can be picked up by attackers while they wait in their base.")]
        [SerializeField] private bool isSiegeTool;
        [Tooltip("Maximum speed the carried body moves at, scaled down for heavier objects.")]
        [SerializeField] private float maxCarrySpeed = 14f;

        private readonly NetworkVariable<ulong> m_HeldBy = new NetworkVariable<ulong>(NoHolder, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_IsFixed = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Team> m_FixedBy = new NetworkVariable<Team>(Team.None, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private Rigidbody m_Rigidbody;
        private NetworkRigidbody m_NetworkRigidbody;
        private Collider[] m_Colliders;
        private Vector3 m_TargetPosition;
        private Quaternion m_TargetRotation;
        private bool m_HasTarget;

        public event Action StateChanged;

        public string DisplayName => displayName;
        public FixRule FixRule => fixRule;
        public bool IsSiegeTool => isSiegeTool;
        public bool IsHeld => m_HeldBy.Value != NoHolder;
        public ulong HeldBy => m_HeldBy.Value;
        public bool IsFixed => m_IsFixed.Value;
        public Team FixedBy => m_FixedBy.Value;
        public float Mass => m_Rigidbody != null ? m_Rigidbody.mass : 1f;
        public Rigidbody Body => m_Rigidbody;
        public Collider[] Colliders => m_Colliders;
        public BuildPiece Piece { get; private set; }

        /// <summary>Movement speed multiplier applied to the carrier (heavier = slower).</summary>
        public float CarrySpeedMultiplier => Mathf.Clamp(1f - (Mass - 15f) / 150f, 0.6f, 1f);

        private void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_NetworkRigidbody = GetComponent<NetworkRigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>();
            Piece = GetComponent<BuildPiece>();

            if (m_NetworkRigidbody != null)
            {
                // Kinematic state is driven by ownership *and* the fixed flag, so manage it here.
                m_NetworkRigidbody.AutoUpdateKinematicState = false;
            }
        }

        public override void OnNetworkSpawn()
        {
            m_HeldBy.OnValueChanged += HandleHeldChanged;
            m_IsFixed.OnValueChanged += HandleFixedChanged;
            ApplyPhysicsState();
        }

        public override void OnNetworkDespawn()
        {
            m_HeldBy.OnValueChanged -= HandleHeldChanged;
            m_IsFixed.OnValueChanged -= HandleFixedChanged;
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            base.OnOwnershipChanged(previous, current);

            // If the carrier disconnected, the object was redistributed while still flagged as held.
            if (HasAuthority && IsHeld && m_HeldBy.Value != NetworkManager.LocalClientId)
            {
                m_HeldBy.Value = NoHolder;
            }
            if (!HasAuthority) m_HasTarget = false;

            ApplyPhysicsState();
        }

        private void HandleHeldChanged(ulong previous, ulong current)
        {
            ApplyPhysicsState();
            StateChanged?.Invoke();
        }

        private void HandleFixedChanged(bool previous, bool current)
        {
            ApplyPhysicsState();
            StateChanged?.Invoke();
        }

        private void ApplyPhysicsState()
        {
            if (m_Rigidbody == null) return;

            bool simulate = IsSpawned && HasAuthority && !m_IsFixed.Value;
            m_Rigidbody.isKinematic = !simulate;
            m_Rigidbody.useGravity = !(simulate && IsHeld);
            m_Rigidbody.interpolation = simulate && IsHeld ? RigidbodyInterpolation.Interpolate : RigidbodyInterpolation.None;
        }

        #region Owner-side API (call only while owning the object)

        /// <summary>Marks the object as carried by <paramref name="clientId"/>. Requires ownership.</summary>
        public bool TryBeginHold(ulong clientId)
        {
            if (!HasAuthority || IsHeld) return false;

            NetworkObject.SetOwnershipLock(true);
            m_IsFixed.Value = false;
            m_FixedBy.Value = Team.None;
            m_HeldBy.Value = clientId;
            m_HasTarget = false;
            ApplyPhysicsState();
            return true;
        }

        public void SetHoldTarget(Vector3 position, Quaternion rotation)
        {
            m_TargetPosition = position;
            m_TargetRotation = rotation;
            m_HasTarget = true;
        }

        /// <summary>Stops carrying and optionally throws the object.</summary>
        public void EndHold(Vector3 releaseVelocity)
        {
            if (!HasAuthority) return;

            m_HasTarget = false;
            m_HeldBy.Value = NoHolder;
            NetworkObject.SetOwnershipLock(false);
            ApplyPhysicsState();

            m_Rigidbody.linearVelocity = releaseVelocity;
            m_Rigidbody.angularVelocity *= 0.3f;
        }

        /// <summary>Nails the object in place where it currently is.</summary>
        public void Fix(Team team)
        {
            if (!HasAuthority) return;

            m_HasTarget = false;
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
            m_HeldBy.Value = NoHolder;
            m_FixedBy.Value = team;
            m_IsFixed.Value = true;
            NetworkObject.SetOwnershipLock(false);
            ApplyPhysicsState();
        }

        /// <summary>Releases a nailed object so it falls (used when its support is destroyed).</summary>
        public void Unfix()
        {
            if (!HasAuthority || !m_IsFixed.Value) return;
            m_IsFixed.Value = false;
            m_FixedBy.Value = Team.None;
            ApplyPhysicsState();
        }

        #endregion

        private void FixedUpdate()
        {
            if (!m_HasTarget || !IsSpawned || !HasAuthority || !IsHeld || m_IsFixed.Value) return;

            float dt = Time.fixedDeltaTime;
            float speedLimit = maxCarrySpeed / Mathf.Sqrt(Mathf.Max(1f, Mass / 10f));

            Vector3 toTarget = m_TargetPosition - m_Rigidbody.position;
            Vector3 velocity = toTarget / dt * 0.5f;
            m_Rigidbody.linearVelocity = Vector3.ClampMagnitude(velocity, Mathf.Max(4f, speedLimit));

            Quaternion delta = m_TargetRotation * Quaternion.Inverse(m_Rigidbody.rotation);
            delta.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f) angle -= 360f;
            if (float.IsNaN(axis.x) || Mathf.Abs(angle) < 0.01f)
            {
                m_Rigidbody.angularVelocity = Vector3.zero;
            }
            else
            {
                Vector3 angular = axis * (angle * Mathf.Deg2Rad / dt * 0.3f);
                m_Rigidbody.angularVelocity = Vector3.ClampMagnitude(angular, 15f);
            }
        }
    }
}
