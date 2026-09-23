using System;
using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeMatchTests
    {
        static NativeMatchState Make(bool target=false, float hz=1, ProvingProfile profile=null)
        {
            var p=profile??ProvingProfile.CreateMatchDefault(); var c=NativeMatchConfiguration.Default(p);
            c.DurationMinutes=1;c.TargetEnabled=target;c.TargetPoints=1000;
            return new NativeMatchState(4,c,p,hz);
        }
        static NativeStanding Row(NativeMatchState s,int seat) => Array.Find(s.Read().Standings,r=>r.Seat==seat);
        static void Kill(NativeMatchState s,int victim,int killer) => s.RecordDamage(victim,killer,new DamageResult(100,true));
        [Test] public void ChainAwardsAreCumulativeThenIncrementAndDeathResetsAfterMutualKills()
        {
            var s=Make(); int[] expected={100,300,500,800,1200,1600};
            foreach(int value in expected) { s.BeginTick();Kill(s,1,0);s.EndTick();Assert.That(Row(s,0).Score,Is.EqualTo(value)); }
            s.BeginTick();Kill(s,0,2);Kill(s,2,0);s.EndTick();
            int before=Row(s,0).Score;s.BeginTick();Kill(s,1,0);s.EndTick();Assert.That(Row(s,0).Score,Is.EqualTo(before+100));
        }
        [Test] public void InclusiveAssistWindowDeduplicatesHitsAndClearsVictimLedger()
        {
            var s=Make();s.BeginTick();s.RecordDamage(1,2,new DamageResult(10.25f,false));s.RecordDamage(1,2,new DamageResult(9.5f,false));s.EndTick();
            for(int i=0;i<5;i++){s.BeginTick();if(i==4)Kill(s,1,0);s.EndTick();}
            Assert.That(Row(s,2).Assists,Is.EqualTo(1));Assert.That(Row(s,2).Score,Is.EqualTo(50));Assert.That(Row(s,2).DamageDealt,Is.EqualTo(19.75));
            Assert.That(Row(s,0).Assists,Is.Zero);Assert.That(Row(s,1).Assists,Is.Zero);
            s.BeginTick();Kill(s,1,0);s.EndTick();Assert.That(Row(s,2).Assists,Is.EqualTo(1));
        }
        [Test] public void AssistOutsideWindowAndWorldSelfKillsDoNotAwardKillerPoints()
        {
            var s=Make();s.BeginTick();s.RecordDamage(1,2,new DamageResult(1,false));s.EndTick();
            for(int i=0;i<6;i++){s.BeginTick();if(i==5)Kill(s,1,0);s.EndTick();}
            Assert.That(Row(s,2).Assists,Is.Zero);
            s.BeginTick();Kill(s,2,-1);Kill(s,3,3);s.EndTick();
            Assert.That(Row(s,2).Deaths,Is.EqualTo(1));Assert.That(Row(s,3).Kills,Is.Zero);
        }
        [Test] public void ChainContinuesAcrossAnyGapUntilDeathAndPairCountsRemain()
        {
            var s=Make();s.BeginTick();Kill(s,1,0);s.EndTick();
            for(int i=0;i<10;i++){s.BeginTick();if(i==9)Kill(s,1,0);s.EndTick();}
            Assert.That(Row(s,0).Score,Is.EqualTo(300));
            for(int i=0;i<11;i++){s.BeginTick();if(i==10)Kill(s,1,0);s.EndTick();}
            Assert.That(Row(s,0).Score,Is.EqualTo(500));
            Assert.That(s.Read().DirectKills(0,1),Is.EqualTo(3));
            Assert.That(s.Read().KillChain(0),Is.EqualTo(3));
            s.BeginTick();s.RecordDamage(1,2,new DamageResult(1,false));Kill(s,1,0);s.EndTick();
            Assert.That(s.Read().DirectKills(2,1),Is.Zero);
            s.BeginTick();Kill(s,0,1);s.EndTick();
            Assert.That(s.Read().DirectKills(0,1),Is.EqualTo(4));
            Assert.That(s.Read().DirectKills(1,0),Is.EqualTo(1));
            Assert.That(s.Read().KillChain(0),Is.Zero);
            var snapshot=s.Read();snapshot.DirectKillsByPair[1]=99;snapshot.KillChains[0]=99;
            Assert.That(s.Read().DirectKills(0,1),Is.EqualTo(4));
            Assert.That(s.Read().KillChain(0),Is.Zero);
            Assert.That(Make().Read().DirectKills(0,1),Is.Zero);
        }
        [Test] public void FinalTickAppliesAllAwardsAndTargetPrecedesTimeWithFrozenResult()
        {
            var p=ProvingProfile.CreateMatchDefault();p.Set("score.chainTotal1",1000);p.Set("score.chainTotal2",1000);p.Set("score.chainTotal3",1000);p.Set("score.chainTotal4",1000);
            var s=Make(true,1,p);for(int i=0;i<59;i++){s.BeginTick();s.EndTick();}
            s.BeginTick();Kill(s,2,0);Kill(s,3,1);s.EndTick();
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Overtime));Assert.That(s.Read().Trigger,Is.EqualTo("score-limit"));
            s.BeginTick();s.RecordDamage(2,0,new DamageResult(1,false));Kill(s,2,1);s.EndTick();
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Finished));Assert.That(s.Read().Winner,Is.EqualTo(0)); // assist breaks equal chain totals
            var snapshot=s.Read();snapshot.Standings[0].Score=-1;
            long tick=s.Read().Tick;s.BeginTick();Kill(s,0,1);s.EndTick();
            Assert.That(s.Read().Tick,Is.EqualTo(tick));Assert.That(s.Read().Standings[0].Score,Is.EqualTo(1050));
        }
        [Test] public void TimeOvertimeKeepsOriginalTriggerAndNoExtraTimeLimit()
        {
            var s=Make();for(int i=0;i<61;i++){s.BeginTick();s.EndTick();}
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Overtime));Assert.That(s.RemainingSeconds,Is.Zero);
            s.BeginTick();Kill(s,1,0);s.EndTick();Assert.That(s.Read().Trigger,Is.EqualTo("time-limit"));Assert.That(s.Read().Winner,Is.Zero);
        }
        [Test] public void EarlyTargetOvertimeZerosTimerThroughFinish()
        {
            var p=ProvingProfile.CreateMatchDefault();for(int i=1;i<=4;i++)p.Set("score.chainTotal"+i,1000);
            var s=Make(true,50,p);s.BeginTick();Kill(s,2,0);Kill(s,3,1);s.EndTick();
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Overtime));Assert.That(s.Read().RemainingTicks,Is.Zero);
            s.BeginTick();s.RecordDamage(2,0,new DamageResult(1,false));Kill(s,2,1);s.EndTick();
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Finished));Assert.That(s.RemainingSeconds,Is.Zero);
        }
        [Test] public void ConfigurationMetadataRejectsBadStepsAndProfileIsFrozen()
        {
            var p=ProvingProfile.CreateMatchDefault();var c=NativeMatchConfiguration.Default(p);c.TargetPoints=1001;
            Assert.Throws<ArgumentException>(()=>new NativeMatchState(4,c,p,50));
            var s=Make(false,50,p);p.Set("score.chainTotal1",900);s.BeginTick();Kill(s,1,0);s.EndTick();Assert.That(Row(s,0).Score,Is.EqualTo(100));
            p.Set("score.chainTotal1",301);Assert.Throws<ArgumentException>(()=>Make(false,50,p));
        }
    }
}
