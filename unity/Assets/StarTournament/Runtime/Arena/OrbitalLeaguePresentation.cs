using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    /// <summary>Renderer-only kit for immutable Combat Bowl. Never creates a physics object.</summary>
    public sealed class OrbitalLeaguePresentation : MonoBehaviour
    {
        // Surface epsilon avoids coplanar z-fighting; technical, not traversable geometry.
        const float Epsilon=.006f;
        // Tessellation and texture resolution are fixed asset encoding, not gameplay controls.
        const int TextureSize=256;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        Mesh cube; Material wall,floor,ceiling,trim,cyan,white,sign; Font signFont; ProvingProfile p;
        Color oldAmbient; AmbientMode oldMode;
        public int LocalLightCount {get;private set;}
        public string ProfileIdentity {get;private set;}
        public void Build(ArenaDefinition definition,ProvingProfile profile)
        {
            if(profile.Validate().Count!=0)throw new ArgumentException("Invalid Orbital League art profile");
            p=profile;ProfileIdentity=p.Id+"@"+p.Version;
            oldAmbient=RenderSettings.ambientLight;oldMode=RenderSettings.ambientMode;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white*p.Get("light.fill");
            // A mesh-only source, not CreatePrimitive: decoration has no transient collider either.
            cube=CubeMesh();owned.Add(cube);
            wall=Surface("Coated titanium",p.Get("surface.wallValue"),false);
            floor=Surface("Matte graphite",p.Get("surface.floorValue"),true);
            ceiling=Surface("Service ceiling",p.Get("surface.wallValue")*p.Get("surface.ceilingValue"),false);
            trim=Flat("Anodized graphite",Palette("trim"));
            cyan=Flat("League cyan",Palette("cyan"));
            white=new Material(Shader.Find("StarTournament/OrbitalSurface")){name="Broad white luminaire"};white.SetColor("_Color",Palette("white")*p.Get("light.emission"));owned.Add(white);
            signFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            signFont.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /",64,FontStyle.Normal);
            sign=new Material(Shader.Find("StarTournament/OrbitalSurface")){name="Depth-tested league lettering"};owned.Add(sign);RefreshFont(signFont);Font.textureRebuilt+=RefreshFont;
            foreach(var s in definition.Solids)
            {
                var target=transform.parent.Find(s.Id);if(!target)continue;
                bool roof=s.Id.Contains("roof")||s.Id.Contains("ceiling");bool ground=s.Material=="floor";
                var filter=target.GetComponent<MeshFilter>();var mesh=Instantiate(filter.sharedMesh);owned.Add(mesh);
                var normals=mesh.normals;var uv=mesh.uv;var vertices=mesh.vertices;
                for(int i=0;i<uv.Length;i++)
                {
                    Vector3 v=Vector3.Scale(vertices[i],s.Size);var n=normals[i];
                    uv[i]=(Mathf.Abs(n.y)>.5f?new Vector2(v.x,v.z):Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y):new Vector2(v.x,v.y))/p.Get("surface.tile");
                }
                mesh.uv=uv;filter.sharedMesh=mesh;target.GetComponent<Renderer>().sharedMaterial=roof?ceiling:ground?floor:wall;
                if(roof)Roof(s);
                else if(ground)Floor(s);
                else Walls(s);
            }
            // Central hall slab is also the basement ceiling: dress its underside only inside the corridor.
            CeilingFixture(new Vector3(0,3.58f,-4),Quaternion.identity);
            CeilingFixture(new Vector3(0,3.58f,1),Quaternion.identity);
            CeilingFixture(new Vector3(0,3.58f,6),Quaternion.identity);
            HallMarkings();Labels();
            // Fixed six-zone rig is a technical budget, not a per-fixture light spawn rule.
            for(int zone=0;zone<6;zone++)
            {
                var go=new GameObject("League zone fill");go.transform.SetParent(transform,false);go.transform.localPosition=Position("layout.fill-"+zone);
                var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=Palette("fill");l.intensity=p.Get("light.intensity");l.range=p.Get("light.range");l.shadows=LightShadows.None;LocalLightCount++;
            }
            foreach(var entry in batches)
            {
                var go=new GameObject(entry.Key.name+" / batched detail");go.transform.SetParent(transform,false);
                var mesh=new Mesh{name=go.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray());owned.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=entry.Key;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }
        Vector3 Position(string key)=>new Vector3(p.Get(key+".x"),p.Get(key+".y"),p.Get(key+".z"));
        Color Palette(string key)=>new Color(p.Get("color."+key+".r"),p.Get("color."+key+".g"),p.Get("color."+key+".b"));
        Material Surface(string name,float value,bool isFloor)
        {
            var m=Flat(name,Color.white);m.SetFloat("_Metallic",p.Get("surface.metallic"));m.SetFloat("_Smoothness",p.Get(isFloor?"surface.floorSmoothness":"surface.smoothness"));
            var t=new Texture2D(TextureSize,TextureSize,TextureFormat.RGBA32,true){name=name+" / panel tile",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Trilinear,anisoLevel=4};owned.Add(t);
            var pixels=new Color[TextureSize*TextureSize];float gap=p.Get("detail.panelGap")/p.Get("surface.tile")*TextureSize;
            for(int y=0;y<TextureSize;y++)for(int x=0;x<TextureSize;x++)
            {
                // Stable spatial noise: no simulation random state, repeatable authored texture.
                uint hash=(uint)(x*73856093)^((uint)y*19349663u);hash^=hash>>13;
                float noise=((hash%1024)/1023f-.5f)*p.Get("surface.wear");
                float edge=Mathf.Min(x,y,TextureSize-1-x,TextureSize-1-y);
                float shade=edge<gap?.32f:edge<gap+1?1.12f:1;
                bool screw=(Mathf.Pow(x-9,2)+Mathf.Pow(y-9,2)<6)||(Mathf.Pow(x-(TextureSize-10),2)+Mathf.Pow(y-9,2)<6);
                float v=screw?value*.4f:Mathf.Clamp01(value*shade+noise);
                pixels[y*TextureSize+x]=new Color(v*.96f,v*.99f,v,1);
            }
            t.SetPixels(pixels);t.Apply(true,true);m.SetTexture("_BaseMap",t);return m;
        }
        Material Flat(string name,Color color)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,color=color};m.SetFloat("_Smoothness",p.Get("surface.detailSmoothness"));owned.Add(m);return m;
        }
        void Box(Material material,Vector3 position,Vector3 size,Quaternion rotation)
        {
            if(!batches.TryGetValue(material,out var list))batches[material]=list=new List<CombineInstance>();
            list.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(position,rotation,size)});
        }
        void Walls(ArenaSolid s)
        {
            // Surface-mounted strips stay within the wall silhouette (six millimetre overlay).
            bool xWall=s.Size.x>s.Size.z;float length=xWall?s.Size.x:s.Size.z;
            var along=xWall?Vector3.right:Vector3.forward;var normal=xWall?Vector3.forward:Vector3.right;
            float depth=xWall?s.Size.z:s.Size.x;float bottom=s.Position.y-s.Size.y/2;
            foreach(int side in new[]{-1,1})
            {
                Vector3 face=s.Position+normal*(side*(depth/2+Epsilon));
                void Strip(Material m,float y,float h,float len)
                    =>Box(m,new Vector3(face.x,y,face.z)+normal*(side*Epsilon*(m==trim?0:m==white?2:1)),xWall?new Vector3(len,h,Epsilon):new Vector3(Epsilon,h,len),Quaternion.identity);
                Strip(trim,bottom+p.Get("detail.trimHeight")/2,p.Get("detail.trimHeight"),length);
                Strip(cyan,bottom+p.Get("detail.bandHeight"),p.Get("detail.bandWidth"),length);
                Strip(trim,bottom+s.Size.y-p.Get("detail.headerInset"),p.Get("detail.headerWidth"),length);
                Strip(white,bottom+s.Size.y-p.Get("detail.headerInset")-p.Get("detail.lightStripWidth"),p.Get("detail.lightStripWidth"),length*.85f);
                if(length>3)
                {
                    float width=Mathf.Min(length*.5f,p.Get("detail.ventWidth"));
                    Strip(trim,bottom+p.Get("detail.ventElevation"),p.Get("detail.ventHeight"),width);
                    for(int i=0;i<5;i++)Strip(wall,bottom+p.Get("detail.ventElevation")+((i+.5f)/5-.5f)*p.Get("detail.ventHeight"),p.Get("detail.ventHeight")/20,width*.92f);
                }
            }
            if(s.Id.StartsWith("column"))
            {
                foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.right,Vector3.left})
                {
                    var pos=s.Position+direction*(s.Size.x/2+Epsilon*2);
                    Box(trim,pos,Mathf.Abs(direction.x)>.5f?new Vector3(Epsilon,p.Get("detail.columnStripHeight")+.2f,.18f):new Vector3(.18f,p.Get("detail.columnStripHeight")+.2f,Epsilon),Quaternion.identity);
                    Box(cyan,pos+direction*Epsilon,Mathf.Abs(direction.x)>.5f?new Vector3(Epsilon,p.Get("detail.columnStripHeight"),p.Get("detail.columnStripWidth")):new Vector3(p.Get("detail.columnStripWidth"),p.Get("detail.columnStripHeight"),Epsilon),Quaternion.identity);
                }
            }
        }
        void Floor(ArenaSolid s)
        {
            float inset=p.Get("detail.inset"),w=p.Get("detail.lineWidth");
            Vector3 top=s.Position+s.Rotation*Vector3.up*(s.Size.y/2+Epsilon);
            // Stair nosings emphasize actual existing edges, never fake steps.
            if(s.Id.Contains("-step-"))
            {
                bool xStep=s.Size.x<s.Size.z;
                Box(cyan,top,xStep?new Vector3(w,Epsilon,s.Size.z*.85f):new Vector3(s.Size.x*.85f,Epsilon,w),s.Rotation);return;
            }
            if(s.Size.x<inset*2||s.Size.z<inset*2)return;
            foreach(int side in new[]{-1,1})
            {
                Box(cyan,top+s.Rotation*new Vector3(side*(s.Size.x/2-inset),0,0),new Vector3(w,Epsilon,s.Size.z-inset*2),s.Rotation);
                Box(cyan,top+s.Rotation*new Vector3(0,0,side*(s.Size.z/2-inset)),new Vector3(s.Size.x-inset*2,Epsilon,w),s.Rotation);
            }
        }
        void Roof(ArenaSolid s)
        {
            float spacing=p.Get("light.fixtureSpacing");int nx=Mathf.Max(1,Mathf.FloorToInt(s.Size.x/spacing)),nz=Mathf.Max(1,Mathf.FloorToInt(s.Size.z/spacing));
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                var pos=s.Position+new Vector3((x+.5f)*s.Size.x/nx-s.Size.x/2,-s.Size.y/2-Epsilon,(z+.5f)*s.Size.z/nz-s.Size.z/2);
                CeilingFixture(pos,s.Size.x>s.Size.z?Quaternion.Euler(0,90,0):Quaternion.identity);
            }
        }
        void CeilingFixture(Vector3 pos,Quaternion rotation)
        {
            float w=p.Get("light.fixtureWidth"),len=p.Get("light.fixtureLength");
            Box(trim,pos,new Vector3(w+p.Get("light.fixtureFrame"),p.Get("light.fixtureDepth"),len+p.Get("light.fixtureFrame")),rotation);
            Box(white,pos-Vector3.up*.05f,new Vector3(w,.025f,len),rotation);
            foreach(int side in new[]{-1,1})
            {
                var offset=rotation*new Vector3(side*(w/2+.25f),0,0);
                Box(trim,pos+offset,new Vector3(.18f,.03f,len),rotation);
                for(int i=0;i<12;i++)Box(wall,pos+offset+rotation*new Vector3(0,-.02f,(i+.5f)*len/12-len/2),new Vector3(.15f,.02f,.025f),rotation);
            }
        }
        void HallMarkings()
        {
            float w=p.Get("detail.lineWidth");
            // Sports centre circle, sixty-four vertices is fixed mesh tessellation.
            for(int i=0;i<64;i++)
            {
                float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;var start=new Vector3(Mathf.Sin(a)*p.Get("detail.ringRadius"),4+Epsilon*2,Mathf.Cos(a)*p.Get("detail.ringRadius"));var end=new Vector3(Mathf.Sin(b)*p.Get("detail.ringRadius"),4+Epsilon*2,Mathf.Cos(b)*p.Get("detail.ringRadius"));
                Box(white,(start+end)/2,new Vector3(w,Epsilon,Vector3.Distance(start,end)),Quaternion.LookRotation(end-start));
            }
        }
        void Label(string text,Vector3 position,Quaternion rotation,float scale=1)
        {
            var go=new GameObject("Sector / "+text);go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation*Quaternion.Euler(0,180,0);
            var label=go.AddComponent<TextMesh>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=64;label.characterSize=p.Get("detail.labelSize")*scale;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=Palette("ink");
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=sign;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        void Labels()
        {
            Label("ORBITAL LEAGUE\nCOMBAT BOWL",Position("layout.label-0"),Quaternion.Euler(0,180,0),.85f);
            Label("01  /  UPPER ARENA",Position("layout.label-1"),Quaternion.identity,.7f);
            Label("B1\nARMOR\nCORRIDOR",Position("layout.label-2"),Quaternion.Euler(0,90,0),.7f);
            Label("B1\nLOWER\nTRANSIT",Position("layout.label-3"),Quaternion.Euler(0,-90,0),.7f);
            int sector=0;
            foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})
            {
                sector++;Label("0"+sector+"\nREADY\nROOM",Position("layout.label-"+(sector+3)),Quaternion.Euler(0,x<0?90:-90,0),.8f);
            }
            Label("NORTH  /  02",Position("layout.label-8"),Quaternion.Euler(0,180,0));
            Label("SOUTH  /  04",Position("layout.label-9"),Quaternion.identity);
            Label("WEST  /  03",Position("layout.label-10"),Quaternion.Euler(0,90,0));
            Label("EAST  /  01",Position("layout.label-11"),Quaternion.Euler(0,-90,0));
        }
        static Mesh CubeMesh()
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            foreach(var n in new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
            {
                var u=Vector3.Cross(n,Mathf.Abs(n.y)>.5f?Vector3.forward:Vector3.up);var v=Vector3.Cross(n,u);int index=vertices.Count;
                foreach(var q in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)}){vertices.Add((n+u*q.x+v*q.y)*.5f);normals.Add(n);uv.Add((q+Vector2.one)*.5f);}
                triangles.AddRange(new[]{index,index+1,index+2,index,index+2,index+3});
            }
            var m=new Mesh{name="League detail cube"};m.SetVertices(vertices);m.SetColors(System.Linq.Enumerable.Repeat(Color.white,vertices.Count).ToArray());m.SetNormals(normals);m.SetUVs(0,uv);m.SetTriangles(triangles,0);return m;
        }
        void RefreshFont(Font font){if(font==signFont&&sign)sign.SetTexture("_MainTex",font.material.mainTexture);}
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldMode;foreach(var asset in owned)if(asset)DestroyImmediate(asset);}
    }
}
