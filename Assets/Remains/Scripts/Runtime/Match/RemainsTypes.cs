using UnityEngine;

namespace Remains
{
    public enum Team : byte
    {
        None = 0,
        Defenders = 1,
        Attackers = 2
    }

    public enum MatchPhase : byte
    {
        Lobby = 0,
        Build = 1,
        Assault = 2,
        Results = 3
    }

    /// <summary>What the defenders guard in the throne room.</summary>
    public enum ObjectiveKind : byte
    {
        Queen = 0,
        Treasure = 1,
        /// <summary>Config only: pick one at random every round.</summary>
        Random = 2
    }

    /// <summary>Why the last round ended.</summary>
    public enum RoundEndReason : byte
    {
        None = 0,
        DefendersEliminated = 1,
        ObjectiveCaptured = 2,
        TimeUp = 3,
        AttackersExhausted = 4
    }

    public static class TeamInfo
    {
        public static readonly Color DefenderColor = new Color32(0x3B, 0x7B, 0xD9, 0xFF);
        public static readonly Color AttackerColor = new Color32(0xE8, 0x79, 0x2B, 0xFF);
        public static readonly Color NeutralColor = new Color32(0xB0, 0xB0, 0xB0, 0xFF);

        public static Color GetColor(Team team)
        {
            switch (team)
            {
                case Team.Defenders: return DefenderColor;
                case Team.Attackers: return AttackerColor;
                default: return NeutralColor;
            }
        }

        public static string GetName(Team team)
        {
            switch (team)
            {
                case Team.Defenders: return "ЗАЩИТА";
                case Team.Attackers: return "АТАКА";
                default: return "НАБЛЮДАТЕЛЬ";
            }
        }

        public static string GetPhaseName(MatchPhase phase)
        {
            switch (phase)
            {
                case MatchPhase.Lobby: return "ОЖИДАНИЕ ИГРОКОВ";
                case MatchPhase.Build: return "ПОДГОТОВКА";
                case MatchPhase.Assault: return "ШТУРМ";
                case MatchPhase.Results: return "ИТОГИ";
                default: return phase.ToString();
            }
        }
    }
}
