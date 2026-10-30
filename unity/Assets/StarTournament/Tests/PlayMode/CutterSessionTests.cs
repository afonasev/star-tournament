using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class CutterSessionTests
    {
        [UnityTest] public IEnumerator GamepadRadiusHitsOffAxisAndCoverBlocksItsOuterVolume()
        {
            var scene=SceneManager.CreateScene("cutter-radius-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("cutter radius fixture");SceneManager.MoveGameObjectToScene(owner,scene);
            CutterPresentation visual=null;
            try
            {
                var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();var life=ProvingProfile.CreateCombatDefault();
                var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
                var motors=new CharacterMotor[3];var positions=new[]{new Vector3(0,20,0),new Vector3(.28f,20,4),new Vector3(.28f,20,8)};
                for(int i=0;i<3;i++){var o=new GameObject("p"+i);o.transform.SetParent(owner.transform);o.layer=ProvingArena.ParticipantLayer;motors[i]=o.AddComponent<CharacterMotor>();motors[i].Initialize(move,positions[i]);}
                var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);
                EquippedCombatFixture.Equip(session);var snapshot=session.Capture();
                snapshot.Lives[0].SelectedWeapon=WeaponId.Cutter;snapshot.Lives[0].Ammo=30;snapshot.Lives[0].SwitchRemaining=0;snapshot.Lives[0].PendingWeapon=default;snapshot.Lives[0].CooldownRemaining=0;session.Restore(snapshot);
                var direction=Vector3.forward;var origin=positions[0]+Vector3.up*move.Get("camera.eyeHeight");
                var resolver=new ShotgunResolver(combat,move.Get("player.capsule.height"));
                var target=new CombatTarget(1,1,session.Pose(1));
                Assert.That(resolver.ResolveTarget(origin,direction,target,20).TargetIndex,Is.EqualTo(-1),"Old zero-radius axis misses");
                Assert.That(resolver.ResolveTarget(origin,direction,target,20,.1f).TargetIndex,Is.EqualTo(0),"Chosen radius includes the off-axis target");
                var actions=new LocalAction[3];actions[0].FireHeld=true;
                void FireTicks(int count){for(int n=0;n<count;n++){for(int i=0;i<3;i++)motors[i].Initialize(move,positions[i]);Physics.SyncTransforms();session.Tick(actions,.02f);}}
                FireTicks(10);Assert.That(session.Life(1).Health,Is.LessThan(100));Assert.That(session.Life(2).Health,Is.LessThan(100),"Participants still do not block beam");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(owner.transform);wall.layer=ProvingArena.WorldLayer;wall.transform.position=new Vector3(.4f,21,6);wall.transform.localScale=new Vector3(.64f,4,.2f);Physics.SyncTransforms();
                var before=session.Capture();before.Lives[1].Health=before.Lives[2].Health=100;session.Restore(before);FireTicks(10);
                Assert.That(session.Life(1).Health,Is.LessThan(100));Assert.That(session.Life(2).Health,Is.EqualTo(100),"Wall edge blocks damage even when the thin axis clears it");
                Assert.That(session.Beam(0).Endpoint.z,Is.LessThan(6));
                string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WEAPON_BALANCE_EVIDENCE");
                if(!string.IsNullOrEmpty(output))
                {
                    Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));System.IO.Directory.CreateDirectory(output);
                    visual=new CutterPresentation(session,owner.transform,motors.Select(m=>m.gameObject).ToArray(),Array.Empty<GameObject>(),Array.Empty<int>());visual.Render();
                    var cameraObject=new GameObject("radius review camera");cameraObject.transform.SetParent(owner.transform);var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.035f,.05f);camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.fieldOfView=45;
                    camera.transform.position=new Vector3(4,23,-1);camera.transform.LookAt(new Vector3(0,21.5f,3));
                    var rt=new RenderTexture(1440,900,24);rt.Create();var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);var active=RenderTexture.active;
                    try
                    {
                        camera.targetTexture=rt;
                        if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline)UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=rt});else camera.Render();
                        RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(output,"cutter-radius-10cm.png"),texture.EncodeToPNG());
                    }
                    finally{camera.targetTexture=null;RenderTexture.active=active;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(texture);}
                    var lines=owner.GetComponentsInChildren<LineRenderer>();Assert.That(lines.Count(l=>l.enabled),Is.EqualTo(3),"Core + muzzle link + single arc; resource count unchanged");
                    var core=lines.Single(l=>l.name=="Core 0");Assert.That(core.startWidth,Is.EqualTo(.0035f),"Keep chosen thin core");
                    var arc=lines.Single(l=>l.name=="Arc 0");float max=0;for(int i=0;i<arc.positionCount;i++){var point=arc.GetPosition(i);max=Mathf.Max(max,Vector3.Distance(point,new Vector3(0,session.Beam(0).Origin.y,point.z)));}Assert.That(max,Is.GreaterThan(.08f).And.LessThanOrEqualTo(.101f));
                    string frozen=JsonUtility.ToJson(session.Capture());visual.Render();Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(frozen));
                }
                yield return null;
            }
            finally{visual?.Dispose();UnityEngine.Object.Destroy(owner);}
            yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator Digit4AndDpadDownAreSeatLocalPlayerEdges()
        {
            var priorEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            var priorBackground=InputSystem.settings.backgroundBehavior;
            // Batchmode has no focused GameView; send only this fixture's injected keyboard to runtime.
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            var k=InputSystem.AddDevice<Keyboard>();var m=InputSystem.AddDevice<Mouse>();var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                var seats=new SeatInputCoordinator();seats.SetActiveSeatCount(2);Assert.That(seats.Assign(0,k),Is.True);Assert.That(seats.Assign(1,pad),Is.True);
                yield return null;seats.Capture(ProvingProfile.CreateDefault(),.02f);seats.Consume(0);seats.Consume(1);
                InputSystem.QueueStateEvent(k,new KeyboardState(Key.Digit4));yield return null;
                Assert.That(k.digit4Key.wasPressedThisFrame,Is.True);seats.Capture(ProvingProfile.CreateDefault(),.02f);
                Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.Cutter));Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.None));
                InputSystem.QueueStateEvent(k,new KeyboardState());InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return null;
                Assert.That(pad.dpad.down.wasPressedThisFrame,Is.True);seats.Capture(ProvingProfile.CreateDefault(),.02f);
                Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.Cutter));Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.None));
                seats.Clear();Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.None));
            }
            finally {InputSystem.RemoveDevice(k);InputSystem.RemoveDevice(m);InputSystem.RemoveDevice(pad);InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;InputSystem.settings.backgroundBehavior=priorBackground;}
        }
        [UnityTest] public IEnumerator BeamPiercesTwoTargetsStopsAtWallAndClearsAtFocusBoundary()
        {
            var scene=SceneManager.CreateScene("cutter-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var owner=new GameObject("cutter fixture");SceneManager.MoveGameObjectToScene(owner,scene);
            try
            {
                var move=ProvingProfile.CreateDefault();var combat=ProvingProfile.CreateNativeCombatDefault();var life=ProvingProfile.CreateCombatDefault();
                var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
                var motors=new CharacterMotor[4];
                for(int i=0;i<4;i++){var o=new GameObject("p"+i);o.transform.SetParent(owner.transform);o.layer=ProvingArena.ParticipantLayer;motors[i]=o.AddComponent<CharacterMotor>();motors[i].Initialize(move,new Vector3(0,20,i*3));}
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(owner.transform);wall.layer=ProvingArena.WorldLayer;wall.transform.position=new Vector3(0,20,7.5f);wall.transform.localScale=new Vector3(5,20,.2f);
                var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);
            EquippedCombatFixture.Equip(session);
                var a=new LocalAction[4];a[0].SelectWeapon=WeaponSelection.Cutter;session.Tick(a,.02f);session.Tick(new LocalAction[4],1);
                // Reset fixture poses after switch time so contacts start at known geometry.
                for(int i=0;i<4;i++)motors[i].Initialize(move,new Vector3(0,20,i*3));Physics.SyncTransforms();
                a=new LocalAction[4];a[0].FireHeld=true;
                for(int n=0;n<2;n++)session.Tick(a,.02f);
                Assert.That(session.Life(1).Health,Is.LessThan(100).And.GreaterThan(98),"Contact below100ms applies its delivered fraction immediately");
                for(int n=0;n<18;n++)session.Tick(a,.02f);
                Assert.That(session.Life(1).Health,Is.LessThan(100));Assert.That(session.Life(2).Health,Is.LessThan(100));Assert.That(session.Life(3).Health,Is.EqualTo(100));
                Assert.That(session.Beam(0).Active,Is.True);Assert.That(session.Beam(0).Endpoint.z,Is.LessThan(7.5f));
                var snapshot=session.Capture();var legacy=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(snapshot));legacy.Version=8;
                Assert.Throws<ArgumentException>(()=>session.Restore(legacy),"Legacy deferred-contact snapshot is explicitly unsupported");
                session.ClearInput();Assert.That(session.Beam(0).Active,Is.False);
                session.Tick(a,.02f);Assert.That(session.Beam(0).Active,Is.False,"Resume requires release");
                session.Restore(snapshot);Assert.That(session.Life(0).CutterEnergy,Is.EqualTo(snapshot.Lives[0].CutterEnergy));
                yield return null;
            }
            finally {UnityEngine.Object.Destroy(owner);}
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
