using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround
{
    // Only a semantic Gamepad layout is safe for shared game actions. A generic
    // HID Joystick does not identify which physical button means jump or pause.
    public static class GamepadCompatibility
    {
        public static bool IsListed(InputDevice device) => device is Keyboard || device is Gamepad || device is Joystick;
        public static bool CanAssign(InputDevice device) => device != null && device.added && device.enabled &&
            (device is Gamepad || (device is Keyboard && Mouse.current != null));

        public static IEnumerable<InputDevice> ListedDevices()
        {
            foreach (var device in InputSystem.devices)
                if (IsListed(device)) yield return device;
        }

        public static string Name(InputDevice device)
        {
            if (device is Keyboard) return "Клавиатура + мышь";
            if (device == null) return "Неизвестное устройство";
            string name = device.description.product;
            if (string.IsNullOrWhiteSpace(name)) name = device.displayName;
            return string.IsNullOrWhiteSpace(name) ? "Контроллер" : name.Trim();
        }

        public static string Status(InputDevice device)
        {
            if (device is Joystick && !(device is Gamepad))
                return "Не поддерживается: включите XInput / совместимый режим или нужен профиль модели";
            if (device is Keyboard && Mouse.current == null) return "Нет мыши";
            return CanAssign(device) ? "Готов к игре" : "Недоступен";
        }
    }
}
