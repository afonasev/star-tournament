using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeCombatTests
    {
        Scene scene;
        GameObject owner;
        ProvingArena arena;
        CharacterMotor[] motors;
        ProvingProfile movement, combat, lifecycle;
        NativeCombatSession session;
        void Setup(bool centeredRifle=false)
        {
            scene=SceneManager.CreateScene("native-combat-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            owner=new GameObject("test-session"); SceneManager.MoveGameObjectToScene(owner,scene);
            movement=ProvingProfile.CreateDefault(); combat=ProvingProfile.CreateNativeCombatDefault(); combat.Set("shot.spread",0);
            if(centeredRifle)combat.Set("rifle.spread",0);
            lifecycle=ProvingProfile.CreateCombatDefault(); lifecycle.Set("combat.killcamSeconds",.2f);
            var root=new GameObject("arena"); root.transform.SetParent(owner.transform); arena=root.AddComponent<ProvingArena>(); arena.Build(AuthoredPhysicsFixture.Freeze(),movement);
            motors=new CharacterMotor[4];
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("seat-"+i); go.transform.SetParent(owner.transform); go.layer=ProvingArena.ParticipantLayer;
                motors[i]=go.AddComponent<CharacterMotor>(); motors[i].Initialize(movement,new Vector3(i*3,0,0));
            }
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,lifecycle,combat);
            EquippedCombatFixture.Equip(session);
        }
        void SelectShotgun()
        {
            var actions=new LocalAction[4];actions[0].SelectWeapon=WeaponSelection.Shotgun;
            session.Tick(actions,.02f);
            for(int tick=0;tick<51;tick++)session.Tick(new LocalAction[4],.02f);
            Assert.That(session.Life(0).SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));
        }
        void LethalShotgun()
        {
            combat.Set("shot.damage",100);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,lifecycle,combat);
            EquippedCombatFixture.Equip(session);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(owner) Object.Destroy(owner);
            if(scene.IsValid()) yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest]
        public IEnumerator DamageNoticeAndViewportPulseFollowActualLossAndLifecycle()
        {
            Setup();yield return null;
            var go=new GameObject("test-vignette",typeof(RectTransform),typeof(CanvasRenderer));
            go.transform.SetParent(owner.transform);var effect=go.AddComponent<DamageVignette>();effect.Bind(session,1,movement);
            var other=new GameObject("other-vignette",typeof(RectTransform));other.transform.SetParent(owner.transform);
            var untouched=other.AddComponent<DamageVignette>();untouched.Bind(session,2,movement);
            int notices=0;DamageNotice received=default;session.Damaged+=n=>{notices++;received=n;};
            var saved=session.Capture();var state=saved.Lives[1];state.Armor=20;saved.Lives[1]=state;session.Restore(saved);
            session.ApplyDamage(1,state.Life,10);session.Tick(new LocalAction[4],.04f);effect.Render(true);untouched.Render(true);
            Assert.That(received.ArmorLost,Is.EqualTo(10));Assert.That(received.HealthLost,Is.Zero);
            Assert.That(effect.HealthHit,Is.False);Assert.That(effect.Opacity,Is.GreaterThan(0));Assert.That(untouched.Opacity,Is.Zero);
            float frozen=effect.Opacity;yield return new WaitForSecondsRealtime(.1f);effect.Render(true);Assert.That(effect.Opacity,Is.EqualTo(frozen));
            session.ApplyDamage(1,state.Life-1,10);session.ApplyDamage(1,state.Life,0);Assert.That(notices,Is.EqualTo(1));
            session.ApplyDamage(1,state.Life,15);session.Tick(new LocalAction[4],.04f);effect.Render(true);
            Assert.That(received.ArmorLost,Is.EqualTo(10));Assert.That(received.HealthLost,Is.EqualTo(5));Assert.That(effect.HealthHit,Is.True);
            var mesh=new UnityEngine.UI.VertexHelper();effect.rectTransform.sizeDelta=new Vector2(960,540);
            typeof(DamageVignette).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,new[]{typeof(UnityEngine.UI.VertexHelper)},null).Invoke(effect,new object[]{mesh});
            var vertices=new System.Collections.Generic.List<UnityEngine.UIVertex>();mesh.GetUIVertexStream(vertices);
            Assert.That(vertices.Count,Is.GreaterThan(0));
            Assert.That(vertices.All(v=>Mathf.Abs(v.position.x)>=960*(.5f-movement.Get("ui.damageVignette.width"))-.01f||Mathf.Abs(v.position.y)>=540*(.5f-movement.Get("ui.damageVignette.width"))-.01f),Is.True,"No mesh reaches the protected center");
            Assert.That(vertices.Max(v=>v.color.a)/255f,Is.LessThanOrEqualTo(movement.Get("ui.damageVignette.opacity")+.004f));mesh.Dispose();
            effect.Render(false);Assert.That(effect.Opacity,Is.Zero);effect.Render(true);Assert.That(effect.Opacity,Is.GreaterThan(0));
            session.Restore(saved);effect.Render(true);Assert.That(effect.Opacity,Is.Zero,"Restore is not a hit");
            session.ApplyDamage(1,session.Life(1).Life,200);effect.Render(true);Assert.That(effect.Opacity,Is.Zero,"Death clears the first-person overlay");
            for(int i=0;i<15;i++)session.Tick(new LocalAction[4],.02f);
            effect.Render(true);Assert.That(session.Life(1).Dead,Is.False);Assert.That(effect.Opacity,Is.Zero,"Respawn clears the overlay");
            effect.Bind(null,-1,movement);session.ApplyDamage(1,session.Life(1).Life,5);effect.Render(true);Assert.That(effect.Opacity,Is.Zero);
        }
        [UnityTest]
        public IEnumerator RealShotKillsDisablesCapsuleAndRespawnsFullLife()
        {
            Setup(); yield return null;
            LethalShotgun();
            motors[1].Initialize(movement,new Vector3(0,0,4));
            SelectShotgun();
            int deaths=0; session.Died+=_=>deaths++;
            var actions=new LocalAction[4]; actions[0].SelectWeapon=WeaponSelection.Shotgun; actions[0].FireHeld=true;
            session.Tick(actions,.02f);
            Assert.That(session.Life(1).Dead,Is.True); Assert.That(deaths,Is.EqualTo(1));
            Assert.That(motors[1].GetComponent<CharacterController>().enabled,Is.False);
            Assert.That(session.Life(0).Ammo,Is.EqualTo(19));
            double paused=session.Time; float remaining=(float)session.Life(1).RespawnRemaining;
            session.ClearInput(); yield return new WaitForSecondsRealtime(.25f);
            Assert.That(session.Time,Is.EqualTo(paused)); Assert.That(session.Life(1).RespawnRemaining,Is.EqualTo(remaining).Within(.00001));
            for(int i=0;i<15;i++) session.Tick(actions,.02f);
            Assert.That(session.Life(1).Dead,Is.False); Assert.That(session.Life(1).Life,Is.EqualTo(2));
            Assert.That(session.Life(1).Health,Is.EqualTo(100)); Assert.That(session.Life(1).RifleAmmo,Is.EqualTo(200)); Assert.That(session.Life(1).ShotgunAmmo,Is.Zero);
            Assert.That(motors[1].GetComponent<CharacterController>().enabled,Is.True);
            Assert.That(session.ShotCount,Is.EqualTo(1),"Held trigger cannot shoot again through pause or cooldown");
            Assert.That(session.ApplyDamage(1,1,100,0,1).Applied,Is.Zero,"Old-life damage cannot hit respawn");
        }
        [UnityTest]
        public IEnumerator LtTapKeepsRtShotAndCameraOnTheSameInterpolatedDirection()
        {
            Setup(centeredRifle:true);yield return null;
            var cameras=new Camera[4];var views=new GameObject[4];CombatPresentation presentation=null;
            try
            {
                for(int i=0;i<4;i++)
                {
                    var cameraObject=new GameObject("LT camera "+i);cameraObject.transform.SetParent(owner.transform);cameras[i]=cameraObject.AddComponent<Camera>();
                    views[i]=new GameObject("LT view "+i);views[i].transform.SetParent(owner.transform);
                }
                presentation=new CombatPresentation(session,movement,combat,owner.transform,scene.GetPhysicsScene(),cameras,
                    motors.Select(m=>m.gameObject).ToArray(),views);
                session.Tick(new LocalAction[4],.02f); // Clear the initial physical-fire release gate.
                var aim=new LocalAction[4];aim[0].LookDegrees=new Vector2(27,-34);session.Tick(aim,.02f);
                var tapAndFire=new LocalAction[4];tapAndFire[0]=new LocalAction{ResetLookPitch=true,Fire=true};
                session.Tick(tapAndFire,.02f);presentation.Render();
                var pose=session.Pose(0);var bullet=session.RifleBullets.Single(b=>b.Owner==0);
                Assert.That(session.ShotCount,Is.EqualTo(1),"RT-style fire edge still launches during the LT reset tick");
                Assert.That(pose.Pitch,Is.GreaterThan(0).And.LessThan(34));Assert.That(pose.Yaw,Is.EqualTo(27).Within(.001));
                Assert.That(Vector3.Distance(cameras[0].transform.forward,bullet.Direction),Is.LessThan(.00001f),
                    "the rendered camera and authoritative rifle shot use the same post-LT orientation");
                for(int tick=0;tick<15;tick++){session.Tick(new LocalAction[4],.02f);presentation.Render();}
                Assert.That(session.Pose(0).Pitch,Is.Zero);
                Assert.That(Vector3.Angle(cameras[0].transform.forward,Quaternion.Euler(0,27,0)*Vector3.forward),Is.LessThan(.01f));
            }
            finally{presentation?.Dispose();}
        }
        [UnityTest]
        public IEnumerator CurrentOccupancyBatchAndInvalidFloorAreSafe()
        {
            Setup(); yield return null;
            Assert.That(session.Spawns.Valid(new Vector3(0,2,4),out _),Is.False,"No support between floors");
            Assert.That(session.Spawns.Valid(new Vector3(0,0,4),out _),Is.True);
            motors[0].Initialize(movement,new Vector3(0,0,4));
            Assert.That(session.Spawns.Valid(new Vector3(0,0,4),out _),Is.False,"Live occupancy rejects slot");
            for(int i=0;i<4;i++) session.ApplyDamage(i,1,100);
            for(int i=0;i<12;i++) session.Tick(new LocalAction[4],.02f);
            for(int i=0;i<4;i++)
            {
                Assert.That(session.Life(i).Life,Is.EqualTo(2));
                for(int j=i+1;j<4;j++) Assert.That(Vector3.Distance(session.Pose(i).Position,session.Pose(j).Position),Is.GreaterThan(movement.Get("player.capsule.radius")*2));
                Assert.That(NavMesh.SamplePosition(session.Pose(i).Position,out _,combat.Get("spawn.sample"),NavMesh.AllAreas),Is.True);
            }
        }
        [UnityTest]
        public IEnumerator ScenePhysicsWallBlocksShotAndDeadParticipantDoesNot()
        {
            Setup(); yield return null;
            LethalShotgun();
            motors[1].Initialize(movement,new Vector3(0,0,4));
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(owner.transform);
            wall.layer=ProvingArena.WorldLayer; wall.transform.position=new Vector3(0,1,2); wall.transform.localScale=new Vector3(2,2,.3f); Physics.SyncTransforms();
            SelectShotgun();
            var actions=new LocalAction[4]; actions[0].SelectWeapon=WeaponSelection.Shotgun; actions[0].Fire=true; session.Tick(actions,.02f);
            Assert.That(session.Life(1).Health,Is.EqualTo(100));
            wall.layer=ProvingArena.MovementOnlyLayer;
            for(int i=0;i<40;i++) session.Tick(new LocalAction[4],.02f);
            session.Tick(actions,.02f); Assert.That(session.Life(1).Dead,Is.True,"Movement-only collider does not stop pellets");
        }
        [UnityTest]
        public IEnumerator RespawnPrefersHiddenUpperFloorAndRechecksOccupiedWinner()
        {
            Setup(); yield return null;
            // Keep one live opponent under the slab. Other capsules cannot affect candidates.
            for(int i=1;i<4;i++) session.ApplyDamage(i,1,100);
            motors[0].Initialize(movement,new Vector3(0,0,4));
            Assert.That(session.Spawns.TryChoose(session.LiveTargets(),out var first),Is.True);
            Assert.That(first.y,Is.EqualTo(AuthoredPhysicsFixture.Value("fixture.upperFloorHeight")+movement.Get("player.capsule.skinWidth")).Within(.01f));
            Vector3 origin=motors[0].State.Position+Vector3.up*movement.Get("camera.eyeHeight");
            Vector3 ray=first+Vector3.up*movement.Get("camera.eyeHeight")-origin;
            Assert.That(scene.GetPhysicsScene().Raycast(origin,ray.normalized,out _,ray.magnitude,1<<ProvingArena.WorldLayer),Is.True,"Slab occludes chosen spawn");
            Assert.That(arena.TryRoute(first,motors[0].State.Position,combat.Get("spawn.sample"),out var route),Is.True);
            Assert.That(route.corners.Length,Is.GreaterThan(2),"Navigation goes through a real floor transition");
            motors[1].Initialize(movement,first); // Physically reserve the previous best slot now.
            Assert.That(session.Spawns.TryChoose(session.LiveTargets(),out var second),Is.True);
            Assert.That(Vector3.Distance(first,second),Is.GreaterThan(movement.Get("player.capsule.radius")*2));
        }
        [UnityTest]
        public IEnumerator MixedPelletsApplyProportionalDamageThroughSession()
        {
            Setup(); yield return null;
            combat.Set("shot.spread",7);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,lifecycle,combat);
            EquippedCombatFixture.Equip(session);
            motors[1].Initialize(movement,new Vector3(0,0,4));
            SelectShotgun();
            var actions=new LocalAction[4]; actions[0].SelectWeapon=WeaponSelection.Shotgun; actions[0].Fire=true;
            session.Tick(actions,.02f);
            // Fixed sampling identity1 with18 pellets and explicit7-degree fixture spread:10 rays hit body zones.
            // Expected damage =10*85/18; misses contribute nothing.
            Assert.That(session.Life(1).Health,Is.EqualTo(100f-850f/18).Within(.01f));
            Assert.That(session.Life(0).Ammo,Is.EqualTo(19));
        }
        [UnityTest]
        public IEnumerator ShotNoticeCapturesEveryResolvedEndpointWithoutChangingDamage()
        {
            Setup();yield return null;combat.Set("shot.spread",0);combat.Set("shot.damage",100);session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,lifecycle,combat);
            EquippedCombatFixture.Equip(session);
            motors[1].Initialize(movement,new Vector3(0,0,4));ShotNotice notice=default;int count=0;session.ShotResolved+=n=>{notice=n;count++;};
            SelectShotgun();
            session.Tick(new[]{new LocalAction{SelectWeapon=WeaponSelection.Shotgun,Fire=true},default,default,default},.02f);
            Assert.That(count,Is.EqualTo(1));Assert.That(notice.Pellets.Length,Is.EqualTo((int)combat.Get("shot.pellets")));
            Assert.That(notice.Pellets[0].Contact,Is.EqualTo(PelletContact.Participant));Assert.That(notice.Pellets[0].TargetSeat,Is.EqualTo(1));
            Assert.That(session.Life(1).Dead,Is.True);
        }
        [UnityTest]
        public IEnumerator RifleHoldAndSameTickSelectionUseDistinctAuthoritativeShots()
        {
            Setup();yield return null;
            combat.Set("rifle.spread",0);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,lifecycle,combat);
            EquippedCombatFixture.Equip(session);
            motors[1].Initialize(movement,new Vector3(0,0,4));
            ShotNotice notice=default;session.ShotResolved+=n=>notice=n;
            var actions=new LocalAction[4];actions[0].FireHeld=true;
            session.Tick(actions,.02f);
            Assert.That(notice.Weapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(notice.Pellets,Is.Empty,"Launch does not resolve a future endpoint");
            Assert.That(session.Life(0).RifleAmmo,Is.EqualTo(199));
            Assert.That(session.Life(1).Health,Is.EqualTo(100),"No launch damage");
            for(int tick=0;tick<7;tick++)session.Tick(actions,.02f);
            Assert.That(session.Life(1).Health,Is.LessThan(100),"Arriving bullets apply damage");
            Assert.That(session.ShotCount,Is.GreaterThan(1),"Held rifle fire repeats on its own cadence");
            int before=session.ShotCount;
            actions[0].SelectWeapon=WeaponSelection.Shotgun;session.Tick(actions,.02f);
            Assert.That(session.Life(0).SelectedWeapon,Is.EqualTo(WeaponId.Rifle));
            Assert.That(session.ShotCount,Is.EqualTo(before),"Switching while held does not edge-fire shotgun");
            for(int tick=0;tick<51;tick++)session.Tick(actions,.02f);
            Assert.That(session.Life(0).SelectedWeapon,Is.EqualTo(WeaponId.Shotgun));
            Assert.That(session.ShotCount,Is.EqualTo(before),"Held trigger cannot fire during the switch");
            actions[0]=default;session.Tick(actions,.02f);
            actions[0]=new LocalAction{Fire=true,SelectWeapon=WeaponSelection.Shotgun};session.Tick(actions,.02f);
            Assert.That(session.ShotCount,Is.EqualTo(before+1));
            Assert.That(notice.Weapon,Is.EqualTo(WeaponId.Shotgun));
            Assert.That(session.Life(0).ShotgunAmmo,Is.EqualTo(19));
        }
        [UnityTest]
        public IEnumerator FreshPressImmediatelyAfterRespawnIsAccepted()
        {
            Setup(); yield return null;
            session.ApplyDamage(0,1,100);
            while(session.Life(0).Dead) session.Tick(new LocalAction[4],.02f);
            var actions=new LocalAction[4]; actions[0].Fire=true; actions[0].FireHeld=true;
            session.Tick(actions,.02f);
            Assert.That(session.ShotCount,Is.EqualTo(1));
        }
        [UnityTest]
        public IEnumerator KillcamTracksOnlyKillerLifeAndAirborneCorpseFindsFloor()
        {
            Setup(); yield return null;
            var cameras=new Camera[4]; var bodies=new GameObject[4]; var views=new GameObject[4];
            for(int i=0;i<4;i++)
            {
                var cameraRoot=new GameObject("camera");cameraRoot.transform.SetParent(owner.transform);cameras[i]=cameraRoot.AddComponent<Camera>();
                bodies[i]=GameObject.CreatePrimitive(PrimitiveType.Cube); bodies[i].transform.SetParent(owner.transform); Object.Destroy(bodies[i].GetComponent<Collider>());
                views[i]=new GameObject("view"); views[i].transform.SetParent(owner.transform);
            }
            var presentation=new CombatPresentation(session,movement,combat,owner.transform,scene.GetPhysicsScene(),cameras,bodies,views);
            motors[1].Initialize(movement,new Vector3(0,2,4));
            session.ApplyDamage(1,1,100,0,1); presentation.Render();
            var corpse=owner.transform.Find("corpse-2-life-1");
            Assert.That(corpse.GetComponent<Renderer>().bounds.min.y,Is.EqualTo(0).Within(.01f));
            Assert.That(bodies[1].activeSelf,Is.False); Assert.That(views[1].activeSelf,Is.False);
            Vector3 anchor=cameras[1].transform.position;
            Assert.That(Vector3.Distance(anchor,session.Pose(1).Position+Vector3.up*movement.Get("camera.eyeHeight")),Is.LessThan(combat.Get("killcam.clipRadius")*2f));
            motors[0].Initialize(movement,new Vector3(4,0,0)); presentation.Render();
            Vector3 expected=(session.Pose(0).Position+Vector3.up*movement.Get("camera.eyeHeight")-cameras[1].transform.position).normalized;
            Assert.That(Vector3.Angle(cameras[1].transform.forward,expected),Is.LessThan(.01f));
            session.Tick(new LocalAction[4],.1f);presentation.Render();
            Assert.That(Vector3.Distance(cameras[1].transform.position,anchor),Is.GreaterThan(.001f));
            session.ApplyDamage(0,1,100); presentation.Render();
            Quaternion frozen=cameras[1].transform.rotation;
            Vector3 frozenPosition=cameras[1].transform.position;
            // Change transform after killer death; tracking must retain that life's final point.
            motors[0].Initialize(movement,new Vector3(-8,0,0)); presentation.Render();
            Assert.That(Quaternion.Angle(frozen,cameras[1].transform.rotation),Is.LessThan(.01f));
            Assert.That(cameras[1].transform.position,Is.EqualTo(frozenPosition));
            session.ApplyDamage(2,1,100); presentation.Render();
            Assert.That(presentation.DeathMessage(2),Does.StartWith("Вы погибли"));
        }
        [UnityTest]
        public IEnumerator AdditiveReloadOwnsAllRuntimeObjects()
        {
            int events=Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length;
            int listeners=Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
            for(int repeat=0;repeat<2;repeat++)
            {
                yield return NativeLoadingTestScene.Load();
                scene=SceneManager.GetSceneByName("ProvingGround"); yield return null;
                var ground=Array.Find(Object.FindObjectsByType<ProvingGround>(FindObjectsSortMode.None),g=>g.gameObject.scene==scene);
                Assert.That(ground,Is.Not.Null);
                Assert.That(ground.GetComponentsInChildren<Camera>().Length,Is.EqualTo(4));
                Assert.That(ground.GetComponentsInChildren<AudioListener>().Length,Is.EqualTo(1));
                Assert.That(ground.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>().Length,Is.EqualTo(1));
                ground.Session.ApplyDamage(1,1,100,0,1); yield return null;
                Assert.That(ground.transform.Find("corpse-2-life-1"),Is.Not.Null);
                yield return SceneManager.UnloadSceneAsync(scene); scene=default; yield return null;
                Assert.That(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length,Is.EqualTo(events));
                Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length,Is.EqualTo(listeners));
                Assert.That(GameObject.Find("corpse-2-life-1"),Is.Null);
            }
        }
    }
}
