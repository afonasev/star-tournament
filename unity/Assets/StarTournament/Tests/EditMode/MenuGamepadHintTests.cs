using NUnit.Framework;
using UnityEngine.InputSystem;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class MenuGamepadHintTests
    {
        [TestCase("XInputControllerWindows","Xbox Wireless Controller","A","B","Y","Menu","X")]
        [TestCase("DualShockGamepadHID","Wireless Controller","×","○","△","Options","□")]
        [TestCase("Gamepad","DualSense Wireless Controller","×","○","△","Options","□")]
        [TestCase("SwitchProControllerHID","Pro Controller","B","A","X","+","Y")]
        [TestCase("Gamepad","Nintendo Switch Pro Controller","B","A","X","+","Y")]
        [TestCase("Gamepad","Unknown Controller","Нижняя","Правая","Верхняя","Start","Левая")]
        [TestCase("EightBitDoSn30ProGamepad","8Bitdo SN30 Pro","Нижняя","Правая","Верхняя","Start","Левая")]
        public void PhysicalConfirmBackAndAdditionalShortcutsUseAdvertisedFamily(string layout,string product,string confirm,string back,string reset,string apply,string delete)
        {
            var family=MenuGamepadHints.Family(layout,product);
            Assert.That(MenuGamepadHints.Glyph(family,MenuPadButton.South),Is.EqualTo(confirm));
            Assert.That(MenuGamepadHints.Glyph(family,MenuPadButton.East),Is.EqualTo(back));
            Assert.That(MenuGamepadHints.Glyph(family,MenuPadButton.North),Is.EqualTo(reset));
            Assert.That(MenuGamepadHints.Glyph(family,MenuPadButton.Start),Is.EqualTo(apply));
            Assert.That(MenuGamepadHints.Glyph(family,MenuPadButton.West),Is.EqualTo(delete));
        }
        [Test]public void HintsRequireActiveConnectedSemanticPadAndNeverChangeInputDevices()
        {
            var pad=InputSystem.AddDevice<Gamepad>();var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                Assert.That(MenuGamepadHints.Label("Назад",pad,true,MenuPadButton.East),Is.EqualTo("Назад · Правая"));
                Assert.That(MenuGamepadHints.Label("Назад",pad,false,MenuPadButton.East),Is.EqualTo("Назад"));
                Assert.That(MenuGamepadHints.Label("Назад",keyboard,true,MenuPadButton.East),Is.EqualTo("Назад"));
                InputSystem.DisableDevice(pad);Assert.That(MenuGamepadHints.Label("Назад",pad,true,MenuPadButton.East),Is.EqualTo("Назад"));
                InputSystem.EnableDevice(pad);InputSystem.RemoveDevice(pad);Assert.That(MenuGamepadHints.Label("Назад",pad,true,MenuPadButton.East),Is.EqualTo("Назад"));
                Assert.That(keyboard.added&&keyboard.enabled,Is.True);
            }
            finally{if(pad.added)InputSystem.RemoveDevice(pad);if(keyboard.added)InputSystem.RemoveDevice(keyboard);}
        }
    }
}
