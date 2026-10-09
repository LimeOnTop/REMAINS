using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Idle life for the queen: a gentle sway, head turning towards the nearest player and panic
    /// (trembling, raised arms) while attackers are capturing her.
    /// </summary>
    public class QueenAnimator : MonoBehaviour
    {
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;

        private float m_Seed;
        private Quaternion m_HeadBase;

        private void Start()
        {
            m_Seed = Random.Range(0f, 10f);
            if (head != null) m_HeadBase = head.localRotation;
        }

        private void Update()
        {
            var objective = CaptureObjective.Instance;
            bool panic = objective != null && (objective.IsBeingCaptured || objective.IsContested);
            bool captured = objective != null && objective.IsCaptured;
            float t = Time.time + m_Seed;

            if (body != null)
            {
                float sway = panic ? Mathf.Sin(t * 30f) * 1.5f : Mathf.Sin(t * 0.8f) * 2f;
                body.localRotation = Quaternion.Euler(0f, 0f, sway);
                body.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 1.6f) * 0.01f, 1f);
            }

            if (head != null)
            {
                Quaternion target = m_HeadBase;
                var member = TeamMember.Local;
                if (member != null && Vector3.Distance(member.transform.position, head.position) < 9f)
                {
                    Vector3 toPlayer = member.transform.position + Vector3.up * 1.5f - head.position;
                    Vector3 local = head.parent.InverseTransformDirection(toPlayer);
                    local.y = Mathf.Clamp(local.y, -0.3f, 0.3f);
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
                    target = Quaternion.Euler(0f, yaw, 0f) * m_HeadBase;
                }
                if (captured) target = Quaternion.Euler(25f, 0f, 0f) * m_HeadBase;
                head.localRotation = Quaternion.Slerp(head.localRotation, target, 4f * Time.deltaTime);
            }

            // Arms: resting, fanning herself, or raised in panic.
            float leftAngle = panic ? -150f + Mathf.Sin(t * 12f) * 15f : 10f;
            float rightAngle = panic ? -150f + Mathf.Sin(t * 12f + 1f) * 15f : -35f + Mathf.Sin(t * 3f) * 12f;
            if (armLeft != null) armLeft.localRotation = Quaternion.Slerp(armLeft.localRotation, Quaternion.Euler(leftAngle, 0f, 8f), 6f * Time.deltaTime);
            if (armRight != null) armRight.localRotation = Quaternion.Slerp(armRight.localRotation, Quaternion.Euler(rightAngle, 0f, -8f), 6f * Time.deltaTime);
        }

#if UNITY_EDITOR
        public void EditorSetup(Transform queenBody, Transform queenHead, Transform left, Transform right)
        {
            body = queenBody;
            head = queenHead;
            armLeft = left;
            armRight = right;
        }
#endif
    }
}
