using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Serializable gameplay state. Position is the participant's feet anchor.</summary>
    [Serializable]
    public struct ParticipantState
    {
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw;
        public float Pitch;
        public bool Grounded;
        public float LookNeutralSeconds; // Gameplay clock only; serializable assistance phase.
        public float LookReturnVelocity;
    }
}
