using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Derived visuals only. Session clock freezes every effect; restore discards transient events.</summary>
    public sealed class RocketPresentation : IDisposable
    {
        sealed class Flight { public GameObject Root; public LineRenderer Trail; }
        sealed class Burst { public GameObject Root; public LineRenderer[] Rings; public double Time; public Vector3 Center; }
        readonly NativeCombatSession session;
        readonly ProvingProfile profile;
        readonly Transform owner;
        readonly Dictionary<uint,Flight> flights=new Dictionary<uint,Flight>();
        readonly Dictionary<uint,Vector3> launchOffsets=new Dictionary<uint,Vector3>();
        readonly List<Burst> bursts=new List<Burst>();
        readonly Material material;
        public RocketPresentation(NativeCombatSession session,ProvingProfile profile,Transform owner)
        {
            this.session=session;this.profile=profile;this.owner=owner;
            var color=new Color(P("rocketRed"),P("rocketGreen"),P("rocketBlue"));
            material=new Material(Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color")){name="Pulse impulse",color=color};
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            session.RocketExploded+=Explode;session.StateRestored+=Clear;
        }
        float P(string key)=>profile.Get("presentation."+key);
        LineRenderer Line(GameObject root,int points)
        {
            var line=root.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=points;
            line.startWidth=line.endWidth=P("rocketTrailWidth");line.useWorldSpace=true;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;return line;
        }
        void Explode(RocketExplosion e)
        {
            var root=new GameObject("Pulse explosion "+e.Rocket.Id);root.transform.SetParent(owner);
            // Fixed circle tessellation is geometry construction, not a gameplay/effect count.
            // Three orthogonal great circles make the impulse readable from any camera direction.
            var rings=new LineRenderer[3];
            for(int i=0;i<rings.Length;i++){var circle=new GameObject("Impulse plane "+i);circle.transform.SetParent(root.transform);rings[i]=Line(circle,33);}
            bursts.Add(new Burst{Root=root,Rings=rings,Time=e.Time,Center=e.Position});
        }
        public void Launch(uint id,Vector3 offset)=>launchOffsets[id]=offset;
        Vector3 Offset(uint id,float distance)=>launchOffsets.TryGetValue(id,out var offset)
            ?offset*(1-Mathf.Clamp01(distance/P("rocketMuzzleBlendDistance"))):Vector3.zero;
        public void Render()
        {
            var rockets=session.Rockets;var ids=new HashSet<uint>(rockets.Select(r=>r.Id));
            foreach(var id in flights.Keys.Where(id=>!ids.Contains(id)).ToArray()){UnityEngine.Object.Destroy(flights[id].Root);flights.Remove(id);launchOffsets.Remove(id);}
            foreach(var id in launchOffsets.Keys.Where(id=>!ids.Contains(id)).ToArray())launchOffsets.Remove(id);
            foreach(var r in rockets)
            {
                if(!flights.TryGetValue(r.Id,out var flight))
                {
                    var root=GameObject.CreatePrimitive(PrimitiveType.Capsule);root.name="Pulse rocket "+r.Id;root.transform.SetParent(owner);
                    var collider=root.GetComponent<Collider>();collider.enabled=false;UnityEngine.Object.Destroy(collider);
                    root.GetComponent<Renderer>().sharedMaterial=material;
                    var trail=new GameObject("Pulse trail");trail.transform.SetParent(root.transform);
                    flights[r.Id]=flight=new Flight{Root=root,Trail=Line(trail,2)};
                }
                var position=r.Position+Offset(r.Id,r.Distance);
                flight.Root.transform.position=position;
                flight.Root.transform.rotation=Quaternion.LookRotation(r.Direction)*Quaternion.Euler(90,0,0);
                flight.Root.transform.localScale=new Vector3(P("rocketBodyRadius")*2,P("rocketBodyLength")/2,P("rocketBodyRadius")*2);
                float length=Mathf.Min(r.Distance,P("rocketTrailLength"));
                flight.Trail.SetPosition(0,position);
                flight.Trail.SetPosition(1,r.Position-r.Direction*length+Offset(r.Id,r.Distance-length));
            }
            for(int i=bursts.Count-1;i>=0;i--)
            {
                var b=bursts[i];float t=(float)((session.Time-b.Time)/P("rocketExplosionSeconds"));
                if(t>=1){UnityEngine.Object.Destroy(b.Root);bursts.RemoveAt(i);continue;}
                float radius=P("rocketExplosionSize")*Mathf.Sqrt(Mathf.Clamp01(t));
                for(int plane=0;plane<b.Rings.Length;plane++)
                {
                    var ring=b.Rings[plane];
                    for(int j=0;j<ring.positionCount;j++)
                    {
                        float a=j*Mathf.PI*2/(ring.positionCount-1),c=Mathf.Cos(a),s=Mathf.Sin(a);
                        var direction=plane==0?new Vector3(c,s,0):plane==1?new Vector3(c,0,s):new Vector3(0,c,s);
                        ring.SetPosition(j,b.Center+direction*radius);
                    }
                    ring.startWidth=ring.endWidth=P("rocketTrailWidth")*(1-t);
                }
            }
        }
        void Clear()
        {
            launchOffsets.Clear();
            foreach(var f in flights.Values)if(f.Root)UnityEngine.Object.Destroy(f.Root);flights.Clear();
            foreach(var b in bursts)if(b.Root)UnityEngine.Object.Destroy(b.Root);bursts.Clear();
        }
        public void Dispose(){session.RocketExploded-=Explode;session.StateRestored-=Clear;Clear();UnityEngine.Object.Destroy(material);}
    }
}
