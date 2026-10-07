using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

namespace StarTournament.ProvingGround
{
    public enum NativeAchievementTier { Bronze, Silver, Gold }
    [Serializable]
    public struct NativeAchievement
    {
        public int Participant;
        public string Id, Name, Fact;
        public NativeAchievementTier Tier;
    }
    /// <summary>Pure post-match rules. Never reads scene, input or gameplay random state.</summary>
    public static class NativeAchievementCatalog
    {
        public const int Version=1;
        public static NativeAchievement[] Eligible(NativeStanding[] rows,int participant,int minimumShots,double minimumBeamSeconds)
        {
            if(rows==null || rows.Length<2 || rows.Count(x=>x.Seat==participant)!=1)
                throw new ArgumentException("Invalid achievement roster");
            if(minimumShots<1 || minimumBeamSeconds<=0 || double.IsNaN(minimumBeamSeconds) || double.IsInfinity(minimumBeamSeconds))
                throw new ArgumentException("Invalid achievement sample");
            var r=rows.Single(x=>x.Seat==participant);var awards=new List<NativeAchievement>();
            bool maxDistance=Maximum(rows,r,x=>x.DistanceTravelled), minDistance=Minimum(rows,r,x=>x.DistanceTravelled);
            bool maxJumps=Maximum(rows,r,x=>x.Jumps), minKills=Minimum(rows,r,x=>x.Kills);
            bool maxShots=Maximum(rows,r,x=>x.Shots), maxDeaths=Maximum(rows,r,x=>x.Deaths);
            bool maxBonus=Maximum(rows,r,x=>x.BonusPickups), minDamage=Minimum(rows,r,x=>x.DamageDealt);
            bool maxReceived=Maximum(rows,r,x=>x.EnemyDamageReceived);
            bool active=HasSample(r,minimumShots,minimumBeamSeconds);
            var accurate=rows.Where(x=>HasSample(x,minimumShots,minimumBeamSeconds)).ToArray();
            bool minAccuracy=active && Minimum(accurate,r,x=>x.AccuracyPercent);
            string kills="Убийств: "+r.Kills, shots="Выстрелов: "+r.Shots, distance="Пройдено: "+N(r.DistanceTravelled)+" м";
            string damage="Врагам: "+N(r.DamageDealt)+" урона", received="От врагов: "+N(r.EnemyDamageReceived)+" урона";
            string precision="Точность: "+r.AccuracyPercent.ToString("0.#",CultureInfo.InvariantCulture)+"%";
            string bonuses="Бонусов: "+r.BonusPickups;
            Add(awards,r,"slowpoke",NativeAchievementTier.Bronze,"Тормозок",minDistance,distance);
            Add(awards,r,"jumper",NativeAchievementTier.Bronze,"Попрыгун",maxJumps,"Прыжков: "+r.Jumps);
            Add(awards,r,"bad-friend",NativeAchievementTier.Bronze,"Плохой друг",Maximum(rows,r,x=>x.AllyDamageDealt),"Союзникам: "+N(r.AllyDamageDealt)+" урона");
            Add(awards,r,"diamond-eye",NativeAchievementTier.Bronze,"Глаз-алмаз",minAccuracy,precision);
            Add(awards,r,"cemetery-sponsor",NativeAchievementTier.Bronze,"Спонсор кладбища",maxDeaths,"Смертей: "+r.Deaths);
            Add(awards,r,"first-pancake",NativeAchievementTier.Bronze,"Первый блин",r.FirstDeathTick>0 && r.FirstDeathTick==rows.Where(x=>x.FirstDeathTick>0).Select(x=>x.FirstDeathTick).DefaultIfEmpty(0).Min(),"Смерть №1 в матче");
            Add(awards,r,"humanitarian",NativeAchievementTier.Bronze,"Гуманист",minDamage,damage);
            Add(awards,r,"walking-target",NativeAchievementTier.Bronze,"Мишень с ногами",maxReceived,received);
            Add(awards,r,"pharmacy-magnate",NativeAchievementTier.Bronze,"Аптечный магнат",Maximum(rows,r,x=>x.HealPickups),"Аптечек: "+r.HealPickups);
            Add(awards,r,"weapon-sommelier",NativeAchievementTier.Bronze,"Оружейный сомелье",Maximum(rows,r,x=>x.WeaponSwitches),"Смен оружия: "+r.WeaponSwitches);
            Add(awards,r,"trainee",NativeAchievementTier.Bronze,"Стажёр",minKills,kills);
            Add(awards,r,"own-pain",NativeAchievementTier.Bronze,"Больно, но своё",Maximum(rows,r,x=>x.SelfDamageDealt),"Себе: "+N(r.SelfDamageDealt)+" урона");
            Add(awards,r,"no-help-needed",NativeAchievementTier.Bronze,"Не дождался помощи",Maximum(rows,r,x=>x.SelfKills),"Самоустранений: "+r.SelfKills);
            Add(awards,r,"all-mine",NativeAchievementTier.Bronze,"Всё моё",maxBonus,bonuses);

            Add(awards,r,"warning-fire",NativeAchievementTier.Silver,"Предупредительный огонь",maxShots&&minKills,shots+" · "+kills);
            Add(awards,r,"cardio",NativeAchievementTier.Silver,"Кардиотренировка",maxDistance&&minKills,distance+" · "+kills);
            Add(awards,r,"almost-dangerous",NativeAchievementTier.Silver,"Почти опасный",minKills&&Maximum(rows.Where(x=>x.Kills==r.Kills).ToArray(),r,x=>x.DamageDealt),damage+" · "+kills);
            Add(awards,r,"assistant-assistant",NativeAchievementTier.Silver,"Ассистент ассистента",Maximum(rows,r,x=>x.Assists)&&minKills,"Ассистов: "+r.Assists+" · "+kills);
            Add(awards,r,"armor-didnt-help",NativeAchievementTier.Silver,"Броня не помогла",Maximum(rows,r,x=>x.ArmorPickups)&&maxDeaths,"Брони: "+r.ArmorPickups+" · смертей: "+r.Deaths);
            Add(awards,r,"greed",NativeAchievementTier.Silver,"Жадность до добра",maxBonus&&Minimum(rows,r,x=>x.Score),bonuses+" · очков: "+r.Score);
            Add(awards,r,"own-opponent",NativeAchievementTier.Silver,"Сам себе противник",r.DamageDealt>0&&r.SelfDamageDealt>r.DamageDealt,"Себе: "+N(r.SelfDamageDealt)+" · врагам: "+N(r.DamageDealt));
            Add(awards,r,"jumped-to-end",NativeAchievementTier.Silver,"Прыгал до последнего",maxJumps&&minKills,"Прыжков: "+r.Jumps+" · "+kills);
            Add(awards,r,"noise-force",NativeAchievementTier.Silver,"Шумовой спецназ",maxShots&&minAccuracy,shots+" · "+precision);
            Add(awards,r,"bad-trade",NativeAchievementTier.Silver,"Обмен невыгодный",maxReceived&&minDamage,received+" · "+damage);

            Add(awards,r,"pacifist",NativeAchievementTier.Gold,"Пацифист года",r.Kills==0&&active&&r.DamageDealt>0,kills+" · "+damage);
            Add(awards,r,"enemy-within",NativeAchievementTier.Gold,"Враг внутри",r.DamageDealt>0&&r.AllyDamageDealt>r.DamageDealt,"Союзникам: "+N(r.AllyDamageDealt)+" · врагам: "+N(r.DamageDealt));
            Add(awards,r,"why-ammo",NativeAchievementTier.Gold,"Зачем тебе патроны?",maxShots&&active&&r.RifleAccuracy.Successful==0&&r.ShotgunAccuracy.Successful==0&&r.RocketAccuracy.Successful==0&&r.CutterAccuracy.Successful==0,shots+" · попаданий во врагов: 0");
            Add(awards,r,"worst-own-enemy",NativeAchievementTier.Gold,"Главный свой враг",r.SelfKills>r.Kills,"Самоустранений: "+r.SelfKills+" · "+kills);
            Add(awards,r,"team-saboteur",NativeAchievementTier.Gold,"Командный вредитель",r.AllyKills>r.Kills,"Союзников убито: "+r.AllyKills+" · "+kills);
            Add(awards,r,"collector",NativeAchievementTier.Gold,"Коллекционер без побед",maxBonus&&r.Kills==0&&r.DamageDealt>0,bonuses+" · "+kills);
            return awards.ToArray();
        }
        public static NativeAchievement[] Select(NativeStanding[] rows,bool[] recipients,int seed,int minimumShots,double minimumBeamSeconds)
        {
            var result=new List<NativeAchievement>();var random=new Random(seed);
            for(int p=0;p<recipients.Length;p++)
            {
                if(!recipients[p])continue;
                var candidates=Eligible(rows,p,minimumShots,minimumBeamSeconds);
                if(candidates.Length==0)continue;
                var tier=candidates.Max(x=>x.Tier);
                var best=candidates.Where(x=>x.Tier==tier).ToArray();
                result.Add(best[random.Next(best.Length)]);
            }
            return result.ToArray();
        }
        static readonly string[] ids={"slowpoke","jumper","bad-friend","diamond-eye","cemetery-sponsor","first-pancake","humanitarian","walking-target","pharmacy-magnate","weapon-sommelier","trainee","own-pain","no-help-needed","all-mine","warning-fire","cardio","almost-dangerous","assistant-assistant","armor-didnt-help","greed","own-opponent","jumped-to-end","noise-force","bad-trade","pacifist","enemy-within","why-ammo","worst-own-enemy","team-saboteur","collector"};
        public static bool IsKnown(string id)=>Array.IndexOf(ids,id)>=0;
        public static bool HasSample(NativeStanding row,int minimumShots,double minimumBeamSeconds)=>row.Shots>=minimumShots||row.CutterAccuracy.Used>=minimumBeamSeconds;
        static string N(double number)=>number.ToString("0.#",CultureInfo.InvariantCulture);
        static bool Maximum(NativeStanding[] rows,NativeStanding row,Func<NativeStanding,double> value)
        {var v=value(row);return rows.Length>1&&v>0&&rows.Any(x=>value(x)<v)&&rows.All(x=>value(x)<=v);}
        static bool Minimum(NativeStanding[] rows,NativeStanding row,Func<NativeStanding,double> value)
        {var v=value(row);return rows.Length>1&&rows.Any(x=>value(x)>v)&&rows.All(x=>value(x)>=v);}
        static void Add(List<NativeAchievement> awards,NativeStanding row,string id,NativeAchievementTier tier,string name,bool eligible,string fact)
        {if(eligible)awards.Add(new NativeAchievement{Participant=row.Seat,Id=id,Tier=tier,Name=name,Fact=fact});}
    }
}
