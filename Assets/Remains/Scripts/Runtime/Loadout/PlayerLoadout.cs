using System;
using System.Collections.Generic;
using Blocks.Gameplay.Core;
using Blocks.Gameplay.Shooter;
using Unity.Netcode;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// The kit a player is currently using. Attackers choose one at the armorer; defenders always use the
    /// garrison kit. The owner writes the choice; every client reads it (damage multipliers, kit limits).
    /// </summary>
    public class PlayerLoadout : NetworkBehaviour, IPlayerAddon
    {
        private static readonly List<PlayerLoadout> s_All = new List<PlayerLoadout>();

        public static PlayerLoadout Local { get; private set; }
        public static IReadOnlyList<PlayerLoadout> All => s_All;

        /// <summary>Raised on the owner when the effective kit changes.</summary>
        public static event Action<LoadoutKit> LocalKitChanged;

        [SerializeField] private LoadoutCatalog catalog;

        private readonly NetworkVariable<int> m_AttackerKitIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private CorePlayerManager m_Player;
        private LoadoutKit m_AppliedKit;

        public LoadoutCatalog Catalog => catalog;

        private Team CurrentTeam => MatchManager.Instance != null && IsSpawned ? MatchManager.Instance.GetTeam(OwnerClientId) : Team.None;
        public int AttackerKitIndex => m_AttackerKitIndex.Value;

        /// <summary>The kit in effect, resolved from the team and the attacker choice.</summary>
        public LoadoutKit Kit
        {
            get
            {
                if (catalog == null) return null;
                // In the lobby warm-up everybody may try the attacker kits.
                bool lobby = MatchManager.Instance != null && MatchManager.Instance.Phase == MatchPhase.Lobby;
                if (CurrentTeam == Team.Defenders && !lobby) return catalog.defenderKit;

                int index = m_AttackerKitIndex.Value;
                return index >= 0 && index < catalog.attackerKits.Count ? catalog.attackerKits[index] : catalog.DefaultAttackerKit;
            }
        }

        public static PlayerLoadout For(ulong clientId)
        {
            foreach (var loadout in s_All)
            {
                if (loadout.IsSpawned && loadout.OwnerClientId == clientId) return loadout;
            }
            return null;
        }

        #region IPlayerAddon

        public void Initialize(CorePlayerManager playerManager)
        {
            m_Player = playerManager;
        }

        public void OnPlayerSpawn()
        {
            s_All.Add(this);
            DamageRules.DamageTakenMultiplier ??= ResolveDamageTakenMultiplier;

            if (IsOwner)
            {
                Local = this;
                m_AttackerKitIndex.OnValueChanged += HandleKitIndexChanged;
                MatchManager.TeamsChanged += ApplyKit;
                MatchManager.PhaseChanged += HandlePhaseChanged;
                ApplyKit();
            }
        }

        public void OnPlayerDespawn()
        {
            s_All.Remove(this);
            if (IsOwner)
            {
                m_AttackerKitIndex.OnValueChanged -= HandleKitIndexChanged;
                MatchManager.TeamsChanged -= ApplyKit;
                MatchManager.PhaseChanged -= HandlePhaseChanged;
                if (Local == this) Local = null;
            }
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
            if (IsOwner && newState != PlayerLifeState.Eliminated) ApplyKit();
        }

        #endregion

        private static float ResolveDamageTakenMultiplier(ulong victimId, DamageType type)
        {
            var kit = For(victimId)?.Kit;
            if (kit == null) return 1f;
            return type == DamageType.Explosion ? kit.damageTakenMultiplier * kit.explosiveDamageTakenMultiplier : kit.damageTakenMultiplier;
        }

        private void HandleKitIndexChanged(int previous, int current) => ApplyKit();
        private void HandlePhaseChanged(MatchPhase previous, MatchPhase current) => ApplyKit();

        /// <summary>Owner: pushes the kit's weapons and perks to the player's components.</summary>
        public void ApplyKit()
        {
            if (!IsOwner) return;
            var kit = Kit;
            if (kit == null) return;

            var weapons = GetComponent<WeaponController>();
            if (weapons != null) weapons.SetAllowedWeapons(kit.weapons);

            if (GrabController.Local != null) GrabController.Local.RefreshSpeed();

            if (m_AppliedKit != kit)
            {
                m_AppliedKit = kit;
                LocalKitChanged?.Invoke(kit);
            }
        }

        /// <summary>Number of teammates (including this player) currently using <paramref name="kit"/>.</summary>
        public static int CountUsers(LoadoutKit kit, ulong excludeClientId = ulong.MaxValue)
        {
            int count = 0;
            foreach (var loadout in s_All)
            {
                if (loadout.IsSpawned && loadout.OwnerClientId != excludeClientId && loadout.Kit == kit) count++;
            }
            return count;
        }

        /// <summary>Owner: tries to switch to an attacker kit. Returns an error message or null on success.</summary>
        public string TrySelectKit(int index)
        {
            if (!IsOwner || catalog == null) return "Недоступно";
            if (index < 0 || index >= catalog.attackerKits.Count) return "Нет такого комплекта";
            var match = MatchManager.Instance;
            bool lobby = match != null && match.Phase == MatchPhase.Lobby;
            if (CurrentTeam != Team.Attackers && !lobby) return "Оружейник снаряжает только атакующих";

            var kit = catalog.attackerKits[index];
            int teamSize = match != null ? Mathf.Max(1, match.AttackersTotal) : 1;
            int users = CountUsers(kit, OwnerClientId);
            if (users >= kit.GetLimit(teamSize)) return $"Лимит комплекта «{kit.kitName}» исчерпан ({users}/{kit.GetLimit(teamSize)})";

            m_AttackerKitIndex.Value = index;
            ApplyKit();
            return null;
        }
    }
}
