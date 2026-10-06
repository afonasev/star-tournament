using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class UnifiedDamageTests
    {
        NativeMatchState Teams()
        {
            var p=ProvingProfile.CreateMatchDefault();
            if(p.Descriptor("score.friendlyOrSelfKillPenalty")!=null)p.Set("score.friendlyOrSelfKillPenalty",0);
            return new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}),NativeMatchConfiguration.Default(p),p,50);
        }
        static NativeStanding Row(NativeMatchState s,int id)=>s.Read().Standings.Single(r=>r.Seat==id);
        [Test] public void ActualLossIncludesShieldOnlyOverflowAndCappedOverkill()
        {
            var life=new CombatLife("victim",ProvingProfile.CreateCombatDefault());life.GrantArmor(50);
            var shield=life.Damage(1,20);Assert.That(shield.HealthLost,Is.Zero);Assert.That(shield.ArmorLost,Is.EqualTo(20));Assert.That(shield.Applied,Is.EqualTo(20));
            var mixed=life.Damage(1,45);Assert.That(mixed.HealthLost,Is.EqualTo(15));Assert.That(mixed.ArmorLost,Is.EqualTo(30));Assert.That(mixed.Applied,Is.EqualTo(45));
            life.GrantArmor(10);var lethal=life.Damage(1,1000);Assert.That(lethal.Applied,Is.EqualTo(95));Assert.That(lethal.Killed,Is.True);
            Assert.That(life.Damage(1,10).Applied,Is.Zero);
            var fractional=new CombatLife("fractional",ProvingProfile.CreateCombatDefault());fractional.GrantArmor(.1f);
            var tick=fractional.Damage(1,1f/3);
            Assert.That(tick.HealthLost,Is.EqualTo(100-fractional.Read().Health));
            Assert.That(tick.ArmorLost,Is.EqualTo(.1f-fractional.Read().Armor));
            Assert.That(tick.Applied,Is.EqualTo(tick.HealthLost+tick.ArmorLost));
        }
        [Test] public void EveryBodyZoneHasWeaponSpecificDamageAndDescriptorsValidateSteps()
        {
            var p=ProvingProfile.CreateNativeCombatDefault();var r=new ShotgunResolver(p,1.8f);
            foreach(HitZone zone in Enum.GetValues(typeof(HitZone)))
            {Assert.That(r.FullDamage(zone,WeaponId.Rifle),Is.EqualTo(10));Assert.That(r.FullDamage(zone,WeaponId.Shotgun),Is.EqualTo(85));}
            foreach(var path in new[]{"rifle.damage","shot.damage","damage.friendlyMultiplier","damage.selfMultiplier"})Assert.That(p.Descriptor(path).Validate(out _),Is.True,path);
            p.Set("damage.friendlyMultiplier",.53f);Assert.That(p.Validate(),Is.Not.Empty);p.Set("damage.friendlyMultiplier",.5f);
            p.Set("damage.selfMultiplier",1.05f);Assert.That(p.Validate(),Is.Not.Empty);
        }
        [Test] public void FriendlyAndSelfLethalsClearRewardsAndChainWithoutReducingKills()
        {
            var s=Teams();s.BeginTick();s.RecordDamage(2,0,new DamageResult(100,true));s.EndTick();
            s.BeginTick();s.RecordDamage(1,2,new DamageResult(8,false));s.RecordDamage(1,0,new DamageResult(30,20,true));s.EndTick();
            Assert.That(Row(s,0).Kills,Is.EqualTo(1));Assert.That(Row(s,0).Score,Is.EqualTo(100));Assert.That(s.Read().KillChain(0),Is.Zero);
            Assert.That(Row(s,0).AllyDamageDealt,Is.EqualTo(50));Assert.That(Row(s,0).DamageDealt,Is.EqualTo(100));Assert.That(Row(s,2).Assists,Is.Zero);
            s.BeginTick();s.RecordDamage(3,0,new DamageResult(100,true));s.EndTick();Assert.That(Row(s,0).Score,Is.EqualTo(200));Assert.That(s.Read().KillChain(0),Is.EqualTo(1));
            s.BeginTick();s.RecordDamage(0,3,new DamageResult(4,false));s.RecordDamage(0,0,new DamageResult(25,true));s.EndTick();
            Assert.That(Row(s,3).Assists,Is.Zero);Assert.That(Row(s,0).DamageReceived,Is.EqualTo(29));Assert.That(Row(s,0).AllyDamageDealt,Is.EqualTo(50));Assert.That(s.Read().KillChain(0),Is.Zero);
        }
        [Test] public void OldLifeTeamkillDoesNotResetNewLifeChainAndFriendlyHitsCannotAssist()
        {
            var s=Teams();s.BeginTick();s.RecordDamage(2,0,new DamageResult(100,true));s.EndTick();
            s.BeginTick();s.RecordDamage(1,0,new DamageResult(10,false));s.RecordDamage(1,0,new DamageResult(90,true),false);s.EndTick();Assert.That(s.Read().KillChain(0),Is.EqualTo(1));
            s.BeginTick();s.RecordDamage(1,3,new DamageResult(100,true));s.EndTick();Assert.That(Row(s,0).Assists,Is.Zero);
            var json=JsonUtility.FromJson<NativeMatchSnapshot>(JsonUtility.ToJson(s.Read()));Assert.That(json.Standings.Single(r=>r.Seat==0).AllyDamageDealt,Is.EqualTo(100));
            Assert.That(Teams().Read().Standings.All(r=>r.DamageDealt==0&&r.AllyDamageDealt==0&&r.DamageReceived==0),Is.True);
        }
        [Test] public void EnvironmentalLethalKeepsPriorEnemyAssist()
        {
            var s=Teams();s.BeginTick();s.RecordDamage(1,2,new DamageResult(10,false));
            s.RecordDamage(1,-1,new DamageResult(90,true));s.EndTick();
            Assert.That(Row(s,2).Assists,Is.EqualTo(1));Assert.That(Row(s,2).Kills,Is.Zero);
        }
        [Test] public void HistoricalZonalValuesAndHashesSurviveMigrationToTorsoDamage()
        {
            var directory=Path.Combine(Path.GetTempPath(),"unified-damage-"+Guid.NewGuid());var path=Path.Combine(directory,"history.json");
            try
            {
                var current=new LabBundle{Profiles=new System.Collections.Generic.List<ProvingProfile>{ProvingProfile.CreateNativeCombatDefault()}};
                var old=current.Clone();old.Profiles[0]=((ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeWeaponRebalance",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(old.Profiles[0],null)).BeforeUnifiedBodyDamage();
                var h=new DesignLabHistory(path,old);h.Create("История");var draft=h.Selected.Snapshot;draft.Set("rifle.head",44);draft.Set("rifle.torso",27);draft.Set("shot.torso",81);h.Save(draft);
                var prior=h.Selected;string id=h.SelectedProfileId;
                var migrated=new DesignLabHistory(path,current);Assert.That(migrated.StorageError,Is.Null);
                Assert.That(migrated.Selected.Snapshot.Get("rifle.damage"),Is.EqualTo(27));Assert.That(migrated.Selected.Snapshot.Get("shot.damage"),Is.EqualTo(81));
                var preserved=migrated.Profiles.Single(p=>p.Id==id).Revisions.Single(r=>r.Number==prior.Number);
                Assert.That(preserved.Hash,Is.EqualTo(prior.Hash));Assert.That(preserved.Snapshot.Hash(),Is.EqualTo(prior.Hash));Assert.That(preserved.Snapshot.Get("rifle.head"),Is.EqualTo(44));
                Assert.That(migrated.Selected.Snapshot.Descriptors.Any(d=>d.Path=="rifle.head"),Is.False);
                var restarted=new DesignLabHistory(path,current);Assert.That(restarted.StorageError,Is.Null);Assert.That(restarted.Selected.Hash,Is.EqualTo(migrated.Selected.Hash));
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
