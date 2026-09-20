using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace StarTournament.ProvingGround
{
    /// <summary>Native route capability scoped to one physical fixture; no participant knowledge.</summary>
    public sealed class NativeNavigationProvider : INativeNavigation
    {
        readonly ProvingArena arena;
        readonly PhysicsScene physics;
        readonly float sampleDistance, heightTolerance, step, radius, height, skin;
        readonly NavMeshPath path = new NavMeshPath();
        readonly Collider[] overlaps = new Collider[16]; // Bounded query buffer; overflow rejects conservatively.
        readonly NativeNavigationTransition[] transitions;
        public string Identity { get; }
        public NativeNavigationProvider(ProvingArena arena, ProvingProfile movement, ProvingProfile navigation)
        {
            this.arena = arena ? arena : throw new ArgumentNullException(nameof(arena));
            if (movement == null || movement.Validate().Count != 0 || navigation == null || navigation.Validate().Count != 0)
                throw new ArgumentException("Invalid navigation geometry profiles");
            physics = arena.gameObject.scene.GetPhysicsScene();
            transitions = arena.ReadNavigationTransitions();
            sampleDistance = navigation.Get("bots.navigation.sampleDistance"); heightTolerance = navigation.Get("bots.navigation.heightTolerance");
            step = movement.Get("player.movement.stepOffset"); radius = movement.Get("player.capsule.radius");
            height = movement.Get("player.capsule.height"); skin = movement.Get("player.capsule.skinWidth");
            if(arena.Definition==null) throw new ArgumentException("Arena definition is required");
            Identity = arena.Definition.Identity + "|movement:" + ArenaGenerator.Fingerprint(movement);
        }
        public bool TryRoute(Vector3 from, Vector3 goal, out NativeNavigationPoint[] points, out string failure)
        {
            points = Array.Empty<NativeNavigationPoint>(); failure = "Invalid endpoint or support";
            if (!arena || !arena.Surface || !arena.Surface.isActiveAndEnabled || !arena.Surface.navMeshData ||
                !Finite(from) || !Finite(goal) || !TrySample(from, out var start) || !TrySample(goal, out var end)) return false;
            if (!NavMesh.CalculatePath(start.Position, end.Position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            { failure = "No complete route"; return false; }
            var corners = path.corners;
            if (corners.Length == 0) return false;
            var result = new List<NativeNavigationPoint>();
            string previousSupport = start.Support;
            // Dense validation catches undeclared vertical shortcuts between sparse native corners.
            // Spacing derives from the existing capsule; it is not a second navigation tuning source.
            for (int i = 0; i < corners.Length; i++)
            {
                Vector3 previous = i == 0 ? start.Position : corners[i - 1];
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, corners[i]) / radius));
                NativeNavigationPoint validated = default;
                for (int sample = 1; sample <= samples; sample++)
                {
                    var position = Vector3.Lerp(previous, corners[i], sample / (float)samples);
                    if (!TryPhysicalSupport(position, out validated, true) || !Connected(previousSupport, validated.Support))
                    { failure = "Invalid route support at " + position + " from " + previousSupport + " to " + validated.Support; return false; }
                    if (validated.Support.StartsWith("transition:", StringComparison.Ordinal) || validated.Support != previousSupport)
                        result.Add(validated);
                    previousSupport = validated.Support;
                }
                result.Add(validated);
            }
            if (!Connected(previousSupport, end.Support)) { failure = "Wrong final support"; return false; }
            // Physical floor height, not the slightly elevated navmesh polygon, is the feet target.
            result[result.Count - 1] = end;
            points = result.ToArray(); failure = null; return true;
        }
        bool TrySample(Vector3 feet, out NativeNavigationPoint point)
        {
            point = default;
            if (!TryPhysicalSupport(feet, out var requested)) return false;
            if (!NavMesh.SamplePosition(feet, out var hit, sampleDistance, NavMesh.AllAreas) || Mathf.Abs(hit.position.y - feet.y) > heightTolerance + step)
                return false;
            if (!TryPhysicalSupport(hit.position, out point, true) || point.Support != requested.Support || Mathf.Abs(point.Position.y - requested.Position.y) > heightTolerance) return false;
            return true;
        }
        public bool TryLocate(Vector3 feet, out NativeNavigationPoint point) => TryPhysicalSupport(feet, out point);
        public bool ValidTransition(string transition, string exitSupport)
        {
            foreach(var value in transitions) if(value.Id==transition&&(value.LowerSupport==exitSupport||value.UpperSupport==exitSupport))return true;
            return false;
        }
        public bool TryPhysicalSupport(Vector3 feet, out NativeNavigationPoint point, bool navMeshProjection = false)
        {
            point = default;
            if (!arena || !Finite(feet)) return false;
            float lift = step + heightTolerance;
            float tolerance = heightTolerance + (navMeshProjection ? step : 0);
            if (!physics.Raycast(feet + Vector3.up * lift, Vector3.down, out var hit, lift + tolerance,
                (1 << ProvingArena.WorldLayer) | (1 << ProvingArena.MovementOnlyLayer), QueryTriggerInteraction.Ignore)) return false;
            // Do not accept a nearest point on another simultaneously loaded arena or an obstacle roof.
            if (!hit.collider.transform.IsChildOf(arena.transform) || Mathf.Abs(hit.point.y - feet.y) > tolerance) return false;
            string support = arena.NavigationSupport(hit.collider);
            if (support == null) return false;
            // Static capsule headroom only; living blockers stay a motor/recovery concern.
            int count = physics.OverlapCapsule(hit.point + Vector3.up * (radius + skin), hit.point + Vector3.up * (height - radius),
                radius - skin, overlaps, (1 << ProvingArena.WorldLayer) | (1 << ProvingArena.MovementOnlyLayer), QueryTriggerInteraction.Ignore);
            if (count == overlaps.Length) return false;
            for (int i = 0; i < count; i++)
            {
                // Stair/ramp support naturally touches a standing capsule on its ascending side.
                var otherSupport = arena.NavigationSupport(overlaps[i]);
                if (overlaps[i] == hit.collider || otherSupport == support || (otherSupport != null && otherSupport.StartsWith("transition:",StringComparison.Ordinal))) continue;
                if (overlaps[i].bounds.max.y <= hit.point.y + step) continue;
                return false;
            }
            point = new NativeNavigationPoint(new Vector3(feet.x, hit.point.y, feet.z), support); return true;
        }
        bool Connected(string from, string to)
        {
            if (from == to) return true;
            foreach(var transition in transitions)
                if ((from==transition.Id&&(to==transition.LowerSupport||to==transition.UpperSupport)) ||
                    (to==transition.Id&&(from==transition.LowerSupport||from==transition.UpperSupport)))return true;
            return false;
        }
        static bool Finite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
    }
}
