using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>well-research-detail-v1. Cosmetic mesh authoring, never physics or simulation random.</summary>
    public sealed class WellLabDetail
    {
        readonly Transform parent;readonly List<Object> owned;readonly ProvingProfile profile;
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        readonly Mesh cube;
        readonly Material trim,panel,screen,green,leaf;
        // Fixed tessellation/texture resolution are bounded resource tiers, not gameplay tuning.
        const int TextureSize=256;
        public WellLabDetail(Transform parent,List<Object> owned,ProvingProfile profile,Material trim,Material panel,Material screen,Material green)
        {
            this.parent=parent;this.owned=owned;this.profile=profile;this.trim=trim;this.panel=panel;this.screen=screen;this.green=green;
            cube=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,Vector3.one)});owned.Add(cube);
            leaf=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Well living foliage",color=new Color(.18f,.36f,.095f)};
            leaf.SetFloat("_Smoothness",.23f);leaf.SetFloat("_Cull",0);owned.Add(leaf);
        }
        public static Texture2D Texture(string kind,ProvingProfile profile)
        {
            var texture=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,true){name="Well "+kind+" texture",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color[TextureSize*TextureSize];
            for(int y=0;y<TextureSize;y++)for(int x=0;x<TextureSize;x++)
            {
                uint h=unchecked((uint)x*73856093u^(uint)y*19349663u);h^=h>>13;h*=1274126177u;
                float n=(h&1023)/1023f-.5f;
                float value=1+n*profile.Get("well.surfaceGrain");
                if(kind=="ceramic"||kind=="floor")
                {
                    int edge=Mathf.Min(x,y,TextureSize-1-x,TextureSize-1-y);
                    if(edge<1)value-=profile.Get("well.jointContrast");
                    else if(edge<2)value+=.025f;
                    if(kind=="floor")value-=.025f*Mathf.Abs(Mathf.Sin(x*.4f)*Mathf.Sin(y*.37f));
                }
                if(kind=="metal")value-=.06f*(.5f+.5f*Mathf.Sin(y*2.3f));
                if(kind=="soil")value=.78f+n*.35f;
                pixels[y*TextureSize+x]=new Color(value,value,value,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,true);return texture;
        }
        public void Box(Material m,Vector3 center,Vector3 size,Quaternion rotation)
        {
            if(!batches.TryGetValue(m,out var batch))batches[m]=batch=new List<CombineInstance>();
            batch.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(center,rotation,size)});
        }
        void FaceBox(Material m,Vector3 face,Quaternion rotation,Vector3 local,Vector3 size)=>Box(m,face+rotation*local,size,rotation);
        public void Wall(ArenaSolid solid)
        {
            // Only large intact wall segments: no headers, sills, shafts or moving-platform clearance.
            if(!solid.Id.StartsWith("f")||solid.Material!="wall"||solid.Size.y<WellArenaCatalog.ClearHeight-.01f)return;
            bool alongX=solid.Size.x>solid.Size.z;float length=alongX?solid.Size.x:solid.Size.z;
            if(length<3.5f)return;
            var normal=alongX?Vector3.forward:Vector3.right;float depth=alongX?solid.Size.z:solid.Size.x;
            foreach(int side in new[]{-1,1})
            {
                // Exterior walls receive their interior face only. Decoration projects at most 6 cm.
                if(solid.Id.Contains("outer-")&&Vector3.Dot(normal*side,solid.Position)>0)continue;
                var rotation=Quaternion.LookRotation(normal*side);
                var face=solid.Position+normal*side*(depth/2+.002f);
                float bottom=solid.Position.y-solid.Size.y/2;
                // Flush lower service panel and a high ventilation grille remain inside this solid's silhouette.
                face.y=bottom+2.2f;
                FaceBox(trim,face,rotation,Vector3.zero,new Vector3(1.15f,1.45f,.035f));
                FaceBox(panel,face,rotation,new Vector3(0,0,.023f),new Vector3(1.05f,1.35f,.018f));
                FaceBox(screen,face,rotation,new Vector3(0,.16f,.038f),new Vector3(.67f,.43f,.012f));
                int department=Mathf.Clamp(Mathf.FloorToInt(bottom/WellArenaCatalog.Storey),0,2);
                // Screen traces: bio trend, physics columns, electronics circuit rows.
                for(int sample=0;sample<9;sample++)
                {
                    float x=-.27f+sample*.065f;
                    float y=department==0?.16f+Mathf.Sin(sample*1.3f)*.10f:department==1?.10f:.12f+(sample%3)*.07f;
                    FaceBox(green,face,rotation,new Vector3(x,y,.046f),new Vector3(.035f,department==1?.06f+(sample%4)*.035f:.018f,.006f));
                }
                for(int row=0;row<3;row++)FaceBox(green,face,rotation,new Vector3(-.22f+row*.20f,-.35f,.04f),new Vector3(.075f,.035f,.012f));
                face.y=bottom+4.65f;
                FaceBox(trim,face,rotation,Vector3.zero,new Vector3(1.5f,.64f,.04f));
                for(int row=0;row<6;row++)FaceBox(panel,face,rotation,new Vector3(0,-.25f+row*.1f,.03f),new Vector3(1.38f,.035f,.025f));
            }
        }
        public void DoorHeaders(ArenaSolid solid)
        {
            if(!solid.Id.EndsWith("-header")||!solid.Id.StartsWith("f"))return;
            bool alongX=solid.Size.x>solid.Size.z;float width=alongX?solid.Size.x:solid.Size.z;
            var normal=alongX?Vector3.forward:Vector3.right;float depth=alongX?solid.Size.z:solid.Size.x;
            foreach(int side in new[]{-1,1})
            {
                if(solid.Id.Contains("outer-")&&Vector3.Dot(normal*side,solid.Position)>0)continue;
                var rotation=Quaternion.LookRotation(normal*side);var face=solid.Position+normal*side*(depth/2+.003f);face.y=solid.Position.y-solid.Size.y/2+.1f;
                FaceBox(trim,face,rotation,Vector3.zero,new Vector3(width,.16f,.035f));
                foreach(int edge in new[]{-1,1})FaceBox(trim,face,rotation,new Vector3(edge*(width/2+.06f),-1.65f,0),new Vector3(.12f,3.2f,.035f));
            }
        }
        public void Plants(Vector3 center)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();var colors=new List<Color>();
            for(int plant=0;plant<5;plant++)
            {
                var basePoint=center+new Vector3((plant%3-1)*.8f,.35f,(plant/3-.5f)*1.2f);
                for(int blade=0;blade<7;blade++)
                {
                    float angle=(blade*2.39996f+plant)*Mathf.Rad2Deg;var rot=Quaternion.Euler(0,angle,0);int start=vertices.Count;
                    for(int step=0;step<=6;step++)
                    {
                        float t=step/6f;float width=Mathf.Sin(t*Mathf.PI)*.13f;
                        float height=profile.Get("well.plantHeight")*(Mathf.Sin(t*Mathf.PI*.72f))*(.75f+(blade%3)*.12f);
                        foreach(int edge in new[]{-1,0,1})
                        {
                            vertices.Add(basePoint+rot*new Vector3(edge*width,height+(edge==0?.025f*Mathf.Sin(t*Mathf.PI):0),t*.49f));
                            uv.Add(new Vector2(edge*.5f+.5f,t));colors.Add(Color.Lerp(new Color(.55f,.67f,.4f),Color.white,t));
                        }
                    }
                    for(int step=0;step<6;step++)for(int side=0;side<2;side++)
                    {int i=start+step*3+side;indices.AddRange(new[]{i,i+3,i+1,i+1,i+3,i+4});}
                }
            }
            var mesh=new Mesh{name="Well curved leaves"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
            Render(mesh,leaf,"Well compact plants");
        }
        void Render(Mesh mesh,Material material,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        public void Finish()
        {
            foreach(var pair in batches)
            {
                var mesh=new Mesh{name="Well batched fittings",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);owned.Add(mesh);Render(mesh,pair.Key,"Well details / "+pair.Key.name);
            }
        }
    }
}
