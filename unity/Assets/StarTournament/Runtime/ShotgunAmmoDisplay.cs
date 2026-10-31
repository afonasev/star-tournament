using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    /// <summary>Weapon-local read-only display. Shared immutable art; per-instance digit properties.</summary>
    public sealed class ShotgunAmmoDisplay : MonoBehaviour
    {
        static Mesh sharedMesh;
        static Material sharedMaterial;
        static int owners;
        MeshRenderer screen;
        MaterialPropertyBlock properties;
        float intensity;
        bool ownsResources;
        public int Ammo {get;private set;}=-1;
        public string DisplayText {get;private set;}="";
        public Renderer Screen=>screen;
        public void Initialize(Transform weapon,ProvingProfile profile,int layer)
        {
            transform.SetParent(weapon,false);gameObject.layer=layer;
            float P(string key)=>profile.Descriptor(ProvingProfile.ShotgunDisplayPrefix+key)!=null?
                profile.Get(ProvingProfile.ShotgunDisplayPrefix+key):ProvingProfile.CreateDefault().Get(ProvingProfile.ShotgunDisplayPrefix+key);
            transform.localPosition=new Vector3(0,P("centerY"),P("rearZ"));
            transform.localScale=new Vector3(P("width"),P("height"),1);intensity=P("intensity");
            if(!sharedMesh)sharedMesh=BuildMesh();
            if(!sharedMaterial)sharedMaterial=new Material(Resources.Load<Shader>("ShotgunDisplay"))
                {name="Vector integrated ammo glass",mainTexture=Resources.Load<Texture2D>("ShotgunDigits")};
            owners++;ownsResources=true;
            gameObject.AddComponent<MeshFilter>().sharedMesh=sharedMesh;
            screen=gameObject.AddComponent<MeshRenderer>();screen.sharedMaterial=sharedMaterial;
            screen.shadowCastingMode=ShadowCastingMode.Off;screen.receiveShadows=false;
            screen.lightProbeUsage=LightProbeUsage.Off;screen.reflectionProbeUsage=ReflectionProbeUsage.Off;
            properties=new MaterialPropertyBlock();SetAmmo(0);
        }
        public void SetAmmo(int value)
        {
            value=Mathf.Max(0,value);if(Ammo==value)return;
            Ammo=value;DisplayText=value.ToString("D2",CultureInfo.InvariantCulture);
            var a=Vector4.zero;var b=Vector4.zero;var c=Vector4.zero;
            for(int i=0;i<DisplayText.Length;i++)
                if(i<4)a[i]=DisplayText[i]-'0';else if(i<8)b[i-4]=DisplayText[i]-'0';else c[i-8]=DisplayText[i]-'0';
            properties.SetVector("_DigitsA",a);properties.SetVector("_DigitsB",b);properties.SetVector("_DigitsC",c);
            properties.SetFloat("_DigitCount",DisplayText.Length);properties.SetFloat("_Intensity",intensity);
            screen.SetPropertyBlock(properties);
        }
        public void SetVisible(bool visible){if(screen)screen.enabled=visible;}
        static Mesh BuildMesh()
        {
            // Asset topology/UVs are immutable artwork, not balance parameters. One face,
            // no hidden back/side geometry: the existing receiver encloses the glass edge.
            var points=new[]{new Vector2(-.4f,-.5f),new Vector2(.4f,-.5f),new Vector2(.5f,-.4f),new Vector2(.5f,.4f),new Vector2(.4f,.5f),new Vector2(-.4f,.5f),new Vector2(-.5f,.4f),new Vector2(-.5f,-.4f)};
            var vertices=new Vector3[8];var uv=new Vector2[8];var triangles=new int[18];
            for(int i=0;i<8;i++){vertices[i]=points[i];uv[i]=points[i]+Vector2.one*.5f;}
            for(int i=0;i<6;i++){triangles[i*3]=0;triangles[i*3+1]=i+2;triangles[i*3+2]=i+1;}
            var mesh=new Mesh{name="Vector display clipped glass"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy()
        {
            if(!ownsResources)return;
            if(--owners==0){Destroy(sharedMesh);Destroy(sharedMaterial);sharedMesh=null;sharedMaterial=null;}
        }
    }
}
