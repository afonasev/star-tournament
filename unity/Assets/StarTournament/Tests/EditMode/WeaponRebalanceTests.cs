using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WeaponRebalanceTests
    {
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
                var file=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));var custom=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single(r=>r.Number==file.SelectedRevision);
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
