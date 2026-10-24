using System.Linq;
namespace StarTournament.ProvingGround
{
    /// <summary>Frozen corridor layout before the two second-floor weapon pickups.</summary>
    public static class WellArenaRevisionFour
    {
        public const string Revision="4";
        public static ArenaDefinition Build()
        {
            var definition=WellArenaRevisionTwo.Build();definition.Revision=Revision;
            definition.Identity=WellArenaCatalog.Id+"@"+Revision;definition.ProfileFingerprint="authored:"+definition.Identity;
            definition.Solids=definition.Solids.Concat(WellFurniturePlan.BuildRevisionFour()).ToArray();return definition;
        }
        public static ProvingProfile AuthoringProfile()
        {
            var definition=Build();var profile=ProvingProfile.CreateCombatBowlAuthoringDefault(definition);
            profile.SetIdentityForWell(4);profile.AddWellLiftAuthoring(definition.Lifts);return profile;
        }
    }
}
