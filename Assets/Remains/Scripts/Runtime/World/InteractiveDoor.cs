using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// A hinged door anyone can open or close with E. The state is replicated; the leaf swings smoothly.
    /// </summary>
    public class InteractiveDoor : NetworkBehaviour
    {
        [SerializeField] private Transform hinge;
        [SerializeField] private float openAngle = 100f;
        [SerializeField] private float speed = 180f;
        [SerializeField] private float reach = 3f;
        [SerializeField] private string displayName = "Дверь";

        private readonly NetworkVariable<bool> m_Open = new NetworkVariable<bool>(false);
        private float m_Angle;

        public bool IsOpen => m_Open.Value;

        private void Update()
        {
            if (hinge != null)
            {
                float target = m_Open.Value ? openAngle : 0f;
                m_Angle = Mathf.MoveTowards(m_Angle, target, speed * Time.deltaTime);
                hinge.localRotation = Quaternion.Euler(0f, m_Angle, 0f);
            }

            if (!IsSpawned || LoadoutMenu.IsOpen) return;
            var member = TeamMember.Local;
            var camera = Camera.main;
            if (member == null || camera == null || member.Player == null || member.Player.PlayerState == null || !member.Player.PlayerState.IsActive) return;
            if (GrabController.Local != null && GrabController.Local.Held != null) return;

            if (!Physics.Raycast(camera.transform.position, camera.transform.forward, out var hit, reach + 1.5f, ~(1 << BrickDebris.DebrisLayer), QueryTriggerInteraction.Ignore)) return;
            if (hit.collider.GetComponentInParent<InteractiveDoor>() != this) return;
            if (Vector3.Distance(member.transform.position, hit.point) > reach) return;

            HudHint.Set(m_Open.Value ? $"E — закрыть · {displayName}" : $"E — открыть · {displayName}", 0f, 1);
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                ToggleRpc();
            }
        }

        [Rpc(SendTo.Owner)]
        private void ToggleRpc()
        {
            m_Open.Value = !m_Open.Value;
        }

        /// <summary>Authority: closes the door (new round).</summary>
        public void ResetState()
        {
            if (HasAuthority) m_Open.Value = false;
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform doorHinge, string name, float angle)
        {
            hinge = doorHinge;
            displayName = name;
            openAngle = angle;
        }
#endif
    }
}
