using System;
using System.IO;

namespace StarTournament.ProvingGround
{
    /// <summary>Validates the environment handoff used when macOS Player replaces its launcher process.</summary>
    public static class DevelopmentNavigationReviewHandoff
    {
        public static bool TryParse(string evidenceDirectory, string arenaFamily, out int family)
        {
            family = 0;
            bool requested = !string.IsNullOrEmpty(evidenceDirectory) || !string.IsNullOrEmpty(arenaFamily);
            if (!requested) return false;
            if (!Path.IsPathFullyQualified(evidenceDirectory) || !int.TryParse(arenaFamily, out family) ||
                family < 0 || family >= ArenaFamilyCatalog.Ids.Count)
                throw new ArgumentException("Invalid development navigation review handoff");
            return true;
        }
    }
}
