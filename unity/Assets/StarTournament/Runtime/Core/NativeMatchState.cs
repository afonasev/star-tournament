using System;
using System.Linq;
using System.Globalization;

namespace StarTournament.ProvingGround
{
    public enum NativeMatchPhase { Running, Overtime, Finished }
    public readonly struct KillScoreEvent
    {
        public readonly int Source, Points, Chain;
        public readonly bool Enemy, SeriesEligible;
        public KillScoreEvent(int source,int points,int chain=0,bool enemy=false,bool seriesEligible=false)
        {Source=source;Points=points;Chain=chain;Enemy=enemy;SeriesEligible=seriesEligible;}
    }
    [Serializable]
    public struct WeaponAccuracy
    {
        public double Used, Successful;
        public double Fraction => Used > 0 ? Successful / Used : 0;
    }
    [Serializable]
    public struct NativeStanding
    {
        // Legacy field name: stable participant slot, not a device/viewport binding.
        public int Seat, Kills, Assists, Deaths, Score;
        public int SelfKills, AllyKills, AccumulatedPenalty;
        public double DamageDealt, DamageReceived, AllyDamageDealt;
        public double DistanceTravelled, SelfDamageDealt, EnemyDamageReceived;
        public int Jumps, Shots, WeaponSwitches, HealPickups, ArmorPickups, BonusPickups;
        public long FirstDeathTick;
        public WeaponAccuracy RifleAccuracy, ShotgunAccuracy, RocketAccuracy, CutterAccuracy;
        public double AccuracyPercent
        {
            get
            {
                double total=0;int count=0;
                AddAccuracy(RifleAccuracy,ref total,ref count);AddAccuracy(ShotgunAccuracy,ref total,ref count);
                AddAccuracy(RocketAccuracy,ref total,ref count);AddAccuracy(CutterAccuracy,ref total,ref count);
                return count==0?0:total/count*100;
            }
        }
        static void AddAccuracy(WeaponAccuracy accuracy,ref double total,ref int count)
        { if(accuracy.Used>0){total+=accuracy.Fraction;count++;} }
    }
    [Serializable]
    public struct NativeTeamStanding
    {
        public NativeTeam Team;
        public int Score;
    }
    [Serializable]
    public sealed class NativeMatchSnapshot
    {
        // Additive counters deserialize to zero in historical projections; they cannot be reconstructed.
        public int Version = 2;
        public string TuningIdentity;
        public long[] AssistLedger;
        public int[] ChainAwards;
        public long[] LastEligibleKillTicks;
        public bool[] DiedThisTick;
        public int AchievementTelemetryVersion;
        public int AwardSeed, MinimumShots;
        public double MinimumBeamSeconds;
        public bool[] AwardRecipients;
        public bool AwardsFrozen;
        public NativeAchievement[] Achievements;
        public long Tick, RemainingTicks;
        public NativeMatchPhase Phase;
        public string Trigger;
        public int Winner;
        public NativeTeam WinnerTeam;
        public NativeRosterSnapshot Roster;
        public NativeStanding[] Standings;
        public NativeTeamStanding[] Teams;
        public int[] DirectKillsByPair, KillChains;
        public int ParticipantCount => KillChains?.Length ?? 0;
        public int DirectKills(int killer,int victim) => DirectKillsByPair[killer*ParticipantCount+victim];
        public int KillChain(int participant) => KillChains[participant];
    }
    /// <summary>Data-only tick reducer. Damage is already capped and life-validated by CombatLife.</summary>
    public sealed class NativeMatchState
    {
        readonly NativeStanding[] rows;
        readonly long[,] ledger;
        readonly int[] chains, chainAwards, totals, directKillsByPair;
        readonly bool[] diedThisTick;
        readonly long durationTicks, assistTicks, chainWindowTicks;
        readonly bool timedChains;
        readonly int chainFirstAward, chainAwardStep;
        readonly long[] lastEligibleKillTicks;
        readonly int assistPoints, increment, target, penalty;
        readonly double tickHz;
        readonly string tuningIdentity;
        readonly string legacyTuningIdentity;
        long tick;
        string trigger;
        bool enteredOvertime;
        int winner = -1;
        NativeTeam winnerTeam;
        NativeStanding[] finalRows;
        readonly bool[] awardRecipients;
        NativeAchievement[] achievements=Array.Empty<NativeAchievement>();
        int awardSeed;
        readonly int minimumShots;
        readonly double minimumBeamSeconds;
        bool awardsFrozen, legacyAchievementsDisabled;
        int telemetryVersion=1;
        public NativeMatchPhase Phase { get; private set; }
        public NativeMatchConfiguration Configuration { get; }
        public NativeMatchRoster Roster { get; }
        long RemainingTicks => enteredOvertime ? 0 : Math.Max(0,durationTicks-tick);
        public double RemainingSeconds => RemainingTicks/tickHz;
        public NativeMatchState(int seats, NativeMatchConfiguration config, ProvingProfile profile, float hz)
            : this(NativeMatchRoster.Ffa(seats), config, profile, hz) { }

