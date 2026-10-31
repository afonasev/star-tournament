using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    /// <summary>Read-only instrument inset into the weapon's authored display mount.</summary>
    public sealed class PulseAmmoDisplay : MonoBehaviour
    {
        static Mesh sharedMesh;
        static Material sharedMaterial;
        static int owners;
        MeshRenderer screen;
        MaterialPropertyBlock properties;
        bool ownsResources;
        public int Ammo {get;private set;}=-1;
        public float ReserveFraction {get;private set;}=-1;
        public string DisplayText {get;private set;}="";
        public Renderer Screen=>screen;

        public void Initialize(Transform weapon,ProvingProfile profile,int layer)
        {
            transform.SetParent(weapon,false);gameObject.layer=layer;
            var defaults=ProvingProfile.CreateDefault();
            float P(string key)=>profile.Descriptor(ProvingProfile.PulseDisplayPrefix+key)!=null?
                profile.Get(ProvingProfile.PulseDisplayPrefix+key):defaults.Get(ProvingProfile.PulseDisplayPrefix+key);
            transform.localPosition=new Vector3(0,P("centerY"),P("rearZ"));
            transform.localScale=Vector3.one*P("diameter");
            // Semantic geometry markers define the new side instrument; historical GLBs retain rear mounting.
            var markers=weapon.GetComponentsInChildren<Transform>(true);
            var authored=markers.FirstOrDefault(t=>t.name=="pulse-display-mount");
            var normal=markers.FirstOrDefault(t=>t.name=="pulse-display-normal");
            var up=markers.FirstOrDefault(t=>t.name=="pulse-display-up");
            if(authored&&normal&&up)
            {
                transform.position=authored.position+weapon.TransformVector(new Vector3(0,
                    P("centerY")-defaults.Get(ProvingProfile.PulseDisplayPrefix+"centerY"),
                    P("rearZ")-defaults.Get(ProvingProfile.PulseDisplayPrefix+"rearZ")));
                // Mesh front normal is -Z, so its forward axis points into the housing.
                transform.rotation=Quaternion.LookRotation(authored.position-normal.position,up.position-authored.position);
                transform.localScale=Vector3.one*P("diameter")*(.118f/.144f); // Authored bezel diameter ratio, not gameplay scale.
            }

            if(!sharedMesh)sharedMesh=BuildMesh();
            if(!sharedMaterial)sharedMaterial=new Material(Resources.Load<Shader>("PulseDisplay"))
                {name="Pulse recessed reserve instrument",mainTexture=Resources.Load<Texture2D>("ShotgunDigits")};
            owners++;ownsResources=true;
            gameObject.AddComponent<MeshFilter>().sharedMesh=sharedMesh;
            screen=gameObject.AddComponent<MeshRenderer>();screen.sharedMaterial=sharedMaterial;
            screen.shadowCastingMode=ShadowCastingMode.Off;screen.receiveShadows=false;
            screen.lightProbeUsage=LightProbeUsage.Off;screen.reflectionProbeUsage=ReflectionProbeUsage.Off;
            properties=new MaterialPropertyBlock();
            properties.SetFloat("_Intensity",P("intensity"));properties.SetFloat("_ArcIntensity",P("arcIntensity"));
            properties.SetFloat("_DigitScale",P("digitScale"));SetAmmo(0,1);
        }

        public void SetAmmo(int value,float capacity)
        {
            value=Mathf.Max(0,value);float fraction=capacity>0?Mathf.Clamp01(value/capacity):0;
            if(Ammo==value&&ReserveFraction==fraction)return;
            Ammo=value;ReserveFraction=fraction;DisplayText=value.ToString("D2",CultureInfo.InvariantCulture);
            var a=Vector4.zero;var b=Vector4.zero;var c=Vector4.zero;
            for(int i=0;i<DisplayText.Length;i++)
                if(i<4)a[i]=DisplayText[i]-'0';else if(i<8)b[i-4]=DisplayText[i]-'0';else c[i-8]=DisplayText[i]-'0';
            properties.SetVector("_DigitsA",a);properties.SetVector("_DigitsB",b);properties.SetVector("_DigitsC",c);
            properties.SetFloat("_DigitCount",DisplayText.Length);properties.SetFloat("_Reserve",fraction);
            properties.SetFloat("_Empty",value==0?1:0);screen.SetPropertyBlock(properties);
        }
        public void SetVisible(bool visible){if(screen)screen.enabled=visible;}

        static Mesh BuildMesh()
        {
            // Immutable artwork topology: one front face, no hidden sides/back or collider.
            // 48 sides keep the silhouette smooth at FPS scale, at one shared draw per owner.
            const int segments=48;
            var vertices=new Vector3[segments+1];var uv=new Vector2[segments+1];var indices=new int[segments*3];
            uv[0]=Vector2.one*.5f;
            for(int i=0;i<segments;i++)
            {
                float angle=i*Mathf.PI*2/segments;
                vertices[i+1]=new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0)*.5f;
                uv[i+1]=(Vector2)vertices[i+1]+Vector2.one*.5f;
                indices[i*3]=0;indices[i*3+1]=(i+1)%segments+1;indices[i*3+2]=i+1;
            }
            var mesh=new Mesh{name="Pulse circular reserve glass"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=indices;
            mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy()
        {
            if(!ownsResources)return;
            if(--owners==0){Destroy(sharedMesh);Destroy(sharedMaterial);sharedMesh=null;sharedMaterial=null;}
        }
    }
}
