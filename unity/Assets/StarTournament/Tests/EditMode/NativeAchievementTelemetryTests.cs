using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class NativeAchievementTelemetryTests
    {
        static NativeMatchState Make(ProvingProfile profile=null)
        {
            profile=profile??ProvingProfile.CreateMatchDefault();
            var config=NativeMatchConfiguration.Default(profile);config.TargetEnabled=true;config.TargetPoints=1000;
            for(int tier=1;tier<=5;tier++)profile.Set("score.chainTotal"+tier,1000);
            return new NativeMatchState(2,config,profile,1);
        }

        [Test]
        public void CountersAndDamageAreRecordedAsAppliedFacts()
        {
            var match=Make();match.BeginTick();
            match.RecordMovement(0,2.5,true);match.RecordShot(0);match.RecordWeaponSwitch(0);
            match.RecordPickup(0,NativeBotPickupKind.Heal);match.RecordPickup(0,NativeBotPickupKind.Weapon);
            match.RecordDamage(0,0,new DamageResult(4,false),originalSource:0);
            match.RecordDamage(1,0,new DamageResult(6,false),originalSource:0);
            var row=match.Read().Standings.Single(x=>x.Seat==0);var victim=match.Read().Standings.Single(x=>x.Seat==1);
            Assert.That(row.DistanceTravelled,Is.EqualTo(2.5));Assert.That(row.Jumps,Is.EqualTo(1));
            Assert.That(row.Shots,Is.EqualTo(1));Assert.That(row.WeaponSwitches,Is.EqualTo(1));Assert.That(row.HealPickups,Is.EqualTo(1));Assert.That(row.BonusPickups,Is.EqualTo(1));
            Assert.That(row.SelfDamageDealt,Is.EqualTo(4));Assert.That(row.DamageDealt,Is.EqualTo(6));Assert.That(victim.EnemyDamageReceived,Is.EqualTo(6));
        }

        [Test]
        public void FinishedTelemetrySnapshotRestoresFrozenProjectionAndRejectsInvalidBeforeMutation()
        {
            var match=Make();match.ConfigureAchievementRecipients(new[]{true,false});match.BeginTick();
            match.RecordMovement(0,2,true);match.RecordDamage(1,0,new DamageResult(100,true));match.EndTick();
            Assert.That(match.Phase,Is.EqualTo(NativeMatchPhase.Finished));
            var saved=JsonUtility.FromJson<NativeMatchSnapshot>(JsonUtility.ToJson(match.Read()));Assert.That(saved.AwardsFrozen,Is.True);Assert.That(saved.AchievementTelemetryVersion,Is.EqualTo(1));
            var restored=Make();restored.Restore(saved);Assert.That(restored.Read().AwardsFrozen,Is.True);
            Assert.That(restored.Read().AwardSeed,Is.EqualTo(saved.AwardSeed));
            Assert.That(saved.Achievements.Length,Is.EqualTo(1));
            Assert.That(restored.Read().Achievements[0].Id,Is.EqualTo(saved.Achievements[0].Id));
            var exposed=restored.Read();exposed.Achievements[0].Name="mutated";exposed.AwardRecipients[0]=false;restored.RecordShot(0);restored.RecordMovement(0,10,true);
            Assert.That(restored.Read().Achievements[0].Name,Is.EqualTo(saved.Achievements[0].Name));
            Assert.That(restored.Read().AwardRecipients[0],Is.True);Assert.That(restored.Read().Standings.Single(x=>x.Seat==0).Shots,Is.Zero);
            var forged=restored.Read();forged.Achievements[0].Fact="fake";
            Assert.Throws<ArgumentException>(()=>restored.Restore(forged));
            var invalid=restored.Read();invalid.AwardRecipients=Array.Empty<bool>();
            Assert.Throws<ArgumentException>(()=>restored.Restore(invalid));
            Assert.That(restored.Read().AwardRecipients.Length,Is.EqualTo(2));
        }

        [Test]
        public void LegacyMarkerDisablesAwardsForRestoredMatch()
        {
            var source=Make();source.ConfigureAchievementRecipients(new[]{true,false});source.BeginTick();
            var old=source.Read();old.AchievementTelemetryVersion=0;old.AwardRecipients=null;old.Achievements=null;old.AwardsFrozen=false;
            var restored=Make();restored.Restore(old);Assert.That(restored.Read().Achievements,Is.Empty);
            restored.BeginTick();restored.RecordDamage(1,0,new DamageResult(100,true));restored.EndTick();
            Assert.That(restored.Read().Achievements,Is.Empty);
        }
    }
}
