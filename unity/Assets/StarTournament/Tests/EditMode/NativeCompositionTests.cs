using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeCompositionTests
    {
        static NativeParticipantInfo[] Infos(int count,params int[] humans)=>Enumerable.Range(0,count).Select(i=>
            new NativeParticipantInfo(humans.Contains(i)?NativeParticipantKind.LocalHuman:NativeParticipantKind.DiagnosticFixture,"P"+i,NativeParticipantColors.For(i))).ToArray();
        [TestCase(2,1)] [TestCase(8,1)] [TestCase(8,4)]
        public void IndependentCountsAndFrozenSnapshot(int count,int seats)
        {
            var mapping=Enumerable.Range(count-seats,seats).Reverse().ToArray();var info=Infos(count,mapping);
            var composition=new NativeMatchComposition(NativeMatchRoster.Ffa(count),info,mapping);
            int original=mapping[0];mapping[0]=0;info[original].Name="changed";
            var snapshot=composition.Read();snapshot.LocalParticipants[0]=0;snapshot.Participants[original].Name="aliased";
            Assert.That(composition.ParticipantAt(0),Is.EqualTo(original));Assert.That(composition.Participant(original).Name,Is.EqualTo("P"+original));
            var restored=NativeMatchComposition.Restore(JsonUtility.FromJson<NativeCompositionSnapshot>(JsonUtility.ToJson(composition.Read())));
            Assert.That(restored.Read().LocalParticipants,Is.EqualTo(composition.Read().LocalParticipants));Assert.That(restored.ParticipantCount,Is.EqualTo(count));
        }
        [Test] public void SeatZeroControlsParticipantSevenAndClearsOldActions()
        {
            var c=new NativeMatchComposition(NativeMatchRoster.Ffa(8),Infos(8,7,2),new[]{7,2});
            var output=Enumerable.Repeat(new LocalAction{Fire=true},8).ToArray();
            c.AssembleLocalActions(new[]{new LocalAction{Move=Vector2.right},new LocalAction{Jump=true}},output);
            Assert.That(output[7].Move,Is.EqualTo(Vector2.right));Assert.That(output[2].Jump,Is.True);
            Assert.That(output.All(a=>!a.Fire),Is.True);Assert.That(c.SeatOf(7),Is.Zero);Assert.That(c.SeatOf(0),Is.EqualTo(-1));
        }
        [Test] public void BotViewsHaveSeatsButNeverReceiveHumanActions()
        {
            var info=new[]{
                new NativeParticipantInfo(NativeParticipantKind.Bot,"Бот 1",NativeParticipantColors.For(0),1),
                new NativeParticipantInfo(NativeParticipantKind.LocalHuman,"P1",NativeParticipantColors.For(1))};
            var c=new NativeMatchComposition(NativeMatchRoster.Ffa(2),info,new[]{0,1});
            var output=new LocalAction[2];
            c.AssembleLocalActions(new[]{new LocalAction{Fire=true,Jump=true},new LocalAction{Move=Vector2.left}},output);
            Assert.That(output[0].Fire,Is.False);Assert.That(output[0].Jump,Is.False);
            Assert.That(output[1].Move,Is.EqualTo(Vector2.left));
            info[0].Kind=NativeParticipantKind.DiagnosticFixture;
            Assert.Throws<ArgumentException>(()=>new NativeMatchComposition(NativeMatchRoster.Ffa(2),info,new[]{0,1}));
        }
        [Test] public void RejectsInvalidMappingsKindsAndDifficulty()
        {
            var r=NativeMatchRoster.Ffa(2);
            foreach(var map in new[]{new int[0],new[]{0,0},new[]{0,2},new[]{-1},new[]{0,1,0,1,0}})
                Assert.Throws<ArgumentException>(()=>new NativeMatchComposition(r,Infos(2,0,1),map));
            Assert.Throws<ArgumentException>(()=>new NativeMatchComposition(r,Infos(2,0,1),new[]{0}));
            Assert.Throws<ArgumentException>(()=>new NativeMatchComposition(r,Infos(2,0),new[]{1}));
            var info=Infos(2,0);info[1].Kind=(NativeParticipantKind)99;
            Assert.Throws<ArgumentException>(()=>new NativeMatchComposition(r,info,new[]{0}));
            info[1]=new NativeParticipantInfo(NativeParticipantKind.Bot,"Bot",NativeParticipantColors.For(1),99);
            Assert.Throws<ArgumentException>(()=>new NativeMatchComposition(r,info,new[]{0}));
            info[1]=new NativeParticipantInfo(NativeParticipantKind.Bot,"Bot",NativeParticipantColors.For(1),1);
            var c=new NativeMatchComposition(r,info,new[]{0});
            Assert.That(c.Participant(1).Label,Is.EqualTo("Bot · Боец"));Assert.Throws<InvalidOperationException>(()=>c.RequireSupportedSources(false,true));
            c.RequireSupportedSources(true,false);
            var fixture=new NativeMatchComposition(r,Infos(2,0),new[]{0});
            Assert.Throws<InvalidOperationException>(()=>fixture.RequireSupportedSources(true,false));
        }
    }
}
