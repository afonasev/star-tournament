using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabCompatibilityViewTests
    {
        string directory,path,id,originalHash;LabBundle baseline;LabReleaseCatalog seed;
        const string Interval="presentation.botBanterIntervalSeconds";
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-lab-view-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        LabBundle Default(float interval){var copy=baseline.Clone();copy.Set(Interval,interval);return copy;}
        DesignLabHistory Open(bool author)=>new DesignLabHistory(path,baseline,releases:seed,authoring:author);
        DesignLabHistory Prepare(bool author)
        {
            baseline=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateBloodDefault()}};
            var before=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",BindingFlags.Instance|BindingFlags.NonPublic);
            var previous=new LabBundle{Profiles=baseline.Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList()};
            var old=new DesignLabHistory(path,previous);old.Create("Default");id=old.SelectedProfileId;
            var draft=old.Selected.Snapshot;draft.Set("blood.enabled",0);draft.Set("audio.effectsDefaultPercent",0);old.Save(draft);originalHash=old.Selected.Hash;
            seed=LabReleaseCatalog.Factory(Default(30));
            if(author)
            {
                var file=DesignLabHistoryTests.ReadFixture(path,previous);var snapshot=Default(30);
                file.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Add(new LabRevision{Number=5,Hash=snapshot.Hash(),Snapshot=snapshot,ReleaseCandidate=true});
                File.WriteAllText(path,JsonUtility.ToJson(file));
            }
            var bytes=File.ReadAllBytes(path);var history=Open(author);
            Assert.That(history.StorageError,Is.Null);Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes),"Catalogue-backed read is not a disk transaction even with default persistMigration=true");
            Assert.That(history.Selected.Snapshot.Get(Interval),Is.EqualTo(30));
            Assert.That(history.Selected.CompatibilitySourceHash,Is.EqualTo(originalHash));
            history.Select(id,history.Selected.Number);
            Assert.That(JsonUtility.FromJson<LabParameterEnvelope>(File.ReadAllText(path)).Schema,Is.EqualTo(3));
            return history;
        }
        void NextDefault(DesignLabHistory history,bool author,int localVersion)
        {
            if(author)
            {
                history.Select(DesignLabHistory.ReleaseId,5);var draft=history.Selected.Snapshot;draft.Set(Interval,40);history.Save(draft);history.MarkSelectedForRelease(true);
                history.Select(id,localVersion);
            }
            else{seed=LabReleaseCatalog.Factory(Default(40));seed.Entries[0].Revision=2;}
        }
        [TestCase(false,false)][TestCase(false,true)][TestCase(true,false)][TestCase(true,true)]
        public void RepeatedUpgradeRebuildsDerivedFieldsButPreservesExplicitSavedValues(bool author,bool save)
        {
            var history=Prepare(author);
            if(save){var draft=history.Selected.Snapshot;draft.Set("rifle.damage",32);history.Save(draft);Assert.That(history.Selected.CompatibilitySourceNumber,Is.Zero);}
            int number=history.Selected.Number;NextDefault(history,author,number);var bytes=File.ReadAllBytes(path);
            var updated=Open(author);Assert.That(updated.StorageError,Is.Null);
            Assert.That(updated.Selected.Number,Is.EqualTo(number),"Technical storage key stays stable across repeated interpretation");
            Assert.That(updated.Selected.Snapshot.Get(Interval),Is.EqualTo(save?30:40));
            Assert.That(updated.Selected.Snapshot.Get("blood.enabled"),Is.Zero);Assert.That(updated.Selected.Snapshot.Get("audio.effectsDefaultPercent"),Is.Zero);
            Assert.That(updated.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Hash==originalHash).Snapshot.Descriptors.Any(d=>d.Path==Interval),Is.False);
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
            int count=updated.SelectedProfile.Revisions.Count;updated.Select(id,number);
            var again=Open(author);Assert.That(again.StorageError,Is.Null);Assert.That(again.SelectedProfile.Revisions.Count,Is.EqualTo(count));
            Assert.That(again.Selected.Snapshot.Get(Interval),Is.EqualTo(save?30:40));
        }
        [TestCase(false)][TestCase(true)]public void ExplicitMarkFreezesFullViewForFutureRelease(bool author)
        {
            var history=Prepare(author);int number=history.Selected.Number;
            history.MarkSelectedForRelease(true);Assert.That(history.Selected.CompatibilitySourceNumber,Is.Zero);
            NextDefault(history,author,number);var updated=Open(author);Assert.That(updated.StorageError,Is.Null);
            Assert.That(updated.Selected.Snapshot.Get(Interval),Is.EqualTo(30));Assert.That(updated.Selected.ReleaseCandidate,Is.True);
            Assert.That(updated.LatestAvailableRevision(id),Is.EqualTo(number),"A newer technical cache cannot shadow an explicitly saved version in the profile selector");
            if(author)
            {
                var entry=updated.BuildReleaseCatalog().Entries.Single(e=>e.ProfileId==id);
                Assert.That(entry.Revision,Is.EqualTo(number));Assert.That(entry.Snapshot.Get(Interval),Is.EqualTo(30));
            }
        }
        static void WriteEnvelope(string path,LabParameterEnvelope file)
        {
            file.Integrity=(string)typeof(DesignLabHistory).GetMethod("CompatibilityIntegrity",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{file});
            File.WriteAllText(path,JsonUtility.ToJson(file));
        }
        [Test]public void LegacyRowsWithoutProvenanceAreNotGuessedOrReinterpreted()
        {
            var history=Prepare(false);int number=history.Selected.Number;
            var file=JsonUtility.FromJson<LabParameterEnvelope>(File.ReadAllText(path));
            File.WriteAllText(path,JsonUtility.ToJson(file.History));NextDefault(history,false,number);var bytes=File.ReadAllBytes(path);
            var updated=Open(false);Assert.That(updated.StorageError,Is.Null);Assert.That(updated.Selected.CompatibilitySourceNumber,Is.Zero);
            Assert.That(updated.Selected.Snapshot.Get(Interval),Is.EqualTo(30));Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
        }
        [TestCase("missing")][TestCase("self")][TestCase("hash")][TestCase("duplicate")][TestCase("syntax")][TestCase("chain")][TestCase("inner-integrity")][TestCase("outer-integrity")]
        public void InvalidDerivedSourceMetadataPreservesFileAndDisablesWrites(string kind)
        {
            var history=Prepare(false);var file=JsonUtility.FromJson<LabParameterEnvelope>(File.ReadAllText(path));
            var view=file.Views.Single(v=>v.ProfileId==id&&v.Number==history.Selected.Number);
            if(kind=="missing")view.SourceNumber=999;
            if(kind=="self"){view.SourceNumber=view.Number;view.SourceHash=history.Selected.Hash;}
            if(kind=="hash")view.SourceHash=new string('0',64);
            if(kind=="duplicate")file.Views.Add(view);
            if(kind=="syntax"){view.SourceNumber=0;view.SourceHash="not-a-hash";}
            if(kind=="chain")
            {
                int source=file.Views.First(v=>v.Number!=view.Number).Number;view.SourceNumber=source;
                view.SourceHash=file.History.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==source).Hash;
            }
            if(kind=="inner-integrity")file.History.Integrity="wrong";
            WriteEnvelope(path,file);
            if(kind=="outer-integrity"){file.Integrity="wrong";File.WriteAllText(path,JsonUtility.ToJson(file));}
            var bytes=File.ReadAllBytes(path);
            var invalid=Open(false);Assert.That(invalid.Writable,Is.False);Assert.That(invalid.StorageError,Is.Not.Null);
            Assert.Throws<IOException>(()=>invalid.Create("must not replace source"));Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
        }
    }
}
