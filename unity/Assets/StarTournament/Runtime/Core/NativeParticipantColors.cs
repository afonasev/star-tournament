using UnityEngine;
using System.Linq;
using System;

namespace StarTournament.ProvingGround
{
    public static class NativeParticipantColors
    {
        static readonly string[] keys={"blue","magenta","yellow","orange","cyan","green","red","white"};
        static readonly Color[] colors=Read(ProvingProfile.CreateParticipantPaletteDefault());
        // Separate presentation RNG: setup preview and gameplay UnityEngine.Random never consume it.
        static readonly System.Random assignmentRandom=new System.Random();
        public static NativeMatchComposition FreezeNewMatch(NativeMatchComposition draft,Func<int,int> next=null,ProvingProfile profile=null)
        {
            if(draft==null)throw new ArgumentNullException(nameof(draft));
            var palette=Read(profile);
            if(draft.Roster.Mode!=NativeMatchMode.Ffa)
            {
                if(profile==null)return draft;
                var teams=draft.Read();
                // Setup already resolves the selected blue/magenta team swap.
                for(int p=0;p<teams.Participants.Length;p++)teams.Participants[p].Color=palette[draft.Participant(p).Color==For(0)?0:1];
                return NativeMatchComposition.Restore(teams);
            }
            if(next==null)
            {
                lock(assignmentRandom)return FreezeNewMatch(draft,assignmentRandom.Next,profile);
            }
            var snapshot=draft.Read();
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
        public static Color[] Read(ProvingProfile profile=null)
        {
            if(profile==null)return (Color[])colors.Clone();
            // v2 adds presentation-only panel metadata; the palette/color contract is unchanged.
            if(profile.Id!=ProvingProfile.ParticipantPaletteId||(profile.Version!=1&&profile.Version!=2&&profile.Version!=3)||profile.Validate().Count!=0)
                throw new ArgumentException("Invalid participant palette profile");
            foreach(var d in profile.Descriptors)NativeMatchConfiguration.ValidateValue(profile,d.Path,profile.Get(d.Path));
            var palette=keys.Select(key=>new Color32((byte)profile.Get("participant.color."+key+".r"),
                (byte)profile.Get("participant.color."+key+".g"),(byte)profile.Get("participant.color."+key+".b"),255)).Select(c=>(Color)c).ToArray();
            if(palette.Distinct().Count()!=palette.Length)throw new ArgumentException("Participant colors must be unique");
            return palette;
        }
        public static Color For(int participant)=>colors[participant];
    }
}
