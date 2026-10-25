using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public class NativeBotTacticsRuntimeTests
    {
        Scene scene;ProvingGround ground;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            // A scene owns a real background Lab read; let its existing readiness contract
            // complete before domain shutdown, rather than leaving Unity serialization on a worker.
            if(ground){yield return NativeLoadingTestScene.WaitLabHistory(ground);ground.enabled=true;}
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator RearRifleHitTurnsBotAndReacquiresBeforeReturningFire()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            ground.StartBotReview(NativeBotTacticsReview.Composition(false,NativeBotDifficulty.Hard,NativeBotDifficulty.Normal,0),271029);ground.enabled=false;
            float floor=AuthoredPhysicsFixture.Value("fixture.floorHeight");
            var own=new Vector3(-8,floor,26);var shooter=new Vector3(-8,floor,30);
            ground.PlaceCombatReviewSeat(0,own,0,180);
            float pitch=-Mathf.Atan2(ground.Profile.Get("player.capsule.height")*ground.CombatProfile.Get("zone.torsoY")-ground.Profile.Get("camera.eyeHeight"),4)*Mathf.Rad2Deg;
            ground.PlaceCombatReviewSeat(1,shooter,pitch,180);
            ground.PlaceCombatReviewSeat(2,new Vector3(-30,floor,-30),0);ground.PlaceCombatReviewSeat(3,new Vector3(30,floor,-30),0);Physics.SyncTransforms();
            var session=ground.Session;var driver=ground.BotDriver;var actions=new LocalAction[4];float dt=1/ground.Profile.Get("simulation.fixedTickHz");
            var armored=session.Capture();armored.Lives[0].Armor=ground.LifeProfile.Get("armor.maximum");session.Restore(armored);
            var origin=shooter+Vector3.up*ground.Profile.Get("camera.eyeHeight");
            var target=own+Vector3.up*ground.Profile.Get("player.capsule.height")*ground.CombatProfile.Get("zone.torsoY");var ray=target-origin;
            Assert.That(scene.GetPhysicsScene().Raycast(origin,ray.normalized,out _,ray.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore),Is.False,"Controlled rear-hit lane must have clear world LOS");
            // Setup/restore deliberately requires an actual released tick before the next fire edge.
            session.Tick(actions,dt);
            int hits=0;bool firstHitUnknown=false,firstHitArmorOnly=false,reacquired=false,returnedFire=false;
            session.Damaged+=hit=>{if(hit.Participant==0){hits++;if(hits==1){firstHitUnknown=!driver.Perception.Read(0).Enemies.Any(e=>e.Sighting.Participant==1);firstHitArmorOnly=hit.ArmorLost>0&&hit.HealthLost==0;}}};
            driver.ProduceActions(actions,dt);Assert.That(driver.Perception.Read(0).Enemies.Any(e=>e.Sighting.Participant==1),Is.False,"Shooter begins outside FOV");
            // Preserve the controlled starting position until the physical bullet arrives.
            Array.Clear(actions,0,4);actions[1]=new LocalAction{Fire=true};session.Tick(actions,dt);
            Assert.That(session.ShotCount,Is.GreaterThan(0),"The real fire edge must launch a bullet after setup release");
            while(session.Time<2.5)
            {
                Array.Clear(actions,0,4);driver.ProduceActions(actions,dt);
                var knowledge=driver.Perception.Read(0);bool sees=knowledge.Enemies.Any(e=>e.Sighting.Participant==1&&e.Visible);
                reacquired|=hits>0&&sees;
                if(actions[0].Fire||actions[0].FireHeld){Assert.That(sees,Is.True,"No return fire before ordinary observation");returnedFire|=hits>0;}
                for(int i=1;i<4;i++)actions[i]=default;
                // Until contact the test controls motion only; all turn/knowledge actions after contact are real.
                if(hits==0)actions[0]=default;
                session.Tick(actions,dt);if(returnedFire)break;
            }
            Assert.That(hits,Is.GreaterThan(0),"Actual rifle resolver must hit the back of the native participant");
            Assert.That(firstHitUnknown,Is.True);Assert.That(firstHitArmorOnly,Is.True,"Shield-only damage must also trigger the reaction");Assert.That(reacquired,Is.True);Assert.That(returnedFire,Is.True);
            Assert.That(session.ShotCount,Is.GreaterThan(1),"The return action must actually launch the bot weapon through the native session");
            Assert.That(driver.Planner(0).Capture().DamageLife,Is.Zero,"Visual confirmation consumes directional cue");
            Debug.Log($"REAR_DAMAGE_RESPONSE hits={hits} reacquired={reacquired} returnedFire={returnedFire} seconds={session.Time:F2}");
        }
        [UnityTest] public IEnumerator VeteranPlannerJumpUsesNativeMotorAndLostTargetDoesNotStopSearch()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            ground.StartBotReview(NativeBotTacticsReview.Composition(false,NativeBotDifficulty.Hard,NativeBotDifficulty.Normal,0),271021);ground.enabled=false;
            Assert.That(ground.BotBehaviorProfile.Get("bots.easy.jumpChance"),Is.Zero);
            Assert.That(ground.BotBehaviorProfile.Get("bots.normal.jumpChance"),Is.EqualTo(.3f));
            Assert.That(ground.BotBehaviorProfile.Get("bots.hard.jumpChance"),Is.EqualTo(.55f),"Packaged legacy Default must adopt the behavior update");
            var arena=ground.GetComponentsInChildren<ProvingArena>().Single();var behavior=ProvingProfile.CreateBotBehaviorDefault();
            behavior.Set("bots.hard.jumpChance",1);behavior.Set("bots.hard.surpriseJumpChance",1);behavior.Set("bots.cooperation.personalitySpread",0);
            var routes=new NativeNavigationProvider(arena,ground.Profile,ground.BotNavigationProfile);
            var tactics=new NativeBotTactics(arena,scene.GetPhysicsScene(),routes,ground.Profile,ground.CombatProfile,behavior);
            var planner=new NativeBotPlanner(NativeBotDifficulty.Hard,271021,behavior,ground.BotNavigationProfile,routes,tactics,new NativeShotgunPolicy(ground.Profile,ground.CombatProfile),100);
            var start=new Vector3(19,0,8);var actor=new GameObject("planner-jump-proof");actor.transform.SetParent(ground.transform);actor.layer=ProvingArena.ParticipantLayer;
            actor.AddComponent<CharacterController>();var motor=actor.AddComponent<CharacterMotor>();motor.Initialize(ground.Profile,start);
            float dt=1/ground.Profile.Get("simulation.fixedTickHz");motor.Tick(default,dt);Physics.SyncTransforms();
            var life=new CombatLife("bot",ProvingProfile.CreateCombatDefault()).Read();
            var frame=new NativeBotFrame{Pose=motor.State,Life=life,Knowledge=new NativeBotKnowledge{OwnLife=life.Life,Alive=true,Enemies=new[]{new NativeBotMemoryEntry{Sighting=new NativeBotSighting(1,1,start+Vector3.forward*4),Visible=true}}}};
            double time=0;bool jumped=false,airborne=false,landed=false;
            for(int i=0;i<300;i++)
            {
                frame.Pose=motor.State;var action=planner.Tick(time,dt,frame);jumped|=action.Jump;motor.Tick(action,dt);Physics.SyncTransforms();time+=dt;
                airborne|=jumped&&!motor.State.Grounded&&motor.State.Position.y>start.y+.01f;
                if(airborne&&motor.State.Grounded){landed=true;break;}
            }
            Assert.That(jumped&&airborne&&landed,Is.True,"Planner must produce a safe physical airborne/landing cycle");
            frame.Pose=motor.State;frame.Knowledge.Enemies[0].Visible=false;frame.Knowledge.Enemies[0].Sighting.Position=motor.State.Position;
            var search=planner.Tick(time,dt,frame);Assert.That(planner.Intent,Is.EqualTo(NativeBotIntent.Search));Assert.That(search.Move.sqrMagnitude,Is.GreaterThan(0));
            var before=motor.State.Position;for(int i=0;i<30;i++){motor.Tick(search,dt);Physics.SyncTransforms();}
            Assert.That(Vector3.Distance(before,motor.State.Position),Is.GreaterThan(.1f));UnityEngine.Object.Destroy(actor);
        }
        [UnityTest] public IEnumerator RetreatActuallyMovesAwayWhileItsReadyRifleDamagesTheVisiblePursuer()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            ground.StartBotReview(NativeBotTacticsReview.Composition(false,NativeBotDifficulty.Hard,NativeBotDifficulty.Normal,0),271019);ground.enabled=false;
            float floor=AuthoredPhysicsFixture.Value("fixture.floorHeight");var start=new Vector3(-8,floor,26);var threat=new Vector3(-8,floor,30);
            ground.PlaceCombatReviewSeat(0,start,0,0);ground.PlaceCombatReviewSeat(1,threat,0,180);
            ground.PlaceCombatReviewSeat(2,new Vector3(-30,floor,-30),0);ground.PlaceCombatReviewSeat(3,new Vector3(30,floor,-30),0);
            var session=ground.Session;session.ApplyDamage(0,session.Life(0).Life,90);Physics.SyncTransforms();
            var origin=start+Vector3.up*ground.Profile.Get("camera.eyeHeight");
            var point=threat+Vector3.up*ground.Profile.Get("player.capsule.height")*ground.CombatProfile.Get("zone.torsoY");
            var ray=point-origin;
            Assert.That(scene.GetPhysicsScene().Raycast(origin,ray.normalized,out var obstruction,ray.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore),Is.False,"This visible-target fixture requires a genuinely clear world LOS");
            var arena=ground.GetComponentsInChildren<ProvingArena>().Single();
            var routes=new NativeNavigationProvider(arena,ground.Profile,ground.BotNavigationProfile);
            var tactics=new NativeBotTactics(arena,scene.GetPhysicsScene(),routes,ground.Profile,ground.CombatProfile,ground.BotBehaviorProfile);
            var planner=new NativeBotPlanner(NativeBotDifficulty.Hard,271019,ground.BotBehaviorProfile,ground.BotNavigationProfile,routes,tactics,new NativeShotgunPolicy(ground.Profile,ground.CombatProfile,ground.CutterProfile,ground.LifeProfile),100);
            int retreatShots=0;float dt=1/ground.Profile.Get("simulation.fixedTickHz");
            for(int i=0;i<50;i++)
            {
                var own=session.Life(0);var rival=session.Life(1);var pose=session.Pose(0);
                var sighting=new NativeBotSighting(1,rival.Life,session.Pose(1).Position);
                var frame=new NativeBotFrame{Pose=pose,Life=own,Knowledge=new NativeBotKnowledge{OwnLife=own.Life,Alive=!own.Dead,Enemies=new[]{new NativeBotMemoryEntry{Sighting=sighting,ObservedAt=session.Time,Visible=!rival.Dead}}},Allies=Array.Empty<NativeBotAlly>()};
                var action=planner.Tick(session.Time,dt,frame);
                if(planner.Intent==NativeBotIntent.Retreat&&action.Fire)retreatShots++;
                var actions=new LocalAction[4];actions[0]=action;session.Tick(actions,dt);
            }
            Assert.That(retreatShots,Is.GreaterThan(0),"A retreat must produce actual ready fire actions");
            Assert.That(session.Life(1).Health,Is.LessThan(100),"Those actions must damage the pursuer through the native weapon resolver");
            Assert.That(Vector3.Dot(session.Pose(0).Position-start,(start-threat).normalized),Is.GreaterThan(.1f),"Native motor must still make retreat progress");
            Assert.That(Mathf.Abs(session.Pose(0).Position.x-start.x),Is.GreaterThan(.1f),"Native retreat has a lateral component");
        }
        [UnityTest] public IEnumerator NativeActionsActuallyUnlockWeaponsCollectBonusesAndRestoreWithoutInstantKnowledge()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            ground.StartBotReview(NativeBotTacticsReview.Composition(false,NativeBotDifficulty.Hard,NativeBotDifficulty.Normal,0),271001);ground.enabled=false;
            var session=ground.Session;var driver=ground.BotDriver;float dt=1/ground.Profile.Get("simulation.fixedTickHz");var actions=new LocalAction[4];int weapons=0,bonuses=0,damage=0;
            session.PickupCollected+=(p,id,kind)=>{if(kind==NativeBotPickupKind.Weapon)weapons++;else bonuses++;};session.Fired+=(p,d)=>{if(d>0)damage++;};session.RifleBulletHit+=e=>{if(e.AppliedDamage>0)damage++;};
            driver.ProduceActions(actions,dt);for(int p=0;p<4;p++)Assert.That(driver.Pickups.Read(p),Is.Empty,"Bootstrap obeys delay");session.Tick(actions,dt);
            var saved=driver.Capture();var corrupt=driver.Capture();corrupt.Pickups.Pending[0].DeliverAt=0;string before=JsonUtility.ToJson(driver.Capture());Assert.Throws<ArgumentException>(()=>driver.Restore(corrupt));Assert.That(JsonUtility.ToJson(driver.Capture()),Is.EqualTo(before));driver.Restore(saved);
            int ticks=0;while(session.Time<90&&session.Match.Phase!=NativeMatchPhase.Finished)
            {Array.Clear(actions,0,4);driver.ProduceActions(actions,dt);session.Tick(actions,dt);if(++ticks%500==0)yield return null;}
            Assert.That(weapons,Is.GreaterThan(0),"Actual physical weapon collection required");Assert.That(bonuses,Is.GreaterThan(0),"Actual useful bonus collection required");Assert.That(damage,Is.GreaterThan(0),"Actual combat outcomes required");
            Assert.That(Enumerable.Range(0,4).Sum(p=>driver.Planner(p).UnlockDecisions),Is.GreaterThan(0),"Purposeful weapon objectives required");
            UnityEngine.Debug.Log($"BOT_TACTICS_NATIVE_OUTCOMES weapons={weapons} bonuses={bonuses} damagingActions={damage}");
        }
    }
}
