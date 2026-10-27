using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class UnifiedArmGripHistoryTests
    {
        static ProvingProfile Previous()=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeUnifiedArmGrips",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [Test] public void FactoryUnifiesBothArmsWithoutChangingContactsOrView()
        {
            var old=Previous();var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeStableArmEntry",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);Assert.That(old.Version,Is.EqualTo(26));Assert.That(current.Version,Is.EqualTo(27));
            foreach(var d in old.Descriptors.Where(d=>!d.Path.EndsWith(".wristAlignment")))Assert.That(current.Get(d.Path),Is.EqualTo(old.Get(d.Path)),d.Path);
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {Assert.That(current.Get("grip."+weapon+".wristAlignment"),Is.EqualTo(1));Assert.That(current.Get("grip."+weapon+".primaryWristAlignment"),Is.EqualTo(1));}
            old.Set("grip.RocketLauncher.wristAlignment",.4f);old.EnsureTrooperDescriptors();
            Assert.That(old.Get("grip.RocketLauncher.wristAlignment"),Is.EqualTo(.4f));Assert.That(old.Validate(),Is.Empty);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void ReleasedDefaultsUpgradeWhileIntentionalOldArmPosesAndHashesSurvive(int mode)
        {
            var path=Path.Combine(Path.GetTempPath(),"unified-arm-history-"+Guid.NewGuid()+".json");
            try
            {
                var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateTrooperDefault()}};
                var history=new DesignLabHistory(path,old,authoring:mode==2);
                if(mode==1)history.Create("Intentional arm pose");else history.Rename(mode==2?"Authored default":"Default");
                var source=history.Selected.Snapshot;source.Set("grip.RocketLauncher.supportYaw",80); // actual pre-Mk2 scene value, not a new trusted schema
                history.Save(source);
                string hash=history.Selected.Hash,bytes=JsonUtility.ToJson(history.Selected.Snapshot);
                var migrated=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(migrated.StorageError,Is.Null);
                foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
                {
                    var prefix="grip."+weapon+".";float expected=mode==0||weapon==WeaponId.Shotgun?1:0;
                    Assert.That(migrated.Selected.Snapshot.Get(prefix+"wristAlignment"),Is.EqualTo(expected),prefix+"left");
                    Assert.That(migrated.Selected.Snapshot.Get(prefix+"primaryWristAlignment"),Is.EqualTo(expected),prefix+"right");
                }
                foreach(var d in old.Descriptors.Where(d=>!d.Path.EndsWith(".wristAlignment")))Assert.That(migrated.Selected.Snapshot.Get(d.Path),Is.EqualTo(mode==0&&(d.Path.StartsWith("grip.RocketLauncher.")||d.Path=="view.switchShoulderDegrees")?current.Get(d.Path):source.Get(d.Path)),d.Path);
                var originals=migrated.Profiles.SelectMany(p=>p.Revisions).Where(r=>r.Hash==hash).ToArray();Assert.That(originals,Is.Not.Empty);
                foreach(var revision in originals)Assert.That(JsonUtility.ToJson(revision.Snapshot),Is.EqualTo(bytes));
                var reopened=new DesignLabHistory(path,current,authoring:mode==2);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
    }
}
