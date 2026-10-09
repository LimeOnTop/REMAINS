using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// A wheeled siege engine pushed by attackers (ram, cannon). Pushers hold E near it while facing it;
    /// speed grows with the combined push weight (kits can count double) and looking left/right steers.
    /// The owner simulates the body; pushers send lightweight push pulses.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public abstract class PushableSiege : NetworkBehaviour
    {
        private static readonly List<PushableSiege> s_All = new List<PushableSiege>();
        public static IReadOnlyList<PushableSiege> All => s_All;

        [Header("Pushing")]
        [SerializeField] private float speedPerPusher = 1.2f;
        [SerializeField] private float maxSpeed = 3.5f;
        [SerializeField] private float maxPushWeight = 4f;
        [SerializeField] private float pushRange = 3.2f;
        [SerializeField] private float turnSpeed = 25f;
        [SerializeField] private float acceleration = 2.5f;

        private readonly NetworkVariable<float> m_PushWeight = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private struct PushState
        {
            public float LastTime;
            public float Steer;
            public float Weight;
        }

        private readonly Dictionary<ulong, PushState> m_Pushers = new Dictionary<ulong, PushState>();
        private readonly List<ulong> m_Expired = new List<ulong>();
        private float m_LocalPushTimer;
        private Collider[] m_Colliders;

        protected Rigidbody Body { get; private set; }
        protected float ForwardSpeed { get; set; }
        protected ulong LastPusher { get; private set; } = ulong.MaxValue;

        public abstract string DisplayName { get; }
        public float PushWeight => m_PushWeight.Value;
        public float Speed => ForwardSpeed;
        public bool LocalCanPush { get; private set; }
        public bool LocalIsPushing { get; private set; }

        protected virtual void Awake()
        {
            Body = GetComponent<Rigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>();
            var networkRigidbody = GetComponent<NetworkRigidbody>();
            if (networkRigidbody != null) networkRigidbody.AutoUpdateKinematicState = true;
        }

        public override void OnNetworkSpawn()
        {
            s_All.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            s_All.Remove(this);
        }

        #region Local player (every client)

        protected virtual void Update()
        {
            if (!IsSpawned) return;
            UpdateLocalPusher();
        }

        /// <summary>Whether the local player may push/operate siege engines right now.</summary>
        protected static bool LocalMayUseSiege(out TeamMember member)
        {
            member = TeamMember.Local;
            if (LoadoutMenu.IsOpen) return false;
            if (member == null || member.Player == null || member.Player.PlayerState == null || !member.Player.PlayerState.IsActive) return false;
            if (GrabController.Local != null && GrabController.Local.Held != null) return false;

            var match = MatchManager.Instance;
            var phase = match != null ? match.Phase : MatchPhase.Lobby;
            return phase == MatchPhase.Lobby || (phase == MatchPhase.Assault && member.Team == Team.Attackers);
        }

        public Vector3 ClosestPoint(Vector3 position)
        {
            Vector3 best = transform.position;
            float bestDistance = float.MaxValue;
            foreach (var siegeCollider in m_Colliders)
            {
                if (siegeCollider == null || !siegeCollider.enabled) continue;
                Vector3 point = siegeCollider.ClosestPoint(position);
                float distance = (point - position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = point;
                }
            }
            return best;
        }

        protected bool IsLocalNearAndFacing(TeamMember member, float range)
        {
            var camera = Camera.main;
            if (camera == null) return false;

            Vector3 closest = ClosestPoint(member.transform.position);
            if (Vector3.Distance(closest, member.transform.position) > range) return false;

            Vector3 toSiege = closest - camera.transform.position;
            toSiege.y = 0f;
            Vector3 look = camera.transform.forward;
            look.y = 0f;
            return toSiege.sqrMagnitude < 0.01f || Vector3.Angle(look, toSiege) < 60f;
        }

        /// <summary>Hint shown when the local player can push. Subclasses may extend it.</summary>
        protected virtual string PushHint => LocalIsPushing
            ? $"{DisplayName.ToUpperInvariant()} · толкают: {m_PushWeight.Value:0.#} · смотрите влево/вправо, чтобы поворачивать"
            : $"Держать E — толкать · {DisplayName}";

        protected virtual bool BlocksPushing => false;

        private void UpdateLocalPusher()
        {
            LocalIsPushing = false;
            LocalCanPush = false;

            if (BlocksPushing || !LocalMayUseSiege(out var member)) return;
            if (!IsLocalNearAndFacing(member, pushRange)) return;

            LocalCanPush = true;
            var keyboard = Keyboard.current;
            LocalIsPushing = keyboard != null && keyboard.eKey.isPressed;
            HudHint.Set(PushHint, 0f, 1);
            if (!LocalIsPushing) return;

            m_LocalPushTimer -= Time.deltaTime;
            if (m_LocalPushTimer > 0f) return;
            m_LocalPushTimer = 0.1f;

            Vector3 look = Camera.main.transform.forward;
            look.y = 0f;
            Push(Vector3.SignedAngle(transform.forward, look, Vector3.up) / 45f);
        }

        /// <summary>Registers the local player as a pusher for a short moment (call repeatedly while pushing).</summary>
        public void Push(float steer)
        {
            var kit = PlayerLoadout.Local != null ? PlayerLoadout.Local.Kit : null;
            PushRpc(Mathf.Clamp(steer, -1f, 1f), kit != null ? kit.pushWeight : 1f);
        }

        [Rpc(SendTo.Owner)]
        private void PushRpc(float steer, float weight, RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            m_Pushers[sender] = new PushState { LastTime = Time.time, Steer = steer, Weight = Mathf.Clamp(weight, 0.5f, 3f) };
            LastPusher = sender;
        }

        #endregion

        #region Owner simulation

        protected virtual void FixedUpdate()
        {
            if (!IsSpawned || !HasAuthority) return;

            m_Expired.Clear();
            float weight = 0f, steerSum = 0f;
            foreach (var pair in m_Pushers)
            {
                if (Time.time - pair.Value.LastTime > 0.35f)
                {
                    m_Expired.Add(pair.Key);
                    continue;
                }
                weight += pair.Value.Weight;
                steerSum += pair.Value.Steer * pair.Value.Weight;
            }
            foreach (var id in m_Expired) m_Pushers.Remove(id);

            weight = Mathf.Min(weight, maxPushWeight);
            if (!Mathf.Approximately(m_PushWeight.Value, weight)) m_PushWeight.Value = weight;

            float targetSpeed = Mathf.Min(weight * speedPerPusher, maxSpeed);
            ForwardSpeed = Mathf.MoveTowards(ForwardSpeed, targetSpeed, acceleration * Time.fixedDeltaTime);

            if (weight > 0f)
            {
                float steer = steerSum / weight;
                Quaternion turn = Quaternion.Euler(0f, steer * turnSpeed * Time.fixedDeltaTime, 0f);
                Body.MoveRotation(turn * Body.rotation);
            }

            Vector3 velocity = transform.forward * ForwardSpeed;
            velocity.y = Body.linearVelocity.y;
            Body.linearVelocity = velocity;
        }

        #endregion

        /// <summary>Camera shake for local players close to <paramref name="point"/>.</summary>
        protected static void ShakeIfNear(Vector3 point, float radius, float strength)
        {
            var camera = Camera.main;
            if (camera == null) return;
            float distance = Vector3.Distance(camera.transform.position, point);
            if (distance > radius) return;

            Blocks.Gameplay.Core.CoreDirector.RequestCameraShake()
                .WithImpulseDefinition(Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseShapes.Bump,
                    Unity.Cinemachine.CinemachineImpulseDefinition.ImpulseTypes.Dissipating, 0.3f)
                .WithVelocity(strength * (1f - distance / radius))
                .Execute();
        }
    }
}
