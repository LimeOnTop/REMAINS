using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// The key objective in the throne room (Docs/GDD.md, section 3.4): the queen or the royal treasure.
    /// Attackers capture it by staying in the zone with no living defender inside; one defender in the zone
    /// contests (freezes) the capture; an empty zone slowly loses progress. Capturing wins the round for the attack.
    /// The session owner counts players and writes the replicated state.
    /// </summary>
    public class CaptureObjective : NetworkBehaviour
    {
        public static CaptureObjective Instance { get; private set; }

        [SerializeField] private GameObject queenVisual;
        [SerializeField] private GameObject treasureVisual;
        [Tooltip("Treasure chest lid; opens as the capture progresses.")]
        [SerializeField] private Transform chestLid;
        [SerializeField] private Light glow;
        [SerializeField] private float radius = 3.6f;
        [SerializeField] private float heightTolerance = 2.5f;

        private readonly NetworkVariable<ObjectiveKind> m_Kind = new NetworkVariable<ObjectiveKind>(ObjectiveKind.Queen);
        private readonly NetworkVariable<float> m_Progress = new NetworkVariable<float>(0f);
        private readonly NetworkVariable<byte> m_Attackers = new NetworkVariable<byte>(0);
        private readonly NetworkVariable<byte> m_Defenders = new NetworkVariable<byte>(0);
        private readonly NetworkVariable<bool> m_Captured = new NetworkVariable<bool>(false);

        private float m_CountTimer;
        private float m_LidAngle;
        private float m_GlowBase;

        public ObjectiveKind Kind => m_Kind.Value;
        public float Progress => m_Progress.Value;
        public int AttackersInZone => m_Attackers.Value;
        public int DefendersInZone => m_Defenders.Value;
        public bool IsCaptured => m_Captured.Value;
        public bool IsBeingCaptured => m_Attackers.Value > 0 && m_Defenders.Value == 0 && !m_Captured.Value;
        public bool IsContested => m_Attackers.Value > 0 && m_Defenders.Value > 0;
        public float Radius => radius;

        public string DisplayName => m_Kind.Value == ObjectiveKind.Queen ? "Королева" : "Сокровищница";
        public string CapturedText => m_Kind.Value == ObjectiveKind.Queen ? "КОРОЛЕВА ПЛЕНЕНА" : "СОКРОВИЩА ЗАХВАЧЕНЫ";
        public string UnderAttackText => m_Kind.Value == ObjectiveKind.Queen ? "КОРОЛЕВУ ЗАХВАТЫВАЮТ!" : "СОКРОВИЩНИЦУ ГРАБЯТ!";

        private void Awake()
        {
            if (glow != null) m_GlowBase = glow.intensity;
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            ApplyKind();
            m_Kind.OnValueChanged += HandleKindChanged;
        }

        public override void OnNetworkDespawn()
        {
            m_Kind.OnValueChanged -= HandleKindChanged;
            if (Instance == this) Instance = null;
        }

        private void HandleKindChanged(ObjectiveKind previous, ObjectiveKind current) => ApplyKind();

        private void ApplyKind()
        {
            if (queenVisual != null) queenVisual.SetActive(m_Kind.Value == ObjectiveKind.Queen);
            if (treasureVisual != null) treasureVisual.SetActive(m_Kind.Value == ObjectiveKind.Treasure);
        }

        /// <summary>Authority: new round with the given objective (Random is resolved here).</summary>
        public void ResetObjective(ObjectiveKind kind)
        {
            if (!HasAuthority) return;
            if (kind == ObjectiveKind.Random) kind = Random.value < 0.5f ? ObjectiveKind.Queen : ObjectiveKind.Treasure;
            m_Kind.Value = kind;
            m_Progress.Value = 0f;
            m_Attackers.Value = 0;
            m_Defenders.Value = 0;
            m_Captured.Value = false;
        }

        /// <summary>True if <paramref name="position"/> is inside the capture zone.</summary>
        public bool IsInZone(Vector3 position)
        {
            Vector3 offset = position - transform.position;
            if (Mathf.Abs(offset.y) > heightTolerance) return false;
            offset.y = 0f;
            return offset.magnitude <= radius;
        }

        private void Update()
        {
            UpdateVisuals();
            if (IsSpawned && HasAuthority) UpdateCapture();
        }

        private void UpdateCapture()
        {
            var match = MatchManager.Instance;
            if (match == null || match.Config == null) return;

            m_CountTimer -= Time.deltaTime;
            if (m_CountTimer > 0f) return;
            const float step = 0.2f;
            m_CountTimer = step;

            if (match.Phase != MatchPhase.Assault || m_Captured.Value)
            {
                SetCounts(0, 0);
                return;
            }

            int attackers = 0, defenders = 0;
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                var player = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
                if (player == null || !player.TryGetComponent<CorePlayerState>(out var state) || !state.IsActive) continue;
                if (!IsInZone(player.transform.position)) continue;

                var team = match.GetTeam(clientId);
                if (team == Team.Attackers) attackers++;
                else if (team == Team.Defenders) defenders++;
            }
            SetCounts(attackers, defenders);

            var config = match.Config;
            float progress = m_Progress.Value;
            if (attackers > 0 && defenders == 0)
            {
                float speed = Mathf.Min(2f, 1f + config.captureBonusPerAttacker * (attackers - 1));
                progress += step * speed / Mathf.Max(1f, config.captureSeconds);
            }
            else if (attackers == 0)
            {
                progress -= step * config.captureDecayPerSecond;
            }

            progress = Mathf.Clamp01(progress);
            if (!Mathf.Approximately(progress, m_Progress.Value)) m_Progress.Value = progress;
            if (progress >= 1f) m_Captured.Value = true;
        }

        private void SetCounts(int attackers, int defenders)
        {
            byte a = (byte)Mathf.Min(attackers, 255), d = (byte)Mathf.Min(defenders, 255);
            if (m_Attackers.Value != a) m_Attackers.Value = a;
            if (m_Defenders.Value != d) m_Defenders.Value = d;
        }

        private void UpdateVisuals()
        {
            // The chest opens as it is looted; the glow pulses while under attack.
            if (chestLid != null)
            {
                float target = -110f * Mathf.Clamp01(m_Progress.Value * 1.2f);
                m_LidAngle = Mathf.MoveTowards(m_LidAngle, target, 80f * Time.deltaTime);
                chestLid.localRotation = Quaternion.Euler(m_LidAngle, 0f, 0f);
            }

            if (glow != null)
            {
                float pulse = IsBeingCaptured ? 0.6f + 0.4f * Mathf.Sin(Time.time * 8f) : 1f;
                glow.intensity = m_GlowBase * pulse;
                glow.color = IsBeingCaptured ? new Color(1f, 0.45f, 0.3f) : new Color(1f, 0.85f, 0.5f);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(GameObject queen, GameObject treasure, Transform lid, Light light, float zoneRadius)
        {
            queenVisual = queen;
            treasureVisual = treasure;
            chestLid = lid;
            glow = light;
            radius = zoneRadius;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
