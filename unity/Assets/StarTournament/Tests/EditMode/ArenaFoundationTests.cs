using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ArenaFoundationTests
    {
        static ArenaDefinition Generate(uint seed=17) => ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=seed,GeneratorVersion=ArenaGenerator.FoundationVersion,Profile=ProvingProfile.CreateArenaFoundationDefault()});
        [Test] public void SameRequest_HasStableIdentityAndIndependentCopy()
        {
            var left=Generate();var right=Generate();Assert.That(left.Identity,Is.EqualTo(right.Identity));Assert.That(left.Solids.Length,Is.EqualTo(right.Solids.Length));
            var copy=left.Copy();copy.Solids[0].Id="changed";Assert.That(left.Solids[0].Id,Is.Not.EqualTo("changed"));
        }
        [Test] public void ChangedSeedOrFrozenProfileValue_ChangesIdentity()
        {
            Assert.That(Generate(17).Identity,Is.Not.EqualTo(Generate(18).Identity));var p=ProvingProfile.CreateArenaFoundationDefault();p.Set("fixture.width",33);var changed=ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=17,GeneratorVersion=ArenaGenerator.FoundationVersion,Profile=p});Assert.That(changed.Identity,Is.Not.EqualTo(Generate(17).Identity));
        }
        [Test] public void FoundationDefinition_HasUniqueSolidsAndTwoDeclaredTransitions()
        {
            var d=Generate();Assert.That(d.Solids,Has.Length.GreaterThan(10));Assert.That(d.SpawnRegions,Has.Length.EqualTo(2));Assert.That(d.Transitions,Has.Length.EqualTo(2));Assert.That(d.Transitions[0].OrderedFeet.Length,Is.GreaterThan(2));
        }
        [Test] public void UnsupportedVersion_IsRejectedBeforeSceneCreation()
        {
            Assert.Throws<System.ArgumentException>(()=>ArenaGenerator.Generate(new ArenaGenerationRequest{Seed=1,GeneratorVersion="unknown",Profile=ProvingProfile.CreateArenaFoundationDefault()}));
        }
    }
}
