using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    // Prototype layout 01. All generated solids/anchors are exported to immutable authoring metadata.
    public static class QuarryArenaCatalog
    {
        public const string Id="quarry-prototype-v1", Revision="1", Identity=Id+"@"+Revision;
        public const string Ground="support:quarry-ground", Terrace="support:quarry-terrace";
        public const float TerraceHeight=4;
        static readonly Vector2[] West={new Vector2(-13,27),new Vector2(-25,18),new Vector2(-35,8),new Vector2(-33,-5)};
        static readonly Vector2[] South={new Vector2(-33,-16),new Vector2(-20,-29),new Vector2(4,-31),new Vector2(31,-16)};
        static readonly Vector2[] Tunnel={new Vector2(-24,-5),new Vector2(-16,-5),new Vector2(-12,3),new Vector2(8,3),new Vector2(12,-5),new Vector2(21,-5)};
        static bool Inside(Vector2 p,float x1,float z1,float x2,float z2)=>p.x>=x1&&p.x<=x2&&p.y>=z1&&p.y<=z2;
        static bool Along(Vector2 p,Vector2[] path,float half)
        {
            for(int i=1;i<path.Length;i++)
            {var a=path[i-1];var v=path[i]-a;var t=Mathf.Clamp01(Vector2.Dot(p-a,v)/v.sqrMagnitude);if(Vector2.Distance(p,a+v*t)<=half)return true;}
            return false;
        }
        static bool Open(Vector2 p)=>Inside(p,-19,24,6,38)||Inside(p,-46,-19,-22,2)||Inside(p,19,-19,44,2)||
            Inside(p,-26,-34,-13,-22)||Inside(p,9,-33,23,-21)||Inside(p,-11,-36,8,-24)||
            Along(p,West,4.5f)||Along(p,South,5)||Inside(p,2,24,28,32)||Inside(p,19,-14,33,28);
        public static ArenaDefinition Build()
        {
            var solids=new List<ArenaSolid>();var transitions=new List<NativeNavigationTransition>();
            void Box(string id,Vector3 pos,Vector3 size,string material="quarry-rock",string support=null,Quaternion? rotation=null)
                =>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Material=material,Support=support,Layer=ProvingArena.WorldLayer,Rotation=rotation??Quaternion.identity});
            Box("quarry-ground",new Vector3(0,-.25f,1),new Vector3(100,.5f,82),"floor",Ground);
            // Greedy rectangles preserve the exact mask, reducing hundreds of identical cells to simple boxes.
            const float cell=2;const int nx=50,nz=41;var mask=new int[nx,nz];
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                var p=new Vector2(-50+(x+.5f)*cell,-40+(z+.5f)*cell);
                bool tunnel=Along(p,Tunnel,3.5f);
                mask[x,z]=Open(p)?0:tunnel?2:1;
            }
            int index=0;
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                int type=mask[x,z];if(type==0)continue;int width=1,depth=1;
                while(x+width<nx&&mask[x+width,z]==type)width++;
                while(z+depth<nz){bool same=true;for(int dx=0;dx<width;dx++)same&=mask[x+dx,z+depth]==type;if(!same)break;depth++;}
                for(int dx=0;dx<width;dx++)for(int dz=0;dz<depth;dz++)mask[x+dx,z+dz]=0;
                float bottom=type==2?3.4f:0,top=7;
                Box((type==2?"tunnel-roof-":"rock-mass-")+index++,new Vector3(-50+(x+width*.5f)*cell,(top+bottom)*.5f,-40+(z+depth*.5f)*cell),new Vector3(width*cell,top-bottom,depth*cell));
            }
            // A solid terrace and filled continuous ramps: no walkable void under the raised route.
            Box("terrace",new Vector3(26,2,8),new Vector3(14,4,8),"floor",Terrace);
            void Ramp(string id,Vector3 low,Vector3 high)
            {
                var rotation=Quaternion.LookRotation(high-low,Vector3.up);float length=Vector3.Distance(low,high);
                Box(id,(low+high)*.5f-(rotation*Vector3.up)*.2f,new Vector3(8,.4f,length),"floor",id,rotation);
                // Continuous side guards make each ramp a complete ground-to-terrace route.
                // Without them the baker can cut across its low corner and return to ground.
                foreach(float side in new[]{-1f,1f})
                    Box(id+"-guard-"+side,(low+high)*.5f+Vector3.right*(side*4.3f)+(rotation*Vector3.up)*.6f,new Vector3(.6f,1.4f,length+1),"quarry-cover",null,rotation);
                const int sections=32;
                for(int i=0;i<sections;i++)
                {
                    float t=(i+.5f)/sections;var p=Vector3.Lerp(low,high,t);float h=Mathf.Max(.02f,4f*i/sections-.42f);
                    Box(id+"-fill-"+i,new Vector3(p.x,h*.5f,p.z),new Vector3(8,h,16f/sections),"quarry-ramp-fill");
                }
                var feet=new List<Vector3>{low-(high-low).normalized*.35f};feet[0]=new Vector3(feet[0].x,0,feet[0].z);
                for(int i=1;i<16;i++)feet.Add(Vector3.Lerp(low,high,i/16f));
                var end=high+(new Vector3(high.x-low.x,0,high.z-low.z)).normalized*.4f;feet.Add(end);
                transitions.Add(new NativeNavigationTransition{Id=id,LowerSupport=Ground,UpperSupport=Terrace,OrderedFeet=feet.ToArray()});
            }
            Ramp("transition:quarry-north",new Vector3(24,0,28),new Vector3(24,4,12));
            Ramp("transition:quarry-south",new Vector3(28,0,-12),new Vector3(28,4,4));
            Vector3 Pixel(float x,float y,float height=0)=>new Vector3((x-800)/16,height,(700-y)/16);
            void Cover(string name,float x1,float y1,float x2,float y2,float height,bool low=false)
            {Box(name,Pixel((x1+x2)*.5f,(y1+y2)*.5f,height*.5f),new Vector3((x2-x1)/16,height,(y2-y1)/16),low?"quarry-crate":"quarry-cover");}
            var screens=new[]{new Vector4(620,133,665,220),new Vector4(745,133,790,220),new Vector4(215,710,260,800),new Vector4(215,890,260,970),new Vector4(100,830,178,865),new Vector4(1320,710,1365,800),new Vector4(1320,890,1365,970),new Vector4(1400,830,1478,865),new Vector4(465,1070,530,1120),new Vector4(405,1090,435,1140),new Vector4(1005,1050,1050,1110),new Vector4(1110,1080,1155,1125),new Vector4(705,1105,760,1170),new Vector4(324,430,365,465)};
            for(int i=0;i<screens.Length;i++){var v=screens[i];Cover("spawn-screen-"+i,v.x,v.y,v.z,v.w,6);}
            Box("southwest-spawn-screen",new Vector3(-29,3,-24),new Vector3(2,6,6),"quarry-cover");
            var crates=new[]{new Vector4(610,290,665,315),new Vector4(775,290,835,315),new Vector4(290,935,345,965),new Vector4(383,840,425,868),new Vector4(1190,950,1245,978),new Vector4(1120,840,1160,867),new Vector4(875,1230,915,1260)};
            for(int i=0;i<crates.Length;i++){var v=crates[i];Cover("low-crate-"+i,v.x,v.y,v.z,v.w,1.2f,true);}
            Box("terrace-crate",new Vector3(21,4.6f,8),new Vector3(1.6f,1.2f,2),"quarry-crate");
            var spawns=new[]{Pixel(550,160),Pixel(850,160),Pixel(145,750),Pixel(145,940),Pixel(1430,750),Pixel(1430,940),Pixel(440,1160),Pixel(1080,1145)};
            ArenaPickupDefinition Item(string id,ArenaPickupKind kind,Vector3 p,string support=Ground)=>new ArenaPickupDefinition{Id=id,Kind=kind,Anchor=p,Support=support};
            var pickups=new[]{
                Item("quarry-shotgun-west",ArenaPickupKind.Shotgun,Pixel(360,880)),Item("quarry-shotgun-east",ArenaPickupKind.Shotgun,new Vector3(30,0,-15)),
                Item("quarry-rocket-north",ArenaPickupKind.Pulse,Pixel(700,272)),Item("quarry-laser-terrace",ArenaPickupKind.Cutter,new Vector3(27,4,8)),Item("quarry-plasma-west",ArenaPickupKind.Plasma,Pixel(340,495)),
                Item("quarry-armor-west",ArenaPickupKind.Armor,Pixel(360,735)),Item("quarry-armor-east",ArenaPickupKind.Armor,new Vector3(21,0,-1)),
                Item("quarry-heal-tunnel",ArenaPickupKind.FullHeal,new Vector3(-2,0,3)),Item("quarry-speed-west",ArenaPickupKind.Speed,Pixel(250,615)),Item("quarry-damage-south",ArenaPickupKind.Damage,Pixel(825,1200))};
            pickups[3].Support=Terrace;
            return new ArenaDefinition{MapId=Id,Revision=Revision,Identity=Identity,ProfileFingerprint="authored:"+Identity,Solids=solids.ToArray(),Spawns=spawns,
                SpawnRegions=spawns.Select((p,i)=>new ArenaSpawnRegion{Id="quarry-spawn-"+(i+1),Center=p,Size=new Vector3(1.5f,0,1.5f),Support=Ground}).ToArray(),
                Transitions=transitions.ToArray(),RouteAnchors=new[]{new Vector3(-33,0,-5),new Vector3(27,4,8)},Pickups=pickups};
        }
        public static ProvingProfile AuthoringProfile()=>ProvingProfile.CreateQuarryAuthoring(Build());
    }
}
