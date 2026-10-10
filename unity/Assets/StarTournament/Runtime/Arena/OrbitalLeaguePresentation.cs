using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    /// <summary>Renderer-only kit for immutable Combat Bowl. Never creates a physics object.</summary>
    public sealed partial class OrbitalLeaguePresentation : MonoBehaviour
    {
        // Surface epsilon avoids coplanar z-fighting; technical, not traversable geometry.
        const float Epsilon=.006f;
        // Tessellation and texture resolution are fixed asset encoding, not gameplay controls.
        const int TextureSize=256;
        readonly List<UnityEngine.Object> owned=new List<UnityEngine.Object>();
        readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        readonly List<Transform> orbits=new List<Transform>(),fans=new List<Transform>();
        readonly List<Vector3> fixturePositions=new List<Vector3>();
        Mesh cube; Material wall,floor,ceiling,trim,cyan,media,white,sign; Font signFont; ProvingProfile p;
        ArenaSolid[] structuralSolids;int structuralIndex;
        Color oldAmbient; AmbientMode oldMode;
        public int LocalLightCount {get;private set;}
        public int ShadowLightCount {get;private set;}
        public int FixtureCount => fixturePositions.Count;
        public int FixtureLightCount {get;private set;}
        public string ProfileIdentity {get;private set;}
        public void Build(ArenaDefinition definition,ProvingProfile profile)
        { var steps=BuildSteps(definition,profile);while(steps.MoveNext()){} }
        public IEnumerator BuildSteps(ArenaDefinition definition,ProvingProfile profile)
        {
            profile.EnsureOrbitalLeagueDescriptors();
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
            InitializeWayfinding();
            media=Flat("Muted orbital horizon",Palette("cyan")*p.Get("broadcast.screenValue"));
            white=new Material(Shader.Find("StarTournament/OrbitalSurface")){name="Broad white luminaire"};white.SetColor("_Color",Palette("white")*p.Get("light.emission"));owned.Add(white);
            signFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            signFont.RequestCharactersInTexture("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 /",64,FontStyle.Normal);
            sign=new Material(Shader.Find("StarTournament/OrbitalSurface")){name="Depth-tested league lettering"};owned.Add(sign);RefreshFont(signFont);Font.textureRebuilt+=RefreshFont;
            structuralSolids=definition.Solids;
            for(int solidIndex=0;solidIndex<definition.Solids.Length;solidIndex++)
            {
                if(solidIndex%16==0)yield return null;
                var s=definition.Solids[solidIndex];
                structuralIndex=solidIndex;
                var target=transform.parent.Find(s.Id);if(!target)continue;
                if(s.Surface=="grating"){Grating(s,target);continue;}
                if(s.Surface=="window"){Window(s,target);continue;}
                bool roof=s.Id.Contains("roof")||s.Id.Contains("ceiling");bool ground=s.Material=="floor";
                var filter=target.GetComponent<MeshFilter>();
                // Authored collision boxes intentionally overlap. Their coincident render faces
                // must be removed without changing the immutable solids or their colliders.
                var mesh=VisibleSolidMesh(s,definition.Solids,solidIndex)??Instantiate(filter.sharedMesh);owned.Add(mesh);
                var normals=mesh.normals;var uv=mesh.uv;var vertices=mesh.vertices;
                for(int i=0;i<uv.Length;i++)
                {
                    Vector3 v=Vector3.Scale(vertices[i],s.Size);var n=normals[i];
                    uv[i]=(Mathf.Abs(n.y)>.5f?new Vector2(v.x,v.z):Mathf.Abs(n.x)>.5f?new Vector2(v.z,v.y):new Vector2(v.x,v.y))/p.Get("surface.tile");
                }
                mesh.uv=uv;
                if(s.Id.EndsWith("-diagonal",StringComparison.Ordinal))
                {
                    // The yawed deck shares its top plane with the balcony and spawn slab.
                    // Lift only its render mesh by two surface epsilons; collider stays authored.
                    var lifted=mesh.vertices;
                    for(int i=0;i<lifted.Length;i++)lifted[i].y+=2*Epsilon/s.Size.y;
                    mesh.vertices=lifted;mesh.RecalculateBounds();
                }
                filter.sharedMesh=mesh;target.GetComponent<Renderer>().sharedMaterial=roof?ceiling:ground?floor:wall;
                // Ceiling panels are below the roof. The roof must not shadow the arena-wide key light.
                if(roof){target.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;Roof(s);}
                else if(ground)Floor(s);
                else if(!s.Id.StartsWith("veteran-",StringComparison.Ordinal))Walls(s);
            }
            // Central hall slab is also the basement ceiling: dress its underside only inside the corridor.
            CeilingFixture(new Vector3(0,3.58f,-4),Quaternion.identity);
            CeilingFixture(new Vector3(0,3.58f,1),Quaternion.identity);
            CeilingFixture(new Vector3(0,3.58f,6),Quaternion.identity);
            PickupMarkings(definition);yield return null;Labels();yield return null;BroadcastDetails();yield return null;WayfindingSigns();yield return null;SpaceBackdrop();yield return null;VeteranSlice(definition);yield return null;
            // Shared ceiling zones retain the accepted F2 light and two bounded local shadow maps.
            var poweredFixtures=new HashSet<int>();
            for(int zone=0;zone<16;zone++)
            {
                Vector3 anchor=Position("layout.fill-"+zone);
                int fixtureIndex=Enumerable.Range(0,fixturePositions.Count).OrderBy(i=>(fixturePositions[i]-anchor).sqrMagnitude).First();
                Vector3 fixture=fixturePositions[fixtureIndex];poweredFixtures.Add(fixtureIndex);
                var go=new GameObject("League ceiling light / "+zone);go.transform.SetParent(transform,false);go.transform.localPosition=fixture-Vector3.up*.12f;
                // The visible fixture sits off-centre in some rooms; aim its real beam at the authored zone.
                Vector3 target=anchor-Vector3.up*p.Get("light.targetDrop");
                if(zone==0)target.z+=p.Get("light.hallAimOffsetZ");
                if(zone>=6 && zone<=11)target.x-=Mathf.Sign(fixture.x)*p.Get("light.outerAimInset");
                if(zone>=12)target.x-=Mathf.Sign(fixture.x)*p.Get("light.outerAimInset");
                go.transform.localRotation=Quaternion.LookRotation(target-go.transform.localPosition);
                var l=go.AddComponent<Light>();l.type=LightType.Spot;l.spotAngle=p.Get("light.spotAngle");l.color=Palette("fill");l.intensity=p.Get("light.intensity");l.range=p.Get("light.range");
                bool casts=zone<Mathf.RoundToInt(p.Get("light.shadowCasters"));
                l.shadows=casts?LightShadows.Soft:LightShadows.None;
                if(casts){l.shadowStrength=p.Get("light.shadowStrength");l.shadowCustomResolution=Mathf.RoundToInt(p.Get("light.shadowResolution"));l.shadowBias=p.Get("light.shadowBias");l.shadowNormalBias=p.Get("light.shadowNormalBias");ShadowLightCount++;}
                LocalLightCount++;
            }
            // Every remaining luminous panel needs a real local contribution. These lights share
            // the existing range/angle controls and never allocate another shadow map.
            for(int i=0;i<fixturePositions.Count;i++)
            {
                if(poweredFixtures.Contains(i))continue;
                var go=new GameObject("League fixture light / "+i);go.transform.SetParent(transform,false);
                go.transform.localPosition=fixturePositions[i]-Vector3.up*.12f;
                go.transform.localRotation=Quaternion.LookRotation(Vector3.down);
                var light=go.AddComponent<Light>();light.type=LightType.Spot;
                light.spotAngle=p.Get("light.spotAngle");light.color=Palette("fill");
                float baseRange=p.Get("light.fixtureRange");
                float maxRange=p.Descriptor("light.fixtureRange").Maximum;
                int surfaces=(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer);
                if(gameObject.scene.GetPhysicsScene().Raycast(go.transform.position,Vector3.down,out var surface,maxRange,surfaces,QueryTriggerInteraction.Ignore))
                    light.range=Mathf.Clamp(Mathf.Max(baseRange,surface.distance*p.Get("light.fixtureSurfaceReach")),baseRange,maxRange);
                else light.range=baseRange;
                // Preserve a comparable pool on the lower floor when the beam must travel farther.
                float reach=light.range/baseRange;
                light.intensity=p.Get("light.intensity")*p.Get("light.fixtureContribution")*reach*reach;
                light.shadows=LightShadows.None;
                LocalLightCount++;FixtureLightCount++;
            }
            foreach(var entry in batches)
            {
                var go=new GameObject(entry.Key.name+" / batched detail");go.transform.SetParent(transform,false);
                var mesh=new Mesh{name=go.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(entry.Value.ToArray());owned.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=entry.Key;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            }
        }
        void Grating(ArenaSolid s,Transform target)
        {
            var bars=new List<CombineInstance>();float pitch=p.Get("ring.gratingPitch"),width=p.Get("ring.gratingBar"),depth=p.Get("ring.gratingDepth")/s.Size.y;float y=(1-depth)/2;
            for(float x=-s.Size.x/2+width/2;x<s.Size.x/2;x+=pitch)
                bars.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(new Vector3(x/s.Size.x,y,0),Quaternion.identity,new Vector3(width/s.Size.x,depth,1))});
            for(float z=-s.Size.z/2+width/2;z<s.Size.z/2;z+=pitch)
                bars.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(new Vector3(0,y,z/s.Size.z),Quaternion.identity,new Vector3(1,depth,width/s.Size.z))});
            var mesh=new Mesh{name=s.Id+" / open grating",indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(bars.ToArray());owned.Add(mesh);
            target.GetComponent<MeshFilter>().sharedMesh=mesh;target.GetComponent<Renderer>().sharedMaterial=trim;
        }
        void Window(ArenaSolid s,Transform target)
        {
            bool alongX=s.Size.x>s.Size.z;float length=alongX?s.Size.x:s.Size.z;
            var glass=new Material(Shader.Find("StarTournament/OrbitalGlass")){name="Transparent panoramic glazing"};owned.Add(glass);
            var tint=Palette("cyan");tint.a=p.Get("ring.glassOpacity");glass.SetColor("_Color",tint);glass.SetFloat("_EdgeOpacity",p.Get("ring.glassEdgeOpacity"));
            // A single plane avoids tinting both faces of a box; the original BoxCollider remains sealed.
            var pane=new Mesh{name=s.Id+" / transparent pane"};owned.Add(pane);
            pane.vertices=alongX?new[]{new Vector3(-.5f,-.5f,0),new Vector3(.5f,-.5f,0),new Vector3(.5f,.5f,0),new Vector3(-.5f,.5f,0)}:
                new[]{new Vector3(0,-.5f,-.5f),new Vector3(0,-.5f,.5f),new Vector3(0,.5f,.5f),new Vector3(0,.5f,-.5f)};
            pane.triangles=new[]{0,1,2,0,2,3};pane.RecalculateNormals();pane.RecalculateBounds();
            target.GetComponent<MeshFilter>().sharedMesh=pane;var renderer=target.GetComponent<Renderer>();renderer.sharedMaterial=glass;renderer.shadowCastingMode=ShadowCastingMode.Off;
            float frame=p.Get("ring.windowFrameWidth"),depth=alongX?s.Size.z:s.Size.x;
            foreach(int side in new[]{-1,1})
            {
                Box(trim,s.Position+(alongX?Vector3.right:Vector3.forward)*(side*(length-frame)/2),alongX?new Vector3(frame,s.Size.y,depth+Epsilon):new Vector3(depth+Epsilon,s.Size.y,frame),Quaternion.identity);
                Box(trim,s.Position+Vector3.up*(side*(s.Size.y-frame)/2),alongX?new Vector3(length,frame,depth+Epsilon):new Vector3(depth+Epsilon,frame,length),Quaternion.identity);
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
            var normal=s.Rotation*(xWall?Vector3.forward:Vector3.right);
            float depth=xWall?s.Size.z:s.Size.x;float bottom=s.Position.y-s.Size.y/2;
            foreach(int side in new[]{-1,1})
            {
                Vector3 face=s.Position+normal*(side*(depth/2+Epsilon));
                void Strip(Material m,float y,float h,float len)
                {
                    // Sills and headers only carry detail inside their opaque silhouette.
                    if(y-h/2<bottom || y+h/2>bottom+s.Size.y)return;
                    if(!ExposedAt(s,new Vector3(face.x,y,face.z),normal*side))return;
                    Box(m,new Vector3(face.x,y,face.z)+normal*(side*Epsilon*(m==trim?0:m==white?2:1)),xWall?new Vector3(len,h,Epsilon):new Vector3(Epsilon,h,len),s.Rotation);
                }
                Strip(trim,bottom+p.Get("detail.trimHeight")/2,p.Get("detail.trimHeight"),length);
                if(HasWayfinding && Route(s.Position)!="central")
                {
                    // Repeat at eye level of both routes, clipped to each opaque wall piece.
                    foreach(float level in new[]{p.Get("wayfinding.lowerBandY"),p.Get("wayfinding.upperBandY")})
                    {
                        float h=p.Get("wayfinding.bandWidth");
                        if(level-h/2>=bottom && level+h/2<=bottom+s.Size.y && ExposedAt(s,new Vector3(face.x,level,face.z),normal*side))
                            AccentBox(new Vector3(face.x,level,face.z)+normal*(side*Epsilon),xWall?new Vector3(length,h,Epsilon):new Vector3(Epsilon,h,length),s.Rotation);
                    }
                }
                else Strip(cyan,bottom+p.Get("detail.bandHeight"),p.Get("detail.bandWidth"),length);
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
                    AccentBox(pos+direction*Epsilon,Mathf.Abs(direction.x)>.5f?new Vector3(Epsilon,p.Get("detail.columnStripHeight"),p.Get("detail.columnStripWidth")):new Vector3(p.Get("detail.columnStripWidth"),p.Get("detail.columnStripHeight"),Epsilon),Quaternion.identity);
                }
            }
        }
        void Floor(ArenaSolid s)
        {
            float inset=p.Get("detail.inset"),w=p.Get("detail.lineWidth");
            Vector3 top=s.Position+s.Rotation*Vector3.up*(s.Size.y/2+Epsilon);
            if(s.Id.EndsWith("-diagonal",StringComparison.Ordinal))top+=Vector3.up*2*Epsilon;
            if(!ExposedAt(s,top,s.Rotation*Vector3.up))return;
            // Stair nosings emphasize actual existing edges, never fake steps.
            if(s.Id.Contains("-step-"))
            {
                bool xStep=s.Size.x<s.Size.z;
                AccentBox(top,xStep?new Vector3(w,Epsilon,s.Size.z*.85f):new Vector3(s.Size.x*.85f,Epsilon,w),s.Rotation);return;
            }
            if(s.Size.x<inset*2||s.Size.z<inset*2)return;
            foreach(int side in new[]{-1,1})
            {
                AccentBox(top+s.Rotation*new Vector3(side*(s.Size.x/2-inset),0,0),new Vector3(w,Epsilon,s.Size.z-inset*2),s.Rotation);
                AccentBox(top+s.Rotation*new Vector3(0,0,side*(s.Size.z/2-inset)),new Vector3(s.Size.x-inset*2,Epsilon,w),s.Rotation);
            }
        }
        void Roof(ArenaSolid s)
        {
            if(s.Id=="interior-shell-roof")return; // The shell closes gaps; existing room ceilings own fixtures.
            if(Mathf.Min(s.Size.x,s.Size.z)<p.Get("light.fixtureWidth")+p.Get("light.fixtureFrame"))return;
            float spacing=p.Get("light.fixtureSpacing");int nx=Mathf.Max(1,Mathf.FloorToInt(s.Size.x/spacing)),nz=Mathf.Max(1,Mathf.FloorToInt(s.Size.z/spacing));
            for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)
            {
                var pos=s.Position+s.Rotation*new Vector3((x+.5f)*s.Size.x/nx-s.Size.x/2,-s.Size.y/2-Epsilon,(z+.5f)*s.Size.z/nz-s.Size.z/2);
                if(!ExposedAt(s,pos,-(s.Rotation*Vector3.up)))continue;
                CeilingFixture(pos,s.Rotation*(s.Size.x>s.Size.z?Quaternion.Euler(0,90,0):Quaternion.identity));
            }
        }
        bool ExposedAt(ArenaSolid solid,Vector3 point,Vector3 normal)
        {
            if(Quaternion.Angle(solid.Rotation,Quaternion.identity)>.01f)return true;
            int axis=Mathf.Abs(normal.x)>.5f?0:Mathf.Abs(normal.y)>.5f?1:2;
            int sign=normal[axis]>0?1:-1;
            float plane=solid.Position[axis]+sign*solid.Size[axis]*.5f;
            const float tolerance=.0001f;
            for(int i=0;i<structuralSolids.Length;i++)
            {
                if(i==structuralIndex)continue;var other=structuralSolids[i];
                if(other.Surface=="window"||other.Surface=="grating"||Quaternion.Angle(other.Rotation,Quaternion.identity)>.01f)continue;
                float near=other.Position[axis]-other.Size[axis]*.5f,far=other.Position[axis]+other.Size[axis]*.5f;
                if(near>plane+tolerance||far<plane-tolerance)continue;
                bool outside=sign>0?far>plane+tolerance:near<plane-tolerance;
                bool sameOuter=sign>0?Mathf.Abs(far-plane)<=tolerance:Mathf.Abs(near-plane)<=tolerance;
                if(!outside&&!(sameOuter&&i>structuralIndex))continue;
                bool covered=true;
                for(int a=0;a<3;a++)if(a!=axis &&
                    (point[a]<other.Position[a]-other.Size[a]*.5f-tolerance || point[a]>other.Position[a]+other.Size[a]*.5f+tolerance))covered=false;
                if(covered)return false;
            }
            return true;
        }
        void CeilingFixture(Vector3 pos,Quaternion rotation)
        {
            fixturePositions.Add(pos);
            float w=p.Get("light.fixtureWidth"),len=p.Get("light.fixtureLength");
            Box(trim,pos,new Vector3(w+p.Get("light.fixtureFrame"),p.Get("light.fixtureDepth"),len+p.Get("light.fixtureFrame")),rotation);
            Box(white,pos-rotation*Vector3.up*.05f,new Vector3(w,.025f,len),rotation);
            foreach(int side in new[]{-1,1})
            {
                var offset=rotation*new Vector3(side*(w/2+.25f),0,0);
                Box(trim,pos+offset,new Vector3(.18f,.03f,len),rotation);
                for(int i=0;i<12;i++)Box(wall,pos+offset+rotation*new Vector3(0,-.02f,(i+.5f)*len/12-len/2),new Vector3(.15f,.02f,.025f),rotation);
            }
        }
        void PickupMarkings(ArenaDefinition definition)
        {
            float width=p.Get("detail.lineWidth"),radius=p.Get("detail.ringRadius");
            // Permanent presentation follows authored anchors, independently of pickup availability.
            foreach(var pickup in definition.Pickups ?? Array.Empty<ArenaPickupDefinition>())
            {
                var center=pickup.Anchor+Vector3.up*(Epsilon*2);
                // Sixty-four segments are a fixed mesh tessellation invariant, not a balance value.
                for(int i=0;i<64;i++)
                {
                    float a=i*Mathf.PI*2/64,b=(i+1)*Mathf.PI*2/64;
                    var start=center+new Vector3(Mathf.Sin(a)*radius,0,Mathf.Cos(a)*radius);
                    var end=center+new Vector3(Mathf.Sin(b)*radius,0,Mathf.Cos(b)*radius);
                    Box(white,(start+end)/2,new Vector3(width,Epsilon,Vector3.Distance(start,end)),Quaternion.LookRotation(end-start));
                }
            }
        }
        void Label(string text,Vector3 position,Quaternion rotation,float scale=1,Color? color=null)
        {
            var go=new GameObject("Sector / "+text);go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation*Quaternion.Euler(0,180,0);
            var label=go.AddComponent<TextMesh>();label.text=text;label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.fontSize=64;label.characterSize=p.Get("detail.labelSize")*scale;label.anchor=TextAnchor.MiddleCenter;label.alignment=TextAlignment.Center;label.color=color??(HasWayfinding?RouteColor(position):Palette("ink"));
            var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=sign;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
        }
        void Labels()
        {
            // The original hall title is now part of the large, wall-mounted scoreboard.
            // F2 portal headers are drawn by VeteranSlice; the old low label had no backing.
            Label("B1\nARMOR\nCORRIDOR",Position("layout.label-2"),Quaternion.Euler(0,90,0),.7f);
            Label("B1\nLOWER\nTRANSIT",Position("layout.label-3"),Quaternion.Euler(0,-90,0),.7f);
            // Spawn numbers use the large plaques below; avoid duplicate lettering behind them.
            Label("NORTH  /  02",Position("layout.label-8"),Quaternion.Euler(0,180,0));
            Label("SOUTH  /  04",Position("layout.label-9"),Quaternion.identity);
            Label("WEST  /  03",Position("layout.label-10"),Quaternion.Euler(0,90,0));
            Label("EAST  /  01",Position("layout.label-11"),Quaternion.Euler(0,-90,0));
        }
        void BroadcastDetails()
        {
            // All panels sit on continuous walls. They neither punch openings nor own gameplay state.
            Screen("ORBITAL\nLEAGUE",Position("layout.broadcast-screen-0"),Quaternion.Euler(0,-90,0),0);
            Screen("WEST  /  03",Position("layout.broadcast-screen-1"),Quaternion.Euler(0,-90,0),1);
            Screen("NORTH  /  02",Position("layout.broadcast-screen-2"),Quaternion.identity,2);
            Screen("NORTH  /  02",Position("layout.broadcast-screen-3"),Quaternion.identity,3);
            // Emblem hangs within the hall ceiling silhouette, above players and the pickup.
            Crest(Position("layout.broadcast-crest"),Quaternion.Euler(90,0,0),p.Get("broadcast.orbitRadius")*1.5f);
            foreach(int sx in new[]{-1,1})foreach(int sz in new[]{-1,1})
            {
                int number=sz>0?(sx<0?1:2):(sx<0?3:4);
                var mark=Position("ring.spawn-mark-"+number);float x=mark.x,z=mark.z;
                float markHeight=p.Get("broadcast.spawnMarkHeight");
                Box(trim,new Vector3(x,mark.y,z),new Vector3(Epsilon,markHeight,markHeight*1.15f),Quaternion.identity);
                AccentBox(new Vector3(x-sx*Epsilon*2,mark.y,z-markHeight*.45f),new Vector3(Epsilon,markHeight*.87f,p.Get("broadcast.markWidth")),Quaternion.identity);
                Label("0"+number,new Vector3(x-sx*Epsilon*3,mark.y,z),Quaternion.Euler(0,sx<0?90:-90,0),.72f);
            }
            foreach(int sx in new[]{-1,1})
            {
                // Mount the rise title on the existing opaque side wall. A title over the
                // ramp centre has no backing and appears to float when seen from the hall.
                var wallSolid=structuralSolids.First(s=>s.Id==(sx<0?"west-rise-south":"east-rise-north"));
                Vector3 outward=sx<0?Vector3.forward:Vector3.back;
                var rotation=Quaternion.Euler(0,sx<0?0:180,0);
                float width=p.Get("wayfinding.panelWidth"),height=p.Get("wayfinding.panelHeight");
                float y=p.Get("wayfinding.upperBandY")+p.Get("wayfinding.bandWidth")/2+height/2;
                var panel=new Vector3(wallSolid.Position.x,y,wallSolid.Position.z)+outward*(wallSolid.Size.z/2+Epsilon*2);
                Box(trim,panel,new Vector3(width,height,Epsilon),rotation);
                AccentBox(panel+outward*Epsilon*2+Vector3.down*(height/2-p.Get("wayfinding.panelStripe")/2),
                    new Vector3(width,p.Get("wayfinding.panelStripe"),Epsilon),rotation);
                // Preserve the authored rise title size within its new wall backing.
                Label(sx<0?"WEST RISE / 01":"EAST RISE / 02",panel+outward*Epsilon*4,rotation,.68f);
            }
            for(int i=0;i<2;i++)BasementVent(Position("layout.broadcast-fan-"+i),Quaternion.Euler(0,-90,0));
            foreach(float z in new[]{-6f,0f,6f})
            {
                Box(trim,new Vector3(2.59f,.4f,z),new Vector3(Epsilon,p.Get("broadcast.servicePanelHeight"),1.4f),Quaternion.identity);
                AccentBox(new Vector3(2.57f,.4f,z),new Vector3(Epsilon,p.Get("broadcast.markWidth"),1.2f),Quaternion.identity);
            }
        }
        void Screen(string title,Vector3 position,Quaternion rotation,int motif)
        {
            float w=p.Get("broadcast.screenWidth"),h=p.Get("broadcast.screenHeight"),r=p.Get("broadcast.orbitRadius");
            var face=position+rotation*Vector3.back*.035f;
            Box(trim,position,new Vector3(w+.18f,h+.18f,.04f),rotation);
            Box(floor,face,new Vector3(w,h,Epsilon),rotation);
            // Static horizon and station sectors make the panels read as an orbital broadcast graphic.
            AccentBox(face+rotation*new Vector3(0,-h*.28f,-.012f),new Vector3(w*.86f,p.Get("broadcast.markWidth"),Epsilon),rotation);
            for(int i=0;i<3;i++)Box(white,face+rotation*new Vector3(-w*.39f+i*.18f,-h*.38f,-.014f),new Vector3(.07f,.08f,Epsilon),rotation);
            // Keep the title field clear; the lower route band carries the sector colour.
            var orbitCenter=face+rotation*new Vector3(-w*.26f,.05f,-.018f);
            for(int i=0;i<18;i++)
            {
                float x0=-w*.44f+i*w*.047f,x1=x0+w*.047f;
                float y0=-h*.12f+.13f*Mathf.Sin((x0+w*.44f)*Mathf.PI/w),y1=-h*.12f+.13f*Mathf.Sin((x1+w*.44f)*Mathf.PI/w);
                Vector3 a=new Vector3(x0,y0,-.013f),b=new Vector3(x1,y1,-.013f);
                Box(media,face+rotation*((a+b)*.5f),new Vector3(Vector3.Distance(a,b),p.Get("broadcast.markWidth")*.55f,Epsilon),rotation*Quaternion.Euler(0,0,Mathf.Atan2(y1-y0,x1-x0)*Mathf.Rad2Deg));
            }
            Crest(orbitCenter,rotation,r*(motif==0?1.15f:.8f));
            Label(title,face+rotation*new Vector3(w*.12f,.12f,-.025f),rotation==Quaternion.identity?Quaternion.Euler(0,180,0):Quaternion.Euler(0,90,0),motif==0?.65f:.63f);
            if(motif>0)Label("SECTOR 0"+(motif==1?3:2),face+rotation*new Vector3(w*.13f,-.25f,-.027f),rotation==Quaternion.identity?Quaternion.Euler(0,180,0):Quaternion.Euler(0,90,0),.48f);
        }
        void Crest(Vector3 position,Quaternion rotation,float radius)
        {
            var shapes=new List<CombineInstance>();
            for(int i=0;i<24;i++)
            {
                float a=i*Mathf.PI*2/24;
                shapes.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(new Vector3(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,0),Quaternion.Euler(0,0,a*Mathf.Rad2Deg+90),new Vector3(radius*.35f,p.Get("broadcast.markWidth"),Epsilon))});
            }
            var go=new GameObject("Slow orbital emblem");go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            var mesh=new Mesh{name="League orbit ring"};mesh.CombineMeshes(shapes.ToArray());owned.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=RouteMaterial(position);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            orbits.Add(go.transform);
            Box(white,position+rotation*Vector3.back*.01f,new Vector3(radius*.36f,radius*.36f,Epsilon),rotation*Quaternion.Euler(0,0,45));
        }
        void BasementVent(Vector3 position,Quaternion rotation)
        {
            float r=p.Get("broadcast.ventRadius");
            Box(trim,position,new Vector3(r*2.6f,r*2.6f,Epsilon),rotation);
            var shapes=new List<CombineInstance>();
            for(int i=0;i<4;i++)shapes.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(Vector3.zero,Quaternion.Euler(0,0,i*90),new Vector3(r*1.6f,p.Get("broadcast.markWidth"),Epsilon))});
            var go=new GameObject("Enclosed slow fan");go.transform.SetParent(transform,false);go.transform.localPosition=position+rotation*Vector3.back*.012f;go.transform.localRotation=rotation;
            var mesh=new Mesh{name="Fan rotor"};mesh.CombineMeshes(shapes.ToArray());owned.Add(mesh);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=media;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;fans.Add(go.transform);
            for(int i=-2;i<=2;i++)Box(trim,position+rotation*new Vector3(0,i*r*.38f,-.026f),new Vector3(r*2.25f,p.Get("broadcast.markWidth")*.55f,Epsilon),rotation);
        }
        void Update()
        {
            if(p==null)return;
            float dt=Time.unscaledDeltaTime;
            foreach(var t in orbits)if(t)t.Rotate(0,0,p.Get("broadcast.orbitSpeed")*dt,Space.Self);
            foreach(var t in fans)if(t)t.Rotate(0,0,p.Get("broadcast.fanSpeed")*dt,Space.Self);
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
        // Render-only union of axis-aligned authored boxes. Subtract covered rectangles
        // directly instead of crossing every X/Y boundary, which would create a dense grid.
        // Later solids own equal outer planes, so repeated slabs have one stable owner.
        static Mesh VisibleSolidMesh(ArenaSolid solid,ArenaSolid[] all,int index)
        {
            if(Quaternion.Angle(solid.Rotation,Quaternion.identity)>.01f)return null;
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            const float tolerance=.0001f; // Floating-point comparison of authored faces, not a game dimension.
            float Min(ArenaSolid s,int axis)=>s.Position[axis]-s.Size[axis]*.5f;
            float Max(ArenaSolid s,int axis)=>s.Position[axis]+s.Size[axis]*.5f;
            for(int face=0;face<6;face++)
            {
                int axis=face/2,sign=face%2==0?1:-1;
                int u,v;
                if(axis==0){u=sign>0?1:2;v=sign>0?2:1;}
                else if(axis==1){u=sign>0?2:0;v=sign>0?0:2;}
                else {u=sign>0?0:1;v=sign>0?1:0;}
                float plane=sign>0?Max(solid,axis):Min(solid,axis);
                float uMin=Min(solid,u),uMax=Max(solid,u),vMin=Min(solid,v),vMax=Max(solid,v);
                var visible=new List<Rect>{Rect.MinMaxRect(uMin,vMin,uMax,vMax)};
                for(int j=0;j<all.Length;j++)
                {
                    if(j==index)continue;var other=all[j];
                    if(other.Surface=="window"||other.Surface=="grating"||Quaternion.Angle(other.Rotation,Quaternion.identity)>.01f)continue;
                    float near=Min(other,axis),far=Max(other,axis);
                    if(near>plane+tolerance||far<plane-tolerance)continue;
                    bool outside=sign>0?far>plane+tolerance:near<plane-tolerance;
                    bool sameOuter=sign>0?Mathf.Abs(far-plane)<=tolerance:Mathf.Abs(near-plane)<=tolerance;
                    if(!outside && !(sameOuter&&j>index))continue;
                    float left=Mathf.Max(uMin,Min(other,u)),right=Mathf.Min(uMax,Max(other,u));
                    float bottom=Mathf.Max(vMin,Min(other,v)),top=Mathf.Min(vMax,Max(other,v));
                    if(right-left<=tolerance||top-bottom<=tolerance)continue;
                    var remainder=new List<Rect>();
                    foreach(var region in visible)
                    {
                        float a=Mathf.Max(region.xMin,left),b=Mathf.Min(region.xMax,right);
                        float c=Mathf.Max(region.yMin,bottom),d=Mathf.Min(region.yMax,top);
                        if(b-a<=tolerance||d-c<=tolerance){remainder.Add(region);continue;}
                        if(a-region.xMin>tolerance)remainder.Add(Rect.MinMaxRect(region.xMin,region.yMin,a,region.yMax));
                        if(region.xMax-b>tolerance)remainder.Add(Rect.MinMaxRect(b,region.yMin,region.xMax,region.yMax));
                        if(c-region.yMin>tolerance)remainder.Add(Rect.MinMaxRect(a,region.yMin,b,c));
                        if(region.yMax-d>tolerance)remainder.Add(Rect.MinMaxRect(a,d,b,region.yMax));
                    }
                    visible=remainder;
                    if(visible.Count==0)break;
                }
                var normal=Vector3.zero;normal[axis]=sign;
                foreach(var region in visible)
                {
                    int start=vertices.Count;
                    foreach(var point in new[]{new Vector2(region.xMin,region.yMin),new Vector2(region.xMax,region.yMin),new Vector2(region.xMax,region.yMax),new Vector2(region.xMin,region.yMax)})
                    {
                        var world=Vector3.zero;world[axis]=plane;world[u]=point.x;world[v]=point.y;
                        vertices.Add(new Vector3((world.x-solid.Position.x)/solid.Size.x,(world.y-solid.Position.y)/solid.Size.y,(world.z-solid.Position.z)/solid.Size.z));
                        normals.Add(normal);uv.Add(Vector2.zero);
                    }
                    triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
            }
            var mesh=new Mesh{name=solid.Id+" / exposed faces",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        void RefreshFont(Font font){if(font==signFont&&sign)sign.SetTexture("_MainTex",font.material.mainTexture);}
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldMode;foreach(var asset in owned)if(asset)DestroyImmediate(asset);}
    }
}
