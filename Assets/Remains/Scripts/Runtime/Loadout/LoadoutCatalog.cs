using System.Collections.Generic;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// All kits available in the game. Attackers pick theirs at the armorer; defenders always get the garrison kit.
    /// </summary>
    [CreateAssetMenu(fileName = "LoadoutCatalog", menuName = "Remains/Loadout Catalog")]
    public class LoadoutCatalog : ScriptableObject
    {
        [Tooltip("Kits offered by the armorer to attackers.")]
        public List<LoadoutKit> attackerKits = new List<LoadoutKit>();
        public LoadoutKit defenderKit;

        public LoadoutKit DefaultAttackerKit => attackerKits.Count > 0 ? attackerKits[0] : null;
    }
}
