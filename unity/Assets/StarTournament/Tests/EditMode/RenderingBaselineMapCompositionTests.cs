using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class RenderingBaselineMapCompositionTests
    {
        [TestCase(CombatBowlCatalog.Id,8,1)][TestCase(CombatBowlCatalog.Id,8,4)]
        [TestCase(IndustrialTunnelsCatalog.Id,4,1)][TestCase(IndustrialTunnelsCatalog.Id,4,4)]
        [TestCase(LunarLaboratoryCatalog.Id,8,1)][TestCase(LunarLaboratoryCatalog.Id,8,4)]
        [TestCase(WellArenaCatalog.Id,8,1)][TestCase(WellArenaCatalog.Id,8,4)]
        public void EveryMapUsesItsSupportedHardBotRoster(string map,int participants,int views)
        {
            var c=NativeRenderingBaseline.Composition(map,views);Assert.AreEqual(participants,c.ParticipantCount);Assert.AreEqual(views,c.LocalCount);Assert.IsTrue(AuthoredArenaCatalog.Supports(map,c.ParticipantCount));
            for(int p=0;p<participants;p++){Assert.AreEqual(NativeParticipantKind.Bot,c.Participant(p).Kind);Assert.AreEqual((int)NativeBotDifficulty.Hard,c.Participant(p).Difficulty);}
            for(int s=0;s<views;s++)Assert.AreEqual(s,c.ParticipantAt(s));
        }
    }
}
