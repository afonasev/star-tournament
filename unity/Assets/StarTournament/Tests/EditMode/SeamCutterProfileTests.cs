using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class SeamCutterProfileTests
    {
        [Test] public void V9HistoryAndCustomChargeRemainExactWithLegacyBeam()
        {
            var current=ProvingProfile.CreateCutterDefault();
            Assert.That(current.Get("cutter.seam.legacyBeam"),Is.EqualTo(1));
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeLegacyCutterBeam",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(9));Assert.That(old.Descriptors.Any(d=>d.Path=="cutter.seam.legacyBeam"),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            var path=Path.Combine(Path.GetTempPath(),"seam-legacy-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom v9");
                var draft=first.Selected.Snapshot;draft.Set("cutter.width",.05f);draft.Set("cutter.seam.battery.intensity",2);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(.05f));Assert.That(next.Selected.Snapshot.Get("cutter.seam.battery.intensity"),Is.EqualTo(2));
                Assert.That(next.Selected.Snapshot.Get("cutter.seam.legacyBeam"),Is.EqualTo(1));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void V8RegistryAndValuesStayExactWithBatteryControls()
        {
            var current=ProvingProfile.CreateCutterDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSeamBattery",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(8));Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("cutter.seam.battery.")),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            var path=Path.Combine(Path.GetTempPath(),"seam-fidelity-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom v8");var draft=first.Selected.Snapshot;draft.Set("cutter.width",.05f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(.05f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void V7RegistryAndValuesStayExactWithForearmSwivel()
        {
            var current=ProvingProfile.CreateCutterDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSeamForearmSwivel",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(7));Assert.That(old.Descriptors.Any(d=>d.Path=="cutter.seam.surface.supportElbowSwivel"),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            var path=Path.Combine(Path.GetTempPath(),"seam-fidelity-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom v7");var draft=first.Selected.Snapshot;draft.Set("cutter.width",.05f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(.05f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void V6RegistryAndValuesStayExactWithSurfaceControls()
        {
            var current=ProvingProfile.CreateCutterDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSeamContactSurface",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(6));Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("cutter.seam.surface.")),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            var path=Path.Combine(Path.GetTempPath(),"seam-fidelity-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom v6");var draft=first.Selected.Snapshot;draft.Set("cutter.width",.05f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(.05f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void V5RegistryAndValuesStayExactWithFidelityControls()
        {
            var current=ProvingProfile.CreateCutterDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSeamFidelity",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(5));Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("cutter.seam.detail.")),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            var path=Path.Combine(Path.GetTempPath(),"seam-fidelity-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom v5");var draft=first.Selected.Snapshot;draft.Set("cutter.width",.05f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(.05f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void V4HistoryAndCustomGameplayRemainByteExact()
        {
            var current=ProvingProfile.CreateCutterDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSeamPresentation",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(4));Assert.That(old.Descriptors.Any(d=>d.Path.StartsWith("cutter.seam.")),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            var path=Path.Combine(Path.GetTempPath(),"seam-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom Cutter");
                var draft=first.Selected.Snapshot;draft.Set("cutter.maxRangeMeters",17);draft.Set("cutter.green",.4f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.maxRangeMeters"),Is.EqualTo(17));Assert.That(next.Selected.Snapshot.Get("cutter.green"),Is.EqualTo(.4f));
                Assert.That(next.Selected.Snapshot.Get("cutter.seam.sparkCount"),Is.EqualTo(14));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
