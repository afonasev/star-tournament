using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Published R7 with F1 windows. Default authoring data is frozen per revision; gameplay/Lab edits never rebuild it.</summary>
    public static class CombatBowlCatalog
    {
        public const string Id="combat-bowl-v1", Revision="8", Identity=Id+"@"+Revision;
        public static bool Contains(string id)=>id==Id;
        public static ArenaFreezeSnapshot Freeze(ProvingProfile navigationProfile)=>ArenaFreezeSnapshot.Create(Resolve(Id,Revision),navigationProfile);
        public static ArenaDefinition Resolve(string id,string revision)
        {if(id!=Id||revision!=Revision)throw new ArgumentException("Unknown authored map identity: "+id+"@"+revision);return Build();}
        public static ArenaDefinition Build()
        {
            var definition=Draft();
            // Every authored spatial value is registered in the same descriptor system as Balance Lab.
            // This published revision always resolves its approved defaults, not mutable gameplay profiles.
            var profile=ProvingProfile.CreateCombatBowlAuthoringDefault(definition);
            if(profile.Validate().Count!=0)throw new InvalidOperationException("Invalid Combat Bowl authoring profile");
            ApplyAuthoring(definition,profile);
            return definition;
        }
        public static ProvingProfile AuthoringProfile()=>ProvingProfile.CreateCombatBowlAuthoringDefault(Draft());
        internal static void ApplyAuthoring(ArenaDefinition d,ProvingProfile p)
        {
            Vector3 Read(string key)=>new Vector3(p.Get(key+".x"),p.Get(key+".y"),p.Get(key+".z"));
            foreach(var s in d.Solids){s.Position=Read(s.Id+".position");s.Size=Read(s.Id+".size");s.Rotation=Quaternion.Euler(Read(s.Id+".rotation"));}
            for(int i=0;i<d.Spawns.Length;i++)d.Spawns[i]=Read("spawn-"+i);
            foreach(var s in d.SpawnRegions){s.Center=Read(s.Id+".center");s.Size=Read(s.Id+".extent");}
            foreach(var t in d.Transitions)for(int i=0;i<t.OrderedFeet.Length;i++)t.OrderedFeet[i]=Read(t.Id+".feet-"+i);
            for(int i=0;i<d.RouteAnchors.Length;i++)d.RouteAnchors[i]=Read("route-"+i);
            foreach(var item in d.Pickups)item.Anchor=Read(item.Id+".anchor");
        }
        static ArenaDefinition Draft()
        {
            // Authored P4 defaults, all exported as range-validated profile metadata below.
            var solids=new List<ArenaSolid>();var transitions=new List<NativeNavigationTransition>();
            var regions=new List<ArenaSpawnRegion>();var spawns=new List<Vector3>();
            const string loop="support:lower-loop", hall="support:central", basement="support:armor-corridor";
            const float slab=.4f, thickness=.4f;
            void Box(string id,Vector3 pos,Vector3 size,string material="wall",string support="",Quaternion? rotation=null)
                =>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Material=material,Support=support,Layer=ProvingArena.WorldLayer,Rotation=rotation??Quaternion.identity});
            void Floor(string id,float x1,float x2,float z1,float z2,float y,string support)
                =>Box(id,new Vector3((x1+x2)/2,y-slab/2,(z1+z2)/2),new Vector3(x2-x1,slab,z2-z1),"floor",support);
            // Wall coordinates are centre-lines. Portals are widened by wall thickness to retain clear width.
            void Wall(string id,float x1,float z1,float x2,float z2,float bottom,float top)
                =>Box(id,new Vector3((x1+x2)/2,(bottom+top)/2,(z1+z2)/2),new Vector3(Mathf.Abs(x2-x1)+thickness,top-bottom,Mathf.Abs(z2-z1)+thickness));
            void Roof(string id,float x1,float x2,float z1,float z2,float underside,float depth=slab)
                =>Box(id,new Vector3((x1+x2)/2,underside+depth/2,(z1+z2)/2),new Vector3(x2-x1,depth,z2-z1));
            void Steps(string id,Vector3 start,Vector3 direction,int count,float rise,float tread,float width,string lower,string upper)
            {
                var feet=new List<Vector3>{start-direction*.2f};
                for(int i=0;i<count;i++)
                {
                    var top=start+direction*((i+.5f)*tread)+Vector3.up*((i+1)*rise);
                    var sz=direction.x!=0?new Vector3(tread,(i+1)*rise,width):new Vector3(width,(i+1)*rise,tread);
                    Box(id+"-step-"+i,new Vector3(top.x,start.y+sz.y/2,top.z),sz,"floor","transition:"+id);feet.Add(top);
                }
                feet.Add(start+direction*(count*tread+.2f)+Vector3.up*(count*rise));
                transitions.Add(new NativeNavigationTransition{Id="transition:"+id,LowerSupport=lower,UpperSupport=upper,OrderedFeet=feet.ToArray()});
            }
            // R7 authored coordinates are exported individually by CombatBowlAuthoringProfile.
            // One connected upper support spans corners, balcony, bridges and hall; the lower loop
            // remains a distinct named support even at identical XZ.
            // Continuous lower foundation catches every legal drop from hall, bridge or balcony.
            // The basement aperture remains below this floor and is reached by its two stairs.
            Floor("foundation-north",-36,36,10,36,0,loop);Floor("foundation-south",-36,36,-36,-10,0,loop);
            Floor("foundation-west",-36,-2.6f,-10,10,0,loop);Floor("foundation-east",2.6f,36,-10,10,0,loop);
            Floor("north-flank",-28,28,12,24,0,loop);Floor("south-gallery",-28,28,-24,-12,0,loop);
            Floor("west-loop",-28,-16,-12,12,0,loop);Floor("east-loop",16,28,-12,12,0,loop);
            Floor("armor-corridor",-2.6f,2.6f,-7,7,-1.2f,basement);
            Steps("north-descent",new Vector3(0,-1.2f,7),Vector3.forward,6,.2f,.5f,5.2f,basement,loop);
            Steps("south-descent",new Vector3(0,-1.2f,-7),Vector3.back,6,.2f,.5f,5.2f,basement,loop);
            Floor("north-basement-landing",-2.6f,2.6f,10,12,0,loop);Floor("south-basement-landing",-2.6f,2.6f,-12,-10,0,loop);
            Wall("basement-west",-2.8f,-12,-2.8f,12,-1.6f,3.6f);Wall("basement-east",2.8f,-12,2.8f,12,-1.6f,3.6f);
            Floor("central-hall",-6,6,-8,8,4,hall);Roof("hall-roof",-6.2f,6.2f,-8.2f,8.2f,8);
            foreach(float x in new[]{-2.5f,2.5f})foreach(float z in new[]{-3.5f,3.5f})Box("column-"+x+"-"+z,new Vector3(x,6,z),new Vector3(1.5f,4,1.5f));
            void Grate(string id,float x1,float x2,float z1,float z2)
            {Floor(id,x1,x2,z1,z2,4,hall);solids[solids.Count-1].Layer=ProvingArena.MovementOnlyLayer;solids[solids.Count-1].Surface="grating";}
            Grate("balcony-north",-28,28,18,24);Grate("balcony-south",-28,28,-24,-18);
            Grate("balcony-west",-28,-22,-18,18);Grate("balcony-east",22,28,-18,18);
            Grate("bridge-north",-2,2,8,18);Grate("bridge-south",-2,2,-18,-8);
            Roof("north-roof",-28,28,12,24,8);Roof("south-roof",-28,28,-24,-12,8);
            Roof("west-roof",-28,-16,-12,12,8);Roof("east-roof",16,28,-12,12,8);
            // Opaque architectural soffits over the two lower spawn recesses protect them from
            // grated-upper observers. They are part of the central shell, not eight spawn cages.
            Roof("north-inner-soffit",-16,-4,12,20,2.6f);Roof("south-inner-soffit",4,16,-20,-12,2.6f);
            Wall("north-inner-return-west",-16,12,-16,18,0,3.6f);Wall("north-inner-return-east",-4,12,-4,18,0,3.6f);
            Wall("north-inner-offset-front",-16,18,-8,18,0,3.6f);
            Wall("south-inner-return-west",4,-18,4,-12,0,3.6f);Wall("south-inner-return-east",16,-18,16,-12,0,3.6f);
            Wall("south-inner-offset-front",8,-18,16,-18,0,3.6f);
            Wall("inner-north-west",-16,12,-2.8f,12,0,4.2f);Wall("inner-north-east",2.8f,12,16,12,0,4.2f);
            Wall("inner-south-west",-16,-12,-2.8f,-12,0,4.2f);Wall("inner-south-east",2.8f,-12,16,-12,0,4.2f);
            Wall("hall-west-south",-6.2f,-8,-6.2f,1.8f,4,8);Wall("hall-west-north",-6.2f,6.2f,-6.2f,8,4,8);
            Wall("hall-east-south",6.2f,-8,6.2f,-6.2f,4,8);Wall("hall-east-north",6.2f,-1.8f,6.2f,8,4,8);
            Wall("hall-north-lintel",-2.2f,8.2f,2.2f,8.2f,6.2f,8);Wall("hall-south-lintel",-2.2f,-8.2f,2.2f,-8.2f,6.2f,8);
            foreach(int sx in new[]{-1,1})
            {
                string side=sx<0?"west":"east",low=loop;
                Floor(side+"-low",Mathf.Min(sx*28,sx*36),Mathf.Max(sx*28,sx*36),-4,4,0,low);
                Roof(side+"-low-ceiling",Mathf.Min(sx*28,sx*36),Mathf.Max(sx*28,sx*36),-4,4,3.2f);
                // Wide gateway with a flush lower link and an opaque lintel.
                Floor(side+"-gateway",Mathf.Min(sx*24,sx*32),Mathf.Max(sx*24,sx*32),-4,4,0,low);
                Wall(side+"-gateway-lintel",sx*28,-4,sx*28,4,3.2f,8);
                foreach(int sz in new[]{-1,1})
                {
                    string corner=(sz>0?"north":"south")+side;
                    float xa=Mathf.Min(sx*28,sx*36),xb=Mathf.Max(sx*28,sx*36),za=sz>0?14:-36,zb=sz>0?36:-14;
                    Box(corner+"-spawn",new Vector3((xa+xb)/2,2,(za+zb)/2),new Vector3(xb-xa,4,zb-za),"floor",hall);Roof(corner+"-roof",xa,xb,za,zb,8);
                    Steps(corner+"-outer-rise",new Vector3(sx*32,0,sz*4),Vector3.forward*sz,20,.2f,.5f,8,low,hall);
                    // Matching stepped ceiling has the same rise/run and maintains full headroom.
                    for(int i=0;i<20;i++)Roof(corner+"-slope-ceiling-"+i,xa,xb,Mathf.Min(sz*(4+i*.5f),sz*(4+(i+1)*.5f)),Mathf.Max(sz*(4+i*.5f),sz*(4+(i+1)*.5f)),3.2f+(i+1)*.2f,1.2f);
                    Wall(corner+"-divider",sx*28,sz*4,sx*28,sz*20,0,8);
                    Wall(corner+"-gateway-jamb",sx*28,sz*4,sx*30,sz*4,0,8);
                    // Wide diagonal link; its opaque sidewalls put the spawn off the sight axis.
                    var start=new Vector3(sx*24,4,sz*18);var end=new Vector3(sx*32,4,sz*26);var mid=(start+end)/2;
                    var dir=(end-start).normalized;var across=Vector3.Cross(Vector3.up,dir);var rot=Quaternion.LookRotation(dir);
                    Box(corner+"-diagonal",mid-Vector3.up*slab/2,new Vector3(6,slab,Vector3.Distance(start,end)+2),"floor",hall,rot);
                    foreach(int edge in new[]{-1,1})Box(corner+"-diagonal-side-"+edge,mid+dir*1.75f+across*(edge*3.2f)+Vector3.up*2,new Vector3(.4f,4,Vector3.Distance(start,end)-1.5f),"wall","",rot);
                    spawns.Add(new Vector3(sx*33,4,sz*17));regions.Add(new ArenaSpawnRegion{Id="spawn-"+corner,Support=hall,Center=new Vector3(sx*33,4,sz*17),Size=new Vector3(1,0,1)});
                }
            }
            foreach(int sz in new[]{-1,1})
            {
                string side=sz>0?"north":"south",low=loop;float za=sz>0?28:-36,zb=sz>0?36:-28;
                Floor(side+"-outer-low",-18,18,za,zb,0,low);Roof(side+"-outer-low-ceiling",-18,18,za,zb,3.2f);
                foreach(int sx in new[]{-1,1})
                {
                    string id=side+(sx<0?"west":"east")+"-cross-rise";
                    Steps(id,new Vector3(sx*18,0,sz*32),Vector3.right*sx,20,.2f,.5f,8,low,hall);
                    for(int i=0;i<20;i++)Roof(id+"-ceiling-"+i,Mathf.Min(sx*(18+i*.5f),sx*(18+(i+1)*.5f)),Mathf.Max(sx*(18+i*.5f),sx*(18+(i+1)*.5f)),za,zb,3.2f+(i+1)*.2f,1.2f);
                    Floor(side+"-entry-"+sx,sx<0?-18:12,sx<0?-12:18,sz>0?24:-28,sz>0?28:-24,0,low);
                    Roof(side+"-entry-roof-"+sx,sx<0?-18:12,sx<0?-12:18,sz>0?24:-28,sz>0?28:-24,3.2f);
                }
                Wall(side+"-divider-middle",-12,sz*24,12,sz*24,0,8);
                Wall(side+"-divider-left",-28,sz*24,-18,sz*24,0,3.6f);
                Wall(side+"-divider-right",18,sz*24,28,sz*24,0,3.6f);
                Wall(side+"-entry-lintel-left",-18,sz*24,-12,sz*24,3.2f,8);
                Wall(side+"-entry-lintel-right",12,sz*24,18,sz*24,3.2f,8);
                Wall("hall-"+side+"-left",-6,sz*8.2f,-2.2f,sz*8.2f,4,8);Wall("hall-"+side+"-right",2.2f,sz*8.2f,6,sz*8.2f,4,8);
            }
            // The station shell seals the atrium and roof junctions above the local lowered ceilings.
            Roof("interior-shell-roof",-36,36,-36,36,8.4f);
            // Opaque hull piers, sills and headers surround smaller glazed openings.
            // Every dimension below becomes range-validated authoring metadata, frozen in revision 8.
            foreach(int side in new[]{-1,1})foreach(bool alongX in new[]{false,true})
            {
                string axis=alongX?"z":"x",prefix="hull-"+axis+"-"+side;
                var centers=alongX?new[]{-32f,-12f,0f,12f,32f}:new[]{-30f,-20f,0f,20f,30f};
                float cursor=-36.2f;
                void HullPart(string id,float from,float to,float bottom,float top,bool glass=false)
                {
                    Box(id,alongX?new Vector3((from+to)/2,(bottom+top)/2,side*36.2f):new Vector3(side*36.2f,(bottom+top)/2,(from+to)/2),
                        alongX?new Vector3(to-from,top-bottom,.4f):new Vector3(.4f,top-bottom,to-from));
                    if(glass)solids[solids.Count-1].Surface="window";
                }
                for(int i=0;i<centers.Length;i++)
                {
                    float center=centers[i];bool upper=Mathf.Abs(center)>(alongX?28:14);
                    float width=alongX?(upper?6:8):(upper?8:6),bottom=upper?4.8f:.6f,top=upper?7.4f:2.6f;
                    float left=center-width/2,right=center+width/2;
                    HullPart(prefix+"-pier-"+i,cursor,left,0,8.4f);
                    HullPart(prefix+"-sill-"+i,left,right,0,bottom);
                    HullPart(prefix+"-header-"+i,left,right,top,8.4f);
                    HullPart("window-"+axis+"-"+side+"-"+i,left,right,bottom,top,true);
                    cursor=right;
                }
                HullPart(prefix+"-pier-end",cursor,36.2f,0,8.4f);
            }
            // Keep the accepted ordinary hall ramps and basement as an additional route pair.
            foreach(int side in new[]{-1,1})
            {
                string id=side<0?"west-rise":"east-rise";float z=-side*4;var start=new Vector3(side*18,0,z);var end=new Vector3(side*6,4,z);
                var rotation=Quaternion.Euler(0,0,Mathf.Atan2(4,12)*Mathf.Rad2Deg*-side);var normal=rotation*Vector3.up;
                Box(id,(start+end)/2-normal*slab/2,new Vector3(Vector3.Distance(start,end),slab,4),"floor","transition:"+id,rotation);
                Wall(id+"-south",side*18,z-2.2f,side*6,z-2.2f,0,8);Wall(id+"-north",side*18,z+2.2f,side*6,z+2.2f,0,8);
                Roof(id+"-roof",Mathf.Min(side*18,side*6),Mathf.Max(side*18,side*6),z-2.2f,z+2.2f,8);
                var feet=new List<Vector3>{start+Vector3.right*side*.2f};for(int i=1;i<12;i++)feet.Add(Vector3.Lerp(start,end,i/12f));feet.Add(end-Vector3.right*side*.2f);
                transitions.Add(new NativeNavigationTransition{Id="transition:"+id,LowerSupport=loop,UpperSupport=hall,OrderedFeet=feet.ToArray()});
            }
            void Spawn(string id,Vector3 position,string support){spawns.Add(position);regions.Add(new ArenaSpawnRegion{Id=id,Support=support,Center=position,Size=new Vector3(1,0,1)});}
            Spawn("spawn-inner-north",new Vector3(-10,0,14),loop);Spawn("spawn-outer-north",new Vector3(6,0,32),loop);
            Spawn("spawn-outer-south",new Vector3(-6,0,-32),loop);Spawn("spawn-inner-south",new Vector3(10,0,-14),loop);
            return new ArenaDefinition{Revision=Revision,MapId=Id,Identity=Identity,ProfileFingerprint="authored:"+Identity,Pickups=new[]{
                new ArenaPickupDefinition{Id="armor-basement",Kind=ArenaPickupKind.Armor,Support=basement,Anchor=new Vector3(0,-1.2f,0)},
                new ArenaPickupDefinition{Id="armor-sw",Kind=ArenaPickupKind.Armor,Support=hall,Anchor=new Vector3(-32,4,-32)},
                new ArenaPickupDefinition{Id="armor-ne",Kind=ArenaPickupKind.Armor,Support=hall,Anchor=new Vector3(32,4,32)},
                new ArenaPickupDefinition{Id="speed-nw",Kind=ArenaPickupKind.Speed,Support=hall,Anchor=new Vector3(-32,4,32)},
                new ArenaPickupDefinition{Id="speed-se",Kind=ArenaPickupKind.Speed,Support=hall,Anchor=new Vector3(32,4,-32)},
                new ArenaPickupDefinition{Id="damage-hall",Kind=ArenaPickupKind.Damage,Support=hall,Anchor=new Vector3(0,4,0)}},Solids=solids.ToArray(),Spawns=spawns.ToArray(),SpawnRegions=regions.ToArray(),Transitions=transitions.ToArray(),RouteAnchors=new[]{new Vector3(0,-1.2f,0),new Vector3(0,4,0)}};
        }
    }
}
