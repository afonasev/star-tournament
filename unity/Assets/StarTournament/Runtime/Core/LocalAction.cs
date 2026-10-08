using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    public enum WeaponId { Rifle = 1, Shotgun = 2, RocketLauncher = 3, Cutter = 4 }
    public enum WeaponSelection { None, Rifle, Shotgun, Previous, Next, RocketLauncher, Cutter }
    /// <summary>Device-independent command consumed by one local participant on one fixed tick.</summary>
    [Serializable]
    public struct LocalAction
    {
        public Vector2 Move;
        public Vector2 LookDegrees;
        public bool GamepadLookAssistance; // Explicit device-independent assistance request for this seat.
        public bool ManualLook; // Held right-stick intent, preserved even between fixed ticks.
        public bool GamepadLookLocked; // LT holds the current authoritative pitch while yaw remains available.
        public bool ResetLookPitch; // Latched LT tap command; motor applies it once on a fixed tick.
        public bool Jump;
        public bool Fire; // Latched short tap between fixed ticks.
        public bool FireHeld; // Physical level persists across fixed ticks.
        public WeaponSelection SelectWeapon; // One command, consumed on the next fixed tick.
        public bool ShowRoster;
    }
}
