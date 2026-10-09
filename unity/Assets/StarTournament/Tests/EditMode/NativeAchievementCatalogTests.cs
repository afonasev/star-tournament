using System;
using System.Linq;
using NUnit.Framework;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class NativeAchievementCatalogTests
    {
        static NativeStanding[] Rows()=>Enumerable.Range(0,4).Select(p=>new NativeStanding{Seat=p,Kills=2,Score=20,DamageDealt=20,DistanceTravelled=20,Shots=10,RifleAccuracy=new WeaponAccuracy{Used=10,Successful=5}}).ToArray();
        static NativeAchievement[] Eligible(NativeStanding[] rows)=>NativeAchievementCatalog.Eligible(rows,0,10,1);
        [TestCase("slowpoke")]
        [TestCase("jumper")]
        [TestCase("bad-friend")]
        [TestCase("diamond-eye")]
        [TestCase("cemetery-sponsor")]
        [TestCase("first-pancake")]
        [TestCase("humanitarian")]
        [TestCase("walking-target")]
        [TestCase("pharmacy-magnate")]
        [TestCase("weapon-sommelier")]
        [TestCase("trainee")]
        [TestCase("own-pain")]
        [TestCase("no-help-needed")]
        [TestCase("all-mine")]
        [TestCase("warning-fire")]
        [TestCase("cardio")]
        [TestCase("almost-dangerous")]
        [TestCase("assistant-assistant")]
        [TestCase("armor-didnt-help")]
        [TestCase("greed")]
        [TestCase("own-opponent")]
        [TestCase("jumped-to-end")]
        [TestCase("noise-force")]
        [TestCase("bad-trade")]
        [TestCase("pacifist")]
        [TestCase("enemy-within")]
        [TestCase("why-ammo")]
        [TestCase("worst-own-enemy")]
        [TestCase("team-saboteur")]
        [TestCase("collector")]
        public void CatalogIncludesEachNominationForItsActualFacts(string id)
        {
            var rows=Rows();ref var r=ref rows[0];
            switch(id)
            {
                case "slowpoke": r.DistanceTravelled=5;break;
                case "jumper": r.Jumps=5;break;
                case "bad-friend": r.AllyDamageDealt=50;break;
                case "diamond-eye": r.RifleAccuracy.Successful=1;break;
                case "cemetery-sponsor": r.Deaths=5;break;
                case "first-pancake": r.FirstDeathTick=3;rows[1].FirstDeathTick=5;break;
                case "humanitarian": r.DamageDealt=5;break;
                case "walking-target": r.EnemyDamageReceived=50;break;
                case "pharmacy-magnate": r.HealPickups=5;break;
                case "weapon-sommelier": r.WeaponSwitches=5;break;
                case "trainee": r.Kills=1;break;
                case "own-pain": r.SelfDamageDealt=50;break;
                case "no-help-needed": r.SelfKills=1;break;
                case "all-mine": r.BonusPickups=5;break;
                case "warning-fire": r.Shots=20;r.Kills=1;break;
                case "cardio": r.DistanceTravelled=50;r.Kills=1;break;
                case "almost-dangerous": r.Kills=1;rows[1].Kills=1;rows[1].DamageDealt=5;break;
                case "assistant-assistant": r.Assists=5;r.Kills=1;break;
                case "armor-didnt-help": r.ArmorPickups=5;r.Deaths=5;break;
                case "greed": r.BonusPickups=5;r.Score=5;break;
                case "own-opponent": r.SelfDamageDealt=5;r.DamageDealt=10;break;
                case "jumped-to-end": r.Jumps=5;r.Kills=1;break;
                case "noise-force": r.Shots=20;r.RifleAccuracy.Successful=1;break;
                case "bad-trade": r.EnemyDamageReceived=50;r.DamageDealt=5;break;
                case "pacifist": r.Kills=1;r.Assists=5;r.DamageDealt=5;break;
                case "enemy-within": r.AllyDamageDealt=5;r.DamageDealt=10;break;
                case "why-ammo": r.Shots=20;r.RifleAccuracy.Successful=1;r.DamageDealt=10;break;
                case "worst-own-enemy": r.SelfDamageDealt=5;r.Deaths=5;break;
                case "team-saboteur": r.AllyKills=1;r.Kills=1;break;
                case "collector": r.BonusPickups=5;r.Kills=1;r.DistanceTravelled=50;break;
                default: Assert.Fail("Unknown test nomination");break;
            }
            var award=Eligible(rows).Single(x=>x.Id==id);
            Assert.That(award.Participant,Is.Zero);Assert.That(award.Name,Is.Not.Empty);Assert.That(award.Fact,Is.Not.Empty);
            Assert.That(NativeAchievementCatalog.IsKnown(award.Id),Is.True);
        }
        [Test] public void EmptyMatchAndAllEqualActiveMatchHaveNoComparativeAwards()
        {
            var empty=Enumerable.Range(0,4).Select(p=>new NativeStanding{Seat=p}).ToArray();
            Assert.That(Eligible(empty),Is.Empty);Assert.That(Eligible(Rows()),Is.Empty);
        }
        [Test] public void PartialTiesQualifyEveryoneSharingTheRecordIncludingBotComparisons()
        {
            var rows=Rows();rows[0].Jumps=rows[1].Jumps=5;
            Assert.That(NativeAchievementCatalog.Eligible(rows,0,10,1).Any(x=>x.Id=="jumper"),Is.True);
            Assert.That(NativeAchievementCatalog.Eligible(rows,1,10,1).Any(x=>x.Id=="jumper"),Is.True);
            Assert.That(NativeAchievementCatalog.Eligible(rows,2,10,1).Any(x=>x.Id=="jumper"),Is.False);
            rows[2].Jumps=10;
            Assert.That(Eligible(rows).Any(x=>x.Id=="jumper"),Is.False,"A stronger bot still takes the record");
        }
        [Test] public void AShotgunPelletCountCannotTurnOneShotIntoAQualifyingSample()
        {
            var rows=Rows();rows[0].Shots=1;rows[0].RifleAccuracy=default;
            rows[0].ShotgunAccuracy=new WeaponAccuracy{Used=20,Successful=0};
            Assert.That(Eligible(rows).Any(x=>x.Id=="diamond-eye"),Is.False);
            rows[0].CutterAccuracy=new WeaponAccuracy{Used=1,Successful=0};
            Assert.That(Eligible(rows).Any(x=>x.Id=="diamond-eye"),Is.True,"Cutter qualifies through emitted duration");
        }
        [Test] public void GoldPrecedesBothSilverAndBronzeAndChoiceIsStableForSavedSeed()
        {
            var rows=Rows();rows[0].SelfDamageDealt=5;rows[0].Deaths=5;rows[0].AllyKills=1;rows[0].Kills=1;rows[0].Jumps=5;
            var mask=new[]{true,false,false,false};
            var candidates=Eligible(rows);Assert.That(candidates.Select(x=>x.Tier).Distinct().Count(),Is.EqualTo(3));
            var selected=NativeAchievementCatalog.Select(rows,mask,77,10,1);
            Assert.That(selected.Length,Is.EqualTo(1));Assert.That(selected[0].Tier,Is.EqualTo(NativeAchievementTier.Gold));
            for(int i=0;i<100;i++)Assert.That(NativeAchievementCatalog.Select(rows,mask,77,10,1)[0].Id,Is.EqualTo(selected[0].Id));
            var seen=Enumerable.Range(0,100).Select(seed=>NativeAchievementCatalog.Select(rows,mask,seed,10,1)[0].Id).Distinct().ToArray();
            Assert.That(seen.Length,Is.EqualTo(2),"Both deserved gold nominations can be selected");
            Assert.That(NativeAchievementCatalog.Select(rows,new bool[4],77,10,1),Is.Empty,"All-bot roster receives no awards");
        }
        [Test] public void SilverPrecedesBronzeAndNoCandidateIsSkipped()
        {
            var rows=Rows();rows[0].Jumps=5;rows[0].Kills=1;
            Assert.That(NativeAchievementCatalog.Select(rows,new[]{true,false,false,false},5,10,1)[0].Tier,Is.EqualTo(NativeAchievementTier.Silver));
            Assert.That(NativeAchievementCatalog.Select(Rows(),new[]{true,false,false,false},5,10,1),Is.Empty);
        }
        [Test] public void LoneLowestKillerCanBeAlmostDangerousEvenInADuel()
        {
            var rows=Rows().Take(2).ToArray();rows[0].Kills=1;
            Assert.That(Eligible(rows).Any(a=>a.Id=="almost-dangerous"),Is.True);
            Assert.That(NativeAchievementCatalog.Eligible(rows,0,10,1,1).Any(a=>a.Id=="almost-dangerous"),Is.False);
            rows[0].DamageDealt=0;Assert.That(Eligible(rows).Any(a=>a.Id=="almost-dangerous"),Is.False);
            rows=Rows();rows[0].Kills=rows[1].Kills=1;Assert.That(Eligible(rows).Any(a=>a.Id=="almost-dangerous"),Is.False,"multiple equal lowest-kill peers keep complete-tie exclusion");
        }
        [Test] public void IdleParticipantCannotCollectAwardsFromDeathsOrComparativeMinimums()
        {
            var rows=Rows();rows[0]=new NativeStanding{Seat=0,Deaths=9,EnemyDamageReceived=100,FirstDeathTick=1};
            Assert.That(Eligible(rows),Is.Empty);
            Assert.That(NativeAchievementCatalog.Eligible(rows,0,10,1,1),Is.Not.Empty,"old frozen rules remain available");
            rows[0].Shots=9;Assert.That(Eligible(rows),Is.Empty);
            rows[0].Shots=10;Assert.That(Eligible(rows),Is.Not.Empty,"existing sample threshold qualifies participation");
            rows[0].Shots=0;rows[0].DamageDealt=1;Assert.That(Eligible(rows),Is.Not.Empty,"actual enemy contribution qualifies below shot threshold");
        }
        [Test] public void NewGoldKeepsSuccessfulCombatAndSmallAccidentalHarm()
        {
            foreach(string id in new[]{"pacifist","enemy-within","why-ammo","worst-own-enemy","team-saboteur","collector"})
            {
                var rows=Rows();ref var row=ref rows[0];row.Kills=1;row.DamageDealt=10;
                if(id=="pacifist")row.Assists=5;
                if(id=="enemy-within")row.AllyDamageDealt=1;
                if(id=="why-ammo"){row.Shots=20;row.RifleAccuracy.Successful=1;}
                if(id=="worst-own-enemy"){row.SelfDamageDealt=1;row.Deaths=5;}
                if(id=="team-saboteur")row.AllyKills=1;
                if(id=="collector"){row.BonusPickups=5;row.DistanceTravelled=50;}
                var award=Eligible(rows).Single(a=>a.Id==id);Assert.That(award.RulesVersion,Is.EqualTo(2));
                Assert.That(row.Kills,Is.Positive);Assert.That(row.RifleAccuracy.Successful,Is.Positive);
                Assert.That(row.SelfKills,Is.Zero);Assert.That(row.SelfDamageDealt,Is.LessThan(row.DamageDealt));
                Assert.That(row.AllyDamageDealt,Is.LessThan(row.DamageDealt));Assert.That(row.AllyKills,Is.LessThanOrEqualTo(row.Kills));
                if(id=="why-ammo"){row.DamageDealt=25;Assert.That(Eligible(rows).Any(a=>a.Id==id),Is.False,"all three independent ranks are required");}
            }
        }
        [Test] public void FourActiveHumansCanShareGoldRecordsAgainstBots()
        {
            var rows=Enumerable.Range(0,8).Select(p=>new NativeStanding{Seat=p,Kills=p<4?1:2,Shots=10,DamageDealt=20,SelfDamageDealt=p<4?5:0,Deaths=p<4?5:1}).ToArray();
            var awards=NativeAchievementCatalog.Select(rows,new[]{true,true,true,true,false,false,false,false},77,10,1);
            Assert.That(awards.Length,Is.EqualTo(4));Assert.That(awards.All(a=>a.Tier==NativeAchievementTier.Gold&&a.Id=="worst-own-enemy"),Is.True);
        }
        [Test] public void UnknownRulesVersionIsRejected()
        {Assert.Throws<ArgumentException>(()=>NativeAchievementCatalog.Eligible(Rows(),0,10,1,99));}
        [Test] public void FirstDeathUsesTickAndAllowsSimultaneousFirstVictims()
        {
            var rows=Rows();rows[0].FirstDeathTick=rows[1].FirstDeathTick=8;rows[2].FirstDeathTick=9;
            Assert.That(Eligible(rows).Any(x=>x.Id=="first-pancake"),Is.True);
            Assert.That(NativeAchievementCatalog.Eligible(rows,1,10,1).Any(x=>x.Id=="first-pancake"),Is.True);
            Assert.That(NativeAchievementCatalog.Eligible(rows,2,10,1).Any(x=>x.Id=="first-pancake"),Is.False);
        }
    }
}
