using System;

namespace StarTournament.ProvingGround
{
    /// <summary>Stable admission contract for the authored Combat Bowl, independent of its UI.</summary>
    public static class CombatBowlRules
    {
        public const int MinimumParticipants=2, MaximumParticipants=8;
        public static bool SupportsParticipantCount(int count) => count>=MinimumParticipants&&count<=MaximumParticipants;
        public static void RequireParticipantCount(int count)
        {
            if(!SupportsParticipantCount(count))
                throw new ArgumentException("COMBAT_BOWL_ROSTER_LIMIT|Combat Bowl supports 2–8 participants; adjust the roster.");
        }
    }
}
