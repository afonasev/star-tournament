using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Structure 03; north = +Z. Dimensions are immutable map authoring, not editable balance.</summary>
    public static class WellArenaCatalog
    {
        public const string Id="well-laboratory-v1", Revision="5", Identity=Id+"@"+Revision;
        public const float Storey=7.2f, Slab=.8f, ClearHeight=Storey-Slab, Edge=24.8f, Pit=9.6f;
        const float DoorHeight=3.2f, DoorHalf=1.6f, Wall=.3f;
        public static string Support(int floor)=>"support:well-"+floor;
        public static ProvingProfile AuthoringProfile()
        {
            var definition=Build();var p=ProvingProfile.CreateCombatBowlAuthoringDefault(definition);
            p.SetIdentityForWell();p.AddWellLiftAuthoring(definition.Lifts);return p;
        }
        public static ArenaDefinition Build()
        {
            var solids=new List<ArenaSolid>();var transitions=new List<NativeNavigationTransition>();
            var spawns=new List<Vector3>();var regions=new List<ArenaSpawnRegion>();
            void Box(string id,Vector3 pos,Vector3 size,string material="wall",string support="",string surface=null,int layer=ProvingArena.WorldLayer)
                =>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Rotation=Quaternion.identity,Material=material,Support=support,Surface=surface,Layer=layer});
            void Deck(string id,float x,float z,float width,float depth,float y,string support,bool grate=false)
            {
                // Open steel bridges remain light; the building's concrete floor plates use Slab.
                float thickness=grate?.3f:Slab;
                Box(id,new Vector3(x,y-thickness/2,z),new Vector3(width,thickness,depth),"floor",support,grate?"well-grate":"well-floor",grate?ProvingArena.MovementOnlyLayer:ProvingArena.WorldLayer);
            }
            void Segment(string id,bool alongX,float fixedAt,float start,float end,float y,float height=ClearHeight)
            {
                float middle=(start+end)/2,length=Mathf.Abs(end-start);
                Box(id,alongX?new Vector3(middle,y+height/2,fixedAt):new Vector3(fixedAt,y+height/2,middle),alongX?new Vector3(length,height,Wall):new Vector3(Wall,height,length));
            }
            void DoorWall(string id,bool alongX,float fixedAt,float start,float end,float doorAt,float y,float half=DoorHalf)
            {
                if(start>end){float t=start;start=end;end=t;}
                Segment(id+"-a",alongX,fixedAt,start,doorAt-half,y);
                Segment(id+"-b",alongX,fixedAt,doorAt+half,end,y);
                Segment(id+"-header",alongX,fixedAt,doorAt-half,doorAt+half,y+DoorHeight,ClearHeight-DoorHeight);
            }
            for(int f=0;f<3;f++)
            {
                float y=f*Storey;string k="f"+f+"-",support=Support(f);
                Deck(k+"north",0,17.2f,49.6f,15.2f,y,support);Deck(k+"south",0,-17.2f,49.6f,15.2f,y,support);
                Deck(k+"west",-17.2f,0,15.2f,19.2f,y,support);Deck(k+"east",17.2f,0,15.2f,19.2f,y,support);
                foreach(int side in new[]{-1,1})
                {
                    DoorWall(k+"outer-x"+side,false,side*Edge,-Edge,Edge,0,y,3.5f);
                    DoorWall(k+"outer-z"+side,true,side*Edge,-Edge,Edge,0,y,3.5f);
                }
                // One common structural grid. Corner rooms reuse the outer shell; no disconnected spawn screens.
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    string room=k+"corner-"+x+"-"+z;
                    DoorWall(room+"-front",true,z*16,x*Pit,x*Edge,x*14.4f,y);
                    DoorWall(room+"-side",false,x*Pit,z*16,z*Edge,z*18.4f,y);
                    bool pickupRoom=f==2&&x==z; // NE cutter and SW armour: no initial or respawn candidates.
                    if(!pickupRoom)
                    {
                        var pos=new Vector3(x*21,y,z*21);spawns.Add(pos);
                        regions.Add(new ArenaSpawnRegion{Id="spawn-"+room,Center=pos,Size=new Vector3(2,0,2),Support=support});
                    }
                }
                // Stair-side rooms use aligned ends. Floor 2 shifts them against the courtyard,
                // opening an uninterrupted outer corridor past both stair entrances.
                foreach(int side in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    string room=k+"side-"+side+"-"+z;
                    float min=f==1?Pit:16,max=f==1?16:Edge,centre=(min+max)/2;
                    DoorWall(room+"-front",false,side*16,z*3.2f,z*Pit,z*6.4f,y);
                    foreach(float end in new[]{3.2f,Pit})DoorWall(room+"-end-"+end,true,z*end,side*min,side*max,side*centre,y);
                }
                // Same atrium opening rhythm on all storeys; lower sills and glass preserve fire/sight semantics.
                foreach(int axis in new[]{0,1})foreach(int side in new[]{-1,1})for(int w=0;w<6;w++)
                {
                    float t=-8+3.2f*w;
                    bool portal=axis==0?((f==0||f==2)&&Mathf.Abs(t)<2):((f==1&&Mathf.Abs(t)<2)||(f==2&&(Mathf.Abs(t-4.8f)<2||Mathf.Abs(t+4.8f)<2)));
                    if(portal)continue;
                    Vector3 Position(float h)=>axis==0?new Vector3(side*Pit,y+h,t):new Vector3(t,y+h,side*Pit);
                    Vector3 Size(float h,float thickness)=>axis==0?new Vector3(thickness,h,3.2f):new Vector3(3.2f,h,thickness);
                    Box(k+"sill-"+axis+side+w,Position(.4f),Size(.8f,.24f));
                    Box(k+"cap-"+axis+side+w,Position((3.4f+ClearHeight)/2),Size(ClearHeight-3.4f,.24f));
                    if((w+axis+f)%2==0)Box(k+"glass-"+axis+side+w,Position(2.1f),Size(2.6f,.10f),"glass",surface:"lunar-glass");
                }
                if(f==1)Deck("rocket-bridge",0,0,3.6f,19.5f,y,support,true);
                if(f==2)
                {
                    Deck("H-west",-4.8f,0,3,19.5f,y,support,true);Deck("H-east",4.8f,0,3,19.5f,y,support,true);
                    Deck("H-crossbar",0,0,19.5f,3,y,support,true);
                }
                foreach(int side in new[]{-1,1})Deck(k+"lift-lip"+side,0,side*25.2f,7,.8f,y,support);
            }
            Deck("garden",0,0,19.2f,19.2f,0,Support(0));
            float roof=3*Storey;
            foreach(int side in new[]{-1,1})
            {
                Deck("shaft-bottom"+side,0,side*28.7f,9.6f,7.8f,-.3f,"support:shaft-bottom");
                Box("lift-roof"+side,new Vector3(0,roof-Slab/2,side*28.7f),new Vector3(9.6f,Slab,7.8f));
                Box("shaft-back"+side,new Vector3(0,roof/2,side*32.6f),new Vector3(9.6f,roof,Wall));
                foreach(int x in new[]{-1,1})Box("shaft-side"+side+x,new Vector3(x*4.8f,roof/2,side*28.7f),new Vector3(Wall,roof,7.8f));
                // Extend the external annex, retaining the old 2.4 horizontal / 1 vertical flight slope.
                const float start=25.5f;float far=start+Storey/2*2.4f,back=far+2.8f,centre=(Edge+back)/2,width=back-Edge;
                Deck("stair-base"+side,side*centre,0,width,7.4f,0,Support(0));
                for(int f=0;f<2;f++)
                {
                    float y=f*Storey;string k="stair-"+side+"-"+f,mid="support:"+k+"-mid";
                    Deck(k+"start",side*25.3f,1.8f,1.2f,3.4f,y,Support(f));
                    Deck(k+"landing",side*(far+1.3f),0,3,7.2f,y+Storey/2,mid);
                    Deck(k+"end",side*25.05f,-1.8f,.9f,3.4f,y+Storey,Support(f+1));
                    void March(string id,Vector3 lo,Vector3 hi,string lower,string upper)
                    {
                        const int count=15;var dir=hi-lo;dir.y=0;float length=dir.magnitude;dir.Normalize();var feet=new List<Vector3>{lo-dir*.25f};
                        for(int i=0;i<count;i++)
                        {
                            float top=lo.y+(hi.y-lo.y)*(i+1)/count;var pos=lo+dir*length*(i+.5f)/count;pos.y=top-.12f;
                            Box(id+"-tread-"+i,pos,new Vector3(length/count,.24f,3.2f),"floor",id);feet.Add(new Vector3(pos.x,top,pos.z));
                        }
                        feet.Add(hi+dir*.25f);transitions.Add(new NativeNavigationTransition{Id=id,LowerSupport=lower,UpperSupport=upper,OrderedFeet=feet.ToArray()});
                    }
                    March("transition:"+k+"-low",new Vector3(side*start,y,1.8f),new Vector3(side*far,y+Storey/2,1.8f),Support(f),mid);
                    March("transition:"+k+"-high",new Vector3(side*far,y+Storey/2,-1.8f),new Vector3(side*start,y+Storey,-1.8f),mid,Support(f+1));
                }
                Box("stair-roof"+side,new Vector3(side*centre,roof-Slab/2,0),new Vector3(width,Slab,7.4f));
                Box("stair-back"+side,new Vector3(side*back,roof/2,0),new Vector3(Wall,roof,7.4f));
                foreach(int z in new[]{-1,1})Box("stair-side"+side+z,new Vector3(side*centre,roof/2,z*3.7f),new Vector3(width,roof,Wall));
            }
            foreach(var floor in solids.Where(s=>s.Id.StartsWith("f2-")&&(s.Id.EndsWith("north")||s.Id.EndsWith("south")||s.Id.EndsWith("west")||s.Id.EndsWith("east"))).ToArray())
                Box("roof-"+floor.Id,floor.Position+Vector3.up*Storey,floor.Size);
            solids.AddRange(WellFurniturePlan.Build());
            var items=new List<ArenaPickupDefinition>();
            void Item(string id,ArenaPickupKind kind,float x,float z,int f)=>items.Add(new ArenaPickupDefinition{Id=id,Kind=kind,Anchor=new Vector3(x,f*Storey,z),Support=Support(f)});
            Item("damage-garden",ArenaPickupKind.Damage,0,0,0);
            Item("armor-01",ArenaPickupKind.Armor,0,21,0);Item("shotgun-01",ArenaPickupKind.Shotgun,0,-21,0);
            Item("rocket-02",ArenaPickupKind.Pulse,0,0,1);
            // Immutable revision-5 anchors, exposed in Well authoring Lab; offset from corner spawns.
            Item("shotgun-02",ArenaPickupKind.Shotgun,-18,-20,1);Item("cutter-02",ArenaPickupKind.Cutter,18,20,1);
            Item("armor-02",ArenaPickupKind.Armor,-12.8f,6.4f,1);Item("speed-02",ArenaPickupKind.Speed,12.8f,-6.4f,1);
            Item("heal-H",ArenaPickupKind.FullHeal,0,0,2);
            Item("cutter-03",ArenaPickupKind.Cutter,21,21,2);Item("armor-03",ArenaPickupKind.Armor,-21,-21,2);
            return new ArenaDefinition
            {
                MapId=Id,Revision=Revision,Identity=Identity,ProfileFingerprint="authored:"+Identity,
                Solids=solids.ToArray(),Transitions=transitions.ToArray(),Spawns=spawns.ToArray(),SpawnRegions=regions.ToArray(),
                RouteAnchors=new[]{new Vector3(0,0,18),new Vector3(-21,Storey,0),new Vector3(0,2*Storey,-18)},Pickups=items.ToArray(),
                Lifts=new[]{new ArenaLiftDefinition{Id="north",Center=new Vector3(0,0,28.7f),Size=new Vector3(6.8f,.3f,6.2f),Bottom=0,Top=2*Storey,InitialHeight=2*Storey},new ArenaLiftDefinition{Id="south",Center=new Vector3(0,0,-28.7f),Size=new Vector3(6.8f,.3f,6.2f),Bottom=0,Top=2*Storey,InitialHeight=0}}
            };
        }
    }
}
