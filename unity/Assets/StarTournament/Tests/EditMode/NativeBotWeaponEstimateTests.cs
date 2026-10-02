using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public class NativeBotWeaponEstimateTests
    {
        NativeShotgunPolicy Policy(ProvingProfile combat=null,ProvingProfile life=null)=>new NativeShotgunPolicy(ProvingProfile.CreateDefault(),combat??ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateCutterDefault(),life??ProvingProfile.CreateCombatDefault());
        NativeBotFrame Frame(float distance=10,Vector3 velocity=default)
        {
            var life=new CombatLife("bot",ProvingProfile.CreateCombatDefault()).Read();
            life.CutterOwned=life.ShotgunOwned=life.RocketOwned=true;life.CutterEnergy=30;life.ShotgunAmmo=life.RocketAmmo=20;
            var enemy=new NativeBotMemoryEntry{Visible=true,Sighting=new NativeBotSighting(1,1,Vector3.forward*distance){Velocity=velocity}};
            return new NativeBotFrame{Pose=new ParticipantState(),Life=life,Knowledge=new NativeBotKnowledge{Enemies=new[]{enemy}}};
        }
        float Score(NativeShotgunPolicy policy,WeaponId id,NativeBotFrame f,float error=0,float rate=230)=>policy.Estimate.Estimate(id,f,f.Knowledge.Enemies[0],0,0,1,16,32,rate,error,.7f,1);
        [Test] public void RifleSpreadReducesDistantExpectedDamage()
        {
            var combat=ProvingProfile.CreateNativeCombatDefault();var f=Frame(30);combat.Set("rifle.spread",0);
            float accurate=Score(Policy(combat),WeaponId.Rifle,f);combat.Set("rifle.spread",6);
            Assert.That(Score(Policy(combat),WeaponId.Rifle,f),Is.LessThan(accurate*.5f));
        }
        [Test] public void SwitchingCooldownAndAmmunitionConsumeActualAttackOpportunity()
        {
            var p=Policy();var f=Frame();float ready=Score(p,WeaponId.Rifle,f);
            f.Life.RifleAmmo=1;Assert.That(Score(p,WeaponId.Rifle,f),Is.LessThan(ready));
            f.Life.RifleCooldownRemaining=1;Assert.That(Score(p,WeaponId.Rifle,f),Is.Zero);
            f=Frame();float alternative=Score(p,WeaponId.Cutter,f);f.Life.SelectedWeapon=WeaponId.Cutter;
            Assert.That(Score(p,WeaponId.Cutter,f),Is.GreaterThan(alternative));
            f.Life.CutterEnergy=0;Assert.That(Score(p,WeaponId.Cutter,f),Is.Zero);
        }
        [Test] public void PendingSwitchCannotForecastFireFromTheOutgoingWeapon()
        {
            var p=Policy();var f=Frame();f.Life.SwitchRemaining=.25;f.Life.PendingWeapon=WeaponId.Shotgun;
            Assert.That(Score(p,WeaponId.Rifle,f),Is.Zero);
            Assert.That(Score(p,WeaponId.Shotgun,f),Is.GreaterThan(0));
        }
        [Test] public void ALaunchedRocketRetainsValueBeyondTheLaunchWindow()
        {
            var p=Policy();var f=Frame(60);f.Life.SelectedWeapon=WeaponId.RocketLauncher;
            Assert.That(Score(p,WeaponId.RocketLauncher,f),Is.GreaterThan(0));
        }
        [Test] public void TrackableMotionIsNotAnAbsoluteVelocityPenalty()
        {
            var p=Policy();var still=Frame();still.Life.SelectedWeapon=WeaponId.Cutter;
            var moving=Frame(10,Vector3.right*3);moving.Life.SelectedWeapon=WeaponId.Cutter;
            Assert.That(Score(p,WeaponId.Cutter,moving),Is.GreaterThan(Score(p,WeaponId.Cutter,still)*.9f));
            Assert.That(Score(p,WeaponId.Cutter,moving,0,1),Is.LessThan(Score(p,WeaponId.Cutter,moving)));
        }
        [Test] public void OnlyVisibleCollinearEnemiesImproveBeamEstimate()
        {
            var p=Policy();var f=Frame(8);f.Life.SelectedWeapon=WeaponId.Cutter;float single=Score(p,WeaponId.Cutter,f);
            f.Knowledge.Enemies=f.Knowledge.Enemies.Append(new NativeBotMemoryEntry{Visible=true,Sighting=new NativeBotSighting(2,1,Vector3.forward*12)}).ToArray();
            Assert.That(Score(p,WeaponId.Cutter,f),Is.GreaterThan(single*1.5f));
            f.Knowledge.Enemies[1].Visible=false;Assert.That(Score(p,WeaponId.Cutter,f),Is.EqualTo(single).Within(.001));
            f.Knowledge.Enemies[0].Visible=false;Assert.That(Score(p,WeaponId.Cutter,f),Is.Zero);
        }
        [Test] public void OwnedAmmoAndKnownVitalsBoundDiscreteDamage()
        {
            var p=Policy();var f=Frame();float full=Score(p,WeaponId.Rifle,f);
            f.Knowledge.Enemies[0].Sighting.HasVitals=true;f.Knowledge.Enemies[0].Sighting.Health=10;
            Assert.That(Score(p,WeaponId.Rifle,f),Is.LessThanOrEqualTo(10));Assert.That(full,Is.GreaterThan(10));
        }
        [Test] public void PenetrationCanJustifySwitchWithoutUnconditionalCutterPriority()
        {
            var p=Policy();var f=Frame(8);
            float rifle=Score(p,WeaponId.Rifle,f),single=Score(p,WeaponId.Cutter,f);
            Assert.That(single,Is.LessThan(rifle));
            f.Knowledge.Enemies=f.Knowledge.Enemies.Concat(Enumerable.Range(2,3).Select(i=>new NativeBotMemoryEntry{Visible=true,Sighting=new NativeBotSighting(i,1,Vector3.forward*(8+i*2))})).ToArray();
            Assert.That(Score(p,WeaponId.Cutter,f),Is.GreaterThan(rifle*1.15f));
        }
        [Test] public void PreviousBotRegistryDefaultsAndCustomValuesRemainImmutable()
        {
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".json");
            try
            {
                var old=DesignLabHistoryTests.Shipped();var previous=typeof(ProvingProfile).GetMethod("BeforeBotWeaponEvaluation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);old.Profiles=old.Profiles.Select(p=>(ProvingProfile)previous.Invoke(p,null)).ToList();
                var h=new DesignLabHistory(path,old);Assert.That(h.StorageError,Is.Null);h.Create("Old tuning");var draft=h.Selected.Snapshot;draft.Set("bots.strategy.weaponHorizonSeconds",.75f);h.Save(draft);
                string hash=h.Selected.Hash;var current=new DesignLabHistory(path,DesignLabHistoryTests.Shipped());Assert.That(current.StorageError,Is.Null);
                Assert.That(current.Selected.Snapshot.Get("bots.strategy.weaponHorizonSeconds"),Is.EqualTo(.75f));
                Assert.That(current.SelectedProfile.Revisions.Any(r=>r.Hash==hash),Is.True);
            }
            finally {if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        }
    }
}
