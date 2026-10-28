using System.Globalization;
using UnityEngine;
using UnityEngine.Rendering;
namespace StarTournament.ProvingGround
{
    /// <summary>COBALT model attachment. Reads ammo and simulation time; never owns gameplay state.</summary>
    public sealed class RifleAmmoDisplay : MonoBehaviour
    {
        // Bounded immutable art cache shared by all seats/world models for this runtime.
        static Mesh sharedMesh;
        static Material sharedMaterial;
        MeshRenderer screen;
        MaterialPropertyBlock properties;
        float intensity,drainSeconds,fillSeconds,pulseGain,from,target,duration,pulse;
        double began,lastTime;
        float sentFraction=float.NaN,sentPulse=float.NaN;
        int life=-1;
        public int Ammo {get;private set;}=-1;
        public string DisplayText {get;private set;}="";
        public float Fraction {get;private set;}
        public float TargetFraction=>target;
        // The normalized three-color ramp is the approved artwork, not a gameplay threshold.
        public Color BarColor=>Color.Lerp(Color.red,Color.Lerp(Color.yellow,new Color(.45f,1,.015f),Mathf.Clamp01((Fraction-.5f)*2)),Mathf.Clamp01(Fraction*2));
        public Renderer Screen=>screen?screen:GetComponent<Renderer>();
        public void Initialize(Transform weapon,ProvingProfile profile,int layer)
        {
            transform.SetParent(weapon,false);gameObject.layer=layer;
            ProvingProfile defaults=null;
            float P(string key)=>profile.Descriptor(ProvingProfile.RifleDisplayPrefix+key)!=null?profile.Get(ProvingProfile.RifleDisplayPrefix+key):(defaults??(defaults=ProvingProfile.CreateDefault())).Get(ProvingProfile.RifleDisplayPrefix+key);
            transform.localPosition=new Vector3(P("centerX"),P("centerY"),P("centerZ"));
            // Local +X runs towards the stock; -Z is the front-face normal of our mesh.
            float tilt=P("tilt")*Mathf.Deg2Rad;
            transform.localRotation=Quaternion.LookRotation(new Vector3(Mathf.Cos(tilt),-Mathf.Sin(tilt),0),new Vector3(Mathf.Sin(tilt),Mathf.Cos(tilt),0));
            transform.localScale=new Vector3(P("width"),P("height"),1);
            intensity=P("intensity");drainSeconds=P("drainSeconds");fillSeconds=P("fillSeconds");pulseGain=P("pulseGain");
            if(!sharedMesh)sharedMesh=BuildMesh();
            if(!sharedMaterial)sharedMaterial=new Material(Resources.Load<Shader>("RifleDisplay")){name="COBALT integrated ammo glass",hideFlags=HideFlags.DontSave};
            gameObject.AddComponent<MeshFilter>().sharedMesh=sharedMesh;
            screen=gameObject.AddComponent<MeshRenderer>();screen.sharedMaterial=sharedMaterial;
            screen.shadowCastingMode=ShadowCastingMode.Off;screen.receiveShadows=false;
            screen.lightProbeUsage=LightProbeUsage.Off;screen.reflectionProbeUsage=ReflectionProbeUsage.Off;
            properties=new MaterialPropertyBlock();
        }
        public void Render(int ammo,float capacity,double time,int lifeId)
        {
            if(!screen)return;
            ammo=Mathf.Max(0,ammo);float next=capacity>0?Mathf.Clamp01(ammo/capacity):0;
            bool first=Ammo<0,rewound=!first&&time<lastTime,respawn=!first&&lifeId!=life;
            if(first||rewound)
            {from=target=Fraction=next;began=time;duration=0;pulse=0;}
            else
            {
                Evaluate(time);
                if(ammo!=Ammo||next!=target||respawn)
                {
                    from=respawn?0:Fraction;target=next;began=time;
                    duration=next>=from?fillSeconds:drainSeconds;pulse=pulseGain;
                }
            }
            bool digitsChanged=ammo!=Ammo;
            if(digitsChanged)
            {
                Ammo=ammo;DisplayText=ammo.ToString("D2",CultureInfo.InvariantCulture);
                var a=Vector4.zero;var b=Vector4.zero;var c=Vector4.zero;
                for(int i=0;i<DisplayText.Length;i++)
                    if(i<4)a[i]=DisplayText[i]-'0';else if(i<8)b[i-4]=DisplayText[i]-'0';else c[i-8]=DisplayText[i]-'0';
                properties.SetVector("_DigitsA",a);properties.SetVector("_DigitsB",b);properties.SetVector("_DigitsC",c);properties.SetFloat("_DigitCount",DisplayText.Length);
            }
            Evaluate(time);life=lifeId;lastTime=time;
            float response=pulse*(1-Mathf.Clamp01((float)(time-began)/drainSeconds));
            if(digitsChanged||Fraction!=sentFraction||response!=sentPulse)
            {
                properties.SetFloat("_Fill",Fraction);properties.SetColor("_BarColor",BarColor);
                properties.SetFloat("_Intensity",intensity);properties.SetFloat("_Pulse",response);
                screen.SetPropertyBlock(properties);sentFraction=Fraction;sentPulse=response;
            }
        }
        void Evaluate(double time)
        {
            float t=duration>0?Mathf.Clamp01((float)(time-began)/duration):1;
            Fraction=Mathf.Lerp(from,target,t*t*(3-2*t));
        }
        public void SetVisible(bool visible){if(Screen)Screen.enabled=visible;}
        static Mesh BuildMesh()
        {
            // Authored topology/UV artwork: ten shader segments, clipped bezel, 6 mm enclosure.
            // No hidden back face: the receiver encloses it. These are asset invariants.
            var corners=new[]{new Vector2(-.47f,-.5f),new Vector2(.47f,-.5f),new Vector2(.5f,-.32f),new Vector2(.5f,.32f),new Vector2(.47f,.5f),new Vector2(-.47f,.5f),new Vector2(-.5f,.32f),new Vector2(-.5f,-.32f)};
            var vertices=new Vector3[24];var uv=new Vector2[24];var triangles=new int[66];
            for(int i=0;i<8;i++)
            {
                vertices[i]=corners[i];uv[i]=corners[i]+Vector2.one*.5f;
                vertices[8+i]=corners[i];vertices[16+i]=new Vector3(corners[i].x,corners[i].y,.006f);
                uv[8+i]=uv[16+i]=new Vector2(-1,-1);
                int next=(i+1)%8,k=18+i*6;
                triangles[k]=8+i;triangles[k+1]=8+next;triangles[k+2]=16+i;
                triangles[k+3]=8+next;triangles[k+4]=16+next;triangles[k+5]=16+i;
            }
            for(int i=0;i<6;i++){triangles[i*3]=0;triangles[i*3+1]=i+2;triangles[i*3+2]=i+1;}
            var mesh=new Mesh{name="COBALT integrated strip enclosure",hideFlags=HideFlags.DontSave};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
