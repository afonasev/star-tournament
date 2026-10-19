using System.IO;

namespace StarTournament.ProvingGround
{
    // Changed formats publish to a new namespace. Previous players keep their own files.
    public static class VersionedUserData
    {
        public const string Profiles="player-profiles-v2.json";
        public const string LegacyProfiles="player-profiles-v1.json";
        public const string LabHistory="design-lab-history-v3.json";
        public const string LegacyLabHistory="design-lab-history-v2.json";

        public static string ReadPath(string destination,string legacyPath)
        {
            if(Directory.Exists(destination))throw new IOException("User data destination is a directory");
            return !File.Exists(destination)&&File.Exists(legacyPath)?legacyPath:destination;
        }
    }
}
