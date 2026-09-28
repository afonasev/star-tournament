using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class CombatBowlR7PhysicsTests
    {
        GameObject root;ProvingArena arena;ProvingProfile movement,combat;SafeSpawnSelector spawn;
        [UnitySetUp] public IEnumerator Setup()
        {
            root=new GameObject("R7-physics");arena=root.AddComponent<ProvingArena>();movement=ProvingProfile.CreateDefault();combat=ProvingProfile.CreateNativeCombatDefault();
            arena.Build(CombatBowlCatalog.Freeze(movement),movement);spawn=new SafeSpawnSelector(Physics.defaultPhysicsScene,arena,movement,combat);yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup(){Object.Destroy(root);yield return null;}
        [UnityTest] public IEnumerator EightRegionsAreValidAndAllTwentyEightPairsAreBodyHidden()
        {
            foreach(var p in arena.Spawns)Assert.That(spawn.Valid(p,out _),Is.True,"Invalid spawn "+p);
            for(int a=0;a<8;a++)for(int b=a+1;b<8;b++)
            {
                Assert.That(spawn.BodyVisible(arena.Spawns[a],arena.Spawns[b]),Is.False,$"Body LOS {a}:{arena.Spawns[a]} -> {b}:{arena.Spawns[b]}");
                Assert.That(spawn.BodyVisible(arena.Spawns[b],arena.Spawns[a]),Is.False,$"Body LOS {b} -> {a}");
            }
            for(int count=2;count<=8;count++)Assert.That(spawn.TryInitial(NativeMatchRoster.Ffa(count),ProvingProfile.CreateTeamDefault().Get("spawn.initialOpponentSeparation"),out var positions),Is.True,$"FFA {count}: {spawn.InitialFailure}");
            yield return null;
        }
        [UnityTest] public IEnumerator CentralFacadeHasNoHorizontalFloorSeamOpening()
        {
            foreach(int side in new[]{-1,1})for(float x=-8.2f;x<=8.2f;x+=.2f)
                Assert.That(Physics.CheckSphere(new Vector3(x,3.8f,side*8.2f),.02f,1<<ProvingArena.WorldLayer),Is.True,"Open facade seam at "+x+","+side);
            yield return null;
        }
        [UnityTest] public IEnumerator LowerTunnelTraversesEndToEndWithOrdinaryMotor()
        {
            foreach(int direction in new[]{-1,1})foreach(float lane in new[]{-1.5f,0f,1.5f})
            {
                var go=new GameObject("F7-tunnel-motor");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;
                go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,new Vector3(lane,0,-direction*12));
                bool reachedBasement=false;
                for(int tick=0;tick<600;tick++)
                {
                    motor.Tick(new LocalAction{Move=new Vector2(0,direction)},1f/60);Physics.SyncTransforms();
                    if(Mathf.Abs(motor.State.Position.z)<5)reachedBasement|=motor.State.Position.y< -1;
                    if(direction*motor.State.Position.z>12)break;
                }
                Assert.That(reachedBasement,Is.True,"Tunnel descent missing");
                Assert.That(direction*motor.State.Position.z,Is.GreaterThan(12),"Tunnel blocked: "+motor.State.Position);
                Assert.That(motor.State.Position.y,Is.EqualTo(0).Within(.12f));Object.DestroyImmediate(go);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CentralPlinthAndRampUndersidesRejectWalkingEntry()
        {
            foreach(int side in new[]{-1,1})foreach(float x in new[]{7f,10f,13f})
            {
                float z=-side*4;
                var go=new GameObject("F7-under-ramp-probe");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;
                go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();
                motor.Initialize(movement,new Vector3(side*x,0,12));
                for(int tick=0;tick<180;tick++){motor.Tick(new LocalAction{Move=Vector2.down},1f/60);Physics.SyncTransforms();}
                Assert.That(motor.State.Position.z,Is.GreaterThan(z+2),"Entered ramp underside: "+motor.State.Position);
                Object.DestroyImmediate(go);
            }
            foreach(int side in new[]{-1,1})foreach(float z in new[]{-7f,0f,7f})
                Assert.That(Physics.CheckSphere(new Vector3(side*5,1,z),.1f,1<<ProvingArena.WorldLayer),Is.True,"Open central plinth");
            yield return null;
        }
        [UnityTest] public IEnumerator ExteriorCrossStairsSpanAllUsableLanes()
        {
            foreach(int side in new[]{-1,1})foreach(int direction in new[]{-1,1})foreach(float lane in new[]{25f,28f,31f})
            {
                var go=new GameObject("F7-full-width-motor");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;
                go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,new Vector3(direction*17,0,side*lane));
                for(int tick=0;tick<180;tick++)
                {
                    motor.Tick(new LocalAction{Move=new Vector2(direction,0)},1f/60);Physics.SyncTransforms();
                    if(Mathf.Abs(motor.State.Position.x)>29)break;
                }
                Assert.That(Mathf.Abs(motor.State.Position.x),Is.GreaterThan(28.5f),"Blocked outer lane: "+motor.State.Position);
                Assert.That(motor.State.Position.y,Is.EqualTo(4).Within(.12f),"Side bypass at "+motor.State.Position);
                Object.DestroyImmediate(go);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OuterCeilingCavitiesAreSolidUpToShell()
        {
            foreach(var solid in arena.Definition.Solids.Where(s=>s.Id.Contains("slope-ceiling")||s.Id.Contains("cross-rise-ceiling")||s.Id.EndsWith("low-ceiling")||s.Id.Contains("entry-roof")))
            foreach(float along in new[]{-.4f,0,.4f})foreach(float across in new[]{-.4f,0,.4f})
            {
                var underside=solid.Position+solid.Rotation*new Vector3(across*solid.Size.x,-solid.Size.y/2,along*solid.Size.z);
                for(float y=underside.y+.5f;y<8.3f;y+=.5f)
                    Assert.That(Physics.CheckSphere(new Vector3(underside.x,y,underside.z),.04f,1<<ProvingArena.WorldLayer),Is.True,$"Open ceiling cavity {solid.Id} at {underside.x},{y},{underside.z}");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator JumpingDownOuterRisesCannotLandAboveCeiling()
        {
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})foreach(bool cross in new[]{false,true})
            {
                var start=cross?new Vector3(sx*29,4,sz*28):new Vector3(sx*32,4,sz*15);
                var move=cross?new Vector2(-sx,0):new Vector2(0,-sz);
                var go=new GameObject("ceiling-jump-proof");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();
                var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start);
                for(int tick=0;tick<180;tick++)
                {
                    motor.Tick(new LocalAction{Move=move,Jump=tick%30==1},1f/60);Physics.SyncTransforms();
                    var pos=motor.State.Position;float distance=cross?Mathf.Abs(pos.x)-18:Mathf.Abs(pos.z)-4;
                    if(distance>=0&&distance<=10)
                        Assert.That(pos.y,Is.LessThan(3.2f+distance*.48f),$"Motor entered above ceiling: {pos}, cross={cross}");
                }
                Object.DestroyImmediate(go);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator UpperNorthSouthDividersBlockExtraStraightOpenings()
        {
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            foreach(float x in new[]{19f,21f,23f})foreach(float height in new[]{4.5f,5.6f,7.5f})
                Assert.That(Physics.Raycast(new Vector3(sx*x,height,sz*22),Vector3.forward*sz,4,1<<ProvingArena.WorldLayer),Is.True,$"Upper divider opening {sx*x},{sz}, height {height}");
            yield return null;
        }
        [UnityTest] public IEnumerator WideDiagonalsSupportStraightMotorMovementAcrossFullWidth()
        {
            foreach(var solid in arena.Definition.Solids.Where(x=>x.Id.EndsWith("-diagonal")))
            foreach(int direction in new[]{-1,1})foreach(int lane in new[]{-1,0,1})foreach(float speed in new[]{1f,ProvingProfile.CreateCombatDefault().Get("speed.multiplier")})
            {
                var forward=solid.Rotation*Vector3.forward;
                var lateral=solid.Rotation*Vector3.right*(lane*(solid.Size.x/2-movement.Get("player.capsule.radius")-movement.Get("player.capsule.skinWidth")));
                var center=solid.Position+Vector3.up*solid.Size.y/2;
                var start=center-forward*direction*(solid.Size.z/2-1)+lateral;var goal=center+forward*direction*(solid.Size.z/2-1)+lateral;
                var go=new GameObject("straight-wide-diagonal");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;
                go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start);motor.SetHorizontalSpeedMultiplier(speed);
                for(int tick=0;tick<300;tick++)
                {
                    var delta=goal-motor.State.Position;var move=new Vector2(delta.x,delta.z);if(move.magnitude<.2f)break;
                    motor.Tick(new LocalAction{Move=move.normalized},1f/60);Physics.SyncTransforms();
                    Assert.That(motor.State.Position.y,Is.EqualTo(4).Within(.1f),$"Dropped from {solid.Id}, lane={lane}");
                }
                Assert.That(Vector3.Distance(motor.State.Position,goal),Is.LessThan(.3f),$"{solid.Id}, direction={direction}, lane={lane}, speed={speed}, stopped={motor.State.Position}, goal={goal}");
                Object.DestroyImmediate(go);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LowerSpawnFlanksAreOpenWithoutAddedWallsOrLowCeilings()
        {
            foreach(int side in new[]{-1,1})
            {
                var route=new[]{new Vector3(-18*side,0,15*side),new Vector3(-10*side,0,15*side),new Vector3(-6*side,0,19*side)};
                var go=new GameObject("open-spawn-flank-motor");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;
                go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,route[0]);
                foreach(var goal in route.Skip(1))
                {
                    for(int tick=0;tick<300;tick++)
                    {
                        var delta=goal-motor.State.Position;var move=new Vector2(delta.x,delta.z);if(move.magnitude<.2f)break;
                        motor.Tick(new LocalAction{Move=move.normalized},1f/60);Physics.SyncTransforms();
                    }
                    Assert.That(Vector3.Distance(motor.State.Position,goal),Is.LessThan(.3f),$"Lower flank {side}: {motor.State.Position} -> {goal}");
                }
                Object.DestroyImmediate(go);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LowerEntriesKeepStandingCapsuleHeadroomInBothDirections()
        {
            foreach(int side in new[]{-1,1})foreach(int entry in new[]{-1,1})foreach(bool reverse in new[]{false,true})
            {
                var start=new Vector3(entry*15,0,side*(reverse?20:30));var goal=new Vector3(entry*15,0,side*(reverse?30:20));
                var go=new GameObject("entry-headroom-motor");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;
                go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start);
                for(int tick=0;tick<300;tick++)
                {
                    var delta=goal-motor.State.Position;var move=new Vector2(delta.x,delta.z);if(move.magnitude<.2f)break;
                    motor.Tick(new LocalAction{Move=move.normalized},1f/60);Physics.SyncTransforms();
                }
                Assert.That(Vector3.Distance(motor.State.Position,goal),Is.LessThan(.3f),$"Entry {entry},{side}, reverse={reverse}: {motor.State.Position}");Object.DestroyImmediate(go);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OuterCorridorToBalconyAllowsDirectWalkingWithoutDetours()
        {
            var failures=new System.Collections.Generic.List<string>();
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            foreach(bool reverse in new[]{false,true})foreach(float speed in new[]{1f,ProvingProfile.CreateCombatDefault().Get("speed.multiplier")})
            {
                var points=new[]{new Vector3(sx*32,4,sz*17),new Vector3(sx*32,4,sz*26),new Vector3(sx*24,4,sz*18)};
                if(reverse)System.Array.Reverse(points);
                var go=new GameObject("direct-diagonal-motor");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;
                go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,points[0]);motor.SetHorizontalSpeedMultiplier(speed);
                foreach(var goal in points.Skip(1))
                {
                    for(int tick=0;tick<300;tick++)
                    {
                        var delta=goal-motor.State.Position;var horizontal=new Vector2(delta.x,delta.z);
                        if(horizontal.magnitude<.2f)break;
                        motor.Tick(new LocalAction{Move=horizontal.normalized},1f/60);Physics.SyncTransforms();
                    }
                    if(Vector3.Distance(goal,motor.State.Position)>.3f)
                        failures.Add($"corner {sx},{sz}, reverse={reverse}, speed={speed}, goal={goal}, stopped={motor.State.Position}");
                }
                Object.DestroyImmediate(go);
            }
            Assert.That(failures,Is.Empty,string.Join("\n",failures));yield return null;
        }
        [UnityTest] public IEnumerator WideDiagonalsTraverseBothDirectionsAtNormalAndBoostedSpeed()
        {
            var tuning=ProvingProfile.CreateNavigationDefault();var provider=new NativeNavigationProvider(arena,movement,tuning);
            foreach(var solid in arena.Definition.Solids.Where(x=>x.Id.EndsWith("-diagonal")))
            foreach(int direction in new[]{-1,1})foreach(int lane in new[]{-1,0,1})foreach(float speed in new[]{1f,ProvingProfile.CreateCombatDefault().Get("speed.multiplier")})
            {
                var forward=solid.Rotation*Vector3.forward;var lateral=solid.Rotation*Vector3.right*(lane*(solid.Size.x/2-movement.Get("player.capsule.radius")-movement.Get("player.capsule.skinWidth")));
                var center=solid.Position+Vector3.up*solid.Size.y/2;var start=center-forward*direction*(solid.Size.z/2-1)+lateral;var goal=center+forward*direction*(solid.Size.z/2-1)+lateral;
                var go=new GameObject("diagonal-motor");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,start);motor.SetHorizontalSpeedMultiplier(speed);
                var bot=new NativeBotNavigation(provider,tuning);bot.SetStaticGoal(goal);
                for(int tick=0;tick<900&&bot.Status!=NativeNavigationStatus.Arrived;tick++){motor.Tick(bot.Tick(tick/60d,motor.State,new NativeBotKnowledge{Alive=true,OwnLife=1}),1f/60);Physics.SyncTransforms();}
                Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived),$"{solid.Id} direction={direction} lane={lane} speed={speed}: {bot.Capture().Failure} at {motor.State.Position}");
                Assert.That(motor.State.Position.y,Is.EqualTo(4).Within(.1f));Object.DestroyImmediate(go);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BalconyDropLandsOnLowerSupportWithoutDamage()
        {
            var go=new GameObject("drop-player");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,new Vector3(-22.6f,4,0));
            var session=new NativeCombatSession(new[]{motor},arena,Physics.defaultPhysicsScene,movement,ProvingProfile.CreateCombatDefault(),combat);
            for(int tick=0;tick<180;tick++)session.Tick(new[]{new LocalAction{Move=tick<15?Vector2.right:Vector2.zero}},1f/60);
            Assert.That(motor.State.Position.y,Is.EqualTo(0).Within(.1f));Assert.That(motor.State.Grounded,Is.True);Assert.That(session.Life(0).Health,Is.EqualTo(100));Assert.That(session.Life(0).Dead,Is.False);yield return null;
        }
        [UnityTest] public IEnumerator EntireUpperLoopAndBothBridgesAreConnectedWithoutChangingFloor()
        {
            var go=new GameObject("upper-loop-player");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,new Vector3(-25,4,0));
            var tuning=ProvingProfile.CreateNavigationDefault();var provider=new NativeNavigationProvider(arena,movement,tuning);
            foreach(var goal in new[]{new Vector3(-25,4,12),new Vector3(-18,4,21),new Vector3(0,4,21),new Vector3(0,4,0),new Vector3(0,4,-21),new Vector3(18,4,-21),new Vector3(25,4,-12),new Vector3(25,4,12),new Vector3(18,4,21),new Vector3(-18,4,21),new Vector3(-25,4,12),new Vector3(-25,4,0)})
            {
                var bot=new NativeBotNavigation(provider,tuning);bot.SetStaticGoal(goal);
                for(int tick=0;tick<1800&&bot.Status!=NativeNavigationStatus.Arrived;tick++)
                {motor.Tick(bot.Tick(tick/60d,motor.State,new NativeBotKnowledge{Alive=true,OwnLife=1}),1f/60);Physics.SyncTransforms();Assert.That(motor.State.Position.y,Is.GreaterThan(3.8f),$"Upper route to {goal} dropped at {motor.State.Position}; {JsonUtility.ToJson(bot.Capture())}");}
                Assert.That(bot.Status,Is.EqualTo(NativeNavigationStatus.Arrived),$"Upper route {goal}: {bot.Capture().Failure}, pose {motor.State.Position}");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AllVisibleCandidatesStillProduceAValidFallback()
        {
            var live=arena.Spawns.Select((p,i)=>new CombatTarget(i,1,new ParticipantState{Position=p+Vector3.right*movement.Get("player.capsule.radius")*2})).ToArray();
            foreach(var candidate in arena.Spawns)Assert.That(live.Any(x=>spawn.BodyVisible(x.Pose.Position,candidate)),Is.True);
            Assert.That(spawn.TryChoose(live,out var fallback),Is.True);Assert.That(spawn.Valid(fallback-Vector3.up*movement.Get("player.capsule.skinWidth"),out _),Is.True);yield return null;
        }
        [UnityTest] public IEnumerator NearbyHiddenCandidateBeatsFartherVisibleCandidate()
        {
            var definition=AuthoredPhysicsFixture.Freeze().Definition;
            var near=new Vector3(0,0,-4);var far=new Vector3(-10,0,-8);var observer=new Vector3(0,0,-8);
            definition.Spawns=new[]{far,near};
            definition.Solids=definition.Solids.Concat(new[]{new ArenaSolid{Id="hidden-near-screen",Material="wall",Position=new Vector3(0,1.5f,-6),Size=new Vector3(3,3,.4f),Rotation=Quaternion.identity,Layer=ProvingArena.WorldLayer}}).ToArray();
            arena.Build(ArenaFreezeSnapshot.Create(definition,movement),movement);yield return null;
            var selector=new SafeSpawnSelector(Physics.defaultPhysicsScene,arena,movement,combat);
            Assert.That(selector.BodyVisible(observer,near),Is.False);Assert.That(selector.BodyVisible(observer,far),Is.True);
            Assert.That(arena.TryRoute(near,observer,combat.Get("spawn.sample"),out var nearRoute),Is.True);Assert.That(arena.TryRoute(far,observer,combat.Get("spawn.sample"),out var farRoute),Is.True);
            float Length(UnityEngine.AI.NavMeshPath path){float length=0;for(int i=1;i<path.corners.Length;i++)length+=Vector3.Distance(path.corners[i-1],path.corners[i]);return length;}
            Assert.That(Length(nearRoute),Is.LessThan(Length(farRoute)));
            Assert.That(selector.TryChoose(new[]{new CombatTarget(0,1,new ParticipantState{Position=observer})},out var chosen),Is.True);
            Assert.That(Vector3.Distance(chosen,near+Vector3.up*movement.Get("player.capsule.skinWidth")),Is.LessThan(.01f));
        }
        [UnityTest] public IEnumerator PickupInstancesKeepIndependentTimersAndRestoreByStableId()
        {
            CharacterMotor Motor(Vector3 feet)
            {var go=new GameObject("pickup-test-player");go.transform.SetParent(root.transform);go.layer=ProvingArena.ParticipantLayer;go.AddComponent<CharacterController>();var m=go.AddComponent<CharacterMotor>();m.Initialize(movement,feet);return m;}
            var first=Motor(new Vector3(-32,4,-24));var second=Motor(new Vector3(32,4,24));
            var session=new NativeCombatSession(new[]{first,second},arena,Physics.defaultPhysicsScene,movement,ProvingProfile.CreateCombatDefault(),combat);
            first.Initialize(movement,new Vector3(-32,4,-28));session.Tick(new LocalAction[2],1f/60);
            Assert.That(session.ArmorPickups.Single(x=>x.InstanceId=="armor-sw").Available,Is.False);
            Assert.That(session.ArmorPickups.Single(x=>x.InstanceId=="armor-ne").Available,Is.True);
            Assert.That(session.HealPickups.Single(x=>x.InstanceId=="heal-basement").Available,Is.False);
            var snapshot=session.Capture();System.Array.Reverse(snapshot.ArmorPickups);System.Array.Reverse(snapshot.SpeedPickups);
            session.Tick(new LocalAction[2],1f/60);session.Restore(snapshot);
            Assert.That(session.ArmorPickups.Single(x=>x.InstanceId=="armor-sw").RespawnRemaining,Is.EqualTo(snapshot.ArmorPickups.Single(x=>x.InstanceId=="armor-sw").RespawnRemaining));
            var invalid=session.Capture();invalid.ArmorPickups[0].InstanceId=invalid.ArmorPickups[1].InstanceId;
            var before=JsonUtility.ToJson(session.Capture());Assert.Throws<System.ArgumentException>(()=>session.Restore(invalid));Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(before));
            for(int tick=0;tick<601;tick++)session.Tick(new LocalAction[2],1f/60);
            Assert.That(session.SpeedPickups.All(x=>x.Available),Is.True);
            first.Initialize(movement,new Vector3(-32,4,28));session.Tick(new LocalAction[2],1f/60);
            Assert.That(session.SpeedPickups.Single(x=>x.InstanceId=="speed-nw").Available,Is.False);Assert.That(session.SpeedPickups.Single(x=>x.InstanceId=="speed-se").Available,Is.True);
            yield return null;
        }
        [UnityTest] public IEnumerator StationCeilingSealsEveryInteriorAndOuterJunction()
        {
            for(int x=-35;x<=35;x+=2)for(int z=-31;z<=31;z+=2)
                Assert.That(Physics.Raycast(new Vector3(x,8.2f,z),Vector3.up,1,1<<ProvingArena.WorldLayer),Is.True,$"Open roof at {x},{z}");
            yield return null;
        }
        [UnityTest] public IEnumerator GratingSupportsActorAndPassesShotsBothDirectionsWhileHallAndWindowBlock()
        {
            var below=new Vector3(-25,1,0);var above=new Vector3(-25,6,0);
            Assert.That(Physics.Raycast(below,Vector3.up,5,1<<ProvingArena.WorldLayer),Is.False);
            Assert.That(Physics.Raycast(above,Vector3.down,5,1<<ProvingArena.WorldLayer),Is.False);
            Assert.That(Physics.Raycast(above,Vector3.down,out var hit,5,1<<ProvingArena.MovementOnlyLayer),Is.True);Assert.That(hit.collider.name,Is.EqualTo("balcony-west"));
            Assert.That(Physics.Raycast(new Vector3(0,2,0),Vector3.up,3,1<<ProvingArena.WorldLayer),Is.True);
            Assert.That(Physics.Raycast(new Vector3(-32,2,0),Vector3.left,10,1<<ProvingArena.WorldLayer),Is.True);
            var go=new GameObject("grating-motor");go.transform.SetParent(root.transform);go.AddComponent<CharacterController>();var motor=go.AddComponent<CharacterMotor>();motor.Initialize(movement,new Vector3(-25,4,0));
            for(int i=0;i<120;i++)motor.Tick(default,1f/60);Assert.That(motor.State.Position.y,Is.EqualTo(4).Within(.1f));yield return null;
        }
    }
}
