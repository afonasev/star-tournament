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
        static readonly string[] Names={"Vega","Orion","Nova","Atlas","Lyra","Sirius","Rigel"};
        public int Count=>bots.Count;
        public Bot At(int index)=>bots[index];
        public bool CanAdd(int humans)=>humans>=1 && humans<=NativeMatchComposition.MaximumLocalSeats && humans+Count<NativeMatchRoster.MaximumParticipants;
        public void Add(int humans)
        {
            if(!CanAdd(humans))throw new ArgumentException("Participant limit reached");
            string name=Names.First(n=>bots.All(b=>b.Name!=n));
            bots.Add(new Bot{Name=name,Difficulty=(int)NativeBotDifficulty.Normal,Team=(humans+Count)%2==0?NativeTeam.TeamA:NativeTeam.TeamB});
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
        public NativeMatchComposition Build(int humans,NativeMatchMode mode,NativeTeam[] humanTeams,bool swapped)
        {
            if(humans<1||humans>NativeMatchComposition.MaximumLocalSeats||humanTeams==null||humanTeams.Length<humans)
                throw new ArgumentException("Invalid local seats");
            int total=humans+Count;
            var roster=mode==NativeMatchMode.Ffa?NativeMatchRoster.Ffa(total):new NativeMatchRoster(mode,humanTeams.Take(humans).Concat(bots.Select(b=>b.Team)).ToArray());
            var snapshot=roster.Read();
            var metadata=Enumerable.Range(0,total).Select(p=>new NativeParticipantInfo(p<humans?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,
                p<humans?"Игрок "+(p+1):bots[p-humans].Name,NativeStandingsView.Identity(snapshot,p,swapped),p<humans?-1:bots[p-humans].Difficulty)).ToArray();
            return new NativeMatchComposition(roster,metadata,Enumerable.Range(0,humans).ToArray());
        }
    }
}
