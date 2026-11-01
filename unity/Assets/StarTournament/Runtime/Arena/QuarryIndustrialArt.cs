using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    // Decorative trim has no collider; main machines/rocks use the canonical solids.
    public sealed class QuarryIndustrialArt : MonoBehaviour,IArenaMeshOwner
    {
        readonly List<Object> owned=new List<Object>();readonly Dictionary<Material,List<CombineInstance>> parts=new Dictionary<Material,List<CombineInstance>>();Mesh cube;
        public void Build(ArenaDefinition definition,Material[] palette)
        {
            var profile=ProvingProfile.CreateQuarryAuthoring(definition);
            var sample=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=sample.GetComponent<MeshFilter>().sharedMesh;DestroyImmediate(sample);
            void Box(Vector3 p,Vector3 size,Material material,Quaternion? rot=null)
            {
                if(!parts.TryGetValue(material,out var list))parts[material]=list=new List<CombineInstance>();
                list.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(p,rot??Quaternion.identity,size)});
            }
            // One material-group mesh per local dressing family; no per-rivet renderers.
            foreach(var solid in definition.Solids)
            {
                if(solid.Material=="quarry-machine")
                {
                    foreach(float side in new[]{-1f,1f})
                    {
                    var q=solid.Rotation;var front=solid.Position+q*new Vector3(0,0,side*(solid.Size.z*.5f+.012f));
                    for(int i=0;i<6;i++)Box(front+q*Vector3.up*((i-2.5f)*solid.Size.y/9),new Vector3(solid.Size.x*.65f,.055f,.025f),palette[9],q);
                    Box(front+q*new Vector3(solid.Size.x*.38f,solid.Size.y*.25f,0),new Vector3(.12f,.2f,.04f),palette[6],q);
                    }
                }
            }
            var origin=new Vector3(-11,0,-23);var turn=Quaternion.Euler(0,-28,0);
            void Loader(Vector3 p,Vector3 s,Material mat,Quaternion? q=null)=>Box(origin+turn*p,s,mat,turn*(q??Quaternion.identity));
            foreach(float x in new[]{-1.19f,1.19f})foreach(float z in new[]{-1.25f,.75f})Loader(new Vector3(x,3.15f,z),new Vector3(.13f,2.15f,.13f),palette[6]);
            Loader(new Vector3(.9f,3.3f,-2.1f),new Vector3(.18f,2.1f,.18f),palette[9]);
            foreach(float x in new[]{-1.65f,1.65f})foreach(float z in new[]{-2.05f,2f})
            {
                // Tread blocks describe tyres without making tiny collision snags.
                for(int i=0;i<12;i++){float a=i*Mathf.PI*2/12;Loader(new Vector3(x,1.1f+Mathf.Sin(a)*1.08f,z+Mathf.Cos(a)*1.08f),new Vector3(.84f,.12f,.25f),palette[9],Quaternion.Euler(-a*Mathf.Rad2Deg,0,0));}
                Loader(new Vector3(x+Mathf.Sign(x)*.4f,1.1f,z),new Vector3(.06f,.7f,.7f),palette[6]);
            }
            foreach(float x in new[]{-1f,1f})Loader(new Vector3(x,2,2.9f),new Vector3(.15f,.15f,2.2f),palette[7],Quaternion.Euler(20,0,0));
            // Workshop ribs, roll-up door, window and task lamps establish an inhabited upper floor.
            for(float x=-40;x<=40;x+=4)Box(new Vector3(x,10.4f,37.18f),new Vector3(.1f,4.8f,.12f),palette[8]);
            Box(new Vector3(22,10.4f,37.15f),new Vector3(5,2.4f,.12f),palette[10]);
            foreach(float x in new[]{19.4f,22f,24.6f})Box(new Vector3(x,10.4f,37.04f),new Vector3(.15f,2.7f,.12f),palette[7]);
            foreach(float y in new[]{9.1f,11.7f})Box(new Vector3(22,y,37.04f),new Vector3(5.3f,.15f,.12f),palette[7]);
            for(int i=0;i<13;i++)Box(new Vector3(31,8.2f+i*.32f,37.12f),new Vector3(4,.24f,.15f),i%2==0?palette[8]:palette[7]);
            for(float x=-32;x<16;x+=6)
            {
                Box(new Vector3(x,12.74f,31),new Vector3(.08f,.1f,8.8f),palette[8],Quaternion.Euler(-7,0,0));
                Box(new Vector3(x,11.8f,36.8f),new Vector3(1.2f,.16f,.4f),palette[12]);
            }
            foreach(var x in new[]{-27f,0f,28f})
            {
                var lamp=new GameObject("Workshop task light");lamp.transform.SetParent(transform,false);lamp.transform.localPosition=new Vector3(x,11.6f,33);
                var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.73f,.4f);light.intensity=profile.Get("quarry.art.workshopLightIntensity");light.range=profile.Get("quarry.art.workshopLightRange");light.shadows=LightShadows.None;
            }
            // Marked steel plates and hazard edges at the top; lower court stays raw stone.
            for(float x=-40;x<40;x+=5)Box(new Vector3(x,8.012f,22),new Vector3(2,.015f,.12f),palette[6]);
            foreach(var portal in definition.Solids.Where(s=>s.Id.EndsWith("portal-lintel")))
            {
                bool east=portal.Id.StartsWith("east");var q=Quaternion.Euler(0,east?-90:0,0);var outward=east?Vector3.right:Vector3.back;
                for(float x=-3;x<=3;x+=1.2f)Box(portal.Position+outward*.53f+q*Vector3.right*x,new Vector3(.4f,.58f,.03f),palette[9],q*Quaternion.Euler(0,0,-25));
            }
            foreach(var pair in parts)
            {
                var mesh=new Mesh{name="Quarry grouped industrial trim"};mesh.CombineMeshes(pair.Value.ToArray(),true,true);owned.Add(mesh);
                var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=pair.Key;renderer.shadowCastingMode=ShadowCastingMode.Off;
            }
        }
        public IEnumerable<Mesh> ReadOwnedMeshes()=>ArenaMeshResidency.OwnedMeshes(owned);
        public IEnumerable<Mesh> ReadDiagnosticMeshes()=>System.Array.Empty<Mesh>();
        public void ReleaseUnusedMeshes(HashSet<int> ids)=>ArenaMeshResidency.ReleaseUnused(owned,ids);
        void OnDestroy(){foreach(var item in owned)if(item)DestroyImmediate(item);}
    }
}
