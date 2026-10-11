using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Bright white laboratory skin. All decoration is collider-free; authored solids own obstruction.</summary>
    public sealed class WellArenaPresentation:MonoBehaviour
    {
        readonly List<Object> owned=new List<Object>();readonly Dictionary<Material,List<CombineInstance>> batches=new Dictionary<Material,List<CombineInstance>>();
        UnityEngine.Rendering.AmbientMode oldMode;Color oldAmbient;
        Material fontMaterial;Font signFont;
        Material white,floor,metal,glass,door,soil;Material[] colours;Mesh cube;ProvingProfile profile;
        Material Mat(string name,Color colour,bool unlit=false,VisualMaterialRole role=VisualMaterialRole.Wall)
        {var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit")){name=name,color=colour};m.SetFloat("_Smoothness",.35f);owned.Add(m);if(!unlit)NativeVisualMaterials.Register(this,m,role);return m;}
        void Box(Material m,Vector3 centre,Vector3 size)
        {if(!batches.TryGetValue(m,out var b)){b=new List<CombineInstance>();batches[m]=b;}b.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(centre,Quaternion.identity,size)});}
        void Label(string value,Vector3 position,Quaternion rotation,Color colour,float size=.5f)
        {
            var go=new GameObject("Well sign: "+value);go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=rotation;
            size*=profile.Get("well.signScale");var text=go.AddComponent<TextMesh>();text.text=value;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontSize=64;text.characterSize=size/8;text.color=colour;
            if(!fontMaterial){signFont=text.font;fontMaterial=new Material(Shader.Find("StarTournament/LunarSign"));fontMaterial.mainTexture=signFont.material.mainTexture;owned.Add(fontMaterial);Font.textureRebuilt+=RefreshFont;}
            var renderer=text.GetComponent<MeshRenderer>();renderer.sharedMaterial=fontMaterial;var bounds=renderer.localBounds;go.transform.localPosition=position-rotation*bounds.center;
        }
        void RefreshFont(Font font){if(font==signFont&&fontMaterial)fontMaterial.mainTexture=font.material.mainTexture;}
        public void Build(ArenaDefinition definition,ProvingProfile profile)
        {
            this.profile=profile;oldMode=RenderSettings.ambientMode;oldAmbient=RenderSettings.ambientLight;var primitive=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=primitive.GetComponent<MeshFilter>().sharedMesh;DestroyImmediate(primitive);
            white=Mat("Well ceramic white",new Color(.92f,.95f,.96f));floor=Mat("Well pale floor",new Color(.63f,.71f,.74f),role:VisualMaterialRole.Floor);metal=Mat("Well brushed metal",new Color(.25f,.32f,.36f),role:VisualMaterialRole.Metal);metal.SetFloat("_Metallic",.6f);
            door=Mat("Closed office doors",new Color(.42f,.50f,.55f));soil=Mat("Garden soil",new Color(.23f,.15f,.08f));
            glass=Mat("Armoured glass",new Color(.35f,.80f,.89f,profile.Get("well.glassOpacity")));glass.SetFloat("_Surface",1);glass.SetFloat("_Blend",0);glass.SetFloat("_SrcBlend",5);glass.SetFloat("_DstBlend",10);glass.SetFloat("_ZWrite",0);glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");glass.renderQueue=3000;
            colours=new[]{Mat("01 bio green",new Color(.12f,.62f,.30f)),Mat("02 physics blue",new Color(.08f,.42f,.80f)),Mat("03 electronics yellow",new Color(.90f,.67f,.08f))};
            // Replace the generic physical meshes with material batches, keeping their exact colliders.
            foreach(var solid in definition.Solids)
            {
                var tr=transform.parent.Find(solid.Id);if(!tr)continue;tr.GetComponent<Renderer>().enabled=false;
                Material m=solid.Material=="glass"?glass:solid.Material=="door"?door:solid.Material=="floor"?(solid.Id=="garden"?soil:floor):white;
                if(solid.Surface=="well-grate")
                {
                    // Collision is a movement-only deck; visible spaced bars leave firing sightlines through the mesh.
                    for(float x=solid.Position.x-solid.Size.x/2;x<solid.Position.x+solid.Size.x/2;x+=profile.Get("well.grateSpacing"))Box(metal,new Vector3(x,solid.Position.y,solid.Position.z),new Vector3(.055f,solid.Size.y,solid.Size.z));
                    for(float z=solid.Position.z-solid.Size.z/2;z<solid.Position.z+solid.Size.z/2;z+=profile.Get("well.grateSpacing"))Box(metal,new Vector3(solid.Position.x,solid.Position.y,z),new Vector3(solid.Size.x,solid.Size.y,.055f));
                }
                else Box(m,solid.Position,solid.Size);
                if(solid.Material=="wall"&&solid.Size.y>2)
                {
                    int f=Mathf.Clamp(Mathf.FloorToInt(solid.Position.y/5),0,2);var stripe=solid.Size;stripe.y=.16f;stripe.x+=.012f;stripe.z+=.012f;Box(colours[f],new Vector3(solid.Position.x,f*5+1.1f,solid.Position.z),stripe);
                }
                if(solid.Material=="equipment")
                {Box(metal,solid.Position+Vector3.up*(solid.Size.y/2+.02f),new Vector3(solid.Size.x,.05f,solid.Size.z));Box(colours[Mathf.Clamp((int)(solid.Position.y/5),0,2)],solid.Position+Vector3.up*.35f,new Vector3(solid.Size.x+.01f,.12f,solid.Size.z+.01f));}
                if(solid.Material=="glass")
                {var frame=solid.Size;frame.y=.06f;frame.x+=.06f;frame.z+=.06f;Box(metal,solid.Position+Vector3.up*solid.Size.y/2,frame);Box(metal,solid.Position-Vector3.up*solid.Size.y/2,frame);}
                if(solid.Material=="door")
                {char side=solid.Id[solid.Id.Length-1];var normal=side=='N'?Vector3.forward:side=='S'?Vector3.back:side=='E'?Vector3.right:Vector3.left;Label("КАБИНЕТ\nЗАКРЫТ",solid.Position+normal*.14f,Quaternion.LookRotation(-normal),Color.white,.25f);}
            }
            for(int f=0;f<3;f++)
            {
                string department=new[]{"БИОРАЗРАБОТКИ","ФИЗИКА","ЭЛЕКТРОНИКА"}[f];Color colour=colours[f].color;
                Label("0"+(f+1)+"  /  "+department,new Vector3(-9,f*5+2.7f,24.63f),Quaternion.identity,colour,.85f);
                Label("0"+(f+1)+"  /  "+department,new Vector3(9,f*5+2.7f,-24.63f),Quaternion.Euler(0,180,0),colour,.85f);
                Label("ЛИФТ  /  01 ↕ 03",new Vector3(0,f*5+3.95f,24.58f),Quaternion.identity,colour,.38f);
                Label("ЛИФТ  /  01 ↕ 03",new Vector3(0,f*5+3.95f,-24.58f),Quaternion.Euler(0,180,0),colour,.38f);
                foreach(int side in new[]{-1,1})Label("ЛЕСТНИЦА  /  0"+(f+1),new Vector3(side*24.6f,f*5+3.95f,0),Quaternion.Euler(0,side>0?90:-90,0),colour,.38f);
                // Authored fixture locations: broad rings of soft overhead light, clear of all passages.
                foreach(float x in new[]{-18f,0,18f})foreach(float z in new[]{-18f,0,18f})
                {
                    if(x==0&&z==0)continue;var pos=new Vector3(x,f*5+4.5f,z);
                    Box(metal,pos,new Vector3(3,.10f,1));Box(white,pos-Vector3.up*.07f,new Vector3(2.8f,.03f,.8f));
                    var go=new GameObject("Office ceiling light");go.transform.SetParent(transform,false);go.transform.localPosition=pos-Vector3.up*.15f;go.transform.localRotation=Quaternion.Euler(90,0,0);
                    var light=go.AddComponent<Light>();light.type=LightType.Spot;light.spotAngle=profile.Get("well.spotAngle");light.innerSpotAngle=Mathf.Min(light.spotAngle,profile.Get("well.innerAngle"));light.intensity=profile.Get("well.lamp");light.range=profile.Get("well.lampRange");light.color=new Color(1,.98f,.93f);light.shadows=LightShadows.None;
                }
            }
            // Low plant beds: no collider clutter. The central garden paths and landing area stay empty.
            foreach(var centre in new[]{new Vector3(-6,0,-6),new Vector3(6,0,6)})
            {
                Box(white,centre+Vector3.up*.15f,new Vector3(3,.3f,3));Box(soil,centre+Vector3.up*.31f,new Vector3(2.8f,.05f,2.8f));
                for(int x=-1;x<=1;x++)for(int z=-1;z<=1;z++)Box(colours[0],centre+new Vector3(x,.5f,z),new Vector3(.5f,.4f,.5f));
            }
            // Subtle panel joints are cosmetic, and cannot catch the capsule.
            for(int f=0;f<3;f++)for(float t=-24;t<=24;t+=profile.Get("well.panelSize"))
            {Box(metal,new Vector3(t,f*5+.006f,17.2f),new Vector3(.015f,.008f,15));Box(metal,new Vector3(t,f*5+.006f,-17.2f),new Vector3(.015f,.008f,15));}
            foreach(var pair in batches)
            {
                var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32,name="Well material batch"};mesh.CombineMeshes(pair.Value.ToArray());owned.Add(mesh);
                var go=new GameObject(pair.Key.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;
            }
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.9f,.94f,1)*profile.Get("well.fill");
        }
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;foreach(var o in owned)if(o)Destroy(o);}
    }
}
