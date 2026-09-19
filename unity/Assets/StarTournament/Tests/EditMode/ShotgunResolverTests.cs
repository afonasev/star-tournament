using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class ShotgunResolverTests
    {
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
            Assert.That(mixed,Is.EqualTo(200f/3).Within(.001f));
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
