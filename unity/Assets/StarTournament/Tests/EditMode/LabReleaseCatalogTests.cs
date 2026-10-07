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
            var experiment=history.Selected.Snapshot;experiment.Set("rifle.damage",33);history.Save(experiment);history.MarkSelectedForRelease(true);
            var restart=new DesignLabHistory(path,baseline,releases:catalogue,resetToLatestDefault:true);
            Assert.That(restart.Selected.ReleaseSequence,Is.EqualTo(2));
            Assert.That(restart.Profiles.Single(p=>p.Id=="alternative").Revisions.Single(r=>r.Number==5).ReleaseCandidate,Is.True);
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
            var file=UnityEngine.JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));var r=file.Profiles.Last().Revisions[0];
            r.ReleaseSequence=1;r.ReleaseHash=r.Hash;File.WriteAllText(path,UnityEngine.JsonUtility.ToJson(file));
            var history=new DesignLabHistory(path,baseline,releases:LabReleaseCatalog.Factory(baseline),resetToLatestDefault:true);
            Assert.That(history.Profiles.Last().Revisions[0].ReleaseSequence,Is.Zero);
            Assert.That(history.Profiles.Last().Protected,Is.False);
        }
        [Test]public void ExplicitStagingRetainsProfilesAndPreservesSourceHistory()
        {
            const string asset="Assets/StarTournament/Resources/LabReleaseCatalog.json";
            string original=File.ReadAllText(asset),oldEnvironment=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY");
            var build=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("StarTournament.ProvingGround.Editor.LabReleaseBuild")).First(t=>t!=null);
            var prepare=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("StarTournament.ProvingGround.Editor.ProvingGroundBuild")).First(t=>t!=null);
            try
            {
                prepare.GetMethod("Prepare").Invoke(null,null);
                var baseline=UnityEngine.Object.FindAnyObjectByType<ProvingGround>().CaptureLabBundle();
                var history=new DesignLabHistory(path,baseline,releases:UnityEngine.JsonUtility.FromJson<LabReleaseCatalog>(original));
                history.Create("Первый релизный профиль");string first=history.SelectedProfileId;
                var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);history.MarkSelectedForRelease(true);
                history.Create("Второй релизный профиль");string second=history.SelectedProfileId;history.MarkSelectedForRelease(true);
                var source=File.ReadAllBytes(path);Environment.SetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY",path);
                build.GetMethod("PromoteMarked").Invoke(null,null);
                var staged=UnityEngine.JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(asset));
                Assert.That(staged.Entries.Count,Is.EqualTo(UnityEngine.JsonUtility.FromJson<LabReleaseCatalog>(original).Entries.Count+2));
                Assert.That(staged.Entries.Single(e=>e.ProfileId==first).ProfileName,Is.EqualTo("Первый релизный профиль"));
                Assert.That(staged.Entries.Single(e=>e.ProfileId==second).ProfileName,Is.EqualTo("Второй релизный профиль"));
                Assert.That(File.ReadAllBytes(path),Is.EqualTo(source));
                var clean=new DesignLabHistory(Path.Combine(directory,"new-install.json"),baseline,releases:staged,resetToLatestDefault:true);
                Assert.That(clean.SelectedProfileId,Is.EqualTo(DesignLabHistory.ReleaseId));Assert.That(clean.Profiles.Any(p=>p.Id==first),Is.True);
            }
            finally
            {
                File.WriteAllText(asset,original);AssetDatabase.ImportAsset(asset,ImportAssetOptions.ForceUpdate);
                Environment.SetEnvironmentVariable("STAR_TOURNAMENT_LAB_RELEASE_HISTORY",oldEnvironment);
            }
        }
    }
}
