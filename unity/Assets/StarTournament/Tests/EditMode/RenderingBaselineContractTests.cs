using System;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class RenderingBaselineContractTests
    {
        [Test] public void EntryIsExplicitAndMapsAreDistinct(){Assert.IsFalse(NativeRenderingBaseline.Requested(new[]{"-muted"}));Assert.IsTrue(NativeRenderingBaseline.Requested(new[]{"-renderingBaseline"}));Assert.AreEqual(4,new System.Collections.Generic.HashSet<string>(NativeRenderingBaseline.Maps).Count);}
        [TestCase(1)][TestCase(4)] public void CompositionKeepsEightHardBots(int views){var c=NativeRenderingBaseline.Composition(views);Assert.AreEqual(8,c.ParticipantCount);Assert.AreEqual(views,c.LocalCount);for(int n=0;n<8;n++)Assert.AreEqual(NativeParticipantKind.Bot,c.Participant(n).Kind);}
        [Test] public void MatchConfigurationUsesValidProfileDefault(){var p=ProvingProfile.CreateMatchDefault();var c=NativeRenderingBaseline.Configuration(p);Assert.DoesNotThrow(()=>c.Validate(p));Assert.IsFalse(c.TargetEnabled);Assert.AreEqual(5,c.DurationMinutes);Assert.AreEqual((int)p.Get("match.ffaTargetDefault"),c.TargetPoints);}
        [Test] public void BatchStartupStillRequiresReadyWorld(){Assert.IsFalse(NativeRenderingBaseline.ReadyForReview(false,false,true));Assert.IsTrue(NativeRenderingBaseline.ReadyForReview(true,false,true));Assert.IsFalse(NativeRenderingBaseline.ReadyForReview(true,false,false));Assert.IsTrue(NativeRenderingBaseline.ReadyForReview(true,true,false));}
        [Test] public void BackgroundAdapterCannotModifyEditorWindows(){Assert.Throws<NotSupportedException>(()=>RenderingBaselineMacBackground.Apply());}
        [Test] public void UnsupportedViewsFail(){Assert.Throws<ArgumentOutOfRangeException>(()=>NativeRenderingBaseline.Composition(2));}
        [Test] public void EmptyCountersRemainUnavailable(){Assert.AreEqual(0,new NativeRenderingBaseline.Distribution(Array.Empty<double>()).count);var d=new NativeRenderingBaseline.Distribution(new double[]{5,1,3});Assert.AreEqual(3,d.count);Assert.AreEqual(3,d.p50);Assert.AreEqual(5,d.p95);}
    }
}
