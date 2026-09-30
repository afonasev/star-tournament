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
        [UnityTearDown] public IEnumerator Cleanup(){if(ground)ground.enabled=true;if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator RetreatActuallyMovesAwayWhileItsReadyRifleDamagesTheVisiblePursuer()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
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
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
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
