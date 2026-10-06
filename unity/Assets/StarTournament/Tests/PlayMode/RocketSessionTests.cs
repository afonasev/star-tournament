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
    public sealed class RocketSessionTests
    {
        Scene scene; GameObject owner; ProvingArena arena; CharacterMotor[] motors;
        ProvingProfile move,combat,life; NativeCombatSession session;
        void Setup(bool teams=false,bool threeAllies=false)
        {
            scene=SceneManager.CreateScene("rocket-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            owner=new GameObject("rocket owner");SceneManager.MoveGameObjectToScene(owner,scene);
            move=ProvingProfile.CreateDefault();combat=ProvingProfile.CreateNativeCombatDefault();life=ProvingProfile.CreateCombatDefault();
            arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),move);
            motors=new CharacterMotor[4];Vector3[] positions={new Vector3(-8,0,0),new Vector3(-8,0,6),new Vector3(-6,0,6),new Vector3(8,0,0)};
            for(int i=0;i<4;i++){var o=new GameObject("p"+i);o.transform.SetParent(owner.transform);o.layer=ProvingArena.ParticipantLayer;motors[i]=o.AddComponent<CharacterMotor>();motors[i].Initialize(move,positions[i]);}
            var profile=ProvingProfile.CreateMatchDefault();var config=NativeMatchConfiguration.Default(profile);
            var match=teams?new NativeMatchState(new NativeMatchRoster(NativeMatchMode.Teams,new[]{NativeTeam.TeamA,NativeTeam.TeamA,threeAllies?NativeTeam.TeamA:NativeTeam.TeamB,NativeTeam.TeamB}),config,profile,50):new NativeMatchState(4,config,profile,50);
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat,match);
            EquippedCombatFixture.Equip(session);
            var actions=new LocalAction[4];actions[0].SelectWeapon=WeaponSelection.RocketLauncher;
            session.Tick(actions,.02f);for(int i=0;i<51;i++)session.Tick(new LocalAction[4],.02f);
        }
        void Fire(){var a=new LocalAction[4];a[0].Fire=true;session.Tick(a,.02f);}
        void Ticks(int n){for(int i=0;i<n;i++)session.Tick(new LocalAction[4],.02f);}
        GameObject Wall(Vector3 center,Vector3 size)
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.SetParent(owner.transform);wall.layer=ProvingArena.WorldLayer;wall.transform.position=center;wall.transform.localScale=size;Physics.SyncTransforms();return wall;
        }
        [UnityTest] public IEnumerator Digit3AndDpadRightAreSeatLocalPlayerEdges()
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
                InputSystem.QueueStateEvent(k,new KeyboardState(Key.Digit3));yield return null;
                Assert.That(k.digit3Key.wasPressedThisFrame,Is.True);seats.Capture(ProvingProfile.CreateDefault(),.02f);
                Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.RocketLauncher));Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.None));
                InputSystem.QueueStateEvent(k,new KeyboardState());InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;
                Assert.That(pad.dpad.right.wasPressedThisFrame,Is.True);seats.Capture(ProvingProfile.CreateDefault(),.02f);
                Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.RocketLauncher));Assert.That(seats.Consume(0).SelectWeapon,Is.EqualTo(WeaponSelection.None));
                seats.Clear();Assert.That(seats.Consume(1).SelectWeapon,Is.EqualTo(WeaponSelection.None));
            }
            finally {InputSystem.RemoveDevice(k);InputSystem.RemoveDevice(m);InputSystem.RemoveDevice(pad);InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;InputSystem.settings.backgroundBehavior=priorBackground;}
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator DirectAndSplashUseUniformBoostOnceWithArmor()
        {
            Setup();yield return null;var s=session.Capture();s.Lives[1].Armor=50;s.DamageRemaining[0]=10;session.Restore(s);
            int explosions=0;session.RocketExploded+=_=>explosions++;Fire();Ticks(12);
            Assert.That(explosions,Is.EqualTo(1));Assert.That(session.Life(1).Health,Is.EqualTo(22.5f));Assert.That(session.Life(1).Armor,Is.Zero);
            Assert.That(session.Life(2).Health,Is.LessThan(100));Assert.That(session.Rockets,Is.Empty);
            float hp=session.Life(1).Health;Ticks(20);Assert.That(session.Life(1).Health,Is.EqualTo(hp));Assert.That(explosions,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator FriendlyDirectAndSelfSplashAreHalvedWithoutFriendlyKillAward()
        {
            Setup(true);yield return null;motors[1].Initialize(move,new Vector3(-8,0,2));
            Assert.That(session.ApplyDamage(1,1,10,0,1).Applied,Is.EqualTo(5),"All weapons share friendly policy");
            var snapshot=session.Capture();snapshot.Lives[1].Health=40;session.Restore(snapshot);
            Fire();Ticks(4);Assert.That(session.Life(1).Dead,Is.True);Assert.That(session.Life(0).Health,Is.LessThan(100));
            Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).Kills,Is.Zero);
            Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).AllyKills,Is.EqualTo(1));
            Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).AccumulatedPenalty,Is.EqualTo(200));
        }
        [UnityTest] public IEnumerator MultiVictimFriendlySplashAttributesAppliedDamageAndPenaltyOnceThroughRestoreAndRespawn()
        {
            Setup(true,true);yield return null;
            motors[1].Initialize(move,new Vector3(-8,0,2));motors[2].Initialize(move,new Vector3(-7,0,2));
            var state=session.Capture();state.Lives[0].Health=1;state.Lives[1].Health=1;state.Lives[1].Armor=10;state.Lives[2].Health=1;session.Restore(state);
            Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).AllyDamageDealt,Is.Zero);
            Fire();Ticks(8);var row=session.Match.Read().Standings.Single(r=>r.Seat==0);
            Assert.That((row.SelfKills,row.AllyKills,row.Kills,row.AccumulatedPenalty,row.Score),Is.EqualTo((1,2,0,600,-600)));
            Assert.That(row.DamageDealt,Is.Zero);Assert.That(row.AllyDamageDealt,Is.EqualTo(12),"Actual allied shield and HP loss is counted without overkill");
            Ticks(10);Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).AccumulatedPenalty,Is.EqualTo(600));
            var saved=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(session.Capture()));session.Restore(saved);
            Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).Score,Is.EqualTo(-600));
            Ticks(400);Assert.That(session.Life(0).Dead,Is.False);Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).AccumulatedPenalty,Is.EqualTo(600));
            session.ApplyDamage(3,session.Life(3).Life,500,0,session.Life(0).Life);session.Tick(new LocalAction[4],.02f);
            Assert.That(session.Match.Read().Standings.Single(r=>r.Seat==0).Score,Is.EqualTo(-500));
        }
        [UnityTest] public IEnumerator ThinWallAndFloorSweepExplodeOnceAndOccludeSplash()
        {
            Setup();yield return null;Wall(new Vector3(-8,1,3),new Vector3(6,4,.01f));
            int count=0;int direct=-2;session.RocketExploded+=e=>{count++;direct=e.DirectSeat;};
            Fire();Ticks(10);Assert.That(count,Is.EqualTo(1));Assert.That(direct,Is.EqualTo(-1));Assert.That(session.Life(1).Health,Is.EqualTo(100));
            motors[0].Tick(new LocalAction{LookDegrees=new Vector2(0,-60)},.02f);session.ClearInput();Ticks(61);Fire();Ticks(10);
            Assert.That(count,Is.EqualTo(2),"Downward rocket contacts floor once");
        }
        [UnityTest] public IEnumerator FlightRestoresWithoutDuplicatesSurvivesOwnerDeathAndNewSessionClearsIt()
        {
            Setup();yield return null;Fire();var snap=session.Capture();Assert.That(snap.Rockets.Length,Is.EqualTo(1));
            session.ClearInput();double time=session.Time;var position=session.Rockets[0].Position;yield return new WaitForSecondsRealtime(.05f);
            Assert.That(session.Time,Is.EqualTo(time));Assert.That(session.Rockets[0].Position,Is.EqualTo(position));
            var invalid=JsonUtility.FromJson<NativeCombatSessionSnapshot>(JsonUtility.ToJson(snap));invalid.Rockets=new[]{snap.Rockets[0],snap.Rockets[0]};
            Assert.Throws<ArgumentException>(()=>session.Restore(invalid));Assert.That(session.Rockets.Length,Is.EqualTo(1));
            int count=0;session.RocketExploded+=_=>count++;session.ApplyDamage(0,1,100);session.ApplyDamage(1,1,20);Ticks(12);
            Assert.That(count,Is.EqualTo(1));Assert.That(session.Life(1).KillerLife,Is.EqualTo(1));
            session.Restore(snap);session.ClearInput();Ticks(12);Assert.That(count,Is.EqualTo(2),"Replayed flight produces exactly one new event");
            var repeated=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),move,life,combat);Assert.That(repeated.Rockets,Is.Empty);Assert.That(repeated.Life(0).RocketAmmo,Is.Zero);
        }
    }
}
