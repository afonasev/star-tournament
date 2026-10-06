using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
namespace StarTournament.ProvingGround.Tests.EditMode
{
    public class NativeBotTacticsTests
    {
        static NativeBotObservationFrame[] Frames(int life=1)=>Enumerable.Range(0,3).Select(_=>new NativeBotObservationFrame{OwnLife=life,Alive=true}).ToArray();
        static NativeBotPickupEvent[] World(bool available)=>new[]{new NativeBotPickupEvent{Id="pulse",Kind=NativeBotPickupKind.Weapon,Weapon=WeaponId.RocketLauncher,Position=Vector3.forward*10,Available=available}};
        [Test] public void PickupDelayBootstrapDisappearanceOrderingAndRespawnDoNotRevealCollector()
        {
            var profile=ProvingProfile.CreateBotPerceptionDefault();var k=new NativeBotPickupKnowledge(new[]{NativeBotDifficulty.Easy,NativeBotDifficulty.Normal,NativeBotDifficulty.Hard},profile);
            k.Sample(0,Frames(),World(true));for(int i=0;i<3;i++)Assert.That(k.Read(i),Is.Empty);
            k.Sample(.16,Frames(),World(false));Assert.That(k.Read(2).Single().Available,Is.True);Assert.That(k.Read(1),Is.Empty);
            var saved=k.Capture();var restored=new NativeBotPickupKnowledge(new[]{NativeBotDifficulty.Easy,NativeBotDifficulty.Normal,NativeBotDifficulty.Hard},profile);restored.Restore(saved);
            foreach(var target in new[]{k,restored}){target.Sample(.32,Frames(),World(false));Assert.That(target.Read(2).Single().Available,Is.False);target.Sample(1.5,Frames(),World(false));for(int i=0;i<3;i++)Assert.That(target.Read(i).Single().Available,Is.False);target.Sample(1.6,Frames(2),World(true));for(int i=0;i<3;i++)Assert.That(target.Read(i),Is.Empty);}
            Assert.That(JsonUtility.ToJson(k.Capture()),Is.EqualTo(JsonUtility.ToJson(restored.Capture())));
            Assert.That(typeof(NativeBotPickupEvent).GetFields().Any(f=>f.Name.Contains("Collector")||f.Name.Contains("Health")),Is.False);
        }
        [Test] public void DirectVitalsAndMovementAgeWithoutHiddenUpdatesAndReportsRetainSourceTime()
        {
            var roster=new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB});
            var p=new NativeBotPerception(roster,new[]{NativeBotDifficulty.Hard,NativeBotDifficulty.Hard,NativeBotDifficulty.Hard},ProvingProfile.CreateBotPerceptionDefault());
            var frames=Frames();frames[0].Direct=new[]{new NativeBotSighting(2,1,Vector3.zero,20,30)};p.Sample(0,frames);
            frames[0].Direct=new[]{new NativeBotSighting(2,1,Vector3.forward,10,5)};p.Sample(.1,frames);
            Assert.That(p.Read(0).Enemies.Single().Sighting.Velocity.z,Is.EqualTo(10).Within(.001));
            frames[0].Direct=Array.Empty<NativeBotSighting>();p.Sample(.31,frames);
            var reported=p.Read(1).Enemies.Single();Assert.That(reported.ObservedAt,Is.EqualTo(0));Assert.That(reported.Sighting.Health,Is.EqualTo(20));
            var stale=p.Read(0).Enemies.Single();Assert.That(stale.Sighting.Health,Is.EqualTo(10));Assert.That(stale.Sighting.Armor,Is.EqualTo(5));Assert.That(stale.Visible,Is.False);
            p.Sample(7,frames);Assert.That(p.Read(0).Enemies,Is.Empty);Assert.That(p.Read(1).Enemies,Is.Empty);
        }
        [Test] public void CorruptPickupRestoreIsAtomicAndNoSameTickDelivery()
        {
            var p=ProvingProfile.CreateBotPerceptionDefault();var k=new NativeBotPickupKnowledge(new[]{NativeBotDifficulty.Easy,NativeBotDifficulty.Normal,NativeBotDifficulty.Hard},p);k.Sample(0,Frames(),World(true));string before=JsonUtility.ToJson(k.Capture());
            var bad=k.Capture();bad.Pending[0].DeliverAt=0;Assert.Throws<ArgumentException>(()=>k.Restore(bad));Assert.That(JsonUtility.ToJson(k.Capture()),Is.EqualTo(before));
        }
        sealed class Route:INativeNavigation
        {
            public string Identity=>"tactics-route";public bool Block;
            public bool TryLocate(Vector3 feet,out NativeNavigationPoint point){point=new NativeNavigationPoint(feet,"floor");return true;}
            public bool ValidTransition(string a,string b)=>false;
            public bool TryRoute(Vector3 from,Vector3 to,out NativeNavigationPoint[] points,out string failure){points=new[]{new NativeNavigationPoint(to,"floor")};failure=Block?"blocked":null;return !Block;}
        }
        sealed class Tactics:INativeBotTactics,INativeRocketSurfaceTactics
        {
            public Vector3 SurfacePoint,FirstPoint;public bool Reach=true,HasSurface=true,EarlyWall;public Vector3[] Anchors=>new[]{Vector3.forward*20};
            public bool CanMove(Vector3 a,Vector3 b,float d)=>true;public bool CanJump(ParticipantState a,Vector3 d)=>false;public bool Covered(Vector3 a,Vector3 b)=>true;
            public bool Surface(Vector3 feet,float probe,out Vector3 point){point=feet;point.y=0;SurfacePoint=point;return HasSurface;}
            public bool FirstContact(Vector3 o,Vector3 d,float distance,out Vector3 point){point=EarlyWall?o+d: o+d*distance;FirstPoint=point;return true;}
            public bool SplashReach(Vector3 a,Vector3 b)=>Reach;
        }
        static NativeBotPlanner Bot(Route route,Tactics tactics)=>new NativeBotPlanner(NativeBotDifficulty.Hard,9,ProvingProfile.CreateBotBehaviorDefault(),ProvingProfile.CreateNavigationDefault(),route,tactics,new NativeShotgunPolicy(ProvingProfile.CreateDefault(),ProvingProfile.CreateNativeCombatDefault()),100);
        static NativeBotFrame Frame()=>new NativeBotFrame{Pose=new ParticipantState{Grounded=true},Life=new CombatLife("bot",ProvingProfile.CreateCombatDefault()).Read(),Knowledge=new NativeBotKnowledge{OwnLife=1,Alive=true}};
        [Test] public void VulnerableEnemyWinsObservedTargetChoiceAndStrongEnemyTriggersRetreat()
        {
            var bot=Bot(new Route(),new Tactics());var f=Frame();
            f.Knowledge.Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.forward*8,100,100),Visible=true},new NativeBotMemoryEntry{Sighting=new NativeBotSighting(2,1,Vector3.forward*12,10,0),Visible=true}};
            bot.Tick(0,.02f,f);Assert.That(bot.Capture().Target,Is.EqualTo(2));
            var retreat=Bot(new Route(),new Tactics());f.Knowledge.Enemies=f.Knowledge.Enemies.Take(1).ToArray();retreat.Tick(0,.02f,f);Assert.That(retreat.Intent,Is.EqualTo(NativeBotIntent.Retreat));
        }
        [Test] public void PickupPolicyChoosesNeededHealThenUnlockAndRefillAndRejectsUnreachable()
        {
            var bot=Bot(new Route(),new Tactics());var f=Frame();f.Life.Health=10;
            f.Pickups=new[]{new NativeBotPickupEvent{Id="heal",Kind=NativeBotPickupKind.Heal,Available=true,Position=Vector3.forward*3},new NativeBotPickupEvent{Id="gun",Kind=NativeBotPickupKind.Weapon,Weapon=WeaponId.Shotgun,Available=true,Position=Vector3.forward*10}};
            bot.Tick(0,.02f,f);Assert.That(bot.PickupId,Is.EqualTo("heal"));f.Life.Health=100;bot.Tick(.2,.02f,f);Assert.That(bot.PickupId,Is.EqualTo("gun"));Assert.That(bot.UnlockDecisions,Is.GreaterThan(0));
            f.Life.ShotgunOwned=true;f.Life.ShotgunAmmo=0;bot.Tick(.4,.02f,f);Assert.That(bot.PickupId,Is.EqualTo("gun"));
            f.Pickups[1].Available=false;bot.Tick(.6,.02f,f);Assert.That(bot.PickupId,Is.Null);
            var route=new Route{Block=true};var impossible=Bot(route,new Tactics());f.Pickups[1].Available=true;impossible.Tick(0,.02f,f);Assert.That(impossible.PickupId,Is.Null);
        }
        [Test] public void RocketUsesObservedLeadSurfaceAndRejectsEarlyWallAndAlliedSplash()
        {
            var t=new Tactics();var f=Frame();f.Life.RocketOwned=true;f.Life.RocketAmmo=20;
            f.Knowledge.Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,Vector3.forward*30,100,0){Velocity=Vector3.right*2},Visible=true}};
            Bot(new Route(),t).Tick(0,.02f,f);Assert.That(t.SurfacePoint.x,Is.GreaterThan(0));Assert.That(t.SurfacePoint.y,Is.EqualTo(0));
            t.EarlyWall=true;var unsafeBot=Bot(new Route(),t);Assert.That(unsafeBot.Tick(0,.02f,f).SelectWeapon,Is.Not.EqualTo(WeaponSelection.RocketLauncher));Assert.That(unsafeBot.RocketRejected,Is.GreaterThan(0));
            t.EarlyWall=false;f.Allies=new[]{new NativeBotAlly{Participant=2,Position=Vector3.forward*30}};Assert.That(Bot(new Route(),t).Tick(0,.02f,f).SelectWeapon,Is.Not.EqualTo(WeaponSelection.RocketLauncher));
        }
    }
}
