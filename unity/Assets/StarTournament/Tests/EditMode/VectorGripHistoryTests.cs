using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class VectorGripHistoryTests
    {
        static ProvingProfile Previous()=> (ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeVectorGrip",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [Test] public void OnlyShotgunFactoryPoseChangesAndCustomValuesSurvive()
        {
            var old=Previous();var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCobaltThumb",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);Assert.That(old.Version,Is.EqualTo(22));Assert.That(current.Version,Is.EqualTo(24));
            foreach(var d in old.Descriptors.Where(d=>!d.Path.StartsWith("grip.Shotgun.")))Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            old.Set("grip.Shotgun.supportYaw",-17);old.Set("grip.Shotgun.left.Index.Distal",11);old.EnsureTrooperDescriptors();
            Assert.That(old.Get("grip.Shotgun.supportYaw"),Is.EqualTo(-17));Assert.That(old.Get("grip.Shotgun.left.Index.Distal"),Is.EqualTo(11));
            foreach(var d in current.Descriptors.Where(d=>d.Path.StartsWith("grip.Shotgun.")&&d.Path!="grip.Shotgun.supportYaw"&&d.Path!="grip.Shotgun.left.Index.Distal"))Assert.That(old.Get(d.Path),Is.EqualTo(d.DefaultValue),d.Path);
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void PublishedSceneDefaultUpgradesWhileNamedAndAuthoredDefaultsStayExact()
        {
            foreach(int mode in new[]{0,1,2})
            {
                string path=Path.Combine(Path.GetTempPath(),"vector-scene-history-"+Guid.NewGuid()+".json");
                try
                {
                    var pose=Previous();
                    var old=new LabBundle{Profiles=new List<ProvingProfile>{pose}};var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateTrooperDefault()}};
                    var first=new DesignLabHistory(path,old,authoring:mode==2);
                    if(mode==1)first.Create("Intentional scene grip");else first.Rename(mode==2?"Authored default":"Default");
                    if(mode>0){var draft=first.Selected.Snapshot;draft.Set("grip.Shotgun.supportYaw",80);draft.Set("grip.Shotgun.supportZ",.42f);draft.Set("grip.Shotgun.wristAlignment",0);first.Save(draft);}
                    var source=first.Selected.Snapshot;
                    string hash=first.Selected.Hash,bytes=UnityEngine.JsonUtility.ToJson(first.Selected.Snapshot);
                    var migrated=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(migrated.StorageError,Is.Null,"mode="+mode);
                    foreach(var d in pose.Descriptors.Where(d=>d.Path.StartsWith("grip.Shotgun.")))
                        Assert.That(migrated.Selected.Snapshot.Get(d.Path),Is.EqualTo(mode==0?current.Get(d.Path):source.Get(d.Path)),"mode="+mode+" "+d.Path);
                    var original=migrated.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash);Assert.That(UnityEngine.JsonUtility.ToJson(original.Snapshot),Is.EqualTo(bytes));
                }
                finally{if(File.Exists(path))File.Delete(path);}
            }
        }
        [Test] public void F01ReleasedAndCustomThumbHashesRemainReadable()
        {
            var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCobaltThumb",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeVectorThumb",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(current,null);
            Assert.That(old.Version,Is.EqualTo(23));
            foreach(var d in old.Descriptors.Where(d=>!d.Path.StartsWith("grip.Shotgun.left.Thumb.")&&d.Path!="grip.Shotgun.thumbOpposition"))
                Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            string path=Path.Combine(Path.GetTempPath(),"vector-thumb-"+Guid.NewGuid()+".json");
            try
            {
                var h=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{old}});
                string released=h.Selected.Hash,bytes=UnityEngine.JsonUtility.ToJson(h.Selected.Snapshot);
                h.Rename("Default");
                var upgradedDefault=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{current}});
                Assert.That(upgradedDefault.StorageError,Is.Null);
                Assert.That(upgradedDefault.Selected.Snapshot.Get("grip.Shotgun.thumbOpposition"),Is.EqualTo(current.Get("grip.Shotgun.thumbOpposition")));
                h.Create("Custom thumb");var snapshot=h.Selected.Snapshot;snapshot.Set("grip.Shotgun.thumbOpposition",12);h.Save(snapshot);string custom=h.Selected.Hash;
                var migrated=new DesignLabHistory(path,new LabBundle{Profiles=new List<ProvingProfile>{current}});
                Assert.That(migrated.StorageError,Is.Null);Assert.That(migrated.Selected.Snapshot.Get("grip.Shotgun.thumbOpposition"),Is.EqualTo(12));
                var revisions=migrated.Profiles.SelectMany(p=>p.Revisions).ToArray();
                Assert.That(revisions.Select(r=>r.Hash),Does.Contain(custom));
                var originals=revisions.Where(r=>r.Hash==released).ToArray();
                Assert.That(originals,Is.Not.Empty);
                foreach(var revision in originals)Assert.That(UnityEngine.JsonUtility.ToJson(revision.Snapshot),Is.EqualTo(bytes));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void SavedReleaseAndCustomPoseHashesRemainReadable()
        {
            var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateTrooperDefault()}};
            string path=Path.Combine(Path.GetTempPath(),"vector-grip-history-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,old);string released=history.Selected.Hash;history.Create("Custom Vector grip");var s=history.Selected.Snapshot;s.Set("grip.Shotgun.supportYaw",-17);s.Set("grip.Shotgun.right.Middle.Intermediate",31);history.Save(s);string custom=history.Selected.Hash;
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);Assert.That(migrated.Selected.Snapshot.Get("grip.Shotgun.supportYaw"),Is.EqualTo(-17));Assert.That(migrated.Selected.Snapshot.Get("grip.Shotgun.right.Middle.Intermediate"),Is.EqualTo(31));
                var hashes=migrated.Profiles.SelectMany(p=>p.Revisions).Select(r=>r.Hash).ToArray();Assert.That(hashes,Does.Contain(released));Assert.That(hashes,Does.Contain(custom));
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
