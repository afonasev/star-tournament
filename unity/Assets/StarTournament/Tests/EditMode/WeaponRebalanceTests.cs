using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WeaponRebalanceTests
    {
        [Test] public void RifleParametersKeepTimerQuantizationAndStopAtNewCapacity()
        {
            var p=ProvingProfile.CreateCombatDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            Assert.That(p.Get("rifle.startingAmmo"),Is.EqualTo(240));
            Assert.That(p.Get("rifle.cooldownSeconds"),Is.EqualTo(.10f/1.2f));
            Assert.That(combat.Get("rifle.spread"),Is.EqualTo(1.32f));
            Assert.That(combat.Get("rifle.damage"),Is.EqualTo(10));
            Assert.That(p.Validate(),Is.Empty);Assert.That(combat.Validate(),Is.Empty);
            var life=new CombatLife("p",p);int fired=0;
            // Current 50 Hz timer deliberately rounds nominal .083333 to .10 seconds.
            for(int tick=0;tick<1200;tick++){if(life.Fire(true))fired++;life.Advance(.02f);}
            Assert.That(fired,Is.EqualTo(240));Assert.That(life.Read().RifleAmmo,Is.Zero);
            life.Advance(1);Assert.That(life.Fire(true),Is.False);
            life.Damage(1,100);life.Advance(p.Get("combat.killcamSeconds"));life.Respawn(1);
            Assert.That(life.Read().RifleAmmo,Is.EqualTo(240));
        }
        [Test] public void RifleSpreadComparisonUsesIdenticalSeedsAtThreeDistances()
        {
            var current=ProvingProfile.CreateNativeCombatDefault();var previous=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(current));previous.Set("rifle.spread",1.1f);
            var oldResolver=new ShotgunResolver(previous,1.8f);var resolver=new ShotgunResolver(current,1.8f);
            foreach(float distance in new[]{10f,30f,60f})
            {
                int oldHits=0,newHits=0;float maxRadius=0;
                for(uint seed=1;seed<=4096;seed++)
                {
                    var a=oldResolver.Direction(Quaternion.identity,0,seed,WeaponId.Rifle);
                    var b=resolver.Direction(Quaternion.identity,0,seed,WeaponId.Rifle);
                    var oldPoint=a*(distance/a.z);var point=b*(distance/b.z);
                    float oldRadius=new Vector2(oldPoint.x,oldPoint.y).magnitude;
                    float radius=new Vector2(point.x,point.y).magnitude;
                    Assert.That(radius/oldRadius,Is.EqualTo(Mathf.Tan(1.32f*Mathf.Deg2Rad)/Mathf.Tan(1.1f*Mathf.Deg2Rad)).Within(.00001));
                    if(Mathf.Abs(oldPoint.x)<=.25f&&Mathf.Abs(oldPoint.y)<=.9f)oldHits++;
                    if(Mathf.Abs(point.x)<=.25f&&Mathf.Abs(point.y)<=.9f)newHits++;
                    maxRadius=Mathf.Max(maxRadius,radius);
                }
                Assert.That(newHits,Is.LessThanOrEqualTo(oldHits));
                TestContext.WriteLine($"rifle-spread distance={distance}m previous={oldHits}/4096 current={newHits}/4096 radius={maxRadius}m target=0.5x1.8m rectangle, not gameplay acceptance");
            }
        }
        [Test] public void RifleParameterMigrationRetainsPreviousReleaseAndCustomHistory()
        {
            var current=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault()}};
            var old=new LabBundle{Profiles=current.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeRifleBurstParameters",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)).ToList()};
            var path=Path.Combine(Path.GetTempPath(),"rifle-parameters-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,old);string oldRelease=history.Selected.Hash;
                history.Create("Custom rifle");var draft=history.Selected.Snapshot;
                draft.Set("rifle.startingAmmo",175);draft.Set("rifle.cooldownSeconds",.15f);draft.Set("rifle.spread",2.3f);history.Save(draft);
                string oldCustom=history.Selected.Hash;
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("rifle.startingAmmo"),Is.EqualTo(175));
                Assert.That(migrated.Selected.Snapshot.Get("rifle.cooldownSeconds"),Is.EqualTo(.15f));
                Assert.That(migrated.Selected.Snapshot.Get("rifle.spread"),Is.EqualTo(2.3f));
                Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==oldRelease),Is.True);
                Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==oldCustom),Is.True);
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                migrated.Select(DesignLabHistory.ReleaseId,migrated.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Last().Number);
                Assert.That(migrated.Selected.Snapshot.Get("rifle.startingAmmo"),Is.EqualTo(240));
                Assert.That(migrated.Selected.Snapshot.Get("rifle.spread"),Is.EqualTo(1.32f));
            }
            finally {if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void CadenceAndSwitchMeetFloatTickBoundariesWithoutEarlyUnlock()
        {
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());Assert.That(life.Fire(true),Is.True);
            for(int i=0;i<4;i++){life.Advance(.02f);Assert.That(life.Fire(true),Is.False);}
            life.Advance(.02f);Assert.That(life.Fire(true),Is.True);
            life.CollectWeapon(WeaponId.Shotgun);life.Select(WeaponSelection.Shotgun);
            for(int i=0;i<24;i++){life.Advance(.02f);Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Rifle));Assert.That(life.Fire(true),Is.False);}
            life.Advance(.02f);Assert.That(life.Read().SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));Assert.That(life.Read().SwitchRemaining,Is.Zero);
        }
        [Test] public void ShortFractionalContactIsImmediateAndObsoleteControlsDoNotAffectIt()
        {
            var p=ProvingProfile.CreateCutterDefault();p.Set("cutter.contactTickSeconds",1);p.Set("cutter.hitRadius",.5f);
            var beam=new CutterBeam(p,1);float total=0;
            beam.Advance(.005,30,Vector3.zero,Vector3.forward,20,new[]{0f},new[]{1},1,(_,d)=>total+=d);
            Assert.That(total,Is.EqualTo(.5f));beam.Stop();
            var restored=new CutterBeam(p,1);restored.Restore(beam.Read());
            restored.Advance(.002,30,Vector3.zero,Vector3.forward,20,new[]{0f},new[]{2},1.5f,(_,d)=>total+=d);
            Assert.That(total,Is.EqualTo(.8f).Within(.00001));
        }
        [Test] public void SharedBodyQueryMissesEmptyCapsuleAndSupportsPenetratingCallers()
        {
            var p=ProvingProfile.CreateNativeCombatDefault();var resolver=new ShotgunResolver(p,1.8f);
            var target=new CombatTarget(0,1,new ParticipantState{Position=Vector3.forward*5});
            Assert.That(resolver.ResolveTarget(new Vector3(0,.5f,0),Vector3.forward,target,20).TargetIndex,Is.EqualTo(-1));
            Assert.That(resolver.ResolveTarget(new Vector3(0,1.1f,0),Vector3.forward,target,20).TargetIndex,Is.EqualTo(0));
            var farther=new CombatTarget(1,1,new ParticipantState{Position=Vector3.forward*8});
            Assert.That(resolver.ResolveTarget(new Vector3(0,1.1f,0),Vector3.forward,farther,20).TargetIndex,Is.EqualTo(0));
            Assert.That(resolver.ResolveTarget(new Vector3(0,1.1f,0),Vector3.forward,farther,6).TargetIndex,Is.EqualTo(-1));
        }
        [Test] public void BoostedSplashAtOneMeterSurvivesButDirectCanKill()
        {
            var r=new RocketResolver(ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateDefault());
            var life=new CombatLife("p",ProvingProfile.CreateCombatDefault());life.Damage(1,r.Damage(1,false,1.5f));
            Assert.That(life.Read().Health,Is.EqualTo(4.375f));Assert.That(life.Read().Dead,Is.False);
            Assert.That(r.Damage(0,true,1),Is.EqualTo(85));Assert.That(r.Damage(0,true,1.5f),Is.EqualTo(127.5f));
        }
        [Test] public void PublishedRebalancePreservesHistoricalHashesAndCustomValuesOnRepeatedOpen()
        {
            var current=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateCutterDefault()}};
            var old=new LabBundle{Profiles=current.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)).ToList()};
            var path=Path.Combine(Path.GetTempPath(),"weapon-rebalance-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,old);string oldRelease=history.Selected.Hash;
                history.Create("Custom");var draft=history.Selected.Snapshot;draft.Set("rifle.damage",23);draft.Set("weapon.switchSeconds",.8f);history.Save(draft);// Simulate the former Player, where the now-obsolete radius was editable.
                var file=DesignLabHistoryTests.ReadFixture(path,old);var custom=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single(r=>r.Number==file.SelectedRevision);
                custom.Snapshot.Set("cutter.hitRadius",.1f);custom.Hash=custom.Snapshot.Hash();File.WriteAllText(path,JsonUtility.ToJson(file,true));string oldCustom=custom.Hash;
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(23));Assert.That(migrated.Selected.Snapshot.Get("weapon.switchSeconds"),Is.EqualTo(.8f));
                Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==oldRelease),Is.True);Assert.That(migrated.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==oldCustom),Is.True);
                Assert.That(migrated.Selected.Snapshot.IsVisible("cutter.hitRadius"),Is.False);Assert.That(migrated.Selected.Snapshot.Get("cutter.hitRadius"),Is.EqualTo(.1f));
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                migrated.Select(DesignLabHistory.ReleaseId,migrated.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Last().Number);
                Assert.That(migrated.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(10));
            }
            finally {if(File.Exists(path))File.Delete(path);}
        }
    }
}
