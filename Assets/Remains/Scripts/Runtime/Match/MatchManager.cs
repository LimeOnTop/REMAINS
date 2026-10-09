using System;
using System.Collections.Generic;
using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Remains
{
    /// <summary>
    /// A client's persistent side for the whole match. Roles (defend/attack) swap every round,
    /// so the group is stored instead of the team.
    /// </summary>
    public struct TeamAssignment : INetworkSerializable, IEquatable<TeamAssignment>
    {
        public ulong ClientId;
        public byte Group;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Group);
        }

        public bool Equals(TeamAssignment other) => ClientId == other.ClientId && Group == other.Group;
        public override bool Equals(object obj) => obj is TeamAssignment other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(ClientId, Group);
    }

    /// <summary>
    /// Match state machine: lobby → build → assault → results → (swap sides) → build...
    /// Runs on the authority (session owner in distributed authority, server in client-server);
    /// every other client reads the replicated state. See Docs/GDD.md, sections 2–3.
    /// </summary>
    public class MatchManager : NetworkBehaviour
    {
        public static MatchManager Instance { get; private set; }

        /// <summary>Raised on every client when the phase changes (previous, current).</summary>
        public static event Action<MatchPhase, MatchPhase> PhaseChanged;

        /// <summary>Raised on every client when team membership or roles change.</summary>
        public static event Action TeamsChanged;

        /// <summary>Raised on the requesting client when the authority answers a respawn request (granted, respawnAtServerTime).</summary>
        public static event Action<bool, double> RespawnResponse;

        [SerializeField] private MatchConfig config;
        [SerializeField] private RemainsMap map;


        private readonly NetworkVariable<MatchPhase> m_Phase = new NetworkVariable<MatchPhase>(MatchPhase.Lobby);
        private readonly NetworkVariable<double> m_PhaseEndTime = new NetworkVariable<double>(0);
        private readonly NetworkVariable<int> m_Round = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> m_AttackerTickets = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> m_DefendersAlive = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> m_DefendersTotal = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> m_AttackersAlive = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> m_AttackersTotal = new NetworkVariable<int>(0);
        private readonly NetworkVariable<Team> m_LastWinner = new NetworkVariable<Team>(Team.None);
        private readonly NetworkVariable<RoundEndReason> m_LastReason = new NetworkVariable<RoundEndReason>(RoundEndReason.None);
        private readonly NetworkVariable<bool> m_Overtime = new NetworkVariable<bool>(false);
        private readonly NetworkVariable<int> m_ScoreGroup0 = new NetworkVariable<int>(0);
        private readonly NetworkVariable<int> m_ScoreGroup1 = new NetworkVariable<int>(0);
        private NetworkList<TeamAssignment> m_Assignments;
        private readonly NetworkVariable<Unity.Collections.FixedString64Bytes> m_MatchId = new NetworkVariable<Unity.Collections.FixedString64Bytes>();

        // Authority-only bookkeeping: attackers that were granted a ticket but have not respawned yet.
        private readonly Dictionary<ulong, double> m_PendingRespawns = new Dictionary<ulong, double>();
        private readonly List<ulong> m_ScratchIds = new List<ulong>();
        private float m_SyncTimer;

        #region Public state

        public MatchConfig Config => config;
        public MatchPhase Phase => m_Phase.Value;
        public int Round => m_Round.Value;
        public int AttackerTickets => m_AttackerTickets.Value;
        public int DefendersAlive => m_DefendersAlive.Value;
        public int DefendersTotal => m_DefendersTotal.Value;
        public int AttackersAlive => m_AttackersAlive.Value;
        public int AttackersTotal => m_AttackersTotal.Value;
        public Team LastWinner => m_LastWinner.Value;
        public RoundEndReason LastReason => m_LastReason.Value;
        /// <summary>True while the assault is extended because attackers are capturing the objective.</summary>
        public bool IsOvertime => m_Overtime.Value;
        public int PlayerCount => m_Assignments != null ? m_Assignments.Count : 0;

        /// <summary>Unique id of this match, shared by all clients (used for voice channel names).</summary>
        public string MatchId => m_MatchId.Value.ToString();

        public double Now => NetworkManager != null ? NetworkManager.ServerTime.Time : 0d;

        /// <summary>Seconds left in the current phase (0 when the phase has no timer running).</summary>
        public float TimeRemaining => m_PhaseEndTime.Value <= 0 ? 0f : Mathf.Max(0f, (float)(m_PhaseEndTime.Value - Now));

        public bool IsLobbyCountdownRunning => Phase == MatchPhase.Lobby && m_PhaseEndTime.Value > 0;

        public Team GetTeam(ulong clientId)
        {
            if (m_Assignments == null) return Team.None;
            foreach (var assignment in m_Assignments)
            {
                if (assignment.ClientId == clientId) return GroupToTeam(assignment.Group, m_Round.Value);
            }
            return Team.None;
        }

        public Team LocalTeam => NetworkManager != null ? GetTeam(NetworkManager.LocalClientId) : Team.None;

        /// <summary>Round wins of the side the local player belongs to, and of the other side.</summary>
        public void GetScores(out int ours, out int theirs)
        {
            int localGroup = GetGroup(NetworkManager != null ? NetworkManager.LocalClientId : ulong.MaxValue);
            int s0 = m_ScoreGroup0.Value, s1 = m_ScoreGroup1.Value;
            ours = localGroup == 1 ? s1 : s0;
            theirs = localGroup == 1 ? s0 : s1;
        }

        #endregion

        #region Lifecycle

        private void Awake()
        {
            m_Assignments = new NetworkList<TeamAssignment>();
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            if (map == null) map = RemainsMap.Instance;

            m_Phase.OnValueChanged += HandlePhaseChanged;
            m_Round.OnValueChanged += HandleRoundChanged;
            m_Assignments.OnListChanged += HandleAssignmentsChanged;

            DamageRules.CanDamagePlayer = CanDamagePlayer;

            if (HasAuthority && m_MatchId.Value.Length == 0)
            {
                m_MatchId.Value = Guid.NewGuid().ToString("N").Substring(0, 12);
            }

            // Warm-up sandbox: players waiting in the lobby can already play with materials and siege engines.
            if (HasAuthority && m_Phase.Value == MatchPhase.Lobby && RoundObject.All.Count == 0)
            {
                SpawnRoundObjects();
            }

            PhaseChanged?.Invoke(m_Phase.Value, m_Phase.Value);
            TeamsChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            m_Phase.OnValueChanged -= HandlePhaseChanged;
            m_Round.OnValueChanged -= HandleRoundChanged;
            m_Assignments.OnListChanged -= HandleAssignmentsChanged;

            if (DamageRules.CanDamagePlayer == CanDamagePlayer) DamageRules.CanDamagePlayer = null;
            if (Instance == this) Instance = null;
        }

        private void HandlePhaseChanged(MatchPhase previous, MatchPhase current) => PhaseChanged?.Invoke(previous, current);
        private void HandleRoundChanged(int previous, int current) => TeamsChanged?.Invoke();
        private void HandleAssignmentsChanged(NetworkListEvent<TeamAssignment> change) => TeamsChanged?.Invoke();

        #endregion

        #region Rules

        private static Team GroupToTeam(byte group, int round)
        {
            // Group 0 defends in even rounds, group 1 in odd rounds.
            bool defends = (group == 0) == (round % 2 == 0);
            return defends ? Team.Defenders : Team.Attackers;
        }

        private byte TeamToGroup(Team team)
        {
            bool group0Defends = m_Round.Value % 2 == 0;
            if (team == Team.Defenders) return (byte)(group0Defends ? 0 : 1);
            return (byte)(group0Defends ? 1 : 0);
        }

        private int GetGroup(ulong clientId)
        {
            foreach (var assignment in m_Assignments)
            {
                if (assignment.ClientId == clientId) return assignment.Group;
            }
            return -1;
        }

        private bool CanDamagePlayer(ulong attackerId, ulong victimId)
        {
            var phase = m_Phase.Value;
            if (phase == MatchPhase.Build || phase == MatchPhase.Results) return false;

            if (config != null && config.friendlyFire) return true;

            var attackerTeam = GetTeam(attackerId);
            return attackerTeam == Team.None || attackerTeam != GetTeam(victimId);
        }

        /// <summary>
        /// True if structures may currently be damaged (assault, plus the lobby warm-up; walls are rebuilt each round).
        /// </summary>
        public bool StructuresVulnerable => m_Phase.Value == MatchPhase.Assault || m_Phase.Value == MatchPhase.Lobby;

        #endregion

        #region Authority loop

        private void Update()
        {
            if (!IsSpawned || !HasAuthority || config == null) return;

            m_SyncTimer -= Time.deltaTime;
            if (m_SyncTimer <= 0f)
            {
                m_SyncTimer = 0.5f;
                SyncAssignments();
                UpdateAliveCounts();
            }

            double now = Now;
            switch (m_Phase.Value)
            {
                case MatchPhase.Lobby:
                    UpdateLobby(now);
                    break;
                case MatchPhase.Build:
                    if (now >= m_PhaseEndTime.Value) StartAssault(now);
                    break;
                case MatchPhase.Assault:
                    UpdateAssault(now);
                    break;
                case MatchPhase.Results:
                    if (now >= m_PhaseEndTime.Value)
                    {
                        m_Round.Value++;
                        StartRound(now, false);
                    }
                    break;
            }
        }

        private void UpdateLobby(double now)
        {
            bool enoughPlayers = m_Assignments.Count >= config.minPlayersToStart;
            if (!enoughPlayers)
            {
                if (m_PhaseEndTime.Value > 0) m_PhaseEndTime.Value = 0;
                return;
            }

            if (m_PhaseEndTime.Value <= 0)
            {
                m_PhaseEndTime.Value = now + config.lobbyCountdown;
            }
            else if (now >= m_PhaseEndTime.Value)
            {
                StartRound(now, true);
            }
        }

        private void UpdateAssault(double now)
        {
            // Forget granted respawns that should have happened long ago (player left, etc.).
            m_ScratchIds.Clear();
            foreach (var pending in m_PendingRespawns)
            {
                if (now > pending.Value) m_ScratchIds.Add(pending.Key);
            }
            foreach (var id in m_ScratchIds) m_PendingRespawns.Remove(id);

            var objective = CaptureObjective.Instance;
            if (objective != null && objective.IsCaptured)
            {
                EndRound(now, Team.Attackers, RoundEndReason.ObjectiveCaptured);
                return;
            }

            bool bothTeamsPresent = m_DefendersTotal.Value > 0 && m_AttackersTotal.Value > 0;
            if (bothTeamsPresent)
            {
                if (m_DefendersAlive.Value == 0)
                {
                    EndRound(now, Team.Attackers, RoundEndReason.DefendersEliminated);
                    return;
                }

                if (m_AttackerTickets.Value <= 0 && m_AttackersAlive.Value == 0 && m_PendingRespawns.Count == 0)
                {
                    EndRound(now, Team.Defenders, RoundEndReason.AttackersExhausted);
                    return;
                }
            }

            if (now >= m_PhaseEndTime.Value)
            {
                // Overtime: the round goes on while attackers keep capturing (up to the configured limit).
                bool capturing = objective != null && objective.AttackersInZone > 0 && objective.Progress > 0f;
                if (capturing && now < m_PhaseEndTime.Value + config.maxOvertime)
                {
                    if (!m_Overtime.Value) m_Overtime.Value = true;
                    return;
                }
                EndRound(now, Team.Defenders, RoundEndReason.TimeUp);
            }
        }

        private void SyncAssignments()
        {
            var connected = NetworkManager.ConnectedClientsIds;

            for (int i = m_Assignments.Count - 1; i >= 0; i--)
            {
                if (!Contains(connected, m_Assignments[i].ClientId))
                {
                    m_Assignments.RemoveAt(i);
                }
            }

            foreach (var clientId in connected)
            {
                if (GetGroup(clientId) >= 0) continue;

                byte group;
                if (m_Phase.Value == MatchPhase.Lobby)
                {
                    CountGroups(out int g0, out int g1);
                    group = (byte)(g0 <= g1 ? 0 : 1);
                }
                else
                {
                    // Late joiners always reinforce the attack (GDD 2.1).
                    group = TeamToGroup(Team.Attackers);
                }

                m_Assignments.Add(new TeamAssignment { ClientId = clientId, Group = group });
            }
        }

        private static bool Contains(IReadOnlyList<ulong> list, ulong value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == value) return true;
            }
            return false;
        }

        private void CountGroups(out int group0, out int group1)
        {
            group0 = 0;
            group1 = 0;
            foreach (var assignment in m_Assignments)
            {
                if (assignment.Group == 0) group0++;
                else group1++;
            }
        }

        private void UpdateAliveCounts()
        {
            int defendersAlive = 0, defendersTotal = 0, attackersAlive = 0, attackersTotal = 0;

            foreach (var assignment in m_Assignments)
            {
                var team = GroupToTeam(assignment.Group, m_Round.Value);
                bool alive = IsPlayerAlive(assignment.ClientId);

                if (team == Team.Defenders)
                {
                    defendersTotal++;
                    if (alive) defendersAlive++;
                }
                else
                {
                    attackersTotal++;
                    if (alive) attackersAlive++;
                    if (alive) m_PendingRespawns.Remove(assignment.ClientId);
                }
            }

            SetIfChanged(m_DefendersAlive, defendersAlive);
            SetIfChanged(m_DefendersTotal, defendersTotal);
            SetIfChanged(m_AttackersAlive, attackersAlive);
            SetIfChanged(m_AttackersTotal, attackersTotal);
        }

        private bool IsPlayerAlive(ulong clientId)
        {
            var playerObject = NetworkManager.SpawnManager.GetPlayerNetworkObject(clientId);
            if (playerObject == null) return false;
            return playerObject.TryGetComponent<CorePlayerState>(out var state) && state.IsActive;
        }

        private static void SetIfChanged(NetworkVariable<int> variable, int value)
        {
            if (variable.Value != value) variable.Value = value;
        }

        private void StartRound(double now, bool fresh)
        {
            if (fresh)
            {
                ShuffleGroups();
            }

            m_PendingRespawns.Clear();
            m_LastWinner.Value = Team.None;
            m_AttackerTickets.Value = 0;

            RoundObject.DespawnAll();
            ResetWorld();
            UpdateAliveCounts();
            SpawnRoundObjects();

            m_PhaseEndTime.Value = now + config.buildPhaseDuration;
            m_Phase.Value = MatchPhase.Build;
        }

        private void ShuffleGroups()
        {
            var ids = new List<ulong>();
            foreach (var assignment in m_Assignments) ids.Add(assignment.ClientId);

            for (int i = ids.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (ids[i], ids[j]) = (ids[j], ids[i]);
            }

            m_Assignments.Clear();
            for (int i = 0; i < ids.Count; i++)
            {
                // With an odd count the extra player lands in group 1, which attacks in the first round.
                m_Assignments.Add(new TeamAssignment { ClientId = ids[i], Group = (byte)(i % 2 == 0 ? 1 : 0) });
            }
        }

        private void StartAssault(double now)
        {
            UpdateAliveCounts();

            bool small = m_Assignments.Count < config.smallSessionThreshold;
            float perPlayer = small ? config.smallSessionTicketsPerPlayer : config.attackerTicketsPerPlayer;
            m_AttackerTickets.Value = Mathf.CeilToInt(Mathf.Max(1, m_AttackersTotal.Value) * perPlayer);

            float duration = small ? config.smallSessionAssaultDuration : config.assaultPhaseDuration;
            m_PhaseEndTime.Value = now + duration;
            m_Phase.Value = MatchPhase.Assault;
        }

        private void EndRound(double now, Team winner, RoundEndReason reason)
        {
            m_LastWinner.Value = winner;
            m_LastReason.Value = reason;
            m_Overtime.Value = false;

            byte winningGroup = TeamToGroup(winner);
            if (winningGroup == 0) m_ScoreGroup0.Value++;
            else m_ScoreGroup1.Value++;

            m_PhaseEndTime.Value = now + config.resultsDuration;
            m_Phase.Value = MatchPhase.Results;
        }

        private void SpawnRoundObjects()
        {
            if (map == null) map = RemainsMap.Instance;
            if (map == null) return;

            foreach (var placement in map.placements)
            {
                if (placement.prefab == null) continue;
                var instance = Instantiate(placement.prefab, placement.position, placement.rotation);
                instance.Spawn();
            }
        }

        /// <summary>Authority: restores the castle, closes doors and wipes the attack plan for a new round.</summary>
        private void ResetWorld()
        {
            foreach (var wall in WallSection.All) wall.ResetAll();
            foreach (var door in FindObjectsByType<InteractiveDoor>()) door.ResetState();
            if (BattleMapBoard.Instance != null) BattleMapBoard.Instance.ClearBoard();
            if (CaptureObjective.Instance != null) CaptureObjective.Instance.ResetObjective(config.objectiveKind);
        }

        #endregion

        #region Respawn requests

        /// <summary>
        /// Called by an eliminated attacker during the assault. The authority spends one ticket from the shared
        /// pool and tells the requester when its respawn wave departs.
        /// </summary>
        [Rpc(SendTo.Owner)]
        public void RequestRespawnRpc(RpcParams rpcParams = default)
        {
            ulong sender = rpcParams.Receive.SenderClientId;
            double now = Now;

            bool granted = m_Phase.Value == MatchPhase.Assault
                           && GetTeam(sender) == Team.Attackers
                           && m_AttackerTickets.Value > 0
                           && !m_PendingRespawns.ContainsKey(sender);

            double respawnAt = 0;
            if (granted)
            {
                m_AttackerTickets.Value--;

                float wave = Mathf.Max(1f, config.attackerRespawnWave);
                double minimum = now + 3.0;
                respawnAt = Math.Ceiling(minimum / wave) * wave;
                m_PendingRespawns[sender] = respawnAt + 5.0;
            }

            RespawnResponseRpc(granted, respawnAt, RpcTarget.Single(sender, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void RespawnResponseRpc(bool granted, double respawnAt, RpcParams rpcParams = default)
        {
            RespawnResponse?.Invoke(granted, respawnAt);
        }

        #endregion
    }
}
