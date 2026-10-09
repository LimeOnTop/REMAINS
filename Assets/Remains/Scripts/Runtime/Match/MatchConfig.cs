using UnityEngine;

namespace Remains
{
    /// <summary>
    /// Tunable match rules. See Docs/GDD.md, section 14.
    /// </summary>
    [CreateAssetMenu(fileName = "MatchConfig", menuName = "Remains/Match Config")]
    public class MatchConfig : ScriptableObject
    {
        [Header("Players")]
        public int maxPlayers = 60;
        [Tooltip("Players required before the lobby countdown starts.")]
        public int minPlayersToStart = 2;
        [Tooltip("Below this player count the small-session rules apply (more tickets, shorter assault).")]
        public int smallSessionThreshold = 16;

        [Header("Phase durations (seconds)")]
        public float lobbyCountdown = 15f;
        public float buildPhaseDuration = 60f;
        public float assaultPhaseDuration = 480f;
        public float smallSessionAssaultDuration = 300f;
        public float resultsDuration = 10f;

        [Header("Respawns")]
        [Tooltip("Attacker respawn pool = attackers at assault start x this value.")]
        public float attackerTicketsPerPlayer = 2f;
        public float smallSessionTicketsPerPlayer = 3f;
        [Tooltip("Attackers respawn together in waves of this length.")]
        public float attackerRespawnWave = 6f;
        [Tooltip("Respawn delay outside of the assault phase.")]
        public float freeRespawnDelay = 3f;

        [Header("Zones")]
        [Tooltip("Barricades can be nailed within this radius of the castle centre.")]
        public float buildZoneRadius = 30f;
        [Tooltip("Defenders cannot leave this radius around the castle during the preparation phase.")]
        public float defenderLeashRadius = 32f;

        [Header("Barricades")]
        [Tooltip("Seconds needed to nail a held piece in place.")]
        public float fixDuration = 0.6f;

        [Header("Objective")]
        [Tooltip("What the defenders guard in the throne room: the queen, the treasure chest, or a random one each round.")]
        public ObjectiveKind objectiveKind = ObjectiveKind.Random;
        [Tooltip("Seconds of uncontested presence one attacker needs to capture the objective.")]
        public float captureSeconds = 25f;
        [Tooltip("Capture speed bonus per extra attacker in the zone (capped at x2).")]
        public float captureBonusPerAttacker = 0.25f;
        [Tooltip("Progress lost per second while no attacker is in the zone.")]
        public float captureDecayPerSecond = 0.02f;
        [Tooltip("The assault is extended up to this many seconds while attackers are capturing when time runs out.")]
        public float maxOvertime = 30f;

        [Header("Damage")]
        public bool friendlyFire = false;
    }
}
