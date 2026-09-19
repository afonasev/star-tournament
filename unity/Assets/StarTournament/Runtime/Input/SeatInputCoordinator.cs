using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround
{
    // Device ownership lives here, outside serializable gameplay state.
    public sealed class SeatInputCoordinator
    {
        public const int SeatCount = 4; // Approved local-seat limit, not a balance control.
        readonly InputDevice[] devices = new InputDevice[SeatCount];
        readonly LocalAction[] queued = new LocalAction[SeatCount];
        readonly bool[] fireReleaseRequired = new bool[SeatCount];
        Mouse pairedMouse;
        bool pauseRequested;
        public bool HasKeyboard => Array.Exists(devices, d => d is Keyboard);
        public bool Ready { get { for (int i = 0; i < SeatCount; i++) if (!IsConnected(i)) return false; return true; } }
        public bool IsConnected(int slot) => devices[slot] != null && devices[slot].added &&
            (!(devices[slot] is Keyboard) || (pairedMouse != null && pairedMouse.added));
        public string Label(int slot) => devices[slot] == null ? "Ожидает устройство" :
            devices[slot] is Keyboard ? "Клавиатура + мышь" : devices[slot].displayName + " #" + devices[slot].deviceId;
        public bool Assign(int slot, InputDevice device)
        {
            if (slot < 0 || slot >= SeatCount || device == null || !device.added || !(device is Keyboard || device is Gamepad)) return false;
            for (int i = 0; i < SeatCount; i++) if (devices[i] == device) return false;
            if (device is Keyboard)
            {
                if (Mouse.current == null) return false;
                for (int i=0;i<SeatCount;i++) if (devices[i] is Keyboard) return false;
                pairedMouse=Mouse.current;
            }
            devices[slot] = device;
            return true;
        }
        void Join(InputDevice device)
        {
            for (int i = 0; i < SeatCount; i++) if (devices[i] == null) { Assign(i, device); return; }
        }
        public void PollSetup()
        {
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && Mouse.current != null) Join(Keyboard.current);
            foreach (var pad in Gamepad.all) if (pad.startButton.wasPressedThisFrame) Join(pad);
        }
        public void Reset() { Array.Clear(devices, 0, devices.Length); pairedMouse=null; Clear(); }
        public void Clear() { Array.Clear(queued, 0, queued.Length); pauseRequested = false; for(int i=0;i<SeatCount;i++) fireReleaseRequired[i]=true; }
        public bool ConsumePause() { bool value = pauseRequested; pauseRequested = false; return value; }
        // Capture once per rendered frame; consume edges/delta once per simulation tick.
        public void Capture(ProvingProfile profile, float deltaTime)
        {
            for (int i = 0; i < SeatCount; i++)
            {
                if (!IsConnected(i)) continue;
                var a = queued[i];
                if (devices[i] is Keyboard keyboard)
                {
                    a.Move = Vector2.ClampMagnitude(new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                        (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1);
                    a.LookDegrees += pairedMouse.delta.ReadValue() * profile.Get("input.mouseDegreesPerPixel");
                    a.Jump |= keyboard.spaceKey.wasPressedThisFrame;
                    CaptureFire(i, ref a, pairedMouse.leftButton.isPressed, pairedMouse.leftButton.wasPressedThisFrame);
                    a.ShowRoster = keyboard.tabKey.isPressed;
                    pauseRequested |= keyboard.escapeKey.wasPressedThisFrame;
                }
                else if (devices[i] is Gamepad pad)
                {
                    a.Move = Deadzone(pad.leftStick.ReadValue(), profile.Get("input.deadzone"));
                    a.LookDegrees += Deadzone(pad.rightStick.ReadValue(), profile.Get("input.deadzone")) * profile.Get("input.gamepadDegreesPerSecond") * deltaTime;
                    a.Jump |= pad.buttonSouth.wasPressedThisFrame;
                    CaptureFire(i, ref a, pad.rightTrigger.isPressed, pad.rightTrigger.wasPressedThisFrame);
                    a.ShowRoster = pad.selectButton.isPressed;
                    pauseRequested |= pad.startButton.wasPressedThisFrame;
                }
                queued[i] = a;
            }
        }
        void CaptureFire(int slot, ref LocalAction action, bool held, bool edge)
        {
            if (fireReleaseRequired[slot])
            {
                if (!held) fireReleaseRequired[slot] = false;
                action.Fire = action.FireHeld = false;
                return;
            }
            action.FireHeld = held; action.Fire |= edge;
        }
        static Vector2 Deadzone(Vector2 value, float threshold) => value.magnitude < threshold ? Vector2.zero : Vector2.ClampMagnitude(value, 1);
        public LocalAction Consume(int slot)
        {
            var value = queued[slot];
            queued[slot].Jump = false; queued[slot].Fire = false; queued[slot].LookDegrees = Vector2.zero;
            return value;
        }
    }
}
