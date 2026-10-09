using System;
using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Player addon that applies the match rules to one player: team colour, spawning at the team's area,
    /// staying inside the allowed zone during the build phase and respawning (free outside the assault,
    /// from the attackers' shared pool during it, never for defenders). See Docs/GDD.md, section 3.
    /// </summary>
    public class TeamMember : NetworkBehaviour, IPlayerAddon
    {
        private static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int k_Color = Shader.PropertyToID("_Color");

        /// <summary>The local player's instance.</summary>
        public static TeamMember Local { get; private set; }

        /// <summary>Raised when the local player's status line changes (respawn countdown, warnings...).</summary>
        public static event Action<string> LocalStatusChanged;

        [Tooltip("Renderers tinted with the team colour. If empty, all skinned mesh renderers are used.")]
        [SerializeField] private Renderer[] tintRenderers;
        [Range(0f, 1f)]
        [SerializeField] private float tintStrength = 0.75f;

        private CorePlayerManager m_Player;
        private Team m_Team = Team.None;
        private double m_RespawnAt = -1;
        private bool m_WaitingForResponse;
        private float m_PlacementDelay = -1f;
        private float m_LeashTimer;
        private string m_Status = string.Empty;
        private MaterialPropertyBlock m_PropertyBlock;
        private Color[] m_OriginalColors;

        public Team Team => m_Team;
        public string Status => m_Status;
        public CorePlayerManager Player => m_Player;

        #region IPlayerAddon

        public void Initialize(CorePlayerManager playerManager)
        {
            m_Player = playerManager;
            if (tintRenderers == null || tintRenderers.Length == 0)
            {
                tintRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
        }

        public void OnPlayerSpawn()
        {
            MatchManager.TeamsChanged += RefreshTeam;
            MatchManager.PhaseChanged += HandlePhaseChanged;
            RefreshTeam();

            if (IsOwner)
            {
                Local = this;
                MatchManager.RespawnResponse += HandleRespawnResponse;

                // Late joiners: move to the team area once the template's initial spawn has run.
                var match = MatchManager.Instance;
                if (match != null && match.Phase != MatchPhase.Lobby)
                {
                    m_PlacementDelay = 0.5f;
                }
            }
        }

        public void OnPlayerDespawn()
        {
            MatchManager.TeamsChanged -= RefreshTeam;
            MatchManager.PhaseChanged -= HandlePhaseChanged;

            if (IsOwner)
            {
                MatchManager.RespawnResponse -= HandleRespawnResponse;
                if (Local == this) Local = null;
            }
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (!IsOwner) return;

            if (newState == PlayerLifeState.Eliminated)
            {
                HandleLocalDeath();
            }
            else if (newState == PlayerLifeState.Respawned)
            {
                SetStatus(string.Empty);
            }
        }

        #endregion

        #region Team

        private void RefreshTeam()
        {
            var match = MatchManager.Instance;
            m_Team = match != null ? match.GetTeam(OwnerClientId) : Team.None;
            ApplyTint(TeamInfo.GetColor(m_Team));
        }

        private void ApplyTint(Color teamColor)
        {
            if (tintRenderers == null) return;
            m_PropertyBlock ??= new MaterialPropertyBlock();

            if (m_OriginalColors == null)
            {
                m_OriginalColors = new Color[tintRenderers.Length];
                for (int i = 0; i < tintRenderers.Length; i++)
                {
                    var material = tintRenderers[i] != null ? tintRenderers[i].sharedMaterial : null;
                    m_OriginalColors[i] = material != null && material.HasProperty(k_BaseColor) ? material.GetColor(k_BaseColor) : Color.white;
                }
            }

            for (int i = 0; i < tintRenderers.Length; i++)
            {
                var renderer = tintRenderers[i];
                if (renderer == null) continue;

                Color color = m_Team == Team.None ? m_OriginalColors[i] : Color.Lerp(m_OriginalColors[i], teamColor, tintStrength);
                renderer.GetPropertyBlock(m_PropertyBlock);
                m_PropertyBlock.SetColor(k_BaseColor, color);
                m_PropertyBlock.SetColor(k_Color, color);
                renderer.SetPropertyBlock(m_PropertyBlock);
            }
        }

        #endregion

        #region Owner logic

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            var match = MatchManager.Instance;
            if (match == null) return;

            if (m_PlacementDelay >= 0f)
            {
                m_PlacementDelay -= Time.deltaTime;
                if (m_PlacementDelay < 0f)
                {
                    if (match.Phase == MatchPhase.Assault && m_Team == Team.Attackers)
                    {
                        Respawn(match);
                    }
                    else if (match.Phase == MatchPhase.Assault)
                    {
                        // A defender joining mid-assault would be a free extra life; spectate instead.
                        m_Player.PlayerState.SetLifeState(PlayerLifeState.Eliminated);
                    }
                    else
                    {
                        Respawn(match);
                    }
                }
            }

            if (m_RespawnAt >= 0)
            {
                double remaining = m_RespawnAt - match.Now;
                if (remaining <= 0)
                {
                    m_RespawnAt = -1;
                    Respawn(match);
                }
                else
                {
                    SetStatus($"ВОЗРОЖДЕНИЕ ЧЕРЕЗ {Mathf.CeilToInt((float)remaining)}");
                }
            }

            if (match.Phase == MatchPhase.Build)
            {
                m_LeashTimer -= Time.deltaTime;
                if (m_LeashTimer <= 0f)
                {
                    m_LeashTimer = 0.2f;
                    EnforceBuildPhaseZone(match);
                }
            }
        }

        private void HandlePhaseChanged(MatchPhase previous, MatchPhase current)
        {
            if (!IsOwner || previous == current) return;

            var match = MatchManager.Instance;
            if (match == null) return;

            if (current == MatchPhase.Build)
            {
                // New round: everybody starts alive in their team's area.
                m_RespawnAt = -1;
                m_WaitingForResponse = false;
                RefreshTeam();
                Respawn(match);
            }
            else if (current == MatchPhase.Results)
            {
                m_RespawnAt = -1;
                SetStatus(string.Empty);
            }
        }

        private void HandleLocalDeath()
        {
            var match = MatchManager.Instance;
            if (match == null) return;

            if (match.Phase != MatchPhase.Assault)
            {
                m_RespawnAt = match.Now + match.Config.freeRespawnDelay;
                return;
            }

            if (m_Team == Team.Attackers)
            {
                m_WaitingForResponse = true;
                SetStatus("ЗАПРОС ВОЗРОЖДЕНИЯ...");
                match.RequestRespawnRpc();
            }
            else
            {
                SetStatus("ВЫ ПОГИБЛИ — ЖДИТЕ СЛЕДУЮЩЕГО РАУНДА");
            }
        }

        private void HandleRespawnResponse(bool granted, double respawnAt)
        {
            if (!m_WaitingForResponse) return;
            m_WaitingForResponse = false;

            if (granted)
            {
                m_RespawnAt = respawnAt;
            }
            else
            {
                SetStatus("ПУЛ ВОЗРОЖДЕНИЙ ИСЧЕРПАН");
            }
        }

        private void Respawn(MatchManager match)
        {
            var map = RemainsMap.Instance;
            if (map == null || m_Player == null) return;

            Vector3 position = map.GetSpawnPoint(m_Team, match.Phase);
            var movement = m_Player.CoreMovement;
            if (movement != null)
            {
                movement.transform.rotation = map.GetSpawnRotation(position);
                movement.SetPosition(position);
                movement.ResetMovementForces();
            }

            var stats = m_Player.CoreStats;
            if (stats != null)
            {
                float missing = stats.GetMaxValue(StatKeys.Health) - stats.GetCurrentValue(StatKeys.Health);
                if (missing > 0f)
                {
                    stats.ModifyStat(StatKeys.Health, missing, OwnerClientId, ModificationSource.Regeneration);
                }
            }

            var state = m_Player.PlayerState;
            if (state != null && state.LifeState == PlayerLifeState.Eliminated)
            {
                state.SetLifeState(PlayerLifeState.Respawned);
            }

            SetStatus(string.Empty);
        }

        private void EnforceBuildPhaseZone(MatchManager match)
        {
            var map = RemainsMap.Instance;
            if (map == null || m_Player == null || m_Player.PlayerState == null || !m_Player.PlayerState.IsActive) return;

            Vector3 center;
            float radius;
            if (m_Team == Team.Attackers && map.attackerBase != null)
            {
                center = map.attackerBase.position;
                radius = map.attackerBaseRadius;
            }
            else if (m_Team == Team.Defenders)
            {
                center = map.FortressPosition;
                radius = match.Config.defenderLeashRadius;
            }
            else
            {
                return;
            }

            Vector3 offset = transform.position - center;
            offset.y = 0f;
            if (offset.magnitude <= radius) return;

            Vector3 inside = center + offset.normalized * (radius - 1.5f);
            if (Physics.Raycast(inside + Vector3.up * 50f, Vector3.down, out var hit, 100f, map.groundMask, QueryTriggerInteraction.Ignore))
            {
                inside = hit.point + Vector3.up * 0.1f;
            }

            m_Player.CoreMovement.SetPosition(inside);
            m_Player.CoreMovement.ResetMovementForces();
            SetStatus(m_Team == Team.Attackers ? "ДОЖДИТЕСЬ НАЧАЛА ШТУРМА" : "НЕ УХОДИТЕ ДАЛЕКО ОТ КРЕПОСТИ");
            CancelInvoke(nameof(ClearStatus));
            Invoke(nameof(ClearStatus), 2f);
        }

        private void ClearStatus()
        {
            if (m_RespawnAt < 0 && !m_WaitingForResponse) SetStatus(string.Empty);
        }

        private void SetStatus(string status)
        {
            if (m_Status == status) return;
            m_Status = status;
            LocalStatusChanged?.Invoke(status);
        }

        #endregion
    }
}
