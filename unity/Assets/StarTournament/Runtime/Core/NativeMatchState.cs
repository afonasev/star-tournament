using System;
using System.Linq;

namespace StarTournament.ProvingGround
{
    public enum NativeMatchPhase { Running, Overtime, Finished }
    [Serializable]
    public struct NativeStanding
    {
        public int Seat, Kills, Assists, Deaths, Score;
        public double DamageDealt, DamageReceived;
    }
    [Serializable]
    public sealed class NativeMatchSnapshot
    {
        public long Tick, RemainingTicks;
        public NativeMatchPhase Phase;
        public string Trigger;
        public int Winner;
        public NativeStanding[] Standings;
    }
    /// <summary>Data-only tick reducer. Damage is already capped and life-validated by CombatLife.</summary>
    public sealed class NativeMatchState
    {
        readonly NativeStanding[] rows;
        readonly long[,] ledger;
        readonly int[] chains, chainAwards, totals;
        readonly long[] lastKill;
        readonly bool[] diedThisTick;
        readonly long durationTicks, assistTicks, gapTicks;
        readonly int assistPoints, increment, target;
        readonly double tickHz;
        long tick;
        string trigger;
        bool enteredOvertime;
        int winner = -1;
        NativeStanding[] finalRows;
        public NativeMatchPhase Phase { get; private set; }
        public NativeMatchConfiguration Configuration { get; }
        long RemainingTicks => enteredOvertime ? 0 : Math.Max(0,durationTicks-tick);
        public double RemainingSeconds => RemainingTicks/tickHz;
        public NativeMatchState(int seats, NativeMatchConfiguration config, ProvingProfile profile, float hz)
        {
            if (seats < 2 || hz <= 0 || float.IsNaN(hz) || float.IsInfinity(hz) || profile.Id != "unity-native-match-v1" || profile.Version != 1 || profile.Validate().Count != 0)
                throw new ArgumentException("Invalid match profile");
            foreach (var d in profile.Descriptors) NativeMatchConfiguration.ValidateValue(profile, d.Path, profile.Get(d.Path));
            config.Validate(profile); Configuration=config; tickHz=hz;
            durationTicks=(long)Math.Round(config.DurationMinutes*60d*hz);
            assistTicks=(long)Math.Ceiling(profile.Get("score.assistWindow")*hz);
            gapTicks=(long)Math.Ceiling(profile.Get("score.chainGap")*hz);
            assistPoints=(int)profile.Get("score.assistPoints"); increment=(int)profile.Get("score.chainIncrement");
            totals=Enumerable.Range(1,5).Select(i=>(int)profile.Get("score.chainTotal"+i)).ToArray();
            for(int i=1;i<totals.Length;i++) if(totals[i]<totals[i-1]) throw new ArgumentException("Chain totals must be monotonic");
            target=config.TargetEnabled ? config.TargetPoints : 0;
            rows=new NativeStanding[seats]; ledger=new long[seats,seats]; chains=new int[seats]; chainAwards=new int[seats];
            lastKill=new long[seats]; diedThisTick=new bool[seats];
            for(int i=0;i<seats;i++) { rows[i].Seat=i; lastKill[i]=-1; ClearLedger(i); }
        }
        void ClearLedger(int victim) { for(int i=0;i<rows.Length;i++) ledger[victim,i]=-1; }
        public void BeginTick() { if(Phase!=NativeMatchPhase.Finished) tick++; }
        public void RecordDamage(int victim, int attacker, DamageResult result)
        {
            if(Phase==NativeMatchPhase.Finished || result.Applied<=0) return;
            rows[victim].DamageReceived+=result.Applied;
            if(attacker>=0 && attacker!=victim)
            {
                rows[attacker].DamageDealt+=result.Applied;
                ledger[victim,attacker]=tick;
            }
            if(!result.Killed) return;
            rows[victim].Deaths++; diedThisTick[victim]=true;
            for(int i=0;i<rows.Length;i++) if(i!=attacker && i!=victim && ledger[victim,i]>=0 && tick-ledger[victim,i]<=assistTicks)
            { rows[i].Assists++; rows[i].Score+=assistPoints; }
            if(attacker>=0 && attacker!=victim)
            {
                bool continues=lastKill[attacker]>=0 && tick-lastKill[attacker]<=gapTicks;
                int length=continues ? chains[attacker]+1 : 1;
                int award=length<=totals.Length ? totals[length-1] : totals[totals.Length-1]+(length-totals.Length)*increment;
                rows[attacker].Kills++; rows[attacker].Score+=award-(continues?chainAwards[attacker]:0);
                chains[attacker]=length; chainAwards[attacker]=award; lastKill[attacker]=tick;
            }
            ClearLedger(victim);
        }
        public void EndTick()
        {
            if(Phase==NativeMatchPhase.Finished) return;
            // Reset after every death award, including mutual kills: no dead participant keeps a chain.
            for(int i=0;i<rows.Length;i++) if(diedThisTick[i])
            { chains[i]=chainAwards[i]=0; lastKill[i]=-1; diedThisTick[i]=false; }
            int maximum=rows.Max(r=>r.Score);
            if(trigger==null) trigger=target>0 && maximum>=target ? "score-limit" : tick>=durationTicks ? "time-limit" : null;
            if(trigger==null) return;
            var leaders=rows.Where(r=>r.Score==maximum).ToArray();
            if(leaders.Length!=1) { enteredOvertime=true; Phase=NativeMatchPhase.Overtime; return; }
            Phase=NativeMatchPhase.Finished; winner=leaders[0].Seat; finalRows=Ordered();
        }
        NativeStanding[] Ordered() => rows.OrderByDescending(r=>r.Score).ThenBy(r=>r.Seat).ToArray();
        public NativeMatchSnapshot Read() => new NativeMatchSnapshot { Tick=tick, RemainingTicks=RemainingTicks,
            Phase=Phase, Trigger=trigger, Winner=winner, Standings=finalRows==null ? Ordered() : (NativeStanding[])finalRows.Clone() };
    }
}
