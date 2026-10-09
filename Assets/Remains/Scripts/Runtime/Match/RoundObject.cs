using System.Collections.Generic;
using Unity.Netcode;

namespace Remains
{
    /// <summary>
    /// Marks a dynamically spawned network object that belongs to a single round (build items, siege engines).
    /// Keeps a registry so any session owner (including a migrated one) can clean the round up.
    /// </summary>
    public class RoundObject : NetworkBehaviour
    {
        private static readonly HashSet<RoundObject> s_All = new HashSet<RoundObject>();

        public static IReadOnlyCollection<RoundObject> All => s_All;

        public override void OnNetworkSpawn()
        {
            s_All.Add(this);
        }

        public override void OnNetworkDespawn()
        {
            s_All.Remove(this);
        }

        public override void OnDestroy()
        {
            s_All.Remove(this);
            base.OnDestroy();
        }

        /// <summary>
        /// Despawns every round object. Objects owned by other clients are asked to despawn themselves.
        /// </summary>
        public static void DespawnAll()
        {
            var snapshot = new List<RoundObject>(s_All);
            foreach (var roundObject in snapshot)
            {
                roundObject.Despawn();
            }
        }

        public void Despawn()
        {
            if (!IsSpawned) return;

            if (HasAuthority)
            {
                NetworkObject.Despawn();
            }
            else
            {
                DespawnRpc();
            }
        }

        [Rpc(SendTo.Owner)]
        private void DespawnRpc()
        {
            if (IsSpawned)
            {
                NetworkObject.Despawn();
            }
        }
    }
}
