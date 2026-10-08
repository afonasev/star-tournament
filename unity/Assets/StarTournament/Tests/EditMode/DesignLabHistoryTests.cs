using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class DesignLabHistoryTests
    {
        string directory,path;
        public static LabBundle Shipped()=>new LabBundle{Profiles={ProvingProfile.CreateDefault(),ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault(),ProvingProfile.CreateTrooperDefault(),ProvingProfile.CreateMatchDefault(),ProvingProfile.CreateTeamDefault(),ProvingProfile.CreateRosterDefault(),ProvingProfile.CreateBotPerceptionDefault(),ProvingProfile.CreateNavigationDefault(),ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateCombatBowlRingPresentationDefault(),CombatBowlCatalog.AuthoringProfile(),ProvingProfile.CreateCutterDefault(),ProvingProfile.CreateParticipantPaletteDefault(),ProvingProfile.CreateDeathDefault(),ProvingProfile.CreateBloodDefault(),ProvingProfile.CreateRocketEffectsDefault(),ProvingProfile.CreateIndustrialTunnelsPresentation(),IndustrialTunnelsCatalog.AuthoringProfile(),ProvingProfile.CreateLunarPresentation(),LunarLaboratoryCatalog.AuthoringProfile()}};
        [SetUp]public void Setup(){directory=Path.Combine(Path.GetTempPath(),"st-lab-tests-"+Guid.NewGuid());Directory.CreateDirectory(directory);path=Path.Combine(directory,"history.json");}
        [TearDown]public void Cleanup(){Directory.Delete(directory,true);}
        [Test]public void IdentitySurfaceUpgradePreservesPaletteRevisionAndCustomColors()
        {
            var prior=typeof(ProvingProfile).GetMethod("BeforeIdentitySurface",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>(ProvingProfile)prior.Invoke(p,null)).ToList();
            var history=new DesignLabHistory(path,old);history.Create("Мои цвета");
            var draft=history.Selected.Snapshot;draft.Set("participant.color.blue.r",70);history.Save(draft);
            var previous=history.Selected;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("participant.color.blue.r"),Is.EqualTo(70));
            Assert.That(upgraded.Selected.Snapshot.Get("participant.surface.panelFloor"),Is.EqualTo(.65f));
            var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Number==previous.Number);
            Assert.That(preserved.Hash,Is.EqualTo(previous.Hash));
            Assert.That(preserved.Snapshot.Descriptors.Any(d=>d.Path.StartsWith("participant.surface.")),Is.False);
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [Test]public void DiscretePanelUpgradePreservesV2SurfaceTuningAndHistory()
        {
            var prior=typeof(ProvingProfile).GetMethod("BeforeDiscreteIdentityPanels",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>(ProvingProfile)prior.Invoke(p,null)).ToList();
            var history=new DesignLabHistory(path,old);history.Create("Мои панели");
            var draft=history.Selected.Snapshot;draft.Set("participant.surface.detailGain",7);draft.Set("participant.color.blue.r",70);history.Save(draft);
            var previous=history.Selected;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("participant.surface.detailGain"),Is.EqualTo(7));
            Assert.That(upgraded.Selected.Snapshot.Get("participant.color.blue.r"),Is.EqualTo(70));
            Assert.That(upgraded.Selected.Snapshot.Get("participant.surface.shellStart"),Is.EqualTo(.09f));
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==previous.Number).Hash,Is.EqualTo(previous.Hash));
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [Test]public void CombinedPulseV1AndPaletteV2UpgradePreservesCustomHistory()
        {
            var palette=typeof(ProvingProfile).GetMethod("BeforeDiscreteIdentityPanels",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>(ProvingProfile)palette.Invoke(p.BeforePulseRefinement(),null)).ToList();
            var history=new DesignLabHistory(path,old);history.Create("Мой Pulse и панели");
            var draft=history.Selected.Snapshot;draft.Set("pulseFx.fireSizeVariation",.7f);draft.Set("participant.surface.detailGain",7);history.Save(draft);
            var prior=SeedLegacyPulseSize(history,7);string json=JsonUtility.ToJson(prior.Snapshot);
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("pulseFx.fireSizeVariation"),Is.EqualTo(.7f));
            Assert.That(upgraded.Selected.Snapshot.Get("participant.surface.detailGain"),Is.EqualTo(7));
            Assert.That(upgraded.Selected.Snapshot.Get("pulseFx.lightSeconds"),Is.EqualTo(.26f));
            Assert.That(upgraded.Selected.Snapshot.Get("participant.surface.shellStart"),Is.EqualTo(.09f));
            var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number);
            Assert.That(preserved.Hash,Is.EqualTo(prior.Hash));Assert.That(JsonUtility.ToJson(preserved.Snapshot),Is.EqualTo(json));
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [TestCase(false)][TestCase(true)]public void MovementAudioUpgradePreservesV11HistoryAndCustomMix(bool beforeCompactMenu)
        {
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>p.BeforeMovementAudio()).ToList();
            if(beforeCompactMenu)old.Profiles=old.Profiles.Select(p=>p.BeforeCompactMatchMenu()).ToList();
            Assert.That(old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId).Version,Is.EqualTo(11));
            var history=new DesignLabHistory(path,old);history.Create("Мой микс");
            var draft=history.Selected.Snapshot;draft.Set("audio.footstepGain",.3f);draft.Set("audio.footstepDistanceMeters",3.4f);history.Save(draft);
            string hash=history.Selected.Hash;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("audio.footstepGain"),Is.EqualTo(.3f));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.footstepDistanceMeters"),Is.EqualTo(3.4f));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.jumpGain"),Is.EqualTo(.38f));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.landGain"),Is.EqualTo(.6f));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==hash),Is.True);
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [TestCase(false)][TestCase(true)] public void GamepadAxesUpgradePreservesLegacyRevisionAndCustomSensitivity(bool beforeVignette)
        {
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeGamepadLook",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke((ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeGamepadTriggerAim",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p.BeforeFootstepMix(),null),null)).Select(p=>p.BeforeAudio()).ToList();
            if(beforeVignette)old.Profiles=old.Profiles.Select(p=>p.BeforeDamageVignette()).ToList();
            Assert.That(old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId).Version,Is.EqualTo(beforeVignette?7:8));
            var history=new DesignLabHistory(path,old);history.Create("Мой геймпад");
            // Construct the file written by the former runtime: its shared axis was editable.
            // The current Save contract correctly refuses edits to that obsolete field.
            var file=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));
            var prior=file.Profiles.Single(p=>p.Id==file.SelectedId).Revisions.Single(r=>r.Number==file.SelectedRevision);
            prior.Snapshot.Set("input.gamepadDegreesPerSecond",90);prior.Hash=prior.Snapshot.Hash();
            File.WriteAllText(path,JsonUtility.ToJson(file,true));
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get(GamepadLookSettings.HorizontalPath),Is.EqualTo(90));
            Assert.That(upgraded.Selected.Snapshot.Get(GamepadLookSettings.VerticalPath),Is.EqualTo(90));
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Snapshot.Descriptors.Any(d=>d.Path==GamepadLookSettings.HorizontalPath),Is.False);
        }
        [Test]public void GamepadTapThresholdAppendsCompatibleRevisionAndKeepsPriorSnapshotImmutable()
        {
            var old=Shipped();int index=old.Profiles.FindIndex(p=>p.Id==ProvingProfile.DefaultId);
            old.Profiles[index]=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeGamepadTriggerAim",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(old.Profiles[index],null);
            var history=new DesignLabHistory(path,old);Assert.That(history.StorageError,Is.Null);history.Create("До LT tap");
            var prior=history.Selected;string priorJson=JsonUtility.ToJson(prior.Snapshot);string priorHash=prior.Hash;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Descriptors.Any(d=>d.Path=="input.gamepadTapAimThresholdSeconds"),Is.True);
            Assert.That(upgraded.Selected.Snapshot.Get("input.gamepadTapAimThresholdSeconds"),Is.EqualTo(.22f));
            var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Hash==priorHash);
            Assert.That(JsonUtility.ToJson(preserved.Snapshot),Is.EqualTo(priorJson));
        }
        static LabBundle BeforeRebalance(LabBundle bundle)
        {bundle.Profiles=bundle.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)).ToList();return bundle;}
        [Test]public void MigrationCacheSeparatesShippedMetadataAndKeepsDetachedHistoricalSnapshots()
        {
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>p.BeforeUnifiedBodyDamage().BeforeTravelingRifle()).ToList();
            var original=new DesignLabHistory(path,old);original.Create("Immutable");
            string sourceFile=File.ReadAllText(path),originalHash=original.Selected.Hash;
            var metadataVariants=new[]{"First trusted label","Second trusted label","Third trusted label","First trusted label"};
            foreach(string label in metadataVariants)
            {
                File.WriteAllText(path,sourceFile);var shipped=Shipped();shipped.Profile("zone.headY").Descriptor("zone.headY").Label=label;
                var upgraded=new DesignLabHistory(path,shipped);Assert.That(upgraded.StorageError,Is.Null);
                var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Hash==originalHash);
                Assert.That(preserved.Snapshot.Profile("zone.headY").Descriptor("zone.headY").Label,Is.EqualTo(label));
                string immutableJson=JsonUtility.ToJson(preserved.Snapshot);
                var detached=upgraded.Selected.Snapshot;detached.Set("rifle.damage",99);
                var reopened=new DesignLabHistory(path,shipped);Assert.That(reopened.StorageError,Is.Null);
                Assert.That(JsonUtility.ToJson(reopened.SelectedProfile.Revisions.Single(r=>r.Hash==originalHash).Snapshot),Is.EqualTo(immutableJson));
                Assert.That(reopened.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(20));
            }
        }
        [Test]public void PulseEffectsAppendCompatibleRevisionAndKeepHistoricalHashAndRocketTuning()
        {
            var old=Shipped();old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.RocketEffectsId);
            var history=new DesignLabHistory(path,old);history.Create("Мой Pulse");
            var prior=SeedLegacyPulseSize(history,2.4f);
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("pulseFx.fireCount"),Is.EqualTo(9));
            foreach(var d in ProvingProfile.CreateRocketEffectsDefault().Descriptors)Assert.That(upgraded.Selected.Snapshot.Domain(d.Path),Is.EqualTo("Presentation"));
            Assert.That(upgraded.Selected.Snapshot.Get("presentation.rocketExplosionSize"),Is.EqualTo(2.4f));
            var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number);
            Assert.That(preserved.Hash,Is.EqualTo(prior.Hash));
            Assert.That(preserved.Snapshot.Profiles.Any(p=>p.Id==ProvingProfile.RocketEffectsId),Is.False);
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [TestCase(1)][TestCase(2)]public void SerializedPulseProfileUpgradesAtStartupAndPreservesTuning(int priorVersion)
        {
            var profile=ProvingProfile.CreateRocketEffectsDefault();profile=priorVersion==1?profile.BeforePulseRefinement():profile.BeforePulseVisibility();
            profile.Set("pulseFx.fireSizeVariation",.7f);profile.EnsureRocketEffectsDescriptors();
            Assert.That(profile.Version,Is.EqualTo(3));Assert.That(profile.Get("pulseFx.blastRadiusScale"),Is.EqualTo(.8f));
            Assert.That(profile.Get("pulseFx.fireSizeVariation"),Is.EqualTo(.7f));
            string json=JsonUtility.ToJson(profile);profile.EnsureRocketEffectsDescriptors();Assert.That(JsonUtility.ToJson(profile),Is.EqualTo(json));
        }
        [Test]public void PulseV2VisibilityUpgradePreservesHistoryAndUsesSelectedScale()
        {
            var old=Shipped();old.Profiles=old.Profiles.Select(p=>p.BeforePulseVisibility()).ToList();
            Assert.That(old.Profiles.Single(p=>p.Id==ProvingProfile.RocketEffectsId).Version,Is.EqualTo(2));
            var history=new DesignLabHistory(path,old);history.Create("Pulse v2");var draft=history.Selected.Snapshot;
            draft.Set("pulseFx.lightIntensity",6);history.Save(draft);var prior=history.Selected;string json=JsonUtility.ToJson(prior.Snapshot);
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("pulseFx.blastRadiusScale"),Is.EqualTo(.8f));
            Assert.That(upgraded.Selected.Snapshot.Get("pulseFx.lightIntensity"),Is.EqualTo(6));
            var preserved=upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number);
            Assert.That(preserved.Hash,Is.EqualTo(prior.Hash));Assert.That(JsonUtility.ToJson(preserved.Snapshot),Is.EqualTo(json));
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        LabRevision SeedLegacyPulseSize(DesignLabHistory history,float value)
        {
            // Simulate a file saved by v1 when this control was editable. Current UI correctly hides it.
            var file=JsonUtility.FromJson<LabHistoryFile>(File.ReadAllText(path));
            var revision=file.Profiles.Single(p=>p.Id==history.SelectedProfileId).Revisions.Single(r=>r.Number==history.Selected.Number);
            revision.Snapshot.Set("presentation.rocketExplosionSize",value);revision.Hash=revision.Snapshot.Hash();
            File.WriteAllText(path,JsonUtility.ToJson(file));return revision;
        }
        [Test]public void PulseV1RevisionMigratesLightMetadataWithoutChangingItsHashOrCustomValues()
        {
            var old=Shipped();old.Profiles=old.Profiles.Select(p=>p.BeforePulseRefinement()).ToList();
            var history=new DesignLabHistory(path,old);history.Create("Pulse v1");var draft=history.Selected.Snapshot;
            draft.Set("pulseFx.fireSizeVariation",.7f);history.Save(draft);
            var seeded=SeedLegacyPulseSize(history,7);string hash=seeded.Hash,json=JsonUtility.ToJson(seeded.Snapshot);
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("pulseFx.lightSeconds"),Is.EqualTo(.26f));
            Assert.That(upgraded.Selected.Snapshot.Get("pulseFx.fireSizeVariation"),Is.EqualTo(.7f));
            Assert.That(upgraded.Selected.Snapshot.IsVisible("presentation.rocketExplosionSize"),Is.False);
            Assert.That(JsonUtility.ToJson(upgraded.SelectedProfile.Revisions.Single(r=>r.Hash==hash).Snapshot),Is.EqualTo(json));
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [Test]public void CompleteRegistryHasUniquePathsAndValidDefaults()
        {var bundle=Shipped();Assert.That(bundle.Validate(),Is.Empty,string.Join(";",bundle.Validate().Select(i=>i.Path+": "+i.Message)));Assert.That(bundle.Profiles.Count,Is.EqualTo(typeof(ProvingGround).GetFields().Count(f=>f.FieldType==typeof(ProvingProfile))),"Each public runtime profile must be represented in the bundle");Assert.That(bundle.Descriptors.Count(),Is.GreaterThan(200));Assert.That(bundle.Descriptors.Select(d=>d.Path).Distinct().Count(),Is.EqualTo(bundle.Descriptors.Count()));}
        [Test]public void TravelingRifleMigratesExactOldHistoryWithoutLosingRevisionOrTuning()
        {
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>p.BeforeUnifiedBodyDamage().BeforeTravelingRifle()).ToList();
            var history=new DesignLabHistory(path,old);history.Create("Hitscan");
            var draft=history.Selected.Snapshot;draft.Set("rifle.range",120);draft.Set("rifle.head",44);draft.Set("rifle.torso",31);history.Save(draft);
            var before=history.Selected;string hash=before.Hash;int revision=before.Number;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("rifle.speed"),Is.EqualTo(100));
            Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(upgraded.Selected.Snapshot.Descriptors.Any(d=>d.Path=="rifle.range"),Is.False);
            var preserved=upgraded.Profiles.Single(p=>p.Id==upgraded.SelectedProfile.Id).Revisions.Single(r=>r.Number==revision);
            Assert.That(preserved.Hash,Is.EqualTo(hash));Assert.That(preserved.Snapshot.Get("rifle.range"),Is.EqualTo(120));Assert.That(preserved.Snapshot.Get("rifle.head"),Is.EqualTo(44));
            var reopened=new DesignLabHistory(path,Shipped());Assert.That(reopened.StorageError,Is.Null);
            Assert.That(reopened.Selected.Number,Is.EqualTo(upgraded.Selected.Number));Assert.That(reopened.Selected.Hash,Is.EqualTo(upgraded.Selected.Hash));
        }
        [Test]public void TravelingRiflePreservesPrePulseVersionAndLoadsItsHistory()
        {
            var beforePulse=typeof(ProvingProfile).GetMethod("BeforePulse",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Where(p=>p.Id!=ProvingProfile.ParticipantPaletteId && p.Id!="cutter-beam-v1").Select(p=>(ProvingProfile)beforePulse.Invoke(p.BeforeUnifiedBodyDamage().BeforeTravelingRifle(),null)).ToList();
            Assert.That(old.Profiles.Single(p=>p.Id=="unity-native-combat-v1").Version,Is.EqualTo(3));
            Assert.That(old.Descriptors.Any(d=>d.Path=="rifle.range"),Is.True);
            var history=new DesignLabHistory(path,old);history.Create("До Pulse");var draft=history.Selected.Snapshot;draft.Set("rifle.head",44);draft.Set("rifle.torso",31);history.Save(draft);
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(upgraded.Selected.Snapshot.Get("rifle.speed"),Is.EqualTo(100));Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Snapshot.Descriptors.Any(d=>d.Path=="rifle.head")&&r.Snapshot.Get("rifle.head")==44),Is.True);
        }
        [Test]public void AudioUpgradePreservesOldRevisionHashAndCustomCombatValues()
        {
            var old=BeforeRebalance(Shipped());var current=old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId);
            var beforeLook=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeGamepadLook",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(current.BeforeFootstepMix(),null);
            old.Profiles[old.Profiles.IndexOf(current)]=beforeLook.BeforeAudio();
            Assert.That(old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId).Version,Is.EqualTo(8));
            var history=new DesignLabHistory(path,old);history.Create("До звука");
            var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);
            string oldHash=history.Selected.Hash;
            var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.effectsDefaultPercent"),Is.EqualTo(80));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==oldHash),Is.True);
        }
        [Test]public void FootstepMixUpgradeRetunesOldDefaultAndPreservesHistory()
        {
            var old=BeforeRebalance(Shipped());var current=old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId);
            old.Profiles[old.Profiles.IndexOf(current)]=current.BeforeFootstepMix();
            var history=new DesignLabHistory(path,old);history.Create("Старые шаги");
            string oldHash=history.Selected.Hash;
            var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("audio.footstepDistanceMeters"),Is.EqualTo(2.1f));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.footstepGain"),Is.EqualTo(.5f));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==oldHash),Is.True);
        }
        [Test]public void FootstepMixUpgradeKeepsCustomOldInterval()
        {
            var old=BeforeRebalance(Shipped());var current=old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId);
            old.Profiles[old.Profiles.IndexOf(current)]=current.BeforeFootstepMix();
            var history=new DesignLabHistory(path,old);history.Create("Свои шаги");
            var draft=history.Selected.Snapshot;draft.Set("audio.footstepDistanceMeters",3.4f);history.Save(draft);
            string oldHash=history.Selected.Hash;
            var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("audio.footstepDistanceMeters"),Is.EqualTo(3.4f));
            Assert.That(upgraded.Selected.Snapshot.Get("audio.footstepGain"),Is.EqualTo(.5f));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==oldHash),Is.True);
        }
        [Test]public void SerializedV10ProfileGetsNewStepDefaultsWithoutOverwritingCustomInterval()
        {
            var shipped=ProvingProfile.CreateDefault().BeforeFootstepMix();
            shipped.EnsureDefaultDescriptors();
            Assert.That(shipped.Version,Is.EqualTo(ProvingProfile.DefaultVersion));
            Assert.That(shipped.Get("audio.footstepDistanceMeters"),Is.EqualTo(2.1f));
            Assert.That(shipped.Descriptor("audio.footstepDistanceMeters").DefaultValue,Is.EqualTo(2.1f));
            Assert.That(shipped.Get("audio.footstepGain"),Is.EqualTo(.5f));
            var custom=ProvingProfile.CreateDefault().BeforeFootstepMix();
            custom.Set("audio.footstepDistanceMeters",3.4f);
            custom.EnsureDefaultDescriptors();
            Assert.That(custom.Get("audio.footstepDistanceMeters"),Is.EqualTo(3.4f));
        }
        [Test]public void GamepadUpgradePreservesAudioOnlyRevisionHash()
        {
            var old=BeforeRebalance(Shipped());var current=old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId);
            var prior=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeGamepadLook",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(current.BeforeFootstepMix(),null);
            Assert.That(prior.Version,Is.EqualTo(9));
            old.Profiles[old.Profiles.IndexOf(current)]=prior;
            var history=new DesignLabHistory(path,old);history.Create("Звук до отдельных осей");
            string oldHash=history.Selected.Hash;
            var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.Selected.Snapshot.Get("audio.effectsDefaultPercent"),Is.EqualTo(80));
            Assert.That(upgraded.Selected.Snapshot.Get(GamepadLookSettings.HorizontalPath),Is.EqualTo(180));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==oldHash),Is.True);
        }
        [Test]public void DeathProfileUpgradePreservesPriorRevisionHashesAndCustomValues()
        {
            var old=BeforeRebalance(Shipped());old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.DeathPresentationId);
            var history=new DesignLabHistory(path,old);history.Create("До ragdoll");var draft=history.Selected.Snapshot;draft.Set("rifle.damage",31);history.Save(draft);
            string hash=history.Selected.Hash;var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));
            Assert.That(upgraded.Selected.Snapshot.Get("corpse.shotgunSpeed"),Is.EqualTo(9));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==hash),Is.True);
            Assert.That(upgraded.Selected.Snapshot.IsEditable("corpse.shotgunSpeed"),Is.True);
        }
        [Test]public void DeathImpulseUpgradeKeepsCustomValuesAndOldHashes()
        {
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>p.BeforeDeathImpulseIncrease()).ToList();
            var history=new DesignLabHistory(path,old);history.Create("Мой импульс");var draft=history.Selected.Snapshot;draft.Set("corpse.rifleSpeed",4.3f);history.Save(draft);
            string hash=history.Selected.Hash;var upgraded=new DesignLabHistory(path,Shipped());
            Assert.That(upgraded.StorageError,Is.Null);Assert.That(upgraded.Selected.Snapshot.Get("corpse.rifleSpeed"),Is.EqualTo(4.3f));
            Assert.That(upgraded.SelectedProfile.Revisions.Any(r=>r.Hash==hash),Is.True);
            Assert.That(upgraded.Selected.Snapshot.Profile("corpse.rifleSpeed").Version,Is.EqualTo(2));
            Assert.That(upgraded.Selected.Snapshot.IsEditable("corpse.rifleSpeed"),Is.True);
        }
        [Test]public void ShotOriginDescriptorMigratesOldHistoryWithoutChangingSavedValues()
        {
            var old=BeforeRebalance(Shipped());var current=old.Profiles.Single(p=>p.Id==ProvingProfile.DefaultId);
            // The historical v6 registry predates damage-vignette descriptors as well.
            var beforeLook=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeGamepadLook",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(current.BeforeFootstepMix(),null);
            var profile=beforeLook.BeforeAudio().BeforeDamageVignette();old.Profiles[old.Profiles.IndexOf(current)]=profile;
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
            var old=BeforeRebalance(Shipped());
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
        [Test]public void DefaultDisplayPreservesLegacyHistoryAndUserNames()
        {
            var history=new DesignLabHistory(path,Shipped());Assert.That(history.SelectedProfileName,Is.EqualTo("Default"));
            history.Rename("Опубликованный профиль");var source=File.ReadAllBytes(path);var hash=history.Selected.Hash;
            var reopened=new DesignLabHistory(path,Shipped());Assert.That(reopened.StorageError,Is.Null);
            Assert.That(reopened.SelectedProfileName,Is.EqualTo("Default"));Assert.That(reopened.SelectedProfile.Name,Is.EqualTo("Default"));
            Assert.That(reopened.Profiles.Single(p=>p.Id==DesignLabHistory.ReleaseId).Name,Is.EqualTo("Default"));
            Assert.That(reopened.Selected.Hash,Is.EqualTo(hash));Assert.That(File.ReadAllBytes(path),Is.EqualTo(source));
            reopened.Rename("Мой баланс");Assert.That(new DesignLabHistory(path,Shipped()).SelectedProfileName,Is.EqualTo("Мой баланс"));
            reopened.Create("Опубликованный профиль");Assert.That(reopened.SelectedProfileName,Is.EqualTo("Опубликованный профиль"));
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
        [Test]public async Task AsyncSelectionPreservesSnapshotsAndBlocksConcurrentMutation()
        {
            var seed=new DesignLabHistory(path,Shipped());seed.Create("async");
            var draft=seed.Selected.Snapshot;draft.Set("rifle.damage",31);seed.Save(draft);
            string id=seed.SelectedProfileId;
            var hashes=seed.SelectedProfile.Revisions.Select(r=>r.Number+":"+r.Hash).ToArray();
            using(var entered=new ManualResetEventSlim())using(var release=new ManualResetEventSlim())
            {
                var history=new DesignLabHistory(path,Shipped(),(temp,destination)=>
                {
                    entered.Set();
                    if(!release.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("Test did not release writer");
                    File.Replace(temp,destination,null);
                });
                var operation=history.SelectAsync(id,1);
                try
                {
                    Assert.That(await Task.Run(()=>entered.Wait(TimeSpan.FromSeconds(25))),Is.True);
                    Assert.That(operation.IsCompleted,Is.False,"Selection yields while disk publication is held");
                    Assert.That(history.Busy,Is.True);Assert.That(history.Selected.Number,Is.EqualTo(2));
                    Assert.Throws<InvalidOperationException>(()=>history.Select(id,1));
                }
                finally {release.Set();await operation;}
                Assert.That(history.Busy,Is.False);Assert.That(history.Selected.Number,Is.EqualTo(1));
                Assert.That(history.SelectedProfile.Revisions.Select(r=>r.Number+":"+r.Hash),Is.EqualTo(hashes));
                Assert.That(new DesignLabHistory(path,Shipped()).Selected.Number,Is.EqualTo(1));
            }
        }
        [Test]public async Task AsyncWriteFailureLeavesSelectionAndHistoryIntact()
        {
            var seed=new DesignLabHistory(path,Shipped());seed.Create("async");
            var draft=seed.Selected.Snapshot;draft.Set("rifle.damage",31);seed.Save(draft);
            var bytes=File.ReadAllBytes(path);var hash=seed.Selected.Hash;
            var history=new DesignLabHistory(path,Shipped(),(temp,destination)=>throw new IOException("Injected failure"));
            IOException error=null;
            try {await history.SelectAsync(seed.SelectedProfileId,1);}
            catch(IOException failure){error=failure;}
            Assert.That(error,Is.Not.Null);
            Assert.That(history.Busy,Is.False);Assert.That(history.Selected.Hash,Is.EqualTo(hash));
            Assert.That(File.ReadAllBytes(path),Is.EqualTo(bytes));
            Assert.That(Directory.GetFiles(directory),Has.Length.EqualTo(1));
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
            Assert.That(LabBundle.AuditedExcludedPaths.Count(),Is.EqualTo(30));
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
            var old=BeforeRebalance(Shipped());old.Profiles.RemoveAll(p=>p.Id=="cutter-beam-v1"||p.Id==ProvingProfile.ParticipantPaletteId);
            var first=new DesignLabHistory(path,old);first.Create("Старый");var prior=first.Selected;
            var current=new DesignLabHistory(path,Shipped());Assert.That(current.StorageError,Is.Null);
            Assert.That(current.Selected.Snapshot.Get("cutter.energyCapacity"),Is.EqualTo(30));
            Assert.That(current.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(current.Selected.Number,Is.GreaterThan(prior.Number));
        }
        [TestCase(false)] [TestCase(true)]
        public void CutterDamageUpgradePreservesHistoryAndRefreshesPublishedBaseline(bool custom)
        {
            var old=BeforeRebalance(Shipped());old.Profiles=old.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCutterDamage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)).ToList();
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
            var old=BeforeRebalance(Shipped());old.Profiles.RemoveAll(p=>p.Id==ProvingProfile.ParticipantPaletteId);int index=old.Profiles.FindIndex(p=>p.Id=="cutter-beam-v1");
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
            var priorBundle=BeforeRebalance(Shipped());priorBundle.Profiles=priorBundle.Profiles.Select(p=>(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWeaponPickups",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(p,null)).ToList();
            var old=new DesignLabHistory(path,priorBundle);old.Create("До подбора");var draft=old.Selected.Snapshot;draft.Set("rifle.damage",31);old.Save(draft);var prior=old.Selected;
            var upgraded=new DesignLabHistory(path,Shipped());Assert.That(upgraded.StorageError,Is.Null);
            Assert.That(upgraded.SelectedProfile.Revisions.Single(r=>r.Number==prior.Number).Hash,Is.EqualTo(prior.Hash));
            Assert.That(upgraded.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(31));Assert.That(upgraded.Selected.Snapshot.Get("weaponPickup.respawnSeconds"),Is.EqualTo(15));
            Assert.That(new DesignLabHistory(path,Shipped()).StorageError,Is.Null);
        }
        [Test]public void PaletteUpgradePreservesOldRevisionAndAllowsMetadataValidatedTuning()
        {
            var before=BeforeRebalance(Shipped());before.Profiles.RemoveAll(p=>p.Id==ProvingProfile.ParticipantPaletteId);
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
