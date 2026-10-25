using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTournament.ProvingGround
{
    /// <summary>Pooled green spheres read session state; their radius never determines hits.</summary>
    public sealed class PlasmaPresentation : IDisposable
    {
        sealed class Orb { public GameObject Root; public Transform Core, Shell; public LineRenderer Trail; public double Began; }
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
        // Retention bound only; active effects and authoritative projectiles are never discarded.
        const int RetainedPoolLimit=128;
        public int ActiveCount=>flights.Count+impacts.Count;
        public PlasmaPresentation(NativeCombatSession session,Transform owner,Func<int,Vector3> muzzle=null)
        {
            this.session=session;this.owner=owner;this.muzzle=muzzle;p=session.PlasmaProfile;
            coreMaterial=Material("FORGE white-green core",new Color(.7f,1,.42f),false);
            shellMaterial=new Material(Shader.Find("StarTournament/PlasmaCorona")){name="FORGE green corona"};
            shellMaterial.SetColor("_Tint",new Color(.14f,1,.025f,.7f));
            trailMaterial=Material("FORGE short wake",new Color(.12f,1,.015f,.45f),true);
            session.PlasmaBoltHit+=Contact;session.StateRestored+=Clear;
        }
        static Material Material(string name,Color color,bool transparent)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};m.SetColor("_BaseColor",color);
            if(transparent){m.SetFloat("_Surface",1);m.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);m.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);m.SetFloat("_ZWrite",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;}
            return m;
        }
        Transform Sphere(Transform parent,string name,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Sphere);go.name=name;go.transform.SetParent(parent,false);
            var c=go.GetComponent<Collider>();c.enabled=false;UnityEngine.Object.Destroy(c);
            var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            return go.transform;
        }
        Orb Rent()
        {
            if(pool.Count>0){var old=pool.Pop();old.Root.SetActive(true);return old;}
            var root=new GameObject("FORGE energy sphere");root.transform.SetParent(owner,false);
            var orb=new Orb{Root=root};orb.Core=Sphere(root.transform,"white-hot core",coreMaterial);orb.Shell=Sphere(root.transform,"green corona",shellMaterial);
            var trail=new GameObject("short green wake");trail.transform.SetParent(root.transform,false);orb.Trail=trail.AddComponent<LineRenderer>();
            orb.Trail.sharedMaterial=trailMaterial;orb.Trail.positionCount=2;orb.Trail.useWorldSpace=true;orb.Trail.shadowCastingMode=ShadowCastingMode.Off;
            orb.Trail.startColor=Color.white;orb.Trail.endColor=new Color(1,1,1,0);orb.Trail.numCapVertices=3;
            return orb;
        }
        void Return(Orb orb){orb.Root.SetActive(false);if(pool.Count<RetainedPoolLimit)pool.Push(orb);else UnityEngine.Object.Destroy(orb.Root);}
        void Size(Orb orb,float radius){orb.Core.localScale=Vector3.one*radius;orb.Shell.localScale=Vector3.one*(radius*2);}
        public void Launch(uint id,int shooter)=>pending[id]=shooter;
        Vector3 Offset(uint id,float distance)=>offsets.TryGetValue(id,out var offset)?offset*(1-Mathf.Clamp01(distance/p.Get("plasma.muzzleBlendDistance"))):Vector3.zero;
        void Contact(PlasmaBoltContact contact)
        {
            if(flights.TryGetValue(contact.Bullet.Id,out var prior)){Return(prior);flights.Remove(contact.Bullet.Id);}
            offsets.Remove(contact.Bullet.Id);pending.Remove(contact.Bullet.Id);
            var flash=Rent();flash.Began=contact.Time;flash.Root.transform.position=contact.Contact.Endpoint;flash.Trail.enabled=false;
            Size(flash,p.Get("plasma.impactRadius"));impacts.Add(flash);
        }
        public void Render()
        {
            shellMaterial.SetFloat("_Phase",(float)session.Time*p.Get("plasma.orbPulseHz"));
            var bolts=session.PlasmaBolts;var ids=new HashSet<uint>(bolts.Select(b=>b.Id));
            foreach(var id in flights.Keys.Where(id=>!ids.Contains(id)).ToArray()){Return(flights[id]);flights.Remove(id);offsets.Remove(id);pending.Remove(id);}
            foreach(var b in bolts)
            {
                if(pending.TryGetValue(b.Id,out var shooter)){if(muzzle!=null)offsets[b.Id]=muzzle(shooter)-(b.Position-b.Direction*b.Distance);pending.Remove(b.Id);}
                if(!flights.TryGetValue(b.Id,out var orb))flights[b.Id]=orb=Rent();
                var head=b.Position+Offset(b.Id,b.Distance);orb.Root.transform.position=head;
                float pulse=1+p.Get("plasma.orbPulseAmplitude")*Mathf.Sin((float)session.Time*p.Get("plasma.orbPulseHz")*Mathf.PI*2+b.Id);Size(orb,p.Get("plasma.orbRadius")*pulse);
                float tail=Mathf.Max(0,b.Distance-p.Get("plasma.trailLength"));orb.Trail.enabled=true;
                orb.Trail.startWidth=p.Get("plasma.orbRadius");orb.Trail.endWidth=0;
                orb.Trail.SetPosition(0,head);orb.Trail.SetPosition(1,b.Position-b.Direction*(b.Distance-tail)+Offset(b.Id,tail));
            }
            for(int i=impacts.Count-1;i>=0;i--){var flash=impacts[i];float age=(float)((session.Time-flash.Began)/p.Get("plasma.impactSeconds"));if(age>=1){Return(flash);impacts.RemoveAt(i);}else Size(flash,p.Get("plasma.impactRadius")*(1-age));}
        }
        void Clear(){foreach(var orb in flights.Values)Return(orb);flights.Clear();foreach(var orb in impacts)Return(orb);impacts.Clear();pending.Clear();offsets.Clear();}
        public void Dispose(){session.PlasmaBoltHit-=Contact;session.StateRestored-=Clear;Clear();while(pool.Count>0)UnityEngine.Object.Destroy(pool.Pop().Root);UnityEngine.Object.Destroy(coreMaterial);UnityEngine.Object.Destroy(shellMaterial);UnityEngine.Object.Destroy(trailMaterial);}
    }
}
