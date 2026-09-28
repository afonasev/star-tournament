using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class ParticipantColorAssignmentTests
    {
        NativeMatchComposition Draft(int count,NativeMatchMode mode=NativeMatchMode.Ffa,bool swapped=false)
        {
            var setup=new NativeBotSetup();setup.SetAi(0,true);
            for(int p=1;p<count;p++)setup.Add(1);
            return setup.Build(1,mode,new[]{NativeTeam.TeamA},swapped);
        }
        [Test] public void SeededAssignmentsAreUniqueSerializableAndDetachedForEveryRosterSize()
        {
            for(int count=2;count<=8;count++)for(int seed=0;seed<24;seed++)
            {
                var draft=Draft(count);var before=JsonUtility.ToJson(draft.Read());
                var frozen=NativeParticipantColors.FreezeNewMatch(draft,new System.Random(seed).Next);
                Assert.That(frozen.Read().Participants.Select(p=>p.Color).Distinct().Count(),Is.EqualTo(count));
                Assert.That(frozen.Read().Participants.All(p=>NativeParticipantColors.Contains(p.Color)),Is.True);
                Assert.That(JsonUtility.ToJson(draft.Read()),Is.EqualTo(before));
                var restored=NativeMatchComposition.Restore(JsonUtility.FromJson<NativeCompositionSnapshot>(JsonUtility.ToJson(frozen.Read())));
                Assert.That(JsonUtility.ToJson(restored.Read()),Is.EqualTo(JsonUtility.ToJson(frozen.Read())));
                Assert.That(JsonUtility.ToJson(NativeParticipantColors.FreezeNewMatch(draft,new System.Random(seed).Next).Read()),Is.EqualTo(JsonUtility.ToJson(frozen.Read())));
            }
        }
        [Test] public void ControlledDrawsProveSlotIndependenceAndFullPaletteSampling()
        {
            var draft=Draft(8);int calls=0;
            var frozen=NativeParticipantColors.FreezeNewMatch(draft,max=>{calls++;return max-1;});
            Assert.That(calls,Is.EqualTo(8));Assert.That(frozen.Participant(0).Color,Is.EqualTo(NativeParticipantColors.Read().Last()));
            Assert.That(frozen.Participant(0).Color,Is.Not.EqualTo(draft.Participant(0).Color));
            // Identical independent draws are legal; no probabilistic assertion that two matches must differ.
            Assert.That(JsonUtility.ToJson(NativeParticipantColors.FreezeNewMatch(draft,max=>0).Read()),Is.EqualTo(JsonUtility.ToJson(draft.Read())));
            Assert.Throws<ArgumentOutOfRangeException>(()=>NativeParticipantColors.FreezeNewMatch(draft,max=>max));
        }
        [Test] public void TeamsNeverDrawAndAssignmentLeavesGameplayRandomUntouched()
        {
            foreach(bool swap in new[]{false,true})
            {
                var teams=Draft(8,NativeMatchMode.Teams,swap);
                Assert.That(NativeParticipantColors.FreezeNewMatch(teams,_=>throw new Exception("Team RNG draw")),Is.SameAs(teams));
                foreach(var p in Enumerable.Range(0,8))Assert.That(teams.Participant(p).Color,Is.EqualTo(NativeStandingsView.TeamColor(teams.Roster.TeamOf(p),swap)));
            }
            var state=UnityEngine.Random.state;
            NativeParticipantColors.FreezeNewMatch(Draft(8));Assert.That(UnityEngine.Random.state,Is.EqualTo(state));
        }
    }
}
