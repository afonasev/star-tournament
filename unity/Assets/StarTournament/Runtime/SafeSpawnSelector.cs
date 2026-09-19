using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace StarTournament.ProvingGround
{
    /// <summary>Queries current physics and navigation at readiness, never a cached safety score.</summary>
    public sealed class SafeSpawnSelector
    {
        readonly PhysicsScene physics;
        readonly ProvingArena arena;
        readonly ProvingProfile motor, combat;
        readonly Collider[] overlap = new Collider[1]; // Any overlap rejects the candidate; enumeration is unnecessary.
        readonly List<Vector3> slots = new List<Vector3>();
        public IReadOnlyList<Vector3> Slots => slots;
        public SafeSpawnSelector(PhysicsScene physics, ProvingArena arena, ProvingProfile motor, ProvingProfile combat)
        {
            this.physics = physics; this.arena = arena; this.motor = motor; this.combat = combat;
            float w = motor.Get("fixture.width"), d = motor.Get("fixture.depth");
            float inset = combat.Get("spawn.inset"), spacing = combat.Get("spawn.spacing");
            // Four authored corner regions, each with a 2x2 slot grid, are fixture topology.
            for (int floor = 0; floor < 2; floor++)
                for (int side = -1; side <= 1; side += 2)
                    for (int x = 0; x < 2; x++)
                        for (int z = 0; z < 2; z++)
                            slots.Add(new Vector3(side * (w/2 - inset - x*spacing),
                                motor.Get(floor == 0 ? "fixture.floorHeight" : "fixture.upperFloorHeight"),
                                floor == 0 ? -d/2 + inset + z*spacing : d/2 - inset - z*spacing));
            slots.AddRange(arena.Spawns);
        }
        public bool Valid(Vector3 authored, out Vector3 position)
        {
            position = default;
            float tolerance = combat.Get("spawn.floorTolerance"), sample = combat.Get("spawn.sample");
            int movementMask = (1 << ProvingArena.WorldLayer) | (1 << ProvingArena.MovementOnlyLayer) | (1 << ProvingArena.ParticipantLayer);
            if (!physics.Raycast(authored + Vector3.up*tolerance, Vector3.down, out var support, tolerance*2,
                    (1 << ProvingArena.WorldLayer) | (1 << ProvingArena.MovementOnlyLayer), QueryTriggerInteraction.Ignore)) return false;
            if (Vector3.Angle(support.normal, Vector3.up) > motor.Get("player.movement.slopeLimitDegrees")) return false;
            if (!NavMesh.SamplePosition(support.point, out var nav, sample, NavMesh.AllAreas) || Mathf.Abs(nav.position.y - authored.y) > tolerance) return false;
            // Keep the physical support point: NavMesh is only evidence of walkable connectivity,
            // not permission to move a capsule through a wall or to a different floor.
            position = support.point + Vector3.up * motor.Get("player.capsule.skinWidth");
            float radius = motor.Get("player.capsule.radius"), height = motor.Get("player.capsule.height");
            return physics.OverlapCapsule(position + Vector3.up*radius, position + Vector3.up*(height-radius), radius,
                overlap, movementMask, QueryTriggerInteraction.Ignore) == 0;
        }
        public bool TryChoose(CombatTarget[] live, out Vector3 selected)
        {
            selected = default; bool found = false, bestHidden = false; float bestDistance = -1;
            foreach (Vector3 slot in slots)
            {
                if (!Valid(slot, out var candidate)) continue;
                bool hidden = true; float minimum = float.PositiveInfinity;
                foreach (var enemy in live)
                {
                    Vector3 eye = enemy.Pose.Position + Vector3.up*motor.Get("camera.eyeHeight");
                    Vector3 delta = candidate + Vector3.up*motor.Get("camera.eyeHeight") - eye;
                    if (!physics.Raycast(eye, delta.normalized, out _, delta.magnitude, 1 << ProvingArena.WorldLayer, QueryTriggerInteraction.Ignore)) hidden = false;
                    // Ground airborne opponents before a small NavMesh query. A failed route is
                    // zero safety, never infinite distance or permission to invent a connection.
                    Vector3 endpoint = enemy.Pose.Position;
                    if (physics.Raycast(eye, Vector3.down, out var ground, motor.Get("fixture.upperFloorHeight") + motor.Get("fixture.wallHeight"),
                        1 << ProvingArena.WorldLayer, QueryTriggerInteraction.Ignore)) endpoint = ground.point;
                    float distance = 0;
                    if (arena.TryRoute(candidate, endpoint, combat.Get("spawn.sample"), out var path))
                        for (int k = 1; k < path.corners.Length; k++) distance += Vector3.Distance(path.corners[k-1], path.corners[k]);
                    minimum = Mathf.Min(minimum, distance);
                }
                if (!found || (hidden && !bestHidden) || (hidden == bestHidden && minimum > bestDistance))
                { found = true; bestHidden = hidden; bestDistance = minimum; selected = candidate; }
            }
            return found;
        }
    }
}
