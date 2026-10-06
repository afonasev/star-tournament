using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class CutterBeamTests
    {
        static double Step(CutterBeam b,double seconds,double energy,float distance,System.Action<int,float> hit,float wall=20)
            =>b.Advance(seconds,energy,Vector3.zero,Vector3.forward,wall,new[]{distance},new[]{1},1,hit);
        [TestCase(.01,100)] [TestCase(.02,100)] [TestCase(.1,100)]
        [TestCase(.01,300)] [TestCase(.02,300)] [TestCase(.1,300)]
        public void ThreeSecondsContactDealsConfiguredReferenceDamage(double dt,float referenceDamage)
        {
            var p=ProvingProfile.CreateCutterDefault();p.Set("cutter.referenceDamage",referenceDamage);var b=new CutterBeam(p,1);float total=0;
            for(int i=0;i<System.Math.Round(3/dt);i++)Step(b,dt,30,0,(_,d)=>total+=d);
            Assert.That(total,Is.EqualTo(referenceDamage).Within(.001));
        }
        [Test] public void GrowthClipsAndDoesNotCreditUndeliveredContact()
        {
            var b=new CutterBeam(ProvingProfile.CreateCutterDefault(),1);float total=0;
            Step(b,.075,30,15,(_,d)=>total+=d);Assert.That(b.Read().Endpoint.z,Is.EqualTo(10).Within(.0001));Assert.That(total,Is.Zero);
            Step(b,.075,30,15,(_,d)=>total+=d);Assert.That(total,Is.EqualTo(3.75f).Within(.0001));
            Step(b,.1,30,15,(_,d)=>total+=d,12);Assert.That(total,Is.EqualTo(3.75f).Within(.0001));Assert.That(b.Read().Endpoint.z,Is.EqualTo(12));
        }
        [Test] public void ShortBurstsConsumeFractionalEnergyAndContactRestarts()
        {
            var b=new CutterBeam(ProvingProfile.CreateCutterDefault(),1);double energy=30;float total=0;
            for(int i=0;i<100;i++){energy-=Step(b,.01,energy,0,(_,d)=>total+=d);b.Stop();}
            Assert.That(energy,Is.EqualTo(29).Within(1e-9));Assert.That(total,Is.EqualTo(100).Within(.001));
            energy-=Step(b,40,energy,-1,(_,d)=>total+=d);Assert.That(energy,Is.Zero.Within(1e-9));
        }
        [TestCase(100)] [TestCase(300)] public void RestorePreservesPartialTimeAndFractionalDamage(float referenceDamage)
        {
            var p=ProvingProfile.CreateCutterDefault();p.Set("cutter.referenceDamage",referenceDamage);var a=new CutterBeam(p,1);float total=0;
            Step(a,.16,30,0,(_,d)=>total+=d);var b=new CutterBeam(p,1);b.Restore(a.Read());
            Step(b,2.84,30,0,(_,d)=>total+=d);Assert.That(total,Is.EqualTo(referenceDamage).Within(.001));
        }
        [Test] public void TargetsAreIndependentAndWallBlocksThird()
        {
            var b=new CutterBeam(ProvingProfile.CreateCutterDefault(),3);var total=new float[3];
            b.Advance(3.15,30,Vector3.zero,Vector3.forward,10,new[]{0f,0f,11f},new[]{1,2,3},1,(i,d)=>total[i]+=d);
            Assert.That(total[0],Is.EqualTo(total[1]));Assert.That(total[0],Is.GreaterThanOrEqualTo(300));Assert.That(total[2],Is.Zero);
        }
        [Test] public void BoostUsesCommonArmorAndExactRemainder()
        {
            var p=ProvingProfile.CreateCutterDefault();var b=new CutterBeam(p,1);var life=new CombatLife("target",ProvingProfile.CreateCombatDefault());life.GrantArmor(100);
            for(int i=0;i<30;i++)b.Advance(.1,30,Vector3.zero,Vector3.forward,20,new[]{0f},new[]{1},2,(_,d)=>life.Damage(1,d));
            Assert.That(life.Read().Armor,Is.Zero);Assert.That(life.Read().Health,Is.Zero);Assert.That(life.Read().Dead,Is.True);
        }
        [Test] public void ContactGapPreservesAppliedFractionsWithoutDebtAcrossNewLife()
        {
            var p=ProvingProfile.CreateCutterDefault();p.Set("cutter.referenceDamage",100);
            var b=new CutterBeam(p,1);float total=0;
            Step(b,.06,30,0,(_,d)=>total+=d);Step(b,.02,30,-1,(_,d)=>total+=d);Step(b,.06,30,0,(_,d)=>total+=d);
            Assert.That(total,Is.EqualTo(4).Within(.0001));Step(b,.04,30,0,(_,d)=>total+=d);Assert.That(total,Is.EqualTo(16f/3).Within(.0001));
            b.Advance(.1,30,Vector3.zero,Vector3.forward,20,new[]{0f},new[]{2},1,(_,d)=>total+=d);
            Assert.That(total,Is.EqualTo(26f/3).Within(.0001));
        }
        [Test] public void FourthSlotKeepsEnergyAndRespawnRestoresIt()
        {
            var p=ProvingProfile.CreateCombatDefault();var life=new CombatLife("p",p);
            life.CollectWeapon(WeaponId.Cutter);life.Select(WeaponSelection.Previous);life.Advance(1);Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Cutter));
            life.ConsumeCutter(.05);life.Select(WeaponSelection.Next);life.Advance(1);Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            life.Select(WeaponSelection.Cutter);life.Advance(1);Assert.That(life.Read().CutterEnergy,Is.EqualTo(29.95));
            life.Damage(1,100);life.Advance(10);life.Respawn(1);Assert.That(life.Read().CutterEnergy,Is.Zero);
        }
    }
}
