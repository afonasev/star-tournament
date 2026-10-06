using System;
using NUnit.Framework;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests.EditMode
{
    public sealed class RocketBotTests
    {
        sealed class Route:INativeNavigation
        {
            public string Identity=>"pulse-test";
            public bool TryLocate(Vector3 feet,out NativeNavigationPoint point){point=new NativeNavigationPoint(feet,"floor");return true;}
            public bool ValidTransition(string a,string b)=>false;
            public bool TryRoute(Vector3 a,Vector3 b,out NativeNavigationPoint[] points,out string failure){points=new[]{new NativeNavigationPoint(b,"floor")};failure=null;return true;}
        }
        sealed class Tactics:INativeBotTactics,INativeRocketTactics
        {
            public bool Clear=true; public Vector3[] Anchors=>new[]{Vector3.forward*20};
            public bool CanMove(Vector3 a,Vector3 b,float d)=>true;
            public bool CanJump(ParticipantState s,Vector3 d)=>true;
            public bool Covered(Vector3 a,Vector3 b)=>false;
            public bool ClearRocketShot(Vector3 o,Vector3 d,float distance,out Vector3 end){end=o+d*distance;return Clear;}
        }
        [Test] public void CutterUsesHeldActionAtProfileRangeAndExhaustionFallsBack()
        {
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            var bot=new NativeBotPlanner(NativeBotDifficulty.Normal,7,ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateNavigationDefault(),new Route(),new Tactics(),new NativeShotgunPolicy(move,combat),100);
            var state=new CombatLife("p",ProvingProfile.CreateCombatDefault());state.CollectWeapon(WeaponId.Cutter);state.Select(WeaponSelection.Cutter);state.Advance(1);
            var frame=new NativeBotFrame{Pose=new ParticipantState{Grounded=true},Life=state.Read(),Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true,Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.forward*10),Visible=true,ObservedAt=0}}},Allies=Array.Empty<NativeBotAlly>()};
            bool held=false;
            for(int i=0;i<100;i++){var a=bot.Tick(i*.02,.02f,frame);held|=a.FireHeld;Assert.That(a.Fire,Is.False,"Cutter holds rather than pulsing edges");}
            Assert.That(held,Is.True);frame.Life.CutterEnergy=0;
            Assert.That(bot.Tick(2,.02f,frame).SelectWeapon,Is.Not.EqualTo(WeaponSelection.Cutter));
        }
        [TestCase(3,WeaponSelection.None)]
        [TestCase(12,WeaponSelection.RocketLauncher)]
        [TestCase(60,WeaponSelection.RocketLauncher)]
        public void ExpectedDamageReplacesDistanceOnlyWeaponPriority(float distance,WeaponSelection expected)
        {
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            var bot=new NativeBotPlanner(NativeBotDifficulty.Normal,7,ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateNavigationDefault(),new Route(),new Tactics(),new NativeShotgunPolicy(move,combat),100);
            var frame=new NativeBotFrame{Pose=new ParticipantState{Grounded=true},Life=new CombatLife("p",ProvingProfile.CreateCombatDefault()).Read(),Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true,Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.forward*distance),Visible=true,ObservedAt=0}}},Allies=Array.Empty<NativeBotAlly>()};
            frame.Life.CutterEnergy=0;frame.Life.RocketOwned=frame.Life.ShotgunOwned=true;frame.Life.RocketAmmo=frame.Life.ShotgunAmmo=20;
            Assert.That(bot.Tick(0,.02f,frame).SelectWeapon,Is.EqualTo(expected));
        }
        [TestCase(false,false)][TestCase(true,true)]
        public void NewBalanceRejectsUnsafePulseAndChoosesAvailableShotgun(bool clear,bool ally)
        {
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            var bot=new NativeBotPlanner(NativeBotDifficulty.Normal,7,ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateNavigationDefault(),new Route(),new Tactics{Clear=clear},new NativeShotgunPolicy(move,combat),100);
            var frame=new NativeBotFrame{Pose=new ParticipantState{Grounded=true},Life=new CombatLife("p",ProvingProfile.CreateCombatDefault()).Read(),Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true,Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.forward*12),Visible=true,ObservedAt=0}}},Allies=ally?new[]{new NativeBotAlly{Participant=2,Position=Vector3.forward*12}}:Array.Empty<NativeBotAlly>()};
            frame.Life.RocketOwned=frame.Life.ShotgunOwned=true;frame.Life.RocketAmmo=frame.Life.ShotgunAmmo=20;
            // Exhaustion makes the available shotgun the useful fallback, without relying
            // on a superseded unconditional distance preference over a ready rifle.
            frame.Life.RifleAmmo=0;
            Assert.That(bot.Tick(0,.02f,frame).SelectWeapon,Is.EqualTo(WeaponSelection.Shotgun));
            Assert.That(bot.RocketRejected,Is.GreaterThan(0),"Safety check rejected Pulse rather than authorizing an unsafe shot");
        }
        [TestCase(12,true,false,WeaponSelection.RocketLauncher)]
        [TestCase(60,true,false,WeaponSelection.RocketLauncher)]
        [TestCase(3,true,false,WeaponSelection.Shotgun)]
        [TestCase(12,false,false,WeaponSelection.None)]
        [TestCase(12,true,true,WeaponSelection.None)]
        public void BotsUseMediumAndFarRocketsButAvoidCloseOccludedAndAlliedSplash(float distance,bool clear,bool ally,WeaponSelection expected)
        {
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            // Controlled weapon contrast tests Pulse safety/range, independent of changing published balance.
            combat.Set("rifle.damage",20);combat.Set("shot.damage",70);combat.Set("shot.spread",7);combat.Set("rocket.maximumDamage",100);
            var legacyLife=ProvingProfile.CreateCombatDefault();legacyLife.Set("rifle.cooldownSeconds",.12f);
            var bot=new NativeBotPlanner(NativeBotDifficulty.Normal,7,ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateNavigationDefault(),new Route(),new Tactics{Clear=clear},new NativeShotgunPolicy(move,combat,lifecycle:legacyLife),100);
            var frame=new NativeBotFrame{Pose=new ParticipantState{Grounded=true},Life=new CombatLife("p",legacyLife).Read(),Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true,Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.forward*distance),Visible=true,ObservedAt=0}}},Allies=ally?new[]{new NativeBotAlly{Participant=2,Position=Vector3.forward*distance}}:Array.Empty<NativeBotAlly>()};
            // Isolate Pulse versus shotgun when a safe attack is possible; unsafe cases
            // retain the ready rifle and independently exercise the safety rejection.
            frame.Life.CutterEnergy=0;frame.Life.RocketOwned=frame.Life.ShotgunOwned=true;frame.Life.RocketAmmo=frame.Life.ShotgunAmmo=20;
            if(clear&&!ally)frame.Life.RifleAmmo=0;
            Assert.That(bot.Tick(0,.02f,frame).SelectWeapon,Is.EqualTo(expected));
            if(!clear||ally)Assert.That(bot.RocketRejected,Is.GreaterThan(0));
        }
    }
}
