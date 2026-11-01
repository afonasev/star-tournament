using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    public sealed class QuarryArenaPresentation : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();Mesh markings,arrows;
        Color oldAmbient;AmbientMode oldMode;bool ownsAmbient;Font signFont;Material fontMaterial;
        Material Make(ProvingProfile p,int i)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Quarry shared "+i,color=new Color(p.Get("quarry.art.color-"+i+".0"),p.Get("quarry.art.color-"+i+".1"),p.Get("quarry.art.color-"+i+".2"))};
            m.SetFloat("_Smoothness",p.Get("quarry.art.smoothness"));materials.Add(m);return m;
        }
        public void Build(ArenaDefinition definition)
        {
            var profile=ProvingProfile.CreateQuarryAuthoring(definition);oldAmbient=RenderSettings.ambientLight;oldMode=RenderSettings.ambientMode;ownsAmbient=true;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.white*profile.Get("quarry.art.ambient");
            var palette=new Material[13];for(int i=0;i<palette.Length;i++)palette[i]=Make(profile,i);
            bool industrial=definition.Revision=="3";
            palette[12].EnableKeyword("_EMISSION");palette[12].SetColor("_EmissionColor",palette[12].color*profile.Get("quarry.art.workshopLightIntensity"));
            if(industrial)
            {
                foreach(int i in new[]{0,1,2,5,7})
                {
                    var color=palette[i].color;palette[i].shader=Shader.Find("StarTournament/QuarrySurface");palette[i].SetColor("_BaseColor",color);
                    palette[i].SetFloat("_Mode",i==0?2:i==5||i==7?3:1);
                    palette[i].SetFloat("_StoneCell",profile.Get("quarry.art.stoneCell"));
                }
                palette[5].SetColor("_BaseColor",palette[7].color);
                palette[1].SetColor("_BaseColor",palette[11].color);
            }
            var lookup=new ArenaPresentationLookup(transform.parent);
            foreach(var solid in definition.Solids)
            {
                var renderer=lookup.Find(solid.Id).GetComponent<MeshRenderer>();
                int index=solid.Material=="quarry-metal"?7:solid.Material=="quarry-machine"?8:solid.Material=="quarry-rubber"?9:solid.Material=="quarry-glass"?10:solid.Material=="quarry-portal"?6:solid.Material=="quarry-crate"?3:solid.Material=="floor"?(solid.Support==QuarryArenaCatalog.Ground?0:solid.Support==QuarryArenaCatalog.Upper?5:2):1;
                renderer.sharedMaterial=palette[index];
            }
            markings=ArenaPickupMarkings.Build(transform,definition,palette[4],profile.Get("quarry.art.pickupRingRadius"),profile.Get("quarry.art.pickupRingWidth"),_=>profile.Get("quarry.art.pickupRingHeight"));
            if(industrial)gameObject.AddComponent<QuarryIndustrialArt>().Build(definition,palette);
            if(definition.Revision=="1")return;
            void Label(string name,string text,Vector3 position,float yaw,float size)
            {
                var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,yaw,0);
                var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.fontSize=80;mesh.characterSize=size/8;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=Color.white;
                if(!fontMaterial){signFont=mesh.font;fontMaterial=new Material(Shader.Find("StarTournament/LunarSign")){name="Quarry depth-tested lettering"};materials.Add(fontMaterial);RefreshFont(signFont);Font.textureRebuilt+=RefreshFont;}
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=fontMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            float sign=profile.Get("quarry.art.signHeight");
            foreach(var solid in definition.Solids.Where(s=>s.Id.EndsWith("portal-lintel")))
            {
                bool east=solid.Id.StartsWith("east");var outward=east?Vector3.right:Vector3.back;
                Label(solid.Id+"-sign","ТОННЕЛЬ",solid.Position+outward*.52f,east?-90:0,sign);
            }
            var middle=definition.Solids.Single(s=>s.Id=="middle-west");var upper=definition.Solids.Single(s=>s.Id=="upper-excavation");
            Label("middle-level-sign",industrial?"УСТУПЫ  +4 м":"МАСТЕРСКАЯ  +4 м",middle.Position-Vector3.up*2+Vector3.back*(middle.Size.z*.5f+.03f),0,sign);
            Label("upper-level-sign",industrial?"МАСТЕРСКАЯ  +8 м":"ВЫРАБОТКА  +8 м",new Vector3(0,6,upper.Position.z-upper.Size.z*.5f-.03f),0,sign);
            Label("lower-level-sign","ПОГРУЗКА  0 м",new Vector3(19,2,-38),180,sign);
            foreach(var solid in definition.Solids.Where(s=>s.Id.EndsWith("tunnel-lamp")))
            {
                var go=new GameObject(solid.Id+"-light");go.transform.SetParent(transform,false);go.transform.localPosition=solid.Position-Vector3.up*.3f;
                var lamp=go.AddComponent<Light>();lamp.type=LightType.Point;lamp.color=palette[6].color;lamp.range=profile.Get("quarry.art.lampRange");lamp.intensity=profile.Get("quarry.art.lampIntensity");lamp.shadows=LightShadows.None;
            }
            var vertices=new List<Vector3>();var triangles=new List<int>();float width=profile.Get("quarry.art.arrowWidth"),spacing=profile.Get("quarry.art.arrowSpacing");
            foreach(var ramp in definition.Solids.Where(s=>s.Support!=null&&s.Support.StartsWith("transition:")))
                for(float z=-ramp.Size.z*.5f+spacing*.5f;z<ramp.Size.z*.5f-spacing*.5f;z+=spacing)
                {
                    var points=new[]{new Vector2(-.18f,-.5f),new Vector2(.18f,-.5f),new Vector2(.18f,0),new Vector2(.5f,0),new Vector2(0,.5f),new Vector2(-.5f,0),new Vector2(-.18f,0)};
                    int start=vertices.Count;vertices.Add(ramp.Position+ramp.Rotation*new Vector3(0,.21f,z));
                    foreach(var point in points)vertices.Add(ramp.Position+ramp.Rotation*new Vector3(point.x*width,.21f,z+point.y*width*1.5f));
                    for(int i=0;i<points.Length;i++){triangles.Add(start);triangles.Add(start+1+(i+1)%points.Length);triangles.Add(start+1+i);}
                }
            arrows=new Mesh{name="Quarry ramp direction markings"};arrows.SetVertices(vertices);arrows.SetTriangles(triangles,0);arrows.RecalculateNormals();arrows.RecalculateBounds();
            var markingsGo=new GameObject("Ramp direction markings");markingsGo.transform.SetParent(transform,false);markingsGo.AddComponent<MeshFilter>().sharedMesh=arrows;var arrowsRenderer=markingsGo.AddComponent<MeshRenderer>();arrowsRenderer.sharedMaterial=palette[6];arrowsRenderer.shadowCastingMode=ShadowCastingMode.Off;arrowsRenderer.receiveShadows=true;
        }
        public static void ConfigureDaylight(Light sun)=>sun.intensity=QuarryArenaCatalog.AuthoringProfile().Get("quarry.art.sun");
        void RefreshFont(Font font){if(font==signFont&&fontMaterial)fontMaterial.mainTexture=font.material.mainTexture;}
        void OnDestroy(){Font.textureRebuilt-=RefreshFont;if(ownsAmbient){RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;}if(markings)DestroyImmediate(markings);if(arrows)DestroyImmediate(arrows);foreach(var material in materials)if(material)DestroyImmediate(material);}
    }
}
