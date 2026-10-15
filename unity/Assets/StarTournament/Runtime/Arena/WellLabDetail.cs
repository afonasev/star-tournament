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
        readonly Material trim,panel,screen,green,leaf,porcelain,alloy,blue,amber;
        // Fixed tessellation/texture resolution are bounded resource tiers, not gameplay tuning.
        const int TextureSize=256;
        public WellLabDetail(Transform parent,List<Object> owned,ProvingProfile profile,Material trim,Material panel,Material screen,Material green)
        {
            this.parent=parent;this.owned=owned;this.profile=profile;this.trim=trim;this.panel=panel;this.screen=screen;this.green=green;
            cube=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,Vector3.one)});owned.Add(cube);
            porcelain=DetailMaterial("Well enamel fittings",new Color(.83f,.84f,.78f),.05f,.34f);
            alloy=DetailMaterial("Well satin aluminium",new Color(.57f,.60f,.60f),.7f,.42f);
            blue=DetailMaterial("Well physics components",new Color(.14f,.38f,.58f),.35f,.3f);
            amber=DetailMaterial("Well electronics substrate",new Color(.36f,.25f,.09f),.25f,.28f);
            leaf=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Well living foliage",color=Color.white};
            var foliage=Texture("leaf",profile);owned.Add(foliage);leaf.SetTexture("_BaseMap",foliage);
            leaf.SetFloat("_Smoothness",.32f);leaf.SetFloat("_Cull",0);owned.Add(leaf);
        }
        Material DetailMaterial(string name,Color colour,float metallic,float smoothness)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=colour};m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",smoothness);owned.Add(m);return m;
        }
        static float Grain(int x,int y)
        {uint h=unchecked((uint)x*73856093u^(uint)y*19349663u);h^=h>>13;h*=1274126177u;return (h&1023)/1023f-.5f;}
        static float Noise(int x,int y,int cell)
        {
            int count=TextureSize/cell,gx=x/cell,gy=y/cell;float tx=(x%cell)/(float)cell,ty=(y%cell)/(float)cell;
            tx=tx*tx*(3-2*tx);ty=ty*ty*(3-2*ty);
            return Mathf.Lerp(Mathf.Lerp(Grain(gx,gy),Grain((gx+1)%count,gy),tx),Mathf.Lerp(Grain(gx,(gy+1)%count),Grain((gx+1)%count,(gy+1)%count),tx),ty);
        }
        static float Height(string kind,int x,int y)
        {
            x=(x%TextureSize+TextureSize)%TextureSize;y=(y%TextureSize+TextureSize)%TextureSize;
            float u=x/(float)TextureSize,v=y/(float)TextureSize;
            float cloud=Mathf.Sin(u*Mathf.PI*4+Mathf.Sin(v*Mathf.PI*6))*Mathf.Cos(v*Mathf.PI*4);
            if(kind=="soil")return Noise(x,y,32)*.4f+Noise(x,y,8)*.4f+Noise(x,y,2)*.25f+Grain(x,y)*.18f;
            if(kind=="metal")return Mathf.Sin(y*2.3f)*.05f+Grain(x,y)*.04f;
            int edge=Mathf.Min(x,y,TextureSize-1-x,TextureSize-1-y);
            return (edge<1?-.18f:edge<3?-.05f:0)+cloud*.015f+Grain(x,y)*(kind=="floor"?.04f:.008f);
        }
        public static Texture2D Texture(string kind,ProvingProfile profile,bool normal=false)
        {
            var texture=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,true,normal){name="Well "+kind+(normal?" relief":" texture"),wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};
            var pixels=new Color[TextureSize*TextureSize];
            for(int y=0;y<TextureSize;y++)for(int x=0;x<TextureSize;x++)
            {
                float n=Grain(x,y),grain=profile.Get("well.surfaceGrain"),u=x/(float)(TextureSize-1),v=y/(float)(TextureSize-1);
                if(normal)
                {
                    float dx=Height(kind,x-1,y)-Height(kind,x+1,y),dy=Height(kind,x,y-1)-Height(kind,x,y+1);
                    var direction=new Vector3(dx*grain*10,dy*grain*10,1).normalized;
                    // Uncompressed desktop normals use RGB with alpha 1 (URP RG-or-AG multiplies R by A).
                    pixels[y*TextureSize+x]=new Color(direction.x*.5f+.5f,direction.y*.5f+.5f,direction.z*.5f+.5f,1);continue;
                }
                if(kind=="leaf")
                {
                    float mid=Mathf.Abs(u-.5f),vein=Mathf.Exp(-mid*130);
                    float branch=Mathf.Pow(Mathf.Max(0,Mathf.Cos((v-mid*.6f)*Mathf.PI*28)),18)*Mathf.Clamp01(mid*20);
                    var colour=Color.Lerp(new Color(.065f,.19f,.035f),new Color(.29f,.46f,.10f),Mathf.Clamp01(v*.6f+(1-mid*2)*.35f));
                    pixels[y*TextureSize+x]=colour*(1+vein*.28f+branch*.14f+n*.06f);continue;
                }
                float value=1+n*grain*.4f;
                if(kind=="ceramic"||kind=="floor")
                {
                    int edge=Mathf.Min(x,y,TextureSize-1-x,TextureSize-1-y);
                    if(edge<1)value-=profile.Get("well.jointContrast");else if(edge<2)value+=.025f;
                    value+=Height(kind,x,y)*grain*(kind=="floor"?3:1);
                }
                if(kind=="metal")value-=.09f*(.5f+.5f*Mathf.Sin(y*2.3f));
                if(kind=="soil")value=.83f+Height(kind,x,y)*.45f;
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
                int department=Mathf.Clamp(Mathf.FloorToInt(bottom/WellArenaCatalog.Storey),0,2);
                int hash=0;foreach(char c in solid.Id)hash=unchecked(hash*31+c);
                int variant=(hash&0x7fffffff)%5;
                // Authored shallow modules: maximum 6 cm from a solid wall; never over an opening.
                face.y=bottom+2.25f;
                if(variant==0)Service(face,rotation);
                else if(variant==1)Instrument(face,rotation,department);
                else if(variant==2)Samples(face,rotation,department);
                else if(variant==3)Circuit(face,rotation,department);
                // Some walls intentionally stay quiet, with just a high grille and skirting.
                face.y=bottom+4.8f;
                if(variant!=2)Vent(face+rotation*Vector3.right*(length>7?1.1f:0),rotation,variant==0?2.1f:1.35f);
                face.y=bottom+.09f;
                FaceBox(alloy,face,rotation,Vector3.zero,new Vector3(length,.18f,.022f));
            }
        }
        void Frame(Vector3 face,Quaternion rotation,float width,float height)
        {
            FaceBox(trim,face,rotation,Vector3.zero,new Vector3(width,height,.025f));
            FaceBox(alloy,face,rotation,new Vector3(0,0,.018f),new Vector3(width-.035f,height-.035f,.018f));
            FaceBox(porcelain,face,rotation,new Vector3(0,0,.03f),new Vector3(width-.10f,height-.10f,.012f));
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
                FaceBox(trim,face,rotation,new Vector3(x*(width/2-.055f),y*(height/2-.055f),.04f),new Vector3(.022f,.022f,.008f));
        }
        void Vent(Vector3 face,Quaternion rotation,float width)
        {
            Frame(face,rotation,width,.62f);
            FaceBox(trim,face,rotation,new Vector3(0,0,.039f),new Vector3(width-.13f,.46f,.008f));
            for(int row=0;row<7;row++)FaceBox(alloy,face,rotation,new Vector3(0,-.21f+row*.07f,.05f),new Vector3(width-.17f,.025f,.015f));
        }
        void Service(Vector3 face,Quaternion rotation)
        {
            Frame(face,rotation,.9f,1.55f);
            FaceBox(trim,face,rotation,new Vector3(.29f,0,.046f),new Vector3(.045f,.23f,.012f));
            for(int i=0;i<4;i++)FaceBox(alloy,face,rotation,new Vector3(0,.42f+i*.06f,.044f),new Vector3(.5f,.018f,.008f));
            FaceBox(green,face,rotation,new Vector3(-.22f,-.48f,.046f),new Vector3(.055f,.028f,.01f));
        }
        void Instrument(Vector3 face,Quaternion rotation,int department)
        {
            Frame(face,rotation,1.45f,1.25f);
            FaceBox(screen,face,rotation,new Vector3(-.12f,.12f,.042f),new Vector3(.98f,.66f,.008f));
            for(int sample=0;sample<24;sample++)
            {
                float x=-.55f+sample*.037f,y=.12f+Mathf.Sin(sample*(department+1)*.4f)*.18f;
                FaceBox(department==1?blue:green,face,rotation,new Vector3(x,y,.048f),new Vector3(.025f,.022f,.004f));
            }
            for(int row=0;row<4;row++)FaceBox(alloy,face,rotation,new Vector3(-.3f+row*.18f,-.4f,.045f),new Vector3(.09f,.055f,.012f));
            FaceBox(green,face,rotation,new Vector3(.56f,.29f,.046f),new Vector3(.045f,.045f,.008f));
        }
        void Samples(Vector3 face,Quaternion rotation,int department)
        {
            Frame(face,rotation,1.85f,1.65f);
            FaceBox(screen,face,rotation,new Vector3(0,.08f,.04f),new Vector3(1.61f,1.12f,.008f));
            for(int i=0;i<5;i++)
            {
                float x=-.61f+i*.305f;
                FaceBox(alloy,face,rotation,new Vector3(x,.05f,.046f),new Vector3(.20f,.77f,.016f));
                FaceBox(department==0?green:department==1?blue:amber,face,rotation,new Vector3(x,.07f,.052f),new Vector3(.10f,.43f+(i%2)*.14f,.008f));
                FaceBox(porcelain,face,rotation,new Vector3(x,-.39f,.054f),new Vector3(.19f,.035f,.01f));
            }
            FaceBox(alloy,face,rotation,new Vector3(0,-.64f,.045f),new Vector3(1.4f,.06f,.012f));
        }
        void Circuit(Vector3 face,Quaternion rotation,int department)
        {
            Frame(face,rotation,1.6f,1.4f);
            FaceBox(department==2?amber:screen,face,rotation,new Vector3(0,.03f,.042f),new Vector3(1.34f,1.11f,.008f));
            for(int i=0;i<6;i++)
            {
                float x=-.5f+(i%3)*.49f,y=-.23f+(i/3)*.53f;
                FaceBox(alloy,face,rotation,new Vector3(x,y,.049f),new Vector3(.27f,.32f,.012f));
                FaceBox(department==0?green:blue,face,rotation,new Vector3(x,y,.054f),new Vector3(.13f,.15f,.004f));
                FaceBox(green,face,rotation,new Vector3(x+.18f,y+.1f,.052f),new Vector3(.04f,.04f,.008f));
                FaceBox(alloy,face,rotation,new Vector3(x,y-.21f,.05f),new Vector3(.33f,.015f,.006f));
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
                FaceBox(alloy,face,rotation,new Vector3(0,.015f,.023f),new Vector3(width-.05f,.09f,.018f));
                foreach(int edge in new[]{-1,1})
                {
                    FaceBox(trim,face,rotation,new Vector3(edge*(width/2+.06f),-1.65f,0),new Vector3(.12f,3.2f,.035f));
                    FaceBox(alloy,face,rotation,new Vector3(edge*(width/2+.06f),-1.65f,.023f),new Vector3(.07f,3.2f,.018f));
                }
            }
        }
        public void Plants(Vector3 center)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();var colors=new List<Color>();
            // Fixed botanical mesh tier. Every leaf stays inside the existing bed and plantHeight envelope.
            for(int plant=0;plant<9;plant++)
            {
                var basePoint=center+new Vector3((plant%3-1)*.74f+Mathf.Sin(plant*7)*.08f,.34f,(plant/3-1)*.74f);
                for(int blade=0;blade<11;blade++)
                {
                    float phase=blade*2.39996f+plant*.87f;var rot=Quaternion.Euler(0,phase*Mathf.Rad2Deg,0);int start=vertices.Count;
                    float reach=.34f+(blade%4)*.075f,height=profile.Get("well.plantHeight")*(.67f+(blade%3)*.15f);
                    for(int step=0;step<=12;step++)
                    {
                        float t=step/12f,width=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI)),.85f)*(.065f+(blade%3)*.018f);
                        float rise=Mathf.Sin(t*Mathf.PI*.82f)*height;
                        for(int edge=-2;edge<=2;edge++)
                        {
                            float across=edge*.5f;
                            vertices.Add(basePoint+rot*new Vector3(across*width,rise+(.018f-.05f*across*across)*Mathf.Sin(t*Mathf.PI),t*reach+Mathf.Sin(t*Mathf.PI)*.05f));
                            uv.Add(new Vector2(across*.5f+.5f,t));colors.Add(Color.white);
                        }
                    }
                    for(int step=0;step<12;step++)for(int side=0;side<4;side++)
                    {int i=start+step*5+side;indices.AddRange(new[]{i,i+5,i+1,i+1,i+5,i+6});}
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
