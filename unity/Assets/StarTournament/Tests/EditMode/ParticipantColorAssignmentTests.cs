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
        [Test] public void ApprovedPaletteIncludesBrightYellowAndEightDistinctColors()
        {
            Assert.That(NativeParticipantColors.Read().Select(c=>ColorUtility.ToHtmlStringRGB(c)),Is.EqualTo(new[]{"459EFF","EE6BFF","FFE641","FFAD35","38D5FF","40E878","FF5656","F4F7FF"}));
        }
        [Test] public void PaletteTuningAppliesAtStartAndFrozenLegacyColorsRestoreWithoutCurrentPalette()
        {
            var profile=ProvingProfile.CreateParticipantPaletteDefault();profile.Set("participant.color.blue.r",70);
            var frozen=NativeParticipantColors.FreezeNewMatch(Draft(8),_=>0,profile);
            Assert.That(frozen.Participant(0).Color,Is.EqualTo((Color)new Color32(70,158,255,255)));
            profile.Set("participant.color.blue.r",71);
            Assert.That(NativeMatchComposition.Restore(frozen.Read()).Participant(0).Color,Is.EqualTo(frozen.Participant(0).Color));
            var legacy=Draft(2).Read();legacy.Participants[0].Color=new Color32(59,130,246,255);legacy.Participants[1].Color=new Color32(217,70,239,255);
            Assert.That(NativeMatchComposition.Restore(legacy).Read().Participants.Select(p=>p.Color),Is.EqualTo(legacy.Participants.Select(p=>p.Color)));
            foreach(bool swap in new[]{false,true})
            {
                var draft=Draft(8,NativeMatchMode.Teams,swap);
                var teams=NativeParticipantColors.FreezeNewMatch(draft,_=>throw new Exception("Team RNG"),profile);
                var palette=NativeParticipantColors.Read(profile);
                for(int p=0;p<8;p++)Assert.That(teams.Participant(p).Color,Is.EqualTo(palette[draft.Participant(p).Color==NativeParticipantColors.For(0)?0:1]));
            }
        }
        [Test] public void PaletteRejectsOutOfRangeFractionalAndDuplicateColors()
        {
            var profile=ProvingProfile.CreateParticipantPaletteDefault();profile.Set("participant.color.yellow.r",256);
            Assert.Throws<ArgumentException>(()=>NativeParticipantColors.Read(profile));
            profile.Set("participant.color.yellow.r",254.5f);Assert.Throws<ArgumentException>(()=>NativeParticipantColors.Read(profile));
            foreach(var channel in new[]{"r","g","b"})profile.Set("participant.color.yellow."+channel,profile.Get("participant.color.blue."+channel));
            Assert.Throws<ArgumentException>(()=>NativeParticipantColors.Read(profile));
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
