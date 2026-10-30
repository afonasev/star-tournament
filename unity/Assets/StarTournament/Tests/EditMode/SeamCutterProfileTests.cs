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
        [Test] public void GamepadRadiusIsVisibleAndPredecessorKeepsExactSnapshots()
        {
            var current=ProvingProfile.CreateCutterDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCutterGamepadRadius",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(current.Version,Is.EqualTo(14));Assert.That(old.Version,Is.EqualTo(13));
            Assert.That(current.Get("cutter.damageRadius"),Is.EqualTo(.1f));Assert.That(current.Get("cutter.jitter")*current.Get("cutter.dischargeScale"),Is.EqualTo(.1f).Within(.000001));
            Assert.That(old.Descriptor("cutter.damageRadius"),Is.Null);Assert.That(old.Get("cutter.jitter"),Is.EqualTo(.08f));
            var path=Path.Combine(Path.GetTempPath(),"cutter-radius-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom cutter");var draft=first.Selected.Snapshot;draft.Set("cutter.jitter",.13f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.IsVisible("cutter.damageRadius"),Is.True);Assert.That(next.Selected.Snapshot.Get("cutter.jitter"),Is.EqualTo(.13f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
                var reopened=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(next.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void ContactTraceDefaultPassesMatchStartValidationWithoutChangingDuration()
        {
            const string path="cutter.seam.contactSeconds";
            var profile=ProvingProfile.CreateCutterDefault();
            var descriptor=profile.Descriptors.Single(d=>d.Path==path);
            Assert.That(profile.Get(path),Is.EqualTo(.065f));
            Assert.That(descriptor.DefaultValue,Is.EqualTo(.065f));
            Assert.That(descriptor.Step,Is.EqualTo(.005f));
            foreach(var item in profile.Descriptors)
                Assert.DoesNotThrow(()=>NativeMatchConfiguration.ValidateValue(profile,item.Path,profile.Get(item.Path)),item.Path);
        }

        [TestCase(.01f)] [TestCase(.06f)] [TestCase(.065f)] [TestCase(.07f)] [TestCase(.5f)]
        public void ContactTraceAcceptsDeclaredStepValues(float value)
        {
            var profile=ProvingProfile.CreateCutterDefault();
            Assert.DoesNotThrow(()=>NativeMatchConfiguration.ValidateValue(profile,"cutter.seam.contactSeconds",value));
        }

        [TestCase(.066f)] [TestCase(.009f)] [TestCase(.505f)]
        public void ContactTraceRejectsOffStepAndOutOfRangeValues(float value)
        {
            var profile=ProvingProfile.CreateCutterDefault();
            Assert.Throws<ArgumentException>(()=>NativeMatchConfiguration.ValidateValue(profile,"cutter.seam.contactSeconds",value));
        }

        [Test] public void DefaultProfileValuesMatchTheirDeclaredRangesAndSteps()
        {
            var profile=ProvingProfile.CreateCutterDefault();
            Assert.That(profile.Validate(),Is.Empty);
        }

        [Test] public void V12ThumbContactIsAdditiveAndHistoricalSnapshotRemainsExact()
        {
            var latest=ProvingProfile.CreateCutterDefault();
            var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCutterGamepadRadius",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(latest,null);
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeSeamThumbContact",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(12));Assert.That(current.Version,Is.EqualTo(13));
            Assert.That(current.Descriptors.Count-old.Descriptors.Count,Is.EqualTo(6));
            foreach(var d in old.Descriptors)
            {
                Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
                Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)),d.Path);
            }
            var path=Path.Combine(Path.GetTempPath(),"seam-thumb-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Rename("Default");
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});
                Assert.That(next.StorageError,Is.Null);
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
                Assert.That(next.Selected.Snapshot.Get("cutter.seam.surface.thumbContactX"),Is.EqualTo(-.045f));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }

        [Test] public void V11CoreDefaultAdvancesWhileCustomAndHistoricalValuesStayExact()
        {
            var current=ProvingProfile.CreateCutterDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeThickerLegacyCutterCore",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(11));
            Assert.That(old.Get("cutter.seam.legacyCoreWidth"),Is.EqualTo(.003f));
            Assert.That(old.Descriptors.Single(d=>d.Path=="cutter.seam.legacyCoreWidth").DefaultValue,Is.EqualTo(.003f));
            Assert.That(current.Get("cutter.seam.legacyCoreWidth"),Is.EqualTo(.0035f));
            var path=Path.Combine(Path.GetTempPath(),"seam-f8-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Rename("Default");
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});
                Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.seam.legacyCoreWidth"),Is.EqualTo(.0035f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
                File.Delete(path);
                first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Custom v11");
                var draft=first.Selected.Snapshot;draft.Set("cutter.seam.legacyCoreWidth",.006f);first.Save(draft);
                hash=first.Selected.Hash;bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                next=new DesignLabHistory(path,new LabBundle{Profiles={current}});
                Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.seam.legacyCoreWidth"),Is.EqualTo(.006f));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void V10WideCoreSettingIsPreservedWithoutOverridingThinLegacyCore()
        {
            var current=ProvingProfile.CreateCutterDefault();
            Assert.That(current.Get("cutter.seam.legacyCoreWidth"),Is.LessThan(current.Get("cutter.arcWidth")));
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeLegacyCutterCoreWidth",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(10));Assert.That(old.Descriptors.Any(d=>d.Path=="cutter.seam.legacyCoreWidth"),Is.False);
            foreach(var d in old.Descriptors){Assert.That(JsonUtility.ToJson(current.Descriptors.Single(x=>x.Path==d.Path)),Is.EqualTo(JsonUtility.ToJson(d)));Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)));}
            var path=Path.Combine(Path.GetTempPath(),"seam-core-history-"+Guid.NewGuid()+".json");
            try
            {
                var first=new DesignLabHistory(path,new LabBundle{Profiles={old}});first.Create("Wide v10");
                var draft=first.Selected.Snapshot;draft.Set("cutter.width",.05f);first.Save(draft);
                string hash=first.Selected.Hash,bytes=JsonUtility.ToJson(first.Selected.Snapshot);
                var next=new DesignLabHistory(path,new LabBundle{Profiles={current}});Assert.That(next.StorageError,Is.Null);
                Assert.That(next.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(.05f));
                Assert.That(next.Selected.Snapshot.Get("cutter.seam.legacyCoreWidth"),Is.EqualTo(current.Get("cutter.seam.legacyCoreWidth")));
                Assert.That(JsonUtility.ToJson(next.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
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
