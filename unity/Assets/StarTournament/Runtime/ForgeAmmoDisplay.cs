using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    /// <summary>Read-only rear instrument. Shared artwork, per-owner values, no clock or gameplay writes.</summary>
    public sealed class ForgeAmmoDisplay : MonoBehaviour
    {
        // Twenty-four windows are immutable artwork, not rounds or a gameplay capacity.
        public const int SegmentCount=24;
        static Mesh sharedMesh;
        static Material sharedMaterial;
        static int owners;
        bool ownsResources;
        MeshRenderer screen;
        MaterialPropertyBlock properties;
        public int Ammo {get;private set;}=-1;
        public float ReserveFraction {get;private set;}=-1;
        public string DisplayText {get;private set;}="";
        public Renderer Screen=>screen;
        public int LitSegments=>Ammo>0?Mathf.CeilToInt(ReserveFraction*SegmentCount):0;

        public void Initialize(Transform weapon,ProvingProfile profile,int layer)
        {
            transform.SetParent(weapon,false);gameObject.layer=layer;
            var defaults=ProvingProfile.CreateDefault();
            float P(string key)=>profile.Descriptor(ProvingProfile.ForgeDisplayPrefix+key)!=null?
                profile.Get(ProvingProfile.ForgeDisplayPrefix+key):defaults.Get(ProvingProfile.ForgeDisplayPrefix+key);
            transform.localPosition=new Vector3(0,P("centerY"),P("rearZ"));
            if(!sharedMesh)sharedMesh=BuildMesh();
            if(!sharedMaterial)sharedMaterial=new Material(Resources.Load<Shader>("ForgeDisplay"))
                {name="FORGE shared inset instrument",mainTexture=Resources.Load<Texture2D>("ShotgunDigits")};
            owners++;ownsResources=true;
            gameObject.AddComponent<MeshFilter>().sharedMesh=sharedMesh;
            screen=gameObject.AddComponent<MeshRenderer>();screen.sharedMaterial=sharedMaterial;
            screen.shadowCastingMode=ShadowCastingMode.Off;screen.receiveShadows=false;
            screen.lightProbeUsage=LightProbeUsage.Off;screen.reflectionProbeUsage=ReflectionProbeUsage.Off;
            properties=new MaterialPropertyBlock();
            properties.SetVector("_Layout",new Vector4(P("radius"),P("width"),P("height"),P("offsetY")));
            properties.SetFloat("_Intensity",P("intensity"));properties.SetFloat("_LowFraction",P("lowFraction"));
            SetAmmo(0,1);
        }
        public void SetAmmo(int value,float capacity)
        {
            value=Mathf.Max(0,value);float fraction=capacity>0?Mathf.Clamp01(value/capacity):0;
            if(Ammo==value&&ReserveFraction==fraction)return;
            Ammo=value;ReserveFraction=fraction;DisplayText=value.ToString(CultureInfo.InvariantCulture);
            var a=Vector4.zero;var b=Vector4.zero;var c=Vector4.zero;
            for(int i=0;i<DisplayText.Length;i++)
                if(i<4)a[i]=DisplayText[i]-'0';else if(i<8)b[i-4]=DisplayText[i]-'0';else c[i-8]=DisplayText[i]-'0';
            properties.SetVector("_DigitsA",a);properties.SetVector("_DigitsB",b);properties.SetVector("_DigitsC",c);
            properties.SetFloat("_DigitCount",DisplayText.Length);properties.SetFloat("_LitSegments",LitSegments);
            properties.SetFloat("_Reserve",fraction);properties.SetFloat("_Empty",value==0?1:0);
            screen.SetPropertyBlock(properties);
        }
        public void SetVisible(bool visible){if(screen)screen.enabled=visible;}
        static Mesh BuildMesh()
        {
            // One shared annulus with a true hole, plus an eight-sided inset face.
            // Vertex shader applies profile dimensions; conservative bounds cover their hard ranges.
            const int sides=96;
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var parts=new List<Vector2>();var triangles=new List<int>();
            void V(Vector2 p,float part){vertices.Add(p);uv.Add(p*.5f+Vector2.one*.5f);parts.Add(new Vector2(part,0));}
            for(int i=0;i<=sides;i++)
            {
                float angle=i*Mathf.PI*2/sides;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                V(direction,0);V(direction*.855f,0);
                if(i==sides)continue;
                int j=i*2;triangles.AddRange(new[]{j,j+1,j+2,j+2,j+1,j+3});
            }
            int center=vertices.Count;V(Vector2.zero,1);
            var outline=new[]{new Vector2(-.8f,-1),new Vector2(.8f,-1),new Vector2(1,-.68f),new Vector2(1,.68f),new Vector2(.8f,1),new Vector2(-.8f,1),new Vector2(-1,.68f),new Vector2(-1,-.68f)};
            foreach(var p in outline)V(p,1);
            for(int i=0;i<8;i++)triangles.AddRange(new[]{center,center+1+(i+1)%8,center+1+i});
            var mesh=new Mesh{name="FORGE annulus and faceted counter"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,parts);mesh.SetTriangles(triangles,0);
            mesh.RecalculateNormals();mesh.bounds=new Bounds(new Vector3(0,-.04f,0),new Vector3(.34f,.42f,.02f));return mesh;
        }
        public void ReleaseResources()
        {
            // Model roots release even never-selected/inactive weapon children. Corpse clones
            // have no initialized ownership, so they cannot decrement the shared resource count.
            if(!ownsResources)return;ownsResources=false;
            if(--owners!=0)return;
            Destroy(sharedMesh);Destroy(sharedMaterial);sharedMesh=null;sharedMaterial=null;
        }
        void OnDestroy()=>ReleaseResources();
    }
}
