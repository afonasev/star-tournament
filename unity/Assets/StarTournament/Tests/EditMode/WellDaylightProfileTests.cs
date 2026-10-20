using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class WellDaylightProfileTests
    {
        [Test] public void PackagedDefaultUsesApprovedLightingWithoutChangingCatalog()
        {
            var owner=new GameObject("Packaged Default baseline");
            try
            {
                var catalog=LabReleaseCatalog.Load();var before=JsonUtility.ToJson(catalog);
                // Use runtime profile ordering and its dynamic evaluation profile exactly.
                var baseline=owner.AddComponent<ProvingGround>().CaptureLabBundle();
                var history=DesignLabHistory.ResolvePublishedDefault(baseline,catalog);
                Assert.That(history.StorageError,Is.Null);Assert.That(history.ReleaseError,Is.Null);
                var defaults=ProvingProfile.CreateWellDefault();
                foreach(var d in defaults.Descriptors)
                    Assert.That(history.Selected.Snapshot.Get(d.Path),Is.EqualTo(defaults.Get(d.Path)),d.Path);
                Assert.That(history.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-authoring-v1").Version,Is.EqualTo(5));
                Assert.That(JsonUtility.ToJson(catalog),Is.EqualTo(before));
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);}
        }
        [TestCase(false,false)][TestCase(true,false)][TestCase(false,true)][TestCase(true,true)]
        public void PreserveV4HashesAndEverySavedLightingValue(bool compact,bool custom)
        {
            string directory=Path.Combine(Path.GetTempPath(),"well-daylight-"+Guid.NewGuid());Directory.CreateDirectory(directory);string path=Path.Combine(directory,"history.json");
            try
            {
                var current=DesignLabHistoryTests.Shipped();var old=current.Clone();
                var predecessor=typeof(ProvingProfile).GetMethod("BeforeWellDaylight",BindingFlags.Instance|BindingFlags.NonPublic);
                old.Profiles=old.Profiles.Select(p=>(ProvingProfile)predecessor.Invoke(p,null)).ToList();
                var history=new DesignLabHistory(path,old);history.Create("Дневной свет v4");
                var draft=history.Selected.Snapshot.Clone();draft.Set("well.liftSpeed",5);
                if(custom){draft.Set("well.daylight",1.2f);draft.Set("well.lamp",9);draft.Set("well.fill",.37f);draft.Set("well.innerAngle",85);draft.Set("well.daylightShadow",.6f);draft.Set("well.lampRange",16);}
                history.Save(draft);string hash=history.Selected.Hash;int number=history.Selected.Number;
                if(!compact){var file=typeof(DesignLabHistory).GetField("data",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(history);File.WriteAllText(path,JsonUtility.ToJson(file));}
                var upgraded=new DesignLabHistory(path,current);Assert.That(upgraded.StorageError,Is.Null);
                var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Number==number);
                Assert.That(preserved.Hash,Is.EqualTo(hash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(hash));
                Assert.That(preserved.Snapshot.Get("well.lamp"),Is.EqualTo(custom?9:7));
                Assert.That(upgraded.Selected.Snapshot.Profiles.Single(p=>p.Id=="well-gameplay-presentation-v1").Version,Is.EqualTo(6));
                var defaults=ProvingProfile.CreateWellDefault();
                foreach(var pair in new[]{("well.daylight",1.2f),("well.lamp",9f),("well.fill",.37f),("well.innerAngle",85f),("well.daylightShadow",.6f),("well.lampRange",16f)})
                    Assert.That(upgraded.Selected.Snapshot.Get(pair.Item1),Is.EqualTo(custom?pair.Item2:preserved.Snapshot.Get(pair.Item1)),pair.Item1);
                Assert.That(upgraded.Selected.Snapshot.Get("well.liftSpeed"),Is.EqualTo(5));
                Assert.That(upgraded.Selected.Snapshot.Get("well.localLightScale"),Is.EqualTo(2));
                Assert.That(upgraded.Selected.Snapshot.Get("well.sunYaw"),Is.EqualTo(-55));
                foreach(var descriptor in defaults.Descriptors)Assert.That(descriptor.Validate(out var reason),Is.True,reason);
                int count=upgraded.SelectedProfile.Revisions.Count;var reopened=new DesignLabHistory(path,current);
                Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.SelectedProfile.Revisions.Count,Is.EqualTo(count));
                Assert.That(reopened.Selected.Hash,Is.EqualTo(upgraded.Selected.Hash));
                Assert.That(reopened.SelectedProfile.Revisions.Single(r=>r.Number==number).Hash,Is.EqualTo(hash));
            }
            finally{Directory.Delete(directory,true);}
        }
    }
}
