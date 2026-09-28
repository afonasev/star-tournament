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
        [TestCase(12,true,false,WeaponSelection.RocketLauncher)]
        [TestCase(60,true,false,WeaponSelection.RocketLauncher)]
        [TestCase(3,true,false,WeaponSelection.Shotgun)]
        [TestCase(12,false,false,WeaponSelection.None)]
        [TestCase(12,true,true,WeaponSelection.None)]
        public void BotsUseMediumAndFarRocketsButAvoidCloseOccludedAndAlliedSplash(float distance,bool clear,bool ally,WeaponSelection expected)
        {
            var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            var bot=new NativeBotPlanner(NativeBotDifficulty.Normal,7,ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateNavigationDefault(),new Route(),new Tactics{Clear=clear},new NativeShotgunPolicy(move,combat),100);
            var frame=new NativeBotFrame{Pose=new ParticipantState{Grounded=true},Life=new CombatLife("p",ProvingProfile.CreateCombatDefault()).Read(),Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true,Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.forward*distance),Visible=true,ObservedAt=0}}},Allies=ally?new[]{new NativeBotAlly{Participant=2,Position=Vector3.forward*distance}}:Array.Empty<NativeBotAlly>()};
            Assert.That(bot.Tick(0,.02f,frame).SelectWeapon,Is.EqualTo(expected));
        }
    }
}