        public NativeMatchState(NativeMatchRoster roster, NativeMatchConfiguration config, ProvingProfile profile, float hz)
        {
            Roster = roster ?? throw new ArgumentNullException(nameof(roster));
            int seats = roster.Count;
            if (hz <= 0 || float.IsNaN(hz) || float.IsInfinity(hz) || profile == null || profile.Id != "unity-native-match-v1" || (profile.Version != 1 && profile.Version != 2 && profile.Version != 3) || profile.Validate().Count != 0)
                throw new ArgumentException("Invalid match profile");
            foreach (var d in profile.Descriptors) NativeMatchConfiguration.ValidateValue(profile, d.Path, profile.Get(d.Path));
            config.Validate(profile); Configuration=config; tickHz=hz;
            minimumShots=(int)(profile.Descriptor("achievement.minimumShots")!=null?profile.Get("achievement.minimumShots"):ProvingProfile.CreateMatchDefault().Get("achievement.minimumShots"));
            minimumBeamSeconds=profile.Descriptor("achievement.minimumBeamSeconds")!=null?profile.Get("achievement.minimumBeamSeconds"):ProvingProfile.CreateMatchDefault().Get("achievement.minimumBeamSeconds");
            durationTicks=(long)Math.Round(config.DurationMinutes*60d*hz);
            assistTicks=(long)Math.Ceiling(profile.Get("score.assistWindow")*hz);
            assistPoints=(int)profile.Get("score.assistPoints"); increment=(int)profile.Get("score.chainIncrement");
            totals=Enumerable.Range(1,5).Select(i=>(int)profile.Get("score.chainTotal"+i)).ToArray();
            for(int i=1;profile.Version<3&&i<totals.Length;i++) if(totals[i]<totals[i-1]) throw new ArgumentException("Chain totals must be monotonic");
            penalty=profile.Descriptor("score.friendlyOrSelfKillPenalty")==null
                ? (int)ProvingProfile.CreateMatchDefault().Get("score.friendlyOrSelfKillPenalty")
                : (int)profile.Get("score.friendlyOrSelfKillPenalty");
            timedChains=profile.Version>=3;
            chainWindowTicks=timedChains?(long)Math.Floor((double)profile.Get("score.chainWindowSeconds")*hz+1e-6):0;
            chainFirstAward=totals[0];
            chainAwardStep=timedChains?(int)profile.Get("score.chainAwardStep"):0;
            lastEligibleKillTicks=Enumerable.Repeat(-1L,seats).ToArray();
            target=config.TargetEnabled ? config.TargetPoints : 0;
            awardSeed=Guid.NewGuid().GetHashCode();
            legacyTuningIdentity=string.Join("|",new[]{hz.ToString("R",CultureInfo.InvariantCulture),durationTicks.ToString(),assistTicks.ToString(),assistPoints.ToString(),increment.ToString(),target.ToString(),penalty.ToString(),string.Join(",",totals)});
            tuningIdentity=legacyTuningIdentity+"|"+minimumShots.ToString()+"|"+minimumBeamSeconds.ToString("R",CultureInfo.InvariantCulture);
            if(timedChains)tuningIdentity+="|timed-linear-v3|"+chainWindowTicks+"|"+chainFirstAward+"|"+chainAwardStep;
            rows=new NativeStanding[seats]; ledger=new long[seats,seats]; chains=new int[seats]; chainAwards=new int[seats];
            directKillsByPair=new int[seats*seats]; diedThisTick=new bool[seats];
            awardRecipients=new bool[seats];
            for(int i=0;i<seats;i++) { rows[i].Seat=i; ClearLedger(i); }
        }
        void ClearLedger(int victim) { for(int i=0;i<rows.Length;i++) ledger[victim,i]=-1; }
        public void BeginTick()
        {
            if(Phase==NativeMatchPhase.Finished)return;
            tick++;
            if(timedChains)for(int i=0;i<chains.Length;i++)
                if(chains[i]>0&&tick-lastEligibleKillTicks[i]>chainWindowTicks)ResetChain(i);
        }
        void ResetChain(int participant){chains[participant]=chainAwards[participant]=0;lastEligibleKillTicks[participant]=-1;}
        public void ConfigureAchievementRecipients(bool[] recipients)
        {
            if(tick!=0||Phase!=NativeMatchPhase.Running||legacyAchievementsDisabled||recipients==null||recipients.Length!=rows.Length)throw new ArgumentException("Invalid achievement recipients",nameof(recipients));
            Array.Copy(recipients,awardRecipients,rows.Length);
        }
        public void RecordMovement(int participant,double horizontalDistance,bool acceptedJump)
        {
            CheckParticipant(participant);if(Phase==NativeMatchPhase.Finished)return;
            if(!FiniteDamage(horizontalDistance))throw new ArgumentOutOfRangeException(nameof(horizontalDistance));
            rows[participant].DistanceTravelled+=horizontalDistance;if(acceptedJump)rows[participant].Jumps++;
        }
        public void RecordShot(int participant){CheckParticipant(participant);if(Phase!=NativeMatchPhase.Finished)rows[participant].Shots++;}
        public void RecordWeaponSwitch(int participant){CheckParticipant(participant);if(Phase!=NativeMatchPhase.Finished)rows[participant].WeaponSwitches++;}
        public void RecordPickup(int participant,NativeBotPickupKind kind)
        {
            CheckParticipant(participant);if(Phase==NativeMatchPhase.Finished)return;
            if(kind==NativeBotPickupKind.Heal){rows[participant].HealPickups++;rows[participant].BonusPickups++;}
            else if(kind==NativeBotPickupKind.Armor){rows[participant].ArmorPickups++;rows[participant].BonusPickups++;}
            else if(kind==NativeBotPickupKind.Speed||kind==NativeBotPickupKind.Damage)rows[participant].BonusPickups++;
        }
        void CheckParticipant(int participant){if(participant<0||participant>=rows.Length)throw new ArgumentOutOfRangeException(nameof(participant));}
        public KillScoreEvent RecordDamage(int victim, int attacker, DamageResult result, bool chainEligible=true, int? originalSource=null)
        {
            if(Phase==NativeMatchPhase.Finished || result.Applied<=0) return default;
            int source=originalSource??attacker;
            if(victim<0 || victim>=rows.Length || source < -1 || source>=rows.Length || attacker < -1 || attacker>=rows.Length) throw new ArgumentOutOfRangeException();
            bool enemy=attacker>=0 && attacker!=victim && !Roster.AreAllies(attacker,victim);
            bool enemySource=source>=0&&source!=victim&&!Roster.AreAllies(source,victim);
            rows[victim].DamageReceived+=result.Applied;
            if(source==victim)rows[victim].SelfDamageDealt+=result.Applied;
            if(source>=0 && source!=victim && Roster.AreAllies(source,victim)) rows[source].AllyDamageDealt+=result.Applied;
            if(enemySource)rows[victim].EnemyDamageReceived+=result.Applied;
            if(enemy)
            {
                rows[attacker].DamageDealt+=result.Applied;
                ledger[victim,attacker]=tick;
            }
            if(!result.Killed) return default;
            KillScoreEvent scoreEvent=new KillScoreEvent(source,0);
            rows[victim].Deaths++; if(rows[victim].FirstDeathTick==0)rows[victim].FirstDeathTick=tick; diedThisTick[victim]=true;
            if(source>=0 && (source==victim || Roster.AreAllies(source,victim)))
            {
                if(source==victim) rows[source].SelfKills++; else rows[source].AllyKills++;
                rows[source].AccumulatedPenalty+=penalty; rows[source].Score-=penalty;
                scoreEvent=new KillScoreEvent(source,-penalty);
                if(source!=victim && chainEligible) ResetChain(source);
            }
            // Environmental deaths retain the existing assist rule; self and allied kills award none.
            if(source<0 || (source!=victim && !Roster.AreAllies(source,victim)))
            {
                for(int i=0;i<rows.Length;i++) if(i!=attacker && i!=victim && !Roster.AreAllies(i,victim) && ledger[victim,i]>=0 && tick-ledger[victim,i]<=assistTicks)
                { rows[i].Assists++; rows[i].Score+=assistPoints; }
            }
            if(enemy)
            {
                int length=chainEligible?chains[attacker]+1:1;
                int award=timedChains?checked(chainFirstAward+(length-1)*chainAwardStep):
                    length<=totals.Length ? totals[length-1] : totals[totals.Length-1]+(length-totals.Length)*increment;
                int points=timedChains||!chainEligible?award:award-chainAwards[attacker];
                rows[attacker].Kills++; rows[attacker].Score+=points;
                scoreEvent=new KillScoreEvent(attacker,points,length,true,chainEligible);
                directKillsByPair[attacker*rows.Length+victim]++;
                if(chainEligible){chains[attacker]=length; chainAwards[attacker]=award;lastEligibleKillTicks[attacker]=tick;}
            }
            ClearLedger(victim);
            return scoreEvent;
        }
        public void RecordAccuracy(int participant,WeaponId weapon,double used,double successful)
        {
            if(participant<0||participant>=rows.Length)throw new ArgumentOutOfRangeException(nameof(participant));
            if(weapon!=WeaponId.Rifle&&weapon!=WeaponId.Shotgun&&weapon!=WeaponId.RocketLauncher&&weapon!=WeaponId.Cutter)throw new ArgumentOutOfRangeException(nameof(weapon));
            if(!FiniteAccuracy(used)||!FiniteAccuracy(successful))throw new ArgumentException("Invalid accuracy counters");
            if(Phase==NativeMatchPhase.Finished||used==0&&successful==0)return;
            ref var row=ref rows[participant];
            var counter=weapon==WeaponId.Rifle?row.RifleAccuracy:weapon==WeaponId.Shotgun?row.ShotgunAccuracy:weapon==WeaponId.RocketLauncher?row.RocketAccuracy:row.CutterAccuracy;
            counter.Used+=used;counter.Successful+=successful;
            if(!FiniteAccuracy(counter.Used)||!FiniteAccuracy(counter.Successful)||counter.Successful>counter.Used)throw new ArgumentException("Invalid accuracy counters");
            if(weapon==WeaponId.Rifle)row.RifleAccuracy=counter;else if(weapon==WeaponId.Shotgun)row.ShotgunAccuracy=counter;else if(weapon==WeaponId.RocketLauncher)row.RocketAccuracy=counter;else row.CutterAccuracy=counter;
        }
        public void EndTick()
        {
            if(Phase==NativeMatchPhase.Finished) return;
            // Reset after every death award, including mutual kills: no dead participant keeps a chain.
            for(int i=0;i<rows.Length;i++) if(diedThisTick[i])
            { ResetChain(i); diedThisTick[i]=false; }
            var teamRows = TeamTotals();
            int maximum = Roster.Mode == NativeMatchMode.Teams ? teamRows.Max(r=>r.Score) : rows.Max(r=>r.Score);
            if(trigger==null) trigger=target>0 && maximum>=target ? "score-limit" : tick>=durationTicks ? "time-limit" : null;
            if(trigger==null) return;
            int leaderCount = Roster.Mode == NativeMatchMode.Teams
                ? teamRows.Count(r=>r.Score==maximum) : rows.Count(r=>r.Score==maximum);
            if(leaderCount!=1) { enteredOvertime=true; Phase=NativeMatchPhase.Overtime; return; }
            Phase=NativeMatchPhase.Finished;
            if (Roster.Mode == NativeMatchMode.Teams) winnerTeam=teamRows.Single(r=>r.Score==maximum).Team;
            else winner=rows.Single(r=>r.Score==maximum).Seat;
            finalRows=Ordered();
            if(!legacyAchievementsDisabled&&!awardsFrozen){achievements=NativeAchievementCatalog.Select(finalRows,(bool[])awardRecipients.Clone(),awardSeed,minimumShots,minimumBeamSeconds);awardsFrozen=true;}
        }
        NativeTeamStanding[] TeamTotals()
        {
            if (Roster.Mode != NativeMatchMode.Teams) return Array.Empty<NativeTeamStanding>();
            int a=0,b=0;
            foreach (var row in rows)
                if (Roster.TeamOf(row.Seat)==NativeTeam.TeamA) a+=row.Score; else b+=row.Score;
            var teamA=new NativeTeamStanding { Team=NativeTeam.TeamA, Score=a };
            var teamB=new NativeTeamStanding { Team=NativeTeam.TeamB, Score=b };
            return a>=b ? new[]{teamA,teamB} : new[]{teamB,teamA};
        }
        NativeStanding[] Ordered() => rows.OrderByDescending(r=>r.Score).ThenBy(r=>r.Seat).ToArray();
        public NativeMatchSnapshot Read() => new NativeMatchSnapshot { Version=timedChains?3:2, LastEligibleKillTicks=timedChains?(long[])lastEligibleKillTicks.Clone():null, TuningIdentity=tuningIdentity, Tick=tick, RemainingTicks=RemainingTicks,
            Phase=Phase, Trigger=trigger, Winner=winner, WinnerTeam=winnerTeam, Roster=Roster.Read(), Teams=TeamTotals(),
            Standings=finalRows==null ? Ordered() : (NativeStanding[])finalRows.Clone(),
            DirectKillsByPair=(int[])directKillsByPair.Clone(), KillChains=(int[])chains.Clone(),
            AssistLedger=Enumerable.Range(0,rows.Length*rows.Length).Select(i=>ledger[i/rows.Length,i%rows.Length]).ToArray(),
            ChainAwards=(int[])chainAwards.Clone(), DiedThisTick=(bool[])diedThisTick.Clone(),
            AchievementTelemetryVersion=telemetryVersion,AwardSeed=awardSeed,MinimumShots=minimumShots,MinimumBeamSeconds=minimumBeamSeconds,
            AwardRecipients=(bool[])awardRecipients.Clone(),AwardsFrozen=awardsFrozen,Achievements=(NativeAchievement[])achievements.Clone() };

