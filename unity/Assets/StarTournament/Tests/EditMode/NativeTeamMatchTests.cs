using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeTeamMatchTests
    {
        static NativeMatchRoster Roster(int count=4) => new NativeMatchRoster(NativeMatchMode.Teams,
            Enumerable.Range(0,count).Select(i=>i%2==0 ? NativeTeam.TeamA : NativeTeam.TeamB).ToArray());
        static NativeMatchState Make(bool target=false, int count=4, int firstAward=100)
        {
            var p=ProvingProfile.CreateLegacyMatchDefault();
            for(int i=1;i<=5;i++) p.Set("score.chainTotal"+i,Math.Max(firstAward,p.Get("score.chainTotal"+i)));
            var c=NativeMatchConfiguration.Default(p);c.DurationMinutes=1;c.TargetEnabled=target;c.TargetPoints=1000;
            return new NativeMatchState(Roster(count),c,p,1);
        }
        static void Kill(NativeMatchState s,int victim,int killer) => s.RecordDamage(victim,killer,new DamageResult(100,true));
        static void Tick(NativeMatchState s,Action events=null) { s.BeginTick();events?.Invoke();s.EndTick(); }
        static int Total(NativeMatchState s,NativeTeam t) => s.Read().Teams.Single(r=>r.Team==t).Score;

        [Test] public void RosterRejectsInvalidCountsModesAndAssignments()
        {
            foreach(int count in new[]{-1,0,1,9}) Assert.Throws<ArgumentOutOfRangeException>(()=>NativeMatchRoster.Ffa(count));
            Assert.Throws<ArgumentNullException>(()=>new NativeMatchRoster(NativeMatchMode.Teams,null));
            Assert.Throws<ArgumentException>(()=>new NativeMatchRoster((NativeMatchMode)2,new NativeTeam[2]));
            Assert.Throws<ArgumentException>(()=>new NativeMatchRoster(NativeMatchMode.Ffa,new[]{NativeTeam.TeamA,NativeTeam.None}));
            foreach(var invalid in new[]{new[]{NativeTeam.TeamA,NativeTeam.TeamA},new[]{NativeTeam.TeamB,NativeTeam.TeamB},
                new[]{NativeTeam.TeamA,NativeTeam.None},new[]{NativeTeam.TeamA,(NativeTeam)9}})
                Assert.Throws<ArgumentException>(()=>new NativeMatchRoster(NativeMatchMode.Teams,invalid));
            Assert.That(Roster(2).Count,Is.EqualTo(2));Assert.That(Roster(8).Count,Is.EqualTo(8));
            Assert.That(NativeMatchRoster.Ffa(8).Read().Teams,Is.All.EqualTo(NativeTeam.None));
            // Uneven but nonempty teams are valid; no invented balancing restriction.
            Assert.That(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}).Count,Is.EqualTo(3));
        }
        [Test] public void RosterCopiesBothConstructorInputAndReadOutput()
        {
            var input=new[]{NativeTeam.TeamA,NativeTeam.TeamB};var roster=new NativeMatchRoster(NativeMatchMode.Teams,input);
            input[0]=NativeTeam.TeamB;var copy=roster.Read();copy.Mode=NativeMatchMode.Ffa;copy.Teams[1]=NativeTeam.TeamA;
            Assert.That(roster.Mode,Is.EqualTo(NativeMatchMode.Teams));Assert.That(roster.TeamOf(0),Is.EqualTo(NativeTeam.TeamA));
            Assert.That(roster.TeamOf(1),Is.EqualTo(NativeTeam.TeamB));
        }
        [Test] public void TeamSumReachesTargetWithoutAnyIndividualReachingIt()
        {
            var s=Make(true,8,500);Tick(s,()=>{Kill(s,1,0);Kill(s,3,6);});
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Finished));Assert.That(s.Read().WinnerTeam,Is.EqualTo(NativeTeam.TeamA));
            Assert.That(s.Read().Winner,Is.EqualTo(-1));Assert.That(s.Read().Standings.Max(r=>r.Score),Is.EqualTo(500));
            Assert.That(Total(s,NativeTeam.TeamA),Is.EqualTo(1000));Assert.That(s.Read().Roster.Teams.Length,Is.EqualTo(8));
        }
        [Test] public void AtomicUnequalTargetCrossingsChooseHigherTeamIncludingLateEvents()
        {
            var s=Make(true,8,1000);Tick(s,()=>{Kill(s,1,0);Kill(s,2,3);Kill(s,4,5);});
            Assert.That(s.Read().WinnerTeam,Is.EqualTo(NativeTeam.TeamB));Assert.That(Total(s,NativeTeam.TeamB),Is.EqualTo(2000));
            Assert.That(s.Read().Teams[0].Team,Is.EqualTo(NativeTeam.TeamB));
        }
        [Test] public void FinalTickTargetTieEntersOvertimeAndAssistBreaksTieWithoutNewTimeLimit()
        {
            var s=Make(true,4,1000);for(int i=0;i<59;i++)Tick(s);
            Tick(s,()=>{Kill(s,1,0);Kill(s,2,3);});
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Overtime));Assert.That(s.Read().Trigger,Is.EqualTo("score-limit"));
            Assert.That(s.Read().WinnerTeam,Is.EqualTo(NativeTeam.None));Assert.That(s.RemainingSeconds,Is.Zero);
            // Beyond another full regulation duration, still tied and running overtime.
            for(int i=0;i<61;i++)Tick(s);
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Overtime));
            Tick(s,()=>{s.RecordDamage(1,2,new DamageResult(1.25f,false));Kill(s,1,0);Kill(s,2,3);});
            Assert.That(s.Read().WinnerTeam,Is.EqualTo(NativeTeam.TeamA));Assert.That(s.Read().Trigger,Is.EqualTo("score-limit"));
            Assert.That(Total(s,NativeTeam.TeamA),Is.EqualTo(1050));Assert.That(Total(s,NativeTeam.TeamB),Is.EqualTo(1000));
            Assert.That(s.Read().KillChain(0),Is.EqualTo(2));
        }
        [Test] public void TimeWinnerUsesTeamSumRatherThanTopIndividual()
        {
            var s=Make();Tick(s,()=>{Kill(s,1,0);Kill(s,3,2);});
            Tick(s,()=>{Kill(s,1,0);Kill(s,3,2);});
            Tick(s,()=>Kill(s,0,1));Tick(s,()=>Kill(s,2,1));Tick(s,()=>Kill(s,0,1));
            Assert.That(Total(s,NativeTeam.TeamA),Is.EqualTo(600));Assert.That(Total(s,NativeTeam.TeamB),Is.EqualTo(500));
            for(int i=5;i<60;i++)Tick(s);
            Assert.That(s.Read().Standings[0].Seat,Is.EqualTo(1));Assert.That(s.Read().WinnerTeam,Is.EqualTo(NativeTeam.TeamA));
            Assert.That(s.Read().Trigger,Is.EqualTo("time-limit"));
        }
        [Test] public void ZeroScoreTimeTieKeepsTimeTriggerUntilTeamBLeads()
        {
            var s=Make();for(int i=0;i<60;i++)Tick(s);Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Overtime));
            Tick(s,()=>Kill(s,0,1));Assert.That(s.Read().WinnerTeam,Is.EqualTo(NativeTeam.TeamB));
            Assert.That(s.Read().Trigger,Is.EqualTo("time-limit"));Assert.That(s.Read().RemainingTicks,Is.Zero);
        }
        [Test] public void SnapshotJsonIsIndependentAndFinishedStateRejectsFurtherEvents()
        {
            var s=Make(true,4,1000);Tick(s,()=>Kill(s,1,0));var before=JsonUtility.ToJson(s.Read());
            var copy=JsonUtility.FromJson<NativeMatchSnapshot>(before);
            Assert.That(copy.Roster.Mode,Is.EqualTo(NativeMatchMode.Teams));Assert.That(copy.WinnerTeam,Is.EqualTo(NativeTeam.TeamA));
            Assert.That(copy.Standings.Length,Is.EqualTo(4));Assert.That(copy.Teams[0].Score,Is.EqualTo(1000));
            var direct=s.Read();direct.Roster.Teams[0]=NativeTeam.TeamB;direct.Teams[0].Score=-5;direct.Standings[0].Score=-1;
            Tick(s,()=>Kill(s,0,1));Assert.That(JsonUtility.ToJson(s.Read()),Is.EqualTo(before));
            var fresh=new NativeMatchState(s.Roster,s.Configuration,ProvingProfile.CreateMatchDefault(),1);
            Assert.That(fresh.Read().Tick,Is.Zero);Assert.That(fresh.Read().Standings.All(r=>r.Score==0),Is.True);
            Tick(fresh,()=>Kill(fresh,1,0));Assert.That(Total(fresh,NativeTeam.TeamA),Is.EqualTo(100));
            Assert.That(fresh.Read().Standings.Sum(r=>r.Assists),Is.Zero);
        }
        [Test] public void EightParticipantFfaKeepsIndividualWinnerAndEmptyTeamTotals()
        {
            var p=ProvingProfile.CreateMatchDefault();var c=NativeMatchConfiguration.Default(p);c.DurationMinutes=1;
            var s=new NativeMatchState(8,c,p,1);Tick(s,()=>Kill(s,0,7));for(int i=1;i<60;i++)Tick(s);
            Assert.That(s.Read().Winner,Is.EqualTo(7));Assert.That(s.Read().WinnerTeam,Is.EqualTo(NativeTeam.None));
            Assert.That(s.Read().Teams,Is.Empty);Assert.That(s.Read().Roster.Mode,Is.EqualTo(NativeMatchMode.Ffa));
        }
        [Test] public void EightParticipantReducerDiagnostic()
        {
            // Bounded diagnostic workload, not a Player/frame-rate acceptance threshold.
            var p=ProvingProfile.CreateMatchDefault();var c=NativeMatchConfiguration.Default(p);c.DurationMinutes=30;
            var s=new NativeMatchState(Roster(8),c,p,50);var samples=new double[10000];
            var clock=new Stopwatch();
            for(int i=0;i<samples.Length;i++)
            {
                clock.Restart();Tick(s,()=>{for(int j=0;j<8;j++)s.RecordDamage(j,(j+1)%8,new DamageResult(1.25f,false));});
                clock.Stop();samples[i]=clock.Elapsed.TotalMilliseconds;
            }
            Array.Sort(samples);
            UnityEngine.Debug.Log($"TEAM_REDUCER_DIAGNOSTIC ticks={samples.Length} participants=8 p50ms={samples[5000]:F6} p95ms={samples[9500]:F6} p99ms={samples[9900]:F6} worstms={samples[9999]:F6} editor_not_player=true");
            Assert.That(s.Read().Tick,Is.EqualTo(10000));Assert.That(s.Read().Standings.Sum(r=>r.DamageDealt),Is.EqualTo(100000));
            Assert.That(s.Phase,Is.EqualTo(NativeMatchPhase.Running));
        }
    }
}
