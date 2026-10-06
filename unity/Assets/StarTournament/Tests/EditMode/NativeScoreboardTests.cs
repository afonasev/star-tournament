using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeScoreboardTests
    {
        static NativeMatchState Make(bool teams=true,bool target=false,ProvingProfile profile=null)
        {
            var p=profile??ProvingProfile.CreateMatchDefault();var c=NativeMatchConfiguration.Default(p);c.DurationMinutes=1;c.TargetEnabled=target;c.TargetPoints=1000;
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}):NativeMatchRoster.Ffa(4);
            return new NativeMatchState(roster,c,p,1);
        }
        static NativeStanding Row(NativeMatchState state,int id)=>state.Read().Standings.Single(s=>s.Seat==id);
        static void Hit(NativeMatchState s,int victim,int source,float applied,bool killed=false)=>s.RecordDamage(victim,source,new DamageResult(applied,killed));
        [Test] public void EnemyAllySelfAndWorldAreSeparatedAndPenaltyCannotBeErasedByChainReward()
        {
            var s=Make();s.BeginTick();Hit(s,2,0,100,true);Hit(s,1,0,30,true);Hit(s,0,0,20,true);s.EndTick();
            var r=Row(s,0);Assert.That((r.Kills,r.AllyKills,r.SelfKills,r.Score,r.AccumulatedPenalty),Is.EqualTo((1,1,1,-300,400)));
            Assert.That((r.DamageDealt,r.AllyDamageDealt,r.DamageReceived),Is.EqualTo((100d,30d,20d)));
            s.BeginTick();Hit(s,3,0,100,true);s.EndTick();Assert.That(Row(s,0).Score,Is.EqualTo(-200));Assert.That(Row(s,0).AccumulatedPenalty,Is.EqualTo(400));
            s.BeginTick();Hit(s,1,-1,100,true);Hit(s,2,0,0,true);s.EndTick();Assert.That(Row(s,1).SelfKills,Is.Zero);Assert.That(Row(s,0).Kills,Is.EqualTo(2));
            Assert.That(s.Read().DirectKills(0,1),Is.Zero);
        }
        [Test] public void OriginalFriendlySourceIsRetainedWithNoEnemyRewardsOrAlliedAssists()
        {
            var s=Make();s.BeginTick();Hit(s,2,3,25);Hit(s,2,0,25);s.RecordDamage(2,-1,new DamageResult(50,true),originalSource:3);s.EndTick();
            Assert.That(Row(s,3).AllyDamageDealt,Is.EqualTo(75));Assert.That(Row(s,3).AllyKills,Is.EqualTo(1));Assert.That(Row(s,3).Score,Is.EqualTo(-200));
            Assert.That(Row(s,3).Assists,Is.Zero);Assert.That(Row(s,0).Assists,Is.Zero,"Friendly lethal cannot award an enemy assist");
            var ffa=Make(false);ffa.BeginTick();Hit(ffa,1,0,100,true);Hit(ffa,0,0,40,true);ffa.EndTick();
            Assert.That(NativeStandingsView.Values(Row(ffa,0),false)[4],Is.EqualTo("100"));Assert.That(Row(ffa,0).Score,Is.EqualTo(-100));
        }
        [Test] public void SameTickNetTargetRankingAndWinnerAreEvaluatedAfterPenalty()
        {
            var p=ProvingProfile.CreateMatchDefault();for(int i=1;i<=5;i++)p.Set("score.chainTotal"+i,1000);
            var s=Make(false,true,p);s.BeginTick();Hit(s,2,0,100,true);Hit(s,0,0,100,true);s.EndTick();
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Running));Assert.That(Row(s,0).Score,Is.EqualTo(800));
            s.BeginTick();Hit(s,3,1,100,true);s.EndTick();Assert.That(s.Read().Winner,Is.EqualTo(1));Assert.That(s.Read().Standings[0].Score,Is.EqualTo(1000));
            var teams=Make();teams.BeginTick();Hit(teams,0,0,100,true);Hit(teams,1,0,100,true);teams.EndTick();
            Assert.That(teams.Read().Teams.Single(t=>t.Team==NativeTeam.TeamA).Score,Is.EqualTo(-400));Assert.That(teams.Read().Teams[0].Team,Is.EqualTo(NativeTeam.TeamB));
        }
        [Test] public void FrozenPenaltySerializableLedgerFinalAndFreshRepeat()
        {
            var p=ProvingProfile.CreateMatchDefault();var s=Make(false,false,p);p.Set("score.friendlyOrSelfKillPenalty",600);
            s.BeginTick();Hit(s,3,2,25);Hit(s,1,0,100,true);Hit(s,0,0,100,true);s.EndTick();
            var snapshot=JsonUtility.FromJson<NativeMatchSnapshot>(JsonUtility.ToJson(s.Read()));var restored=Make(false);restored.Restore(snapshot);
            Assert.That(restored.Phase,Is.EqualTo(NativeMatchPhase.Running));Assert.That(restored.Read().Trigger,Is.Null);
            var mismatched=Make(false,false,p);Assert.Throws<ArgumentException>(()=>mismatched.Restore(snapshot));
            restored.BeginTick();Hit(restored,3,1,75,true);restored.EndTick();Assert.That(Row(restored,2).Assists,Is.EqualTo(1));Assert.That(Row(restored,0).AccumulatedPenalty,Is.EqualTo(200));
            for(int guard=0;guard<60&&restored.Read().Tick<60;guard++){restored.BeginTick();restored.EndTick();}
            Assert.That(restored.Read().Tick,Is.EqualTo(60),"Match stopped advancing before its one-minute time limit: "+JsonUtility.ToJson(restored.Read()));
            Assert.That(restored.Read().Winner,Is.EqualTo(1));
            var final=JsonUtility.FromJson<NativeMatchSnapshot>(JsonUtility.ToJson(restored.Read()));var finalRestore=Make(false);finalRestore.Restore(final);Hit(finalRestore,1,1,100,true);
            Assert.That(JsonUtility.ToJson(finalRestore.Read()),Is.EqualTo(JsonUtility.ToJson(final)));
            Assert.That(Make(false).Read().Standings.All(r=>r.Score==0&&r.AccumulatedPenalty==0&&r.AllyDamageDealt==0&&r.SelfKills==0&&r.AllyKills==0),Is.True);
        }
        [Test] public void MetadataAndHistoricalProjectionAreCompatible()
        {
            var p=ProvingProfile.CreateMatchDefault();var d=p.Descriptor("score.friendlyOrSelfKillPenalty");Assert.That((d.Minimum,d.Maximum,d.Step,d.DefaultValue),Is.EqualTo((0f,20000f,1f,200f)));
            var old=JsonUtility.FromJson<NativeMatchSnapshot>("{\"Standings\":[{\"Seat\":0,\"Kills\":2,\"Score\":300,\"DamageDealt\":200}]}");
            Assert.That(old.Standings[0].AccumulatedPenalty,Is.Zero);Assert.That(old.Standings[0].AllyDamageDealt,Is.Zero);
            Assert.That(NativeStandingsView.Values(old.Standings[0],true)[7],Is.EqualTo("300"));
        }
    }
}
