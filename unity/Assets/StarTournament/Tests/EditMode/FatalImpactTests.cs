using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class FatalImpactTests
    {
        [Test] public void ShotAttributionUsesOnlyThisTargetsCurrentLife()
        {
            var pellets=new[]{new PelletNotice(Vector3.forward,Vector3.forward*5,Vector3.back,PelletContact.Participant,2,4),
                new PelletNotice(Vector3.right,Vector3.right*3,Vector3.left,PelletContact.Participant,2,3),
                new PelletNotice(Vector3.left,Vector3.left*4,Vector3.right,PelletContact.Participant,1,4)};
            var impact=FatalImpact.FromShot(new ShotNotice(7,0,1,1,Vector3.zero,pellets),2,4);
            Assert.That(impact.Valid,Is.True);Assert.That(impact.Sequence,Is.EqualTo(7));Assert.That(impact.Direction,Is.EqualTo(Vector3.forward));
            Assert.That(impact.Point,Is.EqualTo(Vector3.forward*5));Assert.That(FatalImpact.FromShot(new ShotNotice(8,0,1,1,Vector3.zero,pellets),2,5).Valid,Is.False);
        }
        [Test] public void WeaponStrengthAndExplosionFalloffAreProfileOwned()
        {
            var p=ProvingProfile.CreateDeathDefault();Assert.That(p.Validate(),Is.Empty);
            Assert.That(p.Get("corpse.shotgunSpeed"),Is.EqualTo(9));Assert.That(p.Get("corpse.rifleSpeed"),Is.EqualTo(3.75f));
            Assert.That(p.Get("corpse.cutterSpeed"),Is.EqualTo(.375f));Assert.That(p.Get("corpse.rocketSpeed"),Is.EqualTo(13.5f));
            float Speed(WeaponId w,float fraction=0)=>new FatalImpact(w,1,Vector3.up,Vector3.zero,fraction).Velocity(p).magnitude;
            Assert.That(Speed(WeaponId.Shotgun),Is.GreaterThan(Speed(WeaponId.Rifle)));Assert.That(Speed(WeaponId.Rifle),Is.GreaterThan(Speed(WeaponId.Cutter)));
            Assert.That(Speed(WeaponId.RocketLauncher,.75f),Is.LessThan(Speed(WeaponId.RocketLauncher,.25f)));Assert.That(Speed(WeaponId.RocketLauncher,1),Is.Zero);
            Assert.That(default(FatalImpact).Velocity(p),Is.EqualTo(Vector3.zero));
            foreach(var d in p.Descriptors){Assert.That(d.Label,Is.Not.Empty);Assert.That(d.Description,Is.Not.Empty);Assert.That(d.Step,Is.GreaterThan(0));}
        }
    }
}
