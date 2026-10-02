using System;
using System.Collections.Generic;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Approved ring + centre topology. All emitted geometry is immutable authored data.</summary>
    public static class IndustrialTunnelsCatalog
    {
        public const string Id="industrial-tunnels-v1", Revision="1", Identity=Id+"@"+Revision;
        const float RoomY=-.6f, Slab=.3f, Ceiling=4.8f;
        sealed class Zone { public string Name; public Rect Rect; public Zone(string n,float x,float z,float w,float h){Name=n;Rect=new Rect(x,z,w,h);} }
        sealed class Ramp { public string Name, Room; public Vector3 Low,High; public Rect Rect; }
        static readonly Zone[] Rooms={new Zone("nw",-26,10,12,12),new Zone("ne",14,10,12,12),new Zone("sw",-26,-22,12,12),new Zone("se",14,-22,12,12),new Zone("central",-8,-6,16,12)};
        static List<Zone> Corridors()
        {
            var a=new List<Zone>{new Zone("north",-14,17,28,4),new Zone("south",-14,-21,28,4),new Zone("west",-25,-10,4,20),new Zone("east",21,-10,4,20)};
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {a.Add(new Zone("inner-horizontal",sx<0?-14:3,sz<0?-15:11,11,4));a.Add(new Zone("inner-vertical",sx<0?-7:3,sz<0?-15:6,4,9));}
            return a;
        }
        static List<Ramp> Ramps()
        {
            var a=new List<Ramp>();
            void Add(string name,string room,Vector3 low,Vector3 high)
            {bool x=low.x!=high.x; a.Add(new Ramp{Name=name,Room=room,Low=low,High=high,Rect=new Rect(x?Mathf.Min(low.x,high.x):low.x-2,x?low.z-2:Mathf.Min(low.z,high.z),x?2:4,x?4:2)});}
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                string room=(sz>0?"n":"s")+(sx<0?"w":"e");
                Add(room+"-outer",room,new Vector3(sx*16,RoomY,sz*19),new Vector3(sx*14,0,sz*19));
                Add(room+"-inner",room,new Vector3(sx*16,RoomY,sz*13),new Vector3(sx*14,0,sz*13));
                Add(room+"-side",room,new Vector3(sx*23,RoomY,sz*12),new Vector3(sx*23,0,sz*10));
                Add(room+"-centre","central",new Vector3(sx*5,RoomY,sz*4),new Vector3(sx*5,0,sz*6));
            }
            return a;
        }
        public static ArenaDefinition Build()
        {
            var d=Draft();var p=ProvingProfile.CreateIndustrialTunnelsAuthoring(d);
            if(p.Validate().Count!=0)throw new InvalidOperationException("Invalid tunnel authoring");
            // Same metadata/range contract as Combat Bowl, with a separate namespace.
            Vector3 Read(string key)=>new Vector3(p.Get("tunnels.map."+key+".x"),p.Get("tunnels.map."+key+".y"),p.Get("tunnels.map."+key+".z"));
            foreach(var s in d.Solids){s.Position=Read(s.Id+".position");s.Size=Read(s.Id+".size");s.Rotation=Quaternion.Euler(Read(s.Id+".rotation"));}
            return d;
        }
        public static ProvingProfile AuthoringProfile()=>ProvingProfile.CreateIndustrialTunnelsAuthoring(Draft());
        static ArenaDefinition Draft()
        {
            var solids=new List<ArenaSolid>();var transitions=new List<NativeNavigationTransition>();var ramps=Ramps();var corridors=Corridors();
            var cells=new Dictionary<Vector2Int,string>();var occupied=new HashSet<Vector2Int>();
            // One-metre authoring grid is a construction invariant; final coordinates are exported to the profile.
            for(int x=-26;x<26;x++)for(int z=-22;z<22;z++)
            {
                var c=new Vector2(x+.5f,z+.5f);string support=null;
                foreach(var room in Rooms)if(room.Rect.Contains(c))support="support:"+room.Name;
                if(support==null)foreach(var corridor in corridors)if(corridor.Rect.Contains(c))support="support:tunnels";
                if(support==null)continue;var key=new Vector2Int(x,z);occupied.Add(key);
                bool ramp=false;foreach(var r in ramps)if(r.Rect.Contains(c))ramp=true;
                if(!ramp)cells.Add(key,support);
            }
            void Box(string id,Vector3 pos,Vector3 size,string mat="wall",string support="",Quaternion? rot=null,string surface=null,int layer=ProvingArena.WorldLayer)
                =>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Material=mat,Support=support,Rotation=rot??Quaternion.identity,Surface=surface,Layer=layer});
            void Cover(string id,Rect rect)
                =>Box("grille-"+id,new Vector3(rect.center.x,Ceiling+.05f,rect.center.y),new Vector3(rect.width,.1f,rect.height),surface:"tunnel-grille",layer:ProvingArena.MovementOnlyLayer);
            var used=new HashSet<Vector2Int>();int index=0;
            for(int z=-22;z<22;z++)for(int x=-26;x<26;x++)
            {
                var key=new Vector2Int(x,z);if(used.Contains(key)||!cells.TryGetValue(key,out var support))continue;
                int w=1,h=1;while(cells.TryGetValue(new Vector2Int(x+w,z),out var s)&&s==support&&!used.Contains(new Vector2Int(x+w,z)))w++;
                bool next=true;while(next){for(int i=0;i<w;i++)if(!cells.TryGetValue(new Vector2Int(x+i,z+h),out var s2)||s2!=support||used.Contains(new Vector2Int(x+i,z+h))){next=false;break;}if(next)h++;}
                for(int i=0;i<w;i++)for(int j=0;j<h;j++)used.Add(new Vector2Int(x+i,z+j));
                float y=support=="support:tunnels"?0:RoomY;string id="deck-"+(index++);var rect=new Rect(x,z,w,h);
                Box(id,new Vector3(x+w*.5f,y-Slab*.5f,z+h*.5f),new Vector3(w,Slab,h),"floor",support);Cover(id,rect);
            }
            foreach(var ramp in ramps)
            {
                string id="transition:"+ramp.Name;var rot=Quaternion.LookRotation(ramp.High-ramp.Low,Vector3.up);
                Box(ramp.Name+"-ramp",(ramp.High+ramp.Low)*.5f-rot*Vector3.up*Slab*.5f,new Vector3(4,Slab,Vector3.Distance(ramp.High,ramp.Low)),"floor",id,rot);
                var direction=ramp.High-ramp.Low;direction.y=0;direction.Normalize();var feet=new List<Vector3>{ramp.Low-direction*.15f};
                for(int i=1;i<5;i++)feet.Add(Vector3.Lerp(ramp.Low,ramp.High,i/5f));feet.Add(ramp.High+direction*.15f);
                transitions.Add(new NativeNavigationTransition{Id=id,LowerSupport="support:"+ramp.Room,UpperSupport="support:tunnels",OrderedFeet=feet.ToArray()});Cover(ramp.Name,ramp.Rect);
            }
            // Merge exposed perimeter edges: continuous room/corridor shells, never walls across portals.
            foreach(var direction in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
            {
                var edges=new HashSet<Vector2Int>();foreach(var cell in occupied)if(!occupied.Contains(cell+direction))edges.Add(cell);
                var along=direction.x!=0?Vector2Int.up:Vector2Int.right;
                for(int z=-22;z<22;z++)for(int x=-26;x<26;x++)
                {
                    var cell=new Vector2Int(x,z);if(!edges.Remove(cell))continue;int n=1;while(edges.Remove(cell+along*n))n++;
                    var center=new Vector3(x+.5f+direction.x*.6f+along.x*(n-1)*.5f,(Ceiling+RoomY)*.5f,z+.5f+direction.y*.6f+along.y*(n-1)*.5f);
                    Box("shell-"+(index++),center,new Vector3(direction.x!=0?.2f:n+.2f,Ceiling-RoomY,direction.x!=0?n+.2f:.2f));
                }
            }
            var spawns=new[]{new Vector3(-18,RoomY,19),new Vector3(18,RoomY,14),new Vector3(-18,RoomY,-14),new Vector3(18,RoomY,-19)};
            var regions=new List<ArenaSpawnRegion>();for(int i=0;i<4;i++)regions.Add(new ArenaSpawnRegion{Id="tunnel-spawn-"+i,Support="support:"+Rooms[i].Name,Center=spawns[i],Size=new Vector3(2,0,2)});
            var pickups=new[]{
                new ArenaPickupDefinition{Id="tunnels-shotgun-nw",Kind=ArenaPickupKind.Shotgun,Anchor=new Vector3(-20,RoomY,16),Support="support:nw"},
                new ArenaPickupDefinition{Id="tunnels-cutter-ne",Kind=ArenaPickupKind.Cutter,Anchor=new Vector3(20,RoomY,16),Support="support:ne"},
                new ArenaPickupDefinition{Id="tunnels-damage-central",Kind=ArenaPickupKind.Damage,Anchor=new Vector3(0,RoomY,0),Support="support:central"},
                new ArenaPickupDefinition{Id="tunnels-armor-west",Kind=ArenaPickupKind.Armor,Anchor=new Vector3(-23,0,0),Support="support:tunnels"},
                new ArenaPickupDefinition{Id="tunnels-armor-east",Kind=ArenaPickupKind.Armor,Anchor=new Vector3(23,0,0),Support="support:tunnels"},
                new ArenaPickupDefinition{Id="tunnels-pulse-south",Kind=ArenaPickupKind.Pulse,Anchor=new Vector3(0,0,-19),Support="support:tunnels"}};
            return new ArenaDefinition{MapId=Id,Revision=Revision,Identity=Identity,ProfileFingerprint="authored:"+Identity,Solids=solids.ToArray(),Spawns=spawns,SpawnRegions=regions.ToArray(),Transitions=transitions.ToArray(),RouteAnchors=new[]{new Vector3(0,RoomY,0),new Vector3(0,0,-19)},Pickups=pickups};
        }
    }
}
