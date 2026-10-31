using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ForgeStationaryComparisonTests
    {
        Scene scene;Gamepad pad;
        [Serializable] public sealed class Result
        {
            public string weapon;public int plasmaVersion,shots,hits;public float distance,damage,nominalInterval,speed;
            public double deathTime,firstShotTime,firstHitTime;public double[] shotTimes;public float targetDisplacement;
        }
        [Serializable] public sealed class Report {public int tickHz=50,health=100,armor=0;public string aim="torso center; shipped rifle spread retained";public Result[] results;}
        [UnityTearDown] public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator StationaryTargetUsesRealCadenceTravelAndSpread()
        {
            var results=new List<Result>();
            foreach(float distance in new[]{5f,10f,20f})foreach(int variant in new[]{0,1,2})
            {
                scene=SceneManager.CreateScene("forge-ttk-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                var owner=new GameObject("stationary native combat");SceneManager.MoveGameObjectToScene(owner,scene);
                var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();var life=ProvingProfile.CreateCombatDefault();
                var plasma=ProvingProfile.CreatePlasmaDefault();
                if(variant==0)plasma=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeForgeCadence",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(plasma,null);
                var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
                var motors=new CharacterMotor[2];for(int i=0;i<2;i++){var go=new GameObject("participant-"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(-8,0,-12+i*distance));}
                var s=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,plasmaProfile:plasma);
                var snap=s.Capture();snap.Lives[0].PlasmaOwned=true;snap.Lives[0].PlasmaAmmo=100;snap.Lives[0].SelectedWeapon=variant==2?WeaponId.Rifle:WeaponId.Plasma;snap.Lives[0].Ammo=variant==2?snap.Lives[0].RifleAmmo:100;
                for(int i=0;i<2;i++){snap.Lives[i].Health=100;snap.Lives[i].Armor=0;}
                snap.Poses[0].Pitch=Mathf.Atan2(move.Get("camera.eyeHeight")-move.Get("player.capsule.height")*combat.Get("zone.torsoY"),distance)*Mathf.Rad2Deg;
                s.Restore(snap);Physics.SyncTransforms();yield return null;
                var result=new Result{weapon=variant==2?"Rifle":variant==0?"FORGE before":"FORGE after",plasmaVersion=plasma.Version,distance=distance,damage=variant==2?combat.Get("rifle.damage"):plasma.Get("plasma.damage"),nominalInterval=variant==2?life.Get("rifle.cooldownSeconds"):plasma.Get("plasma.cooldownSeconds"),speed=variant==2?combat.Get("rifle.speed"):plasma.Get("plasma.speed")};
                var shots=new List<double>();s.ShotResolved+=n=>{if(n.Shooter==0)shots.Add(n.Time);};
                s.Damaged+=n=>{if(n.Participant==1){if(result.hits++==0)result.firstHitTime=n.Time;}};
                var start=s.Pose(1).Position;var actions=new LocalAction[2];actions[0].FireHeld=true;
                for(int tick=0;tick<500&&!s.Life(1).Dead;tick++)s.Tick(actions,.02f);
                Assert.That(s.Life(1).Dead,Is.True,"Stationary target not killed: "+result.weapon+" "+distance);
                result.deathTime=s.Time;result.shots=shots.Count;result.firstShotTime=shots[0];result.shotTimes=shots.ToArray();result.targetDisplacement=Vector3.Distance(start,s.Pose(1).Position);
                Assert.That(result.targetDisplacement,Is.LessThan(.03f));Assert.That(result.hits,Is.EqualTo(variant==2?10:5));
                if(variant!=2)for(int i=1;i<shots.Count;i++)Assert.That(shots[i]-shots[i-1],Is.EqualTo(variant==0?.1:.2).Within(.00001));
                results.Add(result);yield return SceneManager.UnloadSceneAsync(scene);scene=default;
            }
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_FORGE_EVIDENCE");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"ttk.json"),JsonUtility.ToJson(new Report{results=results.ToArray()},true));}
        }
        static void Capture(Camera camera,string path)
        {
            var target=new RenderTexture(1280,720,24);var prior=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=prior;camera.rect=rect;RenderTexture.active=active;UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);}
        }
        [UnityTest] public IEnumerator NativeFiringClipShowsRevisedOrbs()
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_FORGE_EVIDENCE");Assert.That(output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(output);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);Assert.That(ground.Running,Is.True,ground.CombatReviewDiagnostic());ground.enabled=false;
            var s=ground.Session;Assert.That(s.PlasmaProfile.Get("plasma.speed"),Is.EqualTo(14.4f));Assert.That(s.PlasmaProfile.Get("plasma.orbRadius"),Is.EqualTo(.1521f));
            ground.PlaceCombatReviewSeat(0,new Vector3(-6,4,0),0,90);ground.PlaceCombatReviewSeat(1,new Vector3(4,4,0),0,270);
            // Let native gravity settle both participants before fixing equal torso aim.
            for(int tick=0;tick<100;tick++)s.Tick(new LocalAction[s.ParticipantCount],.02f);
            var feet=s.Pose(0).Position;var center=s.Pose(1).Position+Vector3.up*ground.Profile.Get("player.capsule.height")*ground.CombatProfile.Get("zone.torsoY");
            var delta=center-(feet+Vector3.up*ground.Profile.Get("camera.eyeHeight"));
            ground.PlaceCombatReviewSeat(0,feet,-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
            var snapshot=s.Capture();snapshot.Lives[0].PlasmaOwned=true;snapshot.Lives[0].PlasmaAmmo=100;snapshot.Lives[1].Health=100;snapshot.Lives[1].Armor=0;
            var camera=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled&&c.targetTexture==null).OrderBy(c=>c.depth).First();
            foreach(var weapon in new[]{WeaponId.Plasma,WeaponId.Rifle})
            {
                snapshot.Lives[0].SelectedWeapon=weapon;snapshot.Lives[0].Ammo=weapon==WeaponId.Plasma?100:snapshot.Lives[0].RifleAmmo;s.Restore(snapshot);
                string frames=Path.Combine(output,weapon.ToString());Directory.CreateDirectory(frames);
                var actions=new LocalAction[s.ParticipantCount];actions[0].FireHeld=true;
                for(int tick=0;tick<100;tick++)
                {
                    s.Tick(actions,.02f);ground.SendMessage("LateUpdate");yield return null;
                    Capture(camera,Path.Combine(frames,"frame-"+tick.ToString("D3")+".png"));
                }
                File.WriteAllText(Path.Combine(output,"visual-"+weapon+".json"),JsonUtility.ToJson(s.Capture(),true));
                Assert.That(Vector3.Distance(snapshot.Poses[1].Position,s.Pose(1).Position),Is.LessThan(.03f),"Visual target must stand still");
                Assert.That(s.Life(1).Dead,Is.True,"Native visual volley must kill the target: "+weapon);
            }
        }
    }
}
