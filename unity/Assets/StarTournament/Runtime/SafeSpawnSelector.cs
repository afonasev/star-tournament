using System.Collections.Generic;
using System.Linq;
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
        public int InitialSearchNodes { get; private set; }
        public string InitialFailure { get; private set; }
        // Complete placement is committed by the caller only after this bounded search succeeds.
        public bool TryInitial(NativeMatchRoster roster,float separation,out Vector3[] selected,int searchBudget=0,int candidateBudget=0)
        {
            if(roster==null || float.IsNaN(separation) || float.IsInfinity(separation) || separation<0)
                throw new System.ArgumentException("Invalid initial allocation");
            if(searchBudget==0)searchBudget=(int)ProvingProfile.CreateRosterDefault().Get("spawn.initialSearchBudgetNodes");
            if(searchBudget<1)throw new System.ArgumentOutOfRangeException(nameof(searchBudget));
            if(candidateBudget==0)candidateBudget=(int)ProvingProfile.CreateRosterDefault().Get("spawn.initialCandidateBudget");
            if(candidateBudget<1)throw new System.ArgumentOutOfRangeException(nameof(candidateBudget));
            InitialSearchNodes=0;InitialFailure=null;selected=System.Array.Empty<Vector3>();
            var candidates=new List<Vector3>();
            // Initial-only coverage of the fixed fixture, using existing profile geometry/inset/spacing.
            // Corner regions alone cannot satisfy eight-way FFA separation; never reduce that separation.
            var initialSlots=new List<Vector3>(slots);
            float inset=combat.Get("spawn.inset"),step=Mathf.Max(combat.Get("spawn.spacing"),separation);
            float halfWidth=motor.Get("fixture.width")/2-inset,halfDepth=motor.Get("fixture.depth")/2-inset;
            for(int floor=0;floor<2;floor++)
                for(float x=-halfWidth;x<=halfWidth;x+=step)
                    for(float z=-halfDepth;z<=halfDepth;z+=step)
                    {
                        if(initialSlots.Count>=candidateBudget){InitialFailure="CandidateBudgetExceeded";return false;}
                        initialSlots.Add(new Vector3(x,motor.Get(floor==0?"fixture.floorHeight":"fixture.upperFloorHeight"),z));
                    }
            foreach(var slot in initialSlots)if(Valid(slot,out var candidate)&&!candidates.Contains(candidate))candidates.Add(candidate);
            int n=candidates.Count;var overlapPair=new bool[n,n];var closePair=new bool[n,n];
            float radius=motor.Get("player.capsule.radius")+motor.Get("player.capsule.skinWidth");
            for(int a=0;a<n;a++)for(int b=0;b<n;b++)
            {
                var delta=candidates[a]-candidates[b];
                float gap=Mathf.Max(0,Mathf.Abs(delta.y)-(motor.Get("player.capsule.height")-2*motor.Get("player.capsule.radius")));
                overlapPair[a,b]=delta.x*delta.x+delta.z*delta.z+gap*gap<4*radius*radius;
                closePair[a,b]=delta.magnitude<separation;
            }
            var reserved=new int[roster.Count];bool exhausted=false;
            bool Compatible(int participant,int candidate,int prefix)
            {
                for(int j=0;j<prefix;j++)if(overlapPair[candidate,reserved[j]] ||
                    (!roster.AreAllies(participant,j)&&closePair[candidate,reserved[j]]))return false;
                return true;
            }
            bool Place(int participant)
            {
                if(participant==reserved.Length)return true;
                for(int c=0;c<n;c++)
                {
                    if(InitialSearchNodes>=searchBudget){exhausted=true;return false;}
                    InitialSearchNodes++;
                    if(!Compatible(participant,c,participant))continue;
                    reserved[participant]=c;bool viable=true;
                    for(int future=participant+1;future<reserved.Length&&viable;future++)
                    {
                        bool any=false;for(int k=0;k<n;k++)if(Compatible(future,k,participant+1)){any=true;break;}
                        viable=any;
                    }
                    if(viable&&Place(participant+1))return true;
                    if(exhausted)return false;
                }
                return false;
            }
            if(Place(0)){selected=reserved.Select(i=>candidates[i]).ToArray();return true;}
            InitialFailure=exhausted?"SearchBudgetExceeded":"NoValidPlacement";return false;
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
