using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    // Renderer-only markings. Collision remains the six authored shell boxes.
    public sealed class FiringRangePresentation : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        readonly List<MeshRenderer> sourceRenderers=new List<MeshRenderer>(),combinedRenderers=new List<MeshRenderer>();
        public int BeforeRenderers {get;private set;}
        public int AfterRenderers {get;private set;}
        Material Make(Color color,bool emissive=false)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){color=color};
            m.SetFloat("_Smoothness",.18f);
            if(emissive){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color);}
            materials.Add(m);return m;
        }
        void Box(string name,Vector3 p,Vector3 size,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localScale=size;
            DestroyImmediate(go.GetComponent<Collider>());var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
        }
        void Sign(string name,string text,Vector3 p,float yaw,float height,Color color)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=p;go.transform.localRotation=Quaternion.Euler(0,yaw+180,0);
            var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");mesh.fontSize=80;// TextMesh glyphs use a ten-unit font grid at this fontSize.
            mesh.characterSize=height/8;mesh.anchor=TextAnchor.MiddleCenter;mesh.alignment=TextAlignment.Center;mesh.color=color;
            var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=mesh.font.material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        }
        internal static readonly Color[] Palette={new Color32(50,183,202,255),new Color32(148,165,177,255),new Color32(48,64,79,255),new Color32(215,233,241,255)};
        internal static void Layout(ArenaDefinition definition,System.Action<string,Vector3,Vector3,int> box,System.Action<string,string,Vector3,float,float,Color> sign)
        {
            // Dimensions are immutable authoring detail, recorded alongside the map profile below.
            foreach(float side in new[]{-1f,1f})for(int z=-20;z<=20;z+=8)
            {
                box("wall-panel",new Vector3(side*17.94f,3.5f,z),new Vector3(.04f,5.8f,7.5f),1);
                box("wall-strip",new Vector3(side*17.89f,1,z),new Vector3(.03f,.10f,7.5f),0);
            }
            foreach(float z in new[]{-8f,4f,16f})
            {
                box("distance-mark",new Vector3(1,.012f,z-2),new Vector3(28,.012f,.07f),0);
                sign("distance-sign",(z+20).ToString("0")+" m",new Vector3(17.84f,2,z),-90,1.2f,Color.white);
            }
            foreach(var spawn in definition.Spawns)
            {box("spawn-mark",spawn+new Vector3(0,.014f,-1.1f),new Vector3(2,.014f,.12f),0);}
            foreach(float x in new[]{-10f,0f,10f})foreach(float z in new[]{-14f,2f,18f})
            {box("ceiling-frame",new Vector3(x,7.96f,z),new Vector3(4.2f,.08f,2.2f),2);box("ceiling-light",new Vector3(x,7.90f,z),new Vector3(4,.04f,2),3);}
            box("backstop-panel",new Vector3(0,3,23.96f),new Vector3(33,5,.05f),2);
            sign("range-title","ПОЛИГОН",new Vector3(0,5.4f,23.88f),180,2.5f,Color.white);
            sign("range-subtitle","СВОБОДНАЯ ТРЕНИРОВКА",new Vector3(0,3.4f,23.87f),180,.8f,new Color32(110,211,229,255));
            var captions=new[]{"БРОНЯ","СКОРОСТЬ","УРОН","ЗДОРОВЬЕ\nПАТРОНЫ","VECTOR","PULSE","РЕЗАК","FORGE"};
            for(int i=0;i<definition.Pickups.Length;i++)
            {var point=definition.Pickups[i].Anchor;box("supply-mark",point+new Vector3(0,.012f,0),new Vector3(1.5f,.012f,1.5f),2);sign("supply-sign",captions[i],new Vector3(-17.84f,2,point.z),90,.55f,Color.white);}
        }
        public void Build(ArenaDefinition definition,bool optimize=true)
        {
            var profile=FiringRangeCatalog.AuthoringProfile();
            Vector3 V(string key)=>new Vector3(profile.Get("range.decor."+key+".x"),profile.Get("range.decor."+key+".y"),profile.Get("range.decor."+key+".z"));
            var kit=Enumerable.Range(0,Palette.Length).Select(i=>{var rgb=V("material-"+i);return Make(new Color(rgb.x,rgb.y,rgb.z),i==0||i==3);}).ToArray();
            foreach(var material in kit)material.SetFloat("_Smoothness",profile.Get("range.decor.smoothness"));
            int box=0,sign=0;
            Layout(definition,(name,pos,size,material)=>{string key=name+"-"+box++;Box(key,V(key+".position"),V(key+".size"),kit[material]);},
                (name,text,pos,yaw,height,color)=>{string key=name+"-"+sign++;var settings=V(key+".text");var rgb=V(key+".color");Sign(key,text,V(key+".position"),settings.x,settings.y,new Color(rgb.x,rgb.y,rgb.z));});
            BeforeRenderers=GetComponentsInChildren<MeshRenderer>().Length;
            if(optimize)Optimize();
            AfterRenderers=GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled);
        }
        public void Optimize()
        {
            // Combine identical-material static boxes; no gameplay colliders, transforms, textures or lights change.
            var sources=GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled&&!r.GetComponent<TextMesh>()).ToArray();
            foreach(var group in sources.GroupBy(r=>r.sharedMaterial))
            {
                var data=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=transform.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
                var mesh=new Mesh{name="range-static-"+group.Key.name};mesh.CombineMeshes(data);meshes.Add(mesh);
                var go=new GameObject("range-static-batch",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(transform,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=group.Key;renderer.shadowCastingMode=ShadowCastingMode.Off;combinedRenderers.Add(renderer);
                foreach(var source in group){source.enabled=false;sourceRenderers.Add(source);}
            }
        }
        public void SetCombined(bool combined)
        {foreach(var source in sourceRenderers)source.enabled=!combined;foreach(var batch in combinedRenderers)batch.enabled=combined;}
        void OnDestroy(){foreach(var mesh in meshes)if(mesh)DestroyImmediate(mesh);foreach(var material in materials)if(material)DestroyImmediate(material);}
    }
}
