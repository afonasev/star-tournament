using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Frozen revision 1 authoring from main@3fcffe87, only for trusted historical Lab decoding. Never selected as the current arena.</summary>
    internal static class WellArenaRevisionOne
    {
        public const string Id="well-laboratory-v1",Revision="1",Identity=Id+"@"+Revision;
        public const float Storey=5, Edge=24.8f, Pit=9.6f;
        public static string Support(int floor)=>"support:well-"+floor;
        public static ProvingProfile AuthoringProfile()
        {
            var p=ProvingProfile.CreateCombatBowlAuthoringDefault(Build());p.SetIdentityForWell(1);p.AddWellLiftAuthoring(Build().Lifts);return p;
        }
        public static ArenaDefinition Build()
        {
            var solids=new List<ArenaSolid>();var transitions=new List<NativeNavigationTransition>();
            void Box(string id,Vector3 pos,Vector3 size,string material="wall",string support="",string surface=null,int layer=ProvingArena.WorldLayer)
                =>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Rotation=Quaternion.identity,Material=material,Support=support,Surface=surface,Layer=layer});
            void Deck(string id,float x,float z,float width,float depth,float y,string support,bool grate=false)
                =>Box(id,new Vector3(x,y-.15f,z),new Vector3(width,.3f,depth),"floor",support,grate?"well-grate":"well-floor",grate?ProvingArena.MovementOnlyLayer:ProvingArena.WorldLayer);
            // Drawing projection is fixed authored content, exported to the immutable Lab authoring profile.
            Vector3 P(float x,float z,float y)=>new Vector3((x-450)*.08f,y,(450-z)*.08f);
            void Line(string id,float x1,float z1,float x2,float z2,float y,float h=4.7f,string mat="wall")
            {var p1=P(x1,z1,y+h*.5f);var p2=P(x2,z2,y+h*.5f);Box(id,(p1+p2)*.5f,new Vector3(Mathf.Max(.24f,Mathf.Abs(p1.x-p2.x)),h,Mathf.Max(.24f,Mathf.Abs(p1.z-p2.z))),mat);}
            void Room(string id,float x,float z,float w,float d,float y,string doors="",bool closed=false)
            {
                int n=0;foreach(char side in "NSEW")
                {
                    bool horizontal=side=='N'||side=='S';float fixedPos=horizontal?(side=='N'?z:z+d):(side=='W'?x:x+w);
                    float start=horizontal?x:z, end=start+(horizontal?w:d),mid=(start+end)/2,halfDoor=24;
                    void Segment(float lo,float hi,string suffix){if(horizontal)Line(id+suffix+side,lo,fixedPos,hi,fixedPos,y);else Line(id+suffix+side,fixedPos,lo,fixedPos,hi,y);}
                    if(!doors.Contains(side)){Segment(start,end,"-wall-");continue;}
                    Segment(start,mid-halfDoor,"-left-");Segment(mid+halfDoor,end,"-right-");
                    if(horizontal)Line(id+"-header-"+side,mid-halfDoor,fixedPos,mid+halfDoor,fixedPos,y+3.2f,1.5f);else Line(id+"-header-"+side,fixedPos,mid-halfDoor,fixedPos,mid+halfDoor,y+3.2f,1.5f);
                    if(closed){if(horizontal)Line(id+"-closed-"+side,mid-halfDoor,fixedPos,mid+halfDoor,fixedPos,y,3.2f,"door");else Line(id+"-closed-"+side,fixedPos,mid-halfDoor,fixedPos,mid+halfDoor,y,3.2f,"door");}n++;
                }
            }
            for(int f=0;f<3;f++)
            {
                float y=f*Storey;string k="f"+f+"-",support=Support(f);
                Deck(k+"north",0,17.2f,49.6f,15.2f,y,support);Deck(k+"south",0,-17.2f,49.6f,15.2f,y,support);
                Deck(k+"west",-17.2f,0,15.2f,19.2f,y,support);Deck(k+"east",17.2f,0,15.2f,19.2f,y,support);
                // Exterior doors are 7m wide; every vertical node begins beyond the square.
                foreach(int side in new[]{-1,1})
                {
                    Box(k+"outer-x-a"+side,new Vector3(side*Edge,y+2.35f,-14.15f),new Vector3(.3f,4.7f,21.3f));
                    Box(k+"outer-x-b"+side,new Vector3(side*Edge,y+2.35f,14.15f),new Vector3(.3f,4.7f,21.3f));
                    Box(k+"outer-z-a"+side,new Vector3(-14.15f,y+2.35f,side*Edge),new Vector3(21.3f,4.7f,.3f));
                    Box(k+"outer-z-b"+side,new Vector3(14.15f,y+2.35f,side*Edge),new Vector3(21.3f,4.7f,.3f));
                    Box(k+"x-header"+side,new Vector3(side*Edge,y+4,0),new Vector3(.3f,1.4f,7));
                    Box(k+"z-header"+side,new Vector3(0,y+4,side*Edge),new Vector3(7,1.4f,.3f));
                }
                // Atrium windows alternate bullet-proof glass and open firing apertures. Bridge/garden portals remain clear.
                foreach(int axis in new[]{0,1})foreach(int side in new[]{-1,1})for(int w=0;w<6;w++)
                {
                    float t=-8+3.2f*w;bool portal=axis==0?(f==0&&Mathf.Abs(t)<2||f==2&&Mathf.Abs(t)<2):(f==1&&Mathf.Abs(t)<2||f==2&&(Mathf.Abs(t-4.8f)<2||Mathf.Abs(t+4.8f)<2));
                    if(portal)continue;
                    Vector3 Position(float h)=>axis==0?new Vector3(side*Pit,y+h,t):new Vector3(t,y+h,side*Pit);
                    Vector3 Size(float h,float thickness)=>axis==0?new Vector3(thickness,h,3.2f):new Vector3(3.2f,h,thickness);
                    Box(k+"sill-"+axis+side+w,Position(.4f),Size(.8f,.24f));Box(k+"cap-"+axis+side+w,Position(4.05f),Size(1.3f,.24f));
                    if((w+axis+f)%2==0)Box(k+"glass-"+axis+side+w,Position(2.1f),Size(2.6f,.10f),"glass",surface:"lunar-glass");
                }
                if(f==0)
                {
                    Room(k+"office-w-n",145,338,95,65,y,"E",true);Room(k+"office-w-s",145,498,95,65,y,"E",true);
                    Room(k+"office-e-n",574,342,92,90,y,"E",true);Room(k+"office-e-s",574,475,92,90,y,"E",true);
                    Room(k+"preparation",382,635,136,60,y,"NS");
                    Box(k+"exhibits",P(450,242,y+.7f),new Vector3(16,1.4f,3.04f),"equipment");
                    Box(k+"bio-bank",P(677,680,y+.7f),new Vector3(5.2f,1.4f,5.2f),"equipment");
                    Line(k+"greenhouse-a",585,235,648,235,y);Line(k+"greenhouse-b",702,235,760,235,y);
                    Line(k+"receiving-a",190,620,280,620,y);Line(k+"receiving-b",280,660,280,715,y);
                }
                if(f==1)
                {
                    Room(k+"office-w-n",243,340,84,75,y,"W",true);Room(k+"office-w-s",243,485,84,75,y,"W",true);
                    Room(k+"test-through",634,385,61,130,y,"WE");Room(k+"office-s-w",333,675,67,82,y,"N",true);Room(k+"office-s-e",500,675,67,82,y,"N",true);
                    Line(k+"optics-a",298,140,298,215,y);Line(k+"optics-b",298,280,298,330,y);
                    Line(k+"instrument",140,643,220,643,y);Line(k+"instrument-b",280,643,328,643,y);
                    Box(k+"optical-table",P(678,233,y+.7f),new Vector3(6,1.4f,2.88f),"equipment");
                    Deck("damage-bridge",0,0,3.6f,19.5f,y,support,true);
                }
                if(f==2)
                {
                    Room(k+"assembly",205,215,60,55,y,"NS");Room(k+"office-n-w",333,143,67,90,y,"S",true);Room(k+"office-n-e",500,143,67,90,y,"S",true);
                    Room(k+"office-w-n",243,340,84,75,y,"W",true);Room(k+"office-w-s",243,485,84,75,y,"W",true);
                    Room(k+"office-e-n",665,340,92,75,y,"W",true);Room(k+"office-e-s",665,485,92,75,y,"W",true);
                    Room(k+"repair",635,645,70,60,y,"WE");
                    Deck("H-west",-4.8f,0,3,19.5f,y,support,true);Deck("H-east",4.8f,0,3,19.5f,y,support,true);Deck("H-crossbar",0,0,19.5f,3,y,support,true);
                }
                // Annex lobbies end at the shaft lip; no floor bridges through a moving platform.
                foreach(int side in new[]{-1,1})
                {
                    Deck(k+"lift-lip"+side,0,side*25.2f,7,.8f,y,support);
                    Box(k+"shaft-back"+side,new Vector3(0,y+2.35f,side*32.6f),new Vector3(9.6f,4.7f,.3f));
                    foreach(int x in new[]{-1,1})Box(k+"shaft-side"+side+x,new Vector3(x*4.8f,y+2.35f,side*28.7f),new Vector3(.3f,4.7f,7.8f));
                }
            }
            Deck("garden",0,0,19.2f,19.2f,0,Support(0));
            foreach(int side in new[]{-1,1}){Deck("shaft-bottom"+side,0,side*28.7f,9.6f,7.8f,-.3f,"support:shaft-bottom");Box("lift-roof"+side,new Vector3(0,14.85f,side*28.7f),new Vector3(9.6f,.3f,7.8f));}
            
            foreach(int side in new[]{-1,1})
            {
                Deck("stair-base"+side,side*29.55f,0,9.5f,7.4f,0,Support(0));
                // Two spacious 3.2m switchback marches per storey, outside the square.
                for(int f=0;f<2;f++)
                {
                    float y=f*Storey;string k="stair-"+side+"-"+f;string mid="support:"+k+"-mid";
                    Deck(k+"start",side*25.3f,1.8f,1.2f,3.4f,y,Support(f));Deck(k+"landing",side*32.8f,0,3,7.2f,y+2.5f,mid);
                    Deck(k+"end",side*25.05f,-1.8f,.9f,3.4f,y+Storey,Support(f+1));
                    void March(string id,Vector3 lo,Vector3 hi,string lower,string upper)
                    {
                        const int count=10;var dir=(hi-lo);dir.y=0;float length=dir.magnitude;dir.Normalize();var feet=new List<Vector3>{lo-dir*.25f};
                        for(int i=0;i<count;i++){float top=lo.y+(hi.y-lo.y)*(i+1)/count;var pos=lo+dir*length*(i+.5f)/count;pos.y=top-.12f;Box(id+"-tread-"+i,pos,new Vector3(length/count,.24f,3.2f),"floor",id);feet.Add(new Vector3(pos.x,top,pos.z));}
                        feet.Add(hi+dir*.25f);transitions.Add(new NativeNavigationTransition{Id=id,LowerSupport=lower,UpperSupport=upper,OrderedFeet=feet.ToArray()});
                    }
                    March("transition:"+k+"-low",new Vector3(side*25.5f,y,1.8f),new Vector3(side*31.5f,y+2.5f,1.8f),Support(f),mid);
                    March("transition:"+k+"-high",new Vector3(side*31.5f,y+2.5f,-1.8f),new Vector3(side*25.5f,y+5,-1.8f),mid,Support(f+1));
                    // Solid underside prevents entering the dead volume below ascending flights.
                }
                Box("stair-roof"+side,new Vector3(side*29.55f,14.85f,0),new Vector3(9.5f,.3f,7.4f));
                Box("stair-back"+side,new Vector3(side*34.3f,7.4f,0),new Vector3(.3f,14.8f,7.4f));
                foreach(int z in new[]{-1,1})Box("stair-side"+side+z,new Vector3(side*29.55f,7.4f,z*3.7f),new Vector3(9.5f,14.8f,.3f));
            }
            // Roof follows the ring; the atrium remains open to a luminous skylight.
            foreach(var floor in solids.Where(s=>s.Id.StartsWith("f2-")&&(s.Id.EndsWith("north")||s.Id.EndsWith("south")||s.Id.EndsWith("west")||s.Id.EndsWith("east"))).ToArray())
                Box("roof-"+floor.Id,floor.Position+Vector3.up*5,floor.Size,"wall");
            var items=new List<ArenaPickupDefinition>();
            void Item(string id,ArenaPickupKind kind,float x,float z,int f)=>items.Add(new ArenaPickupDefinition{Id=id,Kind=kind,Anchor=P(x,z,f*Storey),Support=Support(f)});
            Item("shotgun-01",ArenaPickupKind.Shotgun,220,285,0);Item("rocket-01",ArenaPickupKind.Pulse,315,692,0);Item("armor-garden",ArenaPickupKind.Armor,426,507,0);Item("speed-01",ArenaPickupKind.Speed,710,583,0);
            Item("damage-bridge",ArenaPickupKind.Damage,450,389,1);Item("rocket-02",ArenaPickupKind.Pulse,723,325,1);Item("cutter-02",ArenaPickupKind.Cutter,254,709,1);Item("armor-02",ArenaPickupKind.Armor,465,270,1);
            Item("heal-H",ArenaPickupKind.FullHeal,450,450,2);Item("cutter-03",ArenaPickupKind.Cutter,285,328,2);Item("shotgun-03",ArenaPickupKind.Shotgun,731,710,2);Item("speed-03",ArenaPickupKind.Speed,325,631,2);
            var spawns=new[]{P(210,190,0),P(700,735,0),P(185,580,5),P(690,180,5),P(450,735,10),P(190,300,10),P(610,600,10),P(590,290,0)};
            // Repositioned spawn pockets are sheltered by substantial lab equipment, with two broad exits.
            for(int i=0;i<spawns.Length;i++)
            {
                var p=spawns[i];float sx=i==4?1:p.x>=0?-1:1,sz=p.z>=0?-1:1;
                Box("spawn-cover-x-"+i,p+new Vector3(sx*2.3f,1.6f,0),new Vector3(.3f,3.2f,i==1?5.2f:4.4f),"equipment");
                Box("spawn-cover-z-"+i,p+new Vector3(0,1.6f,sz*2.3f),new Vector3(4.4f,3.2f,.3f),"equipment");
            }
            return new ArenaDefinition{MapId=Id,Revision=Revision,Identity=Identity,ProfileFingerprint="authored:"+Identity,Solids=solids.ToArray(),Transitions=transitions.ToArray(),Spawns=spawns,SpawnRegions=spawns.Select((s,i)=>new ArenaSpawnRegion{Id="spawn-region-"+i,Center=s,Size=new Vector3(2,0,2),Support=Support(Mathf.RoundToInt(s.y/Storey))}).ToArray(),RouteAnchors=new[]{P(290,290,0),P(450,275,5),P(450,650,10)},Pickups=items.ToArray(),Lifts=new[]{new ArenaLiftDefinition{Id="north",Center=new Vector3(0,0,28.7f),Size=new Vector3(6.8f,.3f,6.2f),Bottom=0,Top=10,InitialHeight=10},new ArenaLiftDefinition{Id="south",Center=new Vector3(0,0,-28.7f),Size=new Vector3(6.8f,.3f,6.2f),Bottom=0,Top=10,InitialHeight=0}}};
        }
    }
}
