using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class SeatInputTests : InputTestFixture
    {
        [Test]
        public void KeyboardMousePairDoesNotSwitchToSpareMouse()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>(); var secondKeyboard=InputSystem.AddDevice<Keyboard>();
            var mouse=InputSystem.AddDevice<Mouse>(); Mouse spare=null;
            try
            {
                var seats=new SeatInputCoordinator();
                Assert.That(seats.Assign(0,keyboard),Is.True);
                spare=InputSystem.AddDevice<Mouse>();
                Assert.That(seats.Assign(1,secondKeyboard),Is.False);
                InputSystem.RemoveDevice(mouse);
                Assert.That(seats.IsConnected(0),Is.False,"Another mouse cannot silently take ownership");
            }
            finally
            {
                foreach(var device in new InputDevice[]{keyboard,secondKeyboard,mouse,spare}) if(device!=null && device.added) InputSystem.RemoveDevice(device);
            }
        }
        [Test]
        public void FourDistinctDevicesAreRequiredAndDisconnectBlocksStart()
        {
            var pads = new Gamepad[4];
            try
            {
                var seats = new SeatInputCoordinator();
                for (int i=0;i<pads.Length;i++) pads[i]=InputSystem.AddDevice<Gamepad>();
                Assert.That(seats.Assign(0,pads[0]),Is.True);
                Assert.That(seats.Assign(1,pads[0]),Is.False,"A physical device cannot control two players");
                for(int i=1;i<pads.Length;i++) Assert.That(seats.Assign(i,pads[i]),Is.True);
                Assert.That(seats.Ready,Is.True);
                InputSystem.RemoveDevice(pads[2]);
                Assert.That(seats.Ready,Is.False);
                Assert.That(seats.IsConnected(2),Is.False);
                seats.Reset();
                Assert.That(seats.Ready,Is.False);
            }
            finally { foreach(var pad in pads) if(pad!=null && pad.added) InputSystem.RemoveDevice(pad); }
        }
        [Test]
        public void AiSeatsNeedNoDeviceAndChangingKindReleasesTheirInput()
        {
            var first=InputSystem.AddDevice<Gamepad>();var second=InputSystem.AddDevice<Gamepad>();
            try
            {
                var seats=new SeatInputCoordinator();
                seats.SetActiveSeatCount(3);
                Assert.That(seats.Assign(0,first),Is.True);Assert.That(seats.Assign(2,second),Is.True);
                seats.SetHumanSeat(1,false);seats.SetHumanSeat(2,false);
                Assert.That(seats.IsHumanSeat(0),Is.True);Assert.That(seats.IsHumanSeat(1),Is.False);
                Assert.That(seats.Ready,Is.True);Assert.That(seats.Assign(1,second),Is.False);
                seats.SetHumanSeat(2,true);
                Assert.That(seats.IsConnected(2),Is.False,"Changing ownership releases its device");
                Assert.That(seats.Ready,Is.False);
                Assert.That(seats.Assign(2,second),Is.True);Assert.That(seats.Ready,Is.True);
                seats.SetActiveSeatCount(2);seats.SetActiveSeatCount(3);
                Assert.That(seats.IsHumanSeat(2),Is.True,"A newly visible slot defaults to human");
                Assert.That(seats.Ready,Is.False);
            }
            finally {foreach(var d in new InputDevice[]{first,second})if(d.added)InputSystem.RemoveDevice(d);}
        }
        [Test]
        public void LtTapIsLatchedOnceAndHoldLocksPitchWithoutBlockingYawOrAnotherSeat()
        {
            var pads=new[]{InputSystem.AddDevice<Gamepad>(),InputSystem.AddDevice<Gamepad>()};
            try
            {
                var seats=new SeatInputCoordinator();var profile=ProvingProfile.CreateDefault();
                Assert.That(seats.Assign(0,pads[0]),Is.True);Assert.That(seats.Assign(1,pads[1]),Is.True);
                Capture(seats,pads[0],profile,new GamepadState(),.01f);Capture(seats,pads[1],profile,new GamepadState(),.01f);
                Capture(seats,pads[0],profile,new GamepadState{leftTrigger=1,rightTrigger=1,rightStick=new Vector2(1,1)},.12f);
                Capture(seats,pads[1],profile,new GamepadState{rightStick=Vector2.up},.01f);
                var locked=seats.Consume(0);
                Assert.That(locked.GamepadLookLocked,Is.True);Assert.That(locked.LookDegrees.x,Is.GreaterThan(0));Assert.That(locked.LookDegrees.y,Is.Zero);
                Assert.That(locked.Fire,Is.True,"RT remains independent of LT aim locking");
                var other=seats.Consume(1);Assert.That(other.GamepadLookLocked,Is.False);Assert.That(other.LookDegrees.y,Is.GreaterThan(0));
                Capture(seats,pads[0],profile,new GamepadState{leftTrigger=1,rightStick=Vector2.up},.08f); // Total across all captures is .21 s, below the tap boundary.
                Assert.That(seats.Consume(0).GamepadLookLocked,Is.True,"hold state survives fixed-tick consumption");
                Capture(seats,pads[0],profile,new GamepadState{rightStick=Vector2.up},.01f);
                var released=seats.Consume(0);
                Assert.That(released.ResetLookPitch,Is.True);Assert.That(released.LookDegrees.y,Is.Zero);
                Assert.That(seats.Consume(0).ResetLookPitch,Is.False,"reset edge is consumed exactly once");
            }
            finally {foreach(var pad in pads)if(pad.added)InputSystem.RemoveDevice(pad);}
        }
        [TestCase(.22f,true)]
        [TestCase(.23f,false)]
        public void LtTapDurationUsesOneInclusiveConfiguredBoundary(float pressFrameSeconds,bool resets)
        {
            var pad=InputSystem.AddDevice<Gamepad>();
            try
            {
                var seats=new SeatInputCoordinator();var profile=ProvingProfile.CreateDefault();seats.Assign(0,pad);
                Capture(seats,pad,profile,new GamepadState(),.01f);
                Capture(seats,pad,profile,new GamepadState{leftTrigger=1},pressFrameSeconds);
                Capture(seats,pad,profile,new GamepadState(),.01f);
                var action=seats.Consume(0);
                Assert.That(action.ResetLookPitch,Is.EqualTo(resets));Assert.That(action.GamepadLookLocked,Is.False);
                Assert.That(profile.Descriptor("input.gamepadTapAimThresholdSeconds").Contains(profile.Get("input.gamepadTapAimThresholdSeconds")),Is.True);
            }
            finally {if(pad.added)InputSystem.RemoveDevice(pad);}
        }
        [Test]
        public void LtLifecycleCancellationRequiresPhysicalReleaseAndDeadSeatCannotStartGesture()
        {
            var pad=InputSystem.AddDevice<Gamepad>();Gamepad replacement=null;
            try
            {
                var seats=new SeatInputCoordinator();var profile=ProvingProfile.CreateDefault();seats.Assign(0,pad);
                Capture(seats,pad,profile,new GamepadState(),.01f);
                Capture(seats,pad,profile,new GamepadState{leftTrigger=1},.05f);
                seats.Clear();Capture(seats,pad,profile,new GamepadState{leftTrigger=1},.1f);
                Assert.That(seats.Consume(0).ResetLookPitch,Is.False);
                Capture(seats,pad,profile,new GamepadState(),.01f);
                Capture(seats,pad,profile,new GamepadState{leftTrigger=1},.05f);
                seats.SetSeatAlive(0,false);Capture(seats,pad,profile,new GamepadState{leftTrigger=1}.WithButton(GamepadButton.Start),.1f);
                Assert.That(seats.Consume(0).GamepadLookLocked,Is.False);
                Assert.That(seats.ConsumePause(),Is.True,"dead players retain ordinary pause input");
                seats.SetSeatAlive(0,true);Capture(seats,pad,profile,new GamepadState{leftTrigger=1},.1f);
                Assert.That(seats.Consume(0).GamepadLookLocked,Is.False,"respawn still requires physical release");
                Capture(seats,pad,profile,new GamepadState(),.01f);
                Capture(seats,pad,profile,new GamepadState{leftTrigger=1},.05f);
                Assert.That(seats.Consume(0).GamepadLookLocked,Is.True);
                InputSystem.RemoveDevice(pad);seats.Capture(profile,.01f);
                replacement=InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(replacement,new GamepadState{leftTrigger=1});InputSystem.Update();
                Assert.That(seats.Replace(0,replacement),Is.True);
                seats.Capture(profile,.05f);
                Assert.That(seats.Consume(0).GamepadLookLocked,Is.False,"rebind while LT is held requires release");
                Capture(seats,replacement,profile,new GamepadState(),.01f);
                Capture(seats,replacement,profile,new GamepadState{leftTrigger=1},.05f);
                Assert.That(seats.Consume(0).GamepadLookLocked,Is.True,"a fresh post-rebind gesture works");
            }
            finally {if(pad.added)InputSystem.RemoveDevice(pad);if(replacement!=null&&replacement.added)InputSystem.RemoveDevice(replacement);}
        }
        static void Capture(SeatInputCoordinator seats,Gamepad pad,ProvingProfile profile,GamepadState state,float dt)
        {
            InputSystem.QueueStateEvent(pad,state);
            // InputTestFixture supplies an isolated gameplay runtime for frame-edge assertions in EditMode.
            InputSystem.Update();seats.Capture(profile,dt);
        }
    }
}
