using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Bright white laboratory skin. All decoration is collider-free; authored solids own obstruction.</summary>
    public sealed class WellArenaPresentation:MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();readonly Dictionary<Material,List<Bounds>> batches=new Dictionary<Material,List<Bounds>>();
        UnityEngine.Rendering.AmbientMode oldMode;Color oldAmbient;
        Material fontMaterial;Font signFont;
        Material white,floor,ceiling,metal,glass,door,soil;WellLabDetail detail;Material[] colours;ProvingProfile profile;
        Material Mat(string name,Color colour,bool unlit=false,VisualMaterialRole role=VisualMaterialRole.Wall,bool register=true)
        {var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,color=colour};m.SetFloat("_Smoothness",.35f);owned.Add(m);if(!unlit&&register)NativeVisualMaterials.Register(this,m,role);return m;}
        void Texture(Material material,string kind,float metres)
        {
            var texture=WellLabDetail.Texture(kind,profile);owned.Add(texture);material.SetTexture("_BaseMap",texture);material.SetTextureScale("_BaseMap",Vector2.one/metres);
            var relief=WellLabDetail.Texture(kind,profile,true);owned.Add(relief);material.SetTexture("_BumpMap",relief);material.SetTextureScale("_BumpMap",Vector2.one/metres);material.EnableKeyword("_NORMALMAP");
        }
        void Box(Material m,Vector3 centre,Vector3 size)
        {if(!batches.TryGetValue(m,out var b)){b=new List<Bounds>();batches[m]=b;}b.Add(new Bounds(centre,size));}
        void Label(string value,Vector3 position,Quaternion rotation,Color colour,float size=.5f)
        {
            var go=new GameObject("Well sign: "+value);go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            size*=profile.Get("well.signScale");var text=go.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=96;text.fontStyle=value=="КОЛОДЕЦ"?FontStyle.Bold:FontStyle.Normal;text.characterSize=size/8;text.color=colour;
            if(!fontMaterial){signFont=text.font;fontMaterial=new Material(Shader.Find("StarTournament/LunarSign"));fontMaterial.mainTexture=signFont.material.mainTexture;owned.Add(fontMaterial);Font.textureRebuilt+=RefreshFont;}
            var renderer=text.GetComponent<MeshRenderer>();renderer.sharedMaterial=fontMaterial;var bounds=renderer.localBounds;go.transform.localPosition=position-rotation*bounds.center;
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
            light.transform.rotation=Quaternion.Euler(50,-30,0);
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
                Material m=solid.Material=="glass"?glass:solid.Material=="door"?door:solid.Material=="floor"?(solid.Id=="garden"?soil:floor):white;
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
                    var skin=WallSkin(solid,definition.Solids);Box(m,skin.center,skin.size);
                    // Floor plates end at the wall centreline. Continue the facade across each slab edge.
                    if(solid.Id.Contains("-outer-"))
                    {
                        var seam=skin;seam.center=new Vector3(seam.center.x,Mathf.Floor(solid.Position.y/WellArenaCatalog.Storey)*WellArenaCatalog.Storey+WellArenaCatalog.ClearHeight+WellArenaCatalog.Slab/2,seam.center.z);
                        seam.size=new Vector3(seam.size.x,WellArenaCatalog.Slab,seam.size.z);Box(white,seam.center,seam.size);
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
                {char side=solid.Id[solid.Id.Length-1];var normal=side=='N'?Vector3.forward:side=='S'?Vector3.back:side=='E'?Vector3.right:Vector3.left;Label("КАБИНЕТ\nЗАКРЫТ",solid.Position+normal*.14f,Quaternion.LookRotation(-normal),Color.white,.25f);}
            }
            for(int f=0;f<3;f++)
            {
                string department=new[]{"БИОРАЗРАБОТКИ","ФИЗИКА","ЭЛЕКТРОНИКА"}[f];Color colour=colours[f].color;
                // Signs sit on the existing opaque atrium fascia, clear of glass and portal silhouettes.
                foreach(int side in new[]{-1,1})
                {
                    string title=f==0?"КОЛОДЕЦ":"0"+(f+1)+"  /  "+department;
                    Label(title,new Vector3(0,f*WellArenaCatalog.Storey+4.8f,side*(WellArenaCatalog.Pit-.125f)),Quaternion.Euler(0,side>0?0:180,0),new Color(.17f,.21f,.22f),f==0?1.2f:.8f);
                }
                Label("0"+(f+1)+"  /  "+department,new Vector3(-9,f*WellArenaCatalog.Storey+3.9f,24.63f),Quaternion.identity,colour,.85f);
                Label("0"+(f+1)+"  /  "+department,new Vector3(9,f*WellArenaCatalog.Storey+3.9f,-24.63f),Quaternion.Euler(0,180,0),colour,.85f);
                Label("ЛИФТ  /  01 ↕ 03",new Vector3(0,f*WellArenaCatalog.Storey+3.95f,24.58f),Quaternion.identity,colour,.38f);
                Label("ЛИФТ  /  01 ↕ 03",new Vector3(0,f*WellArenaCatalog.Storey+3.95f,-24.58f),Quaternion.Euler(0,180,0),colour,.38f);
                foreach(int side in new[]{-1,1})Label("ЛЕСТНИЦА  /  0"+(f+1),new Vector3(side*24.6f,f*WellArenaCatalog.Storey+3.95f,0),Quaternion.Euler(0,side>0?90:-90,0),colour,.38f);
                // Authored fixture locations: broad rings of soft overhead light, clear of all passages.
                foreach(float x in new[]{-18f,0,18f})foreach(float z in new[]{-18f,0,18f})
                {
                    if(x==0&&z==0)continue;var pos=new Vector3(x,f*WellArenaCatalog.Storey+WellArenaCatalog.ClearHeight-.2f,z);
                    Box(metal,pos,new Vector3(3,.10f,1));Box(diffuser,pos-Vector3.up*.07f,new Vector3(2.8f,.03f,.8f));
                    var go=new GameObject("Office ceiling light");go.transform.SetParent(transform,false);go.transform.localPosition=pos-Vector3.up*.15f;go.transform.localRotation=Quaternion.Euler(90,0,0);
                    var light=go.AddComponent<Light>();light.type=LightType.Spot;light.spotAngle=profile.Get("well.spotAngle");light.innerSpotAngle=Mathf.Min(light.spotAngle,profile.Get("well.innerAngle"));light.intensity=profile.Get("well.lamp");light.range=profile.Get("well.lampRange");light.color=new Color(1,.98f,.93f);light.shadows=LightShadows.None;
                }
            }
            // Low plant beds: no collider clutter. The central garden paths and landing area stay empty.
            foreach(var centre in new[]{new Vector3(-6,0,-6),new Vector3(6,0,6)})
            {
                Box(white,centre+Vector3.up*.15f,new Vector3(3,.3f,3));Box(soil,centre+Vector3.up*.31f,new Vector3(2.8f,.05f,2.8f));
                detail.Plants(centre);
            }
            detail.Finish();
            foreach(var pair in batches)
            {
                var blockers=pair.Key==glass?null:batches.Where(p=>p.Key!=pair.Key&&p.Key!=glass).SelectMany(p=>p.Value).ToArray();
                var mesh=WellSurfaceMesh.Build(pair.Value,blockers);owned.Add(mesh);
                if(pair.Key==white||pair.Key==floor)
                {
                    var contact=WellContactMesh.Build(mesh,definition.Solids.Where(s=>s.Material!="glass"&&s.Surface!="well-grate").Select(s=>new Bounds(s.Position,s.Size)).ToArray());owned.Add(contact);mesh=contact;
                }
                var go=new GameObject(pair.Key.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();if(pair.Key==white||pair.Key==floor)
                {WellSurfaceMesh.SplitHorizontalMaterials(mesh);renderer.sharedMaterials=new[]{pair.Key,white,ceiling};}
                else renderer.sharedMaterial=pair.Key;
                if(pair.Key==glass)renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(1,.99f,.97f)*profile.Get("well.fill");
        }
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;foreach(var o in owned)if(o)Destroy(o);}
    }
}
