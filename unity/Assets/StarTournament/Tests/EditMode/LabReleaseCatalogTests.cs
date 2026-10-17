using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabReleaseCatalogTests
    {
        string directory,path;
        static LabBundle Baseline()=>new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()}};
        static LabReleaseEntry Entry(int sequence,string id,string name,int number,LabBundle snapshot)=>new LabReleaseEntry{
            Sequence=sequence,ProfileId=id,ProfileName=name,Revision=number,Date="20261007",Hash=snapshot.Hash(),Snapshot=snapshot.Clone()};
        [Test]public void PublishedDefaultResolverIgnoresLocalHistoryAndPreservesCatalogueTuning()
        {
            var baseline=Baseline();var catalogue=LabReleaseCatalog.Factory(baseline);
            var published=baseline.Clone();published.Set("rifle.damage",40);
            catalogue.Entries.Add(Entry(4,DesignLabHistory.ReleaseId,"Default",6,published));
            var alternative=baseline.Clone();alternative.Set("rifle.damage",50);
            catalogue.Entries.Add(Entry(5,"alternative","Alternative",1,alternative));
            var local=new DesignLabHistory(path,baseline);local.Create("Local settings");
            var draft=local.Selected.Snapshot;draft.Set("rifle.damage",60);local.Save(draft);
            var bytes=File.ReadAllBytes(path);
            var resolved=DesignLabHistory.ResolvePublishedDefault(baseline,catalogue);
            Assert.That(resolved.SelectedProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));
            Assert.That(resolved.Selected.ReleaseSequence,Is.EqualTo(4));Assert.That(resolved.Selected.ReleaseNumber,Is.EqualTo(6));
            Assert.That(resolved.Selected.Snapshot.Hash(),Is.EqualTo(published.Hash()));
            Assert.That(resolved.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(40));
            Assert.That(resolved.StartupDiagnostics.FileBytes,Is.Zero);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            var normal=new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true,persistMigration:false);
            Assert.That(normal.Selected.Snapshot.Hash(),Is.EqualTo(resolved.Selected.Snapshot.Hash()));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test,Order(-1)]public void PackagedSceneAndCatalogPreserveEveryPublishedSnapshot()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/StarTournament/Scenes/ProvingGround.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            const string asset="Assets/StarTournament/Resources/LabReleaseCatalog.json";
            var bytes=File.ReadAllBytes(asset);
            try
            {
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                var catalog=LabReleaseCatalog.Load();
                var history=new DesignLabHistory(path,ground.CaptureLabBundle(),releases:catalog,resetToLatestDefault:true);
                Assert.That(history.StorageError,Is.Null);
                Assert.That(history.Compatible(history.Selected),Is.True);
                foreach(var entry in catalog.Entries)
                {
                    var revisions=history.Profiles.Single(p=>p.Id==entry.ProfileId).Revisions;
                    var original=revisions.Single(r=>r.Hash==entry.Hash&&r.ReleaseSequence==entry.Sequence);
                    Assert.That(original.Snapshot.Hash(),Is.EqualTo(entry.Hash));
                    Assert.That(UnityEngine.JsonUtility.ToJson(original.Snapshot),
                        Is.EqualTo(UnityEngine.JsonUtility.ToJson(entry.Snapshot)));
                    Assert.That(revisions.Any(r=>r.ReleaseSequence==entry.Sequence&&history.Compatible(r)),Is.True);
                }
                Assert.That(File.ReadAllBytes(asset),Is.EqualTo(bytes));
            }
            finally {UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
        }
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-release-tests-"+Guid.NewGuid());Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        [Test]public void StartupResetsToLatestDefaultAndKeepsAllProfilesAndExperiments()
        {
            var baseline=Baseline();var catalogue=LabReleaseCatalog.Factory(baseline);
            var tuned=baseline.Clone();tuned.Set("rifle.damage",31);
            catalogue.Entries.Add(Entry(2,DesignLabHistory.ReleaseId,"Default",2,tuned));
            catalogue.Entries.Add(Entry(3,"alternative","Альтернатива",4,baseline));
            var local=new DesignLabHistory(path,baseline);var draft=local.Selected.Snapshot;draft.Set("rifle.damage",32);local.Save(draft);
            var preserved=local.Selected;
            var history=new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true);
            Assert.That(history.SelectedProfileName,Is.EqualTo("Default"));Assert.That(history.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(history.Selected.ReleaseSequence,Is.EqualTo(2));Assert.That(history.Selected.Number,Is.Not.EqualTo(preserved.Number),"Conflicting local v2 must survive");
            Assert.That(history.SelectedProfile.Revisions.Single(r=>r.Number==preserved.Number).Hash,Is.EqualTo(preserved.Hash));
            history.Select("alternative",4);Assert.That(history.SelectedProfileName,Is.EqualTo("Альтернатива"));
            Assert.That(history.SelectedProfileProtected,Is.True);Assert.Throws<InvalidOperationException>(()=>history.DeleteSelected());
            history.Create("Копия альтернативы");string copyId=history.SelectedProfileId;var experiment=history.Selected.Snapshot;experiment.Set("rifle.damage",33);history.Save(experiment);history.MarkSelectedForRelease(true);
            var restart=new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true);
            Assert.That(restart.Selected.ReleaseSequence,Is.EqualTo(2));
            Assert.That(restart.Profiles.Single(p=>p.Id==copyId).Revisions.Single(r=>r.Number==2).ReleaseCandidate,Is.True);
        }
        [TestCase(false)][TestCase(true)]public void WholeShippedProfileIsReadOnlyAndCopiesSurviveUpdates(bool useDefault)
        {
            var baseline=Baseline();var local=new DesignLabHistory(path,baseline);
            if(!useDefault)local.Create("Альтернатива");
            string sourceId=local.SelectedProfileId;
            var old=local.Selected.Snapshot;old.Set("rifle.damage",32);local.Save(old);
            var catalog=LabReleaseCatalog.Factory(baseline);
            if(!useDefault)catalog.Entries.Add(Entry(2,sourceId,"Альтернатива",1,baseline));
            var history=new DesignLabHistory(path,baseline,releases:catalog);history.Select(sourceId,2);
            Assert.That(history.SelectedProfileReadOnly,Is.True);Assert.That(history.IsShipped(history.Selected),Is.False);
            var bytes=File.ReadAllBytes(path);var changed=history.Selected.Snapshot;changed.Set("rifle.damage",33);
            Assert.Throws<InvalidOperationException>(()=>history.Save(changed));
            Assert.Throws<InvalidOperationException>(()=>history.Rename("Переименовано"));
            Assert.Throws<InvalidOperationException>(()=>history.MarkSelectedForRelease(true));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));Assert.That(history.Selected.Hash,Is.EqualTo(old.Hash()));
            history.Create("Моя копия");string copyId=history.SelectedProfileId;
            Assert.That(copyId,Is.Not.EqualTo(sourceId));Assert.That(history.SelectedProfileReadOnly,Is.False);
            Assert.That(history.Selected.Hash,Is.EqualTo(old.Hash()));Assert.That(history.Selected.ReleaseCandidate,Is.False);
            history.Save(changed);history.MarkSelectedForRelease(true);history.Rename("Своя копия");
            var update=baseline.Clone();update.Set("rifle.damage",34);catalog.Entries.Add(Entry(3,sourceId,"Обновлённый",3,update));
            var restart=new DesignLabHistory(path,baseline,releases:catalog,resetToLatestDefault:true);
            restart.Select(sourceId,3);Assert.That(restart.Selected.Hash,Is.EqualTo(update.Hash()));Assert.That(restart.SelectedProfileReadOnly,Is.True);
            restart.Select(copyId,2);Assert.That(restart.Selected.Hash,Is.EqualTo(changed.Hash()));Assert.That(restart.SelectedProfileReadOnly,Is.False);
            Assert.That(restart.Profiles.Single(p=>p.Id==sourceId).Revisions.Single(r=>r.Number==2).Hash,Is.EqualTo(old.Hash()));
        }
        [TestCase(false)][TestCase(true)]
        public void PackageChooserUsesCatalogueInsteadOfHigherLegacyLocalKey(bool useDefault)
        {
            var baseline=Baseline();var local=new DesignLabHistory(path,baseline);
            if(!useDefault)local.Create("Local before package");
            string id=local.SelectedProfileId;
            for(int i=2;i<=9;i++){var draft=local.Selected.Snapshot;draft.Set("rifle.damage",30+i);local.Save(draft);}
            var legacy=local.Selected;
            var tuned=baseline.Clone();tuned.Set("rifle.damage",50);
            var catalog=LabReleaseCatalog.Factory(baseline);
            catalog.Entries.Add(Entry(2,id,"Published",2,tuned));
            var h=new DesignLabHistory(path,baseline,releases:catalog,resetToLatestDefault:true,persistMigration:false);
            int latest=h.LatestAvailableRevision(id);
            h.Select(id,latest);Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(2));Assert.That(h.Selected.Hash,Is.EqualTo(tuned.Hash()));
            var retained=h.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==legacy.Number);
            Assert.That(retained.Hash,Is.EqualTo(legacy.Hash));Assert.That(retained.Date,Is.EqualTo(legacy.Date));
            Assert.That(UnityEngine.JsonUtility.ToJson(retained.Snapshot),Is.EqualTo(UnityEngine.JsonUtility.ToJson(legacy.Snapshot)));
            h.Select(id,legacy.Number);Assert.That(h.SelectedProfileReadOnly,Is.True);
            h.Create("Archive copy");string copy=h.SelectedProfileId;Assert.That(h.Selected.Hash,Is.EqualTo(legacy.Hash));
            Assert.That(h.SelectedProfileReadOnly,Is.False);Assert.That(h.Selected.ReleaseCandidate,Is.False);
            var next=tuned.Clone();next.Set("rifle.damage",51);catalog.Entries.Add(Entry(3,id,"Published",3,next));
            var restarted=new DesignLabHistory(path,baseline,releases:catalog,resetToLatestDefault:true,persistMigration:false);
            restarted.Select(id,restarted.LatestAvailableRevision(id));Assert.That(restarted.Selected.ReleaseNumber,Is.EqualTo(3));
            Assert.That(restarted.Selected.Hash,Is.EqualTo(next.Hash()));
            Assert.That(restarted.Profiles.Single(p=>p.Id==copy).Revisions.Single().Hash,Is.EqualTo(legacy.Hash));
        }
        [Test]public void RegistryUpgradeKeepsSelectedPackagedSourceInsteadOfFactoryTail()
        {
            var baseline=Baseline();var method=typeof(ProvingProfile).GetMethod("BeforeBotBanterInterval",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var previous=new LabBundle{Profiles=baseline.Clone().Profiles.Select(p=>(ProvingProfile)method.Invoke(p,null)).ToList()};
            var tuned=previous.Clone();tuned.Set("rifle.damage",47);
            var catalog=LabReleaseCatalog.Factory(previous);catalog.Entries.Add(Entry(2,DesignLabHistory.ReleaseId,"Published",7,tuned));
            var old=new DesignLabHistory(path,previous,releases:catalog,resetToLatestDefault:true);
            old.Select(DesignLabHistory.ReleaseId,old.Selected.Number);var saved=old.Selected;var bytes=File.ReadAllBytes(path);
            var h=new DesignLabHistory(path,baseline,releases:catalog,persistMigration:false);
            Assert.That(h.StorageError,Is.Null);Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(7));
            Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(47));Assert.That(h.Compatible(h.Selected),Is.True);
            Assert.That(h.SavedIdentity(DesignLabHistory.ReleaseId,h.Selected.Number).Hash,Is.EqualTo(saved.ReleaseHash));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path),"Read-only migration preserves original file");
            Assert.That(h.LatestAvailableRevision(DesignLabHistory.ReleaseId),Is.EqualTo(h.Selected.Number));
            h.ResetToRelease();Assert.That(h.Selected.ReleaseNumber,Is.EqualTo(7));Assert.That(h.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(47));
        }
        [Test]public void MarkingAndSavingDoNotChangeReleaseContentOrInheritTheMark()
        {
            var baseline=Baseline();var catalog=LabReleaseCatalog.Factory(baseline);
            var history=new DesignLabHistory(path,baseline,releases:catalog,resetToLatestDefault:true);
            Assert.Throws<InvalidOperationException>(()=>history.MarkSelectedForRelease(false));
            string shippedHash=history.Selected.Hash;
            history.Create("Мой профиль");var hash=history.Selected.Hash;history.MarkSelectedForRelease(true);
            Assert.That(history.Selected.Hash,Is.EqualTo(hash));Assert.That(history.Selected.ReleaseCandidate,Is.True);
            history.MarkSelectedForRelease(false);Assert.That(history.Selected.ReleaseCandidate,Is.False);history.MarkSelectedForRelease(true);
            var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);
            Assert.That(history.Selected.ReleaseCandidate,Is.False);Assert.That(history.Selected.ReleaseSequence,Is.Zero);
            Assert.That(history.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Revisions[0].Hash,Is.EqualTo(shippedHash));
        }
        [Test]public void CorruptCatalogCannotRewriteLocalHistory()
        {
            var baseline=Baseline();var history=new DesignLabHistory(path,baseline);history.Create("Local");var bytes=File.ReadAllBytes(path);
            var invalid=LabReleaseCatalog.Factory(baseline);invalid.Entries[0].Hash="wrong";
            Assert.Throws<InvalidDataException>(()=>new DesignLabHistory(path,baseline,releases:invalid,resetToLatestDefault:true));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
            var duplicate=LabReleaseCatalog.Factory(baseline);duplicate.Entries.Add(duplicate.Entries[0].Clone());
            Assert.Throws<InvalidDataException>(()=>new DesignLabHistory(path,baseline,releases:duplicate));
        }
        [Test]public void FailedMarkerWritePreservesPreviousSelectionAndMark()
        {
            var baseline=Baseline();var history=new DesignLabHistory(path,baseline,(a,b)=>throw new IOException("disk failure"));
            var hash=history.Selected.Hash;Assert.Throws<IOException>(()=>history.MarkSelectedForRelease(true));
            Assert.That(history.Selected.ReleaseCandidate,Is.False);Assert.That(history.Selected.Hash,Is.EqualTo(hash));Assert.That(File.Exists(path),Is.False);
        }
        [Test]public void LocalShippedClaimsAreReboundOnlyFromTheCatalogue()
        {
            var baseline=Baseline();var local=new DesignLabHistory(path,baseline);local.Create("Local");
            var file=DesignLabHistoryTests.ReadFixture(path,Baseline());var r=file.Profiles.Last().Revisions[0];
            r.ReleaseSequence=1;r.ReleaseHash=r.Hash;File.WriteAllText(path,UnityEngine.JsonUtility.ToJson(file));
            var history=new DesignLabHistory(path,baseline,releases:LabReleaseCatalog.Factory(baseline),resetToLatestDefault:true);
            Assert.That(history.Profiles.Last().Revisions[0].ReleaseSequence,Is.Zero);
            Assert.That(history.Profiles.Last().Protected,Is.False);
        }
        string Project(out LabBundle baseline,out DesignLabHistory history)
        {
            baseline=Baseline();string project=Path.Combine(directory,"project");
            Directory.CreateDirectory(Path.Combine(project,".git"));
            Directory.CreateDirectory(Path.Combine(project,"Assets","StarTournament","Resources"));
            var catalog=LabReleaseCatalog.Factory(baseline);
            File.WriteAllText(CatalogPath(project),UnityEngine.JsonUtility.ToJson(catalog,true));
            history=LabProjectStorage.Open(project,baseline,catalog);history.PersistAuthoring();return project;
        }
        static string CatalogPath(string project)=>Path.Combine(project,"Assets","StarTournament","Resources","LabReleaseCatalog.json");
        static LabReleaseCatalog Catalog(string project)=>UnityEngine.JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(CatalogPath(project)));
        static bool Stage(string project,LabBundle baseline,Action<string,string> publish=null)
        {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("StarTournament.ProvingGround.Editor.LabReleaseStaging")).First(t=>t!=null);
            try{return (bool)type.GetMethod("Stage").Invoke(null,new object[]{project,baseline,publish});}
            catch(System.Reflection.TargetInvocationException e){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException).Throw();throw;}
        }
        [Test]public void ExplicitStagingRetainsProfilesAndPreservesSourceHistory()
        {
            string project=Project(out var baseline,out var history);
            history.Create("Первый релизный профиль");string first=history.SelectedProfileId;
            var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);history.MarkSelectedForRelease(true);
            history.Create("Второй релизный профиль");string second=history.SelectedProfileId;history.MarkSelectedForRelease(true);
            var source=File.ReadAllBytes(history.StoragePath);
            Assert.That(Stage(project,baseline),Is.True);
            var staged=Catalog(project);
            Assert.That(staged.Entries.Count,Is.EqualTo(3));
            Assert.That(staged.Entries.Single(e=>e.ProfileId==first).ProfileName,Is.EqualTo("Первый релизный профиль"));
            Assert.That(staged.Entries.Single(e=>e.ProfileId==second).ProfileName,Is.EqualTo("Второй релизный профиль"));
            Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
            var clean=new DesignLabHistory(Path.Combine(directory,"new-install.json"),baseline,releases:staged,resetToLatestDefault:true);
            Assert.That(clean.SelectedProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));
            clean.Select(first,2);Assert.Throws<InvalidOperationException>(()=>clean.Rename("Запрещено"));
            clean.Create("Копия первого профиля");Assert.That(clean.Profiles.Single(p=>p.Id==clean.SelectedProfileId).Protected,Is.False);
            history=LabProjectStorage.Open(project,baseline,staged);history.Select(first,2);
            history.Create("Копия первого профиля");string copyId=history.SelectedProfileId;history.Rename("Новое имя первого профиля");
            var changed=history.Selected.Snapshot;changed.Set("rifle.damage",32);history.Save(changed);history.MarkSelectedForRelease(true);
            source=File.ReadAllBytes(history.StoragePath);Assert.That(Stage(project,baseline),Is.True);
            var renamed=Catalog(project).Entries.Single(e=>e.ProfileId==copyId);
            Assert.That(renamed.ProfileName,Is.EqualTo("Новое имя первого профиля"));
            Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
        }
        [Test]public void StagingProjectsEveryMarkAndRemovesUnmarkedVersionsWithoutDeletingHistory()
        {
            string project=Project(out var baseline,out var history);
            history.Create("Multiple versions");string id=history.SelectedProfileId;
            int first=history.Selected.Number;history.MarkSelectedForRelease(true);
            var draft=history.Selected.Snapshot;draft.Set("rifle.damage",32);history.Save(draft);
            int second=history.Selected.Number;history.MarkSelectedForRelease(true);
            draft=history.Selected.Snapshot;draft.Set("rifle.damage",33);history.Save(draft);
            Assert.That(Stage(project,baseline),Is.True);
            Assert.That(Catalog(project).Entries.Where(e=>e.ProfileId==id).Select(e=>e.Revision),Is.EquivalentTo(new[]{first,second}));
            history.MarkForRelease(id,first,false);var source=File.ReadAllBytes(history.StoragePath);
            Assert.That(Stage(project,baseline),Is.True);
            Assert.That(Catalog(project).Entries.Where(e=>e.ProfileId==id).Select(e=>e.Revision),Is.EqualTo(new[]{second}));
            Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
            Assert.That(LabProjectStorage.Open(project,baseline,Catalog(project)).Profiles.Single(p=>p.Id==id).Revisions.Count,Is.EqualTo(3));
        }
        [Test]public void EquivalentStagingDoesNotRewriteCatalogueOrSource()
        {
            string project=Project(out var baseline,out var history);
            // Whitespace and serialization format are not changes in release selection.
            File.AppendAllText(CatalogPath(project),"\n\n");
            var target=File.ReadAllBytes(CatalogPath(project));var source=File.ReadAllBytes(history.StoragePath);
            Assert.That(Stage(project,baseline,(a,b)=>Assert.Fail("No publication for equivalent catalogue")),Is.False);
            Assert.That(File.ReadAllBytes(CatalogPath(project)),Is.EqualTo(target));
            Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
        }
        [Test]public void MissingMarkedDefaultRefusesStagingWithoutWrites()
        {
            string project=Project(out var baseline,out var history);history.MarkSelectedForRelease(false);
            var target=File.ReadAllBytes(CatalogPath(project));var source=File.ReadAllBytes(history.StoragePath);
            Assert.Throws<InvalidOperationException>(()=>Stage(project,baseline));
            Assert.That(File.ReadAllBytes(CatalogPath(project)),Is.EqualTo(target));
            Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
        }
        [Test]public void FailedAtomicStagingKeepsPreviousCatalogueAndSourceAndRemovesTemporaryFile()
        {
            string project=Project(out var baseline,out var history);history.Create("New");history.MarkSelectedForRelease(true);
            var target=File.ReadAllBytes(CatalogPath(project));var source=File.ReadAllBytes(history.StoragePath);bool called=false;
            Assert.Throws<IOException>(()=>Stage(project,baseline,(temp,destination)=>{
                called=true;Assert.That(File.Exists(temp),Is.True);Assert.That(destination,Is.EqualTo(CatalogPath(project)));
                throw new IOException("injected atomic replace failure");}));
            Assert.That(called,Is.True);Assert.That(File.ReadAllBytes(CatalogPath(project)),Is.EqualTo(target));
            Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(CatalogPath(project)),"*.tmp"),Is.Empty);
        }
        [Test]public void StagingEntrypointRejectsArbitraryPlayerHistoryBeforeChangingProject()
        {
            string old=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY");
            var build=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("StarTournament.ProvingGround.Editor.LabReleaseBuild")).First(t=>t!=null);
            string root=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,".."));
            string source=LabProjectStorage.ResolvePath(root);var sourceBytes=File.ReadAllBytes(source);
            var targetBytes=File.ReadAllBytes(CatalogPath(root));
            var scenes=UnityEditor.SceneManagement.EditorSceneManager.sceneCount;
            try
            {
                File.WriteAllText(path,"arbitrary history must not be read");
                Environment.SetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY",path);
                var error=Assert.Throws<System.Reflection.TargetInvocationException>(()=>build.GetMethod("PromoteMarked").Invoke(null,null));
                Assert.That(error.InnerException,Is.TypeOf<UnityEditor.Build.BuildFailedException>());
                Assert.That(error.InnerException.Message,Does.Contain("only the project author file"));
                Assert.That(UnityEditor.SceneManagement.EditorSceneManager.sceneCount,Is.EqualTo(scenes));
                Assert.That(File.ReadAllBytes(source),Is.EqualTo(sourceBytes));
                Assert.That(File.ReadAllBytes(CatalogPath(root)),Is.EqualTo(targetBytes));
                Assert.That(File.ReadAllText(path),Is.EqualTo("arbitrary history must not be read"));
            }
            finally{Environment.SetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY",old);}
        }
        [TestCase(false)][TestCase(true)]public void StagingRefusesMissingOrCorruptAuthorSourceWithoutReseeding(bool corrupt)
        {
            string project=Project(out var baseline,out var history);var target=File.ReadAllBytes(CatalogPath(project));
            if(corrupt)File.WriteAllText(history.StoragePath,"broken source");else File.Delete(history.StoragePath);
            byte[] source=corrupt?File.ReadAllBytes(history.StoragePath):null;
            Assert.Throws<IOException>(()=>Stage(project,baseline));
            Assert.That(File.ReadAllBytes(CatalogPath(project)),Is.EqualTo(target));
            if(corrupt)Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
            else Assert.That(File.Exists(history.StoragePath),Is.False);
        }
        [TestCase(false)][TestCase(true)]public void StagingRejectsRewrittenPublishedIdentity(bool dateOnly)
        {
            string project=Project(out var baseline,out var history);history.Create("New");history.MarkSelectedForRelease(true);
            Assert.That(Stage(project,baseline),Is.True);
            var previous=Catalog(project);var entry=previous.Entries.Single(e=>e.ProfileId==history.SelectedProfileId);
            if(dateOnly)entry.Date="20000101";
            else{entry.Snapshot.Set("rifle.damage",51);entry.Hash=entry.Snapshot.Hash();}
            File.WriteAllText(CatalogPath(project),UnityEngine.JsonUtility.ToJson(previous,true));
            var target=File.ReadAllBytes(CatalogPath(project));var source=File.ReadAllBytes(history.StoragePath);
            Assert.Throws<InvalidDataException>(()=>Stage(project,baseline));
            Assert.That(File.ReadAllBytes(CatalogPath(project)),Is.EqualTo(target));
            Assert.That(File.ReadAllBytes(history.StoragePath),Is.EqualTo(source));
        }
        [TestCase(false)][TestCase(true)]public void ReorderedRegistryPathsPreserveTrustedReleaseSnapshotsAndSourceHistory(bool historical)
        {
            var baseline=Baseline();var original=baseline.Clone();
            if(historical)
            {
                var prior=typeof(ProvingProfile).GetMethod("BeforeGamepadTriggerAim",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                original.Profiles=original.Profiles.Select(p=>(ProvingProfile)prior.Invoke(p,null)).ToList();
            }
            var local=new DesignLabHistory(path,baseline);local.Create("Local");var source=File.ReadAllBytes(path);
            var catalog=LabReleaseCatalog.Factory(original);var entry=catalog.Entries.Single();
            string trusted=UnityEngine.JsonUtility.ToJson(entry.Snapshot);
            foreach(var profile in entry.Snapshot.Profiles)
                ((System.Collections.Generic.List<NumericDescriptor>)profile.Descriptors).Reverse();
            string reordered=UnityEngine.JsonUtility.ToJson(entry.Snapshot);
            Assert.That(reordered,Is.Not.EqualTo(trusted));Assert.That(entry.Snapshot.Hash(),Is.EqualTo(entry.Hash));
            var history=new DesignLabHistory(path,baseline,releases:catalog,persistMigration:false);
            Assert.That(history.StorageError,Is.Null);
            var preserved=history.Profiles.Single(p=>p.Id==entry.ProfileId).Revisions.Single(r=>r.Hash==entry.Hash&&r.ReleaseSequence==entry.Sequence);
            Assert.That(UnityEngine.JsonUtility.ToJson(preserved.Snapshot),Is.EqualTo(trusted));
            Assert.That(UnityEngine.JsonUtility.ToJson(entry.Snapshot),Is.EqualTo(reordered),"The caller's catalogue stays detached");
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(source));
            entry.Snapshot.Set("rifle.damage",999);entry.Hash=entry.Snapshot.Hash();
            Assert.Throws<InvalidDataException>(()=>new DesignLabHistory(path,baseline,releases:catalog,persistMigration:false));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(source));
        }
        [Test]public void ValidationCacheUsesFullTrustedMetadataAndKeepsSnapshotsDetached()
        {
            var baseline=Baseline();var catalogue=LabReleaseCatalog.Factory(baseline);
            foreach(string label in new[]{"trusted A","trusted B","trusted A"})
            {
                var trusted=baseline.Clone();trusted.Profile("rifle.damage").Descriptor("rifle.damage").Label=label;
                var history=new DesignLabHistory(path,trusted,releases:catalogue);
                var snapshot=history.Selected.Snapshot;Assert.That(snapshot.Profile("rifle.damage").Descriptor("rifle.damage").Label,Is.EqualTo(label));
                snapshot.Profile("rifle.damage").Descriptor("rifle.damage").Label="poisoned";
                Assert.That(new DesignLabHistory(path,trusted,releases:catalogue).Selected.Snapshot.Profile("rifle.damage").Descriptor("rifle.damage").Label,Is.EqualTo(label));
            }
        }
        [Test]public void HistoricalReleaseKeepsOriginalHashAndHasARunnableCounterpart()
        {
            var baseline=Baseline();baseline.Profiles.Add(ProvingProfile.CreateBloodDefault());
            var old=baseline.Clone();old.Profiles=old.Profiles.Select(p=>p.BeforeBloodIntensityIncrease()).ToList();
            var catalogue=LabReleaseCatalog.Factory(baseline);catalogue.Entries.Add(Entry(2,"historical","Старая версия",1,old));
            var history=new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true);
            var revisions=history.Profiles.Single(p=>p.Id=="historical").Revisions;
            Assert.That(revisions.Any(r=>r.Hash==old.Hash()),Is.True);
            var compatible=revisions.Single(r=>r.ReleaseSequence==2&&history.Compatible(r));
            Assert.That(compatible.ReleaseHash,Is.EqualTo(old.Hash()));Assert.That(compatible.ReleaseNumber,Is.EqualTo(1));
            history.Select("historical",compatible.Number);Assert.That(history.IsShipped(history.Selected),Is.True);
            var restart=new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true);
            Assert.That(restart.SelectedProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));
            Assert.That(restart.Profiles.Single(p=>p.Id=="historical").Revisions.Count,Is.EqualTo(revisions.Count));
        }
    }
}
