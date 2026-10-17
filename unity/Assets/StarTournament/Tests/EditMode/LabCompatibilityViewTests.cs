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
        [TestCase(false)][TestCase(true)]public void MarkChangesOnlySavedSourceFlagWithoutMaterializingOrActivatingView(bool author)
        {
            var history=Prepare(author);int number=history.Selected.Number;var identity=history.SavedIdentity(id,number);var hash=history.Selected.Hash;int count=history.SelectedProfile.Revisions.Count;
            history.MarkSelectedForRelease(true);
            Assert.That(history.Selected.CompatibilitySourceNumber,Is.GreaterThan(0));Assert.That(history.Selected.Hash,Is.EqualTo(hash));
            Assert.That(history.SelectedReleaseMarked,Is.True);Assert.That(history.SelectedProfile.Revisions.Count,Is.EqualTo(count));
            Assert.That(history.SavedIdentity(id,number).Number,Is.EqualTo(identity.Number));Assert.That(history.SavedIdentity(id,number).Hash,Is.EqualTo(identity.Hash));
            NextDefault(history,author,number);var updated=Open(author);Assert.That(updated.StorageError,Is.Null);
            Assert.That(updated.Selected.Snapshot.Get(Interval),Is.EqualTo(40),"Missing fields still follow release Default; a flag did not create a complete saved snapshot");
            Assert.That(updated.SelectedReleaseMarked,Is.True);Assert.That(updated.LatestAvailableRevision(id),Is.EqualTo(number));
            if(author)
            {
                var entry=updated.BuildReleaseCatalog().Entries.Single(e=>e.ProfileId==id);
                Assert.That(entry.Revision,Is.EqualTo(identity.Number));Assert.That(entry.Hash,Is.EqualTo(identity.Hash));
                Assert.That(entry.Snapshot.Descriptors.Any(d=>d.Path==Interval),Is.False);
            }
        }
        [TestCase("20000102")][TestCase("")]
        public void ExecutionViewReportsOriginalSavedIdentityAndExplicitSaveGetsItsOwnIdentity(string date)
        {
            var h=Prepare(false);int view=h.Selected.Number;int number=h.Selected.CompatibilitySourceNumber;
            var file=DesignLabHistoryTests.ReadFixture(path,baseline);var source=file.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==number);
            source.Date=date;File.WriteAllText(path,JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);
            var loaded=Open(false);var identity=loaded.SavedIdentity(id,view);
            Assert.That(identity.Number,Is.EqualTo(number));Assert.That(identity.Date,Is.EqualTo(date));Assert.That(identity.Hash,Is.EqualTo(originalHash));
            Assert.That(identity.Label,Is.EqualTo("v"+number+"-"+(date==""?"без-даты":date)));
            Assert.That(loaded.Selected.IsCompatibilityView,Is.True);Assert.That(loaded.Selected.Hash,Is.Not.EqualTo(identity.Hash));
            Assert.That(loaded.SavedIdentity(id,number).Hash,Is.EqualTo(identity.Hash));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            var draft=loaded.Selected.Snapshot;draft.Set("rifle.damage",32);loaded.Save(draft);
            var saved=loaded.SavedIdentity(id,loaded.Selected.Number);
            Assert.That(saved.Number,Is.EqualTo(loaded.Selected.Number));Assert.That(saved.Hash,Is.EqualTo(loaded.Selected.Hash));
            Assert.That(loaded.Selected.IsCompatibilityView,Is.False);Assert.That(loaded.SavedIdentity(id,number).Date,Is.EqualTo(date));
        }
        [Test]public void PackagedSavedIdentityUsesPublicCatalogueNumberAndUnknownDate()
        {
            baseline=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()}};
            seed=LabReleaseCatalog.Factory(baseline);seed.Entries[0].Revision=7;seed.Entries[0].Date="";
            var h=DesignLabHistory.ResolvePublishedDefault(baseline,seed);var identity=h.SavedIdentity(h.SelectedProfileId,h.Selected.Number);
            Assert.That(h.Selected.Number,Is.Not.EqualTo(7));Assert.That(identity.Number,Is.EqualTo(7));
            Assert.That(identity.Label,Is.EqualTo("v7-без-даты"));Assert.That(identity.Hash,Is.EqualTo(seed.Entries[0].Hash));
        }
        [TestCase(false)][TestCase(true)]
        public void MixedHistoryMigratesEveryCompatibleSourceWithoutBlockingValidSelection(bool incompatible)
        {
            baseline=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault(),incompatible?ProvingProfile.CreateTrooperDefault():ProvingProfile.CreateBloodDefault()}};
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var before=typeof(ProvingProfile).GetMethod(incompatible?"BeforeLowWeaponSwitch":"BeforeBotBanterInterval",flags);
            var rebalance=typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",flags);
            var previous=new LabBundle{Profiles=baseline.Profiles.Select(p=>(ProvingProfile)before.Invoke(incompatible?rebalance.Invoke(p,null):p,null)).ToList()};
            var old=new DesignLabHistory(path,previous);old.Create("Mixed history");id=old.SelectedProfileId;
            var file=DesignLabHistoryTests.ReadFixture(path,previous);var local=file.Profiles.Single(p=>p.Id==id);var source=local.Revisions.Single();source.Date="20000102";
            var saved=baseline.Clone();saved.Set("rifle.damage",32);
            local.Revisions.Add(new LabRevision{Number=9,Date="20261017",Hash=saved.Hash(),Snapshot=saved});
            file.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Add(new LabRevision{Number=2,Hash=baseline.Hash(),Snapshot=baseline.Clone()});
            file.SelectedRevision=9;File.WriteAllText(path,JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);
            seed=LabReleaseCatalog.Factory(baseline);var loaded=Open(false);
            Assert.That(loaded.StorageError,Is.Null);Assert.That(loaded.Writable,Is.True);Assert.That(loaded.Selected.Number,Is.EqualTo(9));
            Assert.That(loaded.Selected.Hash,Is.EqualTo(saved.Hash()));Assert.That(loaded.LatestAvailableRevision(id),Is.EqualTo(9));
            var original=loaded.SelectedProfile.Revisions.Single(r=>r.Number==source.Number);
            Assert.That(original.Hash,Is.EqualTo(source.Hash));Assert.That(original.Date,Is.EqualTo(source.Date));
            Assert.That(JsonUtility.ToJson(original.Snapshot),Is.EqualTo(JsonUtility.ToJson(source.Snapshot)));
            var views=loaded.SelectedProfile.Revisions.Where(r=>r.CompatibilitySourceNumber==source.Number).ToArray();
            if(incompatible)
            {
                Assert.That(views,Is.Empty);Assert.That(loaded.Compatible(original),Is.False);
                Assert.That(loaded.CompatibilityReason(id,source.Number),Does.Contain("view.switchShoulderDegrees"));
                Assert.Throws<InvalidOperationException>(()=>loaded.Select(id,source.Number));
            }
            else
            {
                Assert.That(views.Length,Is.EqualTo(1));Assert.That(loaded.Compatible(views[0]),Is.True);
                Assert.That(loaded.SavedIdentity(id,views[0].Number).Label,Is.EqualTo("v1-20000102"));
                Assert.That(views[0].Snapshot.Get(Interval),Is.EqualTo(baseline.Get(Interval)));
            }
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            var again=Open(false);Assert.That(again.Selected.Hash,Is.EqualTo(saved.Hash()));Assert.That(again.StorageError,Is.Null);
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
