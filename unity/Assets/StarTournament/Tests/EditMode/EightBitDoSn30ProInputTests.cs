using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class EightBitDoSn30ProInputTests
    {
        static EightBitDoSn30ProGamepad AddPad()
        {
            EightBitDoSn30ProGamepad.Register();
            return InputSystem.AddDevice(new InputDeviceDescription
            {
                interfaceName = "HID",
                product = "8Bitdo SN30 Pro",
                capabilities = "{\"vendorId\":1118,\"productId\":736}"
            }) as EightBitDoSn30ProGamepad;
        }

        static void Send(EightBitDoSn30ProGamepad pad, EightBitDoSn30ProHidState state)
        {
            InputSystem.QueueStateEvent(pad, state);
            InputSystem.Update();
        }

        [Test]
        public void BluetoothHidIsRecognizedAsAssignableGamepad()
        {
            var pad = AddPad();
            try
            {
                Assert.That(pad, Is.Not.Null);
                Assert.That(Gamepad.all, Does.Contain(pad));
                var seats = new SeatInputCoordinator();
                Assert.That(seats.Assign(0, pad), Is.True);
                Assert.That(seats.IsConnected(0), Is.True);
            }
            finally { if (pad != null && pad.added) InputSystem.RemoveDevice(pad); }
        }

        [Test]
        public void HidReportMapsMenuGameplayButtonsHatSticksAndTriggers()
        {
            var pad = AddPad();
            try
            {
                Assert.That(pad, Is.Not.Null);
                var neutral = new EightBitDoSn30ProHidState
                {
                    reportId = 1, leftStickX = 32767, leftStickY = 32767,
                    rightStickX = 32767, rightStickY = 32767
                };
                Send(pad, neutral);
                Assert.That(pad.leftStick.ReadValue().magnitude, Is.LessThan(0.02f));
                Assert.That(pad.rightStick.ReadValue().magnitude, Is.LessThan(0.02f));
                Assert.That(pad.dpad.IsPressed(), Is.False);

                var state = neutral;
                state.leftStickX = 65535;
                state.leftStickY = 0;
                state.rightStickX = 65535;
                state.rightStickY = 0;
                state.leftTrigger = 1023;
                state.rightTrigger = 1023;
                state.hat = 1;
                state.buttons = (1 << 0) | (1 << 3) | (1 << 7);
                Send(pad, state);
                Assert.That(pad.leftStick.ReadValue().x, Is.GreaterThan(0.6f));
                Assert.That(pad.leftStick.ReadValue().y, Is.GreaterThan(0.6f));
                Assert.That(pad.rightStick.ReadValue().x, Is.GreaterThan(0.6f));
                Assert.That(pad.rightStick.ReadValue().y, Is.GreaterThan(0.6f));
                Assert.That(pad.leftTrigger.ReadValue(), Is.GreaterThan(0.9f));
                Assert.That(pad.rightTrigger.ReadValue(), Is.GreaterThan(0.9f));
                Assert.That(pad.dpad.up.isPressed, Is.True);
                Assert.That(pad.buttonSouth.ReadValue(), Is.GreaterThan(0.5f), "A / south");
                Assert.That(pad.buttonNorth.ReadValue(), Is.GreaterThan(0.5f), "Y / north");
                Assert.That(pad.startButton.ReadValue(), Is.GreaterThan(0.5f), "Start");

                var seats = new SeatInputCoordinator();
                Assert.That(seats.Assign(0, pad), Is.True);
                seats.Capture(ProvingProfile.CreateDefault(), 0.016f);
                var action = seats.Consume(0);
                Assert.That(action.Move.x, Is.GreaterThan(0.6f));
                Assert.That(action.LookDegrees.x, Is.GreaterThan(0));

                state.hat = 3;
                state.buttons = (1 << 1) | (1 << 2) | (1 << 4) | (1 << 5) | (1 << 6) | (1 << 8) | (1 << 9);
                Send(pad, state);
                Assert.That(pad.dpad.right.isPressed, Is.True);
                Assert.That(pad.buttonEast.ReadValue(), Is.GreaterThan(0.5f));
                Assert.That(pad.buttonWest.ReadValue(), Is.GreaterThan(0.5f));
                Assert.That(pad.leftShoulder.ReadValue(), Is.GreaterThan(0.5f));
                Assert.That(pad.rightShoulder.ReadValue(), Is.GreaterThan(0.5f));
                Assert.That(pad.selectButton.ReadValue(), Is.GreaterThan(0.5f));
                Assert.That(pad.leftStickButton.ReadValue(), Is.GreaterThan(0.5f));
                Assert.That(pad.rightStickButton.ReadValue(), Is.GreaterThan(0.5f));
                state.hat = 5;
                Send(pad, state);
                Assert.That(pad.dpad.down.isPressed, Is.True);
                state.hat = 7;
                Send(pad, state);
                Assert.That(pad.dpad.left.isPressed, Is.True);
            }
            finally { if (pad != null && pad.added) InputSystem.RemoveDevice(pad); }
        }
    }
}
