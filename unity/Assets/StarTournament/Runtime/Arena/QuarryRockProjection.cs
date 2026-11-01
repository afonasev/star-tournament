using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    // Editable deterministic low-poly source. The exact same mesh renders and collides before NavMesh bake.
    public sealed class QuarryRockProjection : MonoBehaviour,IArenaMeshOwner
    {
        readonly List<Mesh> owned=new List<Mesh>();Mesh rock,cylinder;
        public void Apply(GameObject go,ArenaSolid solid)
        {
            if(solid.Surface!="quarry-faceted"&&solid.Surface!="quarry-cylinder")return;
            bool round=solid.Surface=="quarry-cylinder";
            var mesh=round?(cylinder?cylinder:(cylinder=Create(true))):(rock?rock:(rock=Create(false)));
            go.GetComponent<MeshFilter>().sharedMesh=mesh;DestroyImmediate(go.GetComponent<BoxCollider>());
            go.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        Mesh Create(bool round)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            void Face(Vector3 a,Vector3 b,Vector3 c){int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);}
            int sides=round?16:8;var rings=new Vector3[3,sides];
            // Bevels/tessellation are mesh topology invariants, not gameplay-tunable dimensions.
            var octagon=new[]{new Vector2(-.36f,-.5f),new Vector2(.36f,-.5f),new Vector2(.5f,-.36f),new Vector2(.5f,.36f),new Vector2(.36f,.5f),new Vector2(-.36f,.5f),new Vector2(-.5f,.36f),new Vector2(-.5f,-.36f)};
            for(int row=0;row<3;row++)for(int i=0;i<sides;i++)
            {
                var v=round?new Vector2(Mathf.Cos(i*Mathf.PI*2/sides),Mathf.Sin(i*Mathf.PI*2/sides))*.5f:octagon[i];
                float scale=round?(row==1?1:.94f):(row==1?1:row==0?.92f:.85f);
                rings[row,i]=new Vector3(v.x*scale,row==0?-.5f:row==1?.1f:.5f,v.y*scale);
            }
            for(int i=0;i<sides;i++)
            {
                int next=(i+1)%sides;
                Face(new Vector3(0,-.5f,0),rings[0,i],rings[0,next]);Face(new Vector3(0,.5f,0),rings[2,next],rings[2,i]);
                for(int row=0;row<2;row++){Face(rings[row,i],rings[row+1,i],rings[row+1,next]);Face(rings[row,i],rings[row+1,next],rings[row,next]);}
            }
            var mesh=new Mesh{name=round?"Quarry cylinder 16":"Quarry bevel rock"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);return mesh;
        }
        public IEnumerable<Mesh> ReadOwnedMeshes()=>owned;
        public IEnumerable<Mesh> ReadDiagnosticMeshes()=>System.Array.Empty<Mesh>();
        public void ReleaseUnusedMeshes(HashSet<int> ids)=>owned.RemoveAll(m=>!ids.Contains(m.GetInstanceID()));
        void OnDestroy(){foreach(var mesh in owned)if(mesh)DestroyImmediate(mesh);}
    }
}
