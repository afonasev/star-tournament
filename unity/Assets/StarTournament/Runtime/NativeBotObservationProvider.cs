using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>The sole bridge from raw combat truth to filtered bot observations.</summary>
    public sealed class NativeBotObservationProvider
    {
        readonly NativeCombatSession session;
        readonly NativeMatchRoster roster;
        readonly PhysicsScene physics;
        readonly NativeVisibilityQuery visibility;
        readonly float eyeHeight, targetHeight;
        public NativeBotObservationProvider(NativeCombatSession session, NativeMatchRoster roster, PhysicsScene physics,
            ProvingProfile movement, ProvingProfile combat)
        {
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
            if (session.ParticipantCount != roster.Count || !physics.IsValid()) throw new ArgumentException("Invalid observation world");
            this.physics = physics;visibility=new NativeVisibilityQuery(physics);
            eyeHeight = movement.Get("camera.eyeHeight");
            targetHeight = movement.Get("player.capsule.height") * combat.Get("zone.torsoY");
        }
        public NativeBotObservationFrame[] Observe(NativeBotPerception knowledge)
        {
            if (knowledge == null || knowledge.Count != roster.Count) throw new ArgumentException("Observer count mismatch");
            var frames = new NativeBotObservationFrame[roster.Count];
            // All sightings are collected before core memory/report processing.
            for (int observer = 0; observer < frames.Length; observer++)
            {
                var own = session.Life(observer);
                var frame = frames[observer] = new NativeBotObservationFrame { OwnLife = own.Life, Alive = !own.Dead };
                if (own.Dead) continue;
                var pose = session.Pose(observer);
                Vector3 origin = pose.Position + Vector3.up * eyeHeight;
                Vector3 forward = Quaternion.Euler(0, pose.Yaw, 0) * Vector3.forward;
                var seen = new List<NativeBotSighting>();
                for (int target = 0; target < frames.Length; target++)
                {
                    if (target == observer || roster.AreAllies(observer, target)) continue;
                    var life = session.Life(target); if (life.Dead) continue;
                    var feet = session.Pose(target).Position;
                    Vector3 delta = feet + Vector3.up * targetHeight - origin;
                    Vector3 horizontal = new Vector3(delta.x, 0, delta.z);
                    // Horizontal FOV preserves the approved bot contract; vertical LOS still tests slabs.
                    if (Vector3.Angle(forward, horizontal) > knowledge.FieldOfView(observer) * .5f) continue;
                    if (visibility.Query(origin,delta)!=NativeVisibilityResult.Clear) continue;
                    // Same origin and world mask as combat. Transparent armor remains a shot blocker.
                    bool blocked=physics.Raycast(origin,delta.normalized,out _,delta.magnitude,
                        1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore);
                    seen.Add(new NativeBotSighting(target, life.Life, feet, life.Health, life.Armor){ShotBlocked=blocked});
                }
                frame.Direct = seen.ToArray();
            }
            return frames;
        }
    }
}
