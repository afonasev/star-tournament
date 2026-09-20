using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ArenaFoundationTests
    {
        static ArenaDefinition Generate(uint seed=17,int family=0) => ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=seed,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=ProvingProfile.CreateArenaFamiliesDefault(family)});
        [Test] public void SameRequest_HasStableIdentityAndIndependentCopy()
        {
            var left=Generate();var right=Generate();Assert.That(left.Identity,Is.EqualTo(right.Identity));Assert.That(left.Solids.Length,Is.EqualTo(right.Solids.Length));
            var copy=left.Copy();copy.Solids[0].Id="changed";Assert.That(left.Solids[0].Id,Is.Not.EqualTo("changed"));
        }
        [Test] public void ChangedSeedFamilyOrFrozenProfileValue_ChangesIdentity()
        {
            Assert.That(Generate(17).Identity,Is.Not.EqualTo(Generate(18).Identity));Assert.That(Generate(17,0).Identity,Is.Not.EqualTo(Generate(17,1).Identity));var p=ProvingProfile.CreateArenaFamiliesDefault();p.Set("fixture.width",33);var changed=ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=17,GeneratorVersion=ArenaGenerator.FamiliesVersion,Profile=p});Assert.That(changed.Identity,Is.Not.EqualTo(Generate(17).Identity));
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
    }
}
