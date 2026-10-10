using System;
using System.Collections.Generic;
using System.Linq;

namespace StarTournament.ProvingGround
{
    public enum BotBanterReason { Revenge, SeriesEnded, RepeatedKiller, LosingStreak, NarrowWin, Missed, WeakReply, NoReply, KillStreak, SelfExplosion, SelfKill }

    /// <summary>Presentation tuning only; never part of simulation balance or a replay snapshot.</summary>
    public sealed class BotBanterPolicy
    {
        public const string Identity = "bot-banter-v1";
        public double InitialSilence = 15, GlobalMinimum = 25, GlobalMaximum = 40;
        public double BotMinimum = 60, BotMaximum = 90, DisplaySeconds = 4, DuelSeconds = 8;
        public double Chance = .65;
        public float WeakDamageFraction = .1f, NearDeathFraction = .2f;
        public int SeriesLength = 3;
    }

    public readonly struct BotBanterLine
    {
        public readonly int Speaker, Target;
        public readonly BotBanterReason Reason;
        public readonly string Text;
        public readonly double Until;
        public readonly bool SpeakerMayBeDead;
        public BotBanterLine(int speaker,int target,BotBanterReason reason,string text,double until,bool dead)
        { Speaker=speaker;Target=target;Reason=reason;Text=text;Until=until;SpeakerMayBeDead=dead; }
    }

    /// <summary>Independent, disposable presentation history. All amounts are actual HP + shield losses.</summary>
    public sealed class BotBanter
    {
        sealed class History
        {
            public int Life, Chain, Losses, LastKiller=-1, RepeatedLosses;
            public bool Known, Fired;
            public readonly HashSet<int> Revenge=new HashSet<int>();
            public readonly List<Loss> Damage=new List<Loss>();
        }
        readonly struct Loss
        {
            public readonly int Source, SourceLife;
            public readonly double Time;
            public readonly float Health, Armor;
            public Loss(int source,int life,double time,float health,float armor)
            { Source=source;SourceLife=life;Time=time;Health=health;Armor=armor; }
        }
        readonly struct Attempt
        {
            public readonly int Shooter, ShooterLife, Target, TargetLife;
            public readonly double Time;
            public Attempt(int shooter,int life,int target,int targetLife,double time)
            { Shooter=shooter;ShooterLife=life;Target=target;TargetLife=targetLife;Time=time; }
        }
        readonly NativeMatchComposition composition;
        readonly BotBanterPolicy policy;
        readonly Random random;
        readonly History[] history;
        readonly List<Attempt> attempts=new List<Attempt>();
        readonly HashSet<string> used=new HashSet<string>();
        readonly double[] botUntil;
        double globalUntil;
        BotBanterReason? lastReason;
        public BotBanterLine? Current { get; private set; }
        public event Action<BotBanterLine> LineSelected;
        public BotBanter(NativeMatchComposition composition,CombatLifeState[] lives,int seed=0,BotBanterPolicy policy=null)
        {
            this.composition=composition??throw new ArgumentNullException(nameof(composition));
            this.policy=policy??new BotBanterPolicy();random=new Random(seed);
            history=Enumerable.Range(0,composition.ParticipantCount).Select(_=>new History()).ToArray();
            botUntil=new double[history.Length];Reset(lives,0,true);
        }
        bool Bot(int p)=>p>=0&&composition.Participant(p).Kind==NativeParticipantKind.Bot;
        bool Human(int p)=>p>=0&&composition.Participant(p).Kind==NativeParticipantKind.LocalHuman;
        bool Enemy(int a,int b)=>a>=0&&b>=0&&a!=b&&!composition.Roster.AreAllies(a,b);
        public void Reset(CombatLifeState[] lives,double time,bool newMatch=false)
        {
            Current=null;attempts.Clear();used.Clear();lastReason=null;globalUntil=time+policy.InitialSilence;
            for(int i=0;i<history.Length;i++)
            {
                history[i]=new History { Life=lives[i].Life,Known=newMatch&&!lives[i].Dead };
                botUntil[i]=time;
            }
        }
        public void Respawn(int participant,int life)
        {
            var h=history[participant];h.Life=life;h.Known=true;h.Fired=false;h.Damage.Clear();
            attempts.RemoveAll(a=>a.Shooter==participant||a.Target==participant);
        }
        public void Attack(int shooter,int life,int target,int targetLife,double time)
        {
            var h=history[shooter];if(h.Life!=life)return;
            h.Fired=true;attempts.RemoveAll(a=>time-a.Time>policy.DuelSeconds);
            if(target>=0&&Enemy(shooter,target))
                attempts.Add(new Attempt(shooter,life,target,targetLife,time));
        }
        public void Damage(int target,int life,int source,int sourceLife,float health,float armor,double time)
        {
            if(history[target].Life!=life||health+armor<=0)return;
            history[target].Damage.Add(new Loss(source,sourceLife,time,health,armor));
        }
        bool Attempted(int shooter,int target,double time)=>attempts.Any(a=>a.Shooter==shooter&&a.Target==target&&
            a.ShooterLife==history[shooter].Life&&a.TargetLife==history[target].Life&&time-a.Time<=policy.DuelSeconds);

