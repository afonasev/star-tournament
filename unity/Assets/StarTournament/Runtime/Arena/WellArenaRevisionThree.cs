using System.Linq;
namespace StarTournament.ProvingGround
{
    /// <summary>Frozen structure 03 with the original 44 furniture bodies, before corridor service/storage zones.</summary>
    public static class WellArenaRevisionThree
    {
        public const string Revision="3";
        public static ArenaDefinition Build()
        {
            var definition=WellArenaRevisionTwo.Build();definition.Revision=Revision;
            definition.Identity=WellArenaCatalog.Id+"@"+Revision;definition.ProfileFingerprint="authored:"+definition.Identity;
            definition.Solids=definition.Solids.Concat(WellFurniturePlan.BuildRevisionThree()).ToArray();return definition;
        }
        public static ProvingProfile AuthoringProfile()
        {
            var definition=Build();var profile=ProvingProfile.CreateCombatBowlAuthoringDefault(definition);
            profile.SetIdentityForWell(3);profile.AddWellLiftAuthoring(definition.Lifts);return profile;
        }
    }
}
