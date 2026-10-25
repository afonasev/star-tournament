using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabAuthoringTests
    {
        string directory,path;LabBundle baseline;LabReleaseCatalog seed;
        [SetUp] public void Setup()
        {
            directory=Path.Combine(Path.GetTempPath(),"st-lab-authoring-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            path=Path.Combine(directory,"history.json");baseline=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()}};
            seed=LabReleaseCatalog.Factory(baseline);
            var other=baseline.Clone();other.Set("rifle.damage",29);
            seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId="packaged",ProfileName="Packaged",Revision=1,Date="",Hash=other.Hash(),Snapshot=other});
        }
        [TearDown] public void Cleanup(){Directory.Delete(directory,true);}
        DesignLabHistory Open(bool reset=true,Action<string,string> publish=null)=>new DesignLabHistory(path,baseline,releases:seed,
            resetToLatestDefault:reset,persistMigration:false,authoring:true,atomicPublish:publish);
        static void SaveDamage(DesignLabHistory h,float damage){var draft=h.Selected.Snapshot;draft.Set("rifle.damage",damage);h.Save(draft);}
        static LabBundle WithBlood()=>new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateBloodDefault()}};
        [Test] public void ChangedDefaultRefreshesOnlyDerivedMissingFieldsBeforeStaging()
        {
            var before=typeof(ProvingProfile).GetMethod("BeforeShotgunTracerColor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var previous=new LabBundle{Profiles=baseline.Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList()};
            var originalCatalog=LabReleaseCatalog.Factory(previous);
            var old=new DesignLabHistory(path,previous,releases:originalCatalog,authoring:true);old.PersistAuthoring();var source=old.Selected;
            string json=UnityEngine.JsonUtility.ToJson(source.Snapshot);
            var h=new DesignLabHistory(path,baseline,releases:originalCatalog,authoring:true,persistMigration:false);
            var draft=h.Selected.Snapshot;draft.Set("presentation.shotTracerRed",.32f);h.Save(draft);h.MarkSelectedForRelease(true);
            int revision=h.Selected.Number;string hash=h.Selected.Hash;
            h.RefreshAuthoringCompatibility();
            Assert.That(h.Selected.Number,Is.EqualTo(revision));Assert.That(h.Selected.Hash,Is.EqualTo(hash));
            Assert.That(UnityEngine.JsonUtility.ToJson(h.SelectedProfile.Revisions.Single(r=>r.Number==source.Number).Snapshot),Is.EqualTo(json));
            var catalog=h.BuildReleaseCatalog();Assert.That(catalog.Entries.First().Hash,Is.EqualTo(originalCatalog.Entries.First().Hash));
            Assert.That(DesignLabHistory.ResolvePublishedDefault(baseline,catalog).Selected.Hash,Is.EqualTo(hash));
        }
        [Test] public void AuthorCanEditAndRenamePackageProfilesButCannotDeleteDefault()
        {
            var h=Open();string original=h.Selected.Hash;
            Assert.That(h.IsAuthoring,Is.True);Assert.That(h.SelectedProfileReadOnly,Is.False);Assert.That(h.SelectedProfileProtected,Is.True);
            SaveDamage(h,31);Assert.That(h.Selected.Number,Is.EqualTo(2));h.MarkSelectedForRelease(true);h.Rename("Game Default");
            Assert.That(h.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Single(r=>r.Number==1).Hash,Is.EqualTo(original));
            Assert.Throws<InvalidOperationException>(()=>h.DeleteSelected());
            h.Select("packaged",1);Assert.That(h.SelectedProfileReadOnly,Is.False);Assert.That(h.SelectedProfileProtected,Is.False);
            SaveDamage(h,33);h.Rename("Edited package");h.DeleteSelected();
            Assert.That(h.SelectedProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));Assert.That(h.Selected.Number,Is.EqualTo(2));
            Assert.That(h.SelectedProfileName,Is.EqualTo("Game Default"));
        }
        [Test] public void StartupAndResetChooseHighestMarkedDefaultRevisionNotLastMarkerClick()
        {
            var h=Open();SaveDamage(h,31);h.MarkSelectedForRelease(true);SaveDamage(h,32);
            Assert.That(h.Selected.Number,Is.EqualTo(3));Assert.That(h.Selected.ReleaseCandidate,Is.False);
            h.Select(DesignLabHistory.ReleaseId,1);h.MarkSelectedForRelease(false);h.MarkSelectedForRelease(true);
            Assert.That(h.Selected.Number,Is.EqualTo(1),"Changing flags must not activate another revision");
            h.ResetToRelease();Assert.That(h.Selected.Number,Is.EqualTo(2));
            h.Select(DesignLabHistory.ReleaseId,3);var before=File.ReadAllBytes(path);
            var reopened=Open();Assert.That(reopened.Selected.Number,Is.EqualTo(2));CollectionAssert.AreEqual(before,File.ReadAllBytes(path),"Startup selection is in memory");
        }
        [Test] public void MissingMarkedDefaultPersistsAndRemainsEditableWithoutReseedingFlags()
        {
            var h=Open();h.MarkSelectedForRelease(false);var bytes=File.ReadAllBytes(path);var selected=h.Selected.Hash;
            Assert.That(h.ReleaseError,Is.Not.Null);Assert.That(h.Writable,Is.True);
            Assert.Throws<InvalidOperationException>(()=>h.ResetToRelease());Assert.Throws<InvalidOperationException>(()=>h.BuildReleaseCatalog());
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));Assert.That(h.Selected.Hash,Is.EqualTo(selected));
            h=Open();Assert.That(h.ReleaseError,Is.Not.Null);Assert.That(h.Selected.ReleaseCandidate,Is.False);
            SaveDamage(h,31);h.MarkSelectedForRelease(true);Assert.That(h.ReleaseError,Is.Null);h.ResetToRelease();Assert.That(h.Selected.Number,Is.EqualTo(2));
        }
        [Test] public void ExistingAuthorFilesOwnRenamesDeletionAndUnmarks()
        {
            var h=Open();h.Rename("Author Default");h.MarkSelectedForRelease(false);
            h.Select("packaged",1);h.MarkSelectedForRelease(false);
            // Re-enable Default so deleting the other profile can return to a valid release.
            h.Select(DesignLabHistory.ReleaseId,1);h.MarkSelectedForRelease(true);h.Select("packaged",1);h.DeleteSelected();
            var bytes=File.ReadAllBytes(path);var reopened=Open();
            Assert.That(reopened.SelectedProfileName,Is.EqualTo("Author Default"));Assert.That(reopened.Profiles.Any(p=>p.Id=="packaged"),Is.False);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test] public void ProjectionIncludesAllAndOnlyMarkedSavedVersionsWithoutWritingOrSharingSnapshots()
        {
            var h=Open();SaveDamage(h,31);h.MarkSelectedForRelease(true);h.Create("Local");string local=h.SelectedProfileId;
            Assert.That(h.Selected.ReleaseCandidate,Is.False);SaveDamage(h,33);h.MarkSelectedForRelease(true);
            var bytes=File.ReadAllBytes(path);var catalog=h.BuildReleaseCatalog();
            Assert.That(catalog.Entries.Select(e=>e.ProfileId+"/"+e.Revision),Is.EquivalentTo(new[]{DesignLabHistory.ReleaseId+"/1",DesignLabHistory.ReleaseId+"/2","packaged/1",local+"/2"}));
            catalog.Entries.First(e=>e.ProfileId==local).Snapshot.Set("rifle.damage",34);
            Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(33));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            h.Select("packaged",1);h.MarkSelectedForRelease(false);Assert.That(h.BuildReleaseCatalog().Entries.Any(e=>e.ProfileId=="packaged"),Is.False);
        }
        [Test] public void FailedFlagPublicationPreservesSourceSelectionAndPreviousMark()
        {
            var h=Open();h.Rename("Saved");var bytes=File.ReadAllBytes(path);h=Open(publish:(a,b)=>throw new IOException("injected"));
            string hash=h.Selected.Hash;Assert.Throws<IOException>(()=>h.MarkSelectedForRelease(false));
            Assert.That(h.Selected.ReleaseCandidate,Is.True);Assert.That(h.Selected.Hash,Is.EqualTo(hash));Assert.That(h.ReleaseError,Is.Null);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));Assert.That(Directory.GetFiles(directory,"*.tmp"),Is.Empty);
        }
        [Test] public void ProjectionPreservesKnownDatesAndDoesNotInventUnknownDates()
        {
            seed.Entries[0].Date="20260101";var h=Open();var output=h.BuildReleaseCatalog();
            Assert.That(output.Entries.Single(e=>e.ProfileId==DesignLabHistory.ReleaseId).Date,Is.EqualTo("20260101"));
            Assert.That(string.IsNullOrEmpty(output.Entries.Single(e=>e.ProfileId=="packaged").Date),Is.True);
            Assert.That(File.Exists(path),Is.False,"Projection does not persist the seed or publish anything");
        }
        [Test] public void ProductionStillProtectsProfilesAndRejectsAuthorProjection()
        {
            var h=new DesignLabHistory(path,baseline,releases:seed,resetToLatestDefault:true,persistMigration:false);
            Assert.That(h.IsAuthoring,Is.False);Assert.That(h.SelectedProfileReadOnly,Is.True);
            Assert.Throws<InvalidOperationException>(()=>h.Rename("Forbidden"));Assert.Throws<InvalidOperationException>(()=>h.MarkSelectedForRelease(false));
            Assert.Throws<InvalidOperationException>(()=>h.BuildReleaseCatalog());Assert.That(File.Exists(path),Is.False);
            Assert.Throws<InvalidOperationException>(()=>h.MarkForRelease(DesignLabHistory.ReleaseId,1,false));
        }
        [Test] public void ReopeningAuthorDataDoesNotRebindFlagsOnHistoricalReleaseCounterparts()
        {
            baseline=WithBlood();var old=baseline.Clone();
            old.Profiles=old.Profiles.Select(p=>p.BeforeBloodIntensityIncrease()).ToList();
            seed=LabReleaseCatalog.Factory(baseline);seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId="older",ProfileName="Older",Revision=1,Date="",Hash=old.Hash(),Snapshot=old});
            var h=Open();var current=h.Profiles.Single(p=>p.Id=="older").Revisions.Single(r=>h.Compatible(r));
            Assert.That(current.ReleaseCandidate,Is.True);h.Rename("Stored Default");h=Open();
            Assert.That(h.Profiles.Single(p=>p.Id=="older").Revisions.Single(r=>r.Number==current.Number).ReleaseCandidate,Is.True);
            h.MarkForRelease("older",current.Number,false);h=Open();
            Assert.That(h.Profiles.Single(p=>p.Id=="older").Revisions.Single(r=>r.Number==current.Number).ReleaseCandidate,Is.False);
            Assert.That(h.Profiles.Single(p=>p.Id=="older").Revisions.Any(r=>r.Hash==old.Hash()),Is.True);
        }
        [Test] public void RegistryUpgradePreservesOldMarksAndAllowsExplicitUnmarkWithoutActivatingInvalidVersion()
        {
            baseline=new LabBundle{Profiles=baseline.Clone().Profiles.Concat(new[]{ProvingProfile.CreateWellDefault()}).ToList()};
            var method=typeof(ProvingProfile).GetMethod("BeforeWellDetail",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var previous=new LabBundle{Profiles=baseline.Clone().Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList()};
            // Current Well accepts .8; constrain the future grid to exercise refusal, not factory retuning.
            baseline.Profile("well.fill").Descriptor("well.fill").Step=.05f;
            var old=new DesignLabHistory(path,previous,releases:LabReleaseCatalog.Factory(previous),authoring:true);
            old.Rename("Author Default");string original=old.Selected.Hash;
            var file=DesignLabHistoryTests.ReadFixture(path,previous);
            file.Profiles.Add(new LabHistoryProfile{Id="selected-current",Name="Current",Revisions={new LabRevision{Number=1,Hash=baseline.Hash(),Snapshot=baseline.Clone()}}});
            file.SelectedId="selected-current";file.SelectedRevision=1;
            File.WriteAllText(path,UnityEngine.JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);seed=LabReleaseCatalog.Factory(baseline);
            var next=Open();Assert.That(next.Writable,Is.True);Assert.That(next.ReleaseError,Is.Not.Null);
            int selectedBeforeMarks=next.Selected.Number;string selectedHash=next.Selected.Hash;
            Assert.That(next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Single(r=>r.Number==1).ReleaseCandidate,Is.True);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path),"Schema adaptation is in memory, not a rewrite of the project file");
            Assert.Throws<InvalidOperationException>(()=>next.MarkForRelease(DesignLabHistory.ReleaseId,1,true));
            next.MarkForRelease(DesignLabHistory.ReleaseId,1,false);
            var compatible=next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.First(r=>next.Compatible(r));
            next.MarkForRelease(DesignLabHistory.ReleaseId,compatible.Number,true);
            Assert.That(next.Selected.Number,Is.EqualTo(selectedBeforeMarks),"Changing flags does not activate a new version");
            Assert.That(next.Selected.Hash,Is.EqualTo(selectedHash));Assert.That(next.SelectedProfileId,Is.EqualTo("selected-current"));
            next.ResetToRelease();Assert.That(next.Selected.Number,Is.EqualTo(compatible.Number));
            Assert.That(next.BuildReleaseCatalog().Entries.Single().Hash,Is.EqualTo(compatible.Hash));
            Assert.That(next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Single(r=>r.Number==1).Hash,Is.EqualTo(original));
        }
        [Test] public void HistoricalSeedKeepsDefaultVersionOrderAndOriginalPackageProvenance()
        {
            baseline=WithBlood();var old=baseline.Clone();
            old.Profiles=old.Profiles.Select(p=>p.BeforeBloodIntensityIncrease()).ToList();var v3=old.Clone();v3.Set("rifle.damage",31);var v2=old.Clone();v2.Set("rifle.damage",29);
            seed=LabReleaseCatalog.Factory(baseline);
            seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=3,Date="",Hash=v3.Hash(),Snapshot=v3});
            seed.Entries.Add(new LabReleaseEntry{Sequence=3,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=2,Date="",Hash=v2.Hash(),Snapshot=v2});
            var h=Open();Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(3));Assert.That(h.Selected.ReleaseSequence,Is.EqualTo(2));
            Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(h.IsShipped(h.Selected),Is.True);
            Assert.That(h.Profiles.Single().Revisions.Where(r=>h.Compatible(r)&&h.IsShipped(r)).All(r=>r.ReleaseCandidate),Is.True);
            Assert.That(h.Selected.Hash,Is.EqualTo(DesignLabHistory.ResolvePublishedDefault(baseline,seed).Selected.Hash));
        }
        [Test] public void HistoricalProjectionKeepsPublishedNumbersSnapshotsAndDates()
        {
            baseline=WithBlood();var old=baseline.Clone();old.Profiles=old.Profiles.Select(p=>p.BeforeBloodIntensityIncrease()).ToList();
            var tuned=old.Clone();tuned.Set("rifle.damage",31);seed=LabReleaseCatalog.Factory(old);seed.Entries[0].Date="20260101";
            seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=2,Date="20260102",Hash=tuned.Hash(),Snapshot=tuned});
            var h=Open();var output=h.BuildReleaseCatalog();
            Assert.That(output.Entries.Select(e=>e.Revision),Is.EquivalentTo(new[]{1,2}));
            foreach(var entry in seed.Entries)
            {
                var projected=output.Entries.Single(e=>e.ProfileId==entry.ProfileId&&e.Revision==entry.Revision);
                Assert.That(projected.Hash,Is.EqualTo(entry.Hash));Assert.That(projected.Snapshot.Hash(),Is.EqualTo(entry.Snapshot.Hash()));
                Assert.That(projected.Date,Is.EqualTo(entry.Date));
            }
            Assert.That(DesignLabHistory.ResolvePublishedDefault(baseline,output).Selected.Hash,Is.EqualTo(h.Selected.Hash));
            Assert.That(File.Exists(path),Is.False);
        }
        [TestCase(false)][TestCase(true)] public void UpgradeFillsMissingFieldsFromLatestReleaseDefaultAndKeepsZeroFalseAndOriginal(bool author)
        {
            baseline=WithBlood();var previous=baseline.Clone();
            var before=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            previous.Profiles=previous.Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList();
            var old=new DesignLabHistory(path,previous);old.Create("Default");string localId=old.SelectedProfileId;
            var draft=old.Selected.Snapshot;draft.Set("rifle.damage",31);draft.Set("audio.effectsDefaultPercent",0);draft.Set("blood.enabled",0);old.Save(draft);
            var saved=old.Selected;string original=UnityEngine.JsonUtility.ToJson(saved.Snapshot);
            var chosen=baseline.Clone();chosen.Set("presentation.botBanterIntervalSeconds",30);
            var earlier=baseline.Clone();earlier.Set("presentation.botBanterIntervalSeconds",20);
            seed=LabReleaseCatalog.Factory(baseline);
            seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=5,Date="20261016",Hash=chosen.Hash(),Snapshot=chosen});
            seed.Entries.Add(new LabReleaseEntry{Sequence=3,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=2,Date="20261015",Hash=earlier.Hash(),Snapshot=earlier});
            if(author)
            {
                var file=DesignLabHistoryTests.ReadFixture(path,previous);var defaults=file.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId);
                defaults.Revisions.Add(new LabRevision{Number=5,Date="20261016",Hash=chosen.Hash(),Snapshot=chosen.Clone(),ReleaseCandidate=true});
                var unmarked=baseline.Clone();unmarked.Set("presentation.botBanterIntervalSeconds",40);
                defaults.Revisions.Add(new LabRevision{Number=6,Hash=unmarked.Hash(),Snapshot=unmarked});
                File.WriteAllText(path,UnityEngine.JsonUtility.ToJson(file));
                // Project marks override the package. Do not accidentally pass using seed.
                seed=LabReleaseCatalog.Factory(earlier);
            }
            var bytes=File.ReadAllBytes(path);
            var upgraded=new DesignLabHistory(path,baseline,releases:seed,persistMigration:false,authoring:author);
            Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.SelectedProfileId,Is.EqualTo(localId));
            Assert.That(upgraded.Selected.Snapshot.Get("presentation.botBanterIntervalSeconds"),Is.EqualTo(30));
            Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.effectsDefaultPercent"),Is.Zero);
            Assert.That(upgraded.Selected.Snapshot.Get("blood.enabled"),Is.Zero);
            var preserved=upgraded.Profiles.Single(p=>p.Id==localId).Revisions.Single(r=>r.Number==saved.Number);
            Assert.That(preserved.Hash,Is.EqualTo(saved.Hash));Assert.That(preserved.Date,Is.EqualTo(saved.Date));
            Assert.That(UnityEngine.JsonUtility.ToJson(preserved.Snapshot),Is.EqualTo(original));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
            var full=upgraded.Selected.Snapshot;full.Set("rifle.damage",32);upgraded.Save(full);
            Assert.That(upgraded.Selected.Snapshot.Get("presentation.botBanterIntervalSeconds"),Is.EqualTo(30));
            var reloaded=new DesignLabHistory(path,baseline,releases:seed,persistMigration:false,authoring:author);
            Assert.That(reloaded.Selected.Hash,Is.EqualTo(upgraded.Selected.Hash));
            Assert.That(reloaded.Selected.Snapshot.Get("blood.enabled"),Is.Zero);
        }
        [Test] public void PackagedDefaultUsesRevisionOrderAndPreservesItsAuthoredName()
        {
            var v3=baseline.Clone();v3.Set("rifle.damage",31);var v2=baseline.Clone();v2.Set("rifle.damage",29);
            seed=LabReleaseCatalog.Factory(baseline);seed.Entries[0].ProfileName="Named Default";
            seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Named Default",Revision=3,Date="",Hash=v3.Hash(),Snapshot=v3});
            seed.Entries.Add(new LabReleaseEntry{Sequence=3,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Named Default",Revision=2,Date="",Hash=v2.Hash(),Snapshot=v2});
            var h=new DesignLabHistory(path,baseline,releases:seed,resetToLatestDefault:true,persistMigration:false);
            Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(3));Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(h.SelectedProfileName,Is.EqualTo("Named Default"));
        }
        [TestCase(false)][TestCase(true)]
        public void MixedPublishedVersionsUseCanonicalOrderInsteadOfHigherTechnicalKeys(bool author)
        {
            var method=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            float oldDamage=baseline.Get("rifle.damage")+2,latestDamage=baseline.Get("rifle.damage")+4;
            var previous=new LabBundle{Profiles=baseline.Clone().Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList()};previous.Set("rifle.damage",oldDamage);
            var latest=baseline.Clone();latest.Set("rifle.damage",latestDamage);
            seed=LabReleaseCatalog.Factory(previous);
            seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Default",Revision=7,Date="20070102",Hash=latest.Hash(),Snapshot=latest});
            seed.Entries.Add(new LabReleaseEntry{Sequence=3,ProfileId="packaged",ProfileName="Packaged",Revision=1,Date="20000102",Hash=previous.Hash(),Snapshot=previous.Clone()});
            seed.Entries.Add(new LabReleaseEntry{Sequence=4,ProfileId="packaged",ProfileName="Packaged",Revision=7,Date="20070102",Hash=latest.Hash(),Snapshot=latest.Clone()});
            var h=new DesignLabHistory(path,baseline,releases:seed,authoring:author,resetToLatestDefault:true,persistMigration:false);
            Assert.That(h.StorageError,Is.Null);Assert.That(h.ReleaseError,Is.Null);
            var oldView=h.SelectedProfile.Revisions.Single(r=>r.ReleaseNumber==1&&h.Compatible(r));
            Assert.That(oldView.Number,Is.GreaterThan(h.Selected.Number),"This fixture must reproduce a newer physical key for an older public version");
            Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(7));Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(latestDamage));
            Assert.That(h.ReleaseParameterDefaults().Get("rifle.damage"),Is.EqualTo(latestDamage));
            Assert.That(h.LatestAvailableRevision(DesignLabHistory.ReleaseId),Is.EqualTo(h.Selected.Number));
            var packaged=h.Profiles.Single(p=>p.Id=="packaged");var n=h.LatestAvailableRevision(packaged.Id);
            Assert.That(packaged.Revisions.Single(r=>r.Number==n).ReleaseNumber,Is.EqualTo(7));
            h.Select(DesignLabHistory.ReleaseId,oldView.Number);h.ResetToRelease();Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(7));
            if(author)
            {
                var output=h.BuildReleaseCatalog();
                Assert.That(output.Entries.Single(e=>e.ProfileId==DesignLabHistory.ReleaseId&&e.Revision==1).Hash,Is.EqualTo(previous.Hash()));
                Assert.That(DesignLabHistory.ResolvePublishedDefault(baseline,output).Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(latestDamage));
            }
        }
        [Test]public void NewAuthorSaveFollowsPublicNumberWhenCatalogueUsesEarlierStorageKey()
        {
            seed=LabReleaseCatalog.Factory(baseline);seed.Entries[0].Revision=7;seed.Entries[0].Date="20000102";
            var h=Open();Assert.That(h.Selected.Number,Is.EqualTo(1));Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(7));
            SaveDamage(h,31);Assert.That(h.Selected.Number,Is.EqualTo(8));h.MarkSelectedForRelease(true);h.ResetToRelease();
            Assert.That(h.Selected.Number,Is.EqualTo(8));Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
            var reloaded=Open();Assert.That(reloaded.StorageError,Is.Null);Assert.That(reloaded.Selected.Number,Is.EqualTo(8));
            Assert.That(reloaded.SavedIdentity(DesignLabHistory.ReleaseId,1).Number,Is.EqualTo(7));
            Assert.That(reloaded.SavedIdentity(DesignLabHistory.ReleaseId,1).Date,Is.EqualTo("20000102"));
            Assert.That(reloaded.BuildReleaseCatalog().Entries.Single(e=>e.Revision==7).Hash,Is.EqualTo(baseline.Hash()));
        }
        [Test]public void LegacyLowerOwnNumberRemainsImmutableAndCanBeExplicitlySavedAfterPublicVersion()
        {
            seed=LabReleaseCatalog.Factory(baseline);seed.Entries[0].Revision=7;
            var initial=Open();initial.PersistAuthoring();var file=DesignLabHistoryTests.ReadFixture(path,baseline);
            var own=baseline.Clone();float damage=baseline.Get("rifle.damage")+2;own.Set("rifle.damage",damage);
            var legacy=new LabRevision{Number=2,Date="20000103",Hash=own.Hash(),Snapshot=own,ReleaseCandidate=true};
            file.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Add(legacy);file.SelectedRevision=2;
            File.WriteAllText(path,UnityEngine.JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);
            var h=Open();Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(7),"Startup follows public version order, not previous-session choice");
            Assert.That(h.SavedIdentity(DesignLabHistory.ReleaseId,2).Hash,Is.EqualTo(legacy.Hash));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            h.Select(DesignLabHistory.ReleaseId,2);Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(damage));
            SaveDamage(h,damage+1);Assert.That(h.Selected.Number,Is.EqualTo(8));h.MarkSelectedForRelease(true);
            var restarted=Open();Assert.That(restarted.Selected.Number,Is.EqualTo(8));
            var original=restarted.SelectedProfile.Revisions.Single(r=>r.Number==2);
            Assert.That(original.Hash,Is.EqualTo(legacy.Hash));Assert.That(original.Date,Is.EqualTo(legacy.Date));
            Assert.That(UnityEngine.JsonUtility.ToJson(original.Snapshot),Is.EqualTo(UnityEngine.JsonUtility.ToJson(legacy.Snapshot)));
        }
        [TestCase("BeforeUnifiedBodyDamage")][TestCase("BeforeGamepadLook")][TestCase("BeforeLunarLighting")]
        public void StructuralMappingsPreserveOwnSourceAcrossLocalAndAuthorRoles(string transform)
        {
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public;
            var current=new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateLunarPresentation()}};
            var baseRegistry=current.Clone();
            if(transform=="BeforeUnifiedBodyDamage")
            {
                var rebalance=typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",flags);
                baseRegistry=new LabBundle{Profiles=baseRegistry.Profiles.Select(p=>(ProvingProfile)rebalance.Invoke(p,null)).ToList()};
            }
            if(transform=="BeforeGamepadLook")
            {
                var rebalance=typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",flags);
                var trigger=typeof(ProvingProfile).GetMethod("BeforeGamepadTriggerAim",flags);
                baseRegistry=new LabBundle{Profiles=baseRegistry.Profiles.Select(p=>(ProvingProfile)trigger.Invoke(((ProvingProfile)rebalance.Invoke(p,null)).BeforeFootstepMix(),null)).ToList()};
            }
            var method=typeof(ProvingProfile).GetMethod(transform,flags);
            var previous=new LabBundle{Profiles=baseRegistry.Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).Select(p=>transform=="BeforeGamepadLook"?p.BeforeAudio():p).ToList()};
            for(int mode=0;mode<3;mode++)
            {
                string source=Path.Combine(directory,"structural-"+mode+".json");bool author=mode>0;
                var old=new DesignLabHistory(source,previous,releases:LabReleaseCatalog.Factory(previous),authoring:author);
                if(mode!=2)old.Create("Historical own profile");
                var draft=old.Selected.Snapshot;draft.Set("shot.pellets",11);old.Save(draft);
                var file=DesignLabHistoryTests.ReadFixture(source,previous);
                var original=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single(r=>r.Number==file.SelectedRevision);
                // Reconstruct a file saved by the historical editor, including its now-retired editable fields.
                if(transform=="BeforeUnifiedBodyDamage"){original.Snapshot.Set("rifle.head",44);original.Snapshot.Set("rifle.torso",27);original.Snapshot.Set("shot.torso",81);}
                if(transform=="BeforeGamepadLook")original.Snapshot.Set("input.gamepadDegreesPerSecond",90);
                if(transform=="BeforeLunarLighting"){original.Snapshot.Set("lunar.officeLamp",15.5f);original.Snapshot.Set("lunar.officeFill",4.25f);}
                original.Date="20010203";original.Hash=original.Snapshot.Hash();original.ReleaseCandidate=true;
                File.WriteAllText(source,UnityEngine.JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(source);
                var h=new DesignLabHistory(source,current,releases:LabReleaseCatalog.Factory(current),authoring:author,persistMigration:false);
                Assert.That(h.StorageError,Is.Null,transform+" mode="+mode);
                var effective=h.Selected.Snapshot;
                if(transform=="BeforeUnifiedBodyDamage"){Assert.That(effective.Get("rifle.damage"),Is.EqualTo(27));Assert.That(effective.Get("shot.damage"),Is.EqualTo(81));Assert.That(effective.Descriptors.Any(d=>d.Path=="rifle.head"),Is.False);}
                if(transform=="BeforeGamepadLook"){Assert.That(effective.Get(GamepadLookSettings.HorizontalPath),Is.EqualTo(90));Assert.That(effective.Get(GamepadLookSettings.VerticalPath),Is.EqualTo(90));}
                if(transform=="BeforeLunarLighting"){Assert.That(effective.Get("lunar.stairLamp"),Is.EqualTo(15.5f));Assert.That(effective.Get("lunar.stairFill"),Is.EqualTo(4.25f));Assert.That(effective.Get("lunar.officeLamp"),Is.EqualTo(15.5f));}
                Assert.That(effective.Get("shot.pellets"),Is.EqualTo(11));
                var retained=h.SelectedProfile.Revisions.Single(r=>r.Number==original.Number);
                Assert.That(retained.Hash,Is.EqualTo(original.Hash));Assert.That(retained.Date,Is.EqualTo(original.Date));
                Assert.That(UnityEngine.JsonUtility.ToJson(retained.Snapshot),Is.EqualTo(UnityEngine.JsonUtility.ToJson(original.Snapshot)));
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(source));
                h.Save(effective);Assert.That(h.Selected.IsCompatibilityView,Is.False);Assert.That(h.Selected.ReleaseCandidate,Is.False);
                var restart=new DesignLabHistory(source,current,releases:LabReleaseCatalog.Factory(current),authoring:author,persistMigration:false);
                Assert.That(restart.StorageError,Is.Null);Assert.That(restart.Selected.Hash,Is.EqualTo(effective.Hash()));
            }
        }
        [TestCase(false)][TestCase(true)]
        public void HistoricalMarkedDefaultRunsThroughSourceViewAndFlagsDoNotCreateVersions(bool publicSeven)
        {
            var before=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var previous=new LabBundle{Profiles=baseline.Clone().Profiles.Select(p=>(ProvingProfile)before.Invoke(p,null)).ToList()};
            var oldCatalog=LabReleaseCatalog.Factory(previous);if(publicSeven)oldCatalog.Entries[0].Revision=7;
            var old=new DesignLabHistory(path,previous,releases:oldCatalog,authoring:true);
            if(publicSeven)old.Rename("Author Default");else{SaveDamage(old,31);old.MarkSelectedForRelease(true);}
            var original=old.Selected;var bytes=File.ReadAllBytes(path);
            var h=Open();Assert.That(h.StorageError,Is.Null);Assert.That(h.ReleaseError,Is.Null);
            Assert.That(h.Selected.CompatibilitySourceNumber,Is.EqualTo(original.Number));Assert.That(h.SelectedReleaseMarked,Is.True);
            Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(original.Snapshot.Get("rifle.damage")));
            int view=h.Selected.Number;var identity=h.SavedIdentity(DesignLabHistory.ReleaseId,view);var oldIdentity=old.SavedIdentity(DesignLabHistory.ReleaseId,original.Number);
            Assert.That(identity.Number,Is.EqualTo(oldIdentity.Number));Assert.That(identity.Date,Is.EqualTo(oldIdentity.Date));Assert.That(identity.Hash,Is.EqualTo(oldIdentity.Hash));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            int count=h.SelectedProfile.Revisions.Count;string effective=h.Selected.Hash;
            h.MarkSelectedForRelease(false);Assert.That(h.SelectedReleaseMarked,Is.False);Assert.That(h.Selected.Hash,Is.EqualTo(effective));
            h.MarkSelectedForRelease(true);Assert.That(h.SelectedReleaseMarked,Is.True);Assert.That(h.Selected.Number,Is.EqualTo(view));
            Assert.That(h.SelectedProfile.Revisions.Count,Is.EqualTo(count));Assert.That(h.Selected.CompatibilitySourceNumber,Is.EqualTo(original.Number));
            h.ResetToRelease();Assert.That(h.Selected.Number,Is.EqualTo(view));
            var output=h.BuildReleaseCatalog();Assert.That(output.Entries.Any(e=>e.Revision==oldIdentity.Number&&e.Hash==oldIdentity.Hash),Is.True);
            Assert.That(DesignLabHistory.ResolvePublishedDefault(baseline,output).Selected.Hash,Is.EqualTo(effective));
            Assert.That(h.CanMaterializeSelected,Is.True);h.Save(h.Selected.Snapshot);
            Assert.That(h.Selected.Number,Is.GreaterThanOrEqualTo(publicSeven?8:view+1));Assert.That(h.Selected.IsCompatibilityView,Is.False);
            Assert.That(h.SelectedReleaseMarked,Is.False);Assert.That(h.Selected.Hash,Is.EqualTo(effective));
            var preserved=h.SelectedProfile.Revisions.Single(r=>r.Number==original.Number);
            Assert.That(preserved.Hash,Is.EqualTo(original.Hash));Assert.That(preserved.Date,Is.EqualTo(original.Date));
        }
        [Test]public void ProjectionRefusesLegacyRetuneInsteadOfPublishingDifferentAuthorBalance()
        {
            baseline=new LabBundle{Profiles=baseline.Clone().Profiles.Concat(new[]{ProvingProfile.CreateBloodDefault()}).ToList()};var previous=new LabBundle{Profiles=baseline.Clone().Profiles.Select(p=>p.BeforeBloodIntensityIncrease()).ToList()};
            var old=new DesignLabHistory(path,previous,releases:LabReleaseCatalog.Factory(previous),authoring:true);old.Rename("Author blood");
            seed=LabReleaseCatalog.Factory(baseline);var h=Open();Assert.That(h.ReleaseError,Is.Null);
            Assert.That(h.Selected.Snapshot.Get("blood.drops"),Is.EqualTo(10));var bytes=File.ReadAllBytes(path);var identity=h.SavedIdentity(h.SelectedProfileId,h.Selected.Number);
            Assert.That(Assert.Throws<InvalidOperationException>(()=>h.BuildReleaseCatalog()).Message,Does.Contain("Пакет изменит значения"));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            h.Save(h.Selected.Snapshot);int current=h.Selected.Number;h.MarkSelectedForRelease(true);
            foreach(var source in h.SelectedProfile.Revisions.Where(r=>r.ReleaseCandidate&&r.Number!=current).ToArray())h.MarkForRelease(h.SelectedProfileId,source.Number,false);
            var output=h.BuildReleaseCatalog();Assert.That(output.Entries.Single().Revision,Is.EqualTo(current));
            Assert.That(DesignLabHistory.ResolvePublishedDefault(baseline,output).Selected.Snapshot.Get("blood.drops"),Is.EqualTo(10));
            Assert.That(h.SavedIdentity(h.SelectedProfileId,1).Hash,Is.EqualTo(identity.Hash));
        }
        [TestCase(false)][TestCase(true)] public void ExplicitAuthoredDefaultNameIsNotReplacedByLegacyAlias(bool author)
        {
            seed.Entries[0].ProfileName="Опубликованный профиль";
            var h=new DesignLabHistory(path,baseline,releases:seed,authoring:author,resetToLatestDefault:true,persistMigration:false);
            if(author)h.Rename("Опубликованный профиль");
            Assert.That(h.SelectedProfileName,Is.EqualTo("Опубликованный профиль"));
            Assert.That(h.SelectedProfile.Name,Is.EqualTo("Опубликованный профиль"));
        }
        [Test] public void ProjectStoreUsesProjectFilesAndDoesNotWriteDuringOpen()
        {
            string project=Path.Combine(directory,"unity");Directory.CreateDirectory(Path.Combine(project,"Assets","StarTournament"));File.WriteAllText(Path.Combine(directory,".git"),"gitdir: fixture");
            var h=LabProjectStorage.Open(project,baseline,seed);
            Assert.That(h.StoragePath,Is.EqualTo(Path.Combine(project,"DesignLab","authoring-history-v1.json")));Assert.That(File.Exists(h.StoragePath),Is.False);
            h.Create("Author profile");Assert.That(File.Exists(h.StoragePath),Is.True);
            Assert.That(LabProjectStorage.Open(project,baseline,seed).Profiles.Any(p=>p.Name=="Author profile"),Is.True);
        }
        [Test] public void InstalledOrRelativePathCannotBecomeProjectAuthority()
        {
            string project=Path.Combine(directory,"unity");Directory.CreateDirectory(Path.Combine(project,"Assets","StarTournament"));
            Assert.Throws<IOException>(()=>LabProjectStorage.ResolvePath(project));Assert.Throws<ArgumentException>(()=>LabProjectStorage.ResolvePath("relative"));
            Assert.Throws<ArgumentNullException>(()=>LabProjectStorage.Open(project,baseline,null));
            Assert.That(Directory.GetDirectories(project).Select(Path.GetFileName),Is.EqualTo(new[]{"Assets"}));
        }
    }
}