        public BotBanterReason? LastCandidate { get; private set; }
        public void Death(int victim,int killer,int killerLife,WeaponId weapon,CombatLifeState[] lives,float maximumHealth,double time,bool exchangePending=false)
        {
            LastCandidate=null;
            if(Current.HasValue&&Current.Value.Speaker==victim&&!Current.Value.SpeakerMayBeDead)Current=null;
            var dead=history[victim];int oldChain=dead.Chain;dead.Chain=0;
            var candidates=new List<(int speaker,int target,BotBanterReason reason,bool dead)>();
            if(killer==victim)
            {
                dead.Losses=0;dead.LastKiller=-1;dead.RepeatedLosses=0;
                if(Bot(victim))candidates.Add((victim,-1,weapon==WeaponId.RocketLauncher?BotBanterReason.SelfExplosion:BotBanterReason.SelfKill,true));
            }
            else if(Enemy(killer,victim))
            {
                dead.Losses++;dead.RepeatedLosses=dead.LastKiller==killer?dead.RepeatedLosses+1:1;dead.LastKiller=killer;
                if(Bot(victim))
                {
                    if(Human(killer)&&dead.RepeatedLosses==policy.SeriesLength)
                    { dead.Revenge.Add(killer);candidates.Add((victim,killer,BotBanterReason.RepeatedKiller,true)); }
                    else if(dead.Losses==policy.SeriesLength)candidates.Add((victim,-1,BotBanterReason.LosingStreak,true));
                    if(oldChain>=policy.SeriesLength)candidates.Add((victim,killer,BotBanterReason.SeriesEnded,true));
                }
                var winner=history[killer];
                // Projectiles from a dead/previous incarnation do not start a living kill series.
                bool alive=!lives[killer].Dead&&lives[killer].Life==killerLife&&winner.Life==killerLife;
                if(alive)
                {
                    winner.Chain++;winner.Losses=0;winner.LastKiller=-1;winner.RepeatedLosses=0;
                    bool revenge=winner.Revenge.Remove(victim);
                    if(Bot(killer)&&Human(victim))
                    {
                        if(revenge)candidates.Add((killer,victim,BotBanterReason.Revenge,false));
                        bool duel=winner.Known&&dead.Known&&!exchangePending&&dead.Damage.Count>0&&
                            dead.Damage.All(d=>d.Source==killer&&d.SourceLife==killerLife)&&
                            dead.Damage.Any(d=>time-d.Time<=policy.DuelSeconds);
                        if(duel)
                        {
                            var reply=winner.Damage.Where(d=>d.Source==victim&&d.SourceLife==dead.Life).ToArray();
                            float replyDamage=reply.Sum(d=>d.Health+d.Armor);
                            bool recent=reply.Any(d=>time-d.Time<=policy.DuelSeconds);
                            if(lives[killer].Health<=maximumHealth*policy.NearDeathFraction&&recent&&
                                reply.Sum(d=>d.Health)>=maximumHealth*(1-policy.NearDeathFraction)&&
                                winner.Damage.All(d=>d.Source==victim&&d.SourceLife==dead.Life))
                                candidates.Add((killer,victim,BotBanterReason.NarrowWin,false));
                            else if(replyDamage>0&&replyDamage<=maximumHealth*policy.WeakDamageFraction&&recent)
                                candidates.Add((killer,victim,BotBanterReason.WeakReply,false));
                            else if(replyDamage==0&&Attempted(victim,killer,time))
                                candidates.Add((killer,victim,BotBanterReason.Missed,false));
                            else if(replyDamage==0&&!dead.Fired&&Attempted(killer,victim,time))
                                candidates.Add((killer,victim,BotBanterReason.NoReply,false));
                        }
                        if(winner.Chain==policy.SeriesLength)candidates.Add((killer,-1,BotBanterReason.KillStreak,false));
                    }
                }
            }
            else
            {
                dead.Losses=0;dead.LastKiller=-1;dead.RepeatedLosses=0;
                if(killer>=0)history[killer].Chain=0; // allied kill breaks the offender's chain too.
            }
            if(candidates.Count==0)return;
            var selected=candidates.OrderBy(c=>(int)c.reason).First();LastCandidate=selected.reason;
            if(time<globalUntil||time<botUntil[selected.speaker]||lastReason==selected.reason||random.NextDouble()>=policy.Chance)return;
            var available=Lines(selected.reason).Where(s=>!used.Contains(s)).ToArray();if(available.Length==0)return;
            string text=available[random.Next(available.Length)];used.Add(text);lastReason=selected.reason;
            globalUntil=time+Between(policy.GlobalMinimum,policy.GlobalMaximum);
            botUntil[selected.speaker]=time+Between(policy.BotMinimum,policy.BotMaximum);
            Current=new BotBanterLine(selected.speaker,selected.target,selected.reason,text,time+policy.DisplaySeconds,selected.dead);
            LineSelected?.Invoke(Current.Value);
        }
        double Between(double min,double max)=>min+random.NextDouble()*(max-min);
        public static string[] Lines(BotBanterReason reason)
        {
            switch(reason)
            {
                case BotBanterReason.Revenge:return new[]{"А вот и моя месть!","Всё, отомстил!"};
                case BotBanterReason.SeriesEnded:return new[]{"Убили-таки! А я только разошёлся!","Ну всё, кончилась моя серия!"};
                case BotBanterReason.RepeatedKiller:return new[]{"Опять ты меня убил!","Да отстань ты от меня!"};
                case BotBanterReason.LosingStreak:return new[]{"Опять меня убили!","Я играть пришёл, а не помирать!"};
                case BotBanterReason.NarrowWin:return new[]{"Чуть не убил меня, зараза!","Еле выжил! Но ты-то помер!"};
                case BotBanterReason.Missed:return new[]{"Даже не поцарапал!","Ты хоть попади сначала!"};
                case BotBanterReason.WeakReply:return new[]{"Слабак!","Слабо бьёшь!","Попал. А толку?"};
                case BotBanterReason.NoReply:return new[]{"Даже выстрелить не успел!"};
                case BotBanterReason.KillStreak:return new[]{"Кто следующий?","Я сегодня в ударе!"};
                case BotBanterReason.SelfExplosion:return new[]{"Сам себя взорвал. Молодец!"};
                default:return new[]{"Сам себя убил. Талант!"};
            }
        }
    }
}
