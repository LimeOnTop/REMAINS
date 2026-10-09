using System.Collections.Generic;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// An equipment kit handed out by the armorer (Docs/GDD.md, section 6): weapons plus perks.
    /// </summary>
    [CreateAssetMenu(fileName = "Kit", menuName = "Remains/Loadout Kit")]
    public class LoadoutKit : ScriptableObject
    {
        [Header("Presentation")]
        public string kitName = "Штурмовик";
        [TextArea(2, 5)] public string description;
        [Tooltip("One-line summary of the kit's job, shown in the kit list.")]
        public string purpose;
        public Color color = Color.white;

        [Header("Availability")]
        public Team team = Team.Attackers;
        [Tooltip("Maximum share of the team that may use this kit (at least one player can always take it).")]
        [Range(0f, 1f)] public float maxShare = 1f;

        [Header("Weapons")]
        [Tooltip("Weapon prefab names from the player's weapon loadout that this kit may equip.")]
        public List<string> weapons = new List<string>();

        [Header("Perks")]
        public float speedMultiplier = 1f;
        public float damageTakenMultiplier = 1f;
        public float explosiveDamageTakenMultiplier = 1f;
        [Tooltip("How much this player counts when pushing a ram or cannon.")]
        public float pushWeight = 1f;
        [Tooltip("Carrying heavy objects doesn't slow this player down.")]
        public bool noCarryPenalty;
        [Tooltip("Only these players can arm powder charges.")]
        public bool canArmCharges;
        [Tooltip("Cannon reload time multiplier while this player mans a cannon (lower is faster).")]
        public float cannonReloadMultiplier = 1f;
        [Tooltip("Can repair walls and gates.")]
        public bool canRepair;
        [Tooltip("Repair speed multiplier.")]
        public float repairSpeed = 1f;

        public int GetLimit(int teamSize)
        {
            if (maxShare >= 1f) return int.MaxValue;
            return Mathf.Max(1, Mathf.FloorToInt(teamSize * maxShare));
        }
    }
}
