using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellArenaPhysicsTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement;NativeNavigationProvider routes;
        [SetUp] public void Setup()
        {
            root=new GameObject("well-physics");movement=ProvingProfile.CreateDefault();arena=root.AddComponent<ProvingArena>();arena.Build(ArenaFreezeSnapshot.Create(AuthoredArenaCatalog.Resolve(WellArenaCatalog.Id,Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_PHYSICS_REVISION")),movement),movement);
            routes=new NativeNavigationProvider(arena,movement,ProvingProfile.CreateNavigationDefault());
        }
        [UnityTearDown] public IEnumerator Cleanup(){UnityEngine.Object.Destroy(root);yield return null;}
        CharacterMotor Motor(Vector3 pos)
        {var go=new GameObject("well-participant");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,pos,arena);return motor;}
        void Tick(CharacterMotor motor,LocalAction action=default)
        {arena.Lifts.Tick(1/60f,new[]{motor},_=>true);motor.Tick(action,1/60f);Physics.SyncTransforms();}
        [UnityTest] public IEnumerator EverySpawnAndPickupHasCapsuleClearanceAndCorrectPhysicalSupport()
        {
            yield return null;Physics.SyncTransforms();
            foreach(var pos in arena.Definition.Spawns.Concat(arena.Definition.Pickups.Select(x=>x.Anchor)))
            {
                var motor=Motor(pos);for(int i=0;i<10;i++)motor.Tick(default,1/60f);
                Assert.That(motor.State.Position.y,Is.EqualTo(pos.y).Within(.1f),pos.ToString());Assert.That(Vector3.Distance(motor.State.Position,pos),Is.LessThan(.15f),"blocked spawn/pickup "+pos);
                var radius=movement.Get("player.capsule.radius")*.95f;float height=movement.Get("player.capsule.height");
                Assert.That(Physics.CheckCapsule(pos+Vector3.up*(radius+.1f),pos+Vector3.up*(height-radius),radius,(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer)),Is.False,"capsule overlap "+pos);
                UnityEngine.Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator EightParticipantPlacementAndEveryPickupUseTheRealSession()
        {
            yield return null;
            var combat=ProvingProfile.CreateNativeCombatDefault();var roster=NativeMatchRoster.Ffa(8);
            var selector=new SafeSpawnSelector(Physics.defaultPhysicsScene,arena,movement,combat);
            string details=string.Join(", ",arena.Spawns.Select((p,i)=>i+":"+selector.Valid(p,out _)))+"; visible pairs=";
            for(int i=0;i<arena.Spawns.Length;i++)for(int j=i+1;j<arena.Spawns.Length;j++)if(selector.BodyVisible(arena.Spawns[i],arena.Spawns[j])||selector.BodyVisible(arena.Spawns[j],arena.Spawns[i]))details+=i+"/"+j+" ";
            Assert.That(selector.TryInitial(roster,ProvingProfile.CreateTeamDefault().Get("spawn.initialOpponentSeparation"),out var selected),Is.True,selector.InitialFailure+" "+details);
            Assert.That(selected.Length,Is.EqualTo(8));
            foreach(var pickup in arena.Definition.Pickups)
            {
                var motor=Motor(arena.Definition.Spawns.OrderBy(x=>Mathf.Abs(x.y-pickup.Anchor.y)).First());for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                var session=new NativeCombatSession(new[]{motor},arena,Physics.defaultPhysicsScene,movement,ProvingProfile.CreateCombatDefault(),combat);
                if(pickup.Kind==ArenaPickupKind.Damage)session.Tick(new LocalAction[1],(float)session.DamagePickup.Remaining+1/60f);
                if(pickup.Kind==ArenaPickupKind.Speed)session.Tick(new LocalAction[1],(float)session.SpeedPickups.Single(x=>x.InstanceId==pickup.Id).Remaining+1/60f);
                if(pickup.Kind==ArenaPickupKind.FullHeal)session.Tick(new LocalAction[1],(float)session.HealPickups.Single(x=>x.InstanceId==pickup.Id).Remaining+1/60f);
                var pose=motor.State;pose.Position=pickup.Anchor;pose.Velocity=Vector3.zero;motor.RestoreState(pose);
                if(pickup.Kind==ArenaPickupKind.FullHeal)session.ApplyDamage(0,session.Life(0).Life,25);
                bool collected=false;session.PickupCollected+=(seat,id,kind)=>{if(id==pickup.Id)collected=true;};session.Tick(new LocalAction[1],1/60f);
                Assert.That(collected,Is.True,pickup.Id+" real contact/support");UnityEngine.Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator StairRoutesAndRoomBypassesConnectAllFloors()
        {
            yield return null;Physics.SyncTransforms();
            foreach(var from in arena.Definition.Spawns)foreach(var to in arena.Definition.Pickups.Select(x=>x.Anchor))
                Assert.That(routes.TryRoute(from,to,out _,out var failure),Is.True,from+" -> "+to+": "+failure);
            foreach(var transition in arena.Definition.Transitions)
            {
                foreach(bool reverse in new[]{false,true})
                {
                    var points=reverse?transition.OrderedFeet.Reverse().ToArray():transition.OrderedFeet;
                    var motor=Motor(points[0]);for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                    int next=1;
                    for(int tick=0;tick<600&&next<points.Length;tick++)
                    {
                        var delta=points[next]-motor.State.Position;delta.y=0;
                        if(delta.magnitude<.22f){next++;continue;}
                        var local=Quaternion.Euler(0,-motor.State.Yaw,0)*delta.normalized;
                        motor.Tick(new LocalAction{Move=new Vector2(local.x,local.z)},1/60f);Physics.SyncTransforms();
                    }
                    Assert.That(next,Is.EqualTo(points.Length),transition.Id+" reverse="+reverse+" at "+motor.State.Position);Assert.That(motor.State.Position.y,Is.EqualTo(points.Last().y).Within(.35f));UnityEngine.Object.DestroyImmediate(motor.gameObject);
                }
            }
        }
        [UnityTest] public IEnumerator LiftCarriesRealCapsuleRestoreAndStopsWithPassenger()
        {
            yield return null;var motor=Motor(new Vector3(0,0,-27));for(int i=0;i<5;i++)motor.Tick(default,1/60f);
            var session=new NativeCombatSession(new[]{motor},arena,Physics.defaultPhysicsScene,movement,ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault());
            var actions=new[]{default(LocalAction)};
            for(int i=0;i<180;i++)session.Tick(actions,1/60f);
            var saved=session.Capture();float height=arena.Lifts.Capture()[1].Height;Assert.That(height,Is.GreaterThan(WellArenaCatalog.Storey));Assert.That(motor.State.Position.y,Is.EqualTo(height).Within(.15f));
            for(int i=0;i<180;i++){session.Tick(actions,1/60f);Assert.That(motor.State.Position.y,Is.EqualTo(arena.Lifts.Capture()[1].Height).Within(.15f),"carry tick "+i+" pose="+JsonUtility.ToJson(motor.State));}var final=session.Capture();Assert.That(motor.State.Position.y,Is.EqualTo(2*WellArenaCatalog.Storey).Within(.15f));
            session.Restore(saved);Assert.That(arena.Lifts.Capture()[1].Height,Is.EqualTo(height));Assert.That(motor.State.Position,Is.EqualTo(saved.Poses[0].Position));
            for(int i=0;i<180;i++)session.Tick(actions,1/60f);Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(JsonUtility.ToJson(final)),"restored continuation");
            for(int i=0;i<300;i++)session.Tick(actions,1/60f);Assert.That(arena.Lifts.Capture()[1].Height,Is.EqualTo(2*WellArenaCatalog.Storey));
        }
        [UnityTest] public IEnumerator BotBoardsExitsSecondStoreyAndEmptyTripCompletes()
        {
            yield return null;
            foreach(int side in new[]{-1,1})
            {
                float start=side<0?0:2*WellArenaCatalog.Storey;
                var motor=Motor(new Vector3(0,start,side*23));for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                var router=new WellBotLiftRouter(arena.Lifts,routes,arena.WellProfile,movement);var goal=new Vector3(0,WellArenaCatalog.Storey,side*17);
                bool boarded=false,exit=false;
                for(int tick=0;tick<600;tick++)
                {
                    var action=router.Tick(motor.State,goal,true,false,default);Tick(motor,action);
                    boarded|=router.Capture().Riding;exit|=router.Capture().Exiting;
                    if(exit&&motor.State.Grounded&&side*motor.State.Position.z<24&&Mathf.Abs(motor.State.Position.y-WellArenaCatalog.Storey)<.2f)break;
                }
                Assert.That(boarded,Is.True,"boarded "+side);Assert.That(exit,Is.True,"middle exit "+side);
                Assert.That(motor.State.Position.y,Is.EqualTo(WellArenaCatalog.Storey).Within(.25f),"middle floor "+side);
                Assert.That(side*motor.State.Position.z,Is.LessThan(24),"exit "+side);
                for(int i=0;i<300;i++)Tick(motor);
                Assert.That(arena.Lifts.Capture().Single(l=>l.Id==(side<0?"south":"north")).Height,Is.EqualTo(side<0?2*WellArenaCatalog.Storey:0));
                UnityEngine.Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator UnavailableLiftsKeepStairFallbackAndMotorReachesSecondFloor()
        {
            yield return null;
            var states=arena.Lifts.Capture();foreach(var state in states){state.Height=state.Target=2*WellArenaCatalog.Storey;state.Phase=WellLiftPhase.AwaitEmpty;state.Countdown=0;}arena.Lifts.Restore(states);
            foreach(int side in new[]{-1,1})
            {
                var motor=Motor(new Vector3(side*21,0,0));for(int i=0;i<5;i++)motor.Tick(default,1/60f);
                var fallback=new WellBotLiftRouter(arena.Lifts,routes,arena.WellProfile,movement);var goal=new Vector3(side*21,WellArenaCatalog.Storey,0);
                Assert.That(routes.TryRoute(motor.State.Position,goal,out var points,out var failure),Is.True,failure);
                Assert.That(points.Any(p=>p.Support.StartsWith("transition:stair-"+side)),Is.True,"real stair fallback");
                int next=0;
                for(int tick=0;tick<1200&&next<points.Length;tick++)
                {
                    var delta=points[next].Position-motor.State.Position;
                    if(new Vector2(delta.x,delta.z).magnitude<.22f&&Mathf.Abs(delta.y)<.35f){next++;continue;}
                    delta.y=0;var local=Quaternion.Euler(0,-motor.State.Yaw,0)*delta.normalized;
                    var original=new LocalAction{Move=new Vector2(local.x,local.z)};var action=fallback.Tick(motor.State,goal,true,false,original);
                    Assert.That(fallback.Capture().Lift,Is.Null);Assert.That(action.Move,Is.EqualTo(original.Move));Tick(motor,action);
                }
                Assert.That(next,Is.EqualTo(points.Length),"stair fallback "+side+" at "+motor.State.Position);
                Assert.That(Vector3.Distance(motor.State.Position,goal),Is.LessThan(.4f));UnityEngine.Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator SecondFloorOuterCorridorsRemainOpenPastBothStairs()
        {
            yield return null;
            foreach(int side in new[]{-1,1})
            {
                var motor=Motor(new Vector3(side*21,WellArenaCatalog.Storey,-13));
                for(int tick=0;tick<600&&motor.State.Position.z<13;tick++)
                {motor.Tick(new LocalAction{Move=Vector2.up},1/60f);Physics.SyncTransforms();Assert.That(motor.State.Position.y,Is.EqualTo(WellArenaCatalog.Storey).Within(.15f));}
                Assert.That(motor.State.Position.z,Is.GreaterThanOrEqualTo(13));UnityEngine.Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator GalleryCasesBlockCapsulesAndShotsWhileRoutesGoAround()
        {
            yield return null;Physics.SyncTransforms();
            foreach(var solid in arena.Definition.Solids.Where(s=>s.Id.Contains("gallery-cover")||s.Id.Contains("-corridor-")))
            {
                float floor=solid.Position.y-solid.Size.y/2;
                float reach=solid.Id.Contains("-corridor-")?solid.Size.x/2+movement.Get("player.capsule.radius")+movement.Get("player.capsule.skinWidth")+.2f:3;
                var start=new Vector3(solid.Position.x-reach,floor,solid.Position.z);var goal=start+Vector3.right*(2*reach);
                Assert.That(Physics.Raycast(start+Vector3.up*.7f,Vector3.right,out var hit,2*reach,ProvingArena.ShotMask),Is.True,solid.Id);
                Assert.That(hit.collider.name,Is.EqualTo(solid.Id));
                Assert.That(Physics.Raycast(start+Vector3.up*(solid.Size.y+.3f),Vector3.right,out _,2*reach,ProvingArena.ShotMask),Is.False,"Fire above low case");
                var motor=Motor(start);for(int i=0;i<90;i++)Tick(motor,new LocalAction{Move=Vector2.right});
                Assert.That(motor.State.Position.x,Is.LessThan(solid.Position.x-solid.Size.x/2),"real capsule must not enter case");
                UnityEngine.Object.DestroyImmediate(motor.gameObject);motor=Motor(start);for(int i=0;i<5;i++)Tick(motor);
                Assert.That(routes.TryRoute(start,goal,out var points,out var failure),Is.True,solid.Id+" "+failure);
                int next=0;
                for(int tick=0;tick<1200&&next<points.Length;tick++)
                {
                    var delta=points[next].Position-motor.State.Position;
                    if(new Vector2(delta.x,delta.z).magnitude<.22f&&Mathf.Abs(delta.y)<.35f){next++;continue;}
                    delta.y=0;var local=Quaternion.Euler(0,-motor.State.Yaw,0)*delta.normalized;
                    Tick(motor,new LocalAction{Move=new Vector2(local.x,local.z)});
                }
                Assert.That(next,Is.EqualTo(points.Length),solid.Id+" bypass stopped at "+motor.State.Position);
                Assert.That(Vector3.Distance(motor.State.Position,goal),Is.LessThan(.4f));UnityEngine.Object.DestroyImmediate(motor.gameObject);
            }
        }
        [UnityTest] public IEnumerator WindowsBlockFireButNotSightAndGratesPassShots()
        {
            yield return null;
            foreach(var solid in arena.Definition.Solids.Where(s=>s.Material=="glass"))
            {
                var normal=solid.Size.x<solid.Size.z?Vector3.right:Vector3.forward;
                Assert.That(Physics.Raycast(solid.Position-normal*.1f,normal,out var hit,.2f,ProvingArena.ShotMask),Is.True);Assert.That(hit.collider.GetComponent<NativeSightTransparent>(),Is.Not.Null,solid.Id);
            }
            Assert.That(Physics.Raycast(new Vector3(0,WellArenaCatalog.Storey+1,0),Vector3.down,out var bridgeHit,2,ProvingArena.ShotMask),Is.False,"bridge must pass bullets/rockets");
            Assert.That(Physics.Raycast(new Vector3(0,WellArenaCatalog.Storey+1,0),Vector3.down,out _,2,1<<ProvingArena.MovementOnlyLayer),Is.True);
        }
    }
}
