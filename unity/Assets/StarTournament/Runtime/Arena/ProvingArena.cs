using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace StarTournament.ProvingGround
{
    public sealed class ProvingArena : MonoBehaviour
    {
        // Layer numbers are a technical query protocol, not tunable balance.
        public const int WorldLayer = 8, MovementOnlyLayer = 9, ParticipantLayer = 10;
        public const int ShotMask = (1 << WorldLayer) | (1 << ParticipantLayer);
        public Vector3[] Spawns { get; private set; }
        public Vector3 LowerRoutePoint { get; private set; }
        public Vector3 UpperRoutePoint { get; private set; }
        public NavMeshSurface Surface { get; private set; }
        Material floor, wall, accent;
        public void Build(ProvingProfile profile)
        {
            // Fixed semantic palette of clean-future-sport. Dimensions remain profile-owned.
            floor = Material(new Color32(24, 36, 55, 255));
            wall = Material(new Color32(202, 214, 218, 255));
            accent = Material(new Color32(31, 188, 196, 255));
            float w = profile.Get("fixture.width"), d = profile.Get("fixture.depth");
            float ground = profile.Get("fixture.floorHeight"), h = profile.Get("fixture.upperFloorHeight");
            float ft = profile.Get("fixture.floorThickness"), wt = profile.Get("fixture.wallThickness"), wh = profile.Get("fixture.wallHeight");
            float rw = profile.Get("fixture.rampWidth"), run = profile.Get("fixture.rampLength");
            Box("floor-lower", new Vector3(0, ground - ft / 2, 0), new Vector3(w, ft, d), floor);
            Box("floor-upper", new Vector3(0, h - ft / 2, d / 4), new Vector3(w, ft, d / 2), floor);
            Box("north-wall", new Vector3(0, (h + wh) / 2, d / 2), new Vector3(w, h + wh, wt), wall);
            Box("south-wall", new Vector3(0, wh / 2, -d / 2), new Vector3(w, wh, wt), wall);
            Box("west-wall", new Vector3(-w / 2, (h + wh) / 2, 0), new Vector3(wt, h + wh, d), wall);
            Box("east-wall", new Vector3(w / 2, (h + wh) / 2, 0), new Vector3(wt, h + wh, d), wall);
            Box("upper-ceiling", new Vector3(0, h + wh + ft / 2, d / 4), new Vector3(w, ft, d / 2), wall);
            var ramp = Box("ramp", new Vector3(w / 2 - rw / 2 - wt, (h + ground) / 2 - ft / 2, -run / 2),
                new Vector3(rw, ft, Mathf.Sqrt(run * run + (h-ground)*(h-ground))), accent);
            ramp.transform.rotation = Quaternion.Euler(-Mathf.Atan2(h - ground, run) * Mathf.Rad2Deg, 0, 0);
            int steps = Mathf.CeilToInt((h - ground) / profile.Get("fixture.stairRise"));
            float rise = (h - ground) / steps, tread = profile.Get("fixture.stairTread");
            for (int i = 0; i < steps; i++)
            {
                float top = ground + (i + 1) * rise;
                Box("stair-" + i, new Vector3(-w / 2 + rw / 2 + wt, (top + ground) / 2, -(steps-i)*tread+tread/2),
                    new Vector3(rw, top-ground, tread), accent);
            }
            Box("headroom-probe", new Vector3(0, ground + profile.Get("fixture.headroomHeight") + ft / 2, -d / 4), new Vector3(rw, ft, rw), wall);
            Box("movement-only-barrier", new Vector3(-rw, ground + wh / 4, -d / 4), new Vector3(wt, wh / 2, rw), accent, MovementOnlyLayer);
            Box("solid-window-probe", new Vector3(rw, ground + wh / 2, -d / 4), new Vector3(wt, wh, rw), wall);
            Spawns = new Vector3[SeatInputCoordinator.SeatCount];
            // Stagger the fixed fixture slots so neighbouring robots are visible in the comparison views.
            for (int i = 0; i < Spawns.Length; i++) Spawns[i] = new Vector3((i - (Spawns.Length-1)/2f) * rw, ground, -d/2 + rw/2 + (i%2)*rw);
            LowerRoutePoint = new Vector3(0, ground, -d/2 + rw/2);
            UpperRoutePoint = new Vector3(0, h, d/4);
            Physics.SyncTransforms();
            Surface = gameObject.AddComponent<NavMeshSurface>();
            Surface.collectObjects = CollectObjects.Children;
            Surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Keep Unity's baker, but give it the same capsule/step/slope contract as the motor.
            var settings=Surface.GetBuildSettings();
            settings.agentRadius=profile.Get("player.capsule.radius"); settings.agentHeight=profile.Get("player.capsule.height");
            settings.agentClimb=profile.Get("player.movement.stepOffset"); settings.agentSlope=profile.Get("player.movement.slopeLimitDegrees");
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(transform,(1<<WorldLayer)|(1<<MovementOnlyLayer),NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var bakeBounds=new Bounds(Vector3.up*(h+wh)/2,new Vector3(w+2*wt,h+wh+2*ft,d+2*wt));
            Surface.navMeshData=NavMeshBuilder.BuildNavMeshData(settings,sources,bakeBounds,Vector3.zero,Quaternion.identity);
            Surface.AddData();
        }
        public bool TryRoute(Vector3 from, Vector3 to, float sampleDistance, out NavMeshPath path)
        {
            path = new NavMeshPath();
            return NavMesh.SamplePosition(from, out var start, sampleDistance, NavMesh.AllAreas) &&
                NavMesh.SamplePosition(to, out var end, sampleDistance, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
        static Material Material(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.color = color; return material;
        }
        GameObject Box(string id, Vector3 position, Vector3 size, Material material, int layer = WorldLayer)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = id; go.layer = layer;
            go.transform.SetParent(transform, false); go.transform.position = position; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = material; return go;
        }
        void OnDestroy()
        {
            if (floor) Destroy(floor); if (wall) Destroy(wall); if (accent) Destroy(accent);
        }
    }
}
