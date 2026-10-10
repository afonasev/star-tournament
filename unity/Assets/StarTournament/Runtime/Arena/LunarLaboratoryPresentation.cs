using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    /// <summary>Renderer-only authored kit; textures are deterministic baked-at-load assets, owned and disposed here.</summary>
    public sealed class LunarLaboratoryPresentation:MonoBehaviour
    {
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        Mesh cube,cylinder,sphere;Material ivory,ceiling,graphite,metal,grate,paving,ochre,sand,glass,white,blue,green,purple,fontMaterial;Font signFont;ProvingProfile p;
        ArenaDefinition definition;
        AmbientMode oldMode;Color oldAmbient;public int LightCount{get;private set;}
        float P(string key)=>p.Get("lunar."+key);
        public void Build(ArenaDefinition d,ProvingProfile profile)
        { var steps=BuildSteps(d,profile);while(steps.MoveNext()){} }
        public IEnumerator BuildSteps(ArenaDefinition d,ProvingProfile profile)
        {
            definition=d;p=profile;oldMode=RenderSettings.ambientMode;oldAmbient=RenderSettings.ambientLight;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.85f,.88f,1)*P("fill");
            cube=Primitive(PrimitiveType.Cube);cylinder=Primitive(PrimitiveType.Cylinder);sphere=Primitive(PrimitiveType.Sphere);
            var panels=Texture("Ivory ceramic panel / seams, bolts and wear",0);var dust=Texture("Lunar regolith / granular cratered basalt",1);
            ivory=Mat("Ivory ceramic panels",new Color(.88f,.86f,.76f),panels);graphite=Mat("Graphite floor",new Color(.36f,.39f,.4f),Texture("Resilient floor / fine aggregate and tile joints",2));metal=Mat("Brushed structural alloy",new Color(.31f,.36f,.39f),panels,.6f);ochre=Mat("Matte ochre",new Color(.61f,.43f,.19f));sand=Mat("Grey lunar regolith",new Color(.58f,.59f,.6f),dust);white=Mat("Neutral light diffuser",new Color(.95f,.95f,.87f),null,0,true);blue=Mat("N analysis blue grey",new Color(.39f,.57f,.63f));green=Mat("W communications sage",new Color(.42f,.51f,.4f));purple=Mat("E power grey lavender",new Color(.52f,.46f,.58f));
            ceiling=Mat("Acoustic ceiling / perforated panels",new Color(.94f,.94f,.9f),Texture("Ceiling / perforations and narrow panel joints",3));
            grate=Mat("Grating alloy without aliasing shadows",new Color(.31f,.36f,.39f),panels,.6f);paving=Mat("Dust-grey paving",new Color(.51f,.52f,.5f),panels);
            glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Transparent projectile-proof armored glass"};owned.Add(glass);glass.color=new Color(.39f,.66f,.72f,P("glassOpacity"));glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);glass.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);glass.SetFloat("_ZWrite",0);glass.SetFloat("_Smoothness",.7f);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
            int decorated=0;
            foreach(var s in d.Solids)
            {
                if(decorated++%16==0)yield return null;
                var target=transform.parent.Find(s.Id);var renderer=target.GetComponent<Renderer>();
                renderer.sharedMaterial=s.Id.StartsWith("cargo-")?(s.Position.z>0?blue:ochre):s.Id.Contains("-bar-")?grate:s.Surface=="lunar-glass"?glass:s.Surface=="lunar-sand"?sand:(s.Surface=="lunar-graphite"||s.Surface=="lunar-light")?graphite:s.Material=="metal"||s.Surface=="lunar-step"||s.Surface=="lunar-grate"||s.Id.StartsWith("fence-")||s.Id.StartsWith("rail-")?metal:ivory;
                if(s.Surface=="lunar-grate") {renderer.enabled=false;continue;}
                var mesh=Instantiate(target.GetComponent<MeshFilter>().sharedMesh);owned.Add(mesh);var v=mesh.vertices;var norms=mesh.normals;var uv=mesh.uv;
                for(int i=0;i<v.Length;i++){var a=Vector3.Scale(v[i],s.Size);uv[i]=(Mathf.Abs(norms[i].y)>.5f?new Vector2(a.x,a.z):Mathf.Abs(norms[i].x)>.5f?new Vector2(a.z,a.y):new Vector2(a.x,a.y))/P("tile");}mesh.uv=uv;target.GetComponent<MeshFilter>().sharedMesh=mesh;
                if(s.Surface!="lunar-glass")
                {
                    renderer.enabled=false;
                    if(s.Material=="floor"&&s.Surface!="lunar-step")
                    {
                        var top=new List<int>();var bottom=new List<int>();var triangles=mesh.triangles;
                        for(int t=0;t<triangles.Length;t+=3){var list=norms[triangles[t]].y<-.5f?bottom:top;list.Add(triangles[t]);list.Add(triangles[t+1]);list.Add(triangles[t+2]);}
                        var underside=Instantiate(mesh);owned.Add(underside);underside.triangles=bottom.ToArray();mesh.triangles=top.ToArray();
                        Shape(ceiling,underside,s.Position,s.Size,s.Rotation);
                    }
                    Shape(renderer.sharedMaterial,mesh,s.Position,s.Size,s.Rotation);
                }
                if(s.Material=="equipment")Equipment(s);
                if(s.Surface=="lunar-glass")GlassFrame(s);
                if(s.Surface=="lunar-step")Box(ochre,s.Position+Vector3.up*(s.Size.y*.5f+.006f),new Vector3(s.Size.x,.008f,.07f));
            }
            Architecture();yield return null;RoofDetails();yield return null;Zones();yield return null;PickupSigns(d);yield return null;CourtyardDetail();yield return null;Background();yield return null;Flush();
        }
        Mesh Primitive(PrimitiveType t){var go=GameObject.CreatePrimitive(t);var m=Instantiate(go.GetComponent<MeshFilter>().sharedMesh);DestroyImmediate(go);owned.Add(m);return m;}
        Material Mat(string name,Color c,Texture t=null,float metallic=0,bool emissive=false)
        {
            var m=new Material(Shader.Find(emissive?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,color=c};m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.24f);if(t)m.SetTexture("_BaseMap",t);owned.Add(m);return m;
        }
        Texture2D Texture(string name,int kind)
        {
            // Raster size, palette and noise encoding are fixed asset authoring, not simulation numbers.
            const int n=256;var t=new Texture2D(n,n,TextureFormat.RGB24,true){name=name,wrapMode=TextureWrapMode.Repeat};owned.Add(t);var c=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                uint h=unchecked((uint)(x*73856093)^(uint)(y*19349663));h^=h>>13;float noise=(h%1000)/1000f;
                float a=kind==0?.9f+noise*.08f:.58f+noise*.3f+Mathf.PerlinNoise(x*.045f,y*.045f)*.22f;
                if(kind==0){int edge=Math.Min(Math.Min(x,n-1-x),Math.Min(y,n-1-y));if(edge<2)a=.42f;else if(edge<4)a=.73f;if((x<12||x>243)&&(y<12||y>243)&&((x%244)-7)*((x%244)-7)+((y%244)-7)*((y%244)-7)<5)a=.38f;if(h%1201<3)a*=.65f;}
                if(kind==2){a=.83f+noise*.13f;if(x<2||y<2)a=.51f;}
                if(kind==3){a=.95f+noise*.04f;if(x<2||y<2)a=.72f;else if(x%12<2&&y%12<2)a=.48f;}
                c[y*n+x]=new Color(a,a,a);
            }
            t.SetPixels(c);t.Apply(true,true);return t;
        }
        void Shape(Material m,Mesh mesh,Vector3 c,Vector3 scale,Quaternion rot)
        {if(!batches.TryGetValue(m,out var b)){b=new List<CombineInstance>();batches[m]=b;}b.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(c,rot,scale)});}
        void Box(Material m,Vector3 c,Vector3 size,Quaternion? q=null)=>Shape(m,cube,c,size,q??Quaternion.identity);
        void Pipe(Material m,Vector3 a,Vector3 b,float radius)=>Shape(m,cylinder,(a+b)*.5f,new Vector3(radius,Vector3.Distance(a,b)*.5f,radius),Quaternion.FromToRotation(Vector3.up,b-a));
        void Ball(Material m,Vector3 c,Vector3 scale)=>Shape(m,sphere,c,scale,Quaternion.identity);
        void Ring(Material m,Vector3 c,float radius,Vector3 normal,float width)
        {var right=Vector3.Cross(normal,Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up).normalized;var up=Vector3.Cross(normal,right);for(int i=0;i<48;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Pipe(m,c+radius*(right*Mathf.Cos(a)+up*Mathf.Sin(a)),c+radius*(right*Mathf.Cos(b)+up*Mathf.Sin(b)),width);}}
        void Light(Vector3 c,bool office=false,bool stair=false)
        {Box(metal,c,new Vector3(1.5f,.16f,.6f));Box(white,c-Vector3.up*.085f,new Vector3(1.3f,.02f,.43f));var go=new GameObject("Neutral lunar luminaire");go.transform.SetParent(transform,false);go.transform.localPosition=c-Vector3.up*.25f;var l=go.AddComponent<Light>();l.type=LightType.Spot;l.spotAngle=125;go.transform.localRotation=Quaternion.Euler(90,0,0);l.color=new Color(1,.97f,.89f);l.intensity=P(stair?"stairLamp":office?"officeLamp":"lamp");l.range=P(office&&!stair?"officeSpotRange":"range");if(office&&!stair)l.innerSpotAngle=P("officeInnerAngle");l.renderMode=LightRenderMode.ForcePixel;LightCount++;
            if(office)
            {
                // Diffuse ceiling bounce is represented by local point fill, kept out of exterior ambient.
                var fill=new GameObject("Office diffuse ceiling bounce");fill.transform.SetParent(transform,false);fill.transform.localPosition=c-Vector3.up*.8f;
                var f=fill.AddComponent<Light>();f.type=LightType.Point;f.color=new Color(.96f,.98f,1);f.intensity=P(stair?"stairFill":"officeFill");f.range=P(stair?"range":"officeFillRange");f.renderMode=LightRenderMode.ForcePixel;LightCount++;
            }
        }
        void Label(string value,Vector3 pos,Quaternion rot,float size,Color color,Vector2 area)
        {
            var go=new GameObject(value);go.transform.SetParent(transform,false);go.transform.localPosition=pos;go.transform.localRotation=rot;
            var t=go.AddComponent<TextMesh>();t.text=value;t.fontSize=64;t.characterSize=size*10/64*P("signScale");t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;t.color=color;
            if(!fontMaterial){signFont=t.font;fontMaterial=new Material(Shader.Find("StarTournament/LunarSign"));fontMaterial.mainTexture=signFont.material.mainTexture;owned.Add(fontMaterial);Font.textureRebuilt+=RefreshFont;}
            var renderer=t.GetComponent<MeshRenderer>();renderer.sharedMaterial=fontMaterial;
            // Centre actual glyph bounds, including multiline baselines, and fit the backing.
            var bounds=renderer.localBounds;
            float fit=Mathf.Min(1,Mathf.Min(area.x/Mathf.Max(.001f,bounds.size.x),area.y/Mathf.Max(.001f,bounds.size.y)));
            go.transform.localScale=Vector3.one*fit;
            go.transform.localPosition=pos-rot*(bounds.center*fit);
        }
        void WallSign(string wallId,Vector3 normal,string text,Vector2 panel,float textSize,Material material,float verticalOffset=0)
        {
            var wall=Array.Find(definition.Solids,s=>s.Id==wallId);
            var half=Vector3.Scale(wall.Size,normal)*.5f;
            var surface=wall.Position+half+Vector3.up*verticalOffset;
            var rotation=Quaternion.LookRotation(-normal,Vector3.up);
            // Back of the panel touches the authored wall; lettering clears its front by 2 mm.
            Box(material,surface+normal*.01f,new Vector3(panel.x,panel.y,.02f),rotation);
            Label(text,surface+normal*.022f,rotation,textSize,Color.white,panel-new Vector2(.16f,.10f));
        }
        void RefreshFont(Font font){if(font==signFont&&fontMaterial)fontMaterial.mainTexture=font.material.mainTexture;}
        void Grating(ArenaDefinition d)
        {
            // Movement support is continuous for a capsule; visible bars are also canonical shot solids.
            foreach(var s in d.Solids)if(s.Surface=="lunar-grate")
            {Box(metal,s.Position+new Vector3(0,-.13f,0),new Vector3(s.Size.x,.07f,.06f));}
        }
        void Equipment(ArenaSolid s)
        {
            var c=s.Position;var sz=s.Size;
            foreach(int a in new[]{-1,1})Box(metal,c+new Vector3(a*(sz.x*.5f-.12f),0,0),new Vector3(.12f,sz.y+.025f,sz.z+.025f));
            Box(ochre,c+new Vector3(0,0,-sz.z*.5f-.015f),new Vector3(sz.x*.65f,.12f,.02f));
            Box(graphite,c+new Vector3(0,.15f,sz.z*.5f+.015f),new Vector3(sz.x*.6f,.3f,.02f));
            if(s.Id.StartsWith("cargo-"))
            {
                // Flush crate kit: corner straps, lid seal, hinges and recessed carry handles.
                foreach(int face in new[]{-1,1})
                {
                    Box(metal,c+new Vector3(0,sz.y*.5f-.09f,face*(sz.z*.5f+.02f)),new Vector3(sz.x,.12f,.05f));
                    foreach(int side in new[]{-1,1})
                    {var q=c+new Vector3(side*sz.x*.3f,0,face*(sz.z*.5f+.035f));Box(graphite,q,new Vector3(.28f,.32f,.035f));Box(metal,q+Vector3.up*.03f,new Vector3(.18f,.07f,.06f));}
                    Box(ivory,c+new Vector3(sz.x*.25f,-sz.y*.3f,face*(sz.z*.5f+.022f)),new Vector3(.32f,.16f,.03f));
                }
                for(int j=0;j<3;j++)Box(metal,c+new Vector3(-sz.x*.25f+j*sz.x*.25f,sz.y*.5f+.018f,0),new Vector3(.045f,.04f,sz.z*.8f));
            }
            if(s.Id=="scanner"){Ring(blue,c+Vector3.up*1.7f,1.2f,Vector3.forward,.13f);Ball(white,c+Vector3.up*.65f,Vector3.one*.35f);}
            if(s.Id=="samples")for(int j=-1;j<=1;j++)for(int a=0;a<2;a++){var q=c+new Vector3(-.46f,a*.6f-.3f,j*1.05f);Ball(blue,q,new Vector3(.28f,.45f,.28f));}
        }
        void Drift(Vector3 c,float rx,float rz,int seed)
        {
            var random=new System.Random(seed);var v=new List<Vector3>{c+Vector3.up*.008f};var uv=new List<Vector2>{new Vector2(c.x,c.z)/2};var triangles=new List<int>();
            for(int i=0;i<=24;i++){float angle=i*Mathf.PI/12;float r=.65f+(float)random.NextDouble()*.35f;var q=c+new Vector3(Mathf.Cos(angle)*rx*r,0,Mathf.Sin(angle)*rz*r);v.Add(q);uv.Add(new Vector2(q.x,q.z)/2);if(i>0)triangles.AddRange(new[]{0,i+1,i});}
            var mesh=new Mesh{name="Wind-shaped regolith deposit"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();owned.Add(mesh);Shape(sand,mesh,Vector3.zero,Vector3.one,Quaternion.identity);
        }
        void Architecture()
        {
            // Panel seams, rounded structural trims, low ducts, roof service furniture.
            foreach(int sign in new[]{-1,1})
            {
                foreach(float y in new[]{.3f,4.3f,8.8f})
                {Box(ochre,new Vector3(0,y,sign*12.21f),new Vector3(24,.14f,.09f));Box(ochre,new Vector3(sign*12.21f,y,8),new Vector3(.09f,.14f,8));Box(ochre,new Vector3(sign*12.21f,y,-8),new Vector3(.09f,.14f,8));}
                foreach(float x in new[]{-11.8f,11.8f})Pipe(ivory,new Vector3(x,.25f,sign*11.8f),new Vector3(x,8.7f,sign*11.8f),.28f);
                foreach(float z in new[]{-9f,9f})for(int i=0;i<3;i++)Pipe(metal,new Vector3(sign*12.25f,.3f+i*.22f,z-2),new Vector3(sign*12.25f,.3f+i*.22f,z+2),.09f);
                // Rounded trim is seated in the facade; posts meet the physical header.
                foreach(bool upper in new[]{false,true})foreach(float x in !upper||sign<0?new[]{0f}:new[]{-6.5f,6.5f})
                {
                    var frame=EntryTrimBounds(sign,upper,x,!upper||sign<0?4:3);
                    var left=new Vector3(frame.min.x,frame.min.y,frame.center.z);
                    var right=new Vector3(frame.max.x,frame.min.y,frame.center.z);
                    var topLeft=new Vector3(left.x,frame.max.y,left.z);
                    var topRight=new Vector3(right.x,frame.max.y,right.z);
                    Pipe(ivory,left,topLeft,.15f);Pipe(ivory,right,topRight,.15f);Pipe(ivory,topLeft,topRight,.15f);
                }
                for(int q=-28;q<=28;q+=14)
                {var a=new Vector3(q,0,sign*31);Ball(white,a+Vector3.up*3.7f,new Vector3(.22f,.65f,.22f));Light(a+Vector3.up*4);var b=new Vector3(sign*31,0,q);Ball(white,b+Vector3.up*3.7f,new Vector3(.22f,.65f,.22f));Light(b+Vector3.up*4);}
            }
            for(int x=-9;x<=9;x+=6)foreach(int z in new[]{-9,-3,3,9})foreach(float y in new[]{3.9f,8.4f})Light(new Vector3(x,y,z),true);
            foreach(int a in new[]{-1,1})Light(new Vector3(a*19,7.8f,0),true,true);
            // Paving leaves regolith visible; deterministic irregular sand drifts lay over it.
            for(int x=-28;x<=28;x+=4)for(int z=-28;z<=28;z+=4)
            {
                if(Mathf.Abs(x)<15&&Mathf.Abs(z)<15||Mathf.Abs(x)<25&&Mathf.Abs(z)<8)continue;
                if((x+z)%12!=0)Box(paving,new Vector3(x,.015f,z),new Vector3(3.85f,.018f,3.85f));
                if((x*7+z*3)%5==0)Drift(new Vector3(x,.035f,z),2.4f,1.7f,x*17+z);
            }
            foreach(int a in new[]{-1,1})for(int i=-10;i<=10;i+=4)Drift(new Vector3(i,.045f,a*12.7f),2.6f,1.1f,i*11+a);
            Box(ivory,new Vector3(-4,9.4f,2),new Vector3(3,.8f,4));Box(metal,new Vector3(-4,9.85f,2),new Vector3(2.8f,.1f,3.8f));
            Pipe(metal,new Vector3(4,9,0),new Vector3(4,13,0),.12f);Ring(ivory,new Vector3(4,12,0),1.4f,new Vector3(0,.6f,1).normalized,.22f);
        }
        Bounds EntryTrimBounds(int sign,bool upper,float x,float width)
        {
            var id=(sign<0?"s":"n")+(upper?"-upper-door-header":"-ground-header");
            var header=Array.Find(definition.Solids,s=>s.Id==id);
            float bottom=upper?LunarLaboratoryCatalog.Upper:0;
            float top=header.Position.y-header.Size.y*.5f;
            // Cylinder diameter is .15m: embed a quarter diameter in the wall surface.
            float z=header.Position.z+sign*(header.Size.z*.5f-.15f*.25f);
            return new Bounds(new Vector3(x,(bottom+top)*.5f,z),new Vector3(width,top-bottom,.15f));
        }
        void GlassFrame(ArenaSolid s)
        {
            // Flush glazing beads do not extend beyond the canonical glass boundary.
            var c=s.Position;float w=s.Size.x,h=s.Size.y;
            foreach(int a in new[]{-1,1})
            {Box(metal,c+new Vector3(0,a*(h*.5f-.035f),0),new Vector3(w,.07f,.14f));Box(metal,c+new Vector3(a*(w*.5f-.035f),0,0),new Vector3(.07f,h,.14f));}
            // A narrow pale edge reflection makes the thick pane readable without painted symbols.
            Box(blue,c+new Vector3(0,h*.5f-.11f,-.065f),new Vector3(w,.025f,.008f));
        }
        void RoofDetails()
        {
            // Fixed mesh-kit detailing, renderer-only; all sits on inaccessible roofs or flush to walls.
            for(int a=-1;a<=1;a+=2)
            {
                for(int z=-8;z<=8;z+=8)
                {
                    var c=new Vector3(a*7,9.45f,z);Box(metal,c,new Vector3(3,.85f,2.3f));
                    Box(ivory,c+Vector3.up*.47f,new Vector3(3.2f,.1f,2.5f));
                    for(int j=-4;j<=4;j++)Box(graphite,c+new Vector3(j*.3f,.535f,0),new Vector3(.12f,.04f,1.9f));
                    Pipe(ochre,c+new Vector3(0,0,1.2f),new Vector3(a*10,9.4f,z+1.2f),.17f);
                }
                for(int z=-9;z<=9;z+=3)
                {Box(metal,new Vector3(a*11.8f,9.16f,z),new Vector3(.18f,.32f,2.7f));}
                // Opaque lower facade ribs and upper header band, never across a door or glass.
                for(int x=-10;x<=10;x+=4)
                {if(Mathf.Abs(x)<3||x==-6)continue;Box(ivory,new Vector3(x,1.5f,a*12.22f),new Vector3(.18f,2.8f,.15f));}
                for(int x=-8;x<=8;x+=8)
                {Box(graphite,new Vector3(x,8.55f,a*12.22f),new Vector3(2.6f,.36f,.08f));for(int j=-4;j<=4;j++)Box(metal,new Vector3(x+j*.26f,8.55f,a*12.28f),new Vector3(.07f,.3f,.07f));}
            }
            // Angled solar wings and a dish give the roof an identifiable scientific silhouette.
            var solar=Mat("Blue photovoltaic cells",new Color(.055f,.15f,.24f),Texture("Solar cell grid",0),.65f);
            foreach(int a in new[]{-1,1})
            {var c=new Vector3(a*4,10.7f,-8);Pipe(metal,new Vector3(a*4,9,-8),c,.16f);Box(metal,c,new Vector3(4.3f,.14f,3),Quaternion.Euler(18,0,0));for(int x=-2;x<=2;x++)for(int z=-1;z<=1;z++)Box(solar,c+Quaternion.Euler(18,0,0)*new Vector3(x*.8f,.09f,z*.9f),new Vector3(.73f,.03f,.83f),Quaternion.Euler(18,0,0));}
            var dish=new Vector3(3.8f,12.1f,-8);var axis=new Vector3(.25f,.65f,1).normalized;
            Ring(ivory,dish,1.8f,axis,.12f);Ball(ivory,dish,new Vector3(3.3f,1.5f,.38f));Pipe(metal,new Vector3(3.8f,9,-8),dish,.2f);Pipe(metal,dish,dish+axis*1.5f,.07f);
            WallSign("s-upper-cap-1",Vector3.back,"LUNA / 04",new Vector2(3.6f,.65f),.52f,graphite);
        }
        void Zones()
        {
            foreach(float y in new[]{0f,4.5f})
            {
                foreach(int a in new[]{-1,1})
                {
                    // Two faces: exterior and interior lettering, paint never covers glass.
                    var mat=a>0?blue:ochre;string text=a>0?"N 01 / АНАЛИЗ":"S 02 / ИСПЫТАНИЯ";
                    string side=a>0?"n":"s",wall=y==0?side+"-ground-left":side+"-upper-cap-"+(a>0?1:0);
                    foreach(int face in new[]{-1,1})WallSign(wall,Vector3.forward*face,text,new Vector2(6,y==0?1.2f:.65f),y==0?.62f:.45f,mat);
                    if(y==0){Box(mat,new Vector3(6,y+.55f,a*11.8f),new Vector3(7,.09f,.03f));Box(mat,new Vector3(6,y+.78f,a*11.8f),new Vector3(7,.09f,.03f));}
                    // Lower annex face starts at the switchback landing; upper face spans 4.5–9 m.
                    WallSign((a<0?"w":"e")+"-annex-back",Vector3.right*(-a),a<0?"W 03\nКОММУНИКАЦИИ":"E 04\nЭНЕРГОБЛОК",new Vector2(4,1.3f),.44f,a<0?green:purple,y==0?-1.125f:2.25f);
                }
                foreach(float z in new[]{-.8f,0,.8f})Pipe(green,new Vector3(-20.7f,y+.4f,z),new Vector3(-20.7f,y+.4f,z+2),.08f);
                // The lower landing is at 2.25 m, not the ground floor. Both motifs clear it.
                var landing=Array.Find(definition.Solids,v=>v.Id=="w-mid");
                float ringY=y==0?landing.Position.y+landing.Size.y*.5f+1.1f:y+2.6f;
                Ring(green,new Vector3(-20.6f,ringY,-2.5f),.55f,Vector3.right,.09f);Pipe(green,new Vector3(-20.6f,ringY-.55f,-2.5f),new Vector3(-20.6f,ringY+.55f,-2.5f),.055f);
                for(int i=-1;i<=1;i++){Box(purple,new Vector3(20.75f,y+.8f,i*1.05f),new Vector3(.035f,.7f,.72f));Box(graphite,new Vector3(20.71f,y+.8f,i*1.05f),new Vector3(.025f,.35f,.52f));}
            }
            foreach(int a in new[]{-1,1})
            {
                WallSign((a<0?"w":"e")+"-annex-back",Vector3.right*a,a<0?"W 03 / СВЯЗЬ":"E 04 / ЭНЕРГИЯ",new Vector2(5,1.35f),.6f,a<0?green:purple,-2.25f);
                foreach(int face in new[]{-1,1})for(int i=0;i<3;i++)
                {var q=new Vector3(5+i*1.1f,2.1f,-12+face*.2f);Pipe(ochre,q+new Vector3(-.3f,-.35f,0),q+new Vector3(.15f,0,0),.055f);Pipe(ochre,q+new Vector3(.15f,0,0),q+new Vector3(-.3f,.35f,0),.055f);}
            }
            Ring(ochre,new Vector3(0,4.515f,-7),3,Vector3.up,.07f);
            WallSign("lab-front-inner--1",Vector3.forward,"A / СКАНЕР",new Vector2(3.8f,1),.6f,blue);
            WallSign("lab-front-inner-1",Vector3.forward,"B / ОБРАЗЦЫ",new Vector2(3.8f,1),.6f,blue);
            WallSign("divider-base",Vector3.forward,"H +",new Vector2(2,.85f),.8f,blue);
            WallSign("divider-base",Vector3.back,"V >>",new Vector2(2,.85f),.8f,ochre);
        }
        void PickupSigns(ArenaDefinition d)
        {
            foreach(var item in d.Pickups)
            {
                // Yard deposits reach 0.053 m; the plate extends down to the real floor.
                float pavingLift=item.Anchor.y==0?.054f:0;
                var c=item.Anchor+Vector3.up*(.004f+pavingLift);
                Ring(item.Kind==ArenaPickupKind.Cutter?ochre:white,c,.78f,Vector3.up,.008f);
                var centre=c+new Vector3(0,0,-.9f);
                // A flush plate also supports the complete glyph on an open balcony grating.
                Box(graphite,centre-Vector3.up*(pavingLift*.5f),new Vector3(1.2f,.008f+pavingLift,.48f));
                Label(item.Id,centre+Vector3.up*.006f,Quaternion.Euler(90,0,0),.45f,Color.white,new Vector2(1,.36f));
            }
        }
        void CourtyardDetail()
        {
            // Surface-scale gravel and dust are decorative kit detail, below a capsule step.
            // Tall silhouettes live outside the fence; substantial yard obstacles are catalog solids.
            var chips=new[]{RockMesh(70),RockMesh(71),RockMesh(72)};
            var random=new System.Random(63026);
            for(int i=0;i<550;i++)
            {
                float x=-31+(float)random.NextDouble()*62,z=-31+(float)random.NextDouble()*62;
                if(Mathf.Abs(x)<25&&Mathf.Abs(z)<16)continue;
                bool marker=false;foreach(var pickup in definition.Pickups)if(pickup.Anchor.y==0&&(new Vector2(x-pickup.Anchor.x,z-pickup.Anchor.z+.9f)).sqrMagnitude<1.5f){marker=true;break;}
                if(marker)continue;
                float size=.07f+(float)random.NextDouble()*.19f;
                Shape(sand,chips[i%3],new Vector3(x,.027f,z),new Vector3(size,.035f+size*.3f,size),Quaternion.Euler(0,i*43,0));
            }
            foreach(int side in new[]{-1,1})foreach(int row in new[]{-1,1})
            {
                // Canonical insulated service conduit body carries visible pipe joints, valves and labels.
                var c=new Vector3(side*30,.5f,row*15);
                Pipe(metal,c+new Vector3(0,.48f,-4.5f),c+new Vector3(0,.48f,4.5f),.23f);
                for(int j=-3;j<=3;j+=3){Ring(ochre,c+new Vector3(0,.48f,j),.25f,Vector3.forward,.035f);Box(metal,c+new Vector3(0,-.42f,j),new Vector3(1.1f,.12f,.6f));}
                Ring(ochre,c+new Vector3(-side*.5f,.1f,0),.28f,Vector3.right,.035f);
                for(int j=0;j<10;j++)
                {float x=side*(33+j%3*1.7f),z=row*(9+j*2.4f);GroundedRock(chips[j%3],new Vector3(x,0,z),new Vector3(.7f+j%3*.5f,.4f+j%4*.35f,1.1f),Quaternion.Euler(0,j*31,0));}
                Drift(new Vector3(side*23,.045f,row*23),4,2.3f,side*77+row);
            }
        }
        void Background()
        {
            // Finite sky shell and landscape remain inside the existing camera far plane, outside collision/nav.
            float radius=P("skyDistance");var sky=Mat("Black lunar sky and stars",new Color(.006f,.009f,.016f),null,0,true);
            // Inward sphere, reversed winding. No collider and never a navigation source.
            var dome=Instantiate(sphere);owned.Add(dome);var triangles=dome.triangles;Array.Reverse(triangles);dome.triangles=triangles;dome.RecalculateNormals();Shape(sky,dome,Vector3.zero,Vector3.one*radius*2,Quaternion.identity);
            var earth=new Material(Shader.Find("StarTournament/LunarEarth")){name="NASA Blue Marble / clouds and atmosphere"};earth.SetTexture("_BaseMap",Resources.Load<Texture2D>("LunarEarth"));owned.Add(earth);
            Ball(earth,new Vector3(-70,63,90),Vector3.one*P("earthRadius")*2);Ball(white,new Vector3(60,84,75),Vector3.one*P("sunRadius")*2);
            var random=new System.Random(42026);
            for(int i=0;i<450;i++){var dir=new Vector3((float)random.NextDouble()*2-1,(float)random.NextDouble(),(float)random.NextDouble()*2-1).normalized;Ball(white,dir*(radius-3),Vector3.one*(i%4==0?.14f:.07f));}
            // Annulus of triangulated regolith and a continuous jagged mountain horizon.
            var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            const int segments=160;const int rings=9;
            for(int ring=0;ring<=rings;ring++)for(int i=0;i<=segments;i++)
            {
                var point=LandscapeVertex(ring,i,P("mountainHeight"));
                v.Add(point);uv.Add(new Vector2(point.x,point.z)*.12f);
                if(ring<rings&&i<segments){int k=ring*(segments+1)+i;tris.AddRange(new[]{k,k+segments+1,k+1,k+1,k+segments+1,k+segments+2});}
            }
            var terrain=new Mesh{name="Unavailable lunar wastes and mountains"};terrain.SetVertices(v);terrain.SetUVs(0,uv);terrain.SetTriangles(tris,0);terrain.RecalculateNormals();owned.Add(terrain);Shape(sand,terrain,Vector3.zero,Vector3.one,Quaternion.identity);
            // Fill square-to-annulus gap beyond the fence, below the playable plane.
            Box(sand,new Vector3(0,-.45f,0),new Vector3(90,.2f,90));
            foreach(var c in new[]{new Vector3(-42,0,37),new Vector3(48,0,23),new Vector3(8,0,-55),new Vector3(35,0,49)})Crater(c,5);
            for(int i=0;i<100;i++){float a=(float)random.NextDouble()*Mathf.PI*2,r=47+(float)random.NextDouble()*60;var c=new Vector3(Mathf.Sin(a)*r,.2f,Mathf.Cos(a)*r);GroundedRock(RockMesh(i),c,new Vector3(1+i%3,.6f+i%4,1.5f),Quaternion.Euler(0,i*37,0));}
        }
        // Fixed tessellation and contact inset are asset construction tolerances, not gameplay controls.
        internal static Vector3 LandscapeVertex(int ring,int segment,float mountainHeight)
        {
            float a=segment*Mathf.PI*2/160,r=40+ring*10;
            float h=ring<2?-.35f:(.25f+Mathf.PerlinNoise(Mathf.Cos(a)*5+17,Mathf.Sin(a)*5+33)*.65f+Mathf.Abs(Mathf.Sin(a*11+.6f)*.5f+Mathf.Sin(a*23)*.3f+Mathf.Sin(a*7)*.3f)*.55f)*mountainHeight*Mathf.Pow(ring/9f,3)-1;
            return new Vector3(Mathf.Sin(a)*r,h,Mathf.Cos(a)*r);
        }
        internal static float LandscapeHeight(Vector3 point,float mountainHeight)
        {
            // Sample the rendered triangles, including the square underlay; polar interpolation
            // alone disagrees with the polygonal mesh on sloped cells and can leave air gaps.
            float height=Mathf.Abs(point.x)<=45&&Mathf.Abs(point.z)<=45?-.35f:float.NegativeInfinity;
            float angle=Mathf.Atan2(point.x,point.z);if(angle<0)angle+=Mathf.PI*2;
            int segment=Mathf.Min(159,Mathf.FloorToInt(angle*160/(Mathf.PI*2)));
            for(int ring=0;ring<9;ring++)
            {
                var a=LandscapeVertex(ring,segment,mountainHeight);var b=LandscapeVertex(ring+1,segment,mountainHeight);
                var c=LandscapeVertex(ring,segment+1,mountainHeight);var d=LandscapeVertex(ring+1,segment+1,mountainHeight);
                Sample(a,b,c);Sample(c,b,d);
            }
            return float.IsNegativeInfinity(height)?-.35f:height;
            void Sample(Vector3 a,Vector3 b,Vector3 c)
            {
                float det=(b.z-c.z)*(a.x-c.x)+(c.x-b.x)*(a.z-c.z);
                float u=((b.z-c.z)*(point.x-c.x)+(c.x-b.x)*(point.z-c.z))/det;
                float v=((c.z-a.z)*(point.x-c.x)+(a.x-c.x)*(point.z-c.z))/det;
                if(u>=-.00001f&&v>=-.00001f&&u+v<=1.00001f)height=Mathf.Max(height,u*a.y+v*b.y+(1-u-v)*c.y);
            }
        }
        void GroundedRock(Mesh source,Vector3 centre,Vector3 scale,Quaternion rotation)
        {
            var mesh=Instantiate(source);owned.Add(mesh);var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
            {
                var q=rotation*Vector3.Scale(vertices[i],scale);q.x+=centre.x;q.z+=centre.z;
                q.y+=LandscapeHeight(q,P("mountainHeight"))-.025f;vertices[i]=q;
            }
            mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
            Shape(sand,mesh,Vector3.zero,Vector3.one,Quaternion.identity);
        }
        void Crater(Vector3 centre,float radius)
        {
            var v=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();
            // Raised impact lip merges into the existing distant regolith. No playable collision.
            var radii=new[]{0f,.52f,.76f,.86f,1.2f};var heights=new[]{-.4f,-.25f,.7f,.48f,-.3f};
            for(int ring=0;ring<5;ring++)for(int i=0;i<=48;i++)
            {float a=i*Mathf.PI/24;var q=centre+new Vector3(Mathf.Sin(a)*radii[ring]*radius,heights[ring]+.12f*Mathf.Sin(a*7)*radii[ring],Mathf.Cos(a)*radii[ring]*radius);v.Add(q);uv.Add(new Vector2(q.x,q.z)*.12f);if(ring<4&&i<48){int k=ring*49+i;t.AddRange(new[]{k,k+49,k+1,k+1,k+49,k+50});}}
            var mesh=new Mesh{name="Lunar impact crater rim"};mesh.SetVertices(v);mesh.SetUVs(0,uv);mesh.SetTriangles(t,0);mesh.RecalculateNormals();owned.Add(mesh);Shape(sand,mesh,Vector3.zero,Vector3.one,Quaternion.identity);
        }
        Mesh RockMesh(int seed)
        {
            var rng=new System.Random(seed);var v=new Vector3[10];
            for(int i=0;i<5;i++){float a=i*Mathf.PI*2/5;float r=.75f+(float)rng.NextDouble()*.25f;v[i]=new Vector3(Mathf.Cos(a)*r,0,Mathf.Sin(a)*r);v[i+5]=new Vector3(Mathf.Cos(a)*r*.6f,.6f+(float)rng.NextDouble()*.4f,Mathf.Sin(a)*r*.6f);}
            var t=new List<int>();for(int i=0;i<5;i++){int j=(i+1)%5;t.AddRange(new[]{i,i+5,j,j,i+5,j+5});}t.AddRange(new[]{5,6,7,5,7,8,5,8,9});
            // Duplicate vertices for hard faceted normals, retaining the deterministic rock silhouette.
            var points=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();foreach(int i in t){indices.Add(points.Count);points.Add(v[i]);uv.Add(new Vector2(v[i].x,v[i].y));}
            var m=new Mesh{name="Angular lunar basalt"};m.SetVertices(points);m.SetUVs(0,uv);m.SetTriangles(indices,0);m.RecalculateNormals();owned.Add(m);return m;
        }
        void Flush()
        {
            foreach(var pair in batches){var go=new GameObject(pair.Key.name);go.transform.SetParent(transform,false);var m=new Mesh{name=go.name,indexFormat=IndexFormat.UInt32};m.CombineMeshes(pair.Value.ToArray());owned.Add(m);go.AddComponent<MeshFilter>().sharedMesh=m;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=pair.Key;r.shadowCastingMode=pair.Key==white||pair.Key==grate?ShadowCastingMode.Off:ShadowCastingMode.On;}
        }
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;foreach(var item in owned)if(item)DestroyImmediate(item);}
    }
}
