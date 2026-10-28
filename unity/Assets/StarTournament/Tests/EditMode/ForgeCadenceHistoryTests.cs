using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class ForgeCadenceHistoryTests
    {
        [TestCase("BeforeForgeCadence")]
        [TestCase("BeforeForgeHeavyTuning")]
        public void PackagedDefaultAdoptsTuningWithoutRewritingPublishedRevision(string historical)
        {
            var current=new LabBundle{Profiles={ProvingProfile.CreatePlasmaDefault()}};
            var previous=(ProvingProfile)typeof(ProvingProfile).GetMethod(historical,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(current.Profiles[0],null);
            var old=new LabBundle{Profiles={previous}};var catalogue=LabReleaseCatalog.Factory(old);string bytes=JsonUtility.ToJson(catalogue),hash=catalogue.Entries[0].Hash;
            foreach(var upgraded in new[]{DesignLabHistory.ResolveStartupDefault(current,catalogue),DesignLabHistory.ResolvePublishedDefault(current,catalogue)})
            {
                Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("plasma.damage"),Is.EqualTo(24));Assert.That(upgraded.Selected.Snapshot.Get("plasma.speed"),Is.EqualTo(14.4f));
                Assert.That(upgraded.Selected.Snapshot.Get("plasma.cooldownSeconds"),Is.EqualTo(.2f));Assert.That(upgraded.Selected.Snapshot.Get("plasma.orbRadius"),Is.EqualTo(.1521f));
                Assert.That(upgraded.Profiles.SelectMany(p=>p.Revisions).Any(r=>r.Hash==hash),Is.True);
            }
            Assert.That(JsonUtility.ToJson(catalogue),Is.EqualTo(bytes));
        }
        [Test] public void RoundedTorsoExcludesSquareCornerFalsePositive()
        {
            var method=typeof(ShotgunResolver).GetMethod("RoundedBox",BindingFlags.Static|BindingFlags.NonPublic);
            float Cast(float offset)=>(float)method.Invoke(null,new object[]{new Vector3(1+offset,1+offset,-5),Vector3.forward,new Bounds(Vector3.zero,Vector3.one*2),.2f});
            Assert.That(Cast(.15f),Is.EqualTo(-1));Assert.That(Cast(.1f),Is.GreaterThan(0));
        }
        [Test] public void SphereGrazesHeadWhereCenterRayMissesAndRespectsRadiusLimit()
        {
            var movement=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();float height=movement.Get("player.capsule.height"),radius=ProvingProfile.CreatePlasmaDefault().Get("plasma.orbRadius");
            var resolver=new ShotgunResolver(combat,height);var target=new CombatTarget(1,1,new ParticipantState{Position=Vector3.forward*5});
            var origin=new Vector3(combat.Get("zone.headRadius")*height+radius*.75f,combat.Get("zone.headY")*height,0);
            Assert.That(resolver.ResolveTarget(origin,Vector3.forward,target,10).TargetIndex,Is.EqualTo(-1));
            var hit=resolver.ResolveTarget(origin,Vector3.forward,target,10,radius);Assert.That(hit.TargetIndex,Is.EqualTo(0));Assert.That(hit.Zone,Is.EqualTo(HitZone.Head));
            origin.x+=radius*.5f;Assert.That(resolver.ResolveTarget(origin,Vector3.forward,target,10,radius).TargetIndex,Is.EqualTo(-1));
            Assert.That(resolver.ResolveTarget(origin,Vector3.forward,target,10).TargetIndex,Is.EqualTo(-1),"Point weapons retain existing geometry");
        }
        [TestCase("BeforeForgeCadence",3,30f,.09f)]
        [TestCase("BeforeForgeHeavyTuning",4,24f,.117f)]
        public void HistoricalRevisionRetainsBytesHashAndCustomValues(string historical,int version,float speed,float radius)
        {
            string path=Path.Combine(Path.GetTempPath(),"forge-cadence-"+Guid.NewGuid()+".json");
            try
            {
                var current=ProvingProfile.CreatePlasmaDefault();
                var old=(ProvingProfile)typeof(ProvingProfile).GetMethod(historical,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(current,null);
                Assert.That(old.Version,Is.EqualTo(version));Assert.That(old.Get("plasma.cooldownSeconds"),Is.EqualTo(version==3?.1f:.2f));
                Assert.That(old.Get("plasma.speed"),Is.EqualTo(speed));Assert.That(old.Get("plasma.orbRadius"),Is.EqualTo(radius));Assert.That(old.Get("plasma.damage"),Is.EqualTo(20));
                var history=new DesignLabHistory(path,new LabBundle{Profiles={old}});history.Create("Custom FORGE v3");
                var draft=history.Selected.Snapshot;draft.Set("plasma.speed",37);draft.Set("plasma.damage",33);history.Save(draft);
                string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);
                var upgraded=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(upgraded.StorageError,Is.Null);
                Assert.That(JsonUtility.ToJson(upgraded.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
                Assert.That(upgraded.Selected.Snapshot.Get("plasma.speed"),Is.EqualTo(37));Assert.That(upgraded.Selected.Snapshot.Get("plasma.damage"),Is.EqualTo(33));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
