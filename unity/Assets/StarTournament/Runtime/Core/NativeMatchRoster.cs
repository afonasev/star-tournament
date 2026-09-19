using System;
using System.Linq;

namespace StarTournament.ProvingGround
{
    public enum NativeMatchMode { Ffa, Teams }
    public enum NativeTeam { None, TeamA, TeamB }

    [Serializable]
    public sealed class NativeRosterSnapshot
    {
        public NativeMatchMode Mode;
        // Array index is the stable participant identity within this session, not a local seat.
        public NativeTeam[] Teams;
    }

    /// <summary>Immutable gameplay roster. No devices, cameras or presentation identities.</summary>
    public sealed class NativeMatchRoster
    {
        // Structural product limits (GAME_SPEC §2), not balance tuning or device counts.
        public const int MinimumParticipants = 2, MaximumParticipants = 8;
        readonly NativeTeam[] teams;
        public NativeMatchMode Mode { get; }
        public int Count => teams.Length;

        public NativeMatchRoster(NativeMatchMode mode, NativeTeam[] assignments)
        {
            if (assignments == null) throw new ArgumentNullException(nameof(assignments));
            ValidateCount(assignments.Length);
            if (mode != NativeMatchMode.Ffa && mode != NativeMatchMode.Teams)
                throw new ArgumentException("Unknown match mode", nameof(mode));
            teams = (NativeTeam[])assignments.Clone();
            if (mode == NativeMatchMode.Ffa && teams.Any(t => t != NativeTeam.None))
                throw new ArgumentException("FFA participants cannot have teams");
            if (mode == NativeMatchMode.Teams &&
                (teams.Any(t => t != NativeTeam.TeamA && t != NativeTeam.TeamB) ||
                 !teams.Contains(NativeTeam.TeamA) || !teams.Contains(NativeTeam.TeamB)))
                throw new ArgumentException("Team A and Team B must both be nonempty");
            Mode = mode;
        }

        static void ValidateCount(int count)
        {
            if (count < MinimumParticipants || count > MaximumParticipants)
                throw new ArgumentOutOfRangeException(nameof(count), "Match requires 2–8 participants");
        }

        public static NativeMatchRoster Ffa(int participants)
        {
            ValidateCount(participants);
            return new NativeMatchRoster(NativeMatchMode.Ffa, new NativeTeam[participants]);
        }

        public bool AreAllies(int a, int b) => a != b && Mode == NativeMatchMode.Teams && teams[a] == teams[b];
        public NativeTeam TeamOf(int participant) => teams[participant];
        public NativeRosterSnapshot Read() => new NativeRosterSnapshot
        { Mode = Mode, Teams = (NativeTeam[])teams.Clone() };
    }
}
