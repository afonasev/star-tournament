using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Immutable room furniture and sparse corridor service/storage zones, exposed by the map's Lab profile.</summary>
    public static class WellFurniturePlan
    {
        public static bool IsFurniture(ArenaSolid solid)=>solid.Surface!=null&&solid.Surface.StartsWith("well-furniture-");
        public const string WorkZonesProfile="well-work-zones-v1";
        public const string DensityProfile="well-room-gallery-density-v1";
        public static ArenaSolid[] Build()
        {
            var result=new List<ArenaSolid>(BuildRevisionEight());
            // Immutable revision-9 authoring, exposed through existing map Lab descriptors.
            void Add(int floor,string zone,string kind,float x,float z,Vector3 size)
                =>result.Add(new ArenaSolid{Id="furniture-f"+floor+"-density-"+zone+"-"+kind,Position=new Vector3(x,floor*WellArenaCatalog.Storey+size.y/2,z),Size=size,Rotation=Quaternion.identity,Material="equipment",Surface="well-furniture-"+kind,Layer=ProvingArena.WorldLayer,Support=""});
            Add(0,"room","bench",-17,-20.8f,new Vector3(3.2f,1.14f,1.3f));
            Add(1,"room","bench",15.3f,21.2f,new Vector3(1.1f,1.14f,2.7f));
            Add(2,"room","bench",17.3f,-20.2f,new Vector3(1.3f,1.14f,2.8f));
            for(int floor=0;floor<3;floor++)
            {
                int side=floor==1?1:-1;
                Add(floor,"gallery","cabinet",side*11,-side*15.25f,new Vector3(1.1f,1.9f,.9f));
            }
            return result.ToArray();
        }
        /// <summary>Frozen revision-8 work zones; all 56 bodies and their order stay exact.</summary>
        public static ArenaSolid[] BuildRevisionEight()
        {
            var result=BuildRevisionFour();
            // Immutable revision-8 authoring; sizes/positions are exposed by the map Lab profile.
            // Reuse four existing bodies. Shipping cases and every other body remain exact.
            foreach(var solid in result)
            {
                if(solid.Id=="furniture-f0-corner-bench-1-1")
                {
                    solid.Position=new Vector3(17.3f,solid.Position.y,20.7f);
                    solid.Size=new Vector3(1.3f,solid.Size.y,3.6f);
                }
                else if(solid.Id=="furniture-f1-corridor-bench")
                    solid.Position=new Vector3(-18.2f,solid.Position.y,6.4f);
                else if(solid.Id=="furniture-f2-corner-bench--1-1")
                    solid.Position=new Vector3(-17,solid.Position.y,23.85f);
                else if(solid.Id=="furniture-f2-cabinet--1-1")
                    solid.Position=new Vector3(-14.55f,solid.Position.y,22.35f);
            }
            return result;
        }
        /// <summary>Frozen revision-4 furniture, retained for historical map and Lab hashes.</summary>
        public static ArenaSolid[] BuildRevisionFour()
        {
            var result=new List<ArenaSolid>(BuildRevisionThree());
            // well-corridor-zones-v1: fixed authoring, four deliberate additions per storey.
            // Dimensions belong to immutable map revision 4 and are visible in the authoring Lab profile.
            for(int f=0;f<3;f++)
            {
                int side=f==1?-1:1,end=f==1?-1:1;
                void Add(string id,string kind,float x,float z,Vector3 size)
                    =>result.Add(new ArenaSolid{Id="furniture-f"+f+"-corridor-"+id,Position=new Vector3(x,f*WellArenaCatalog.Storey+size.y/2,z),Size=size,Rotation=Quaternion.identity,Material="equipment",Surface="well-furniture-"+kind,Layer=ProvingArena.WorldLayer,Support=""});
                // Paired transport cases form one staging bay beside a blank end-wall section, off the lift approach.
                Add("case-wall","case",side*6.8f,end*23.8f,new Vector3(1.6f,1.05f,1.1f));
                Add("case-aisle","case",side*6.8f,end*21.9f,new Vector3(1.6f,1.05f,1.1f));
                // A single service bench by the lab doors; floor 2's corridor runs outside its shifted rooms.
                Add("bench","bench",side*(f==1?22:12.7f),-end*6.4f,new Vector3(1.1f,1.14f,2));
                Add("cabinet","cabinet",side*11,-end*15.25f,new Vector3(1.1f,1.9f,.9f));
            }
            return result.ToArray();
        }
        /// <summary>Frozen revision-3 furniture. Never add new placements here: historical Lab hashes depend on it.</summary>
        public static ArenaSolid[] BuildRevisionThree()
        {
            var result=new List<ArenaSolid>();
            void Add(string id,string kind,float x,float z,float width,float height,float depth,int floor)
            {result.Add(new ArenaSolid{Id="furniture-f"+floor+"-"+id,Position=new Vector3(x,floor*WellArenaRevisionTwo.Storey+height/2,z),Size=new Vector3(width,height,depth),Rotation=Quaternion.identity,Material="equipment",Surface="well-furniture-"+kind,Layer=ProvingArena.WorldLayer,Support=""});}
            for(int f=0;f<3;f++)
            {
                foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                {
                    string corner=x+"-"+z;
                    Add("corner-bench-"+corner,"bench",x*19.3f,z*23.85f,3.6f,1.14f,1.3f,f);
                    Add("cabinet-"+corner,"cabinet",x*23.9f,z*18.35f,1.3f,2.5f,1.7f,f);
                    if(x==z)Add("supply-"+corner,"case",x*23.9f,z*22.8f,1.3f,.9f,1.2f,f);
                }
                // Side rooms on the middle floor contain pickups; leave those compact rooms open.
                if(f!=1)foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
                    Add("side-bench-"+x+"-"+z,"bench",x*23.9f,z*6.4f,1.3f,1.14f,3,f);
                // Two shipping cases per floor against the solid corner-room frontage.
                // 4.4m open gallery remains between each case and the atrium wall; no doorway is narrowed.
                foreach(int side in new[]{-1,1})Add("gallery-cover-"+side,"case",side*20,side*14.65f,2.6f,1.4f,1.3f,f);
            }
            return result.ToArray();
        }
        public static Vector3 Front(ArenaSolid solid)
        {
            if(solid.Id.Contains("-density-"))
                return solid.Surface.EndsWith("cabinet")||solid.Size.x>=solid.Size.z?Vector3.back*Mathf.Sign(solid.Position.z):Vector3.left*Mathf.Sign(solid.Position.x);
            // Only the new island is turned; frozen predecessors retain their original front.
            if(solid.Id=="furniture-f0-corner-bench-1-1"&&solid.Size.z>solid.Size.x)return Vector3.left;
            if(solid.Id.Contains("-corridor-cabinet"))return Vector3.back*Mathf.Sign(solid.Position.z);
            bool xFace=solid.Id.Contains("cabinet")||solid.Id.Contains("side-bench")||solid.Id.Contains("supply")||solid.Id.Contains("-corridor-bench");
            return xFace?Vector3.left*Mathf.Sign(solid.Position.x):Vector3.back*Mathf.Sign(solid.Position.z);
        }
    }
}
