using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class SeatInputTests
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
    }
}
