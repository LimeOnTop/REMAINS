using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// Field cannon (Docs/GDD.md, section 7.2). Push it with E like a ram; press F to man it — the barrel follows
    /// the gunner's view and LMB fires. Each shot needs a cannonball carried by hand to the muzzle.
    /// The gunner owns the cannon while manning it; the shot is replayed on every client and only the gunner's
    /// copy of the ball applies damage.
    /// </summary>
    public class SiegeCannon : PushableSiege
    {
        public const ulong NoOperator = ulong.MaxValue;

        [Header("Cannon")]
        [SerializeField] private Transform yawPivot;
        [SerializeField] private Transform pitchPivot;
        [SerializeField] private Transform muzzle;
        [SerializeField] private Transform loadPoint;
        [SerializeField] private float muzzleSpeed = 42f;
        [SerializeField] private float spreadDegrees = 1.2f;
        [SerializeField] private float reloadSeconds = 4f;
        [SerializeField] private float yawLimit = 40f;
        [SerializeField] private float minPitch = -6f;
        [SerializeField] private float maxPitch = 32f;
        [SerializeField] private float manRange = 2.8f;
        [SerializeField] private float loadRange = 1.5f;
        [SerializeField] private Material dustMaterial;
        [SerializeField] private Material ballMaterial;

        private readonly NetworkVariable<ulong> m_Operator = new NetworkVariable<ulong>(NoOperator, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<bool> m_Loaded = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> m_Yaw = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<float> m_Pitch = new NetworkVariable<float>(8f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private bool m_WantsToMan;
        private float m_ManTimeout;
        private bool m_LocalManning;
        private double m_NextFireTime;
        private float m_VisualYaw;
        private float m_VisualPitch = 8f;

        public override string DisplayName => "Пушка";
        public bool IsLoaded => m_Loaded.Value;
        public ulong Operator => m_Operator.Value;
        public Vector3 LoadPoint => loadPoint != null ? loadPoint.position : transform.position;
        public float LoadRange => loadRange;

        protected override bool BlocksPushing => m_LocalManning;

        public override void OnNetworkDespawn()
        {
            StopLocalManning();
            base.OnNetworkDespawn();
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            base.OnOwnershipChanged(previous, current);

            // The previous gunner left or lost the cannon: free the seat.
            if (HasAuthority && m_Operator.Value != NoOperator && m_Operator.Value != NetworkManager.LocalClientId)
            {
                m_Operator.Value = NoOperator;
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!IsSpawned) return;

            UpdateVisualAim();
            UpdateLocalGunner();
        }

        private void UpdateVisualAim()
        {
            m_VisualYaw = Mathf.MoveTowardsAngle(m_VisualYaw, m_Yaw.Value, 90f * Time.deltaTime);
            m_VisualPitch = Mathf.MoveTowards(m_VisualPitch, m_Pitch.Value, 60f * Time.deltaTime);
            if (yawPivot != null) yawPivot.localRotation = Quaternion.Euler(0f, m_VisualYaw, 0f);
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.Euler(-m_VisualPitch, 0f, 0f);
        }

        #region Gunner

        private void UpdateLocalGunner()
        {
            ulong localId = NetworkManager.LocalClientId;
            bool isOperator = m_Operator.Value == localId && IsOwner;

            if (m_LocalManning && !isOperator && !m_WantsToMan)
            {
                StopLocalManning();
            }

            var keyboard = Keyboard.current;
            bool fPressed = keyboard != null && keyboard.fKey.wasPressedThisFrame;

            if (m_WantsToMan)
            {
                m_ManTimeout -= Time.deltaTime;
                if (IsOwner && !NetworkObject.IsOwnershipLocked)
                {
                    m_WantsToMan = false;
                    if (m_Operator.Value == NoOperator || m_Operator.Value == localId)
                    {
                        NetworkObject.SetOwnershipLock(true);
                        m_Operator.Value = localId;
                        StartLocalManning();
                    }
                }
                else if (m_ManTimeout <= 0f)
                {
                    m_WantsToMan = false;
                }
                return;
            }

            if (isOperator)
            {
                if (!m_LocalManning) StartLocalManning();
                bool stillValid = LocalMayUseSiege(out var gunner) && Vector3.Distance(ClosestPoint(gunner.transform.position), gunner.transform.position) < manRange + 1.5f;
                if (!stillValid || fPressed)
                {
                    ReleaseSeat();
                    return;
                }
                OperateLocal();
                return;
            }

            // Not manning: offer the seat.
            if (m_Operator.Value != NoOperator || !LocalMayUseSiege(out var member)) return;
            if (!IsLocalNearAndFacing(member, manRange)) return;

            string ammo = m_Loaded.Value ? "заряжена" : "нет ядра";
            HudHint.Set($"F — встать к пушке ({ammo}) · держать E — толкать", 0f, 2);
            if (!fPressed) return;

            if (IsOwner)
            {
                NetworkObject.SetOwnershipLock(true);
                m_Operator.Value = localId;
                StartLocalManning();
            }
            else if (!NetworkObject.IsOwnershipLocked)
            {
                m_WantsToMan = true;
                m_ManTimeout = 0.75f;
                NetworkObject.ChangeOwnership(localId);
            }
        }

        private void OperateLocal()
        {
            var camera = Camera.main;
            if (camera == null) return;

            Vector3 look = camera.transform.forward;
            Vector3 flat = new Vector3(look.x, 0f, look.z);
            float yaw = Mathf.Clamp(Vector3.SignedAngle(transform.forward, flat, Vector3.up), -yawLimit, yawLimit);
            float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f)) * Mathf.Rad2Deg + 4f, minPitch, maxPitch);
            if (Mathf.Abs(Mathf.DeltaAngle(m_Yaw.Value, yaw)) > 0.2f) m_Yaw.Value = yaw;
            if (Mathf.Abs(m_Pitch.Value - pitch) > 0.2f) m_Pitch.Value = pitch;

            double now = MatchManager.Instance != null ? MatchManager.Instance.Now : Time.timeAsDouble;
            bool ready = m_Loaded.Value && now >= m_NextFireTime;
            var match = MatchManager.Instance;
            bool phaseAllowsFire = match == null || match.Phase == MatchPhase.Assault || match.Phase == MatchPhase.Lobby;

            string status = !m_Loaded.Value ? "нужно ядро — поднесите ядро к стволу"
                : !phaseAllowsFire ? "огонь только во время штурма"
                : now < m_NextFireTime ? "банник, пыж, фитиль..." : "ЛКМ — огонь!";
            HudHint.Set($"ПУШКА: {status} · F — отойти", 0f, 20);

            var mouse = Mouse.current;
            if (ready && phaseAllowsFire && mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Fire(now);
            }
        }

        private void Fire(double now)
        {
            // Use the replicated aim so everybody sees the same trajectory.
            Quaternion aim = transform.rotation * Quaternion.Euler(-m_Pitch.Value, m_Yaw.Value, 0f);
            Vector3 direction = Quaternion.Euler(Random.Range(-spreadDegrees, spreadDegrees), Random.Range(-spreadDegrees, spreadDegrees), 0f) * (aim * Vector3.forward);
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + aim * Vector3.forward * 2f;

            var kit = PlayerLoadout.Local != null ? PlayerLoadout.Local.Kit : null;
            float reload = reloadSeconds * (kit != null ? kit.cannonReloadMultiplier : 1f);
            m_NextFireTime = now + reload;
            m_Loaded.Value = false;

            FireRpc(origin, direction * muzzleSpeed, NetworkManager.LocalClientId);
            ForwardSpeed = -1.2f;
        }

        [Rpc(SendTo.Everyone)]
        private void FireRpc(Vector3 origin, Vector3 velocity, ulong shooter)
        {
            bool authoritative = shooter == NetworkManager.LocalClientId;
            CannonballProjectile.Launch(origin, velocity, authoritative, shooter, GetComponentsInChildren<Collider>(), ballMaterial, dustMaterial);
            if (dustMaterial != null) BrickDebris.Dust(origin, 1.6f, dustMaterial, 7);
            ShakeIfNear(origin, 25f, 0.12f);
        }

        private void ReleaseSeat()
        {
            if (IsOwner)
            {
                m_Operator.Value = NoOperator;
                NetworkObject.SetOwnershipLock(false);
            }
            StopLocalManning();
        }

        private void StartLocalManning()
        {
            if (m_LocalManning) return;
            m_LocalManning = true;
            LocalPlayerLocks.SetMovement(this, true);
            LocalPlayerLocks.SetWeapon(this, true);
        }

        private void StopLocalManning()
        {
            if (!m_LocalManning) return;
            m_LocalManning = false;
            LocalPlayerLocks.ReleaseAll(this);
        }

        #endregion

        #region Loading

        /// <summary>Loads the cannon (called by the carrier of a cannonball that reached the muzzle).</summary>
        public void Load()
        {
            if (!IsSpawned || m_Loaded.Value) return;
            if (HasAuthority) m_Loaded.Value = true;
            else LoadRpc();
        }

        [Rpc(SendTo.Owner)]
        private void LoadRpc()
        {
            m_Loaded.Value = true;
        }

        #endregion
    }
}
