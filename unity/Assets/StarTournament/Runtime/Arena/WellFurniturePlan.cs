using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>well-workstations-v1: immutable authored placements, exposed by the map's Lab profile.
    /// These dimensions define map revision 3, not independently adjustable balance or random decoration.</summary>
    public static class WellFurniturePlan
    {
        public static bool IsFurniture(ArenaSolid solid)=>solid.Surface!=null&&solid.Surface.StartsWith("well-furniture-");
        public static ArenaSolid[] Build()
        {
            var result=new List<ArenaSolid>();
            void Add(string id,string kind,float x,float z,float width,float height,float depth,int floor)
            {result.Add(new ArenaSolid{Id="furniture-f"+floor+"-"+id,Position=new Vector3(x,floor*WellArenaCatalog.Storey+height/2,z),Size=new Vector3(width,height,depth),Rotation=Quaternion.identity,Material="equipment",Surface="well-furniture-"+kind,Layer=ProvingArena.WorldLayer,Support=""});}
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
            bool xFace=solid.Id.Contains("cabinet")||solid.Id.Contains("side-bench")||solid.Id.Contains("supply");
            return xFace?Vector3.left*Mathf.Sign(solid.Position.x):Vector3.back*Mathf.Sign(solid.Position.z);
        }
    }
}
