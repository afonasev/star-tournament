using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class TimedKillChainTests
    {
        static NativeMatchState Make(float hz=1,ProvingProfile profile=null,bool teams=false)
        {
            var p=profile??ProvingProfile.CreateMatchDefault();var c=NativeMatchConfiguration.Default(p);c.DurationMinutes=30;
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}):NativeMatchRoster.Ffa(4);
            return new NativeMatchState(roster,c,p,hz);
        }
        static KillScoreEvent Kill(NativeMatchState s,int victim=2,int attacker=0,bool eligible=true)=>s.RecordDamage(victim,attacker,new DamageResult(100,true),eligible);
        static void Advance(NativeMatchState s,int ticks){for(int i=0;i<ticks;i++){s.BeginTick();s.EndTick();}}
        static NativeStanding Row(NativeMatchState s,int id=0)=>s.Read().Standings.Single(r=>r.Seat==id);
        [Test] public void EveryAdjacentKillRenewsWindowAndAwardsKeepGrowingPastFive()
        {
            var s=Make();int total=0;
            for(int n=1;n<=8;n++)
            {Advance(s,n==1?1:9);var e=Kill(s);s.EndTick();total+=n*100;
                Assert.That(e.Points,Is.EqualTo(n*100));Assert.That(e.Chain,Is.EqualTo(n));Assert.That(Row(s).Score,Is.EqualTo(total));}
            Assert.That(s.Read().Tick,Is.EqualTo(64));
        }
        [TestCase(1f)][TestCase(50f)]public void BoundaryIsInclusiveAndNextTickStartsOver(float hz)
        {
            var s=Make(hz);Advance(s,1);Kill(s);s.EndTick();Advance(s,(int)(10*hz));
            Assert.That(Kill(s).Points,Is.EqualTo(200));s.EndTick();Advance(s,(int)(10*hz)+1);
            Assert.That(s.Read().KillChain(0),Is.Zero);Assert.That(Kill(s).Points,Is.EqualTo(100));
            Assert.That(Row(s).Score,Is.EqualTo(400));
        }
        [Test]public void FrozenTuningAndNoTickPauseDoNotChangeWindow()
        {
            var p=ProvingProfile.CreateMatchDefault();var s=Make(profile:p);Advance(s,1);Kill(s);s.EndTick();
            p.Set("score.chainWindowSeconds",0);p.Set("score.chainAwardStep",900);
            for(int i=0;i<20;i++)Assert.That(s.Read().KillChain(0),Is.EqualTo(1));
            Advance(s,10);Assert.That(Kill(s).Points,Is.EqualTo(200));
        }
        [Test]public void SnapshotPreservesOriginalDeadlineAndRejectsForgedOrLegacyState()
        {
            var s=Make(50);Advance(s,1);Kill(s);s.EndTick();Advance(s,499);
            var saved=JsonUtility.FromJson<NativeMatchSnapshot>(JsonUtility.ToJson(s.Read()));var restored=Make(50);restored.Restore(saved);
            Advance(restored,1);Assert.That(Kill(restored).Points,Is.EqualTo(200));restored.EndTick();Advance(restored,501);Assert.That(Kill(restored).Points,Is.EqualTo(100));
            var bad=s.Read();bad.LastEligibleKillTicks[0]=bad.Tick+1;Assert.Throws<ArgumentException>(()=>s.Restore(bad));
            bad=s.Read();bad.LastEligibleKillTicks=Array.Empty<long>();Assert.Throws<ArgumentException>(()=>s.Restore(bad));
            Assert.Throws<ArgumentException>(()=>Make(profile:ProvingProfile.CreateLegacyMatchDefault()).Restore(saved));
            Assert.Throws<ArgumentException>(()=>s.Restore(Make(50,ProvingProfile.CreateLegacyMatchDefault()).Read()));
            var exposed=s.Read();exposed.LastEligibleKillTicks[0]=0;Assert.That(s.Read().LastEligibleKillTicks[0],Is.EqualTo(1));
        }
        [Test]public void AssistsLateProjectilesPenaltyAndSameTickEventsKeepTheirOwnPoints()
        {
            var s=Make(teams:true);Advance(s,1);var first=Kill(s);var second=Kill(s,3);var late=Kill(s,2,0,false);
            Assert.That((first.Points,second.Points,late.Points,late.Chain,late.SeriesEligible),Is.EqualTo((100,200,100,1,false)));
            Assert.That(s.Read().KillChain(0),Is.EqualTo(2));
            var ally=Kill(s,1);Assert.That(ally.Points,Is.EqualTo(-200));Assert.That(s.Read().KillChain(0),Is.Zero);
            Assert.That(Kill(s).Points,Is.EqualTo(100));var self=Kill(s,0);Assert.That(self.Points,Is.EqualTo(-200));s.EndTick();Assert.That(s.Read().KillChain(0),Is.Zero);
            Assert.That(Row(s).Score,Is.EqualTo(100));Assert.That(Row(s).AccumulatedPenalty,Is.EqualTo(400));
            Advance(s,1);s.RecordDamage(2,1,new DamageResult(10,false));Kill(s);s.EndTick();
            Assert.That(Row(s,1).Score,Is.EqualTo(50));Assert.That(s.Read().KillChain(1),Is.Zero);
        }
        [Test]public void LegacyProfileRetainsUnboundedCumulativeRulesAndRestoresExactly()
        {
            var p=ProvingProfile.CreateLegacyMatchDefault();var s=Make(profile:p);
            foreach(int expected in new[]{100,300,500,800,1200,1600}){Advance(s,11);Kill(s);s.EndTick();Assert.That(Row(s).Score,Is.EqualTo(expected));}
            var restored=Make(profile:p);restored.Restore(s.Read());Assert.That(restored.Read().Version,Is.EqualTo(2));
            Assert.That(JsonUtility.ToJson(restored.Read()),Is.EqualTo(JsonUtility.ToJson(s.Read())));
        }
        [Test]public void CurrentFirstRewardCanExceedInactiveLegacyTotals()
        {
            var p=ProvingProfile.CreateMatchDefault();p.Set("score.chainTotal1",500);var s=Make(profile:p);
            Advance(s,1);Assert.That(Kill(s).Points,Is.EqualTo(500));s.EndTick();Advance(s,1);Assert.That(Kill(s).Points,Is.EqualTo(600));
            var bundle=DesignLabHistoryTests.Shipped();bundle.Set("score.chainTotal1",500);Assert.That(bundle.Validate(),Is.Empty);
        }
        [Test]public void NoticeAndVoiceUseActualEventRatherThanCurrentChainOrTotalScore()
        {
            var third=new KillScoreEvent(0,300,3,true,true);var fourth=new KillScoreEvent(0,400,4,true,true);
            Assert.That(NativeKillNotice.Text("<color=#00FF00>Вега</color>","3:1",third),Is.EqualTo("Вы убили <color=#00FF00>Вега</color> · +300 · 3:1\nСерия убийств! · 3"));
            Assert.That(NativeKillNotice.ColorFor(third),Is.EqualTo(NativeKillNotice.SeriesColor));
            Assert.That(NativeKillNotice.Voice(third),Is.EqualTo("kill-triple"));Assert.That(NativeKillNotice.Voice(fourth),Is.EqualTo("kill-quadruple"));
            Assert.That(NativeKillNotice.Voice(new KillScoreEvent(0,100,3,true,false)),Is.Null);
            var ally=new KillScoreEvent(0,-200);Assert.That(NativeKillNotice.Text("Вега","",ally),Is.EqualTo("Вы убили союзника Вега · −200"));Assert.That(NativeKillNotice.ColorFor(ally),Is.EqualTo(Color.red));
            Assert.That(NativeKillNotice.Text("Вега","",new KillScoreEvent(0,0)),Does.EndWith("−0"));
        }
        [Test]public void LabUpgradePreservesHistoricalHashesAndOtherCustomTuning()
        {
            string directory=Path.Combine(Path.GetTempPath(),"st-timed-chain-"+Guid.NewGuid());Directory.CreateDirectory(directory);
            try
            {
                string path=Path.Combine(directory,"history.json");var shipped=DesignLabHistoryTests.Shipped();var old=shipped.Clone();
                old.Profiles[old.Profiles.FindIndex(p=>p.Id=="unity-native-match-v1")]=ProvingProfile.CreateLegacyMatchDefault();
                var history=new DesignLabHistory(path,old);history.Create("Мой баланс");var draft=history.Selected.Snapshot;draft.Set("score.assistPoints",123);draft.Set("score.chainTotal1",150);history.Save(draft);
                string hash=history.Selected.Hash;int number=history.Selected.Number;
                var updated=new DesignLabHistory(path,shipped);Assert.That(updated.StorageError,Is.Null);Assert.That(updated.Selected.Number,Is.GreaterThan(number));
                Assert.That(updated.SelectedProfile.Revisions.Single(x=>x.Number==number).Hash,Is.EqualTo(hash));
                Assert.That(updated.Selected.Snapshot.Get("score.assistPoints"),Is.EqualTo(123));Assert.That(updated.Selected.Snapshot.Get("score.chainTotal1"),Is.EqualTo(150));
                Assert.That(updated.Selected.Snapshot.Get("score.chainWindowSeconds"),Is.EqualTo(10));Assert.That(updated.Selected.Snapshot.Get("score.chainAwardStep"),Is.EqualTo(100));
                int count=updated.SelectedProfile.Revisions.Count;var reopened=new DesignLabHistory(path,shipped);Assert.That(reopened.StorageError,Is.Null);Assert.That(reopened.SelectedProfile.Revisions.Count,Is.EqualTo(count));
                Assert.That(reopened.Selected.Snapshot.IsVisible("score.chainTotal2"),Is.False);Assert.That(reopened.Selected.Snapshot.IsVisible("score.chainTotal1"),Is.True);
            }
            finally{Directory.Delete(directory,true);}
        }
    }
}
