using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeAchievementHistoryTests
    {
        string directory,path;
        static LabBundle Shipped()=>DesignLabHistoryTests.Shipped();
        static ProvingProfile Before(ProvingProfile profile,string method)=>
            (ProvingProfile)typeof(ProvingProfile).GetMethod(method,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(profile,null);

        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-achievement-history-"+Guid.NewGuid());Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}

        [TestCase(false)][TestCase(true)]
        public void AchievementThresholdUpgradePreservesHistoricalMatch(bool removeModeTargets)
        {
            var old=Shipped();var match=old.Profiles.Single(p=>p.Id=="unity-native-match-v1");
            match=Before(match,"BeforeMatchAchievements");
            if(removeModeTargets)match=Before(match,"BeforeModeTargets");
            old.Profiles[old.Profiles.FindIndex(p=>p.Id==match.Id)]=match;
            Assert.That(match.Descriptor("achievement.minimumShots"),Is.Null);
            Assert.That(match.Descriptor("achievement.minimumBeamSeconds"),Is.Null);

            var historical=new DesignLabHistory(path,old);historical.Create("Мой скоринг");
            var custom=historical.Selected.Snapshot;custom.Set("score.assistPoints",123);historical.Save(custom);
            string oldHash=custom.Hash(),oldJson=JsonUtility.ToJson(custom);
            var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("achievement.minimumShots"),Is.EqualTo(10));
            Assert.That(upgraded.Selected.Snapshot.Get("achievement.minimumBeamSeconds"),Is.EqualTo(1));
            Assert.That(upgraded.Selected.Snapshot.Get("score.assistPoints"),Is.EqualTo(123));
            var source=upgraded.SelectedProfile.Revisions.Single(r=>r.Hash==oldHash);
            Assert.That(JsonUtility.ToJson(source.Snapshot),Is.EqualTo(oldJson));
            Assert.That(source.Snapshot.Hash(),Is.EqualTo(oldHash));
            Assert.That(historical.Selected.Hash,Is.EqualTo(oldHash));
        }

        [Test]
        public void HistoricalAchievementReleaseLoadsIdempotently()
        {
            var shipped=Shipped();var old=shipped.Clone();
            int index=old.Profiles.FindIndex(p=>p.Id=="unity-native-match-v1");
            var match=Before(Before(old.Profiles[index],"BeforeMatchAchievements"),"BeforeModeTargets");
            match.Set("score.assistPoints",123);old.Profiles[index]=match;
            var catalog=LabReleaseCatalog.Factory(shipped);
            catalog.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId="historical-match",ProfileName="Historical",Revision=1,Date="20261008",Hash=old.Hash(),Snapshot=old.Clone()});

            var first=new DesignLabHistory(path,shipped,releases:catalog);
            Assert.That(first.StorageError,Is.Null);
            var source=first.Profiles.Single(p=>p.Id=="historical-match").Revisions.Single(r=>r.Hash==old.Hash());
            Assert.That(source.Snapshot.Hash(),Is.EqualTo(old.Hash()));
            Assert.That(first.Profiles.Single(p=>p.Id=="historical-match").Revisions.Any(r=>r.Hash!=old.Hash()&&r.Snapshot.Get("achievement.minimumShots")==10),Is.True);

            var second=new DesignLabHistory(path,shipped,releases:catalog);
            Assert.That(second.StorageError,Is.Null);
            var revisions=second.Profiles.Single(p=>p.Id=="historical-match").Revisions;
            Assert.That(revisions.Count(r=>r.Hash==old.Hash()),Is.EqualTo(1));
            Assert.That(revisions.Count(r=>r.Hash!=old.Hash()&&r.Snapshot.Get("achievement.minimumShots")==10),Is.EqualTo(1));
        }


        [TestCase("achievement.minimumShots")][TestCase("score.assistPoints")]
        public void UnrecognizedPartialHistoricalRegistryStillFailsBeforeWriting(string missing)
        {
            var shipped=Shipped();var old=shipped.Clone();var profile=old.Profiles.Single(p=>p.Id=="unity-native-match-v1");
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var descriptors=(System.Collections.Generic.List<NumericDescriptor>)typeof(ProvingProfile).GetField("descriptors",flags).GetValue(profile);
            var values=(System.Collections.Generic.List<ProvingProfileValue>)typeof(ProvingProfile).GetField("values",flags).GetValue(profile);
            descriptors.RemoveAll(d=>d.Path==missing);values.RemoveAll(v=>v.Path==missing);
            var catalog=LabReleaseCatalog.Factory(shipped);
            catalog.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId="corrupt",ProfileName="Corrupt",Revision=1,Date="20261008",Hash=old.Hash(),Snapshot=old});
            Assert.Throws<InvalidDataException>(()=>new DesignLabHistory(path,shipped,releases:catalog));
            Assert.That(File.Exists(path),Is.False,"Rejected catalog cannot write local history");
        }

        [Test]
        public void AchievementTransformLeavesOtherAndAlreadyHistoricalProfilesUntouched()
        {
            var other=ProvingProfile.CreateDefault();
            Assert.That(Before(other,"BeforeMatchAchievements"),Is.SameAs(other));
            var current=ProvingProfile.CreateMatchDefault();
            Assert.That(Before(Before(current,"BeforeMatchAchievements"),"BeforeMatchAchievements").Descriptors.Count,
                Is.EqualTo(current.Descriptors.Count-2));
            var old=Before(current,"BeforeMatchAchievements");
            Assert.That(Before(old,"BeforeMatchAchievements"),Is.SameAs(old));
            Assert.That(old.Get("score.chainTotal1"),Is.EqualTo(current.Get("score.chainTotal1")));
        }
    }
}
