using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class ForgeHistoryTests
    {
        static ProvingProfile Before(ProvingProfile p,string method)=>(ProvingProfile)typeof(ProvingProfile).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(p,null);
        [Test]public void SmallerViewmodelPreservesV1HistoryAndCustomWorldScale()
        {
            string path=Path.Combine(Path.GetTempPath(),"forge-size-history-"+Guid.NewGuid()+".json");
            try
            {
                var current=ProvingProfile.CreatePlasmaDefault();var old=Before(current,"BeforeForgeViewScale");
                Assert.That(old.Version,Is.EqualTo(1));Assert.That(old.Descriptors.Any(d=>d.Path=="plasma.viewScale"),Is.False);
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom FORGE");
                var draft=first.Selected.Snapshot;draft.Set("plasma.modelScale",1.2f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("plasma.modelScale"),Is.EqualTo(1.2f));
                Assert.That(next.Selected.Snapshot.Get("plasma.viewScale"),Is.EqualTo(.7f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(x=>x.Revisions).Single(x=>x.Hash==hash).Snapshot),Is.EqualTo(bytes));
                old.Set("plasma.modelScale",1.1f);old.EnsurePlasmaDescriptors();
                Assert.That(old.Get("plasma.modelScale"),Is.EqualTo(1.1f));Assert.That(old.Get("plasma.viewScale"),Is.EqualTo(.7f));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test]public void PreviousGripRegistryHasNoForgeAndRetainsEveryOtherDescriptor()
        {
            var current=ProvingProfile.CreateTrooperDefault();var old=Before(current,"BeforeForgeGrip");Assert.That(current.Version,Is.EqualTo(21));Assert.That(old.Version,Is.EqualTo(20));
            Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("grip.Plasma.")),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            Assert.That(Before(current,"BeforeArticulatedFingerGrips").Descriptors.Any(d=>d.Path.StartsWith("grip.Plasma.")),Is.False);
        }
        [Test]public void AllFourPreviousMapRegistriesKeepImmutableRevisions()
        {
            var current=new LabBundle{Profiles={CombatBowlCatalog.AuthoringProfile(),IndustrialTunnelsCatalog.AuthoringProfile(),LunarLaboratoryCatalog.AuthoringProfile(),WellArenaCatalog.AuthoringProfile(),ProvingProfile.CreatePlasmaDefault()}};
            var old=new LabBundle{Profiles=current.Profiles.Where(p=>p.Id!="forge-plasma-v1").Select(p=>Before(p,"BeforeForgePickups")).ToList()};
            Assert.That(old.Profiles.Select(p=>p.Version),Is.EqualTo(new[]{12,1,2,5}));Assert.That(old.Descriptors.Any(d=>d.Path.Contains("forge-")),Is.False);
            string path=Path.Combine(Path.GetTempPath(),"forge-map-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,old);first.Create("Saved map layout");string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,current);Assert.That(next.StorageError,Is.Null);Assert.That(next.Selected.Snapshot.Descriptors.Count(d=>d.Path.Contains("forge-")),Is.EqualTo(18));
                Assert.That(JsonUtility.ToJson(next.Profiles.Single(p=>p.Id==first.SelectedProfileId).Revisions.Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test]public void AdditiveForgeUpgradeKeepsSavedHashAndCustomGrip()
        {
            string path=Path.Combine(Path.GetTempPath(),"forge-history-"+Guid.NewGuid()+".json");
            try
            {
                var current=new LabBundle{Profiles={ProvingProfile.CreateTrooperDefault(),ProvingProfile.CreatePlasmaDefault()}};
                var old=new LabBundle{Profiles={Before(current.Profiles[0],"BeforeForgeGrip")}};
                var first=new DesignLabHistory(path,old);first.Create("Custom grip");var draft=first.Selected.Snapshot;draft.Set("grip.Cutter.supportZ",.5f);first.Save(draft);
                string hash=first.Selected.Hash;string bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,current);Assert.That(next.StorageError,Is.Null);Assert.That(next.Selected.Snapshot.Get("grip.Cutter.supportZ"),Is.EqualTo(.5f));Assert.That(next.Selected.Snapshot.Get("plasma.damage"),Is.EqualTo(20));
                var saved=next.Profiles.SelectMany(x=>x.Revisions).Single(x=>x.Hash==hash);Assert.That(JsonUtility.ToJson(saved.Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
