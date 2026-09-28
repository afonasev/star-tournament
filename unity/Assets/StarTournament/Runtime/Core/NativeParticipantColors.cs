using UnityEngine;
using System.Linq;
using System;

namespace StarTournament.ProvingGround
{
    public static class NativeParticipantColors
    {
        // Approved vetted identity palette from src/match/participantPalette.ts; presentation constants, not balance.
        static readonly Color[] colors={
            new Color32(59,130,246,255),new Color32(217,70,239,255),new Color32(251,113,133,255),new Color32(139,92,246,255),
            new Color32(16,185,129,255),new Color32(245,158,11,255),new Color32(225,29,72,255),new Color32(14,165,233,255),
            new Color32(99,102,241,255),new Color32(20,184,166,255),new Color32(34,197,94,255),new Color32(168,85,247,255)};
        // Separate presentation RNG: setup preview and gameplay UnityEngine.Random never consume it.
        static readonly System.Random assignmentRandom=new System.Random();
        public static NativeMatchComposition FreezeNewMatch(NativeMatchComposition draft,Func<int,int> next=null)
        {
            if(draft==null)throw new ArgumentNullException(nameof(draft));
            if(draft.Roster.Mode!=NativeMatchMode.Ffa)return draft;
            if(next==null)
            {
                lock(assignmentRandom)return FreezeNewMatch(draft,assignmentRandom.Next);
            }
            var palette=Read();var snapshot=draft.Read();
            // Partial Fisher-Yates samples without replacement from the full approved palette.
            for(int p=0;p<snapshot.Participants.Length;p++)
            {
                int offset=next(palette.Length-p);
                if(offset<0||offset>=palette.Length-p)throw new ArgumentOutOfRangeException(nameof(next));
                int selected=p+offset;var color=palette[selected];palette[selected]=palette[p];palette[p]=color;
                snapshot.Participants[p].Color=color;
            }
            return NativeMatchComposition.Restore(snapshot);
        }
        public static bool Contains(Color color)=>colors.Contains(color);
        public static Color[] Read()=>(Color[])colors.Clone();
        public static Color For(int participant)=>colors[participant];
    }
}
