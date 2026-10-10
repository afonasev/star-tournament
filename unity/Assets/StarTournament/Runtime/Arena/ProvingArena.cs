using Unity.AI.Navigation;
using System.Collections;
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
        readonly List<Collider> rocketGrating=new List<Collider>();
        readonly Dictionary<Collider,string> supports=new Dictionary<Collider,string>();
        NativeNavigationTransition[] navigationTransitions;
        public float SupportPitch(Collider support,Vector3 normal,Vector3 forward)
        {
            // Continuous ramps have a real inclined floor: preserve its exact plane.
            if(normal.x!=0f||normal.z!=0f)return CharacterMotor.SurfacePitch(normal,forward);
            var id=NavigationSupport(support);
            if(navigationTransitions!=null)foreach(var transition in navigationTransitions)
            {
                if(transition.Id!=id)continue;
                var feet=transition.OrderedFeet;
                if(feet==null||feet.Length<2)break;
                // Authored transition endpoints define the whole march, independent of tread normals.
                var delta=feet[feet.Length-1]-feet[0];
                var horizontal=new Vector3(delta.x,0,delta.z);
                if(horizontal.sqrMagnitude<=0)break;
                var gradient=horizontal*(delta.y/horizontal.sqrMagnitude);
                return -Mathf.Atan(Vector3.Dot(gradient,forward))*Mathf.Rad2Deg;
            }
            return CharacterMotor.SurfacePitch(normal,forward);
        }
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
        // Canonical full-volume supports, independent of presentation bars/gaps. Other
        // movement-only surfaces stay shot-transparent; this query is exclusive to Pulse.
        public bool RaycastRocketGrating(Vector3 origin, Vector3 direction, float distance, out RaycastHit hit)
        {
            hit=default;bool found=false;var ray=new Ray(origin,direction);
            foreach(var collider in rocketGrating)
                if(collider && collider.enabled && collider.Raycast(ray,out var contact,distance))
                {hit=contact;distance=contact.distance;found=true;}
            return found;
        }
        Material floor, wall, accent;
        public void ClearProjection()
        {
            if (Surface) { if(Surface.navMeshData)NavMeshBuilder.Cancel(Surface.navMeshData); Surface.RemoveData(); if (Surface.navMeshData) DestroyImmediate(Surface.navMeshData); DestroyImmediate(Surface); Surface=null; }
            for(int i=transform.childCount-1;i>=0;i--) DestroyImmediate(transform.GetChild(i).gameObject);
            if(floor)DestroyImmediate(floor);if(wall)DestroyImmediate(wall);if(accent)DestroyImmediate(accent);
            supports.Clear(); rocketGrating.Clear(); navigationTransitions=System.Array.Empty<NativeNavigationTransition>(); Spawns=System.Array.Empty<Vector3>();
        }
        public void Build(ProvingProfile profile)
        {
            Build(CombatBowlCatalog.Freeze(profile),profile);
        }
        public void Build(ArenaFreezeSnapshot frozen, ProvingProfile profile, ProvingProfile artProfile=null)
        { var steps=BuildSteps(frozen,profile,artProfile,false);while(steps.MoveNext()){} }
        public IEnumerator BuildSteps(ArenaFreezeSnapshot frozen, ProvingProfile profile, ProvingProfile artProfile=null,bool asynchronous=true)
        {
            if(frozen==null) throw new System.ArgumentException("ARENA_FREEZE_MISSING_INPUT|family:unknown|element:snapshot");
            ClearProjection();
            var definition=frozen.Definition; frozen.RequireDefinition(definition); Definition=definition; FrozenSnapshot=frozen; supports.Clear();
            floor = Material(new Color32(24, 36, 55, 255));
            wall = Material(new Color32(202, 214, 218, 255));
            accent = Material(new Color32(31, 188, 196, 255));
            int projected=0;
            foreach(var solid in Definition.Solids){if(asynchronous&&projected++%32==0)yield return null;var go=Box(solid.Id,solid.Position,solid.Size,solid.Material=="floor"?floor:solid.Material=="wall"?wall:accent,solid.Layer);go.transform.rotation=solid.Rotation;var collider=go.GetComponent<Collider>();if(solid.Surface=="lunar-glass")go.AddComponent<NativeSightTransparent>();if(!string.IsNullOrEmpty(solid.Support))supports[collider]=solid.Support;if(solid.Surface=="grating")rocketGrating.Add(collider);}
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
            // This map has many sharp corridor corners: a named clearance margin keeps baked corners outside the physical capsule.
            if(Definition.MapId==LunarLaboratoryCatalog.Id)
            {
                settings.agentRadius+=(artProfile??ProvingProfile.CreateLunarPresentation()).Get("lunar.navigationClearance");
                // Eight voxels per physical capsule radius prevent stair/window quantization from inventing shortcuts.
                settings.overrideVoxelSize=true;settings.voxelSize=profile.Get("player.capsule.radius")/8;
            }
            settings.agentClimb=profile.Get("player.movement.stepOffset"); settings.agentSlope=profile.Get("player.movement.slopeLimitDegrees");
            var sources=new List<NavMeshBuildSource>();
            NavMeshBuilder.CollectSources(transform,(1<<WorldLayer)|(1<<MovementOnlyLayer),NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
            var all=Definition.Solids; var bakeBounds=new Bounds(all[0].Position,all[0].Size);foreach(var solid in all)bakeBounds.Encapsulate(new Bounds(solid.Position,solid.Size)); bakeBounds.Expand(2f);
            if(asynchronous)
            {
                Surface.navMeshData=new NavMeshData(settings.agentTypeID);
                yield return NavMeshBuilder.UpdateNavMeshDataAsync(Surface.navMeshData,settings,sources,bakeBounds);
            }
            else Surface.navMeshData=NavMeshBuilder.BuildNavMeshData(settings,sources,bakeBounds,Vector3.zero,Quaternion.identity);
            Surface.AddData();
            if(Definition.Identity==CombatBowlCatalog.Identity)
            {
                var decoration=new GameObject("Orbital League presentation");decoration.transform.SetParent(transform,false);
                var steps=decoration.AddComponent<OrbitalLeaguePresentation>().BuildSteps(Definition,artProfile??ProvingProfile.CreateCombatBowlRingPresentationDefault());
                while(steps.MoveNext()){if(asynchronous)yield return steps.Current;}
            }
            else if(Definition.Identity==LunarLaboratoryCatalog.Identity)
            {
                var decoration=new GameObject("Lunar laboratory presentation");decoration.transform.SetParent(transform,false);
                var steps=decoration.AddComponent<LunarLaboratoryPresentation>().BuildSteps(Definition,artProfile??ProvingProfile.CreateLunarPresentation());
                while(steps.MoveNext()){if(asynchronous)yield return steps.Current;}
            }
            else if(Definition.Identity==IndustrialTunnelsCatalog.Identity)
            {
                var decoration=new GameObject("Industrial tunnels presentation");decoration.transform.SetParent(transform,false);
                var steps=decoration.AddComponent<IndustrialTunnelsPresentation>().BuildSteps(Definition,artProfile??ProvingProfile.CreateIndustrialTunnelsPresentation());
                while(steps.MoveNext()){if(asynchronous)yield return steps.Current;}
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
            if (Surface) { if(Surface.navMeshData)NavMeshBuilder.Cancel(Surface.navMeshData); Surface.RemoveData(); if (Surface.navMeshData) Destroy(Surface.navMeshData); }
            if (floor) Destroy(floor); if (wall) Destroy(wall); if (accent) Destroy(accent);
        }
    }
}
