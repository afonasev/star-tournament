using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class LabParameterStorageTests
    {
        string directory,path;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static LabBundle Small()=>new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()}};
        [SetUp] public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-parameter-storage-"+Guid.NewGuid());Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");}
        [TearDown] public void Cleanup(){Directory.Delete(directory,true);}
        static string State(DesignLabHistory history)=>JsonUtility.ToJson(typeof(DesignLabHistory).GetField("data",Private).GetValue(history));
        static void Resign(LabParameterFile file)
        {
            file.Integrity=(string)typeof(DesignLabHistory).GetMethod("StorageIntegrity",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{file});
        }
        [TestCase(0)] [TestCase(3)]
        public void UnsupportedStorageSchemaPreservesSourceAndDisablesWrites(int schema)
        {
            File.WriteAllText(path,"{\"Schema\":"+schema+",\"Profiles\":[]}");
            var bytes=File.ReadAllBytes(path);
            var history=new DesignLabHistory(path,Small(),persistMigration:false);
            Assert.That(history.StorageError,Is.Not.Null.And.Not.Empty);
            Assert.Throws<IOException>(()=>history.Create("Do not replace unknown data"));
            Assert.Throws<IOException>(()=>history.CompactStorage());
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
        }

        [TestCase("-muted")][TestCase("-noaudio")]
        public void SilentStartupFlagDominatesAudioReviewWithoutSavingPreferences(string flag)
        {
            var preference=PlayerPrefs.GetInt("StarTournament.Audio.EffectsPercent",-1);
            var music=PlayerPrefs.GetInt("StarTournament.Audio.MusicPercent",-1);
            var method=typeof(ProvingGround).GetMethod("ShouldMuteStartup",BindingFlags.Static|BindingFlags.NonPublic);
            Assert.That(method.Invoke(null,new object[]{new[]{flag,"-audioReview"},false}),Is.EqualTo(true));
            Assert.That(method.Invoke(null,new object[]{Array.Empty<string>(),false}),Is.EqualTo(false));
            Assert.That(PlayerPrefs.GetInt("StarTournament.Audio.EffectsPercent",-1),Is.EqualTo(preference));
            Assert.That(PlayerPrefs.GetInt("StarTournament.Audio.MusicPercent",-1),Is.EqualTo(music));
        }
        [Test] public void SessionMutedCannotBeDisabledByDiagnosticReview()
        {
            int effects=PlayerPrefs.GetInt("StarTournament.Audio.EffectsPercent",-1),music=PlayerPrefs.GetInt("StarTournament.Audio.MusicPercent",-1);
            var root=new GameObject("silent-startup-contract");
            try
            {
                var audio=new NativeGameAudio(root.transform,ProvingProfile.CreateDefault(),false,true);
                Assert.That(audio.SessionMuted,Is.True);audio.SetDiagnosticMute(false);
                Assert.That(typeof(NativeGameAudio).GetField("muted",Private).GetValue(audio),Is.EqualTo(true));
                Assert.That(root.GetComponentsInChildren<AudioSource>().Any(s=>s.isPlaying),Is.False);
                Assert.That(PlayerPrefs.GetInt("StarTournament.Audio.EffectsPercent",-1),Is.EqualTo(effects));Assert.That(PlayerPrefs.GetInt("StarTournament.Audio.MusicPercent",-1),Is.EqualTo(music));
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test] public void LobbyMetadataPreservesHistoricalHashesAndSuppliesDefaults()
        {
            var old=Small();var history=new DesignLabHistory(path,old);history.Create("Старый");var prior=history.Selected;
            var current=old.Clone();current.Profiles.Add(ProvingProfile.CreateLobbyDefault());
            var loaded=new DesignLabHistory(path,current,persistMigration:false);
            Assert.That(loaded.StorageError,Is.Null);Assert.That(loaded.ReleaseError,Is.Null);
            Assert.That(loaded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(loaded.ReleaseParameterDefaults().Get("lobby.durationMinutes"),Is.EqualTo(3));
            Assert.That(loaded.ReleaseParameterDefaults().Get("lobby.targetPoints"),Is.EqualTo(2000));
            Assert.That(current.Profile("lobby.targetPoints").Descriptor("lobby.targetPoints").Step,Is.EqualTo(500));
        }

        [Test] public void CompactSavesHaveOnlyParametersAndExactRestartIdentity()
        {
            var b=Small();var history=new DesignLabHistory(path,b);history.Create("Мой баланс");var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);history.MarkSelectedForRelease(true);
            var bytes=File.ReadAllBytes(path);var json=File.ReadAllText(path);var compact=JsonUtility.FromJson<LabParameterFile>(json);
            Assert.That(compact.Schema,Is.EqualTo(2));Assert.That(json,Does.Not.Contain("\"Snapshot\":").And.Not.Contain("\"DefaultValue\":").And.Not.Contain("\"Description\":").And.Not.Contain("\"Minimum\":"));
            Assert.That(compact.Schemas.Count,Is.EqualTo(1));
            Assert.That(compact.Profiles.SelectMany(p=>p.Revisions).All(r=>r.Values.Count==b.Descriptors.Count(d=>b.IsEditable(d.Path))&&r.LegacyOverrides.Count==0),Is.True);
            var restarted=new DesignLabHistory(path,b,persistMigration:false);Assert.That(restarted.StorageError,Is.Null);Assert.That(State(restarted),Is.EqualTo(State(history)));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            history.CompactStorage();Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes),"Repeated compaction is idempotent");
        }
        [Test] public async System.Threading.Tasks.Task ConcurrentCompactReadersKeepTrustedValuesDetached()
        {
            var b=Small();var seed=new DesignLabHistory(path,b);seed.Create("Concurrent");var draft=seed.Selected.Snapshot;draft.Set("rifle.damage",31);seed.Save(draft);string expected=State(seed);var bytes=File.ReadAllBytes(path);
            var readers=await System.Threading.Tasks.Task.WhenAll(Enumerable.Range(0,4).Select(i=>System.Threading.Tasks.Task.Run(()=>new DesignLabHistory(path,b,persistMigration:false))));
            foreach(var reader in readers){Assert.That(reader.StorageError,Is.Null);Assert.That(State(reader),Is.EqualTo(expected));}
            var detached=readers[0].Selected.Snapshot;detached.Set("rifle.damage",32);
            Assert.That(readers[1].Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [TestCase("missing")][TestCase("duplicate")][TestCase("unknown")][TestCase("baseline")][TestCase("hash")][TestCase("readonly")][TestCase("range")]
        public void SelfConsistentCompactTamperingIsRejectedWithoutWriting(string corruption)
        {
            var b=Small();var history=new DesignLabHistory(path,b);history.Create("Test");var file=JsonUtility.FromJson<LabParameterFile>(File.ReadAllText(path));var revision=file.Profiles.Last().Revisions[0];
            if(corruption=="missing")revision.Values.RemoveAt(0);
            if(corruption=="duplicate")revision.Values[0].Path=revision.Values[1].Path;
            if(corruption=="unknown")revision.Values[0].Path="unknown.parameter";
            if(corruption=="baseline")file.Schemas[0].BaselineHash=new string('0',64);
            if(corruption=="hash")revision.Hash=new string('0',64);
            if(corruption=="readonly")revision.Values.Add(new ProvingProfileValue{Path="simulation.fixedTickHz",Value=10});
            if(corruption=="range")
            {
                revision.Values.Single(v=>v.Path=="rifle.damage").Value=1000000;
                var invalid=history.Selected.Snapshot;invalid.Set("rifle.damage",1000000);revision.Hash=invalid.Hash();
            }
            Resign(file);File.WriteAllText(path,JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);
            var bad=new DesignLabHistory(path,b,persistMigration:false);Assert.That(bad.StorageError,Is.Not.Null);Assert.Throws<IOException>(()=>bad.CompactStorage());CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test] public void SelfConsistentDefaultRootCannotBecomeCustomShippedConfiguration()
        {
            var b=Small();var seed=new DesignLabHistory(path,b);seed.CompactStorage();var file=JsonUtility.FromJson<LabParameterFile>(File.ReadAllText(path));var revision=file.Profiles[0].Revisions[0];
            revision.Values.Single(v=>v.Path=="rifle.damage").Value=31;var tampered=seed.Selected.Snapshot;tampered.Set("rifle.damage",31);revision.Hash=tampered.Hash();Resign(file);
            File.WriteAllText(path,JsonUtility.ToJson(file));var bytes=File.ReadAllBytes(path);
            var bad=new DesignLabHistory(path,b,persistMigration:false);Assert.That(bad.StorageError,Is.Not.Null);CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
        }
        [Test] public void LegacyCompactionKeepsCustomHiddenParametersAndBackup()
        {
            var b=Small();var old=new DesignLabHistory(path,b);old.Create("Старый профиль");var legacy=(LabHistoryFile)typeof(DesignLabHistory).GetField("data",Private).GetValue(old);
            var revision=legacy.Profiles.Last().Revisions[0];revision.Snapshot.Set("simulation.fixedTickHz",60);revision.Hash=revision.Snapshot.Hash();File.WriteAllText(path,JsonUtility.ToJson(legacy,true));var bytes=File.ReadAllBytes(path);
            var reader=new DesignLabHistory(path,b,persistMigration:false);var before=State(reader);reader.CompactStorage();
            var compact=JsonUtility.FromJson<LabParameterFile>(File.ReadAllText(path));Assert.That(compact.Profiles.Last().Revisions[0].LegacyOverrides.Single().Path,Is.EqualTo("simulation.fixedTickHz"));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path+".schema1-backup.json"));var restarted=new DesignLabHistory(path,b,persistMigration:false);Assert.That(restarted.StorageError,Is.Null);Assert.That(State(restarted),Is.EqualTo(before));
            var draft=restarted.Selected.Snapshot;draft.Set("simulation.fixedTickHz",50);Assert.Throws<ArgumentException>(()=>restarted.Save(draft));
        }
        [Test] public void FailedCompactionPreservesLegacyAndSelection()
        {
            var b=Small();var seed=new DesignLabHistory(path,b);seed.Create("Test");File.WriteAllText(path,State(seed));var bytes=File.ReadAllBytes(path);
            var history=new DesignLabHistory(path,b,atomicPublish:(a,z)=>throw new IOException("injected"),persistMigration:false);var before=State(history);
            Assert.Throws<IOException>(()=>history.CompactStorage());Assert.That(State(history),Is.EqualTo(before));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));Assert.That(Directory.GetFiles(directory,"*.tmp"),Is.Empty);
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path+".schema1-backup.json"));
        }
        [Test,Timeout(1800000)] public void ExactLargeLegacyHistoryCompactsAndReloadsAllIdentities()
        {
            var source=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_COMPACT_HISTORY_FIXTURE");Assert.That(source,Is.Not.Null.And.Not.Empty,"Explicit owned exact fixture required");File.Copy(source,path);
            const string scenePath="Assets/StarTournament/Scenes/ProvingGround.unity";
            var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scenePath);bool opened=!scene.isLoaded;
            if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
            try
            {
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();var baseline=ground.CaptureLabAuthoringBaseline();var catalog=LabReleaseCatalog.Load();
                var legacy=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));var bytes=File.ReadAllBytes(path);
                var history=new DesignLabHistory(path,baseline,releases:catalog,persistMigration:false);Assert.That(history.StorageError,Is.Null);var before=State(history);history.CompactStorage();
                var size=new FileInfo(path).Length;Assert.That(size,Is.LessThan(2000000));var json=File.ReadAllText(path);Assert.That(json,Does.Not.Contain("\"Snapshot\":").And.Not.Contain("\"Description\":").And.Not.Contain("\"DefaultValue\":"));
                var compact=JsonUtility.FromJson<LabParameterFile>(json);
                if(compact.Schema==3)
                {
                    var envelope=JsonUtility.FromJson<LabParameterEnvelope>(json);Assert.That(envelope.Views,Is.Not.Empty);
                    Assert.That(envelope.Views.All(v=>v.SourceNumber>0&&!string.IsNullOrEmpty(v.SourceHash)),Is.True);
                    compact=envelope.History;
                }
                Assert.That(compact.Schema,Is.EqualTo(2));Assert.That(compact.Profiles.SelectMany(p=>p.Revisions).All(r=>r.LegacyOverrides.Count==0),Is.True);
                var loaded=new DesignLabHistory(path,baseline,releases:catalog,persistMigration:false);Assert.That(loaded.StorageError,Is.Null);Assert.That(State(loaded),Is.EqualTo(before));
                foreach(var p in legacy.Profiles)foreach(var r in p.Revisions)
                {var preserved=loaded.Profiles.Single(q=>q.Id==p.Id).Revisions.Single(q=>q.Number==r.Number);Assert.That(preserved.Hash,Is.EqualTo(r.Hash));Assert.That(preserved.Date,Is.EqualTo(r.Date));foreach(var d in r.Snapshot.Descriptors.Where(d=>r.Snapshot.IsEditable(d.Path)))Assert.That(preserved.Snapshot.Get(d.Path),Is.EqualTo(r.Snapshot.Get(d.Path)),d.Path);}
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path+".schema1-backup.json"));
                var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_COMPACT_HISTORY_OUTPUT");
                if(!string.IsNullOrEmpty(output)){Assert.That(Path.IsPathFullyQualified(output),Is.True);Directory.CreateDirectory(Path.GetDirectoryName(output));File.Copy(path,output,false);}
                Debug.Log("LAB_PARAMETER_COMPACTION_PASS "+JsonUtility.ToJson(new Report{OriginalBytes=bytes.Length,CompactBytes=size,OriginalRevisions=legacy.Profiles.Sum(p=>p.Revisions.Count),Revisions=compact.Profiles.Sum(p=>p.Revisions.Count),Schemas=compact.Schemas.Count,Reload=loaded.StartupDiagnostics}));
            }
            finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        }
        [Serializable] sealed class Report{public long OriginalBytes,CompactBytes;public int OriginalRevisions,Revisions,Schemas;public LabHistoryStartupDiagnostics Reload;}
    }
}
