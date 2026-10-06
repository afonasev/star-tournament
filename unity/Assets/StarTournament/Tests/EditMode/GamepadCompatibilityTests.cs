using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace StarTournament.ProvingGround.Tests
{
    public sealed class GamepadCompatibilityTests
    {
        static InputDevice AddHid(string product, int vendor, int model) => InputSystem.AddDevice(new InputDeviceDescription
        {
            interfaceName = "HID",
            product = product,
            capabilities = "{\"vendorId\":" + vendor + ",\"productId\":" + model + "}"
        });

        [TestCase("DualShock 3", 0x054c, 0x0268)]
        [TestCase("DualShock 4", 0x054c, 0x09cc)]
        [TestCase("DualSense", 0x054c, 0x0ce6)]
        [TestCase("DualSense Edge", 0x054c, 0x0df2)]
        [TestCase("Pro Controller", 0x057e, 0x2009)]
        [TestCase("HORIPAD", 0x0f0d, 0x00c1)]
        [TestCase("PDP Faceoff", 0x0e6f, 0x0180)]
        [TestCase("PowerA Fusion", 0x20d6, 0xa716)]
        public void InstalledHidLayoutsProduceAssignableGamepads(string product, int vendor, int model)
        {
            InputDevice device = null;
            try
            {
                device = AddHid(product, vendor, model);
                Assert.That(device, Is.InstanceOf<Gamepad>(), product);
                Assert.That(GamepadCompatibility.CanAssign(device), Is.True);
                var seats = new SeatInputCoordinator();
                Assert.That(seats.Assign(0, device), Is.True);
                Assert.That(seats.Assign(1, device), Is.False);
            }
            finally { if(device != null && device.added) InputSystem.RemoveDevice(device); }
        }

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        [Test]
        public void MacXboxWirelessLayoutProducesAssignableGamepad()
        {
            InputDevice device = null;
            try
            {
                device = AddHid("Xbox Wireless Controller", 0x045e, 0x02fd);
                Assert.That(device, Is.InstanceOf<Gamepad>());
                Assert.That(GamepadCompatibility.CanAssign(device), Is.True);
            }
            finally { if(device != null && device.added) InputSystem.RemoveDevice(device); }
        }
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        [Test]
        public void WindowsXInputModeProducesAssignableGamepad()
        {
            InputDevice device = null;
            try
            {
                device = InputSystem.AddDevice(new InputDeviceDescription { interfaceName = "XInput", product = "Controller" });
                Assert.That(device, Is.InstanceOf<Gamepad>());
                Assert.That(GamepadCompatibility.CanAssign(device), Is.True);
            }
            finally { if(device != null && device.added) InputSystem.RemoveDevice(device); }
        }
#endif

        [Test]
        public void GenericJoystickIsVisibleButCannotClaimASeat()
        {
            var joystick = InputSystem.AddDevice<Joystick>();
            try
            {
                Assert.That(GamepadCompatibility.IsListed(joystick), Is.True);
                Assert.That(GamepadCompatibility.CanAssign(joystick), Is.False);
                StringAssert.Contains("XInput", GamepadCompatibility.Status(joystick));
                Assert.That(new SeatInputCoordinator().Assign(0, joystick), Is.False);
            }
            finally { if(joystick.added) InputSystem.RemoveDevice(joystick); }
        }
    }
}
