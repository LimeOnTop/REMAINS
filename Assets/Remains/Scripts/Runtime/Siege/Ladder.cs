using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// Assault ladder: once planted (nailed with RMB) the local player climbs it by walking into it
    /// while holding W. Anyone can knock a planted ladder down by grabbing it.
    /// </summary>
    [RequireComponent(typeof(Grabbable))]
    public class Ladder : MonoBehaviour
    {
        [Tooltip("Local-space climbing volume (x = width, y = length along the ladder, z = depth in front of it).")]
        [SerializeField] private Vector3 climbVolumeSize = new Vector3(1.2f, 4.2f, 1.6f);
        [SerializeField] private Vector3 climbVolumeCenter = new Vector3(0f, 2f, 0f);
        [SerializeField] private float climbSpeed = 3.2f;

        private Grabbable m_Grabbable;

        private void Awake()
        {
            m_Grabbable = GetComponent<Grabbable>();
        }

        private void Update()
        {
            if (m_Grabbable == null || !m_Grabbable.IsSpawned || !m_Grabbable.IsFixed) return;

            var member = TeamMember.Local;
            if (member == null || member.Player == null || member.Player.CoreMovement == null) return;
            if (member.Player.PlayerState == null || !member.Player.PlayerState.IsActive) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            Vector3 local = transform.InverseTransformPoint(member.transform.position + Vector3.up * 0.9f);
            Vector3 half = climbVolumeSize * 0.5f;
            Vector3 delta = local - climbVolumeCenter;
            bool inside = Mathf.Abs(delta.x) <= half.x && Mathf.Abs(delta.y) <= half.y && Mathf.Abs(delta.z) <= half.z;
            if (!inside) return;

            if (keyboard.wKey.isPressed)
            {
                member.Player.CoreMovement.SetVerticalVelocity(climbSpeed);
            }
            else if (keyboard.sKey.isPressed)
            {
                member.Player.CoreMovement.SetVerticalVelocity(-climbSpeed);
            }
            else if (!member.Player.CoreMovement.IsGrounded)
            {
                // Hang on the ladder.
                member.Player.CoreMovement.SetVerticalVelocity(0f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.4f);
            Gizmos.DrawWireCube(climbVolumeCenter, climbVolumeSize);
        }
    }
}
