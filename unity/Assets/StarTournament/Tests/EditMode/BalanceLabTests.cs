using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class BalanceLabTests
    {
        [Test] public void ProjectionKeepsGameplayAndDetachedTrustedOwners()
        {
            var runtime=BalanceLab.RuntimeDefaults();var before=runtime.Hash();var balance=BalanceLab.Project(runtime);
            CollectionAssert.AreEquivalent(BalanceLab.Fields.Keys,balance.Descriptors.Select(d=>d.Path));
            Assert.AreEqual(5,BalanceLab.Groups.Values.Select(g=>g.Split('→')[0].Trim()).Distinct().Count());
            foreach(var field in BalanceLab.Fields.Values){Assert.Greater(field.Label.Length,2);Assert.Greater(field.Hint.Length,15);Assert.IsTrue(BalanceLab.Groups.ContainsKey(field.Group));}
            foreach(var path in new[]{"rifle.damage","shot.range","rocket.radius","cutter.damageRadius","player.movement.jumpSpeed","armor.pickupRadius"})Assert.AreEqual(runtime.Get(path),balance.Get(path));
            foreach(var path in new[]{"zone.headY","player.capsule.radius","camera.fieldOfViewDegrees","presentation.healPickupScale","audio.music.gain","simulation.fixedTickHz"})Assert.IsFalse(balance.Descriptors.Any(d=>d.Path==path),path);
            balance.Set("rifle.damage",balance.Get("rifle.damage")+1);Assert.AreEqual(before,runtime.Hash());
            Assert.IsEmpty(balance.Validate());
        }
        [Test] public void ShippedCodecRoundtripsEveryVersionAndRejectsTampering()
        {
            var registry=BalanceLab.Defaults();var archive=JsonUtility.FromJson<LabReleaseCatalog>(File.ReadAllText(Path.Combine(Application.dataPath,"..","DesignLab","Archive","LabReleaseCatalog-full-v1.json")));
            var executed=BalanceLab.ProjectExecutedReleases(archive,BalanceLab.RuntimeDefaults(),out var final);
            var json=BalanceLab.WriteReleases(executed,registry);Assert.Less(json.Length,100000);
            var catalog=LabReleaseCatalog.ParsePackaged(json,registry);Assert.AreEqual(5,catalog.Entries.Count);
            var latest=executed.Entries.Last().Snapshot;
            for(int i=0;i<archive.Entries.Count;i++)
            {
                var original=archive.Entries[i];var entry=catalog.Entries[i];Assert.AreEqual(original.Hash,entry.SourceHash);Assert.AreEqual(original.Revision,entry.Revision);
                Assert.AreEqual(executed.Entries[i].Snapshot.Hash(),entry.Hash);
                foreach(var d in registry.Descriptors)Assert.AreEqual(executed.Entries[i].Snapshot.Get(d.Path),entry.Snapshot.Get(d.Path));
            }
            var published=DesignLabHistory.ResolvePublishedDefault(registry,catalog);Assert.IsNull(published.StorageError);Assert.AreEqual(archive.Entries.Last().Hash,published.SavedIdentity(published.SelectedProfileId,published.Selected.Number).Hash);
            Assert.Throws<InvalidDataException>(()=>LabReleaseCatalog.ParsePackaged(json.Replace("\"Revision\": 12","\"Revision\": 13"),registry));
            var clone=catalog.Clone();clone.Entries[0].Snapshot.Set("rifle.damage",float.PositiveInfinity);Assert.Throws<InvalidDataException>(()=>DesignLabHistory.ResolvePublishedDefault(registry,clone));
        }
        [Test] public void IndependentSnapshotKeepsEveryRetiredFinalValueAndRejectsBalanceOverrides()
        {
            var runtime=BalanceLab.RuntimeDefaults();var original=BalanceLab.ReadAuthoringArchive(Path.Combine(Application.dataPath,"..","DesignLab","Archive","LabReleaseCatalog-full-v1.json"));
            BalanceLab.ProjectExecutedReleases(original,runtime,out var final);var before=BalanceLab.Project(runtime).Hash();
            string json=BalanceLab.WriteRuntimeSnapshot(final,runtime,original.Entries.Last().Hash);BalanceLab.ApplyRuntimeSnapshot(runtime,json);
            foreach(var d in final.Descriptors.Where(d=>!BalanceLab.Fields.ContainsKey(d.Path)))Assert.AreEqual(final.Get(d.Path),runtime.Get(d.Path),d.Path);
            Assert.AreEqual(before,BalanceLab.Project(runtime).Hash());
            Assert.Throws<InvalidDataException>(()=>BalanceLab.ApplyRuntimeSnapshot(runtime,json.Replace("\"Schema\": 1","\"Schema\": 2")));
        }
        [Test] public void LocalProjectionPreservesSavedIdentityAndAtomicReload()
        {
            string folder=Path.Combine(Path.GetTempPath(),"balance-lab-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                var full=BalanceLab.RuntimeDefaults();string path=Path.Combine(folder,"history.json");var old=new DesignLabHistory(path,full);old.Create("Мой баланс");var draft=old.Selected.Snapshot;draft.Set("rifle.damage",31);old.Save(draft);
                byte[] original=File.ReadAllBytes(path);var identity=old.SavedIdentity(old.SelectedProfileId,old.Selected.Number);string id=old.SelectedProfileId;int revision=old.Selected.Number;
                var catalog=LabReleaseCatalog.Factory(BalanceLab.Project(full));var history=DesignLabHistory.OpenBalance(path,full,catalog);
                Assert.IsNull(history.StorageError);CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
                Assert.IsTrue(File.Exists(history.StoragePath+".origin.json"));history.Select(id,revision);Assert.AreEqual(31,history.Selected.Snapshot.Get("rifle.damage"));Assert.AreEqual(identity.Hash,history.SavedIdentity(id,revision).Hash);
                history.CompactStorage();var reloaded=DesignLabHistory.OpenBalance(path,full,catalog);Assert.IsNull(reloaded.StorageError);reloaded.Select(id,revision);Assert.AreEqual(identity.Hash,reloaded.SavedIdentity(id,revision).Hash);Assert.AreEqual(31,reloaded.Selected.Snapshot.Get("rifle.damage"));
                CollectionAssert.AreEqual(original,File.ReadAllBytes(Directory.GetFiles(folder,"*.archive-*.json").Single()));
                // Fast path does not need the original archive to parse normal history.
                File.Move(path,path+".moved");Assert.IsNull(DesignLabHistory.OpenBalance(path,full,catalog).StorageError);
            }
            finally{Directory.Delete(folder,true);}
        }
        [Test] public void CompactAuthorStagingIncludesNewReloadedVersionsAndIsIdempotent()
        {
            string root=Path.Combine(Path.GetTempPath(),"balance-stage-"+Guid.NewGuid().ToString("N"));string project=Path.Combine(root,"unity");Directory.CreateDirectory(Path.Combine(root,".git"));Directory.CreateDirectory(Path.Combine(project,"Assets","StarTournament","Resources"));
            try
            {
                string target=Path.Combine(project,"Assets","StarTournament","Resources","LabReleaseCatalog.json");File.Copy(Path.Combine(Application.dataPath,"StarTournament","Resources","LabReleaseCatalog.json"),target);
                var history=LabProjectStorage.Open(project,BalanceLab.Defaults(),LabReleaseCatalog.Load());history.PersistAuthoring();history.Create("Новый авторский баланс");var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);history.MarkSelectedForRelease(true);
                string id=history.SelectedProfileId;var source=File.ReadAllBytes(history.StoragePath);
                var reopened=LabProjectStorage.Open(project,BalanceLab.Defaults(),LabReleaseCatalog.Load());Assert.IsNull(reopened.StorageError);reopened.BuildReleaseCatalog();
                var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("StarTournament.ProvingGround.Editor.LabReleaseStaging")).First(t=>t!=null);var stage=type.GetMethod("Stage");
                bool Stage()=> (bool)stage.Invoke(null,new object[]{project,BalanceLab.Defaults(),null});
                Assert.IsTrue(Stage());var catalog=LabReleaseCatalog.ParsePackaged(File.ReadAllText(target));Assert.AreEqual(31,catalog.Entries.Single(e=>e.ProfileId==id).Snapshot.Get("rifle.damage"));
                CollectionAssert.AreEqual(source,File.ReadAllBytes(history.StoragePath));var bytes=File.ReadAllBytes(target);Assert.IsFalse(Stage());CollectionAssert.AreEqual(bytes,File.ReadAllBytes(target));
            }
            finally{Directory.Delete(root,true);}
        }
        [Test] public void CompactAuthorReopensWithoutOriginalOrArchive()
        {
            string folder=Path.Combine(Path.GetTempPath(),"balance-author-fast-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string source=Path.Combine(folder,"author.json");string compact=Path.Combine(Application.dataPath,"..","DesignLab","authoring-history-v1.json.balance-v1.json");
                File.Copy(compact,source+".balance-v1.json");var bytes=File.ReadAllBytes(source+".balance-v1.json");
                var history=DesignLabHistory.OpenBalance(source,BalanceLab.RuntimeDefaults(),LabReleaseCatalog.Load(),authoring:true);
                Assert.IsNull(history.StorageError);Assert.IsNull(history.ReleaseError);Assert.AreEqual(11,history.SavedIdentity(history.SelectedProfileId,history.Selected.Number).Number);
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(source+".balance-v1.json"));Assert.IsFalse(File.Exists(source));
            }
            finally{Directory.Delete(folder,true);}
        }
        [Test] public void AuthorProjectionPreservesFlagsAndExportsTheSelectedDefault()
        {
            string folder=Path.Combine(Path.GetTempPath(),"balance-author-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
            try
            {
                string path=Path.Combine(folder,"author.json");File.Copy(Path.Combine(Application.dataPath,"..","DesignLab","authoring-history-v1.json"),path);
                Directory.CreateDirectory(Path.Combine(folder,"Archive"));File.Copy(Path.Combine(Application.dataPath,"..","DesignLab","Archive","LabReleaseCatalog-full-v1.json"),Path.Combine(folder,"Archive","LabReleaseCatalog-full-v1.json"));
                var original=File.ReadAllBytes(path);var full=BalanceLab.RuntimeDefaults();var catalog=LabReleaseCatalog.Load();
                var before=DesignLabHistory.ReadOriginalForBalanceImport(path,full,authoring:true);Assert.IsNull(before.StorageError);
                var history=DesignLabHistory.OpenBalance(path,full,catalog,authoring:true);Assert.IsNull(history.StorageError);Assert.IsNull(history.ReleaseError);
                foreach(var p in before.Profiles)foreach(var r in p.Revisions.Where(r=>r.CompatibilitySourceNumber==0))
                {
                    var saved=history.Profiles.Single(x=>x.Id==p.Id).Revisions.Single(x=>x.Number==r.Number);
                    Assert.AreEqual(r.Hash,history.SavedIdentity(p.Id,r.Number).Hash);Assert.IsTrue(!r.ReleaseCandidate||saved.ReleaseCandidate);
                }
                var exported=history.BuildReleaseCatalog();var reopened=LabReleaseCatalog.ParsePackaged(BalanceLab.WriteReleases(exported,BalanceLab.Defaults()));
                Assert.AreEqual(history.Selected.Hash,DesignLabHistory.ResolvePublishedDefault(BalanceLab.Defaults(),reopened).Selected.Hash);
                CollectionAssert.AreEqual(original,File.ReadAllBytes(path));
            }
            finally{Directory.Delete(folder,true);}
        }
        [Test] public void RealLegacyCopyPreservesAllNamedVersionsAndBytes()
        {
            string fixture=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_BALANCE_LEGACY_FIXTURE");if(string.IsNullOrEmpty(fixture))Assert.Ignore("Owned real-history fixture not selected");
            var original=File.ReadAllBytes(fixture);var file=JsonUtility.FromJson<LabHistoryFile>(System.Text.Encoding.UTF8.GetString(original));
            var catalog=LabReleaseCatalog.Load();var history=DesignLabHistory.OpenBalance(fixture,BalanceLab.RuntimeDefaults(),catalog);
            Assert.IsNull(history.StorageError);
            foreach(var p in file.Profiles)foreach(var r in p.Revisions)
            {
                var saved=history.SavedIdentity(p.Id,r.Number);Assert.AreEqual(r.Hash,saved.Hash,p.Id+" v"+r.Number);Assert.AreEqual(r.Date,saved.Date);
                var projected=history.Profiles.Single(x=>x.Id==p.Id).Revisions.Single(x=>x.Number==r.Number);
                foreach(var d in r.Snapshot.Descriptors.Where(d=>BalanceLab.Fields.ContainsKey(d.Path)))Assert.AreEqual(r.Snapshot.Get(d.Path),projected.Snapshot.Get(d.Path),p.Id+" v"+r.Number+" "+d.Path);
            }
            CollectionAssert.AreEqual(original,File.ReadAllBytes(fixture));
            history.CompactStorage();Assert.IsNull(DesignLabHistory.OpenBalance(fixture,BalanceLab.RuntimeDefaults(),catalog).StorageError);
            Debug.Log("Real balance history: source bytes="+original.LongLength+", compact bytes="+new FileInfo(history.StoragePath).Length+", "+history.StartupDiagnostics);
        }
    }
}
