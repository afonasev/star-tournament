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
