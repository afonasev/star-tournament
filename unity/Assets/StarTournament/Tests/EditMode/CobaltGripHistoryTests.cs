using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class CobaltGripHistoryTests
    {
        static ProvingProfile Previous()=> (ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCobaltGrip",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
        [Test] public void DefaultContactsUpgradeButCustomFitAndOtherWeaponsSurvive()
        {
            var before=Previous();var current=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeVectorGrip",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ProvingProfile.CreateTrooperDefault(),null);
            Assert.That(before.Version,Is.EqualTo(21));Assert.That(current.Version,Is.EqualTo(22));
            foreach(var d in before.Descriptors.Where(d=>d.Path!="grip.Rifle.supportZ"&&d.Path!="grip.Rifle.supportY"))
                Assert.That(current.Get(d.Path),Is.EqualTo(before.Get(d.Path)),d.Path);
            before.Set("grip.Rifle.supportY",-.17f);before.EnsureTrooperDescriptors();
            Assert.That(before.Get("grip.Rifle.supportY"),Is.EqualTo(-.17f));
            Assert.That(before.Get("grip.Rifle.supportZ"),Is.EqualTo(current.Get("grip.Rifle.supportZ")));
            Assert.That(before.Validate(),Is.Empty);
        }
        [Test] public void OldReleasedHashAndCustomGripHistoryRemainReadable()
        {
            var old=new LabBundle{Profiles=new List<ProvingProfile>{Previous()}};
            var current=new LabBundle{Profiles=new List<ProvingProfile>{ProvingProfile.CreateTrooperDefault()}};
            string path=Path.Combine(Path.GetTempPath(),"cobalt-history-"+Guid.NewGuid()+".json");
            try
            {
                var history=new DesignLabHistory(path,old);string released=history.Selected.Hash;
                history.Create("Custom rifle grip");var snapshot=history.Selected.Snapshot;snapshot.Set("grip.Rifle.supportZ",.51f);snapshot.Set("grip.Rifle.supportYaw",80);history.Save(snapshot);string custom=history.Selected.Hash;
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("grip.Rifle.supportZ"),Is.EqualTo(.51f));
                Assert.That(migrated.Selected.Snapshot.Get("grip.Rifle.supportYaw"),Is.EqualTo(80));
                var release=migrated.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId);
                Assert.That(release.Revisions.Last().Snapshot.Get("grip.Rifle.supportYaw"),Is.EqualTo(35));
                var hashes=migrated.Profiles.SelectMany(p=>p.Revisions).Select(v=>v.Hash).ToArray();
                Assert.That(hashes,Does.Contain(released));Assert.That(hashes,Does.Contain(custom));
                var reopened=new DesignLabHistory(path,current);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [Test] public void SerializedLegacyRifleDefaultsUpgradeWithoutChangingCustomOrOtherWeapons()
        {
            var factory=ProvingProfile.CreateTrooperDefault();
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeAnatomicalGrips",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(factory,null);
            old.Set("grip.Rifle.supportX",.07f);
            var other=old.Descriptors.Where(d=>d.Path.StartsWith("grip.")&&!d.Path.StartsWith("grip.Rifle.")).ToDictionary(d=>d.Path,d=>old.Get(d.Path));
            old.EnsureTrooperDescriptors();
            Assert.That(old.Get("grip.Rifle.supportX"),Is.EqualTo(.07f));
            foreach(var d in factory.Descriptors.Where(d=>d.Path.StartsWith("grip.Rifle.")&&d.Path!="grip.Rifle.supportX"))
                Assert.That(old.Get(d.Path),Is.EqualTo(d.DefaultValue),d.Path);
            // @23 upgrades untouched Shotgun defaults, @27 also upgrades PULSE and
            // the shared elbow policy. Every remaining contact stays exactly equal.
            foreach(var pair in other)Assert.That(old.Get(pair.Key),Is.EqualTo(pair.Key.StartsWith("grip.Shotgun.")||pair.Key.StartsWith("grip.RocketLauncher.")||pair.Key.EndsWith(".wristAlignment")?factory.Get(pair.Key):pair.Value),pair.Key);
        }
    }
}
