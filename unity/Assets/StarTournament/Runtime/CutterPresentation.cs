using UnityEngine;
namespace StarTournament.ProvingGround
{
    /// <summary>Bounded straight lightning, driven solely by authoritative segments and session time.</summary>
    public sealed class CutterPresentation : System.IDisposable
    {
        readonly NativeCombatSession session;
        readonly ProvingProfile p;
        readonly GameObject root;
        readonly LineRenderer[] cores,arcs;
        readonly Transform[] impacts;
        readonly Material material,coreMaterial;
        readonly GameObject[] sources;readonly int[] participants;
        public CutterPresentation(NativeCombatSession session,Transform owner,GameObject[] bodies,GameObject[] views,int[] local)
        {
            this.session=session;p=session.CutterProfile;
            root=new GameObject("Cutter beams");root.transform.SetParent(owner,false);
            var shader=Resources.Load<Shader>("CutterBeam");
            material=new Material(shader);coreMaterial=new Material(shader);
            var color=new Color(p.Get("cutter.red"),p.Get("cutter.green"),p.Get("cutter.blue"));
            var coreColor=Color.Lerp(color,Color.white,p.Get("cutter.coreWhiten"))*p.Get("cutter.glow");
            color*=p.Get("cutter.glow");color.a=coreColor.a=1;
            material.SetColor("_Tint",color);coreMaterial.SetColor("_Tint",coreColor);
            material.SetFloat("_NearFade",p.Get("cutter.nearFade"));coreMaterial.SetFloat("_NearFade",p.Get("cutter.nearFade"));
            sources=new GameObject[bodies.Length+views.Length];participants=new int[sources.Length];
            for(int i=0;i<bodies.Length;i++){sources[i]=bodies[i];participants[i]=i;}
            for(int i=0;i<views.Length;i++){sources[bodies.Length+i]=views[i];participants[bodies.Length+i]=local[i];}
            cores=new LineRenderer[sources.Length];arcs=new LineRenderer[cores.Length];impacts=new Transform[cores.Length];
            for(int i=0;i<cores.Length;i++)
            {
                cores[i]=Line("Core "+i,2,p.Get("cutter.width"));arcs[i]=Line("Arc "+i,(int)p.Get("cutter.segments")+1,p.Get("cutter.arcWidth"));
                cores[i].sharedMaterial=coreMaterial;
                var impact=GameObject.CreatePrimitive(PrimitiveType.Sphere);Object.Destroy(impact.GetComponent<Collider>());impact.name="Cutter endpoint";impact.transform.SetParent(root.transform,false);impact.GetComponent<Renderer>().sharedMaterial=material;impact.transform.localScale=Vector3.one*p.Get("cutter.impactSize");impacts[i]=impact.transform;
                cores[i].gameObject.layer=arcs[i].gameObject.layer=impact.layer=sources[i].layer;
            }
        }
        LineRenderer Line(string name,int count,float width)
        {
            var obj=new GameObject(name);obj.transform.SetParent(root.transform,false);var line=obj.AddComponent<LineRenderer>();
            line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=count;line.startWidth=line.endWidth=width;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;
        }
        public void Render()
        {
            for(int i=0;i<cores.Length;i++)
            {
                var state=session.Beam(participants[i]);cores[i].enabled=arcs[i].enabled=state.Active;impacts[i].gameObject.SetActive(state.Active&&state.Wall);
                if(!state.Active)continue;
                var start=sources[i].GetComponent<WeaponModelPresentation>()?.CutterMuzzle??state.Origin;
                cores[i].SetPosition(0,start);cores[i].SetPosition(1,state.Endpoint);impacts[i].position=state.Endpoint;
                var axis=(state.Endpoint-start).normalized;var side=Vector3.Cross(axis,Vector3.up).normalized;var up=Vector3.Cross(side,axis);
                for(int k=0;k<arcs[i].positionCount;k++)
                {
                    float t=(float)k/(arcs[i].positionCount-1);
                    // Fixed irrational hash coefficients decorrelate vertices; frequency/jitter are profile controls.
                    float phase=Mathf.Floor((float)session.Time*p.Get("cutter.frequency"))*78.233f+k*12.9898f;
                    // Sine envelopes anchor both ends exactly; no gameplay RNG or extra hit volume.
                    var offset=(side*Mathf.Sin(phase)+up*Mathf.Cos(phase))*p.Get("cutter.jitter")*Mathf.Sin(t*Mathf.PI);
                    arcs[i].SetPosition(k,Vector3.Lerp(start,state.Endpoint,t)+offset);
                }
            }
        }
        public void Dispose(){Object.Destroy(root);Object.Destroy(material);Object.Destroy(coreMaterial);}
    }
}
