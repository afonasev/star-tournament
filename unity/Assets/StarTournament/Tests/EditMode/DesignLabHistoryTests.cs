using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class DesignLabHistoryTests
    {
        string directory,path;
        public static LabBundle Shipped()=>new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateTrooperDefault(),ProvingProfile.CreateMatchDefault(),ProvingProfile.CreateTeamDefault(),ProvingProfile.CreateRosterDefault(),ProvingProfile.CreateBotPerceptionDefault(),ProvingProfile.CreateNavigationDefault(),ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateCombatBowlRingPresentationDefault(),CombatBowlCatalog.AuthoringProfile(),ProvingProfile.CreateCutterDefault()}};
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-lab-tests-"+Guid.NewGuid());Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        [Test]public void CompleteRegistryHasUniquePathsAndValidDefaults()
        {var bundle=Shipped();Assert.That(bundle.Validate(),Is.Empty,string.Join(";",bundle.Validate().Select(i=>i.Path+": "+i.Message)));Assert.That(bundle.Profiles.Count,Is.EqualTo(typeof(ProvingGround).GetFields().Count(f=>f.FieldType==typeof(ProvingProfile))),"Each public runtime profile must be represented in the bundle");Assert.That(bundle.Descriptors.Count(),Is.GreaterThan(200));Assert.That(bundle.Descriptors.Select(d=>d.Path).Distinct().Count(),Is.EqualTo(bundle.Descriptors.Count()));}
        [Test]public void SaveFromOldBaseAppendsWithoutMutatingEarlierHashesAndRestarts()
        {
            var history=new DesignLabHistory(path,Shipped());history.Create("Первый");string id=history.SelectedProfile.Id;var v1=history.Selected;
            var draft=v1.Snapshot.Clone();draft.Set("rifle.head",31);history.Save(draft);var v2=history.Selected;
            history.Select(id,1);draft=history.Selected.Snapshot.Clone();draft.Set("rifle.head",32);history.Save(draft);
            Assert.That(history.Selected.Number,Is.EqualTo(3));history.Rename("Новое имя");
            var restarted=new DesignLabHistory(path,Shipped());Assert.That(restarted.StorageError,Is.Null);Assert.That(restarted.Selected.Number,Is.EqualTo(3));Assert.That(restarted.SelectedProfile.Id,Is.EqualTo(id));
            Assert.That(restarted.SelectedProfile.Revisions[0].Hash,Is.EqualTo(v1.Hash));Assert.That(restarted.SelectedProfile.Revisions[1].Hash,Is.EqualTo(v2.Hash));
            var detached=restarted.Selected;detached.Snapshot.Set("rifle.head",99);Assert.That(restarted.Selected.Snapshot.Get("rifle.head"),Is.EqualTo(32));
        }
        [Test]public void CorruptionIsPreservedAndCannotBeOverwritten()
        {File.WriteAllText(path,"broken");var history=new DesignLabHistory(path,Shipped());Assert.That(history.StorageError,Is.Not.Null);Assert.Throws<IOException>(()=>history.Create("test"));Assert.That(File.ReadAllText(path),Is.EqualTo("broken"));}
        [Test]public void ProtectedReleaseCannotBeDeletedAndLocalProfileDeletionReturnsToRelease()
        {var history=new DesignLabHistory(path,Shipped());Assert.Throws<InvalidOperationException>(()=>history.DeleteSelected());history.Create("Локальный");history.DeleteSelected();Assert.That(history.SelectedProfile.Id,Is.EqualTo(DesignLabHistory.ReleaseId));Assert.That(history.Profiles.Count,Is.EqualTo(1));}
        [Test]public void InvalidRangeStepAndCrossFieldDraftCannotSave()
        {
            var history=new DesignLabHistory(path,Shipped());var draft=history.Selected.Snapshot.Clone();draft.Set("rifle.head",999);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            draft=history.Selected.Snapshot.Clone();draft.Set("rifle.head",30.5f);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            draft=history.Selected.Snapshot.Clone();draft.Set("score.chainTotal2",0);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            draft=history.Selected.Snapshot.Clone();draft.Set("zone.armTop",.5f);draft.Set("zone.armBottom",.6f);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            Assert.That(File.Exists(path),Is.False);
        }
        [Test]public void WriteFailureLeavesSelectionAndExistingFileIntact()
        {
            var initial=new DesignLabHistory(path,Shipped());initial.Create("test");string original=File.ReadAllText(path),hash=initial.Selected.Hash;
            var history=new DesignLabHistory(path,Shipped(),(temp,destination)=>throw new IOException("Injected atomic replace failure"));var draft=history.Selected.Snapshot.Clone();draft.Set("rifle.head",31);
            Assert.Throws<IOException>(()=>history.Save(draft));Assert.That(history.Selected.Hash,Is.EqualTo(hash));Assert.That(draft.Get("rifle.head"),Is.EqualTo(31));
            Assert.That(File.ReadAllText(path),Is.EqualTo(original));Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
        }
        [Test]public void AuthoringAndDiagnosticMetadataCannotBeSavedAsGameplayTuning()
        {
            var history=new DesignLabHistory(path,Shipped());var draft=history.Selected.Snapshot.Clone();
            var field=draft.Descriptors.First(d=>draft.IsAuthoring(d.Path));draft.Set(field.Path,draft.Get(field.Path)+field.Step);
            Assert.Throws<ArgumentException>(()=>history.Save(draft));
            Assert.That(draft.IsEditable("weapon.probeRange"),Is.False);Assert.That(draft.Domain("rifle.head"),Is.EqualTo("Gameplay"));
            Assert.That(draft.Domain("presentation.rifleTracerWidth"),Is.EqualTo("Presentation"));
        }
        [Test]public void LevelAuthoringIsExcludedWhileGameplayAndReadabilityRemainVisible()
        {
            var bundle=Shipped();
            foreach(var d in bundle.Descriptors.Where(d=>bundle.IsAuthoring(d.Path)))Assert.That(bundle.IsVisible(d.Path),Is.False,d.Path);
            foreach(var path in new[]{"layout.fill-0.x","ring.spawn-mark-1.y","wayfinding.sign-0.yaw","wayfinding.centralHalfWidth","detail.ventWidth","broadcast.screenWidth","light.fixtureWidth","ring.spaceCenterY","ring.windowFrameWidth"})
            {Assert.That(bundle.IsVisible(path),Is.False,path);Assert.That(bundle.IsEditable(path),Is.False,path);}
            foreach(var path in new[]{"rifle.head","player.movement.jumpSpeed","light.intensity","surface.wallValue","color.north.r","ring.glassOpacity","broadcast.fanSpeed"})Assert.That(bundle.IsEditable(path),Is.True,path);
        }
        [Test]public void LegacyPlacementOverrideLoadsAndIsPreservedByNewGameplayRevision()
        {
            var history=new DesignLabHistory(path,Shipped());history.Create("Legacy");
            var file=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));var old=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single();
            old.Snapshot.Set("layout.fill-0.x",1);old.Hash=old.Snapshot.Hash();string oldHash=old.Hash;
            File.WriteAllText(path,JsonUtility.ToJson(file,true));
            var restored=new DesignLabHistory(path,Shipped());Assert.That(restored.StorageError,Is.Null);
            var draft=restored.Selected.Snapshot;draft.Set("rifle.head",31);restored.Save(draft);
            var restarted=new DesignLabHistory(path,Shipped());Assert.That(restarted.StorageError,Is.Null);
            Assert.That(restarted.Selected.Snapshot.Get("layout.fill-0.x"),Is.EqualTo(1));
            Assert.That(restarted.SelectedProfile.Revisions.First().Hash,Is.EqualTo(oldHash));
            draft=restarted.Selected.Snapshot;draft.Set("layout.fill-0.x",2);Assert.Throws<ArgumentException>(()=>restarted.Save(draft));
        }
        [Test]public void AuditCleanupPreservesOldTechnicalAndModelOverridesAndBlocksNewChanges()
        {
            var history=new DesignLabHistory(path,Shipped());history.Create("Legacy audit overrides");
            var file=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));var old=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single();
            Assert.That(LabBundle.AuditedExcludedPaths.Count(),Is.EqualTo(25));
            foreach(var key in LabBundle.AuditedExcludedPaths)
            {
                var d=old.Snapshot.Profile(key).Descriptor(key);float value=old.Snapshot.Get(key);
                old.Snapshot.Set(key,value+d.Step<=d.Maximum?value+d.Step:value-d.Step);
                Assert.That(old.Snapshot.IsVisible(key),Is.False,key);
            }
            old.Hash=old.Snapshot.Hash();string priorHash=old.Hash;File.WriteAllText(path,JsonUtility.ToJson(file,true));
            var restored=new DesignLabHistory(path,Shipped());Assert.That(restored.StorageError,Is.Null);
            var draft=restored.Selected.Snapshot;draft.Set("rifle.head",31);restored.Save(draft);
            var restarted=new DesignLabHistory(path,Shipped());Assert.That(restarted.StorageError,Is.Null);
            Assert.That(restarted.SelectedProfile.Revisions.First().Hash,Is.EqualTo(priorHash));
            foreach(var key in LabBundle.AuditedExcludedPaths)
            {
                Assert.That(restarted.Selected.Snapshot.Get(key),Is.EqualTo(old.Snapshot.Get(key)),key);
                draft=restarted.Selected.Snapshot;draft.Set(key,Shipped().Get(key));
                Assert.Throws<ArgumentException>(()=>restarted.Save(draft),key);
            }
            foreach(var key in new[]{"rifle.head","player.movement.jumpSpeed","bots.navigation.stuckSeconds","light.intensity","view.switchShoulderDegrees","view.switchElbowDegrees","cutter.width"})
                Assert.That(restarted.Selected.Snapshot.IsEditable(key),Is.True,key);
        }
        [Test]public void DescriptorMigrationRefreshesPreviouslyUsedValueIndex()
        {
            var profile=ProvingProfile.CreateCombatBowlRingPresentationDefault();string key="broadcast.screenValue";float expected=profile.Get(key);
            ((System.Collections.Generic.List<NumericDescriptor>)profile.Descriptors).RemoveAll(d=>d.Path==key);
            var values=(System.Collections.Generic.List<ProvingProfileValue>)typeof(ProvingProfile).GetField("values",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(profile);values.RemoveAll(v=>v.Path==key);
            profile.EnsureOrbitalLeagueDescriptors();Assert.That(profile.Get(key),Is.EqualTo(expected));
        }
        [Test]public void PreCutterHistoryAppendsDefaultsAndPreservesOriginalHash()
        {
            var old=Shipped();old.Profiles.RemoveAll(p=>p.Id=="cutter-beam-v1");
            var first=new DesignLabHistory(path,old);first.Create("Старый");var prior=first.Selected;
            var current=new DesignLabHistory(path,Shipped());Assert.That(current.StorageError,Is.Null);
            Assert.That(current.Selected.Snapshot.Get("cutter.energyCapacity"),Is.EqualTo(30));
            Assert.That(current.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(current.Selected.Number,Is.GreaterThan(prior.Number));
        }
        static LabBundle FirstCutterRelease()
        {
            var old=Shipped();int index=old.Profiles.FindIndex(p=>p.Id=="cutter-beam-v1");
            old.Profiles[index]=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(old.Profiles[index]).Replace("\"version\":2","\"version\":1"));
            old.Set("cutter.width",0.012f);return old;
        }
        [Test]public void CutterThicknessUpgradePreservesCustomHistoryAndUpdatesRelease()
        {
            var old=FirstCutterRelease();
            var first=new DesignLabHistory(path,old);first.Create("Настроенный");
            var draft=first.Selected.Snapshot;draft.Set("cutter.width",0.021f);first.Save(draft);var prior=first.Selected;
            var current=new DesignLabHistory(path,Shipped());Assert.That(current.StorageError,Is.Null);
            Assert.That(current.Selected.Snapshot.Get("cutter.width"),Is.EqualTo(0.021f));
            Assert.That(current.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            current.DeleteSelected();Assert.That(current.Selected.Hash,Is.EqualTo(Shipped().Hash()));
            var restarted=new DesignLabHistory(path,Shipped());Assert.That(restarted.StorageError,Is.Null);
            Assert.That(restarted.Selected.Hash,Is.EqualTo(Shipped().Hash()));
        }
        [Test]public void CutterThicknessUpgradeSelectsNewShippedRevision()
        {
            var old=FirstCutterRelease();
            var first=new DesignLabHistory(path,old);first.Create("Временный");first.DeleteSelected();var prior=first.Selected;
            var current=new DesignLabHistory(path,Shipped());Assert.That(current.StorageError,Is.Null);
            Assert.That(current.Selected.Hash,Is.EqualTo(Shipped().Hash()));
            Assert.That(current.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
        }
        [Test]public void TamperedSnapshotHashFailsWithoutDestroyingFile()
        {var history=new DesignLabHistory(path,Shipped());history.Create("test");string original=File.ReadAllText(path);File.WriteAllText(path,original.Replace("\"Value\": 30.0","\"Value\": 31.0"));var restored=new DesignLabHistory(path,Shipped());Assert.That(restored.StorageError,Is.Not.Null);}
    }
}
