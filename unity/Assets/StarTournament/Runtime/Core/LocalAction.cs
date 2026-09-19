using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Device-independent command consumed by one local participant on one fixed tick.</summary>
    [Serializable]
    public struct LocalAction
    {
        public Vector2 Move;
        public Vector2 LookDegrees;
        public bool Jump;
        public bool Fire; // Latched short tap between fixed ticks.
        public bool FireHeld; // Physical level persists across fixed ticks.
        public bool ShowRoster;
    }
}
