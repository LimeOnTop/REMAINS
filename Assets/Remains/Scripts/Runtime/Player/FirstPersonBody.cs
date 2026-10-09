using Blocks.Gameplay.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace Remains
{
    /// <summary>
    /// First-person presentation for the local player: the own body only casts shadows so it never blocks
    /// the camera, while other players see the full model.
    /// </summary>
    public class FirstPersonBody : NetworkBehaviour, IPlayerAddon
    {
        [Tooltip("Renderers hidden from the owner's camera. If empty, all skinned mesh renderers are used.")]
        [SerializeField] private Renderer[] bodyRenderers;

        public void Initialize(CorePlayerManager playerManager)
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
            {
                bodyRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }
        }

        public void OnPlayerSpawn()
        {
            if (!IsOwner) return;

            foreach (var bodyRenderer in bodyRenderers)
            {
                if (bodyRenderer != null) bodyRenderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }

        public void OnPlayerDespawn()
        {
        }

        public void OnLifeStateChanged(PlayerLifeState previousState, PlayerLifeState newState)
        {
        }
    }
}
