using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class FiringRangePlayTests
    {
        Scene scene;Gamepad[] pads=Array.Empty<Gamepad>();
        string priorMap;
        [SetUp] public void RememberMap(){priorMap=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey,CombatBowlCatalog.Id);}
        [UnityTearDown] public IEnumerator Cleanup(){PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,priorMap);foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator NativeShotsPassiveTargetsExactRespawnAndRestore()
        {
            scene=SceneManager.CreateScene("range-contract-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("range");SceneManager.MoveGameObjectToScene(owner,scene);
            var move=ProvingProfile.CreateDefault();var life=ProvingProfile.CreateCombatDefault();var combat=ProvingProfile.CreateNativeCombatDefault();
            var definition=FiringRangeCatalog.Build();var arena=owner.AddComponent<ProvingArena>();arena.Build(ArenaFreezeSnapshot.Create(definition,move),move);
            var positions=FiringRangeCatalog.Positions(definition,1);var motors=new CharacterMotor[positions.Length];
            for(int i=0;i<motors.Length;i++){var go=new GameObject("actor-"+i);go.transform.SetParent(owner.transform);go.layer=ProvingArena.ParticipantLayer;motors[i]=go.AddComponent<CharacterMotor>();motors[i].Initialize(move,positions[i]);}
            var s=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);var actions=new LocalAction[motors.Length];
            Assert.That(s.Training,Is.True);Assert.That(s.Match,Is.Null);AllWeapons(s.Life(0));
            var snap=s.Capture();snap.Poses[0].Position=new Vector3(-8,0,-13);snap.Poses[0].Pitch=Mathf.Atan2(move.Get("camera.eyeHeight")-move.Get("player.capsule.height")*combat.Get("zone.torsoY"),5)*Mathf.Rad2Deg;s.Restore(snap);
            int shots=0;s.Fired+=(p,damage)=>shots++;
            // Even hostile injected actions cannot animate/fire/move a training target.
            actions[1]=new LocalAction{FireHeld=true,Fire=true,Move=Vector2.one};actions[0].FireHeld=true;
            var original=s.Pose(1);for(int tick=0;tick<300&&!s.Life(1).Dead;tick++)s.Tick(actions,.02f);
            Assert.That(shots,Is.GreaterThan(0));Assert.That(s.Life(1).Dead,Is.True,"Real rifle must kill the target");
            Assert.That(s.Pose(1).Position,Is.EqualTo(original.Position));Assert.That(s.Life(1).RifleAmmo,Is.EqualTo((int)life.Get("rifle.startingAmmo")));
            var dead=s.Capture();int previousLife=s.Life(1).Life;double remaining=s.Life(1).RespawnRemaining;
            Assert.That(remaining,Is.EqualTo(life.Get("combat.killcamSeconds")).Within(.00001));
            Array.Clear(actions,0,actions.Length);s.Tick(actions,(float)remaining-.02f);Assert.That(s.Life(1).Dead,Is.True);
            s.Tick(actions,.02f);Assert.That(s.Life(1).Dead,Is.False);Assert.That(s.Life(1).Life,Is.EqualTo(previousLife+1));
            Assert.That(s.Pose(1).Position,Is.EqualTo(definition.TrainingTargets[0]));Assert.That(s.Pose(1).Yaw,Is.EqualTo(180));
            s.Restore(dead);Assert.That(s.Life(1).RespawnRemaining,Is.EqualTo(remaining));s.Tick(actions,(float)remaining-.02f);Assert.That(s.Life(1).Dead,Is.True);s.Tick(actions,.02f);Assert.That(s.Life(1).Dead,Is.False);
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                var weaponState=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(snap));weaponState.Lives[0].SelectedWeapon=weapon;
                weaponState.Lives[0].Ammo=CombatLife.AmmoFor(weaponState.Lives[0],weapon);s.Restore(weaponState);
                Array.Clear(actions,0,actions.Length);actions[0].Fire=actions[0].FireHeld=true;
                for(int tick=0;tick<500&&!s.Life(1).Dead&&!s.Life(0).Dead;tick++)
                {
                    bool semi=weapon==WeaponId.Shotgun||weapon==WeaponId.RocketLauncher;
                    actions[0].Fire=semi&&tick%2==0;actions[0].FireHeld=!semi||tick%2==0;s.Tick(actions,.02f);
                }
                Assert.That(s.Life(1).Dead,Is.True,"Real damage path: "+weapon);
            }
            Array.Clear(actions,0,actions.Length);
            s.ApplyDamage(0,s.Life(0).Life,10000);s.Tick(actions,life.Get("combat.killcamSeconds"));AllWeapons(s.Life(0));
            yield return null;
        }
        static void AllWeapons(CombatLifeState life){foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))Assert.That(CombatLife.Owned(life,weapon),Is.True,weapon.ToString());Assert.That(life.RifleAmmo,Is.GreaterThan(0));Assert.That(life.PlasmaAmmo,Is.GreaterThan(0));}
        [UnityTest] public IEnumerator OneAndFourHumansMenuPauseRepeatAndFrames()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
            Button("main-action-0").onClick.Invoke();yield return null;
            foreach(int humans in new[]{1,4})
            {
                NativeSetupFixture.UnboundHumans(ground,humans);
                Button("lobby-map").onClick.Invoke();yield return null;yield return null;
                var rangeCard=Button("lobby-map-"+FiringRangeCatalog.Id);Assert.That(rangeCard.gameObject.activeInHierarchy,Is.True,"Range must be reachable in the ordinary map picker");
                rangeCard.onClick.Invoke();Assert.That(ground.SelectedMapId,Is.EqualTo(FiringRangeCatalog.Id));
                ground.StartCombatReview(pads.Take(humans).ToArray(),true);yield return NativeLoadingTestScene.Wait(ground);yield return null;
                Assert.That(ground.Running,Is.True,ground.CombatReviewDiagnostic());Assert.That(ground.Composition.LocalCount,Is.EqualTo(humans));
                Assert.That(ground.Session.ParticipantCount,Is.EqualTo(humans+12));Assert.That(ground.BotDriver,Is.Null);Assert.That(ground.Session.Match,Is.Null);
                for(int p=0;p<humans;p++)AllWeapons(ground.Session.Life(p));
                var cameras=ground.GetComponentsInChildren<Camera>().ToArray();Assert.That(cameras.Length,Is.EqualTo(humans));
                var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RANGE_EVIDENCE");
                if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);yield return Capture(cameras[0],Path.Combine(output,"range-"+humans+"-first-person.png"));}
                var before=ground.Session;var frozen=JsonUtility.ToJson(ground.Composition.Read());
                ground.Session.ApplyDamage(humans,ground.Session.Life(humans).Life,10000);ground.EnableNativeInputReview();ground.OpenPauseReviewSeat(0);double timer=ground.Session.Life(humans).RespawnRemaining;
                yield return new WaitForSeconds(.1f);Assert.That(ground.Session.Life(humans).RespawnRemaining,Is.EqualTo(timer));ground.ResumeNativeInputReview();
                Button("Повторить матч").onClick.Invoke();yield return NativeLoadingTestScene.Wait(ground);yield return null;
                Assert.That(ground.Session,Is.Not.SameAs(before));Assert.That(JsonUtility.ToJson(ground.Composition.Read()),Is.EqualTo(frozen));Assert.That(ground.Session.Life(humans).Dead,Is.False);
                if(humans==1){Button("В главное меню").onClick.Invoke();yield return null;Button("main-action-0").onClick.Invoke();yield return null;}
            }
        }
        [UnityTest] public IEnumerator OptimizationPreservesProjection()
        {
            scene=SceneManager.CreateScene("range-optimization-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var root=new GameObject("full-range");SceneManager.MoveGameObjectToScene(root,scene);var arena=root.AddComponent<ProvingArena>();var move=ProvingProfile.CreateDefault();arena.Build(ArenaFreezeSnapshot.Create(FiringRangeCatalog.Build(),move),move);var art=root.GetComponentInChildren<FiringRangePresentation>();var owner=art.gameObject;art.SetCombined(false);
            var lightObject=new GameObject("comparison-light");SceneManager.MoveGameObjectToScene(lightObject,scene);var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(50,-25,0);
            var cameraObject=new GameObject("review-camera");SceneManager.MoveGameObjectToScene(cameraObject,scene);var camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(0,3,-22);camera.transform.rotation=Quaternion.Euler(3,0,0);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.1f,.15f,.2f);
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RANGE_EVIDENCE");int before=owner.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled);
            byte[] beforePixels=null;yield return Pixels(camera,data=>beforePixels=data);if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,"optimization-before.png"),beforePixels);}
            art.SetCombined(true);int after=owner.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled);byte[] afterPixels=null;yield return Pixels(camera,data=>afterPixels=data);
            Assert.That(after,Is.LessThan(before));Assert.That(owner.GetComponentsInChildren<Collider>(),Is.Empty);Assert.That(afterPixels,Is.EqualTo(beforePixels),"Static combine must preserve exact rendered pixels");
            if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllText(Path.Combine(output,"optimization.txt"),"before active renderers="+before+"\nafter active renderers="+after+"\nexact pixels equal\nnew texture assets=0; new lights=0; collision unchanged; LOD N/A for low-poly room shell\n");yield return Capture(camera,Path.Combine(output,"optimization-after.png"));}
            yield return null;
        }
        static IEnumerator Pixels(Camera camera,Action<byte[]> complete)
        {
            var target=new RenderTexture(1280,720,24);var prior=camera.targetTexture;var rect=camera.rect;var active=RenderTexture.active;var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;
                // SRP completes the target during the player loop, after Camera.Render returns.
                for(int frame=0;frame<3;frame++){camera.Render();yield return null;}
                RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                var pixels=image.GetPixels32();Assert.That(pixels.Any(pixel=>!pixel.Equals(pixels[0])),Is.True,"Range evidence must contain rendered geometry, not a uniform uninitialized target");
                complete(image.EncodeToPNG());
            }
            finally{camera.targetTexture=prior;camera.rect=rect;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(target);}
        }
        static IEnumerator Capture(Camera camera,string path){yield return Pixels(camera,data=>File.WriteAllBytes(path,data));}
    }
}
