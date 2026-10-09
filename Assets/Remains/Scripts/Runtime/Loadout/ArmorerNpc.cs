using UnityEngine;
using UnityEngine.InputSystem;

namespace Remains
{
    /// <summary>
    /// The armorer in each attacker tent. Hammers at his anvil, turns to face players who come close and,
    /// when talked to (E), opens the kit selection menu.
    /// </summary>
    public class ArmorerNpc : MonoBehaviour
    {
        [SerializeField] private string npcName = "Оружейник Бранд";
        [SerializeField] private Transform body;
        [SerializeField] private Transform hammerArm;
        [SerializeField] private Transform anvilTop;
        [SerializeField] private Material sparkMaterial;
        [SerializeField] private float talkRange = 3.2f;

        private float m_BaseYaw;
        private float m_HammerTime;
        private bool m_Struck;

        private void Start()
        {
            if (body != null) m_BaseYaw = body.localEulerAngles.y;
            m_HammerTime = Random.Range(0f, 2f);
        }

        private void Update()
        {
            var member = TeamMember.Local;
            bool playerNear = member != null && Vector3.Distance(member.transform.position, transform.position) < 6f;

            AnimateBody(member, playerNear);
            AnimateHammer(playerNear);
            HandleInteraction(member);
        }

        private void AnimateBody(TeamMember member, bool playerNear)
        {
            if (body == null) return;

            float targetYaw = m_BaseYaw;
            if (playerNear)
            {
                Vector3 toPlayer = member.transform.position - body.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.01f)
                {
                    float worldYaw = Quaternion.LookRotation(toPlayer).eulerAngles.y;
                    float parentYaw = body.parent != null ? body.parent.eulerAngles.y : 0f;
                    targetYaw = worldYaw - parentYaw;
                }
            }

            float yaw = Mathf.MoveTowardsAngle(body.localEulerAngles.y, targetYaw, 140f * Time.deltaTime);
            float breathe = 1f + Mathf.Sin(Time.time * 2f) * 0.012f;
            body.localRotation = Quaternion.Euler(0f, yaw, 0f);
            body.localScale = new Vector3(1f, breathe, 1f);
        }

        private void AnimateHammer(bool playerNear)
        {
            if (hammerArm == null) return;

            if (playerNear)
            {
                // Rest the hammer on the shoulder while talking.
                hammerArm.localRotation = Quaternion.Slerp(hammerArm.localRotation, Quaternion.Euler(-150f, 0f, 0f), 6f * Time.deltaTime);
                return;
            }

            m_HammerTime += Time.deltaTime * 1.4f;
            float cycle = Mathf.Repeat(m_HammerTime, 1f);
            // Slow lift, fast strike.
            float angle = cycle < 0.7f ? Mathf.Lerp(-10f, -120f, cycle / 0.7f) : Mathf.Lerp(-120f, -10f, (cycle - 0.7f) / 0.3f);
            hammerArm.localRotation = Quaternion.Euler(angle, 0f, 0f);

            if (cycle > 0.97f && !m_Struck)
            {
                m_Struck = true;
                Sparks();
            }
            else if (cycle < 0.5f)
            {
                m_Struck = false;
            }
        }

        private void Sparks()
        {
            if (anvilTop == null || sparkMaterial == null) return;
            var camera = Camera.main;
            if (camera != null && Vector3.Distance(camera.transform.position, anvilTop.position) > 25f) return;

            for (int i = 0; i < 5; i++)
            {
                var spark = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(spark.GetComponent<Collider>());
                spark.layer = 2;
                spark.transform.position = anvilTop.position + Random.insideUnitSphere * 0.05f;
                spark.transform.localScale = Vector3.one * 0.04f;
                spark.GetComponent<Renderer>().sharedMaterial = sparkMaterial;
                var bodyComponent = spark.AddComponent<Rigidbody>();
                bodyComponent.mass = 0.05f;
                bodyComponent.linearVelocity = new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(1.5f, 3f), Random.Range(-1.5f, 1.5f));
                Destroy(spark, 0.6f);
            }
        }

        private void HandleInteraction(TeamMember member)
        {
            if (member == null || LoadoutMenu.IsOpen || LoadoutMenu.Instance == null) return;
            if (member.Player == null || member.Player.PlayerState == null || !member.Player.PlayerState.IsActive) return;
            if (GrabController.Local != null && GrabController.Local.Held != null) return;
            if (Vector3.Distance(member.transform.position, transform.position) > talkRange) return;

            var camera = Camera.main;
            if (camera == null) return;
            Vector3 toNpc = transform.position + Vector3.up - camera.transform.position;
            if (Vector3.Angle(camera.transform.forward, toNpc) > 35f) return;

            var match = MatchManager.Instance;
            bool attacker = match == null || match.LocalTeam == Team.Attackers || match.Phase == MatchPhase.Lobby;
            if (!attacker)
            {
                HudHint.Set($"{npcName}: «Вражеским не продаю!»", 0f, 2);
                return;
            }

            HudHint.Set($"E — поговорить · {npcName} (выбор снаряжения)", 0f, 2);
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                LoadoutMenu.Instance.Open(npcName);
            }
        }

#if UNITY_EDITOR
        public void EditorSetup(string displayName, Transform npcBody, Transform arm, Transform anvil, Material sparks)
        {
            npcName = displayName;
            body = npcBody;
            hammerArm = arm;
            anvilTop = anvil;
            sparkMaterial = sparks;
        }
#endif
    }
}
