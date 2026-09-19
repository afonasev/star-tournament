namespace StarTournament.ProvingGround
{
    public sealed partial class ProvingProfile
    {
        public static ProvingProfile CreateTeamDefault()
        {
            var p=new ProvingProfile { id="unity-native-team-v1", version=1 };
            // Existing prototype-v1 medium reference; independent of immutable combat@1.
            p.Add("spawn.initialOpponentSeparation", "spawn", "Разделение команд на старте", "Минимальная дистанция между initial spawn противников; союзники могут стоять рядом.", "meters", 1, 8, .5f, 5);
            return p;
        }
    }
}
