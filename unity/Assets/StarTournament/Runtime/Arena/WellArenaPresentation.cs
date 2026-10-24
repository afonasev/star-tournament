using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Bright white laboratory skin. All decoration is collider-free; authored solids own obstruction.</summary>
    public sealed partial class WellArenaPresentation:MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();readonly Dictionary<Material,List<Bounds>> batches=new Dictionary<Material,List<Bounds>>();
        UnityEngine.Rendering.AmbientMode oldMode;Color oldAmbient;
        Material fontMaterial;Font signFont;
        Material white,floor,ceiling,metal,glass,door,soil;Material[] storeyWalls,storeyFloors,storeyPanels;
        readonly HashSet<Material> architecture=new HashSet<Material>();WellLabDetail detail;Material[] colours;ProvingProfile profile;
        public ProvingProfile Settings=>profile;
        Material Mat(string name,Color colour,bool unlit=false,VisualMaterialRole role=VisualMaterialRole.Wall,bool register=true)
        {var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,color=colour};m.SetFloat("_Smoothness",.35f);owned.Add(m);if(!unlit&&register)NativeVisualMaterials.Register(this,m,role);return m;}
        void Texture(Material material,string kind,float metres)
        {
            var texture=WellLabDetail.Texture(kind,profile);owned.Add(texture);material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",Vector2.one/metres);
            var relief=WellLabDetail.Texture(kind,profile,true);owned.Add(relief);material.SetTexture("_BumpMap",relief);material.SetTextureScale("_BumpMap",Vector2.one/metres);material.EnableKeyword("_NORMALMAP");
        }
        void Box(Material m,Vector3 centre,Vector3 size)
        {if(!batches.TryGetValue(m,out var b)){b=new List<Bounds>();batches[m]=b;}b.Add(new Bounds(centre,size));}
        void Label(string value,Vector3 position,Quaternion rotation,Color colour,Vector2 available,float size=.5f)
        {
            var go=new GameObject("Well sign: "+value);go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            size*=profile.Get("well.signScale");var text=go.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=96;text.fontStyle=value=="КОЛОДЕЦ"||value=="01"||value=="02"||value=="03"?FontStyle.Bold:FontStyle.Normal;text.characterSize=size/8;text.color=colour;
            if(!fontMaterial){signFont=text.font;fontMaterial=new Material(Shader.Find("StarTournament/LunarSign"));fontMaterial.mainTexture=signFont.material.mainTexture;owned.Add(fontMaterial);Font.textureRebuilt+=RefreshFont;}
            var renderer=text.GetComponent<MeshRenderer>();renderer.sharedMaterial=fontMaterial;var bounds=renderer.localBounds;
            // Fit the actual glyph bounds, including accents and both lines, to the clear wall face.
            // signScale remains a preferred size; it cannot push lettering through a corner or portal.
            float fit=Mathf.Min(1,available.x/Mathf.Max(bounds.size.x,.0001f),available.y/Mathf.Max(bounds.size.y,.0001f));
            go.transform.localScale=Vector3.one*fit;go.transform.localPosition=position-rotation*(bounds.center*fit);
        }
        void HeaderLabel(string value,ArenaSolid wall,ArenaDefinition definition,Quaternion rotation,Color colour,float size)
        {
            var normal=rotation*Vector3.back;bool alongX=wall.Size.x>wall.Size.z;
            var face=wall.Position+normal*((alongX?wall.Size.z:wall.Size.x)/2+.02f);
            face.y=wall.Position.y-wall.Size.y/2+.75f;
            int along=alongX?0:2,across=alongX?2:0;
            float lo=wall.Position[along]-wall.Size[along]/2,hi=wall.Position[along]+wall.Size[along]/2;
            foreach(var other in definition.Solids.Where(s=>s.Material=="wall"&&s.Size[along]<=.31f&&s.Size[across]>.31f))
            {
                var b=new Bounds(other.Position,other.Size);
                if(face[across]<b.min[across]||face[across]>b.max[across]||face.y<b.min.y||face.y>b.max.y)continue;
                if(b.max[along]<face[along])lo=Mathf.Max(lo,b.max[along]);
                else if(b.min[along]>face[along])hi=Mathf.Min(hi,b.min[along]);
            }
            face[along]=(lo+hi)/2;
            Label(value,face,rotation,colour,new Vector2(hi-lo-.6f,.9f),size);
        }
        void AtriumLabel(string value,ArenaDefinition definition,int floor,int side,Color colour,float size)
        {
            // Merge adjacent opaque caps, keeping every authored bridge opening as a real gap.
            var spans=new List<Vector2>();
            foreach(var cap in definition.Solids.Where(s=>s.Id.StartsWith("f"+floor+"-cap-1"+side)).OrderBy(s=>s.Position.x))
            {
                float lo=cap.Position.x-cap.Size.x/2,hi=cap.Position.x+cap.Size.x/2;
                if(spans.Count>0&&lo<=spans[spans.Count-1].y+.001f)
                {var previous=spans[spans.Count-1];spans[spans.Count-1]=new Vector2(previous.x,hi);}
                else spans.Add(new Vector2(lo,hi));
            }
            var span=spans.OrderByDescending(s=>s.y-s.x).ThenBy(s=>Mathf.Abs(s.x+s.y)).First();
            // The high clear band stays above facade ventilation and below the ceiling.
            Label(value,new Vector3((span.x+span.y)/2,floor*WellArenaCatalog.Storey+5.75f,side*(WellArenaCatalog.Pit-.14f)),
                Quaternion.Euler(0,side>0?0:180,0),colour,new Vector2(span.y-span.x-.6f,.8f),size);
        }
        void Navigation(int f,Color colour)
        {
            float y=f*WellArenaCatalog.Storey,x=f==1?19:11;
            void Route(float cx,float cz,float width,float depth)=>Box(colours[f],new Vector3(cx,y+.008f,cz),new Vector3(width,.008f,depth));
            // Follow the actual inner/outer gallery layout; never paint a route through rooms or furniture.
            foreach(int side in new[]{-1,1})
            {
                Route(0,side*12.5f,x*2+.24f,.24f);Route(side*x,0,.24f,24.76f);
                Route(0,side*18.2f,.24f,11.16f);Route(side*(x+24)/2,0,24-x,.24f);
                Label("0"+(f+1),new Vector3(side*2.3f,y+.018f,side*18.5f),
                    Quaternion.LookRotation(Vector3.down,Vector3.forward*side),colour,new Vector2(3,2.4f),2.2f);
            }
        }
        void StoreyMaterials()
        {
            storeyWalls=new Material[3];storeyFloors=new Material[3];storeyPanels=new Material[3];
            var wallColours=new[]{new Color(.96f,.92f,.81f),new Color(.87f,.92f,.96f),new Color(.91f,.88f,.81f)};
            var floorColours=new[]{new Color(.77f,.73f,.62f),new Color(.62f,.69f,.74f),new Color(.33f,.34f,.32f)};
            var panelColours=new[]{new Color(.44f,.55f,.43f),new Color(.13f,.30f,.44f),new Color(.30f,.31f,.29f)};
            for(int f=0;f<3;f++)
            {
                Material Variant(Material source,string name,Color colour)
                {
                    var m=new Material(source){name=name,color=colour};owned.Add(m);architecture.Add(m);NativeVisualMaterials.Register(this,m,source==floor?VisualMaterialRole.Floor:VisualMaterialRole.Wall);return m;
                }
                storeyWalls[f]=Variant(white,"Well storey "+f+" upper wall",wallColours[f]);
                storeyPanels[f]=Variant(white,"Well storey "+f+" lower panel",panelColours[f]);
                storeyFloors[f]=Variant(floor,"Well storey "+f+" floor",floorColours[f]);
                Texture(storeyFloors[f],new[]{"floor-bio","floor-physics","floor-electronics"}[f],profile.Get("well.panelSize"));
            }
        }
        void WallBox(ArenaSolid solid,Bounds skin,Material fallback)
        {
            if(!solid.Id.StartsWith("f")||solid.Material!="wall"){Box(fallback,skin.center,skin.size);return;}
            int f=Mathf.Clamp(Mathf.FloorToInt(solid.Position.y/WellArenaCatalog.Storey),0,2);
            float split=Mathf.Clamp(f*WellArenaCatalog.Storey+1.3f,skin.min.y,skin.max.y);
            void Part(float lo,float hi,Material m)
            {if(hi-lo>.0001f)Box(m,new Vector3(skin.center.x,(lo+hi)/2,skin.center.z),new Vector3(skin.size.x,hi-lo,skin.size.z));}
            // Partition the exact existing volume; no second skins competing on the wall plane.
            Part(skin.min.y,split,storeyPanels[f]);Part(split,skin.max.y,storeyWalls[f]);
        }
        public static Bounds WallSkin(ArenaSolid solid,IEnumerable<ArenaSolid> solids)
        {
            var result=new Bounds(solid.Position,solid.Size);
            if(solid.Material!="wall")return result;
            int axis=solid.Size.x>solid.Size.z?0:2,across=axis==0?2:0;
            if(solid.Size[across]>.31f)return result;
            var min=result.min;var max=result.max;
            foreach(var other in solids)
            {
                if(other==solid||other.Material!="wall"||other.Size[axis]>.31f||other.Size[across]<=.31f)continue;
                var b=new Bounds(other.Position,other.Size);
                if(b.max.y<=result.min.y+.0001f||b.min.y>=result.max.y-.0001f||solid.Position[across]<b.min[across]-.0001f||solid.Position[across]>b.max[across]+.0001f)continue;
                if(Mathf.Abs(result.min[axis]-other.Position[axis])<.0001f)min[axis]=Mathf.Min(min[axis],b.min[axis]);
                if(Mathf.Abs(result.max[axis]-other.Position[axis])<.0001f)max[axis]=Mathf.Max(max[axis],b.max[axis]);
            }
            // The shaft floor is 30 cm below the terminal; keep its perimeter closed down to that floor.
            if(solid.Id.StartsWith("shaft-"))min.y=-.3f;
            result.SetMinMax(min,max);return result;
        }
        void RefreshFont(Font font){if(font==signFont&&fontMaterial)fontMaterial.mainTexture=font.material.mainTexture;}
        public static void ConfigureDaylight(Light light,ProvingProfile profile)
        {
            light.type=LightType.Directional;light.intensity=profile.Get("well.daylight");
            light.transform.rotation=profile.Version>=5?Quaternion.Euler(profile.Get("well.sunPitch"),profile.Get("well.sunYaw"),0):Quaternion.Euler(50,-30,0);
            light.color=profile.Version>=5?new Color(profile.Get("well.sunRed"),profile.Get("well.sunGreen"),profile.Get("well.sunBlue")):Color.white;
            light.shadowStrength=profile.Get("well.daylightShadow");light.shadows=light.shadowStrength>0?LightShadows.Soft:LightShadows.None;
            // Sub-centimetre contact offset avoids floating trims; fixed shadow-raster precision.
            light.shadowBias=.03f;light.shadowNormalBias=.12f;
        }
        public void Build(ArenaDefinition definition,ProvingProfile profile)
        {
            this.profile=profile;oldMode=RenderSettings.ambientMode;oldAmbient=RenderSettings.ambientLight;
            white=Mat("Well ceramic white",new Color(.95f,.95f,.93f));floor=Mat("Well pale floor",new Color(.71f,.73f,.71f),role:VisualMaterialRole.Floor);metal=Mat("Well brushed metal",new Color(.64f,.67f,.67f),role:VisualMaterialRole.Metal);metal.SetFloat("_Metallic",.35f);
            ceiling=Mat("Well technical ceiling",new Color(.86f,.89f,.89f));
            door=Mat("Closed office doors",new Color(.64f,.67f,.65f));soil=Mat("Garden soil",new Color(.23f,.15f,.08f));
            white.shader=Shader.Find("StarTournament/WellContactSurface");floor.shader=white.shader;ceiling.shader=white.shader;ceiling.SetFloat("_OcclusionStrength",profile.Get("well.contactShade"));white.SetFloat("_OcclusionStrength",profile.Get("well.contactShade"));floor.SetFloat("_OcclusionStrength",profile.Get("well.contactShade"));
            Texture(white,"ceramic",profile.Get("well.panelSize"));Texture(floor,"floor",profile.Get("well.panelSize"));Texture(ceiling,"ceiling",profile.Get("well.panelSize"));Texture(metal,"metal",1);Texture(soil,"soil",4);
            white.SetFloat("_Smoothness",profile.Get("well.surfaceSmoothness"));floor.SetFloat("_Smoothness",profile.Get("well.floorSmoothness"));ceiling.SetFloat("_Smoothness",profile.Get("well.floorSmoothness"));soil.SetFloat("_Smoothness",0);
            architecture.Add(white);architecture.Add(floor);StoreyMaterials();
            var diffuser=Mat("Well warm diffuser",new Color(1,.94f,.80f)*profile.Get("well.fixtureGlow"),true);
            var screen=Mat("Well instrument glass",new Color(.035f,.12f,.16f),true);
            var indicator=Mat("Well instrument readout",new Color(.32f,.75f,.66f),true);
            detail=new WellLabDetail(transform,owned,profile,metal,door,screen,indicator);
            glass=Mat("Armoured glass",new Color(.72f,.90f,.94f,profile.Get("well.glassOpacity")),register:false);glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",1);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.EnableKeyword("_ALPHAPREMULTIPLY_ON");glass.renderQueue=3000;glass.SetFloat("_Smoothness",profile.Get("well.glassSmoothness"));glass.SetFloat("_Metallic",0);glass.SetFloat("_BlendModePreserveSpecular",1);glass.SetShaderPassEnabled("ShadowCaster",false);
            colours=new[]{Mat("01 bio green",new Color(.12f,.62f,.30f)),Mat("02 physics blue",new Color(.08f,.42f,.80f)),Mat("03 electronics yellow",new Color(.90f,.67f,.08f))};
            // Replace the generic physical meshes with material batches, keeping their exact colliders.
            foreach(var solid in definition.Solids)
            {
                var tr=transform.parent.Find(solid.Id);if(!tr)continue;tr.GetComponent<Renderer>().enabled=false;
                int storey=Mathf.Clamp(Mathf.RoundToInt((solid.Position.y+solid.Size.y/2)/WellArenaCatalog.Storey),0,2);
                Material m=solid.Material=="glass"?glass:solid.Material=="door"?door:solid.Material=="floor"?(solid.Id=="garden"?soil:solid.Id.StartsWith("f")?storeyFloors[storey]:floor):white;
                if(WellFurniturePlan.IsFurniture(solid))
                {detail.Furniture(solid);continue;}
                if(solid.Surface=="well-grate")
                {
                    var rail=solid.Size;rail.x=.10f;
                    foreach(int edge in new[]{-1,1})Box(metal,solid.Position+Vector3.right*edge*(solid.Size.x/2-.05f),rail);
                    rail=solid.Size;rail.z=.10f;foreach(int edge in new[]{-1,1})Box(metal,solid.Position+Vector3.forward*edge*(solid.Size.z/2-.05f),rail);
                    // Collision is a movement-only deck; visible spaced bars leave firing sightlines through the mesh.
                    for(float x=solid.Position.x-solid.Size.x/2;x<solid.Position.x+solid.Size.x/2;x+=profile.Get("well.grateSpacing"))Box(metal,new Vector3(x,solid.Position.y,solid.Position.z),new Vector3(.055f,solid.Size.y,solid.Size.z));
                    for(float z=solid.Position.z-solid.Size.z/2;z<solid.Position.z+solid.Size.z/2;z+=profile.Get("well.grateSpacing"))Box(metal,new Vector3(solid.Position.x,solid.Position.y,z),new Vector3(solid.Size.x,solid.Size.y,.055f));
                }
                else
                {
                    var skin=WallSkin(solid,definition.Solids);WallBox(solid,skin,m);
                    // Floor plates end at the wall centreline. Continue the facade across each slab edge.
                    if(solid.Id.Contains("-outer-"))
                    {
                        var seam=skin;seam.center=new Vector3(seam.center.x,Mathf.Floor(solid.Position.y/WellArenaCatalog.Storey)*WellArenaCatalog.Storey+WellArenaCatalog.ClearHeight+WellArenaCatalog.Slab/2,seam.center.z);
                        seam.size=new Vector3(seam.size.x,WellArenaCatalog.Slab,seam.size.z);Box(storeyWalls[Mathf.Clamp(Mathf.FloorToInt(solid.Position.y/WellArenaCatalog.Storey),0,2)],seam.center,seam.size);
                    }
                }
                if(solid.Material=="wall"&&solid.Size.y>2)
                {
                    for(int f=0;f<3;f++)
                    {
                        float stripeY=f*WellArenaCatalog.Storey+1.1f;
                        if(stripeY<solid.Position.y-solid.Size.y/2||stripeY>solid.Position.y+solid.Size.y/2)continue;
                        var skin=WallSkin(solid,definition.Solids);var stripe=skin.size;stripe.y=.16f;stripe.x+=.012f;stripe.z+=.012f;Box(colours[f],new Vector3(skin.center.x,stripeY,skin.center.z),stripe);
                    }
                }
                detail.Wall(solid);detail.DoorHeaders(solid);detail.Facade(solid);
                if(solid.Material=="equipment")
                {Box(metal,solid.Position+Vector3.up*(solid.Size.y/2+.02f),new Vector3(solid.Size.x,.05f,solid.Size.z));Box(colours[Mathf.Clamp((int)(solid.Position.y/WellArenaCatalog.Storey),0,2)],solid.Position+Vector3.up*.35f,new Vector3(solid.Size.x+.01f,.12f,solid.Size.z+.01f));}
                if(solid.Material=="glass")detail.Window(solid);
                if(solid.Material=="door")
                {char side=solid.Id[solid.Id.Length-1];var normal=side=='N'?Vector3.forward:side=='S'?Vector3.back:side=='E'?Vector3.right:Vector3.left;Label("КАБИНЕТ\nЗАКРЫТ",solid.Position+normal*((Mathf.Abs(normal.x)>.5f?solid.Size.x:solid.Size.z)/2+.02f),Quaternion.LookRotation(-normal),Color.white,new Vector2((Mathf.Abs(normal.x)>.5f?solid.Size.z:solid.Size.x)-.3f,solid.Size.y-.3f),.25f);}
            }
            for(int f=0;f<3;f++)
            {
                string department=new[]{"БИОРАЗРАБОТКИ","ФИЗИКА","ЭЛЕКТРОНИКА"}[f];Color colour=colours[f].color;
                foreach(int side in new[]{-1,1})
                {
                    var rotation=Quaternion.Euler(0,side>0?0:180,0);
                    string title=f==0?"КОЛОДЕЦ":"0"+(f+1)+"  /  "+department;
                    AtriumLabel(title,definition,f,side,new Color(.17f,.21f,.22f),f==0?1.2f:.8f);
                    // Corridor-facing shell, strictly between the lift and the corner-room partition.
                    float lo=3.5f+.3f,hi=WellArenaCatalog.Pit-.15f-.3f;
                    Label("0"+(f+1)+"  /  "+department,new Vector3(-side*(lo+hi)/2,f*WellArenaCatalog.Storey+3.9f,side*(WellArenaCatalog.Edge-.17f)),
                        rotation,colour,new Vector2(hi-lo,.9f),.85f);
                    HeaderLabel("ЛИФТ  /  01 ↕ 03",definition.Solids.First(s=>s.Id=="f"+f+"-outer-z"+side+"-header"),definition,rotation,colour,.38f);
                    HeaderLabel("ЛЕСТНИЦА  /  0"+(f+1),definition.Solids.First(s=>s.Id=="f"+f+"-outer-x"+side+"-header"),definition,
                        Quaternion.Euler(0,side>0?90:-90,0),colour,.38f);
                }
                Navigation(f,colour);
                // Authored fixture locations: broad rings of soft overhead light, clear of all passages.
                foreach(float x in new[]{-18f,0,18f})foreach(float z in new[]{-18f,0,18f})
                {
                    if(x==0&&z==0)continue;var pos=new Vector3(x,f*WellArenaCatalog.Storey+WellArenaCatalog.ClearHeight-.2f,z);
                    Box(metal,pos,new Vector3(3,.10f,1));Box(diffuser,pos-Vector3.up*.07f,new Vector3(2.8f,.03f,.8f));
                    var go=new GameObject("Office ceiling light");go.transform.SetParent(transform,false);go.transform.localPosition=pos-Vector3.up*.15f;go.transform.localRotation=Quaternion.Euler(90,0,0);
                    var light=go.AddComponent<Light>();light.type=LightType.Spot;light.spotAngle=profile.Get("well.spotAngle");light.innerSpotAngle=Mathf.Min(light.spotAngle,profile.Get("well.innerAngle"));light.intensity=profile.Get("well.lamp");light.range=profile.Get("well.lampRange");light.color=profile.Version>=5?new Color(1,.97f,.9f):new Color(1,.98f,.93f);
                    // One existing corner-room fixture per storey: stable, camera-independent ownership.
                    // Three default high-tier 512px maps fit the existing 1024px atlas without resizing it.
                    bool shadow=profile.Version>=5&&z==18f&&x==18f;float strength=profile.Version>=5?profile.Get("well.lampShadow"):0;
                    light.shadows=shadow&&strength>0?LightShadows.Soft:LightShadows.None;
                    light.shadowStrength=strength;light.shadowBias=.025f;light.shadowNormalBias=.12f;
                }
            }
            // Low plant beds: no collider clutter. The central garden paths and landing area stay empty.
            foreach(var centre in new[]{new Vector3(-6,0,-6),new Vector3(6,0,6)})
            {
                Box(white,centre+Vector3.up*.15f,new Vector3(3,.3f,3));Box(soil,centre+Vector3.up*.31f,new Vector3(2.8f,.05f,2.8f));
                detail.Plants(centre);
            }
            BuildPickupMarkings(definition);
            detail.Garden();
            detail.Finish();
            var surfaceOwners=new List<Bounds>();
            foreach(var pair in batches)
            {
                var blockers=pair.Key==glass?null:batches.Where(p=>p.Key!=pair.Key&&p.Key!=glass).SelectMany(p=>p.Value).ToArray();
                var mesh=WellSurfaceMesh.Build(pair.Value,blockers,pair.Key==glass?null:surfaceOwners);owned.Add(mesh);
                if(pair.Key!=glass)surfaceOwners.AddRange(pair.Value);
                if(architecture.Contains(pair.Key))
                {
                    var contact=WellContactMesh.Build(mesh,definition.Solids.Where(s=>s.Material!="glass"&&s.Surface!="well-grate").Select(s=>new Bounds(s.Position,s.Size)).ToArray());owned.Add(contact);mesh=contact;
                }
                var go=new GameObject(pair.Key.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();if(architecture.Contains(pair.Key))
                {WellSurfaceMesh.SplitHorizontalMaterials(mesh);int floorIndex=System.Array.IndexOf(storeyFloors,pair.Key);
                    renderer.sharedMaterials=new[]{pair.Key,floorIndex>=0?storeyWalls[floorIndex]:pair.Key==floor?white:pair.Key,ceiling};}
                else renderer.sharedMaterial=pair.Key;
                if(pair.Key==glass)renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=(profile.Version>=5?new Color(profile.Get("well.fillRed"),profile.Get("well.fillGreen"),profile.Get("well.fillBlue")):new Color(1,.99f,.97f))*profile.Get("well.fill");
        }
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;foreach(var o in owned)if(o)Destroy(o);}
    }
}
