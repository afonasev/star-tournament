using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    /// <summary>Authored plasma surfaces and pooled contact/muzzle filaments. Session clock only.</summary>
    public sealed class PlasmaPresentation : IDisposable
    {
        sealed class Orb
        {
            public GameObject Root; public Transform Core, Shell; public LineRenderer Trail;
            public LineRenderer[] Filaments; public double Began; public bool Muzzle; public int Shooter;
            public Vector3 Direction;
        }
        readonly NativeCombatSession session;
        readonly ProvingProfile p;
        readonly Transform owner;
        readonly Func<int,Vector3> muzzle;
        readonly Material coreMaterial,shellMaterial,trailMaterial;
        readonly Dictionary<uint,Orb> flights=new Dictionary<uint,Orb>();
        readonly Dictionary<uint,int> pending=new Dictionary<uint,int>();
        readonly Dictionary<uint,Vector3> offsets=new Dictionary<uint,Vector3>();
        readonly List<Orb> impacts=new List<Orb>();
        readonly Stack<Orb> pool=new Stack<Orb>();
        readonly HashSet<uint> liveIds=new HashSet<uint>();
        readonly List<uint> removed=new List<uint>();
        readonly Vector3[] filamentPositions=new Vector3[9],trailPositions=new Vector3[5];
        const int RetainedPoolLimit=128;
        public int ActiveCount=>flights.Count+impacts.Count;
        public PlasmaPresentation(NativeCombatSession session,Transform owner,Func<int,Vector3> muzzle=null)
        {
            this.session=session;this.owner=owner;this.muzzle=muzzle;p=session.PlasmaProfile;
            coreMaterial=new Material(Shader.Find("StarTournament/PlasmaCorona")){name="FORGE incandescent kernel"};
            coreMaterial.SetColor("_Tint",new Color(.88f,1,.68f,1));coreMaterial.SetFloat("_Filaments",0);
            shellMaterial=new Material(Shader.Find("StarTournament/PlasmaCorona")){name="FORGE turbulent green plasma"};
            shellMaterial.SetColor("_Tint",new Color(.09f,1,.012f,.64f));
            trailMaterial=new Material(Shader.Find("StarTournament/PlasmaFilament")){name="FORGE soft ion filaments"};
            trailMaterial.SetColor("_Tint",new Color(.35f,1,.08f,.75f));
            session.PlasmaBoltHit+=Contact;session.StateRestored+=Clear;
        }
        Transform Sphere(Transform parent,string name,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(parent,false);
            var c=go.GetComponent<Collider>();c.enabled=false;UnityEngine.Object.Destroy(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            return go.transform;
        }
        LineRenderer Line(Transform parent,string name,int count)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=trailMaterial;line.positionCount=count;line.useWorldSpace=false;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            line.startColor=Color.white;line.endColor=new Color(1,1,1,0);line.numCapVertices=3;return line;
        }
        Orb Rent()
        {
            Orb orb;
            if(pool.Count>0){orb=pool.Pop();orb.Root.SetActive(true);}
            else
            {
                var root=new GameObject("FORGE energy sphere");root.transform.SetParent(owner,false);
                orb=new Orb{Root=root};orb.Core=Sphere(root.transform,"incandescent kernel",coreMaterial);orb.Shell=Sphere(root.transform,"turbulent corona",shellMaterial);
                orb.Trail=Line(root.transform,"short tapered wake",5);orb.Trail.useWorldSpace=true;
                orb.Filaments=new LineRenderer[8];for(int i=0;i<orb.Filaments.Length;i++)orb.Filaments[i]=Line(root.transform,"plasma filament "+i,9);
            }
            orb.Root.transform.localScale=Vector3.one;orb.Root.transform.rotation=Quaternion.identity;orb.Core.localPosition=orb.Shell.localPosition=Vector3.zero;
            orb.Muzzle=false;orb.Trail.enabled=true;foreach(var f in orb.Filaments)f.enabled=true;return orb;
        }
        void Return(Orb orb){orb.Root.SetActive(false);if(pool.Count<RetainedPoolLimit)pool.Push(orb);else UnityEngine.Object.Destroy(orb.Root);}
        void Size(Orb orb,float radius){orb.Core.localScale=Vector3.one*(radius*1.1f);orb.Shell.localScale=Vector3.one*(radius*2);}
        public void Launch(uint id,int shooter)=>pending[id]=shooter;
        Vector3 Offset(uint id,float distance)=>offsets.TryGetValue(id,out var offset)?offset*(1-Mathf.Clamp01(distance/p.Get("plasma.muzzleBlendDistance"))):Vector3.zero;
        void Contact(PlasmaBoltContact contact)
        {
            if(flights.TryGetValue(contact.Bullet.Id,out var prior)){Return(prior);flights.Remove(contact.Bullet.Id);}
            offsets.Remove(contact.Bullet.Id);pending.Remove(contact.Bullet.Id);
            var flash=Rent();flash.Began=contact.Time;flash.Trail.enabled=false;
            var normal=contact.Contact.Normal.sqrMagnitude>.001f?contact.Contact.Normal:-contact.Bullet.Direction;
            flash.Root.transform.SetPositionAndRotation(contact.Contact.Endpoint+normal*.006f,Quaternion.LookRotation(normal));
            impacts.Add(flash);
        }
        void Arcs(Orb orb,float radius,float phase)
        {
            for(int j=0;j<orb.Filaments.Length;j++)
            {
                var f=orb.Filaments[j];f.enabled=j<3;if(!f.enabled)continue;
                f.startWidth=radius*.035f;f.endWidth=radius*.005f;f.startColor=new Color(.7f,1,.35f,.5f);f.endColor=new Color(.2f,1,.05f,.1f);
                var rotation=Quaternion.Euler(j*67,phase*17+j*47,j*59);
                for(int k=0;k<9;k++){float a=phase+j*2.1f+k*.48f;float r=radius*(.87f+.08f*Mathf.Sin(k*2+phase));filamentPositions[k]=rotation*new Vector3(Mathf.Cos(a)*r,Mathf.Sin(a)*r,Mathf.Sin(a*2)*radius*.15f);}
                f.SetPositions(filamentPositions);
            }
        }
        void Flash(Orb flash,float age)
        {
            float radius=p.Get("plasma.impactRadius")*(flash.Muzzle?.7f:1);
            float fade=1-age;
            Size(flash,radius*fade);
            // Surface-local lens, not a spherical explosion or gameplay splash.
            flash.Shell.localScale=new Vector3(radius*1.6f*fade,radius*1.6f*fade,radius*(flash.Muzzle?.7f:.25f)*fade);
            flash.Core.localScale=new Vector3(radius*.6f*fade,radius*.6f*fade,radius*.2f*fade);
            for(int j=0;j<flash.Filaments.Length;j++)
            {
                var f=flash.Filaments[j];float a=j*Mathf.PI*.25f;
                f.startWidth=radius*.055f*fade;f.endWidth=0;f.startColor=new Color(.7f,1,.28f,fade);f.endColor=new Color(.12f,1,.02f,0);
                for(int k=0;k<9;k++)
                {
                    float t=k/8f;float reach=radius*(.4f+age*2.7f)*t;
                    float wiggle=Mathf.Sin(j*7+k*2.4f)*radius*.12f*t*fade;
                    filamentPositions[k]=new Vector3(Mathf.Cos(a)*reach-Mathf.Sin(a)*wiggle,Mathf.Sin(a)*reach+Mathf.Cos(a)*wiggle,radius*(Mathf.Sin(t*Mathf.PI)*.25f+age*t*.35f));
                }
                f.SetPositions(filamentPositions);
            }
        }
        static readonly Unity.Profiling.ProfilerMarker renderMarker=new Unity.Profiling.ProfilerMarker("ST.Plasma.Render");
        public void Render() {using(renderMarker.Auto())RenderCore();}
        void RenderCore()
        {
            float phase=(float)session.Time*p.Get("plasma.orbPulseHz");shellMaterial.SetFloat("_Phase",phase);
            var bolts=session.PlasmaBolts;liveIds.Clear();foreach(var b in bolts)liveIds.Add(b.Id);
            removed.Clear();foreach(var id in flights.Keys)if(!liveIds.Contains(id))removed.Add(id);
            foreach(var id in removed){Return(flights[id]);flights.Remove(id);offsets.Remove(id);pending.Remove(id);}
            foreach(var b in bolts)
            {
                if(pending.TryGetValue(b.Id,out var shooter))
                {
                    if(muzzle!=null)
                    {
                        offsets[b.Id]=muzzle(shooter)-(b.Position-b.Direction*b.Distance);
                        var flash=Rent();flash.Muzzle=true;flash.Shooter=shooter;flash.Direction=b.Direction;flash.Began=session.Time;flash.Trail.enabled=false;impacts.Add(flash);
                    }
                    pending.Remove(b.Id);
                }
                if(!flights.TryGetValue(b.Id,out var orb))flights[b.Id]=orb=Rent();
                var head=b.Position+Offset(b.Id,b.Distance);orb.Root.transform.position=head;
                float pulse=1+p.Get("plasma.orbPulseAmplitude")*Mathf.Sin(phase*Mathf.PI*2+b.Id);float radius=p.Get("plasma.orbRadius")*pulse;Size(orb,radius);Arcs(orb,radius,phase+b.Id);
                float tail=Mathf.Max(0,b.Distance-p.Get("plasma.trailLength"));orb.Trail.enabled=true;orb.Trail.startWidth=radius*.9f;orb.Trail.endWidth=0;
                for(int k=0;k<5;k++){float distance=Mathf.Lerp(b.Distance,tail,k/4f);trailPositions[k]=b.Position-b.Direction*(b.Distance-distance)+Offset(b.Id,distance);}
                orb.Trail.SetPositions(trailPositions);
            }
            for(int i=impacts.Count-1;i>=0;i--)
            {
                var flash=impacts[i];float age=(float)((session.Time-flash.Began)/(p.Get("plasma.impactSeconds")*(flash.Muzzle?.45f:1)));
                if(age>=1){Return(flash);impacts.RemoveAt(i);continue;}
                if(flash.Muzzle)flash.Root.transform.SetPositionAndRotation(muzzle(flash.Shooter),Quaternion.LookRotation(flash.Direction));
                Flash(flash,age);
            }
        }
        void Clear(){foreach(var orb in flights.Values)Return(orb);flights.Clear();foreach(var orb in impacts)Return(orb);impacts.Clear();pending.Clear();offsets.Clear();}
        public void Dispose(){session.PlasmaBoltHit-=Contact;session.StateRestored-=Clear;Clear();while(pool.Count>0)UnityEngine.Object.Destroy(pool.Pop().Root);UnityEngine.Object.Destroy(coreMaterial);UnityEngine.Object.Destroy(shellMaterial);UnityEngine.Object.Destroy(trailMaterial);}
    }
}
