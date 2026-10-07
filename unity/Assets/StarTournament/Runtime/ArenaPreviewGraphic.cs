using System.Linq;
using UnityEngine;
using UnityEngine.UI;
namespace StarTournament.ProvingGround
{
    // A presentation-only axonometric miniature of the published map geometry.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ArenaPreviewGraphic : MaskableGraphic
    {
        string map; ArenaDefinition arena;
        public void Show(string id){if(map==id)return;map=id;arena=AuthoredArenaCatalog.Resolve(id);raycastTarget=false;SetVerticesDirty();}
        static Vector2 Project(Vector3 v)=>new Vector2(v.x-v.z,(v.x+v.z)*.38f+v.y*.9f);
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();if(arena==null)return;
            var solids=arena.Solids.Where(s=>s.Size.x>0&&s.Size.z>0&&!s.Id.Contains("roof")&&!s.Id.Contains("ceiling")&&!s.Id.Contains("grille")).OrderBy(s=>s.Position.y+s.Size.y*.5f).ThenByDescending(s=>s.Position.x+s.Position.z).ToArray();
            var points=solids.SelectMany(s=>Corners(s)).Select(Project).ToArray();if(points.Length==0)return;
            var min=new Vector2(points.Min(p=>p.x),points.Min(p=>p.y));var max=new Vector2(points.Max(p=>p.x),points.Max(p=>p.y));
            var rect=rectTransform.rect;float scale=Mathf.Min(rect.width/(max.x-min.x+1),rect.height/(max.y-min.y+1))*.92f;
            foreach(var solid in solids)
            {
                var p=Corners(solid).Select(v=>rect.center+(Project(v)-(min+max)*.5f)*scale).ToArray();
                var top=solid.Size.y<1?new Color32(79,105,125,255):new Color32(166,185,196,255);
                Quad(mesh,p[0],p[1],p[5],p[4],new Color32(34,52,71,255));Quad(mesh,p[1],p[2],p[6],p[5],new Color32(57,76,91,255));Quad(mesh,p[4],p[5],p[6],p[7],top);
            }
        }
        static Vector3[] Corners(ArenaSolid s){var h=s.Size*.5f;var p=new Vector3[8];for(int i=0;i<8;i++){float x=i%4==0||i%4==3?-h.x:h.x;float z=i%4<2?-h.z:h.z;p[i]=s.Position+s.Rotation*new Vector3(x,i<4?-h.y:h.y,z);}return p;}
        static void Quad(VertexHelper m,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color){int n=m.currentVertCount;m.AddVert(a,color,Vector2.zero);m.AddVert(b,color,Vector2.zero);m.AddVert(c,color,Vector2.zero);m.AddVert(d,color,Vector2.zero);m.AddTriangle(n,n+1,n+2);m.AddTriangle(n,n+2,n+3);}
    }
}
