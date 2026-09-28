using System;
using System.Collections.Generic;
using System.Linq;

namespace StarTournament.ProvingGround
{
    /// <summary>Editable setup data; Build returns a detached, validated session composition.</summary>
    public sealed class NativeBotSetup
    {
        public struct Bot { public string Name; public int Difficulty; public NativeTeam Team; }
        readonly List<Bot> bots=new List<Bot>();
        // View ownership is authored separately from the additional bot roster.
        // A view may be bot-driven, but it never becomes a diagnostic fixture.
        readonly bool[] aiSeats=new bool[NativeMatchComposition.MaximumLocalSeats];
        readonly int[] seatDifficulties=Enumerable.Repeat((int)NativeBotDifficulty.Normal,NativeMatchComposition.MaximumLocalSeats).ToArray();
        static readonly string[] Names={"Vega","Orion","Nova","Atlas","Lyra","Sirius","Rigel"};
        public int Count=>bots.Count;
        public Bot At(int index)=>bots[index];
        public bool IsAi(int seat)
        {
            RequireSeat(seat);return aiSeats[seat];
        }
        public void SetAi(int seat,bool value)
        {
            RequireSeat(seat);aiSeats[seat]=value;
        }
        public void RemoveSeat(int seat,int views)
        {
            RequireSeat(seat);
            if(views<=1 || views>NativeMatchComposition.MaximumLocalSeats || seat>=views)throw new ArgumentOutOfRangeException(nameof(views));
            for(int i=seat;i<views-1;i++){aiSeats[i]=aiSeats[i+1];seatDifficulties[i]=seatDifficulties[i+1];}
            aiSeats[views-1]=false;seatDifficulties[views-1]=(int)NativeBotDifficulty.Normal;
        }
        public int SeatDifficulty(int seat)
        {
            RequireSeat(seat);return seatDifficulties[seat];
        }
        public void SetSeatDifficulty(int seat,int difficulty)
        {
            RequireSeat(seat);
            if(difficulty<0||difficulty>2)throw new ArgumentException("Invalid difficulty");
            seatDifficulties[seat]=difficulty;
        }
        public int HumanCount(int views)
        {
            if(views<1||views>NativeMatchComposition.MaximumLocalSeats)throw new ArgumentOutOfRangeException(nameof(views));
            return Enumerable.Range(0,views).Count(seat=>!aiSeats[seat]);
        }
        static void RequireSeat(int seat)
        {
            if(seat<0||seat>=NativeMatchComposition.MaximumLocalSeats)throw new ArgumentOutOfRangeException(nameof(seat));
        }
        public bool CanAdd(int views)=>views>=1 && views<=NativeMatchComposition.MaximumLocalSeats && views+Count<NativeMatchRoster.MaximumParticipants;
        public void Add(int views)
        {
            if(!CanAdd(views))throw new ArgumentException("Participant limit reached");
            string name=Names.First(n=>bots.All(b=>b.Name!=n));
            bots.Add(new Bot{Name=name,Difficulty=(int)NativeBotDifficulty.Normal,Team=(views+Count)%2==0?NativeTeam.TeamA:NativeTeam.TeamB});
        }
        public void Remove(int index)=>bots.RemoveAt(index);
        public void SetDifficulty(int index,int difficulty)
        {
            if(difficulty<0||difficulty>2)throw new ArgumentException("Invalid difficulty");
            var bot=bots[index];bot.Difficulty=difficulty;bots[index]=bot;
        }
        public void SetTeam(int index,NativeTeam team)
        {
            if(team!=NativeTeam.TeamA&&team!=NativeTeam.TeamB)throw new ArgumentException("Invalid team");
            var bot=bots[index];bot.Team=team;bots[index]=bot;
        }
        public NativeMatchComposition Build(int views,NativeMatchMode mode,NativeTeam[] viewTeams,bool swapped)
        {
            if(views<1||views>NativeMatchComposition.MaximumLocalSeats||viewTeams==null||viewTeams.Length<views)
                throw new ArgumentException("Invalid local seats");
            int total=views+Count;
            var roster=mode==NativeMatchMode.Ffa?NativeMatchRoster.Ffa(total):new NativeMatchRoster(mode,viewTeams.Take(views).Concat(bots.Select(b=>b.Team)).ToArray());
            var snapshot=roster.Read();
            var metadata=Enumerable.Range(0,total).Select(p=>
            {
                if(p<views)
                {
                    bool ai=aiSeats[p];
                    return new NativeParticipantInfo(ai?NativeParticipantKind.Bot:NativeParticipantKind.LocalHuman,
                        ai?"Бот "+(p+1):"Игрок "+(p+1),NativeStandingsView.Identity(snapshot,p,swapped),ai?seatDifficulties[p]:-1);
                }
                var bot=bots[p-views];
                return new NativeParticipantInfo(NativeParticipantKind.Bot,bot.Name,NativeStandingsView.Identity(snapshot,p,swapped),bot.Difficulty);
            }).ToArray();
            return new NativeMatchComposition(roster,metadata,Enumerable.Range(0,views).ToArray());
        }
    }
}
