using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class CobaltThumbHistoryTests
    {
        static ProvingProfile Previous() => (ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCobaltThumb",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [Test] public void OnlyRifleThumbDefaultsChangeAndCustomAnglesSurvive()
        {
            var old=Previous();var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCloserWeaponView",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
            Assert.That(old.Version,Is.EqualTo(24));Assert.That(current.Version,Is.EqualTo(25));
            foreach(var d in old.Descriptors.Where(d=>!d.Path.StartsWith("grip.Rifle.left.Thumb.")&&d.Path!="grip.Rifle.thumbOpposition"))
                Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            old.Set("grip.Rifle.left.Thumb.Intermediate",12);old.EnsureTrooperDescriptors();
            Assert.That(old.Get("grip.Rifle.left.Thumb.Intermediate"),Is.EqualTo(12));
            Assert.That(old.Get("grip.Rifle.left.Thumb.Proximal"),Is.EqualTo(current.Get("grip.Rifle.left.Thumb.Proximal")));
            Assert.That(old.Validate(),Is.Empty);
        }
        [Test] public void PublishedDefaultUpdatesButNamedAndAuthoringPosesAndSourceBytesSurvive()
        {
            foreach(int mode in new[]{0,1,2})
            {
                string path=Path.Combine(Path.GetTempPath(),"cobalt-thumb-history-"+Guid.NewGuid()+".json");
                try
                {
                    var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};
                    var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateTrooperDefault()}};
                    var first=new DesignLabHistory(path,old,authoring:mode==2);
                    if(mode==1)first.Create("Custom COBALT thumb");else first.Rename(mode==2?"Authored default":"Default");
                    Assert.That(File.Exists(path),Is.True,"The old history must be persisted before testing migration");
                    if(mode!=0){var draft=first.Selected.Snapshot;draft.Set("grip.Rifle.left.Thumb.Intermediate",12);first.Save(draft);}
                    var source=first.Selected.Snapshot;string hash=first.Selected.Hash,bytes=UnityEngine.JsonUtility.ToJson(source);
                    var migrated=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(migrated.StorageError,Is.Null,"mode="+mode);
                    foreach(var d in source.Descriptors.Where(d=>(d.Path.StartsWith("grip.Rifle.left.Thumb.")||d.Path=="grip.Rifle.thumbOpposition")))
                        Assert.That(migrated.Selected.Snapshot.Get(d.Path),Is.EqualTo(mode==0?current.Get(d.Path):source.Get(d.Path)),"mode="+mode+" "+d.Path);
                    var original=migrated.Profiles.SelectMany(p=>p.Revisions).Single(r=>r.Hash==hash);
                    Assert.That(UnityEngine.JsonUtility.ToJson(original.Snapshot),Is.EqualTo(bytes));
                    var reopened=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
                }
                finally { if(File.Exists(path))File.Delete(path); }
            }
        }
    }
}
