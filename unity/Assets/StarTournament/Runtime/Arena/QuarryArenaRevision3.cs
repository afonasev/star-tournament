using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    // Approved concept A: three open quarry benches. Dimensions are exported to authoring metadata.
    public static class QuarryArenaRevision3
    {
        public const string Id="quarry-prototype-v1", Revision="3", Identity=Id+"@"+Revision;
        public const string Ground="support:quarry-ground", Terrace="support:quarry-terrace", Upper="support:quarry-upper";
        public const float TerraceHeight=4, UpperHeight=8;
        public static readonly Vector3 FrontPortal=new Vector3(-6,0,-13), EastPortal=new Vector3(15,0,4);
        public static ArenaDefinition Build()
        {
            var solids=new List<ArenaSolid>();var transitions=new List<NativeNavigationTransition>();
            void Box(string id,Vector3 pos,Vector3 size,string material="quarry-rock",string support=null,Quaternion? rotation=null)
                =>solids.Add(new ArenaSolid{Id=id,Position=pos,Size=size,Material=material,Support=support,Layer=ProvingArena.WorldLayer,Rotation=rotation??Quaternion.identity});
            Box("quarry-ground",new Vector3(0,-.25f,1),new Vector3(100,.5f,82),"floor",Ground);
            void Bench(string id,float x,float z,float width,float depth,float height,string support)
            {
                // Separate rock body and thin walking cap retain one continuous solid bench.
                Box(id+"-rock",new Vector3(x,(height-.08f)*.5f,z),new Vector3(width,height-.08f,depth));
                Box(id,new Vector3(x,height-.04f,z),new Vector3(width,.08f,depth),"floor",support);
            }
            Bench("middle-west",-29,6,30,28,4,Terrace);
            Bench("middle-east",33,6,22,28,4,Terrace);
            Bench("middle-crossing",4,16,36,8,4,Terrace);
            Bench("upper-excavation",0,29,88,18,8,Upper);
            Box("rear-cliff",new Vector3(0,8,40),new Vector3(100,16,4));
            Box("front-rim",new Vector3(0,1.5f,-39),new Vector3(100,3,2));
            foreach(float side in new[]{-1f,1f})
            {
                Box("lower-rim-"+side,new Vector3(side*47,3,-24),new Vector3(6,6,32));
                Box("middle-rim-"+side,new Vector3(side*47,6,6),new Vector3(6,12,28));
                Box("upper-rim-"+side,new Vector3(side*47,8,30),new Vector3(6,16,20));
            }
            void Ramp(string id,float x,float lowY,float lowZ,string lower,string upper)
            {
                var low=new Vector3(x,lowY,lowZ);var high=low+new Vector3(0,4,20);var rotation=Quaternion.LookRotation(high-low,Vector3.up);float length=Vector3.Distance(low,high);
                Box(id,(low+high)*.5f-(rotation*Vector3.up)*.2f,new Vector3(8,.4f,length),"floor",id,rotation);
                foreach(float side in new[]{-1f,1f})Box(id+"-guard-"+side,(low+high)*.5f+Vector3.right*(side*4.3f)+(rotation*Vector3.up)*.5f,new Vector3(.6f,1.2f,length+1),"quarry-cover",null,rotation);
                // Low-cost filled ramp; guards prevent undeclared lateral ground/ramp/ground shortcuts.
                const int sections=32;
                for(int i=0;i<sections;i++)
                {
                    var p=Vector3.Lerp(low,high,(i+.5f)/sections);float h=Mathf.Max(.02f,lowY+4f*i/sections-.42f);
                    Box(id+"-fill-"+i,new Vector3(p.x,h*.5f,p.z),new Vector3(8,h,20f/sections));
                }
                var feet=new List<Vector3>{low-Vector3.forward*.35f};
                for(int i=1;i<20;i++)feet.Add(Vector3.Lerp(low,high,i/20f));feet.Add(high+Vector3.forward*.4f);
                transitions.Add(new NativeNavigationTransition{Id=id,LowerSupport=lower,UpperSupport=upper,OrderedFeet=feet.ToArray()});
            }
            foreach(float side in new[]{-1f,1f})
            {
                string flank=side<0?"west":"east";
                Ramp("transition:quarry-"+flank+"-lower",side*38,0,-28,Ground,Terrace);
                Ramp("transition:quarry-"+flank+"-upper",side*38,4,0,Terrace,Upper);
            }
            // Only this small outcrop is roofed. The remaining center stays open to the sky.
            // Cells are merged by identical base/top so physical and rendered tunnel faces coincide.
            const int nx=14,nz=12;var mask=new int[nx,nz];
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                float px=-14+x*2+1,pz=-12+z*2+1;
                if((Mathf.Abs(px)>11&&pz< -8)||(px< -11&&pz>8))continue;
                bool passage=(Mathf.Abs(px+6)<=3.5f&&pz<=7.5f)||(Mathf.Abs(pz-4)<=3.5f&&px>=-9.5f);
                int top=4;
                mask[x,z]=top*10+(passage?2:1);
            }
            int index=0;
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++)
            {
                int type=mask[x,z];if(type==0)continue;int width=1,depth=1;
                while(x+width<nx&&mask[x+width,z]==type)width++;
                while(z+depth<nz){bool same=true;for(int dx=0;dx<width;dx++)same&=mask[x+dx,z+depth]==type;if(!same)break;depth++;}
                for(int dx=0;dx<width;dx++)for(int dz=0;dz<depth;dz++)mask[x+dx,z+dz]=0;
                float bottom=type%10==2?3.5f:0,top=type/10;
                Box((bottom>0?"tunnel-roof-":"outcrop-")+index++,new Vector3(-14+(x+width*.5f)*2,(bottom+top)*.5f,-12+(z+depth*.5f)*2),new Vector3(width*2,top-bottom,depth*2),"quarry-rock",Terrace);
            }
            // Framed openings are visible from the lower court and the eastern approach.
            foreach(float x in new[]{-9.5f,-2.5f})Box("front-portal-pillar-"+x,new Vector3(x,1.8f,-12.4f),new Vector3(1,3.6f,1),"quarry-portal");
            Box("front-portal-lintel",new Vector3(-6,3.85f,-12.4f),new Vector3(8,.7f,1),"quarry-portal");
            foreach(float z in new[]{.5f,7.5f})Box("east-portal-pillar-"+z,new Vector3(14.4f,1.8f,z),new Vector3(1,3.6f,1),"quarry-portal");
            Box("east-portal-lintel",new Vector3(14.4f,3.85f,4),new Vector3(1,.7f,8),"quarry-portal");
            Box("front-tunnel-lamp",new Vector3(-6,3.3f,-9),new Vector3(.4f,.2f,.4f),"quarry-portal");
            Box("east-tunnel-lamp",new Vector3(11,3.3f,4),new Vector3(.4f,.2f,.4f),"quarry-portal");
            var spawns=new[]{new Vector3(-23,0,-31),new Vector3(23,0,-31),new Vector3(-26,4,-2),new Vector3(-26,4,14),new Vector3(27,4,7),new Vector3(-28,8,31),new Vector3(0,8,31),new Vector3(28,8,31)};
            QuarryIndustrialDetailsRevision3.Add(solids,spawns);
            // Both roof access paths are coplanar with the existing middle support.
            Box("center-east-bridge",new Vector3(18,3.8f,9.5f),new Vector3(8,.4f,4),"quarry-metal",Terrace);
            Box("center-east-bridge-rail",new Vector3(18,4.55f,11.7f),new Vector3(8,1.1f,.18f),"quarry-portal");
            string Support(Vector3 p)=>p.y==8?Upper:p.y==4?Terrace:Ground;
            ArenaPickupDefinition Item(string id,ArenaPickupKind kind,Vector3 p)=>new ArenaPickupDefinition{Id=id,Kind=kind,Anchor=p,Support=Support(p)};
            var pickups=new[]{
                Item("quarry-shotgun-west",ArenaPickupKind.Shotgun,new Vector3(-20,4,5)),Item("quarry-shotgun-east",ArenaPickupKind.Shotgun,new Vector3(22,0,-17)),
                Item("quarry-rocket-north",ArenaPickupKind.Pulse,new Vector3(9,8,25)),Item("quarry-laser-terrace",ArenaPickupKind.Cutter,new Vector3(-16,8,26)),Item("quarry-plasma-west",ArenaPickupKind.Plasma,new Vector3(-28,4,7)),
                Item("quarry-armor-west",ArenaPickupKind.Armor,new Vector3(-18,4,-2)),Item("quarry-armor-east",ArenaPickupKind.Armor,new Vector3(26,4,-4)),
                Item("quarry-heal-tunnel",ArenaPickupKind.FullHeal,new Vector3(0,0,4)),Item("quarry-speed-west",ArenaPickupKind.Speed,new Vector3(-28,0,-21)),Item("quarry-damage-south",ArenaPickupKind.Damage,new Vector3(0,0,-26))};
            return new ArenaDefinition{MapId=Id,Revision=Revision,Identity=Identity,ProfileFingerprint="authored:"+Identity,Solids=solids.ToArray(),Spawns=spawns,
                SpawnRegions=spawns.Select((p,i)=>new ArenaSpawnRegion{Id="quarry-spawn-"+(i+1),Center=p,Size=new Vector3(1.5f,0,1.5f),Support=Support(p)}).ToArray(),
                Transitions=transitions.ToArray(),RouteAnchors=new[]{new Vector3(0,0,-20),new Vector3(0,8,22)},Pickups=pickups};
        }
        public static ProvingProfile AuthoringProfile()=>ProvingProfile.CreateQuarryAuthoring(Build());
    }
}
