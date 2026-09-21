using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ArenaFoundationTests
    {
        static ArenaDefinition Generate(uint seed=17,int family=0) => ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=seed,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=ProvingProfile.CreateArenaFamiliesDefault(family)});
        static ArenaFreezeSnapshot Freeze(uint seed=17,int family=0) => ArenaGenerator.Freeze(new ArenaGenerationRequest{Seed=seed,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=ProvingProfile.CreateArenaFamiliesDefault(family)});
        [Test] public void SameRequest_HasStableIdentityAndIndependentCopy()
        {
            var left=Generate();var right=Generate();Assert.That(left.Identity,Is.EqualTo(right.Identity));Assert.That(left.Solids.Length,Is.EqualTo(right.Solids.Length));
            var copy=left.Copy();copy.Solids[0].Id="changed";Assert.That(left.Solids[0].Id,Is.Not.EqualTo("changed"));
        }
        [Test] public void ChangedSeedFamilyOrFrozenProfileValue_ChangesIdentity()
        {
            Assert.That(Generate(17).Identity,Is.Not.EqualTo(Generate(18).Identity));Assert.That(Generate(17,0).Identity,Is.Not.EqualTo(Generate(17,1).Identity));var p=ProvingProfile.CreateArenaFamiliesDefault();p.Set("fixture.width",33);var changed=ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=17,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=p});Assert.That(changed.Identity,Is.Not.EqualTo(Generate(17).Identity));
        }
        [Test] public void EveryFamily_FreezeRepeatsIdentity_AndRejectsDefinitionOrProfileDrift()
        {
            for(int family=0;family<ArenaFamilyCatalog.Ids.Count;family++)
            {
                var left=Freeze(91,family);var right=Freeze(91,family);Assert.That(left.Identity,Is.EqualTo(right.Identity));Assert.That(left.FamilyId,Is.EqualTo(ArenaFamilyCatalog.Ids[family]));
                var drift=left.Definition;drift.Solids[0].Id+="-drift";
                var driftError=Assert.Throws<System.ArgumentException>(()=>left.RequireDefinition(drift));Assert.That(driftError.Message,Does.StartWith("ARENA_FREEZE_DEFINITION_MISMATCH|family:"+left.FamilyId));
            }
            var profile=ProvingProfile.CreateArenaFamiliesDefault();var definition=Generate();profile.Set("fixture.width",33);
            var error=Assert.Throws<System.ArgumentException>(()=>ArenaFreezeSnapshot.Create(definition,profile));Assert.That(error.Message,Does.StartWith("ARENA_FREEZE_PROFILE_MISMATCH|family:"+definition.FamilyId));
        }
        [Test] public void EveryRegisteredFamily_HasDistinctCompleteDefinition_AndSeedDoesNotChooseFamily()
        {
            var identities=new System.Collections.Generic.HashSet<string>();
            for(int family=0;family<ArenaFamilyCatalog.Ids.Count;family++)
            {
                var d=Generate(17,family);Assert.That(d.FamilyId,Is.EqualTo(ArenaFamilyCatalog.Ids[family]));Assert.That(d.Solids,Has.Length.GreaterThan(8));Assert.That(d.SpawnRegions,Has.Length.EqualTo(2));Assert.That(d.Transitions,Has.Length.EqualTo(2));Assert.That(d.Transitions[0].OrderedFeet.Length,Is.GreaterThan(2));Assert.That(identities.Add(d.Identity),Is.True);
                Assert.That(Generate(999,family).FamilyId,Is.EqualTo(d.FamilyId));
            }
        }
        [Test] public void FamilyProfile_MapsStableOrdinals_AndRejectsUnknownValue()
        {
            for(int i=0;i<ArenaFamilyCatalog.Ids.Count;i++)Assert.That(ArenaFamilyCatalog.Resolve(i),Is.EqualTo(ArenaFamilyCatalog.Ids[i]));
            var invalid=ProvingProfile.CreateArenaFamiliesDefault();invalid.Set("arena.family",5);Assert.Throws<System.ArgumentException>(()=>ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=1,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=invalid}));
        }
        [Test] public void UnsupportedVersion_IsRejectedBeforeSceneCreation()
        {
            Assert.Throws<System.ArgumentException>(()=>ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=1,GeneratorVersion="unknown",Profile=ProvingProfile.CreateArenaFamiliesDefault()}));
        }
        [Test] public void ArenaFourValidator_AcceptsEveryRegisteredCanonicalFamily()
        {
            for(int family=0;family<ArenaFamilyCatalog.Ids.Count;family++)
            {
                var definition=Generate(81,family);var result=ArenaDefinitionValidator.Validate(definition,ProvingProfile.CreateArenaFamiliesDefault(family));
                Assert.That(result.IsValid,Is.True,result.ToString());
            }
        }
        [TestCase("solid")] [TestCase("layer")] [TestCase("spawn")] [TestCase("armor")] [TestCase("region")] [TestCase("transition")]
        public void ArenaFourValidator_RejectsMutatedCanonicalDataWithElementDiagnostic(string mutation)
        {
            var definition=Generate().Copy(); string expected;
            switch(mutation)
            {
                case "solid": definition.Solids[0].Size.x=0; expected="ARENA_INVALID_SOLID_GEOMETRY"; break;
                case "layer": definition.Solids[0].Layer=0; expected="ARENA_INVALID_COLLISION_LAYER"; break;
                case "spawn": definition.Spawns[0].y=99; expected="ARENA_INVALID_SPAWN"; break;
                case "armor": definition.ArmorPickupAnchor.y=99; expected="ARENA_INVALID_ARMOR_PICKUP_ANCHOR"; break;
                case "region": definition.SpawnRegions[0].Support="missing"; expected="ARENA_UNKNOWN_SPAWN_SUPPORT"; break;
                default: definition.Transitions[0].OrderedFeet[0].y=99; expected="ARENA_INVALID_TRANSITION_ENDPOINT"; break;
            }
            var result=ArenaDefinitionValidator.Validate(definition,ProvingProfile.CreateArenaFamiliesDefault());
            Assert.That(result.IsValid,Is.False);Assert.That(result.Code,Is.EqualTo(expected));Assert.That(result.FamilyId,Is.EqualTo(definition.FamilyId));Assert.That(result.ElementId,Is.Not.Empty);
        }
        [Test] public void ArenaFourValidator_RejectsDuplicateIdsAndBrokenTransitionOrder()
        {
            var duplicate=Generate().Copy();duplicate.Solids[1].Id=duplicate.Solids[0].Id;
            Assert.That(ArenaDefinitionValidator.Validate(duplicate,ProvingProfile.CreateArenaFamiliesDefault()).Code,Is.EqualTo("ARENA_DUPLICATE_SOLID_ID"));
            var broken=Generate().Copy();var feet=broken.Transitions[0].OrderedFeet;feet[1]=feet[0];
            Assert.That(ArenaDefinitionValidator.Validate(broken,ProvingProfile.CreateArenaFamiliesDefault()).Code,Is.EqualTo("ARENA_DISCONTINUOUS_TRANSITION"));
        }
    }
}