        public void ValidateSnapshot(NativeMatchSnapshot snapshot)
        {
            int count=rows.Length;
            if(snapshot==null || snapshot.Version!=(timedChains?3:2) || (snapshot.TuningIdentity!=tuningIdentity&&!(!timedChains&&snapshot.AchievementTelemetryVersion==0&&snapshot.TuningIdentity==legacyTuningIdentity)) || snapshot.Tick<0 || !Enum.IsDefined(typeof(NativeMatchPhase),snapshot.Phase) ||
                snapshot.Roster==null || snapshot.Roster.Mode!=Roster.Mode || snapshot.Roster.Teams==null || !snapshot.Roster.Teams.SequenceEqual(Roster.Read().Teams) ||
                snapshot.Standings==null || snapshot.Standings.Length!=count || !snapshot.Standings.Select(r=>r.Seat).OrderBy(i=>i).SequenceEqual(Enumerable.Range(0,count)) ||
                snapshot.AssistLedger?.Length!=count*count || snapshot.DirectKillsByPair?.Length!=count*count ||
                snapshot.KillChains?.Length!=count || snapshot.ChainAwards?.Length!=count || snapshot.DiedThisTick?.Length!=count)
                throw new ArgumentException("Invalid resumable match snapshot");
            if(timedChains&&(snapshot.LastEligibleKillTicks?.Length!=count||Enumerable.Range(0,count).Any(i=>
                snapshot.KillChains[i]<0||snapshot.ChainAwards[i]<0||
                (snapshot.KillChains[i]==0?snapshot.LastEligibleKillTicks[i]!=-1||snapshot.ChainAwards[i]!=0:
                snapshot.LastEligibleKillTicks[i]<0||snapshot.LastEligibleKillTicks[i]>snapshot.Tick||
                snapshot.Tick-snapshot.LastEligibleKillTicks[i]>chainWindowTicks||
                snapshot.ChainAwards[i]!=(long)chainFirstAward+(snapshot.KillChains[i]-1L)*chainAwardStep))))
                throw new ArgumentException("Invalid timed kill-chain snapshot");
            foreach(var row in snapshot.Standings)
                if(row.Kills<0 || row.Assists<0 || row.Deaths<0 || row.SelfKills<0 || row.AllyKills<0 || row.AccumulatedPenalty<0 ||
                    !FiniteDamage(row.DamageDealt) || !FiniteDamage(row.DamageReceived) || !FiniteDamage(row.AllyDamageDealt) ||
                    !ValidAccuracy(row.RifleAccuracy)||!ValidAccuracy(row.ShotgunAccuracy)||!ValidAccuracy(row.RocketAccuracy)||!ValidAccuracy(row.CutterAccuracy)) throw new ArgumentException("Invalid match counters");
            if(snapshot.AchievementTelemetryVersion!=0&&snapshot.AchievementTelemetryVersion!=1)throw new ArgumentException("Invalid achievement telemetry version");
            if(snapshot.AchievementTelemetryVersion==1)
            {
                if(snapshot.MinimumShots!=minimumShots||snapshot.MinimumBeamSeconds!=minimumBeamSeconds||snapshot.AwardRecipients?.Length!=count||snapshot.Achievements==null||
                    snapshot.Standings.Any(r=>!FiniteDamage(r.DistanceTravelled)||!FiniteDamage(r.SelfDamageDealt)||!FiniteDamage(r.EnemyDamageReceived)||r.Jumps<0||r.Shots<0||r.WeaponSwitches<0||r.HealPickups<0||r.ArmorPickups<0||r.BonusPickups<0||r.FirstDeathTick<0||r.FirstDeathTick>snapshot.Tick))
                    throw new ArgumentException("Invalid achievement telemetry");
                if(snapshot.Achievements.Select(a=>a.Participant).Distinct().Count()!=snapshot.Achievements.Length||snapshot.Achievements.Any(a=>!ValidAward(snapshot,a,count))||!snapshot.AwardsFrozen&&snapshot.Achievements.Length!=0)throw new ArgumentException("Invalid frozen achievements");
                if(snapshot.AwardsFrozen!=(snapshot.Phase==NativeMatchPhase.Finished))throw new ArgumentException("Invalid achievement freeze state");
            }
        }
        bool ValidAward(NativeMatchSnapshot snapshot,NativeAchievement award,int count)
        {
            if(award.Participant<0||award.Participant>=count||!snapshot.AwardRecipients[award.Participant]||!NativeAchievementCatalog.IsKnown(award.Id)||!Enum.IsDefined(typeof(NativeAchievementTier),award.Tier)||string.IsNullOrEmpty(award.Name)||string.IsNullOrEmpty(award.Fact))return false;
            int version=award.RulesVersion==0?1:award.RulesVersion;
            if(version!=1&&version!=NativeAchievementCatalog.Version)return false;
            var eligible=NativeAchievementCatalog.Eligible(snapshot.Standings,award.Participant,minimumShots,minimumBeamSeconds,version);
            if(eligible.Length==0)return false;var highest=eligible.Max(candidate=>candidate.Tier);
            return award.Tier==highest&&eligible.Any(candidate=>candidate.Id==award.Id&&candidate.Tier==award.Tier&&candidate.Name==award.Name&&candidate.Fact==award.Fact);
        }
        static bool FiniteDamage(double value)=>value>=0 && !double.IsNaN(value) && !double.IsInfinity(value);
        static bool FiniteAccuracy(double value)=>FiniteDamage(value);
        static bool ValidAccuracy(WeaponAccuracy value)=>FiniteAccuracy(value.Used)&&FiniteAccuracy(value.Successful)&&value.Successful<=value.Used;
        public void Restore(NativeMatchSnapshot snapshot)
        {
            ValidateSnapshot(snapshot);
            // JsonUtility round-trips a null string as empty; both mean no finish trigger.
            tick=snapshot.Tick;Phase=snapshot.Phase;trigger=string.IsNullOrEmpty(snapshot.Trigger)?null:snapshot.Trigger;winner=snapshot.Winner;winnerTeam=snapshot.WinnerTeam;
            enteredOvertime=snapshot.RemainingTicks==0 && trigger!=null;
            if(snapshot.AchievementTelemetryVersion==0)
            {legacyAchievementsDisabled=true;telemetryVersion=0;Array.Clear(awardRecipients,0,awardRecipients.Length);achievements=Array.Empty<NativeAchievement>();awardsFrozen=false;}
            else
            {legacyAchievementsDisabled=false;telemetryVersion=1;awardSeed=snapshot.AwardSeed;Array.Copy(snapshot.AwardRecipients,awardRecipients,rows.Length);achievements=(NativeAchievement[])snapshot.Achievements.Clone();awardsFrozen=snapshot.AwardsFrozen;}
            foreach(var row in snapshot.Standings)rows[row.Seat]=row;
            if(timedChains)Array.Copy(snapshot.LastEligibleKillTicks,lastEligibleKillTicks,rows.Length);
            Array.Copy(snapshot.KillChains,chains,rows.Length);Array.Copy(snapshot.ChainAwards,chainAwards,rows.Length);
            Array.Copy(snapshot.DirectKillsByPair,directKillsByPair,directKillsByPair.Length);Array.Copy(snapshot.DiedThisTick,diedThisTick,rows.Length);
            for(int i=0;i<snapshot.AssistLedger.Length;i++)ledger[i/rows.Length,i%rows.Length]=snapshot.AssistLedger[i];
            finalRows=Phase==NativeMatchPhase.Finished?Ordered():null;
        }
    }
}
