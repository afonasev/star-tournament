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
        readonly Dictionary<Vector3,Mesh> doorBevels=new Dictionary<Vector3,Mesh>();
        readonly Material trim,panel,screen,green,porcelain,alloy,blue,amber,sage;readonly Material[] displays;
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
            sage=DetailMaterial("Well biology components",new Color(.44f,.55f,.43f),.05f,.28f);
            displays=new Material[3];
            for(int department=0;department<3;department++)
            {
                displays[department]=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name="Well "+new[]{"biology","physics","electronics"}[department]+" diagnostic display",color=Color.white};owned.Add(displays[department]);
                var texture=DisplayTexture(department);owned.Add(texture);displays[department].SetTexture("_BaseMap",texture);
            }

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
            if(kind=="floor-bio")return Noise(x,y,32)*.04f+Noise(x,y,2)*.25f+(Grain(x/3,y/3)>.28f?.45f:0);
            if(kind=="floor-physics")return Noise(x,y,32)*.19f+Noise(x,y,8)*.10f+Grain(x,y)*.03f;
            if(kind=="floor-electronics")
            {
                int px=x%32,py=y%32;float r=new Vector2(px-16,py-16).magnitude;
                return r<7?.11f:r<9?-.05f:Grain(x,y)*.025f;
            }
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
                    float relief=kind=="floor-bio"?3:kind.StartsWith("floor")?18:10;
                    var direction=new Vector3(dx*grain*relief,dy*grain*relief,1).normalized;
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
                if(kind.StartsWith("floor"))
                {
                    int cell=TextureSize/2,px=x%cell,py=y%cell;
                    int edge=Mathf.Min(px,py,cell-1-px,cell-1-py);
                    if(edge<1)value-=profile.Get("well.jointContrast");else if(edge<2)value+=.035f;
                    value+=Height(kind,x,y)*grain*(kind=="floor-bio"?8:kind=="floor-electronics"?3:6);
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
        void Round(Material m,Vector3 face,Quaternion rotation,Vector3 local,Vector3 size,bool flushDoor=false)
        {
            if(!bevels.TryGetValue(size,out var mesh)){mesh=WellDetailMesh.Bevel(size);bevels[size]=mesh;owned.Add(mesh);}
            if(flushDoor)
            {
                // Door face stands 3 mm off the wall. Clip at that wall plane, keeping the visible envelope exact.
                if(!doorBevels.TryGetValue(size,out var clipped)){clipped=WellDetailMesh.ClipBack(mesh,-.003f);doorBevels[size]=clipped;owned.Add(clipped);}mesh=clipped;
            }
            if(!batches.TryGetValue(m,out var batch))batches[m]=batch=new List<CombineInstance>();
            batch.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(face+rotation*local,rotation,Vector3.one)});
        }
        void Turned(Material m,Vector3 face,Quaternion rotation,Vector3 local,float diameter,float length,bool forward=false)
        {
            if(!batches.TryGetValue(m,out var batch))batches[m]=batch=new List<CombineInstance>();
            batch.Add(new CombineInstance{mesh=cylinder,transform=Matrix4x4.TRS(face+rotation*local,rotation*(forward?Quaternion.Euler(90,0,0):Quaternion.identity),new Vector3(diameter,length,diameter))});
        }
        static Texture2D DisplayTexture(int department)
        {
            const int width=512,height=256;var pixels=new Color[width*height];
            var bg=new Color(.014f,.037f,.05f);for(int i=0;i<pixels.Length;i++)pixels[i]=bg;
            void Rect(int x,int y,int w,int h,Color c){for(int b=Mathf.Max(y,0);b<Mathf.Min(y+h,height);b++)for(int a=Mathf.Max(x,0);a<Mathf.Min(x+w,width);a++)pixels[b*width+a]=c;}
            var grid=new Color(.045f,.12f,.14f);var cyan=new[]{new Color(.43f,.78f,.37f),new Color(.21f,.73f,.94f),new Color(.95f,.66f,.18f)}[department];var pale=new Color(.53f,.72f,.72f);
            void Line(Vector2 a,Vector2 b,int width,Color colour)
            {
                int steps=Mathf.CeilToInt(Vector2.Distance(a,b));
                for(int i=0;i<=steps;i++){var point=Vector2.Lerp(a,b,i/(float)Mathf.Max(1,steps));Rect(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.y),width,width,colour);}
            }
            Rect(20,223,470,2,pale);Rect(20,17,470,1,grid);
            if(department==0)
            {
                // Large botanical silhouettes and sample bands, legible as biology at corridor distance.
                Line(new Vector2(166,42),new Vector2(166,207),3,cyan);
                for(int branch=0;branch<5;branch++)foreach(int side in new[]{-1,1})
                {
                    float y=66+branch*27;var tip=new Vector2(166+side*(70-branch*8),y+24);
                    Line(new Vector2(166,y),tip,2,cyan);
                    for(int step=0;step<22;step++)
                    {
                        float t=step/21f;var point=Vector2.Lerp(new Vector2(166,y),tip,t);int half=Mathf.RoundToInt(Mathf.Sin(t*Mathf.PI)*(9-branch));
                        Rect(Mathf.RoundToInt(point.x),Mathf.RoundToInt(point.y)-half,2,half*2+1,cyan*.75f);
                    }
                }
                for(int i=0;i<4;i++){Rect(265,58+i*36,58,4,grid);Rect(265,58+i*36,18+i*8,4,cyan);}
            }
            else if(department==1)
            {
                // Measuring axes and two distinct oscilloscope traces.
                for(int x=22;x<350;x+=27)Rect(x,42,1,164,grid);for(int y=42;y<210;y+=24)Rect(22,y,324,1,grid);
                for(int x=23;x<345;x++){float v=(x-23)/322f;int y=128+Mathf.RoundToInt(35*Mathf.Sin(v*19)+19*Mathf.Sin(v*41));Rect(x,y,2,2,cyan);Rect(x,75+Mathf.RoundToInt(13*Mathf.Sin(v*24)),1,2,new Color(.30f,.47f,.72f));}
            }
            else
            {
                // A readable PCB/logic topology, rather than the biology or waveform screen recoloured.
                for(int chip=0;chip<4;chip++)
                {
                    int x=55+chip%2*166,y=66+chip/2*92;Rect(x,y,52,36,pale*.4f);Rect(x+6,y+6,40,24,bg);
                    for(int pin=0;pin<5;pin++)foreach(int side in new[]{-1,1})Rect(x+side*8+(side>0?52:0),y+3+pin*7,8,2,cyan);
                    Line(new Vector2(x+60,y+17),new Vector2(x+84,y+17),2,cyan);Line(new Vector2(x+84,y+17),new Vector2(x+84,y-20),2,cyan);Line(new Vector2(x+84,y-20),new Vector2(x+110,y-20),2,cyan);
                }
            }
            for(int i=0;i<5;i++){Rect(374,185-i*32,108,4,grid);Rect(374,185-i*32,42+i*11,4,cyan);for(int k=0;k<7;k++)Rect(374+k*9,198-i*32,5,3,pale);}
            for(int i=0;i<12;i++)Rect(22+i*8,234,5,3,pale);for(int i=0;i<4;i++){Rect(22+i*82,25,52,4,grid);Rect(22+i*82,25,22+i*5,4,cyan);}
            var texture=new Texture2D(width,height,TextureFormat.RGB24,true){name="Well "+new[]{"biology","physics","electronics"}[department]+" diagnostics atlas",filterMode=FilterMode.Trilinear};texture.SetPixels(pixels);texture.Apply(true,true);return texture;
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
                int department=Mathf.Clamp(Mathf.RoundToInt(bottom/WellArenaCatalog.Storey),0,2);
                int hash=0;foreach(char c in solid.Id)hash=unchecked(hash*31+c);
                int variant=(hash&0x7fffffff)%5;
                // Authored shallow modules: bounded by the art profile on a solid wall; never over an opening.
                face.y=bottom+2.25f;
                if(variant==0)Service(face,rotation);
                else if(variant<4)
                {
                    if(department==0){if(variant==3)Instrument(face,rotation,department);else Samples(face,rotation,department);}
                    else if(department==1){if(variant==3)Samples(face,rotation,department);else Instrument(face,rotation,department);}
                    else {if(variant==3)Instrument(face,rotation,department);else Circuit(face,rotation,department);}
                }
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
            Round(displays[department],face,rotation,new Vector3(-.12f,.12f,.05f),new Vector3(.94f,.62f,.006f));
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
            Round(displays[department],face,rotation,new Vector3(0,.08f,.047f),new Vector3(1.56f,1.07f,.006f));
            for(int i=0;i<5;i++)
            {
                float x=-.61f+i*.305f;
                Turned(alloy,face,rotation,new Vector3(x,.05f,.043f),.11f,.77f);
                Turned(department==0?sage:department==1?alloy:amber,face,rotation,new Vector3(x,.07f,.052f),.12f,.43f+(i%2)*.14f);
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
            Round(displays[department],face,rotation,new Vector3(0,.03f,.048f),new Vector3(1.29f,1.06f,.006f));
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
                Round(trim,face,rotation,Vector3.zero,new Vector3(width+.40f,.20f,.095f),true);
                Round(alloy,face,rotation,new Vector3(0,.015f,.055f),new Vector3(width+.34f,.12f,.035f));
                foreach(int edge in new[]{-1,1})
                {
                    // Jamb ends at the underside of the lintel. Previously their front
                    // faces overlapped by 5 cm at exactly the same depth and flickered.
                    Round(trim,face,rotation,new Vector3(edge*(width/2+.10f),-1.675f,0),new Vector3(.20f,3.15f,.095f),true);
                    Round(alloy,face,rotation,new Vector3(edge*(width/2+.10f),-1.65f,.055f),new Vector3(.13f,3.2f,.035f));
                }
            }
        }
        void Render(Mesh mesh,Material material,string name)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=material;
        }
        public void Finish()
        {
            foreach(var pair in batches)
            {
                var mesh=new Mesh{name="Well batched fittings",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(pair.Value.ToArray(),true,true);owned.Add(mesh);
                if(pair.Key==trim||pair.Key==alloy){var joined=WellDetailMesh.JoinFlatFaces(mesh);owned.Add(joined);mesh=joined;}
                Render(mesh,pair.Key,"Well details / "+pair.Key.name);
            }
        }
    }
}
