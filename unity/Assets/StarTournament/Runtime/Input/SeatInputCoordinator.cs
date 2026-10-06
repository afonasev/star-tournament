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
        readonly bool[] humanSeats = { true, true, true, true };
        Mouse pairedMouse;
        bool pauseRequested;
        public event Action<int,InputDevice> Joined;
        public event Action<InputDevice> JoinRejected;
        public int ActiveSeatCount { get; private set; } = SeatCount;
        public bool HasKeyboard { get { for(int i=0;i<ActiveSeatCount;i++) if(devices[i] is Keyboard) return true; return false; } }
        public void SetActiveSeatCount(int count)
        {
            if(count < 1 || count > SeatCount) throw new ArgumentOutOfRangeException(nameof(count));
            int previous=ActiveSeatCount;
            for(int i=count;i<SeatCount;i++) ReleaseSeat(i);
            if(count>previous) for(int i=previous;i<count;i++) humanSeats[i]=true;
            ActiveSeatCount=count;
            if(!HasKeyboard) pairedMouse=null;
            Clear();
        }
        public bool IsHumanSeat(int slot) => slot>=0 && slot<ActiveSeatCount && humanSeats[slot];
        public void SetHumanSeat(int slot,bool human)
        {
            if(slot<0||slot>=ActiveSeatCount)throw new ArgumentOutOfRangeException(nameof(slot));
            if(humanSeats[slot]==human)return;
            humanSeats[slot]=human;
            ReleaseSeat(slot);
            if(!HasKeyboard) pairedMouse=null;
        }
        public bool Ready { get { for (int i = 0; i < ActiveSeatCount; i++) if (humanSeats[i] && !IsConnected(i)) return false; return true; } }
        public bool IsConnected(int slot) => slot >= 0 && slot < ActiveSeatCount && devices[slot] != null && devices[slot].added && devices[slot].enabled &&
            (!(devices[slot] is Keyboard) || (pairedMouse != null && pairedMouse.added && pairedMouse.enabled));
        public string Label(int slot) => devices[slot] == null ? "Ожидает устройство" :
            devices[slot] is Keyboard ? "Клавиатура + мышь" : devices[slot].displayName + " #" + devices[slot].deviceId;
        public InputDevice DeviceAt(int slot) => slot>=0 && slot<ActiveSeatCount ? devices[slot] : null;
        public bool Replace(int slot, InputDevice device)
        {
            if(slot<0 || slot>=ActiveSeatCount || !humanSeats[slot] || device==null || !device.added || !device.enabled ||
                !GamepadCompatibility.CanAssign(device))return false;
            for(int i=0;i<ActiveSeatCount;i++)if(i!=slot && devices[i]==device)return false;
            ReleaseSeat(slot);
            if(!HasKeyboard)pairedMouse=null;
            return Assign(slot,device);
        }
        // Setup compaction moves ownership (including disconnected handles), never rejoins devices.
        public void RemoveSeat(int slot)
        {
            if(ActiveSeatCount<=1 || slot<0 || slot>=ActiveSeatCount)throw new ArgumentOutOfRangeException(nameof(slot));
            for(int i=slot;i<ActiveSeatCount-1;i++)
            { devices[i]=devices[i+1];humanSeats[i]=humanSeats[i+1]; }
            ActiveSeatCount--;ReleaseSeat(ActiveSeatCount);humanSeats[ActiveSeatCount]=true;
            if(!HasKeyboard)pairedMouse=null;
            Clear();
        }
        public bool Assign(int slot, InputDevice device)
        {
            if (slot < 0 || slot >= ActiveSeatCount || !humanSeats[slot] || !GamepadCompatibility.CanAssign(device)) return false;
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
            for(int i=0;i<ActiveSeatCount;i++) if(devices[i]==device) return;
            // Y explicitly replaces a lost device before claiming a fresh seat.
            for (int i = 0; i < ActiveSeatCount; i++) if (humanSeats[i] && devices[i] != null && !IsConnected(i))
            {
                ReleaseSeat(i);
                if(Assign(i, device)) Joined?.Invoke(i,device);
                return;
            }
            for (int i = 0; i < ActiveSeatCount; i++) if (humanSeats[i] && devices[i] == null)
            {
                if(Assign(i, device)) Joined?.Invoke(i,device);
                return;
            }
            JoinRejected?.Invoke(device);
        }
        public void PollSetup()
        {
            if(HasKeyboard && (pairedMouse == null || !pairedMouse.added) && Mouse.current != null) pairedMouse=Mouse.current;
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame && Mouse.current != null) Join(Keyboard.current);
            foreach (var pad in Gamepad.all) if (pad.buttonNorth.wasPressedThisFrame) Join(pad);
        }
        public void Reset() { Array.Clear(devices, 0, devices.Length); pairedMouse=null; Clear(); }
        public void Clear() { Array.Clear(queued, 0, queued.Length); pauseRequested = false; for(int i=0;i<SeatCount;i++) fireReleaseRequired[i]=true; }
        public bool ConsumePause() { bool value = pauseRequested; pauseRequested = false; return value; }
        // Capture once per rendered frame; consume edges/delta once per simulation tick.
        public void Capture(ProvingProfile profile, float deltaTime, float? mouseDegreesPerPixelOverride=null, System.Func<int,GamepadLookSettings> gamepadSettings=null)
        {
            // The operator can always pause, including all-AI matches with no gameplay ownership.
            foreach(var device in InputSystem.devices)
                if(device is Keyboard operatorKeyboard)pauseRequested |= operatorKeyboard.escapeKey.wasPressedThisFrame;
            bool anyHuman=false;
            for(int seat=0;seat<ActiveSeatCount;seat++)anyHuman |= humanSeats[seat];
            if(!anyHuman)foreach(var pad in Gamepad.all)pauseRequested |= pad.startButton.wasPressedThisFrame;
            for (int i = 0; i < ActiveSeatCount; i++)
            {
                if (!humanSeats[i] || !IsConnected(i))
                {
                    queued[i]=default;fireReleaseRequired[i]=true;
                    continue;
                }
                var a = queued[i];
                if (devices[i] is Keyboard keyboard)
                {
                    a.Move = Vector2.ClampMagnitude(new Vector2((keyboard.dKey.isPressed ? 1 : 0) - (keyboard.aKey.isPressed ? 1 : 0),
                        (keyboard.wKey.isPressed ? 1 : 0) - (keyboard.sKey.isPressed ? 1 : 0)), 1);
                    a.LookDegrees += pairedMouse.delta.ReadValue() * (mouseDegreesPerPixelOverride ?? profile.Get("input.mouseDegreesPerPixel"));
                    a.Jump |= keyboard.spaceKey.wasPressedThisFrame;
                    CaptureFire(i, ref a, pairedMouse.leftButton.isPressed, pairedMouse.leftButton.wasPressedThisFrame);
                    if(keyboard.digit1Key.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Rifle;
                    else if(keyboard.digit2Key.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Shotgun;
                    else if(keyboard.digit4Key.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Cutter;
                    else if(keyboard.digit3Key.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.RocketLauncher;
                    else if(keyboard.qKey.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Previous;
                    else if(keyboard.eKey.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Next;
                    else if(pairedMouse.scroll.ReadValue().y!=0)a.SelectWeapon=pairedMouse.scroll.ReadValue().y>0?WeaponSelection.Previous:WeaponSelection.Next;
                    a.ShowRoster = keyboard.tabKey.isPressed;
                }
                else if (devices[i] is Gamepad pad)
                {
                    a.Move = Deadzone(pad.leftStick.ReadValue(), profile.Get("input.deadzone"));
                    var look=Deadzone(pad.rightStick.ReadValue(),profile.Get("input.deadzone"));
                    var settings=gamepadSettings!=null?gamepadSettings(i):GamepadLookSettings.Default(profile);
                    a.LookDegrees += Vector2.Scale(look,new Vector2(settings.Horizontal,settings.Vertical))*deltaTime;
                    a.ManualLook=look.sqrMagnitude>0;
                    a.GamepadLookAssistance=settings.AutoLevel;
                    a.Jump |= pad.buttonSouth.wasPressedThisFrame;
                    CaptureFire(i, ref a, pad.rightTrigger.isPressed, pad.rightTrigger.wasPressedThisFrame);
                    if(pad.dpad.left.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Rifle;
                    else if(pad.dpad.up.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Shotgun;
                    else if(pad.dpad.down.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Cutter;
                    else if(pad.dpad.right.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.RocketLauncher;
                    else if(pad.leftShoulder.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Previous;
                    else if(pad.rightShoulder.wasPressedThisFrame)a.SelectWeapon=WeaponSelection.Next;
                    a.ShowRoster = pad.selectButton.isPressed;
                    pauseRequested |= pad.startButton.wasPressedThisFrame;
                }
                queued[i] = a;
            }
        }
        void ReleaseSeat(int slot)
        {
            devices[slot]=null;
            queued[slot]=default;
            fireReleaseRequired[slot]=true;
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
            queued[slot].Jump = false; queued[slot].Fire = false; queued[slot].SelectWeapon=WeaponSelection.None; queued[slot].LookDegrees = Vector2.zero;
            return value;
        }
    }
}
