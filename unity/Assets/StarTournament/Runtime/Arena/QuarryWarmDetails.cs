using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    // Editable asset source. Shallow fittings stay attached to existing solids; never add colliders or route obstructions.
    public sealed class QuarryWarmDetails : MonoBehaviour,IArenaMeshOwner
    {
        // Opt-in matched-render evidence only. Production never creates per-fitting renderers.
        public static bool CaptureUngroupedReference { get; set; }
        public int UncombinedPartCount { get; private set; }
        public int GroupedRendererCount=>groupedRenderers.Count;
        readonly List<MeshRenderer> referenceRenderers=new List<MeshRenderer>();
        readonly List<MeshRenderer> groupedRenderers=new List<MeshRenderer>();
        public void UseGrouped(bool grouped)
        {
            foreach(var r in referenceRenderers)if(r)r.enabled=!grouped;
            foreach(var r in groupedRenderers)if(r)r.enabled=grouped;
        }
        readonly List<Object> owned=new List<Object>();
        readonly Dictionary<(int,int),List<CombineInstance>> groups=new Dictionary<(int,int),List<CombineInstance>>();
        Mesh cube,cylinder;Material[] palette;
        void Part(int family,int material,Mesh mesh,Vector3 position,Vector3 size,Quaternion rotation)
        {
            var key=(family,material);if(!groups.TryGetValue(key,out var parts))groups[key]=parts=new List<CombineInstance>();
            parts.Add(new CombineInstance{mesh=mesh,transform=Matrix4x4.TRS(position,rotation,size)});
        }
        public void Build(ArenaDefinition d,Material[] materials)
        {
            palette=materials;
            var sample=GameObject.CreatePrimitive(PrimitiveType.Cube);cube=sample.GetComponent<MeshFilter>().sharedMesh;DestroyImmediate(sample);
            sample=GameObject.CreatePrimitive(PrimitiveType.Cylinder);cylinder=sample.GetComponent<MeshFilter>().sharedMesh;DestroyImmediate(sample);
            void Box(int family,int mat,Vector3 pos,Vector3 size,Quaternion q)=>Part(family,mat,cube,pos,size,q);
            // Geometry below is the authored fitting topology, not a gameplay/readability tuning parameter.
            foreach(var s in d.Solids.Where(s=>s.Material=="quarry-crate"))
            {
                int family=s.Position.y<4?0:1;var q=s.Rotation;
                foreach(float side in new[]{-1f,1f})
                {
                    float z=side*(s.Size.z*.5f+.035f);
                    foreach(float x in new[]{-.43f,.43f})
                    {
                        Box(family,3,s.Position+q*new Vector3(x*s.Size.x,0,z),new Vector3(.18f,s.Size.y,.075f),q);
                        foreach(float y in new[]{-.35f,.35f})Part(family,7,cylinder,s.Position+q*new Vector3(x*s.Size.x,y*s.Size.y,z+side*.045f),new Vector3(.065f,.012f,.065f),q*Quaternion.Euler(90,0,0));
                    }
                    foreach(float y in new[]{-.43f,.43f})Box(family,3,s.Position+q*new Vector3(0,y*s.Size.y,z),new Vector3(s.Size.x,.17f,.07f),q);
                }
            }
            var origin=new Vector3(-11,0,-23);var turn=Quaternion.Euler(0,-28,0);
            void Loader(int mat,Vector3 p,Vector3 size,Quaternion? rotation=null)=>Box(2,mat,origin+turn*p,size,turn*(rotation??Quaternion.identity));
            void Pin(int mat,Vector3 p,float diameter,float depth,Quaternion rotation)=>Part(2,mat,cylinder,origin+turn*p,new Vector3(diameter,depth*.5f,diameter),turn*rotation);
            foreach(float x in new[]{-1.65f,1.65f})foreach(float z in new[]{-2.05f,2f})
            {
                float face=x+Mathf.Sign(x)*.42f;
                Pin(7,new Vector3(face,1.1f,z),.62f,.08f,Quaternion.Euler(0,0,90));
                Pin(6,new Vector3(face+Mathf.Sign(x)*.05f,1.1f,z),.25f,.08f,Quaternion.Euler(0,0,90));
                for(int j=0;j<6;j++){float a=j*Mathf.PI/3;Pin(8,new Vector3(face+Mathf.Sign(x)*.05f,1.1f+Mathf.Sin(a)*.23f,z+Mathf.Cos(a)*.23f),.075f,.045f,Quaternion.Euler(0,0,90));}
            }
            // Engine grille, side access panels, hinges, step plates, sealed headlamps and hydraulic sleeves.
            Loader(8,new Vector3(0,2.1f,-3.115f),new Vector3(2.2f,.86f,.04f));
            for(int j=0;j<12;j++)Loader(7,new Vector3((j-5.5f)*.17f,2.1f,-3.15f),new Vector3(.035f,.75f,.025f));
            foreach(float side in new[]{-1f,1f})
            {
                Loader(6,new Vector3(side*1.36f,2.15f,-2),new Vector3(.045f,.9f,1.65f));
                Loader(8,new Vector3(side*1.395f,2.3f,-2.5f),new Vector3(.025f,.09f,.35f));
                for(int j=0;j<5;j++)Loader(8,new Vector3(side*1.396f,1.9f+j*.1f,-1.9f),new Vector3(.025f,.035f,.65f));
                Loader(7,new Vector3(side*1.53f,1.65f,-.45f),new Vector3(.15f,.07f,.75f));
                Loader(7,new Vector3(side*1.53f,1.35f,-.45f),new Vector3(.15f,.07f,.75f));
                Loader(8,new Vector3(side*.95f,2,3.1f),new Vector3(.22f,.22f,1.4f),Quaternion.Euler(20,0,0));
                Loader(7,new Vector3(side*.95f,1.7f,3.9f),new Vector3(.12f,.12f,1),Quaternion.Euler(20,0,0));
                Loader(8,new Vector3(side*1.1f,1.7f,3.21f),new Vector3(.42f,.28f,.045f));
                Loader(12,new Vector3(side*1.1f,1.7f,3.24f),new Vector3(.31f,.17f,.025f));
                Loader(8,new Vector3(side*1.16f,3.1f,-.22f),new Vector3(.025f,.075f,1.95f));
            }
            // On-body bucket wear bars do not extend its canonical collision envelope.
            for(int j=0;j<7;j++)Loader(7,new Vector3((j-3)*.49f,.49f,4.8f),new Vector3(.12f,.025f,2));
            Loader(8,new Vector3(0,3.08f,.767f),new Vector3(.035f,.7f,.035f),Quaternion.Euler(0,0,-20));
            // Shallow workshop access-panel framing and fasteners on the existing rear wall.
            for(int j=0;j<10;j++)
            {
                float x=-39+j*5;
                Box(3,8,new Vector3(x,10.4f,37.20f),new Vector3(.085f,4.55f,.045f),Quaternion.identity);
                foreach(float y in new[]{8.3f,10.4f,12.5f})Part(3,7,cylinder,new Vector3(x,y,37.16f),new Vector3(.09f,.018f,.09f),Quaternion.Euler(90,0,0));
            }
            foreach(var s in d.Solids.Where(s=>s.Material=="quarry-machine"&&!s.Id.StartsWith("loader-")))
            {
                if(s.Surface=="quarry-cylinder")continue;
                var q=s.Rotation;float z=-s.Size.z*.5f-.04f;
                Box(3,7,s.Position+q*new Vector3(-s.Size.x*.3f,0,z),new Vector3(.07f,s.Size.y*.8f,.035f),q);
                foreach(float y in new[]{-.3f,.3f})Box(3,7,s.Position+q*new Vector3(0,y*s.Size.y,z),new Vector3(s.Size.x*.8f,.07f,.035f),q);
            }
            foreach(var pair in groups)
            {
                UncombinedPartCount+=pair.Value.Count;
                if(CaptureUngroupedReference)foreach(var part in pair.Value)
                {
                    var raw=new GameObject("Quarry fitting reference");raw.transform.SetParent(transform,false);
                    raw.transform.localPosition=part.transform.GetColumn(3);raw.transform.localRotation=part.transform.rotation;raw.transform.localScale=part.transform.lossyScale;
                    raw.AddComponent<MeshFilter>().sharedMesh=part.mesh;
                    var reference=raw.AddComponent<MeshRenderer>();reference.sharedMaterial=palette[pair.Key.Item2];
                    reference.shadowCastingMode=ShadowCastingMode.Off;reference.receiveShadows=true;reference.enabled=false;referenceRenderers.Add(reference);
                }
                var mesh=new Mesh{name="Quarry warm fittings / "+pair.Key.Item1+" / "+pair.Key.Item2};
                mesh.CombineMeshes(pair.Value.ToArray(),true,true);owned.Add(mesh);
                var go=new GameObject(mesh.name);go.transform.SetParent(transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
                var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=palette[pair.Key.Item2];r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=true;groupedRenderers.Add(r);
            }
            groups.Clear(); // CombineInstance references must not retain source meshes after construction.
        }
        public IEnumerable<Mesh> ReadOwnedMeshes()=>ArenaMeshResidency.OwnedMeshes(owned);
        public IEnumerable<Mesh> ReadDiagnosticMeshes()=>System.Array.Empty<Mesh>();
        public void ReleaseUnusedMeshes(HashSet<int> ids)=>ArenaMeshResidency.ReleaseUnused(owned,ids);
        void OnDestroy(){foreach(var mesh in owned)if(mesh)DestroyImmediate(mesh);}
    }
}
