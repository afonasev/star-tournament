using System;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public enum NativeParticipantKind { LocalHuman, Bot, DiagnosticFixture }

    [Serializable]
    public struct NativeParticipantInfo
    {
        public NativeParticipantKind Kind;
        public string Name;
        public Color Color;
        // -1 is an absent difficulty; explicit integer tag survives Unity's field-based serialization.
        public int Difficulty;
        public NativeParticipantInfo(NativeParticipantKind kind,string name,Color color,int difficulty=-1)
        { Kind=kind;Name=name;Color=color;Difficulty=difficulty; }
        public string Label => Kind==NativeParticipantKind.Bot ? Name+" · "+new[]{"Салага","Боец","Ветеран"}[Difficulty] :
            Name+(Kind==NativeParticipantKind.LocalHuman?" · ИГРОК":" · FIXTURE");
    }

    [Serializable]
    public sealed class NativeCompositionSnapshot
    {
        public NativeRosterSnapshot Roster;
        public NativeParticipantInfo[] Participants;
        public int[] LocalParticipants;
    }

    /// <summary>Frozen session composition. Participant identities never depend on a device or camera index.</summary>
    public sealed class NativeMatchComposition
    {
        // Structural GAME_SPEC limits, independent of physical device adapters.
        public const int MaximumLocalSeats=4;
        readonly NativeParticipantInfo[] participants;
        readonly int[] localParticipants, participantSeats;
        public NativeMatchRoster Roster { get; }
        public int ParticipantCount=>participants.Length;
        public int LocalCount=>localParticipants.Length;
        public NativeMatchComposition(NativeMatchRoster roster,NativeParticipantInfo[] metadata,int[] mapping)
        {
            if(roster==null || metadata==null || mapping==null) throw new ArgumentNullException();
            if(metadata.Length!=roster.Count || mapping.Length<1 || mapping.Length>MaximumLocalSeats)
                throw new ArgumentException("Invalid participant/local-seat counts");
            var snapshot=roster.Read();Roster=new NativeMatchRoster(snapshot.Mode,snapshot.Teams);
            participants=(NativeParticipantInfo[])metadata.Clone();localParticipants=(int[])mapping.Clone();
            participantSeats=Enumerable.Repeat(-1,participants.Length).ToArray();
            // Frozen RGB values are independent of the current palette and remain valid after profile upgrades.
            for(int i=0;i<participants.Length;i++)
            {
                var p=participants[i];
                if(!Enum.IsDefined(typeof(NativeParticipantKind),p.Kind) || string.IsNullOrWhiteSpace(p.Name) ||
                    !FiniteUnit(p.Color.r)||!FiniteUnit(p.Color.g)||!FiniteUnit(p.Color.b)||p.Color.a!=1)
                    throw new ArgumentException("Invalid participant metadata");
                if(p.Kind==NativeParticipantKind.Bot ? p.Difficulty<0 || p.Difficulty>2 : p.Difficulty!=-1)
                    throw new ArgumentException("Invalid difficulty for participant kind");
            }
            for(int a=0;a<participants.Length;a++)for(int b=0;b<a;b++)
            {
                bool equal=participants[a].Color==participants[b].Color;
                if(Roster.Mode==NativeMatchMode.Ffa ? equal : Roster.AreAllies(a,b)!=equal)
                    throw new ArgumentException("Resolved colors must distinguish opponents and agree within teams");
            }
            for(int seat=0;seat<localParticipants.Length;seat++)
            {
                int p=localParticipants[seat];
                if(p<0 || p>=participants.Length || participantSeats[p]!=-1 ||
                    (participants[p].Kind!=NativeParticipantKind.LocalHuman && participants[p].Kind!=NativeParticipantKind.Bot))
                    throw new ArgumentException("Invalid local participant mapping");
                participantSeats[p]=seat;
            }
            for(int p=0;p<participants.Length;p++)
                if(participants[p].Kind==NativeParticipantKind.LocalHuman && participantSeats[p]<0)
                    throw new ArgumentException("Every local human requires one seat");
        }
        static bool FiniteUnit(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&v>=0&&v<=1;
        public NativeParticipantInfo Participant(int id)=>participants[id];
        public int ParticipantAt(int seat)=>localParticipants[seat];
        public int SeatOf(int participant)=>participantSeats[participant];
        public NativeCompositionSnapshot Read()=>new NativeCompositionSnapshot
        {Roster=Roster.Read(),Participants=(NativeParticipantInfo[])participants.Clone(),LocalParticipants=(int[])localParticipants.Clone()};
        public static NativeMatchComposition Restore(NativeCompositionSnapshot snapshot)
        {
            if(snapshot?.Roster==null)throw new ArgumentException("Missing composition");
            return new NativeMatchComposition(new NativeMatchRoster(snapshot.Roster.Mode,snapshot.Roster.Teams),snapshot.Participants,snapshot.LocalParticipants);
        }
        public void AssembleLocalActions(LocalAction[] local,LocalAction[] target)
        {
            if(local==null || target==null || local.Length!=LocalCount || target.Length!=ParticipantCount || ReferenceEquals(local,target))
                throw new ArgumentException("Distinct local and participant action buffers required");
            Array.Clear(target,0,target.Length);
            for(int s=0;s<LocalCount;s++)
                if(Participant(ParticipantAt(s)).Kind==NativeParticipantKind.LocalHuman)
                    target[ParticipantAt(s)]=local[s];
        }
        public void RequireSupportedSources(bool botsAvailable,bool diagnosticsAllowed)
        {
            if(participants.Any(p=>p.Kind==NativeParticipantKind.Bot)&&!botsAvailable)
                throw new InvalidOperationException("Playable bot action source is unavailable");
            if(participants.Any(p=>p.Kind==NativeParticipantKind.DiagnosticFixture)&&!diagnosticsAllowed)
                throw new InvalidOperationException("Fixtures require an explicit development scenario");
        }
    }
}
