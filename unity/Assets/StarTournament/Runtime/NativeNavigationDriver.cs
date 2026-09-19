using System;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Observation/action orchestration, called exactly once before the session's advancing tick.</summary>
    public sealed class NativeNavigationDriver
    {
        readonly NativeCombatSession session;
        readonly NativeBotObservationProvider observations;
        readonly int controlled;
        public NativeBotPerception Perception { get; }
        public NativeBotNavigation Controller { get; }
        public NativeNavigationDriver(NativeCombatSession session, ProvingArena arena, PhysicsScene physics,
            ProvingProfile movement, ProvingProfile combat, ProvingProfile perception, ProvingProfile navigation, int controlled)
        {
            this.session = session;
            if (controlled < 0 || controlled >= session.ParticipantCount) throw new ArgumentOutOfRangeException(nameof(controlled));
            this.controlled = controlled;
            var difficulties = new NativeBotDifficulty[session.ParticipantCount];
            for (int i = 0; i < difficulties.Length; i++) difficulties[i] = NativeBotDifficulty.Normal;
            Perception = new NativeBotPerception(session.Match.Roster, difficulties, perception);
            observations = new NativeBotObservationProvider(session, session.Match.Roster, physics, movement, combat);
            Controller = new NativeBotNavigation(new NativeNavigationProvider(arena, movement, navigation), navigation);
        }
        public void ProduceActions(LocalAction[] actions)
        {
            if (actions == null || actions.Length != session.ParticipantCount) throw new ArgumentException("Invalid action roster");
            Perception.Sample(session.Time, observations.Observe(Perception));
            actions[controlled] = Controller.Tick(session.Time, session.Pose(controlled), Perception.Read(controlled));
        }
    }
}
