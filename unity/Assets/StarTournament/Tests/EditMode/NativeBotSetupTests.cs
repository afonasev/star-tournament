using System;
using System.Linq;
using NUnit.Framework;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class NativeBotSetupTests
    {
        readonly NativeTeam[] teams={NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamA,NativeTeam.TeamB};
        [Test] public void LimitsAndSoloAreValidatedWithoutDiscardingBots()
        {
            var draft=new NativeBotSetup();Assert.Throws<ArgumentOutOfRangeException>(()=>draft.Build(1,NativeMatchMode.Ffa,teams,false));
            for(int i=0;i<7;i++)draft.Add(1);
            Assert.That(draft.CanAdd(1),Is.False);Assert.Throws<ArgumentException>(()=>draft.Add(1));
            var composition=draft.Build(1,NativeMatchMode.Ffa,teams,false);
            Assert.That(composition.ParticipantCount,Is.EqualTo(8));Assert.That(composition.LocalCount,Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(()=>draft.Build(2,NativeMatchMode.Ffa,teams,false));
            draft.Remove(3);draft.Add(1);Assert.That(Enumerable.Range(0,7).Select(i=>draft.At(i).Name).Distinct().Count(),Is.EqualTo(7));
        }
        [Test] public void TeamsDifficultiesAndFrozenCopyStayIndependent()
        {
            var draft=new NativeBotSetup();draft.Add(1);draft.SetDifficulty(0,2);draft.SetTeam(0,NativeTeam.TeamA);
            Assert.Throws<ArgumentException>(()=>draft.Build(1,NativeMatchMode.Teams,teams,false));
            draft.SetTeam(0,NativeTeam.TeamB);var frozen=draft.Build(1,NativeMatchMode.Teams,teams,true);
            draft.SetDifficulty(0,0);draft.Remove(0);
            Assert.That(frozen.Participant(1).Label,Is.EqualTo("Vega · Ветеран"));Assert.That(frozen.Roster.AreAllies(0,1),Is.False);
            Assert.That(frozen.Participant(1).Color,Is.EqualTo(NativeStandingsView.TeamColor(NativeTeam.TeamB,true)));
        }
        [Test] public void EveryHumanCountAndDifficultyBuildsWithoutBotDevices()
        {
            for(int humans=1;humans<=4;humans++)for(int difficulty=0;difficulty<3;difficulty++)
            {
                var draft=new NativeBotSetup();for(int i=humans;i<8;i++){draft.Add(humans);draft.SetDifficulty(i-humans,difficulty);}
                var composition=draft.Build(humans,NativeMatchMode.Teams,teams,false);
                Assert.That(composition.LocalCount,Is.EqualTo(humans));
                for(int p=humans;p<8;p++){Assert.That(composition.SeatOf(p),Is.EqualTo(-1));Assert.That(composition.Participant(p).Difficulty,Is.EqualTo(difficulty));}
            }
        }
    }
}
