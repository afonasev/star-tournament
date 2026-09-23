using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

namespace StarTournament.ProvingGround
{
    [System.Serializable] public sealed class NativeNavigationTransition
    {
        public string Id, LowerSupport, UpperSupport;
        public Vector3[] OrderedFeet;
    }
    public sealed class ProvingArena : MonoBehaviour
    {
        // Layer numbers are a technical query protocol, not tunable balance.
        public const int WorldLayer = 8, MovementOnlyLayer = 9, ParticipantLayer = 10;
        public const int ShotMask = (1 << WorldLayer) | (1 << ParticipantLayer);
        public Vector3[] Spawns { get; private set; }
        public Vector3 LowerRoutePoint { get; private set; }
        public Vector3 UpperRoutePoint { get; private set; }
        public NavMeshSurface Surface { get; private set; }
        public ArenaDefinition Definition { get; private set; }
        public ArenaFreezeSnapshot FrozenSnapshot { get; private set; }
        readonly Dictionary<Collider,string> supports=new Dictionary<Collider,string>();
        NativeNavigationTransition[] navigationTransitions;
        public NativeNavigationTransition[] ReadNavigationTransitions()
        {
            var copy=new NativeNavigationTransition[navigationTransitions.Length];
            for(int i=0;i<copy.Length;i++)
            {
                var t=navigationTransitions[i];copy[i]=new NativeNavigationTransition{Id=t.Id,LowerSupport=t.LowerSupport,UpperSupport=t.UpperSupport,OrderedFeet=(Vector3[])t.OrderedFeet.Clone()};
            }
            return copy;
        }
        public string NavigationSupport(Collider collider)
        {
            return collider && collider.transform.IsChildOf(transform) && supports.TryGetValue(collider,out var support) ? support : null;
        }
        Material floor, wall, accent;
        public void ClearProjection()
        {
            if (Surface) { Surface.RemoveData(); if (Surface.navMeshData) DestroyImmediate(Surface.navMeshData); DestroyImmediate(Surface); Surface=null; }
            for(int i=transform.childCount-1;i>=0;i--) DestroyImmediate(transform.GetChild(i).gameObject);
            if(floor)DestroyImmediate(floor);if(wall)DestroyImmediate(wall);if(accent)DestroyImmediate(accent);
            supports.Clear(); navigationTransitions=System.Array.Empty<NativeNavigationTransition>(); Spawns=System.Array.Empty<Vector3>();
        }
        public void Build(ProvingProfile profile)
        {
            Build(ArenaGenerator.Freeze(new ArenaGenerationRequest{Seed=1,GeneratorVersion=ArenaGenerator.FoundationVersion,Profile=ProvingProfile.CreateArenaFoundationDefault()}),profile);
        }
        public void Build(ArenaFreezeSnapshot frozen, ProvingProfile profile, ProvingProfile artProfile=null)
        {
            if(frozen==null) throw new System.ArgumentException("ARENA_FREEZE_MISSING_INPUT|family:unknown|element:snapshot");
            ClearProjection();
            var definition=frozen.Definition; frozen.RequireDefinition(definition); Definition=definition; FrozenSnapshot=frozen; supports.Clear();
            floor = Material(new Color32(24, 36, 55, 255));
            wall = Material(new Color32(202, 214, 218, 255));
            accent = Material(new Color32(31, 188, 196, 255));
            foreach(var solid in Definition.Solids){var go=Box(solid.Id,solid.Position,solid.Size,solid.Material=="floor"?floor:solid.Material=="wall"?wall:accent,solid.Layer);go.transform.rotation=solid.Rotation;if(!string.IsNullOrEmpty(solid.Support))supports[go.GetComponent<Collider>()]=solid.Support;}
            navigationTransitions=Definition.Transitions;Spawns=(Vector3[])Definition.Spawns.Clone();LowerRoutePoint=Definition.RouteAnchors[0];UpperRoutePoint=Definition.RouteAnchors[1];
            Physics.SyncTransforms();
            Surface = gameObject.AddComponent<NavMeshSurface>();
            Surface.collectObjects = CollectObjects.Children;
            Surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            // Keep Unity's baker, but give it the same capsule/step/slope contract as the motor.
            var settings=Surface.GetBuildSettings();
            // Required for stacked floors/stairs: corner interpolation alone is not the physical height field.
            settings.buildHeightMesh=true;
            settings.agentRadius=profile.Get("player.capsule.radius"); settings.agentHeight=profile.Get("player.capsule.height");
            settings.agentClimb=profile.Get("player.movement.stepOffset"); settings.agentSlope=profile.Get("player.movement.slopeLimitDegrees");
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(transform,(1<<WorldLayer)|(1<<MovementOnlyLayer),NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var all=Definition.Solids; var bakeBounds=new Bounds(all[0].Position,all[0].Size);foreach(var solid in all)bakeBounds.Encapsulate(new Bounds(solid.Position,solid.Size)); bakeBounds.Expand(2f);
            Surface.navMeshData=NavMeshBuilder.BuildNavMeshData(settings,sources,bakeBounds,Vector3.zero,Quaternion.identity);
            Surface.AddData();
            if(Definition.Identity==CombatBowlCatalog.Identity)
            {
                var decoration=new GameObject("Orbital League presentation");decoration.transform.SetParent(transform,false);
                decoration.AddComponent<OrbitalLeaguePresentation>().Build(Definition,artProfile??ProvingProfile.CreateOrbitalLeagueDefault());
            }
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
            if (Surface) { Surface.RemoveData(); if (Surface.navMeshData) Destroy(Surface.navMeshData); }
            if (floor) Destroy(floor); if (wall) Destroy(wall); if (accent) Destroy(accent);
        }
    }
}
