using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeAccuracyRuntimeTests
    {
        Scene scene;GameObject owner;ProvingArena arena;CharacterMotor[] motors;
        ProvingProfile move,combat,life;NativeCombatSession session;
        void Setup()
        {
            scene=SceneManager.CreateScene("accuracy-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            owner=new GameObject("accuracy fixture");SceneManager.MoveGameObjectToScene(owner,scene);
            move=ProvingProfile.CreateDefault();combat=ProvingProfile.CreateNativeCombatDefault();life=ProvingProfile.CreateCombatDefault();
            combat.Set("shot.spread",0);combat.Set("rifle.spread",0);
            arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
            // Stable support is required for delayed projectiles: free-falling targets can leave a horizontal rocket ray.
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(owner.transform);floor.layer=ProvingArena.WorldLayer;
            floor.transform.position=new Vector3(0,19.5f,0);floor.transform.localScale=new Vector3(40,1,40);Physics.SyncTransforms();
            motors=new CharacterMotor[4];
            for(int i=0;i<4;i++){var go=new GameObject("p"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(0,20,i*3));}
            var profile=ProvingProfile.CreateMatchDefault();var config=NativeMatchConfiguration.Default(profile);
            var roster=new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB});
            var match=new NativeMatchState(roster,config,profile,50);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,match);EquippedCombatFixture.Equip(session);
        }
        void Tick(params LocalAction[] actions)=>session.Tick(actions,.02f);
        void Select(WeaponSelection weapon)
        {
            var actions=new LocalAction[4];actions[0].SelectWeapon=weapon;Tick(actions);
            for(int i=0;i<51;i++)Tick(new LocalAction[4]);
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}

        [UnityTest]public IEnumerator DelayedRifleHitUpdatesOnceAndHistoricalPendingBulletDoesNotCount()
        {
            Setup();yield return null;motors[1].Initialize(move,new Vector3(8,20,3));Physics.SyncTransforms();Select(WeaponSelection.Rifle);
            var fire=new LocalAction[4];fire[0].Fire=true;Tick(fire);Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).RifleAccuracy.Used,Is.EqualTo(1));
            for(int i=0;i<5;i++)Tick(new LocalAction[4]);
            var hit=session.Match.Read().Standings.Single(r=>r.Seat==0).RifleAccuracy;
            Assert.That(hit.Successful,Is.EqualTo(1));Assert.That(hit.Used,Is.EqualTo(1));

            // Model a pre-accuracy snapshot with an in-flight bullet whose new field defaults false.
            var snapshot=session.Capture();snapshot.RifleBullets=new[]{new RifleBulletState{Id=++snapshot.ShotSequence,Owner=0,OwnerLife=session.Life(0).Life,
                Position=session.Pose(0).Position+Vector3.up*move.Get("camera.eyeHeight"),Direction=Vector3.forward,DamageMultiplier=1,Distance=0}};
            snapshot.ShotCount++;snapshot.Match.Standings=Array.ConvertAll(snapshot.Match.Standings,row=>{row.RifleAccuracy=default;return row;});
            session.Restore(snapshot);for(int i=0;i<5;i++)Tick(new LocalAction[4]);
            var historical=session.Match.Read().Standings.Single(r=>r.Seat==0).RifleAccuracy;
            Assert.That(historical,Is.EqualTo(default(WeaponAccuracy)));
        }

        [UnityTest]public IEnumerator ShotgunCountsEnemyGeometricPelletsAndRocketNeedsAppliedEnemyDamage()
        {
            Setup();yield return null;
            motors[1].Initialize(move,new Vector3(0,20,6));Physics.SyncTransforms();Select(WeaponSelection.Shotgun);
            var fire=new LocalAction[4];fire[0].Fire=true;Tick(fire);
            var shotgun=session.Match.Read().Standings.Single(r=>r.Seat==0).ShotgunAccuracy;
            Assert.That(shotgun.Used,Is.EqualTo(combat.Get("shot.pellets")));
            Assert.That(shotgun.Successful,Is.Zero,"An allied geometric pellet is not an enemy hit");
            Assert.That(session.Life(1).Health,Is.LessThan(100));

            var matchProfile=ProvingProfile.CreateMatchDefault();var matchConfig=NativeMatchConfiguration.Default(matchProfile);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,
                new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}),matchConfig,matchProfile,50));
            EquippedCombatFixture.Equip(session);motors[0].Initialize(move,new Vector3(0,20,0));motors[1].Initialize(move,new Vector3(8,20,3));motors[2].Initialize(move,new Vector3(0,20,6));Physics.SyncTransforms();
            Select(WeaponSelection.Shotgun);fire=new LocalAction[4];fire[0].Fire=true;Tick(fire);
            shotgun=session.Match.Read().Standings.Single(r=>r.Seat==0).ShotgunAccuracy;
            Assert.That(shotgun.Successful,Is.GreaterThan(0));Assert.That(shotgun.Successful,Is.LessThanOrEqualTo(shotgun.Used));

            // Run the rocket case in a fresh match so the shotgun cannot leave the ally dead.
            var profile=ProvingProfile.CreateMatchDefault();var config=NativeMatchConfiguration.Default(profile);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,
                new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}),config,profile,50));
            EquippedCombatFixture.Equip(session);
            Select(WeaponSelection.RocketLauncher);
            motors[0].Initialize(move,new Vector3(0,20,0));motors[1].Initialize(move,new Vector3(0,20,6));motors[2].Initialize(move,new Vector3(8,20,0));motors[3].Initialize(move,new Vector3(8,20,8));Physics.SyncTransforms();
            fire=new LocalAction[4];fire[0].Fire=true;Tick(fire);
            for(int i=0;i<20;i++)Tick(new LocalAction[4]);
            var rocket=session.Match.Read().Standings.Single(r=>r.Seat==0).RocketAccuracy;
            Assert.That(rocket.Used,Is.EqualTo(1));Assert.That(rocket.Successful,Is.Zero,"Friendly damage does not make a rocket successful");
            Assert.That(session.Life(1).Health,Is.LessThan(100));

            profile=ProvingProfile.CreateMatchDefault();config=NativeMatchConfiguration.Default(profile);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,
                new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,NativeTeam.TeamB,NativeTeam.TeamB}),config,profile,50));
            EquippedCombatFixture.Equip(session);Select(WeaponSelection.RocketLauncher);
            motors[0].Initialize(move,new Vector3(0,20,0));motors[1].Initialize(move,new Vector3(8,20,3));motors[2].Initialize(move,new Vector3(0,20,6));motors[3].Initialize(move,new Vector3(1,20,6));Physics.SyncTransforms();
            fire=new LocalAction[4];fire[0].Fire=true;Tick(fire);for(int i=0;i<20;i++)Tick(new LocalAction[4]);
            rocket=session.Match.Read().Standings.Single(r=>r.Seat==0).RocketAccuracy;
            Assert.That(session.Life(2).Health,Is.LessThan(100));Assert.That(session.Life(3).Health,Is.LessThan(100));
            Assert.That(rocket.Used,Is.EqualTo(1));Assert.That(rocket.Successful,Is.EqualTo(1),"One successful launch is counted once across multiple enemy victims");
        }

        [UnityTest]public IEnumerator CutterCountsEnemyAppliedTimeUnionOnceAcrossAllyAndEnemy()
        {
            Setup();yield return null;
            Select(WeaponSelection.Cutter);
            motors[0].Initialize(move,new Vector3(0,20,0));motors[1].Initialize(move,new Vector3(0,20,3));motors[2].Initialize(move,new Vector3(0,20,6));Physics.SyncTransforms();
            var fire=new LocalAction[4];fire[0].FireHeld=true;
            for(int i=0;i<20;i++)Tick(fire);
            var row=session.Match.Read().Standings.Single(r=>r.Seat==0);var cutter=row.CutterAccuracy;
            Assert.That(session.Life(1).Health,Is.LessThan(100));Assert.That(session.Life(2).Health,Is.LessThan(100));
            Assert.That(cutter.Used,Is.GreaterThan(0));Assert.That(cutter.Successful,Is.GreaterThan(0));
            Assert.That(cutter.Successful,Is.LessThan(cutter.Used),"Enemy contact begins after beam growth while ally time is excluded");
        }
    }
}
