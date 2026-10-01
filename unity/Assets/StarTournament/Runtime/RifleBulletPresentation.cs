using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Flight and contacts derive only from authoritative state/events, never from a visual collision.</summary>
    public sealed class RifleBulletPresentation : IDisposable
    {
        sealed class Impact { public GameObject Root; public double Time; }
        struct LaunchOffset { public Vector3 Value; public float BlendDistance; }
        readonly NativeCombatSession session;
        readonly ProvingProfile profile;
        readonly Transform owner;
        readonly Func<int,Vector3> muzzle;
        readonly Dictionary<uint,int> pendingLaunches=new Dictionary<uint,int>();
        readonly Material material;
        readonly Dictionary<uint,LineRenderer> flights=new Dictionary<uint,LineRenderer>();
        readonly Dictionary<uint,LaunchOffset> offsets=new Dictionary<uint,LaunchOffset>();
        readonly List<Impact> impacts=new List<Impact>();
        public int ActiveCount=>flights.Count+impacts.Count;
        public RifleBulletPresentation(NativeCombatSession session,ProvingProfile profile,Transform owner,Func<int,Vector3> muzzle=null)
        {
            this.session=session;this.profile=profile;this.owner=owner;this.muzzle=muzzle;
            var color=new Color(P("rifleTracerRed"),P("rifleTracerGreen"),P("rifleTracerBlue"));
            material=new Material(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color")){name="Rifle bullet",color=color};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            session.RifleBulletHit+=Contact;session.StateRestored+=Clear;
        }
        float P(string key)=>profile.Get("presentation."+key);
        public void Launch(uint id,int shooter)=>pendingLaunches[id]=shooter;
        public Vector3 VisualPoint(RifleBulletState b,float distance)=>b.Position-b.Direction*(b.Distance-distance)+Offset(b.Id,distance);
        Vector3 Offset(uint id,float distance)=>offsets.TryGetValue(id,out var offset)?offset.Value*(1-Mathf.Clamp01(distance/offset.BlendDistance)):Vector3.zero;
        void Contact(RifleBulletContact e)
        {
            if(flights.TryGetValue(e.Bullet.Id,out var line)){line.gameObject.SetActive(false);UnityEngine.Object.Destroy(line.gameObject);flights.Remove(e.Bullet.Id);}
            offsets.Remove(e.Bullet.Id);pendingLaunches.Remove(e.Bullet.Id);
            var root=GameObject.CreatePrimitive(PrimitiveType.Sphere);root.name="Rifle contact "+e.Bullet.Id;root.transform.SetParent(owner);
            var collider=root.GetComponent<Collider>();collider.enabled=false;UnityEngine.Object.Destroy(collider);
            root.GetComponent<Renderer>().sharedMaterial=material;
            root.transform.position=e.Contact.Endpoint;
            root.transform.localScale=Vector3.one*P("shotImpactSize");
            impacts.Add(new Impact{Root=root,Time=e.Time});
        }
        public void Render()
        {
            var bullets=session.RifleBullets;var ids=new HashSet<uint>(bullets.Select(b=>b.Id));
            foreach(var id in flights.Keys.Where(id=>!ids.Contains(id)).ToArray()){UnityEngine.Object.Destroy(flights[id].gameObject);flights.Remove(id);}
            foreach(var id in offsets.Keys.Where(id=>!ids.Contains(id)).ToArray())offsets.Remove(id);
            foreach(var b in bullets)
            {
                if(pendingLaunches.TryGetValue(b.Id,out var shooter))
                {
                    // The first rendered segment must be linear back to the barrel even when
                    // several fixed ticks carry the head beyond the normal tracer length.
                    if(muzzle!=null)offsets[b.Id]=new LaunchOffset{Value=muzzle(shooter)-(b.Position-b.Direction*b.Distance),BlendDistance=Mathf.Max(P("rifleTracerLength"),b.Distance)};
                    // At distance zero the segment is invisible. Freeze its launch origin only
                    // on the first visible frame: a strafe/recoil tick can move the barrel first.
                    if(b.Distance>0)pendingLaunches.Remove(b.Id);
                }
                if(!flights.TryGetValue(b.Id,out var line))
                {
                    var root=new GameObject("Rifle flight "+b.Id);root.transform.SetParent(owner);
                    flights[b.Id]=line=root.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=2;line.useWorldSpace=true;
                    line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                line.startWidth=line.endWidth=P("rifleTracerWidth");
                float tail=Mathf.Min(b.Distance,Mathf.Max(P("shotMuzzleClearance"),b.Distance-P("rifleTracerLength")));
                line.SetPosition(0,VisualPoint(b,b.Distance));
                line.SetPosition(1,VisualPoint(b,tail));
            }
            for(int i=impacts.Count-1;i>=0;i--)
            {
                var impact=impacts[i];float age=(float)((session.Time-impact.Time)/P("shotImpactSeconds"));
                if(age>=1){UnityEngine.Object.Destroy(impact.Root);impacts.RemoveAt(i);}
                else impact.Root.transform.localScale=Vector3.one*P("shotImpactSize")*(1-Mathf.Clamp01(age));
            }
        }
        void Clear()
        {
            foreach(var line in flights.Values)if(line)UnityEngine.Object.Destroy(line.gameObject);flights.Clear();offsets.Clear();pendingLaunches.Clear();
            foreach(var impact in impacts)if(impact.Root)UnityEngine.Object.Destroy(impact.Root);impacts.Clear();
        }
        public void Dispose(){session.RifleBulletHit-=Contact;session.StateRestored-=Clear;Clear();UnityEngine.Object.Destroy(material);}
    }
}
