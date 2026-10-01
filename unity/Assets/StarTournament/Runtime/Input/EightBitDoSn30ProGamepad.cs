using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

namespace StarTournament.ProvingGround
{
    // Bluetooth X-input mode, observed on SN30 Pro firmware 9.0.3 on macOS.
    // Its HID descriptor exposes a generic joystick, so Gamepad-only seat and menu input miss it.
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct EightBitDoSn30ProHidState : IInputStateTypeInfo
    {
        const ushort StickCenter = 32767;
        public FourCC format => new FourCC('H', 'I', 'D');

        [FieldOffset(0)] public byte reportId;

        [InputControl(name = "leftStick", layout = "Stick", format = "VC2S")]
        [InputControl(name = "leftStick/x", offset = 0, format = "USHT", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5", defaultState = StickCenter)]
        [InputControl(name = "leftStick/y", offset = 2, format = "USHT", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5", defaultState = StickCenter)]
        [FieldOffset(1)] public ushort leftStickX;
        [FieldOffset(3)] public ushort leftStickY;

        [InputControl(name = "rightStick", layout = "Stick", format = "VC2S")]
        [InputControl(name = "rightStick/x", offset = 0, format = "USHT", parameters = "normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5", defaultState = StickCenter)]
        [InputControl(name = "rightStick/y", offset = 2, format = "USHT", parameters = "invert,normalize,normalizeMin=0,normalizeMax=1,normalizeZero=0.5", defaultState = StickCenter)]
        [FieldOffset(5)] public ushort rightStickX;
        [FieldOffset(7)] public ushort rightStickY;

        // Both triggers are 10-bit unsigned HID axes, padded to 16 bits in the report.
        [InputControl(name = "leftTrigger", format = "USHT", parameters = "normalize,normalizeMin=0,normalizeMax=0.01560998")]
        [FieldOffset(9)] public ushort leftTrigger;
        [InputControl(name = "rightTrigger", format = "USHT", parameters = "normalize,normalizeMin=0,normalizeMax=0.01560998")]
        [FieldOffset(11)] public ushort rightTrigger;

        // Hat: 1=up, 3=right, 5=down, 7=left, 0=neutral.
        [InputControl(name = "dpad", format = "BIT", layout = "Dpad", sizeInBits = 4)]
        [InputControl(name = "dpad/up", format = "BIT", layout = "DiscreteButton", parameters = "minValue=8,maxValue=2,nullValue=0,wrapAtValue=9", bit = 0, sizeInBits = 4)]
        [InputControl(name = "dpad/right", format = "BIT", layout = "DiscreteButton", parameters = "minValue=2,maxValue=4", bit = 0, sizeInBits = 4)]
        [InputControl(name = "dpad/down", format = "BIT", layout = "DiscreteButton", parameters = "minValue=4,maxValue=6", bit = 0, sizeInBits = 4)]
        [InputControl(name = "dpad/left", format = "BIT", layout = "DiscreteButton", parameters = "minValue=6,maxValue=8", bit = 0, sizeInBits = 4)]
        [FieldOffset(13)] public byte hat;

        // HID button usages 1..10 in this report follow A, B, X, Y, LB, RB,
        // Back, Start, left-stick press, right-stick press.
        [InputControl(name = "buttonSouth", bit = 0, displayName = "A")]
        [InputControl(name = "buttonEast", bit = 1, displayName = "B")]
        [InputControl(name = "buttonWest", bit = 2, displayName = "X")]
        [InputControl(name = "buttonNorth", bit = 3, displayName = "Y")]
        [InputControl(name = "leftShoulder", bit = 4)]
        [InputControl(name = "rightShoulder", bit = 5)]
        [InputControl(name = "select", bit = 6)]
        [InputControl(name = "start", bit = 7)]
        [InputControl(name = "leftStickPress", bit = 8)]
        [InputControl(name = "rightStickPress", bit = 9)]
        [FieldOffset(14)] public ushort buttons;
    }

    [InputControlLayout(displayName = "8BitDo SN30 Pro", stateType = typeof(EightBitDoSn30ProHidState))]
    public sealed class EightBitDoSn30ProGamepad : Gamepad
    {
        public static void Register()
        {
            InputSystem.RegisterLayout<EightBitDoSn30ProGamepad>(matches: new InputDeviceMatcher()
                .WithInterface("HID")
                .WithCapability("vendorId", 0x045e)
                .WithCapability("productId", 0x02e0)
                .WithProduct("8Bitdo SN30 Pro"));
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize() => Register();
    }
}
