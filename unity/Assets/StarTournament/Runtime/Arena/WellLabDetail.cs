using System.Collections.Generic;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>well-research-detail-v1. Cosmetic mesh authoring, never physics or simulation random.</summary>
    public sealed partial class WellLabDetail
    {
        readonly Transform parent;readonly List<Object> owned;readonly ProvingProfile profile;
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        readonly Mesh cube,cylinder;readonly Dictionary<Vector3,Mesh> bevels=new Dictionary<Vector3,Mesh>();
        readonly Material trim,panel,screen,green,leaf,porcelain,alloy,blue,amber;readonly Material display;
        // Fixed tessellation/texture resolution are bounded resource tiers, not gameplay tuning.
        const int TextureSize=256;
        public WellLabDetail(Transform parent,List<Object> owned,ProvingProfile profile,Material trim,Material panel,Material screen,Material green)
        {
            this.parent=parent;this.owned=owned;this.profile=profile;this.trim=trim;this.panel=panel;this.screen=screen;this.green=green;
            cube=WellSurfaceMesh.Build(new[]{new Bounds(Vector3.zero,Vector3.one)});owned.Add(cube);cylinder=WellDetailMesh.Cylinder();owned.Add(cylinder);
            porcelain=DetailMaterial("Well enamel fittings",new Color(.83f,.84f,.78f),.05f,.34f);
            alloy=DetailMaterial("Well satin aluminium",new Color(.77f,.79f,.78f),.25f,.52f);
            blue=DetailMaterial("Well physics components",new Color(.14f,.38f,.58f),.35f,.3f);
            amber=DetailMaterial("Well electronics substrate",new Color(.36f,.25f,.09f),.25f,.28f);
            display=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Well diagnostic display",color=Color.white};owned.Add(display);
            var displayTexture=DisplayTexture();owned.Add(displayTexture);display.SetTexture("_BaseMap",displayTexture);
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
            // Authored material motifs in one repeat: floor aggregate, large wall panels, perforated ceiling cassettes.
            // Their scale and contrast remain controlled by the existing Well art profile.
            if(kind=="floor")return Noise(x,y,32)*.06f+Noise(x,y,8)*.14f+Noise(x,y,2)*.26f+Grain(x,y)*.08f;
            if(kind=="ceiling")
            {
                int px=x%(TextureSize/2),py=y%(TextureSize/4);
                int seam=Mathf.Min(px,TextureSize/2-1-px,py,TextureSize/4-1-py);
                bool hole=seam>8&&x%12<3&&y%12<3;
                return seam<2?-.15f:hole?-.09f:Grain(x,y)*.008f;
            }
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
                    var direction=new Vector3(dx*grain*(kind=="floor"?18:10),dy*grain*(kind=="floor"?18:10),1).normalized;
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
                if(kind=="ceramic")
                {
                    // Full-height enamel sheets: vertical joints dominate, with a narrow horizontal closure.
                    int vertical=Mathf.Min(x,TextureSize-1-x),horizontal=Mathf.Min(y,TextureSize-1-y);
                    if(vertical<2)value-=profile.Get("well.jointContrast");
                    else if(vertical<4)value+=.035f;
                    if(horizontal<1)value-=profile.Get("well.jointContrast")*.35f;
                    value+=Height(kind,x,y)*grain;
                }
                if(kind=="floor")
                {
                    int cell=TextureSize/2,px=x%cell,py=y%cell;
                    int edge=Mathf.Min(px,py,cell-1-px,cell-1-py);
                    if(edge<1)value-=profile.Get("well.jointContrast");else if(edge<2)value+=.035f;
                    value+=Height(kind,x,y)*grain*6;
                    // Subtle aggregate flecks and panel-to-panel value changes, never gameplay randomness.
                    value+=Grain(x/cell,y/cell)*grain*.5f;
                }
                if(kind=="ceiling")
                {
                    int px=x%(TextureSize/2),py=y%(TextureSize/4);
                    int seam=Mathf.Min(px,TextureSize/2-1-px,py,TextureSize/4-1-py);
                    if(seam<2)value-=profile.Get("well.jointContrast")*1.5f;
                    else if(seam<4)value+=.025f;
                    else if(seam>8&&x%12<3&&y%12<3)value-=profile.Get("well.jointContrast")*.8f;
                    value+=Grain(x,y)*grain*.15f;
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
        void Round(Material m,Vector3 face,Quaternion rotation,Vector3 local,Vector3 size)
        {
            if(!bevels.TryGetValue(size,out var mesh)){mesh=WellDetailMesh.Bevel(size);bevels[size]=mesh;owned.Add(mesh);}
            if(!batches.TryGetValue(m,out var batch))batches[m]=batch=new List<CombineInstance>();
            batch.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(face+rotation*local,rotation,Vector3.one)});
        }
        void Turned(Material m,Vector3 face,Quaternion rotation,Vector3 local,float diameter,float length,bool forward=false)
        {
            if(!batches.TryGetValue(m,out var batch))batches[m]=batch=new List<CombineInstance>();
            batch.Add(new CombineInstance{mesh=cylinder,transform=Matrix4x4.TRS(face+rotation*local,rotation*(forward?Quaternion.Euler(90,0,0):Quaternion.identity),new Vector3(diameter,length,diameter))});
        }
        static Texture2D DisplayTexture()
        {
            const int width=512,height=256;var pixels=new Color[width*height];
            var bg=new Color(.014f,.037f,.05f);for(int i=0;i<pixels.Length;i++)pixels[i]=bg;
            void Rect(int x,int y,int w,int h,Color c){for(int b=Mathf.Max(y,0);b<Mathf.Min(y+h,height);b++)for(int a=Mathf.Max(x,0);a<Mathf.Min(x+w,width);a++)pixels[b*width+a]=c;}
            var grid=new Color(.045f,.12f,.14f);var cyan=new Color(.21f,.83f,.76f);var pale=new Color(.53f,.72f,.72f);
            for(int x=22;x<350;x+=27)Rect(x,42,1,164,grid);for(int y=42;y<210;y+=24)Rect(22,y,324,1,grid);
            Rect(20,223,470,2,pale);Rect(20,17,470,1,grid);
            for(int x=23;x<345;x++){float v=(x-23)/322f;int y=128+Mathf.RoundToInt(35*Mathf.Sin(v*19)+19*Mathf.Sin(v*41));Rect(x,y,2,2,cyan);Rect(x,75+Mathf.RoundToInt(13*Mathf.Sin(v*24)),1,2,new Color(.30f,.47f,.72f));}
            for(int i=0;i<5;i++){Rect(374,185-i*32,108,4,grid);Rect(374,185-i*32,42+i*11,4,cyan);for(int k=0;k<7;k++)Rect(374+k*9,198-i*32,5,3,pale);}
            for(int i=0;i<12;i++)Rect(22+i*8,234,5,3,pale);for(int i=0;i<4;i++){Rect(22+i*82,25,52,4,grid);Rect(22+i*82,25,22+i*5,4,cyan);}
            var texture=new Texture2D(width,height,TextureFormat.RGB24,true){name="Well live diagnostics atlas",filterMode=FilterMode.Trilinear};texture.SetPixels(pixels);texture.Apply(true,true);return texture;
        }
        public void Facade(ArenaSolid solid)
        {
            if(!solid.Id.Contains("-cap-"))return;
            bool alongX=solid.Size.x>solid.Size.z;var normal=alongX?Vector3.forward:Vector3.right;
            int side=Vector3.Dot(normal,solid.Position)>0?-1:1;var rotation=Quaternion.LookRotation(normal*side);
            var face=solid.Position+normal*side*((alongX?solid.Size.z:solid.Size.x)/2+.004f);
            // One louvre every second bay, entirely on the opaque fascia above the existing window.
            int hash=0;foreach(char c in solid.Id)hash=unchecked(hash*31+c);
            if((hash&1)==0&&Mathf.Abs(alongX?solid.Position.x:solid.Position.z)>6)Vent(face,rotation,1.08f);
            float bottom=solid.Position.y-solid.Size.y/2;
            face.y=bottom+.04f;Round(alloy,face,rotation,Vector3.zero,new Vector3(3.19f,.075f,.065f));
        }

        public void Wall(ArenaSolid solid)
        {
            // Only large intact wall segments: no headers, sills, shafts or moving-platform clearance.
            if(!solid.Id.StartsWith("f")||solid.Material!="wall"||solid.Size.y<WellArenaCatalog.ClearHeight-.01f)return;
            bool alongX=solid.Size.x>solid.Size.z;float length=alongX?solid.Size.x:solid.Size.z;
            if(length<3.5f)return;
            var normal=alongX?Vector3.forward:Vector3.right;float depth=alongX?solid.Size.z:solid.Size.x;
            foreach(int side in new[]{-1,1})
            {
                // Exterior walls receive their interior face only. Decoration stays on opaque wall faces.
                if(solid.Id.Contains("outer-")&&Vector3.Dot(normal*side,solid.Position)>0)continue;
                var rotation=Quaternion.LookRotation(normal*side);
                var face=solid.Position+normal*side*(depth/2+.002f);
                float bottom=solid.Position.y-solid.Size.y/2;
                int department=Mathf.Clamp(Mathf.FloorToInt(bottom/WellArenaCatalog.Storey),0,2);
                int hash=0;foreach(char c in solid.Id)hash=unchecked(hash*31+c);
                int variant=(hash&0x7fffffff)%5;
                // Authored shallow modules: bounded by the art profile on a solid wall; never over an opening.
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
            float depth=profile.Get("well.fittingDepth");
            Round(trim,face,rotation,new Vector3(0,0,depth*.22f),new Vector3(width,height,depth*.44f));
            Round(alloy,face,rotation,new Vector3(0,0,depth*.45f),new Vector3(width-.035f,height-.035f,depth*.35f));
            Round(porcelain,face,rotation,new Vector3(0,0,depth*.58f),new Vector3(width-.11f,height-.11f,depth*.15f));
            foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})
                Turned(trim,face,rotation,new Vector3(x*(width/2-.055f),y*(height/2-.055f),depth*.65f),.023f,.009f,true);
        }
        void Vent(Vector3 face,Quaternion rotation,float width)
        {
            Frame(face,rotation,width,.62f);
            face+=rotation*Vector3.forward*(profile.Get("well.fittingDepth")*.65f-.035f);
            FaceBox(trim,face,rotation,new Vector3(0,0,.039f),new Vector3(width-.13f,.46f,.008f));
            for(int row=0;row<7;row++)FaceBox(alloy,face,rotation,new Vector3(0,-.21f+row*.07f,.05f),new Vector3(width-.17f,.025f,.015f));
        }
        void Service(Vector3 face,Quaternion rotation)
        {
            Frame(face,rotation,.9f,1.55f);
            face+=rotation*Vector3.forward*(profile.Get("well.fittingDepth")*.65f-.035f);
            FaceBox(trim,face,rotation,new Vector3(.29f,0,.046f),new Vector3(.045f,.23f,.012f));
            for(int i=0;i<4;i++)FaceBox(alloy,face,rotation,new Vector3(0,.42f+i*.06f,.044f),new Vector3(.5f,.018f,.008f));
            FaceBox(green,face,rotation,new Vector3(-.22f,-.48f,.046f),new Vector3(.055f,.028f,.01f));
        }
        void Instrument(Vector3 face,Quaternion rotation,int department)
        {
            Frame(face,rotation,1.45f,1.25f);
            face+=rotation*Vector3.forward*(profile.Get("well.fittingDepth")*.65f-.035f);
            FaceBox(screen,face,rotation,new Vector3(-.12f,.12f,.042f),new Vector3(.98f,.66f,.008f));
            // UVs of the bevel front span the display, unlike world-scaled union UVs.
            Round(display,face,rotation,new Vector3(-.12f,.12f,.05f),new Vector3(.94f,.62f,.006f));
            for(int row=0;row<3;row++)
            {
                Turned(trim,face,rotation,new Vector3(-.39f+row*.30f,-.4f,.05f),.12f,.025f,true);
                Turned(alloy,face,rotation,new Vector3(-.39f+row*.30f,-.4f,.067f),.086f,.026f,true);
                FaceBox(porcelain,face,rotation,new Vector3(-.39f+row*.30f,-.37f,.083f),new Vector3(.009f,.028f,.003f));
            }
            Turned(trim,face,rotation,new Vector3(.54f,.07f,.057f),.18f,.03f,true);
            Turned(alloy,face,rotation,new Vector3(.54f,.07f,.081f),.13f,.035f,true);
            FaceBox(porcelain,face,rotation,new Vector3(.54f,.11f,.101f),new Vector3(.012f,.04f,.004f));
            for(int i=0;i<5;i++)FaceBox(trim,face,rotation,new Vector3(.51f,-.23f-i*.045f,.05f),new Vector3(.22f,.014f,.008f));
            FaceBox(green,face,rotation,new Vector3(.56f,.29f,.046f),new Vector3(.045f,.045f,.008f));
        }
        void Samples(Vector3 face,Quaternion rotation,int department)
        {
            Frame(face,rotation,1.85f,1.65f);
            face+=rotation*Vector3.forward*(profile.Get("well.fittingDepth")*.65f-.035f);
            FaceBox(screen,face,rotation,new Vector3(0,.08f,.04f),new Vector3(1.61f,1.12f,.008f));
            for(int i=0;i<5;i++)
            {
                float x=-.61f+i*.305f;
                Turned(alloy,face,rotation,new Vector3(x,.05f,.043f),.11f,.77f);
                Turned(department==0?blue:department==1?alloy:amber,face,rotation,new Vector3(x,.07f,.052f),.12f,.43f+(i%2)*.14f);
                Turned(porcelain,face,rotation,new Vector3(x,.43f,.043f),.14f,.08f);
                Turned(trim,face,rotation,new Vector3(x,-.33f,.043f),.14f,.06f);
                FaceBox(porcelain,face,rotation,new Vector3(x,-.39f,.054f),new Vector3(.19f,.035f,.01f));
            }
            FaceBox(alloy,face,rotation,new Vector3(0,-.64f,.045f),new Vector3(1.4f,.06f,.012f));
        }
        void Circuit(Vector3 face,Quaternion rotation,int department)
        {
            Frame(face,rotation,1.6f,1.4f);
            face+=rotation*Vector3.forward*(profile.Get("well.fittingDepth")*.65f-.035f);
            FaceBox(department==2?amber:screen,face,rotation,new Vector3(0,.03f,.042f),new Vector3(1.34f,1.11f,.008f));
            for(int i=0;i<6;i++)
            {
                float x=-.5f+(i%3)*.49f,y=-.23f+(i/3)*.53f;
                Round(alloy,face,rotation,new Vector3(x,y,.055f),new Vector3(.24f,.28f,.035f));
                Round(trim,face,rotation,new Vector3(x,y,.078f),new Vector3(.16f,.19f,.023f));
                for(int pin=0;pin<5;pin++)foreach(int side in new[]{-1,1})
                    FaceBox(alloy,face,rotation,new Vector3(x+side*.105f,y-.07f+pin*.035f,.086f),new Vector3(.055f,.013f,.008f));
                FaceBox(green,face,rotation,new Vector3(x+.18f,y+.1f,.075f),new Vector3(.025f,.025f,.016f));
                FaceBox(alloy,face,rotation,new Vector3(x+.19f,y-.06f,.048f),new Vector3(.009f,.19f,.006f));
                FaceBox(alloy,face,rotation,new Vector3(x+.13f,y-.155f,.048f),new Vector3(.13f,.009f,.006f));
            }
            for(int i=0;i<8;i++)Turned(alloy,face,rotation,new Vector3(-.54f+i*.15f,-.60f,.06f),.05f,.035f,true);
        }
        public void Window(ArenaSolid solid)
        {
            bool alongX=solid.Size.x>solid.Size.z;var normal=alongX?Vector3.forward:Vector3.right;float width=alongX?solid.Size.x:solid.Size.z;
            foreach(int side in new[]{-1,1})
            {
                var rotation=Quaternion.LookRotation(normal*side);var face=solid.Position+normal*side*.052f;
                foreach(int edge in new[]{-1,1})
                {
                    Round(trim,face,rotation,new Vector3(edge*(width/2-.025f),0,.008f),new Vector3(.10f,solid.Size.y+.08f,.06f));
                    Round(alloy,face,rotation,new Vector3(edge*(width/2-.025f),0,.038f),new Vector3(.055f,solid.Size.y+.04f,.025f));
                    Round(trim,face,rotation,new Vector3(0,edge*solid.Size.y/2,.008f),new Vector3(width+.04f,.10f,.06f));
                    Round(alloy,face,rotation,new Vector3(0,edge*solid.Size.y/2,.038f),new Vector3(width+.02f,.055f,.025f));
                }
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
                Round(trim,face,rotation,Vector3.zero,new Vector3(width+.40f,.20f,.095f));
                Round(alloy,face,rotation,new Vector3(0,.015f,.055f),new Vector3(width+.34f,.12f,.035f));
                foreach(int edge in new[]{-1,1})
                {
                    Round(trim,face,rotation,new Vector3(edge*(width/2+.10f),-1.65f,0),new Vector3(.20f,3.2f,.095f));
                    Round(alloy,face,rotation,new Vector3(edge*(width/2+.10f),-1.65f,.055f),new Vector3(.13f,3.2f,.035f));
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
