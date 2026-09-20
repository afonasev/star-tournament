using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    [Serializable] public sealed class ArenaGenerationRequest { public uint Seed; public string GeneratorVersion; public ProvingProfile Profile; }
    [Serializable] public sealed class ArenaSolid { public string Id, Support, Material; public Vector3 Position, Size; public Quaternion Rotation; public int Layer; }
    [Serializable] public sealed class ArenaSpawnRegion { public string Id, Support; public Vector3 Center, Size; }
    [Serializable] public sealed class ArenaDefinition
    {
        public uint Seed; public string GeneratorVersion, ProfileFingerprint, Identity;
        public ArenaSolid[] Solids; public Vector3[] Spawns, RouteAnchors;
        public ArenaSpawnRegion[] SpawnRegions; public NativeNavigationTransition[] Transitions;
        public ArenaDefinition Copy() => JsonUtility.FromJson<ArenaDefinition>(JsonUtility.ToJson(this));
    }

    /// <summary>Pure ARENA-1 generator; it deliberately consumes no match/session randomness.</summary>
    public static class ArenaGenerator
    {
        public const string FoundationVersion = "unity-two-level-fixture-v1";
        public static ArenaDefinition Generate(ArenaGenerationRequest request)
        {
            if (request == null || request.Profile == null || request.Profile.Validate().Count != 0) throw new ArgumentException("Invalid arena foundation profile");
            if (request.GeneratorVersion != FoundationVersion) throw new ArgumentException("Unsupported arena generator version");
            var p=request.Profile; float w=p.Get("fixture.width"), d=p.Get("fixture.depth"), ground=p.Get("fixture.floorHeight"), h=p.Get("fixture.upperFloorHeight");
            float ft=p.Get("fixture.floorThickness"), wt=p.Get("fixture.wallThickness"), wh=p.Get("fixture.wallHeight"), rw=p.Get("fixture.rampWidth"), run=p.Get("fixture.rampLength");
            int steps=Mathf.CeilToInt((h-ground)/p.Get("fixture.stairRise")); if(steps<1) throw new ArgumentException("Invalid foundation stairs");
            float rise=(h-ground)/steps, tread=p.Get("fixture.stairTread"), stairX=-w/2+rw/2+wt;
            var solids=new List<ArenaSolid>();
            void Box(string id,Vector3 position,Vector3 size,string material,string support="",int layer=ProvingArena.WorldLayer,Quaternion? rotation=null) => solids.Add(new ArenaSolid{Id=id,Position=position,Size=size,Material=material,Support=support,Layer=layer,Rotation=rotation??Quaternion.identity});
            Box("floor-lower",new Vector3(0,ground-ft/2,0),new Vector3(w,ft,d),"floor","floor:lower");
            Box("floor-upper",new Vector3(0,h-ft/2,d/4),new Vector3(w,ft,d/2),"floor","floor:upper");
            Box("north-wall",new Vector3(0,(h+wh)/2,d/2),new Vector3(w,h+wh,wt),"wall"); Box("south-wall",new Vector3(0,wh/2,-d/2),new Vector3(w,wh,wt),"wall");
            Box("west-wall",new Vector3(-w/2,(h+wh)/2,0),new Vector3(wt,h+wh,d),"wall"); Box("east-wall",new Vector3(w/2,(h+wh)/2,0),new Vector3(wt,h+wh,d),"wall");
            Box("upper-ceiling",new Vector3(0,h+wh+ft/2,d/4),new Vector3(w,ft,d/2),"wall");
            Box("ramp",new Vector3(w/2-rw/2-wt,(h+ground)/2-ft/2,-run/2),new Vector3(rw,ft,Mathf.Sqrt(run*run+(h-ground)*(h-ground))),"accent","transition:ramp",ProvingArena.WorldLayer,Quaternion.Euler(-Mathf.Atan2(h-ground,run)*Mathf.Rad2Deg,0,0));
            for(int i=0;i<steps;i++){float top=ground+(i+1)*rise;Box("stair-"+i,new Vector3(stairX,(top+ground)/2,-(steps-i)*tread+tread/2),new Vector3(rw,top-ground,tread),"accent","transition:stairs");}
            Box("headroom-probe",new Vector3(0,ground+p.Get("fixture.headroomHeight")+ft/2,-d/4),new Vector3(rw,ft,rw),"wall");
            Box("movement-only-barrier",new Vector3(-rw,ground+wh/4,-d/4),new Vector3(wt,wh/2,rw),"accent","",ProvingArena.MovementOnlyLayer);
            Box("solid-window-probe",new Vector3(rw,ground+wh/2,-d/4),new Vector3(wt,wh,rw),"wall");
            var feet=new Vector3[steps+2]; feet[0]=new Vector3(stairX,ground,-steps*tread);feet[steps+1]=new Vector3(stairX,h,0);for(int i=0;i<steps;i++)feet[i+1]=new Vector3(stairX,ground+(i+1)*rise,-(steps-i)*tread+tread/2);
            var spawns=new Vector3[SeatInputCoordinator.SeatCount];for(int i=0;i<spawns.Length;i++)spawns[i]=new Vector3((i-(spawns.Length-1)/2f)*rw,ground,-d/2+rw/2+(i%2)*rw);
            var definition=new ArenaDefinition{Seed=request.Seed,GeneratorVersion=request.GeneratorVersion,ProfileFingerprint=Fingerprint(p),Solids=solids.OrderBy(s=>s.Id,StringComparer.Ordinal).ToArray(),Spawns=spawns,RouteAnchors=new[]{new Vector3(0,ground,-d/2+rw/2),new Vector3(0,h,d/4)},SpawnRegions=new[]{new ArenaSpawnRegion{Id="spawn:lower",Support="floor:lower",Center=new Vector3(0,ground,0),Size=new Vector3(w,0,d)},new ArenaSpawnRegion{Id="spawn:upper",Support="floor:upper",Center=new Vector3(0,h,0),Size=new Vector3(w,0,d)}},Transitions=new[]{new NativeNavigationTransition{Id="transition:stairs",LowerSupport="floor:lower",UpperSupport="floor:upper",OrderedFeet=feet},new NativeNavigationTransition{Id="transition:ramp",LowerSupport="floor:lower",UpperSupport="floor:upper",OrderedFeet=new[]{new Vector3(w/2-rw/2-wt,ground,-run),new Vector3(w/2-rw/2-wt,h,0)}}}};
            definition.Identity=$"{FoundationVersion}|seed:{request.Seed}|profile:{definition.ProfileFingerprint}"; Validate(definition); return definition;
        }
        public static void Validate(ArenaDefinition d)
        {
            if(d==null||string.IsNullOrWhiteSpace(d.Identity)||d.Solids==null||d.Spawns==null||d.RouteAnchors==null||d.Transitions==null||d.SpawnRegions==null)throw new ArgumentException("Incomplete arena definition");
            var ids=new HashSet<string>();foreach(var s in d.Solids)if(s==null||string.IsNullOrWhiteSpace(s.Id)||!ids.Add(s.Id)||s.Size.x<=0||s.Size.y<=0||s.Size.z<=0||!Finite(s.Position)||!Finite(s.Size))throw new ArgumentException("Invalid arena solid");
            foreach(var t in d.Transitions)if(t==null||string.IsNullOrWhiteSpace(t.Id)||t.OrderedFeet==null||t.OrderedFeet.Length<2)throw new ArgumentException("Invalid arena transition");
        }
        public static string Fingerprint(ProvingProfile p) => string.Join(";",p.Descriptors.OrderBy(d=>d.Path,StringComparer.Ordinal).Select(d=>d.Path+"="+p.Get(d.Path).ToString("R",CultureInfo.InvariantCulture)));
        static bool Finite(Vector3 v)=>!float.IsNaN(v.x)&&!float.IsNaN(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.x)&&!float.IsInfinity(v.y)&&!float.IsInfinity(v.z);
    }
}
