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
        public static LabBundle Shipped()=>new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateTrooperDefault(),ProvingProfile.CreateMatchDefault(),ProvingProfile.CreateTeamDefault(),ProvingProfile.CreateRosterDefault(),ProvingProfile.CreateBotPerceptionDefault(),ProvingProfile.CreateNavigationDefault(),ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateCombatBowlRingPresentationDefault(),CombatBowlCatalog.AuthoringProfile(),ProvingProfile.CreateCutterDefault(),ProvingProfile.CreateParticipantPaletteDefault(),ProvingProfile.CreateDeathDefault()}};
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-lab-tests-"+Guid.NewGuid());Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        [Test]public void CompleteRegistryHasUniquePathsAndValidDefaults()
        {var bundle=Shipped();Assert.That(bundle.Validate(),Is.Empty,string.Join(";",bundle.Validate().Select(i=>i.Path+": "+i.Message)));Assert.That(bundle.Profiles.Count,Is.EqualTo(typeof(ProvingGround).GetFields().Count(f=>f.FieldType==typeof(ProvingProfile))),"Each public runtime profile must be represented in the bundle");Assert.That(bundle.Descriptors.Count(),Is.GreaterThan(200));Assert.That(bundle.Descriptors.Select(d=>d.Path).Distinct().Count(),Is.EqualTo(bundle.Descriptors.Count()));}
        [Test]public void AudioUpgradePreservesOldRevisionHashAndCustomCombatValues()
        {
            var old=Shipped();var current=old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId);
            old.Profiles[old.Profiles.IndexOf(current)]=current.BeforeAudio();
            var history=new DesignLabHistory(path,old);history.Create("До звука");
            var draft=history.Selected.Snapshot;draft.Set("rifle.head",31);history.Save(draft);
            string oldHash=history.Selected.Hash;
            var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("rifle.head"),Is.EqualTo(31));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.effectsDefaultPercent"),Is.EqualTo(80));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==oldHash),Is.True);
        }
        [Test]public void DeathProfileUpgradePreservesPriorRevisionHashesAndCustomValues()
        {
            var old=Shipped();old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.DeathPresentationId);
            var history=new DesignLabHistory(path,old);history.Create("До ragdoll");var draft=history.Selected.Snapshot;draft.Set("rifle.head",31);history.Save(draft);
            string hash=history.Selected.Hash;var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("rifle.head"),Is.EqualTo(31));
            Assert.That(upgraded.Selected.Snapshot.Get("corpse.shotgunSpeed"),Is.EqualTo(9));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==hash),Is.True);
            Assert.That(upgraded.Selected.Snapshot.IsEditable("corpse.shotgunSpeed"),Is.True);
        }
        [Test]public void DeathImpulseUpgradeKeepsCustomValuesAndOldHashes()
        {
            var old=Shipped();old.Profiles=old.Profiles.Select(p=>p.BeforeDeathImpulseIncrease()).ToList();
            var history=new DesignLabHistory(path,old);history.Create("Мой импульс");var draft=history.Selected.Snapshot;draft.Set("corpse.rifleSpeed",4.3f);history.Save(draft);
            string hash=history.Selected.Hash;var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("corpse.rifleSpeed"),Is.EqualTo(4.3f));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==hash),Is.True);
            Assert.That(upgraded.Selected.Snapshot.Profile("corpse.rifleSpeed").Version,Is.EqualTo(2));
            Assert.That(upgraded.Selected.Snapshot.IsEditable("corpse.rifleSpeed"),Is.True);
        }
        [Test]public void ShotOriginDescriptorMigratesOldHistoryWithoutChangingSavedValues()
        {
            var old=Shipped();var current=old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId);
            // The historical v6 registry predates damage-vignette descriptors as well.
            var profile=current.BeforeAudio().BeforeDamageVignette();old.Profiles[old.Profiles.IndexOf(current)]=profile;
            var json=JsonUtility.ToJson(profile);
            // Exact v6 predecessor: remove only the new additive descriptor/value.
            var serialized=JsonUtility.FromJson<LegacyProfile>(json);serialized.version=6;
            serialized.descriptors=serialized.descriptors.Where(d=>d.Path!="presentation.rocketMuzzleBlendDistance").ToArray();
            serialized.values=serialized.values.Where(v=>v.Path!="presentation.rocketMuzzleBlendDistance").ToArray();
            old.Profiles[old.Profiles.IndexOf(profile)]=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(serialized));
            var history=new DesignLabHistory(path,old);history.Create("Existing");var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);
            var hash=history.Selected.Hash;var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(upgraded.Selected.Snapshot.Get("presentation.rocketMuzzleBlendDistance"),Is.EqualTo(3));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(v=>v.Hash==hash),Is.True);
            Assert.That(upgraded.Selected.Snapshot.IsEditable("presentation.rocketMuzzleBlendDistance"),Is.True);
            Assert.That(upgraded.Selected.Snapshot.Validate(),Is.Empty);
        }
        [Test]public void ScoreboardUpgradePreservesOldValuesAndHashes()
        {
            var old=Shipped();
            foreach(var profile in old.Profiles.Where(p=>p.Id==ProvingProfile.DefaultId||p.Id=="unity-native-match-v1").ToArray())
            {
                var serialized=JsonUtility.FromJson<LegacyProfile>(JsonUtility.ToJson(profile));
                if(profile.Id=="unity-native-match-v1")serialized.version=1;
                serialized.descriptors=serialized.descriptors.Where(d=>d.Path!="score.friendlyOrSelfKillPenalty"&&!d.Path.StartsWith("ui.standings")).ToArray();
                serialized.values=serialized.values.Where(d=>d.Path!="score.friendlyOrSelfKillPenalty"&&!d.Path.StartsWith("ui.standings")).ToArray();
                old.Profiles[old.Profiles.IndexOf(profile)]=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(serialized));
            }
            var history=new DesignLabHistory(path,old);history.Create("Сохранённый");var draft=history.Selected.Snapshot;draft.Set("score.assistPoints",63);history.Save(draft);
            var hash=history.Selected.Hash;var revision=history.Selected.Number;var releaseHash=history.Profiles.Single(p=>p.Protected).Revisions[0].Hash;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("score.assistPoints"),Is.EqualTo(63));Assert.That(upgraded.Selected.Snapshot.Get("score.friendlyOrSelfKillPenalty"),Is.EqualTo(200));
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==revision).Hash,Is.EqualTo(hash));
            Assert.That(upgraded.Profiles.Single(p=>p.Protected).Revisions[0].Hash,Is.EqualTo(releaseHash));
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [System.Serializable]sealed class LegacyProfile{public string id;public int version;public NumericDescriptor[] descriptors;public ProvingProfileValue[] values;}
        [Test]public void SaveFromOldBaseAppendsWithoutMutatingEarlierHashesAndRestarts()
        {
            var history=new DesignLabHistory(path,Shipped());history.Create("Первый");string id=history.SelectedProfile.Id;var v1=history.Selected;
            var draft=v1.Snapshot.Clone();draft.Set("rifle.damage",31);history.Save(draft);var v2=history.Selected;
            history.Select(id,1);draft=history.Selected.Snapshot.Clone();draft.Set("rifle.damage",32);history.Save(draft);
            Assert.That(history.Selected.Number,Is.EqualTo(3));history.Rename("Новое имя");
            var restarted=new DesignLabHistory(path,Shipped());Assert.That(restarted.StorageError,Is.Null);Assert.That(restarted.Selected.Number,Is.EqualTo(3));Assert.That(restarted.SelectedProfile.Id,Is.EqualTo(id));
            Assert.That(restarted.SelectedProfile.Revisions[0].Hash,Is.EqualTo(v1.Hash));Assert.That(restarted.SelectedProfile.Revisions[1].Hash,Is.EqualTo(v2.Hash));
            var detached=restarted.Selected;detached.Snapshot.Set("rifle.damage",99);Assert.That(restarted.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(32));
        }
        [Test]public void CorruptionIsPreservedAndCannotBeOverwritten()
        {File.WriteAllText(path,"broken");var history=new DesignLabHistory(path,Shipped());Assert.That(history.StorageError,Is.Not.Null);Assert.Throws<IOException>(()=>history.Create("test"));Assert.That(File.ReadAllText(path),Is.EqualTo("broken"));}
        [Test]public void ProtectedReleaseCannotBeDeletedAndLocalProfileDeletionReturnsToRelease()
        {var history=new DesignLabHistory(path,Shipped());Assert.Throws<InvalidOperationException>(()=>history.DeleteSelected());history.Create("Локальный");history.DeleteSelected();Assert.That(history.SelectedProfile.Id,Is.EqualTo(DesignLabHistory.ReleaseId));Assert.That(history.Profiles.Count,Is.EqualTo(1));}
        [Test]public void InvalidRangeStepAndCrossFieldDraftCannotSave()
        {
            var history=new DesignLabHistory(path,Shipped());var draft=history.Selected.Snapshot.Clone();draft.Set("rifle.damage",999);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            draft=history.Selected.Snapshot.Clone();draft.Set("rifle.damage",30.5f);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            draft=history.Selected.Snapshot.Clone();draft.Set("score.chainTotal2",0);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            draft=history.Selected.Snapshot.Clone();draft.Set("zone.armTop",.5f);draft.Set("zone.armBottom",.6f);Assert.Throws<ArgumentException>(()=>history.Save(draft));
            Assert.That(File.Exists(path),Is.False);
        }
        [Test]public void WriteFailureLeavesSelectionAndExistingFileIntact()
        {
            var initial=new DesignLabHistory(path,Shipped());initial.Create("test");string original=File.ReadAllText(path),hash=initial.Selected.Hash;
            var history=new DesignLabHistory(path,Shipped(),(temp,destination)=>throw new IOException("Injected atomic replace failure"));var draft=history.Selected.Snapshot.Clone();draft.Set("rifle.damage",31);
            Assert.Throws<IOException>(()=>history.Save(draft));Assert.That(history.Selected.Hash,Is.EqualTo(hash));Assert.That(draft.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(File.ReadAllText(path),Is.EqualTo(original));Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
        }
        [Test]public void AuthoringAndDiagnosticMetadataCannotBeSavedAsGameplayTuning()
        {
            var history=new DesignLabHistory(path,Shipped());var draft=history.Selected.Snapshot.Clone();
            var field=draft.Descriptors.First(d=>draft.IsAuthoring(d.Path));draft.Set(field.Path,draft.Get(field.Path)+field.Step);
            Assert.Throws<ArgumentException>(()=>history.Save(draft));
            Assert.That(draft.IsEditable("weapon.probeRange"),Is.False);Assert.That(draft.Domain("rifle.damage"),Is.EqualTo("Gameplay"));
            Assert.That(draft.Domain("presentation.rifleTracerWidth"),Is.EqualTo("Presentation"));
        }
        [Test]public void LevelAuthoringIsExcludedWhileGameplayAndReadabilityRemainVisible()
        {
            var bundle=Shipped();
            foreach(var d in bundle.Descriptors.Where(d=>bundle.IsAuthoring(d.Path)))Assert.That(bundle.IsVisible(d.Path),Is.False,d.Path);
            foreach(var path in new[]{"layout.fill-0.x","ring.spawn-mark-1.y","wayfinding.sign-0.yaw","wayfinding.centralHalfWidth","detail.ventWidth","broadcast.screenWidth","light.fixtureWidth","ring.spaceCenterY","ring.windowFrameWidth"})
            {Assert.That(bundle.IsVisible(path),Is.False,path);Assert.That(bundle.IsEditable(path),Is.False,path);}
            foreach(var path in new[]{"rifle.damage","player.movement.jumpSpeed","light.intensity","surface.wallValue","color.north.r","ring.glassOpacity","broadcast.fanSpeed"})Assert.That(bundle.IsEditable(path),Is.True,path);
        }
        [Test]public void LegacyPlacementOverrideLoadsAndIsPreservedByNewGameplayRevision()
        {
            var history=new DesignLabHistory(path,Shipped());history.Create("Legacy");
            var file=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));var old=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single();
            old.Snapshot.Set("layout.fill-0.x",1);old.Hash=old.Snapshot.Hash();string oldHash=old.Hash;
            File.WriteAllText(path,JsonUtility.ToJson(file,true));
            var restored=new DesignLabHistory(path,Shipped());Assert.That(restored.StorageError,Is.Null);
            var draft=restored.Selected.Snapshot;draft.Set("rifle.damage",31);restored.Save(draft);
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
            var draft=restored.Selected.Snapshot;draft.Set("rifle.damage",31);restored.Save(draft);
            var restarted=new DesignLabHistory(path,Shipped());Assert.That(restarted.StorageError,Is.Null);
            Assert.That(restarted.SelectedProfile.Revisions.First().Hash,Is.EqualTo(priorHash));
            foreach(var key in LabBundle.AuditedExcludedPaths)
            {
                Assert.That(restarted.Selected.Snapshot.Get(key),Is.EqualTo(old.Snapshot.Get(key)),key);
                draft=restarted.Selected.Snapshot;draft.Set(key,Shipped().Get(key));
                Assert.Throws<ArgumentException>(()=>restarted.Save(draft),key);
            }
            foreach(var key in new[]{"rifle.damage","player.movement.jumpSpeed","bots.navigation.stuckSeconds","light.intensity","view.switchShoulderDegrees","view.switchElbowDegrees","cutter.width"})
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
            var old=Shipped();old.Profiles.RemoveAll(p=>p.Id=="cutter-beam-v1"||p.Id==ProvingProfile.ParticipantPaletteId);
            var first=new DesignLabHistory(path,old);first.Create("Старый");var prior=first.Selected;
            var current=new DesignLabHistory(path,Shipped());Assert.That(current.StorageError,Is.Null);
            Assert.That(current.Selected.Snapshot.Get("cutter.energyCapacity"),Is.EqualTo(30));
            Assert.That(current.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(current.Selected.Number,Is.GreaterThan(prior.Number));
        }
        [TestCase(false)] [TestCase(true)]
        public void CutterDamageUpgradePreservesHistoryAndRefreshesPublishedBaseline(bool custom)
        {
            var old=Shipped();old.Profiles=old.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCutterDamage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)).ToList();
            var first=new DesignLabHistory(path,old);
            // NewFile is lazy: persist the old release before simulating an application upgrade.
            first.Create("Сохранить старый опубликованный профиль");first.DeleteSelected();
            if(custom){first.Create("Мой баланс");var draft=first.Selected.Snapshot;draft.Set("cutter.referenceDamage",150);first.Save(draft);}
            var prior=first.Selected;var current=new DesignLabHistory(path,Shipped());
            Assert.That(current.StorageError,Is.Null);
            Assert.That(current.Selected.Snapshot.Get("cutter.referenceDamage"),Is.EqualTo(custom?150:300));
            Assert.That(current.Selected.Snapshot.Get("cutter.referenceContactSeconds"),Is.EqualTo(3));
            Assert.That(current.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(current.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Snapshot.Get("cutter.referenceDamage"),Is.EqualTo(custom?150:100));
            if(custom)current.DeleteSelected();
            Assert.That(current.Selected.Hash,Is.EqualTo(Shipped().Hash()));
        }
        static LabBundle FirstCutterRelease()
        {
            var old=Shipped();old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.ParticipantPaletteId);int index=old.Profiles.FindIndex(p=>p.Id=="cutter-beam-v1");
            old.Profiles[index]=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCutterThickness",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(old.Profiles[index],null);
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
        [Test]public void WeaponPickupUpgradePreservesPreviousReleaseAndUserHistory()
        {
            var priorBundle=Shipped();priorBundle.Profiles=priorBundle.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWeaponPickups",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)).ToList();
            var old=new DesignLabHistory(path,priorBundle);old.Create("До подбора");var draft=old.Selected.Snapshot;draft.Set("rifle.damage",31);old.Save(draft);var prior=old.Selected;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(upgraded.Selected.Snapshot.Get("weaponPickup.respawnSeconds"),Is.EqualTo(15));
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [Test]public void PaletteUpgradePreservesOldRevisionAndAllowsMetadataValidatedTuning()
        {
            var before=Shipped();before.Profiles.RemoveAll(p=>p.Id==ProvingProfile.ParticipantPaletteId);
            var old=new DesignLabHistory(path,before);old.Create("До новой палитры");
            var original=old.Selected;var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==original.Number).Hash,Is.EqualTo(original.Hash));
            var draft=upgraded.Selected.Snapshot;
            Assert.That(draft.IsEditable("participant.color.yellow.r"),Is.True);
            Assert.That(draft.Domain("participant.color.yellow.r"),Is.EqualTo("Presentation"));
            var descriptor=draft.Profile("participant.color.yellow.r").Descriptor("participant.color.yellow.r");
            Assert.That(descriptor.Minimum,Is.EqualTo(0));Assert.That(descriptor.Maximum,Is.EqualTo(255));Assert.That(descriptor.Step,Is.EqualTo(1));
            draft.Set("participant.color.yellow.r",254);upgraded.Save(draft);
            var restarted=new DesignLabHistory(path,Shipped());Assert.That(restarted.StorageError,Is.Null);
            Assert.That(restarted.Selected.Snapshot.Get("participant.color.yellow.r"),Is.EqualTo(254));
            foreach(var channel in new[]{"r","g","b"})draft.Set("participant.color.yellow."+channel,draft.Get("participant.color.blue."+channel));
            Assert.Throws<ArgumentException>(()=>upgraded.Save(draft));
        }
        [Test]public void TamperedSnapshotHashFailsWithoutDestroyingFile()
        {var history=new DesignLabHistory(path,Shipped());history.Create("test");string original=File.ReadAllText(path);File.WriteAllText(path,original.Replace("\"Value\": 30.0","\"Value\": 31.0"));var restored=new DesignLabHistory(path,Shipped());Assert.That(restored.StorageError,Is.Not.Null);}
    }
}
