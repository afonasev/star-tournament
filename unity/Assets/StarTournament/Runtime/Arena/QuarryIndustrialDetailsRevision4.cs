using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    // All blocking silhouettes are authored solids, exported to the quarry Balance Lab profile.
    internal static class QuarryIndustrialDetailsRevision4
    {
        public static void Add(List<ArenaSolid> solids,Vector3[] spawns)
        {
            void Box(string id,Vector3 p,Vector3 s,string mat="quarry-rock",float yaw=0,string shape=null,Quaternion? rotation=null)
                =>solids.Add(new ArenaSolid{Id=id,Position=p,Size=s,Material=mat,Surface=shape,Rotation=rotation??Quaternion.Euler(0,yaw,0),Layer=ProvingArena.WorldLayer});
            void Rock(string id,Vector3 foot,Vector3 size,float yaw=0)=>Box(id,foot+Vector3.up*size.y*.5f,size,"quarry-rock",yaw,"quarry-faceted");
            // Freestanding cargo and rock fragments replace the entire repeated spawn-room template.
            // Each footprint is a canonical solid/profile entry: no invisible spawn shields.
            void Boulder(string id,Vector3 foot,Vector3 size,float yaw=0,bool slab=false)=>Box(id,foot+Vector3.up*size.y*.5f,size,"quarry-rock",yaw,slab?"quarry-slab":"quarry-boulder");
            void Crate(string id,Vector3 foot,Vector3 size,float yaw=0)=>Box(id,foot+Vector3.up*size.y*.5f,size,"quarry-crate",yaw);
            // Lower loading area: detached cut rock, a cargo stack and small loose debris.
            Boulder("loading-west-boulder",new Vector3(-23,0,-26.5f),new Vector3(7.2f,4.1f,4.3f),-12);
            Boulder("loading-west-chip",new Vector3(-28.5f,0,-28),new Vector3(2.5f,1.1f,2),18,true);
            Crate("loading-cargo-base",new Vector3(21,0,-27),new Vector3(5.5f,2.4f,2.8f),8);
            Crate("loading-cargo-top",new Vector3(21.6f,2.4f,-27),new Vector3(3.1f,1.2f,2.2f),-5);
            Boulder("loading-cross-cover",new Vector3(-3,0,-31),new Vector3(5.5f,3.3f,4),22,true);
            Crate("loading-loose-crate",new Vector3(28,0,-24),new Vector3(2,1.3f,1.8f),-19);
            // Middle shelf: separate angled outcrops, with no roofs or room-shaped rings.
            Boulder("middle-west-front",new Vector3(-21.5f,4,-.8f),new Vector3(3.4f,4,7),-14);
            Boulder("middle-west-broken-toe",new Vector3(-23.1f,4,-4.6f),new Vector3(3.5f,2.6f,3.1f),20,true);
            Boulder("middle-west-divider",new Vector3(-26,4,4),new Vector3(5.5f,3.2f,2.6f),-8,true);
            Boulder("middle-north-front",new Vector3(-21.6f,4,14),new Vector3(3.4f,3.6f,5.3f),16);
            Boulder("middle-east-front",new Vector3(26.3f,4,3.1f),new Vector3(6.3f,3.5f,2.8f),9,true);
            Boulder("middle-east-chip",new Vector3(30.5f,4,9),new Vector3(2.7f,1.5f,2.2f),-21);
            // Open workshop storage: individual wooden shipping cases, sheet bundles and a generator.
            Crate("workshop-west-cargo",new Vector3(-28,8,27.5f),new Vector3(6,2.7f,2.4f),-5);
            Crate("workshop-west-top",new Vector3(-29,10.7f,27.5f),new Vector3(2.2f,1,1.8f),8);
            Crate("workshop-center-cargo",new Vector3(0,8,27.4f),new Vector3(5.5f,2.8f,2.5f),4);
            Box("workshop-east-generator",new Vector3(28,9.6f,27.5f),new Vector3(5.4f,3.2f,2.7f),"quarry-machine",-7);
            Crate("workshop-divider-west",new Vector3(-14,8,31),new Vector3(3.4f,2.9f,4),9);
            Crate("workshop-divider-east",new Vector3(14,8,31),new Vector3(3.2f,2.7f,4.2f),-12);
            Crate("workshop-loose-case",new Vector3(-8,8,34),new Vector3(2,1.1f,1.5f),-17);
            // Exposed geological shoulders break the large plane silhouettes; keep the ramp envelope clear.
            for(int row=0;row<3;row++)
            {
                float floor=row*4;float start=row==0?-34:row==1?-5:23;float end=row==0?-12:row==1?17:36;
                for(float z=start;z<end;z+=5.5f)foreach(float side in new[]{-1f,1f})
                    Rock("rim-face-"+row+"-"+side+"-"+z,new Vector3(side*44.6f,floor,z),new Vector3(1.6f,3.8f,5.8f),side*5);
            }
            for(float x=-29;x<=29;x+=6)
                Rock("upper-cliff-face-"+x,new Vector3(x,4,20.25f),new Vector3(6.4f,3.9f,1.4f),x%3);
            foreach(float x in new[]{-12f,2f,7f,12f})Rock("outcrop-front-face-"+x,new Vector3(x,0,-11.9f),new Vector3(3.8f,3.5f,1.1f),x%4);
            // Upper compound: continuous metal rear wall, asymmetric canopy and work bays.
            Box("workshop-back",new Vector3(0,10.4f,37.5f),new Vector3(83,4.8f,.5f),"quarry-metal");
            Box("workshop-awning",new Vector3(-9,12.6f,31),new Vector3(51,.25f,9),"quarry-metal",0,null,Quaternion.Euler(-7,0,0));
            foreach(float x in new[]{-33f,-11f,15f})Box("workshop-post-"+x,new Vector3(x,10.15f,26.9f),new Vector3(.3f,4.3f,.3f),"quarry-portal");
            // Only short segments at the edge: upper floor remains open for combat and circulation.
            foreach(float x in new[]{-21f,3f,23f})Box("upper-parapet-"+x,new Vector3(x,8.6f,21),new Vector3(5,1.2f,.6f),"quarry-metal");
            foreach(float x in new[]{-36f,11f,36f})Box("workshop-cabinet-"+x,new Vector3(x,9.05f,36.6f),new Vector3(2,2.1f,1.1f),"quarry-machine");
            // Stone silhouettes across the middle, plus low cover around an OPEN center roof.
            Rock("middle-stone-west",new Vector3(-18,4,10),new Vector3(3.2f,1.6f,2.2f),-18);
            Rock("middle-stone-east",new Vector3(29,4,17),new Vector3(4.5f,2.2f,2.8f),12);
            Rock("center-low-west",new Vector3(-10,4,-7),new Vector3(4,1.1f,2),-14);
            Rock("center-low-east",new Vector3(9,4,-6),new Vector3(3.2f,1.3f,2),18);
            Rock("center-low-rear",new Vector3(-8,4,9),new Vector3(3,1.2f,2),8);
            Rock("ground-split-stone",new Vector3(12,0,-22),new Vector3(4.2f,2.4f,2.6f),-12);
            Rock("ground-stone-step",new Vector3(15,0,-23.2f),new Vector3(2.6f,1.2f,2.5f),8);
            // Loader: stationary recognisable articulated silhouette with real collision per part.
            var origin=new Vector3(-11,0,-23);var turn=Quaternion.Euler(0,-28,0);
            void Loader(string id,Vector3 p,Vector3 size,string mat,Quaternion? rot=null,string shape=null)=>Box("loader-"+id,origin+turn*p,size,mat,0,shape,turn*(rot??Quaternion.identity));
            Loader("chassis",new Vector3(0,1.35f,0),new Vector3(3.2f,1.1f,6.4f),"quarry-portal");
            Loader("engine",new Vector3(0,2.1f,-2),new Vector3(2.7f,1.4f,2.2f),"quarry-portal");
            Loader("cab",new Vector3(0,3.1f,-.25f),new Vector3(2.3f,2.2f,2),"quarry-glass");
            Loader("cab-roof",new Vector3(0,4.3f,-.25f),new Vector3(2.6f,.25f,2.3f),"quarry-portal");
            foreach(float x in new[]{-1.65f,1.65f})foreach(float z in new[]{-2.05f,2f})
                Loader("wheel-"+x+"-"+z,new Vector3(x,1.1f,z),new Vector3(2.2f,.75f,2.2f),"quarry-rubber",Quaternion.Euler(0,0,90),"quarry-cylinder");
            foreach(float x in new[]{-1f,1f})Loader("arm-"+x,new Vector3(x,1.65f,3.25f),new Vector3(.35f,.45f,3.6f),"quarry-portal",Quaternion.Euler(20,0,0));
            Loader("bucket-floor",new Vector3(0,.35f,4.8f),new Vector3(4,.25f,2.3f),"quarry-machine");
            Loader("bucket-back",new Vector3(0,1,4),new Vector3(4,1.4f,.25f),"quarry-machine",Quaternion.Euler(-18,0,0));
            foreach(float x in new[]{-1.9f,1.9f})Loader("bucket-side-"+x,new Vector3(x,.85f,4.8f),new Vector3(.2f,1,2.2f),"quarry-machine");
            foreach(var p in new[]{new Vector3(32,0,-31),new Vector3(-20,4,18),new Vector3(38,8,34)})
            {
                string id="equipment-"+p.x;Box(id+"-compressor",p+Vector3.up*.8f,new Vector3(1.7f,1.6f,1.2f),"quarry-portal");
                for(int i=0;i<2;i++)Box(id+"-barrel-"+i,p+new Vector3(-2-i, .7f,.1f+i*.8f),new Vector3(.8f,1.4f,.8f),"quarry-machine",0,"quarry-cylinder");
            }
        }
    }
}
