using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class ShotgunResolverTests
    {
        [TestCase(1.2f)][TestCase(1.8f)][TestCase(2.4f)]
        public void TunedZoneDimensionsStayFrozenAndKeepExactBoundaryAndTieRules(float height)
        {
            var profile=ProvingProfile.CreateNativeCombatDefault();profile.Set("zone.headY",.8f);profile.Set("zone.headRadius",.2f);
            var resolver=new ShotgunResolver(profile,height);
            var targets=new[]{new CombatTarget(1,1,new ParticipantState{Position=Vector3.forward*4}),new CombatTarget(2,1,new ParticipantState{Position=Vector3.forward*4})};
            var origin=Vector3.up*(.8f*height);
            var hit=resolver.ResolveSegment(origin,Vector3.forward,targets,0,1,10);
            Assert.That(hit.TargetIndex,Is.EqualTo(0));Assert.That(hit.Zone,Is.EqualTo(HitZone.Head));
            Assert.That(hit.Distance,Is.EqualTo(4-.2f*height).Within(.00001f));
            Assert.That(resolver.ResolveSegment(origin,Vector3.forward,targets,0,1,hit.Distance).TargetIndex,Is.EqualTo(-1),"World wins the exact distance tie");
            profile.Set("zone.headY",1.1f);profile.Set("zone.headRadius",.01f);
            var frozen=resolver.ResolveSegment(origin,Vector3.forward,targets,0,1,10);
            Assert.That(frozen.TargetIndex,Is.EqualTo(hit.TargetIndex));Assert.That(frozen.Zone,Is.EqualTo(hit.Zone));Assert.That(frozen.Distance,Is.EqualTo(hit.Distance));
            Assert.That(resolver.ResolveSegment(origin,Vector3.forward,targets,1,1,10).TargetIndex,Is.EqualTo(1));
            Assert.That(resolver.ResolveSegment(origin,Vector3.forward,targets,1,2,10).TargetIndex,Is.EqualTo(0),"A different owner life can hit the respawned body");
        }
        [Test]public void TravelingSegmentHitsFromInsideLimbAndHasNoLegacyRangeClamp()
        {
            var p=ProvingProfile.CreateNativeCombatDefault();var resolver=new ShotgunResolver(p,1.8f);
            var target=new[]{new CombatTarget(1,1,new ParticipantState{Position=Vector3.zero})};
            var inside=new Vector3(p.Get("zone.armX")*1.8f,(p.Get("zone.armBottom")+p.Get("zone.armTop"))*.9f,0);
            foreach(var direction in new[]{Vector3.forward,Vector3.up})
            {
                var hit=resolver.ResolveSegment(inside,direction,target,0,1,1);
                Assert.That(hit.TargetIndex,Is.EqualTo(0));Assert.That(hit.Distance,Is.Zero);Assert.That(hit.Zone,Is.EqualTo(HitZone.Limb));
            }
            target=new[]{new CombatTarget(1,1,new ParticipantState{Position=Vector3.forward*120})};
            Assert.That(resolver.ResolveSegment(new Vector3(0,1.62f,0),Vector3.forward,target,0,1,130).TargetIndex,Is.EqualTo(0));
        }
        [Test]
        public void ZonesNearestTargetAndWorldOcclusionUseGameplayPose()
        {
            var resolver=new ShotgunResolver(ProvingProfile.CreateNativeCombatDefault(),1.8f);
            var targets=new[]{new CombatTarget(1,1,new ParticipantState{Position=new Vector3(0,0,4)}),new CombatTarget(2,1,new ParticipantState{Position=new Vector3(0,0,8)})};
            var head=resolver.Resolve(new Vector3(0,1.62f,0),Vector3.forward,targets,0,35);
            Assert.That(head.TargetIndex,Is.EqualTo(0)); Assert.That(head.Zone,Is.EqualTo(HitZone.Head));
            var torso=resolver.Resolve(new Vector3(0,1.08f,0),Vector3.forward,targets,0,35);
            Assert.That(torso.Zone,Is.EqualTo(HitZone.Torso));
            var leg=resolver.Resolve(new Vector3(.18f,.3f,0),Vector3.forward,targets,0,35);
            Assert.That(leg.Zone,Is.EqualTo(HitZone.Limb));
            Assert.That(resolver.Resolve(new Vector3(0,1.62f,0),Vector3.forward,targets,0,2).TargetIndex,Is.EqualTo(-1));
            Assert.That(resolver.Resolve(new Vector3(0,1.62f,0),Vector3.forward,targets,1,35).TargetIndex,Is.EqualTo(1));
            float mixed=(resolver.FullDamage(head.Zone)+resolver.FullDamage(torso.Zone)+resolver.FullDamage(leg.Zone))/3;
            Assert.That(mixed,Is.EqualTo(85).Within(.001f));
        }
        [Test]
        public void NativeTuningRejectsFractionalPelletsAndDirectionsRespectSpread()
        {
            var p=ProvingProfile.CreateNativeCombatDefault(); Assert.That(p.Validate(),Is.Empty);
            var resolver=new ShotgunResolver(p,1.8f);
            for(int i=0;i<resolver.PelletCount;i++)
            {
                var direction=resolver.Direction(Quaternion.identity,i,17);
                Assert.That(direction.magnitude,Is.EqualTo(1).Within(.00001f));
                Assert.That(Vector3.Angle(Vector3.forward,direction),Is.LessThanOrEqualTo(p.Get("shot.spread")+.001f));
            }
            p.Set("shot.pellets",1.5f); Assert.That(p.Validate(),Is.Not.Empty);
        }
    }
}
