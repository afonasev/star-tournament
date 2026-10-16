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
            baseline=WithBlood();var previous=baseline.Clone();
            previous.Profiles=previous.Profiles.Select(p=>p.BeforeBloodIntensityIncrease()).ToList();seed=LabReleaseCatalog.Factory(previous);
            var old=new DesignLabHistory(path,previous,releases:seed,authoring:true,resetToLatestDefault:true,persistMigration:false);
            old.Rename("Author Default");string original=old.Selected.Hash;var bytes=File.ReadAllBytes(path);
            var next=Open();Assert.That(next.Writable,Is.True);Assert.That(next.ReleaseError,Is.Not.Null);
            int selectedBeforeMarks=next.Selected.Number;string selectedHash=next.Selected.Hash;
            Assert.That(next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.Single(r=>r.Number==1).ReleaseCandidate,Is.True);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path),"Schema adaptation is in memory, not a rewrite of the project file");
            Assert.Throws<InvalidOperationException>(()=>next.MarkForRelease(DesignLabHistory.ReleaseId,1,true));
            next.MarkForRelease(DesignLabHistory.ReleaseId,1,false);
            var compatible=next.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions.First(r=>next.Compatible(r));
            next.MarkForRelease(DesignLabHistory.ReleaseId,compatible.Number,true);
            Assert.That(next.Selected.Number,Is.EqualTo(selectedBeforeMarks),"Changing flags does not activate a new version");
            Assert.That(next.Selected.Hash,Is.EqualTo(selectedHash));
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
        [Test] public void PackagedDefaultUsesRevisionOrderAndPreservesItsAuthoredName()
        {
            var v3=baseline.Clone();v3.Set("rifle.damage",31);var v2=baseline.Clone();v2.Set("rifle.damage",29);
            seed=LabReleaseCatalog.Factory(baseline);seed.Entries[0].ProfileName="Named Default";
            seed.Entries.Add(new LabReleaseEntry{Sequence=2,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Named Default",Revision=3,Date="",Hash=v3.Hash(),Snapshot=v3});
            seed.Entries.Add(new LabReleaseEntry{Sequence=3,ProfileId=DesignLabHistory.ReleaseId,ProfileName="Named Default",Revision=2,Date="",Hash=v2.Hash(),Snapshot=v2});
            var h=new DesignLabHistory(path,baseline,releases:seed,resetToLatestDefault:true,persistMigration:false);
            Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(3));Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(h.SelectedProfileName,Is.EqualTo("Named Default"));
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
