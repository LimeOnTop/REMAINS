using Unity.Netcode;
using UnityEngine;

namespace Remains
{
    /// <summary>
    /// A cannonball lying around the camp. Carry it (E) to an unloaded cannon's muzzle to load it.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class CannonballAmmo : NetworkBehaviour
    {
        private float m_CheckTimer;

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;

            m_CheckTimer -= Time.deltaTime;
            if (m_CheckTimer > 0f) return;
            m_CheckTimer = 0.1f;

            foreach (var siege in PushableSiege.All)
            {
                if (siege is not SiegeCannon cannon || cannon.IsLoaded) continue;
                if (Vector3.Distance(transform.position, cannon.LoadPoint) > cannon.LoadRange) continue;

                cannon.Load();
                NetworkObject.Despawn();
                return;
            }
        }
    }
}
