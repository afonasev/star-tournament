using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    public sealed partial class QuarryWarmDetails
    {
        readonly List<Vector3> chipPositions=new List<Vector3>();
        public IReadOnlyList<Vector3> ChipPositions=>chipPositions;
        public int ChipRendererCount { get; private set; }
        void BuildGroundChips(ArenaDefinition d,ProvingProfile p)
        {
            int count=Mathf.RoundToInt(p.Get("quarry.warm.chipsPerCluster"));if(count==0)return;
            float size=p.Get("quarry.warm.chipSize"),reach=p.Get("quarry.warm.chipReach"),clear=p.Get("quarry.warm.detailClearance");
            var floors=d.Solids.Where(s=>s.Material=="floor"&&s.Support!=null&&s.Support.StartsWith("support:")).ToArray();
            var protectedSolids=d.Solids.Where(s=>(s.Support!=null&&s.Support.StartsWith("transition:"))||s.Material=="quarry-portal").ToArray();
            // Fixed decoration seed does not consume or change the simulation random stream.
            var random=new System.Random(60201);float Rand()=> (float)random.NextDouble();
            bool InsideXZ(ArenaSolid s,Vector3 position,float margin)
            {
                var local=Quaternion.Inverse(s.Rotation)*(position-s.Position);
                return Mathf.Abs(local.x)<s.Size.x*.5f+margin&&Mathf.Abs(local.z)<s.Size.z*.5f+margin;
            }
            bool Near(Vector3 a,Vector3 b)=>Mathf.Abs(a.y-b.y)<2&&new Vector2(a.x-b.x,a.z-b.z).sqrMagnitude<clear*clear;
            var mesh=ChipMesh();owned.Add(mesh);
            void Place(Vector3 at,float diameter)
            {
                // Use an existing flat surface at the requested level; never bridge a ledge or a ramp.
                if(!floors.Any(f=>Mathf.Abs(f.Position.y+f.Size.y*.5f-at.y)<.1f&&InsideXZ(f,at,-diameter)))return;
                if(d.Spawns.Any(s=>Near(s,at))||d.Pickups.Any(s=>Near(s.Anchor,at)))return;
                if(protectedSolids.Any(s=>InsideXZ(s,at,clear)))return;
                foreach(var s in d.Solids)
                {
                    if(s.Material=="floor")continue;
                    if(s.Position.y-s.Size.y*.5f>at.y+.25f||s.Position.y+s.Size.y*.5f<at.y+.01f)continue;
                    if(InsideXZ(s,at,diameter*.35f))return;
                }
                // Four spatial groups bound renderer count; tiny chips share the existing stone material.
                int family=100+(at.y>2?2:0)+(at.x>0?1:0);
                at.y-=diameter*.035f;
                Part(family,2,mesh,at,new Vector3(diameter,diameter*(.65f+Rand()*.35f),diameter*(.7f+Rand()*.45f)),Quaternion.Euler(0,Rand()*360,0));
                chipPositions.Add(at);
            }
            foreach(var s in d.Solids.Where(s=>s.Material=="quarry-rock"&&!string.IsNullOrEmpty(s.Surface)))
            {
                float level=s.Position.y-s.Size.y*.5f;
                if(Mathf.Abs(level)> .1f&&Mathf.Abs(level-4)>.1f)continue;
                level=Mathf.Round(level);
                for(int i=0;i<count;i++)
                {
                    float angle=Rand()*Mathf.PI*2,spread=.15f+Rand()*reach;
                    var local=new Vector3(Mathf.Cos(angle)*(s.Size.x*.55f+spread),0,Mathf.Sin(angle)*(s.Size.z*.55f+spread));
                    var at=s.Position+s.Rotation*local;at.y=level;
                    Place(at,size*(.3f+Rand()*.95f));
                }
            }
            // Sparse singles on the loading apron; most detail remains at the existing rock bases.
            for(int i=0;i<count*3;i++)Place(new Vector3(-30+Rand()*60,0,-36+Rand()*22),size*(.3f+Rand()*.65f));
            ChipRendererCount=groups.Keys.Count(k=>k.Item1>=100);
        }
        static Mesh ChipMesh()
        {
            // Editable twelve-face flattened fragment; a broken cap avoids identical pointed pyramids.
            var ring=new[]{new Vector3(-.5f,0,-.12f),new Vector3(-.08f,0,.46f),new Vector3(.47f,0,.17f),new Vector3(.12f,0,-.5f)};
            var cap=new[]{new Vector3(-.33f,.31f,-.09f),new Vector3(-.07f,.38f,.29f),new Vector3(.27f,.25f,.12f),new Vector3(.06f,.34f,-.31f)};
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
            for(int i=0;i<4;i++){int j=(i+1)%4;Face(ring[i],ring[j],cap[j]);Face(ring[i],cap[j],cap[i]);}
            Face(cap[0],cap[1],cap[2]);Face(cap[0],cap[2],cap[3]);
            Face(ring[0],ring[2],ring[1]);Face(ring[0],ring[3],ring[2]);
            var mesh=new Mesh{name="Quarry loose limestone chip source"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
