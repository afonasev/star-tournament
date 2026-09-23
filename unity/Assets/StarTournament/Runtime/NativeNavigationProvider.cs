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
            if(arena.Definition==null || arena.FrozenSnapshot==null) throw new ArgumentException("ARENA_FREEZE_MISSING_INPUT|family:unknown|element:navigation");
            arena.FrozenSnapshot.RequireDefinition(arena.Definition);
            Identity = arena.Definition.Identity + "|movement:" + ProfileFingerprintUtility.Fingerprint(movement);
        }
        public bool TryRoute(Vector3 from, Vector3 goal, out NativeNavigationPoint[] points, out string failure)
        {
            points = Array.Empty<NativeNavigationPoint>(); failure = "Invalid endpoint or support";
            if (!arena || !arena.Surface || !arena.Surface.isActiveAndEnabled || !arena.Surface.navMeshData ||
                !Finite(from) || !Finite(goal) || !TrySample(from, out var start) || !TrySample(goal, out var end)) return false;
            if (!NavMesh.CalculatePath(start.Position, end.Position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete)
            { failure = "No complete route"; return false; }
            // The baker may project an endpoint sideways near a ramp. Keep the requested, physically
            // validated endpoint so the motor does not repeatedly arrive at a different XZ position.
            if(!TryPhysicalSupport(goal,out var requestedEnd)||requestedEnd.Support!=end.Support)return false;
            end=requestedEnd;
            var nativeCorners=path.corners;
            var corners=new Vector3[nativeCorners.Length+1];Array.Copy(nativeCorners,corners,nativeCorners.Length);corners[corners.Length-1]=end.Position;
            if (corners.Length == 0) return false;
            var result = new List<NativeNavigationPoint>();
            var trace = new List<NativeNavigationPoint>();
            string previousSupport = start.Support;float physicalHeight=start.Position.y;
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
                    // Walk the physical height field from the last proven feet height, in capsule-sized
                    // horizontal samples. NavMesh corners may interpolate above an entire staircase.
                    // Each step still requires the existing bounded owned ray, capsule headroom,
                    // connected support and independently validated declared transition feet.
                    position.y=physicalHeight;
                    if (!TryPhysicalSupport(position, out validated, true) || !Connected(previousSupport, validated.Support))
                    { failure = "Invalid route support at " + position + " from " + previousSupport + " to " + validated.Support; return false; }
                    physicalHeight=validated.Position.y;trace.Add(validated);
                    // Dense samples prove physical continuity; steering needs only support boundaries and native corners.
                    if (validated.Support != previousSupport)
                        result.Add(validated);
                    previousSupport = validated.Support;
                }
                result.Add(validated);
            }
            if (!Connected(previousSupport, end.Support)) { failure = "Wrong final support"; return false; }
            if (!ValidateDeclaredTransitions(trace, start.Support, end.Support, out failure)) return false;
            // Physical floor height, not the slightly elevated navmesh polygon, is the feet target.
            result[result.Count - 1] = end;
            // A duplicated last corner disables final-point braking and can make the motor oscillate.
            for(int i=result.Count-1;i>0;i--)if(result[i].Support==result[i-1].Support&&result[i].Position==result[i-1].Position)result.RemoveAt(i);
            points = result.ToArray(); failure = null; return true;
        }
        bool ValidateDeclaredTransitions(List<NativeNavigationPoint> trace, string startSupport, string endSupport, out string failure)
        {
            failure = null;
            string previous = startSupport;
            for (int index = 0; index < trace.Count;)
            {
                string support = trace[index].Support;
                if (!support.StartsWith("transition:", StringComparison.Ordinal)) { previous = support; index++; continue; }
                int begin = index;
                while (index < trace.Count && trace[index].Support == support) index++;
                string next = index < trace.Count ? trace[index].Support : endSupport;
                NativeNavigationTransition transition = null;
                foreach (var candidate in transitions) if (candidate.Id == support) { transition = candidate; break; }
                if (transition == null) { failure = "Unknown declared transition " + support; return false; }
                // Replan/restore can begin while the capsule already stands on a declared transition. In that case the
                // physical start support has already been checked; only a declared exit is permitted.
                bool beginsInside = previous == support;
                if ((!beginsInside && !OppositeEnds(transition, previous, next)) ||
                    (beginsInside && next != transition.LowerSupport && next != transition.UpperSupport))
                { failure = "Wrong declared transition endpoints " + previous + " -> " + support + " -> " + next; return false; }
                if (!OrderedFeet(transition, trace, begin, index, previous)) { failure = "Invalid ordered feet " + support; return false; }
                previous = support;
            }
            return true;
        }
        static bool OppositeEnds(NativeNavigationTransition transition, string from, string to) =>
            (from == transition.LowerSupport && to == transition.UpperSupport) ||
            (from == transition.UpperSupport && to == transition.LowerSupport);
        bool OrderedFeet(NativeNavigationTransition transition, List<NativeNavigationPoint> trace, int begin, int end, string enteringSupport)
        {
            if (transition.OrderedFeet == null || transition.OrderedFeet.Length < 2 || begin >= end) return false;
            // Native corners can cross several physical stair treads in one straight segment. The trace proves every sampled
            // point has the declared collider support; every declared foot independently proves the physical transition boundary.
            float direction = Mathf.Sign(transition.OrderedFeet[transition.OrderedFeet.Length - 1].y - transition.OrderedFeet[0].y);
            if (direction == 0) return false;
            for (int i = 1; i < transition.OrderedFeet.Length; i++)
            {
                var previous = transition.OrderedFeet[i - 1]; var current = transition.OrderedFeet[i];
                if (!Finite(previous) || !Finite(current) || Vector3.Distance(previous, current) <= Mathf.Epsilon || direction * (current.y - previous.y) < 0) return false;
            }
            for (int i = 0; i < transition.OrderedFeet.Length; i++)
            {
                if (!TryPhysicalSupport(transition.OrderedFeet[i], out var located)) return false;
                bool endpoint = i == 0 || i == transition.OrderedFeet.Length - 1;
                string endpointSupport = i == 0 ? transition.LowerSupport : transition.UpperSupport;
                if (located.Support != transition.Id && (!endpoint || located.Support != endpointSupport)) return false;
            }
            return true;
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
