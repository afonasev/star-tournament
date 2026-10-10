using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class BotBanterTests
    {
        CombatLifeState[] lives;
        BotBanter banter;
        static BotBanterPolicy Always()=>new BotBanterPolicy { InitialSilence=0,GlobalMinimum=0,GlobalMaximum=0,BotMinimum=0,BotMaximum=0,Chance=1 };
        void Setup(BotBanterPolicy policy=null,bool teams=false)
        {
            var roster=teams?new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}):NativeMatchRoster.Ffa(4);
            var info=Enumerable.Range(0,4).Select(p=>new NativeParticipantInfo(p==0||p==2?NativeParticipantKind.Bot:NativeParticipantKind.LocalHuman,"P"+p,NativeStandingsView.Identity(roster.Read(),p,false),p==0||p==2?1:-1)).ToArray();
            var comp=new NativeMatchComposition(roster,info,new[]{1,3});
            lives=Enumerable.Range(0,4).Select(p=>new CombatLifeState{Life=1,Health=100,ParticipantId="P"+p}).ToArray();
            banter=new BotBanter(comp,lives,7,policy??Always());
        }
        void Die(int victim,int killer,double time=20,WeaponId weapon=WeaponId.Rifle,int killerLife=1,bool pending=false)
        { lives[victim].Dead=true;banter.Death(victim,killer,killerLife,weapon,lives,100,time,pending); }
        void Respawn(int p){lives[p].Life++;lives[p].Dead=false;lives[p].Health=100;banter.Respawn(p,lives[p].Life);}
        void Hit(int target,int source,float hp,float armor=0,double time=19)
        {banter.Damage(target,lives[target].Life,source,source<0?0:lives[source].Life,hp,armor,time);}
        void Duel(float hp=0,float armor=0)
        {banter.Attack(1,1,0,1,19);if(hp+armor>0)Hit(0,1,hp,armor);Hit(1,0,100);}
        [TestCase(0,0,BotBanterReason.Missed)]
        [TestCase(0,1,BotBanterReason.WeakReply)]
        [TestCase(1,0,BotBanterReason.WeakReply)]
        [TestCase(0,10,BotBanterReason.WeakReply)]
        public void ReplyUsesActualShieldAndHealth(float hp,float shield,BotBanterReason reason)
        {Setup();Duel(hp,shield);Die(1,0);Assert.That(banter.LastCandidate,Is.EqualTo(reason));Assert.That(banter.Current.Value.Reason,Is.EqualTo(reason));}
        [Test] public void AboveWeakThresholdDoesNotInsultAccuracyOrDamage()
        {Setup();Duel(10.01f);Die(1,0);Assert.That(banter.LastCandidate,Is.Null);}
        [Test] public void NeverShootingDiffersFromShootingElsewhere()
        {
            Setup();banter.Attack(0,1,1,1,19);Hit(1,0,100);Die(1,0);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.NoReply));
            Setup();banter.Attack(0,1,1,1,19);banter.Attack(1,1,2,1,19);Hit(1,0,100);Die(1,0);Assert.That(banter.LastCandidate,Is.Null);
        }
        [TestCase(-1)] [TestCase(2)] [TestCase(1)]
        public void AssistedEnvironmentalOrSelfDamagedVictimIsNotACleanDuel(int other)
        {Setup();Duel();Hit(1,other,1);Die(1,0);Assert.That(banter.LastCandidate,Is.Null);}
        [Test] public void OldAttemptsOldDamageAndPendingExchangeAreConservative()
        {
            Setup();banter.Attack(1,1,0,1,1);Hit(1,0,100);Die(1,0);Assert.That(banter.LastCandidate,Is.Null);
            Setup();Duel();Die(1,0,pending:true);Assert.That(banter.LastCandidate,Is.Null);
            Setup();banter.Attack(1,1,0,1,19);Hit(0,1,1,time:1);Hit(1,0,100);Die(1,0);Assert.That(banter.LastCandidate,Is.Null,"Old real damage still forbids 'never scratched'");
        }
        [Test] public void DeadOrPreviousLifeKillerCannotBoast()
        {
            Setup();Duel();lives[0].Dead=true;Die(1,0);Assert.That(banter.LastCandidate,Is.Null);
            Setup();Duel();lives[0].Life=2;Die(1,0);Assert.That(banter.LastCandidate,Is.Null);
        }
        [Test] public void NarrowWinMustActuallyBeCausedByThisOpponentRecently()
        {
            Setup();Duel(80);lives[0].Health=20;Die(1,0);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.NarrowWin));
            Setup();Duel(1);Hit(0,2,79);lives[0].Health=20;Die(1,0);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.WeakReply));
            Setup();Duel(80);lives[0].Health=21;Die(1,0);Assert.That(banter.LastCandidate,Is.Null);
        }
        [Test] public void RepeatedDeathsComplainOnceAndFirstRevengeHasPriority()
        {
            Setup();Die(0,1,20);Assert.That(banter.LastCandidate,Is.Null);Respawn(0);
            Die(0,1,40);Assert.That(banter.LastCandidate,Is.Null);Respawn(0);
            Die(0,1,60);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.RepeatedKiller));Respawn(0);
            Die(0,1,80);Assert.That(banter.LastCandidate,Is.Null);Respawn(0);
            Hit(1,0,100,time:99);Die(1,0,100,killerLife:lives[0].Life);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.Revenge));
            Respawn(1);Hit(1,0,100,time:119);Die(1,0,120,killerLife:lives[0].Life);Assert.That(banter.LastCandidate,Is.Not.EqualTo(BotBanterReason.Revenge));
        }
        [Test] public void MixedKillersProduceGeneralComplaintAtThresholdOnly()
        {Setup();Die(0,1);Respawn(0);Die(0,3);Respawn(0);Die(0,1);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.LosingStreak));Respawn(0);Die(0,3);Assert.That(banter.LastCandidate,Is.Null);}
        [Test] public void KillChainAndItsEndHaveRealThresholds()
        {
            Setup();for(int i=0;i<3;i++){Hit(1,0,100);Die(1,0);if(i<2)Respawn(1);}
            Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.KillStreak));
            Die(0,3,21);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.SeriesEnded));
        }
        [Test] public void AlliesNeverProduceEnemyTaunts()
        {Setup(teams:true);Duel();Die(1,0);Assert.That(banter.LastCandidate,Is.Null);}
        [TestCase(WeaponId.RocketLauncher,BotBanterReason.SelfExplosion)]
        [TestCase(WeaponId.Rifle,BotBanterReason.SelfKill)]
        public void SelfDeathUsesActualCause(WeaponId weapon,BotBanterReason reason)
        {Setup();Die(0,0,weapon:weapon);Assert.That(banter.Current.Value.Reason,Is.EqualTo(reason));}
        [Test] public void RestoreMakesExistingLivesUnknownUntilRespawn()
        {
            Setup();banter.Reset(lives,10);Duel();Die(1,0);Assert.That(banter.LastCandidate,Is.Null);
            Respawn(0);Respawn(1);banter.Attack(1,2,0,2,39);Hit(1,0,100,time:39);Die(1,0,40,killerLife:2);Assert.That(banter.LastCandidate,Is.EqualTo(BotBanterReason.Missed));
        }
        [Test] public void GlobalBotCooldownsDropEventsAndNoPhraseOrReasonRepeats()
        {
            var policy=Always();policy.GlobalMinimum=policy.GlobalMaximum=25;policy.BotMinimum=policy.BotMaximum=60;
            Setup(policy);Duel();Die(1,0,20);var first=banter.Current.Value;
            Die(2,2,21);Assert.That(banter.Current.Value.Text,Is.EqualTo(first.Text),"Global gap applies across bots");
            Die(0,0,46);Assert.That(banter.Current,Is.Null,"Death clears obsolete boast and bot gap blocks replacement");
            Respawn(0);Die(0,0,80);Assert.That(banter.Current.Value.Reason,Is.EqualTo(BotBanterReason.SelfKill));
            Respawn(0);Die(0,0,150);Assert.That(banter.Current.Value.Until,Is.EqualTo(84),"Same reason never follows itself; no deferred queue");
            Respawn(0);Die(0,0,220,WeaponId.RocketLauncher);Assert.That(banter.Current.Value.Reason,Is.EqualTo(BotBanterReason.SelfExplosion));
            Respawn(0);Die(0,0,290);Assert.That(banter.Current.Value.Reason,Is.EqualTo(BotBanterReason.SelfExplosion),"Exhausted exact phrase cannot repeat");
        }
        [Test] public void SelectedLineEventIsOnceAndResetNeverReplaysIt()
        {
            Setup();int count=0;BotBanterLine seen=default;banter.LineSelected+=line=>{count++;seen=line;};
            Duel();Die(1,0);Assert.That(count,Is.EqualTo(1));Assert.That(seen.Text,Is.EqualTo(banter.Current.Value.Text));
            banter.Reset(lives,21);Assert.That(count,Is.EqualTo(1));
            Setup(new BotBanterPolicy());banter.LineSelected+=_=>count++;Duel();Die(1,0,1);Assert.That(count,Is.EqualTo(1));
        }
        [Test] public void DefaultPolicyHasInitialSilenceAndProbabilityCanSkip()
        {Setup(new BotBanterPolicy());Duel();Die(1,0,1);Assert.That(banter.Current,Is.Null);var p=Always();p.Chance=0;Setup(p);Duel();Die(1,0);Assert.That(banter.Current,Is.Null);}
    }
}
