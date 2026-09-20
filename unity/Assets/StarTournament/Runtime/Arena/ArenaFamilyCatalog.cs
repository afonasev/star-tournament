using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Pure ARENA-2 family registry. Values are stable public profile ordinals.</summary>
    public static class ArenaFamilyCatalog
    {
        public const string WideHallCircuit="wide-hall-circuit-v1", PerimeterGallery="perimeter-gallery-v1",
            SplitBalconies="split-balconies-v1", StackedCombat="stacked-combat-v1", LowerBasement="lower-basement-v1";
        public static readonly ReadOnlyCollection<string> Ids=Array.AsReadOnly(new[]{WideHallCircuit,PerimeterGallery,SplitBalconies,StackedCombat,LowerBasement});
        public static string Resolve(float value)
        {
            int ordinal=Mathf.RoundToInt(value);
            if(!Mathf.Approximately(value,ordinal)||ordinal<0||ordinal>=Ids.Count)throw new ArgumentException("Unsupported arena family ordinal");
            return Ids[ordinal];
        }
        public static bool Contains(string id)=>Ids.IndexOf(id)>=0;
        public static ArenaDefinition Build(string family,uint seed,ProvingProfile p)
        {
            if(!Contains(family))throw new ArgumentException("Unsupported arena family");
            float w=p.Get("fixture.width"),d=p.Get("fixture.depth"),lo=p.Get("fixture.floorHeight"),hi=p.Get("fixture.upperFloorHeight");
            float ft=p.Get("fixture.floorThickness"),wt=p.Get("fixture.wallThickness"),wh=p.Get("fixture.wallHeight"),rw=p.Get("fixture.rampWidth"),run=p.Get("fixture.rampLength");
            var solids=new List<ArenaSolid>();
            string SemanticId(string id)
            {
                if(family!=WideHallCircuit)return family+":"+id;
                if(id=="upper-deck")return "floor-upper";
                if(id=="ramp-east")return "ramp";
                if(id.StartsWith("stair-west-",StringComparison.Ordinal))return "stair-"+id.Substring("stair-west-".Length);
                return id;
            }
            void Box(string id,Vector3 position,Vector3 size,string material,string support="",int layer=ProvingArena.WorldLayer,Quaternion? rotation=null)=>solids.Add(new ArenaSolid{Id=SemanticId(id),Position=position,Size=size,Material=material,Support=support,Layer=layer,Rotation=rotation??Quaternion.identity});
            string lower=family==LowerBasement?"floor:basement":"floor:lower", upper=family==LowerBasement?"floor:main":"floor:upper";
            Box("floor-lower",new Vector3(0,lo-ft/2,0),new Vector3(w,ft,d),"floor",lower);
            Box("north-wall",new Vector3(0,(hi+wh)/2,d/2),new Vector3(w,hi+wh,wt),"wall");Box("south-wall",new Vector3(0,wh/2,-d/2),new Vector3(w,wh,wt),"wall");
            Box("west-wall",new Vector3(-w/2,(hi+wh)/2,0),new Vector3(wt,hi+wh,d),"wall");Box("east-wall",new Vector3(w/2,(hi+wh)/2,0),new Vector3(wt,hi+wh,d),"wall");
            AddFamilyFloors(family,Box,w,d,lo,hi,ft,wt,wh,rw,upper);
            if(family==WideHallCircuit)
            {
                Box("upper-ceiling",new Vector3(0,hi+wh+ft/2,d/4),new Vector3(w,ft,d/2),"wall");
                Box("headroom-probe",new Vector3(0,lo+p.Get("fixture.headroomHeight")+ft/2,-d/4),new Vector3(rw,ft,rw),"wall");
                Box("movement-only-barrier",new Vector3(-rw,lo+wh/4,-d/4),new Vector3(wt,wh/2,rw),"accent","",ProvingArena.MovementOnlyLayer);
                Box("solid-window-probe",new Vector3(rw,lo+wh/2,-d/4),new Vector3(wt,wh,rw),"wall");
            }
            var transitions=AddTransitions(Box,w,lo,hi,ft,wt,rw,run,p.Get("fixture.stairRise"),p.Get("fixture.stairTread"),lower,upper);
            var spawns=new Vector3[SeatInputCoordinator.SeatCount];for(int i=0;i<spawns.Length;i++)spawns[i]=new Vector3((i-(spawns.Length-1)/2f)*rw,lo,-d/2+rw/2+(i%2)*rw);
            float upperAnchorX=family==PerimeterGallery?-w/2+rw/2:family==SplitBalconies?-w/4:family==StackedCombat?-w/3:0;
            return new ArenaDefinition{Solids=solids.ToArray(),Spawns=spawns,RouteAnchors=new[]{new Vector3(0,lo,-d/2+rw/2),new Vector3(upperAnchorX,hi,d/4)},
                SpawnRegions=new[]{new ArenaSpawnRegion{Id="spawn:"+lower,Support=lower,Center=new Vector3(0,lo,0),Size=new Vector3(w,0,d)},new ArenaSpawnRegion{Id="spawn:"+upper,Support=upper,Center=new Vector3(0,hi,d/4),Size=new Vector3(w,0,d/2)}},Transitions=transitions};
        }
        delegate void AddBox(string id,Vector3 position,Vector3 size,string material,string support,int layer,Quaternion? rotation);
        static void AddFamilyFloors(string family,AddBox box,float w,float d,float lo,float hi,float ft,float wt,float wh,float rw,string upper)
        {
            void B(string id,Vector3 p,Vector3 s,string material,string support="",int layer=ProvingArena.WorldLayer)=>box(id,p,s,material,support,layer,null);
            if(family==WideHallCircuit)
            {
                B("upper-deck",new Vector3(0,hi-ft/2,d/4),new Vector3(w,ft,d/2),"floor",upper);
            }
            else if(family==PerimeterGallery)
            {
                B("gallery-north",new Vector3(0,hi-ft/2,d/2-rw/2),new Vector3(w,ft,rw),"floor",upper);
                B("gallery-west",new Vector3(-w/2+rw/2,hi-ft/2,d/4-rw/2),new Vector3(rw,ft,d/2-rw),"floor",upper);
                B("gallery-east",new Vector3(w/2-rw/2,hi-ft/2,d/4-rw/2),new Vector3(rw,ft,d/2-rw),"floor",upper);
            }
            else if(family==SplitBalconies)
            {
                B("balcony-west",new Vector3(-w/4,hi-ft/2,d/4),new Vector3(w/2-rw/2,ft,d/2),"floor",upper);
                B("balcony-east",new Vector3(w/4,hi-ft/2,d/4),new Vector3(w/2-rw/2,ft,d/2),"floor",upper);
                B("balcony-bridge",new Vector3(0,hi-ft/2,d/2-rw/2),new Vector3(rw,ft,rw),"accent",upper);
            }
            else if(family==StackedCombat)
            {
                B("upper-arena",new Vector3(0,hi-ft/2,d/4),new Vector3(w,ft,d/2),"floor",upper);
                B("lower-divider-west",new Vector3(-w/6,lo+wh/2,-d/6),new Vector3(wt,wh,d/3),"wall");
                B("lower-divider-east",new Vector3(w/6,lo+wh/2,d/8),new Vector3(wt,wh,d/3),"wall");
                B("upper-cover",new Vector3(0,hi+wh/4,d/4),new Vector3(w/5,wh/2,wt*2),"accent");
            }
            else
            {
                B("main-floor",new Vector3(0,hi-ft/2,d/4),new Vector3(w,ft,d/2),"floor",upper);
                B("basement-room-west",new Vector3(-w/4,lo+wh/2,0),new Vector3(wt,wh,d/3),"wall");
                B("basement-room-east",new Vector3(w/4,lo+wh/2,d/8),new Vector3(wt,wh,d/3),"wall");
                B("basement-header",new Vector3(0,lo+wh-wt/2,-d/8),new Vector3(w/2,wt,d/3),"accent");
            }
        }
        static NativeNavigationTransition[] AddTransitions(AddBox box,float w,float lo,float hi,float ft,float wt,float rw,float run,float requestedRise,float tread,string lower,string upper)
        {
            float east=w/2-rw/2-wt,west=-east,angle=-Mathf.Atan2(hi-lo,run)*Mathf.Rad2Deg;
            box("ramp-east",new Vector3(east,(hi+lo)/2-ft/2,-run/2),new Vector3(rw,ft,Mathf.Sqrt(run*run+(hi-lo)*(hi-lo))),"accent","transition:ramp-east",ProvingArena.WorldLayer,Quaternion.Euler(angle,0,0));
            int steps=Mathf.Max(1,Mathf.CeilToInt((hi-lo)/requestedRise));float rise=(hi-lo)/steps;var feet=new Vector3[steps+2];feet[0]=new Vector3(west,lo,-steps*tread);feet[steps+1]=new Vector3(west,hi,0);
            for(int i=0;i<steps;i++){float top=lo+(i+1)*rise;box("stair-west-"+i,new Vector3(west,(top+lo)/2,-(steps-i)*tread+tread/2),new Vector3(rw,top-lo,tread),"accent","transition:stairs-west",ProvingArena.WorldLayer,null);feet[i+1]=new Vector3(west,top,-(steps-i)*tread+tread/2);}
            return new[]{new NativeNavigationTransition{Id="transition:stairs-west",LowerSupport=lower,UpperSupport=upper,OrderedFeet=feet},new NativeNavigationTransition{Id="transition:ramp-east",LowerSupport=lower,UpperSupport=upper,OrderedFeet=new[]{new Vector3(east,lo,-run),new Vector3(east,hi,0)}}};
        }
    }
}
