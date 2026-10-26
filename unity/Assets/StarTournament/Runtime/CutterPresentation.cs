using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround
{
    /// <summary>SEAM renderer. Authoritative segments/notices only; session time freezes every effect.</summary>
    public sealed class CutterPresentation : IDisposable
    {
        readonly NativeCombatSession session;
        readonly ProvingProfile p;
        readonly GameObject root;
        readonly LineRenderer[] cores,arcs,coronas;
        readonly WeaponModelPresentation[] mounts;
        readonly GameObject[] sources;
        readonly int[] participants;
        readonly Material material,coreMaterial,coronaMaterial,contactMaterial,shieldMaterial;
        readonly Contact[,] contacts;
        readonly Batch[] amberBatches,shieldBatches;
        readonly float coronaScale,contactSeconds,sparkSeconds,sparkReach,shieldRadius,muzzleRadius,shieldRingWidth,muzzleBlend,sparkWidth,contactGlow;
        readonly int sparkCount;
        bool disposed;
        struct Contact {public bool Valid,Shield;public int Life;public double Time;public Vector3 Point,Direction;}
        // One reusable dynamic mesh per colour/layer, regardless of contacts or spark count.
        sealed class Batch
        {
            public readonly Mesh Mesh;public readonly GameObject Object;
            readonly List<Vector3> vertices=new List<Vector3>(2048);
            readonly List<Vector2> uv=new List<Vector2>(2048);
            readonly List<Color> colors=new List<Color>(2048);
            readonly List<int> triangles=new List<int>(3072);
            public Batch(string name,Transform parent,int layer,Material material)
            {
                Object=new GameObject(name);Object.transform.SetParent(parent,false);Object.layer=layer;
                Mesh=new Mesh{name=name};Mesh.MarkDynamic();Object.AddComponent<MeshFilter>().sharedMesh=Mesh;
                var r=Object.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;
            }
            public void Begin(){vertices.Clear();uv.Clear();colors.Clear();triangles.Clear();}
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float alpha)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(1,1));uv.Add(new Vector2(0,1));
                var color=new Color(1,1,1,alpha);for(int i=0;i<4;i++)colors.Add(color);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            public void Disc(Vector3 center,Vector3 side,Vector3 up,float radius,float alpha)
                =>Quad(center-side*radius-up*radius,center+side*radius-up*radius,center+side*radius+up*radius,center-side*radius+up*radius,alpha);
            public void End()
            {
                Mesh.Clear();Mesh.SetVertices(vertices);Mesh.SetUVs(0,uv);Mesh.SetColors(colors);Mesh.SetTriangles(triangles,0);Mesh.RecalculateBounds();Object.SetActive(vertices.Count>0);
            }
        }
        public CutterPresentation(NativeCombatSession session,Transform owner,GameObject[] bodies,GameObject[] views,int[] local)
        {
            this.session=session;p=session.CutterProfile;
            var defaults=ProvingProfile.CreateCutterDefault();
            float Read(string name){string path="cutter.seam."+name;return p.Descriptors.Any(d=>d.Path==path)?p.Get(path):defaults.Get(path);}
            coronaScale=Read("coronaScale");contactSeconds=Read("contactSeconds");sparkSeconds=Read("sparkSeconds");sparkReach=Read("sparkReach");
            sparkCount=(int)Read("sparkCount");shieldRadius=Read("shieldRadius");muzzleRadius=Read("muzzleRadius");shieldRingWidth=Read("shieldRingWidth");muzzleBlend=Read("muzzleBlendDistance");sparkWidth=Read("sparkWidth");contactGlow=Read("contactGlow");
            root=new GameObject("Cutter beams");root.transform.SetParent(owner,false);
            var shader=Resources.Load<Shader>("CutterBeam");
            var color=new Color(p.Get("cutter.red"),p.Get("cutter.green"),p.Get("cutter.blue"));
            Material Make(Color tint,bool radial=false){var m=new Material(shader);m.SetColor("_Tint",tint);m.SetFloat("_NearFade",p.Get("cutter.nearFade"));m.SetFloat("_Radial",radial?1:0);return m;}
            var glow=color*p.Get("cutter.glow");glow.a=1;material=Make(glow);
            var core=Color.Lerp(color,Color.white,p.Get("cutter.coreWhiten"))*p.Get("cutter.glow");core.a=1;coreMaterial=Make(core);
            glow.a=Read("coronaAlpha");coronaMaterial=Make(glow);glow.a=1;contactMaterial=Make(glow,true);
            shieldMaterial=Make(new Color(Read("shieldRed"),Read("shieldGreen"),Read("shieldBlue"),1),true);
            sources=bodies.Concat(views).ToArray();participants=Enumerable.Range(0,bodies.Length).Concat(local).ToArray();
            mounts=sources.Select(s=>s.GetComponent<WeaponModelPresentation>()).ToArray();
            contacts=new Contact[bodies.Length,bodies.Length];
            cores=new LineRenderer[sources.Length];arcs=new LineRenderer[sources.Length];coronas=new LineRenderer[sources.Length];
            amberBatches=new Batch[sources.Length];shieldBatches=new Batch[sources.Length];
            for(int i=0;i<sources.Length;i++)
            {
                int layer=sources[i].layer;
                cores[i]=Line("Core "+i,3,p.Get("cutter.width"),coreMaterial,layer);
                coronas[i]=Line("Corona "+i,3,p.Get("cutter.width")*coronaScale,coronaMaterial,layer);
                arcs[i]=Line("Arc "+i,(int)p.Get("cutter.segments")+1,p.Get("cutter.arcWidth"),material,layer);
                amberBatches[i]=new Batch("SEAM contacts "+i,root.transform,layer,contactMaterial);
                shieldBatches[i]=new Batch("SEAM shield "+i,root.transform,layer,shieldMaterial);
            }
            session.Damaged+=Observe;session.StateRestored+=Clear;
        }
        LineRenderer Line(string name,int count,float width,Material mat,int layer)
        {
            var obj=new GameObject(name);obj.transform.SetParent(root.transform,false);obj.layer=layer;var line=obj.AddComponent<LineRenderer>();
            line.sharedMaterial=mat;line.useWorldSpace=true;line.positionCount=count;line.startWidth=line.endWidth=width;
            line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;line.receiveShadows=false;return line;
        }
        void Observe(DamageNotice n)
        {
            if(disposed||!n.Impact.Valid||n.Impact.Weapon!=WeaponId.Cutter||n.HealthLost+n.ArmorLost<=0||n.Shooter<0||n.Shooter>=contacts.GetLength(0)||n.Participant<0||n.Participant>=contacts.GetLength(1))return;
            contacts[n.Shooter,n.Participant]=new Contact{Valid=true,Shield=n.ArmorLost>0,Life=n.Life,Time=n.Time,Point=n.Impact.Point,Direction=n.Impact.Direction};
        }
        void Clear(){Array.Clear(contacts,0,contacts.Length);}
        static void Basis(Vector3 axis,out Vector3 side,out Vector3 up)
        {
            side=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.right:Vector3.up).normalized;up=Vector3.Cross(side,axis).normalized;
        }
        void Impact(Batch batch,Vector3 point,Vector3 direction,float strength,bool shield,int seed)
        {
            Basis(direction,out var side,out var up);
            float phase=(float)session.Time/sparkSeconds;
            batch.Disc(point-direction*.008f,side,up,p.Get("cutter.impactSize")*.5f,strength*contactGlow);
            if(shield)
            {
                // A local expanding ring, not a full body shield. Straight beam remains untouched.
                float radius=shieldRadius*(.65f+.35f*Mathf.Repeat(phase,1));
                const int facets=32;
                for(int k=0;k<facets;k++)
                {
                    float a=k*Mathf.PI*2/facets,b=(k+1)*Mathf.PI*2/facets;
                    var da=side*Mathf.Cos(a)+up*Mathf.Sin(a);var db=side*Mathf.Cos(b)+up*Mathf.Sin(b);
                    batch.Quad(point+da*radius,point+db*radius,point+db*(radius+shieldRingWidth),point+da*(radius+shieldRingWidth),strength*.65f);
                }
                return;
            }
            for(int k=0;k<sparkCount;k++)
            {
                float t=Mathf.Repeat(phase+k*.618034f+seed*.137f,1);
                float angle=k*2.399963f+seed;
                var radial=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                var velocity=radial*(.35f+.65f*Mathf.Abs(Mathf.Sin(k*17.17f+seed)))-direction*.28f;
                var end=point+velocity*sparkReach*t;var start=point+velocity*sparkReach*Mathf.Max(0,t-.18f);
                var width=Vector3.Cross(direction,velocity).normalized*sparkWidth;
                batch.Quad(start-width,end-width,end+width,start+width,strength*(1-t));
            }
        }
        public void Render()
        {
            if(disposed)return;
            for(int i=0;i<cores.Length;i++)
            {
                var state=session.Beam(participants[i]);var amber=amberBatches[i];var shield=shieldBatches[i];amber.Begin();shield.Begin();
                bool visible=state.Active&&sources[i]&&sources[i].activeInHierarchy;
                cores[i].enabled=arcs[i].enabled=coronas[i].enabled=visible;
                if(visible)
                {
                    var start=mounts[i]?mounts[i].CutterMuzzle:state.Origin;
                    var axis=(state.Endpoint-state.Origin).normalized;Basis(axis,out var side,out var up);
                    float length=Vector3.Distance(state.Origin,state.Endpoint);
                    // Viewmodel parallax joins the real ray near the muzzle, then remains collinear
                    // with authoritative contacts. No hit query or simulation endpoint is changed.
                    float merge=Mathf.Min(length,Mathf.Max(muzzleBlend,Vector3.Dot(start-state.Origin,axis)+muzzleBlend));
                    var junction=state.Origin+axis*merge;
                    cores[i].SetPosition(0,start);cores[i].SetPosition(1,junction);cores[i].SetPosition(2,state.Endpoint);
                    coronas[i].SetPosition(0,start);coronas[i].SetPosition(1,junction);coronas[i].SetPosition(2,state.Endpoint);
                    float first=Vector3.Distance(start,junction),total=first+Vector3.Distance(junction,state.Endpoint);
                    for(int k=0;k<arcs[i].positionCount;k++)
                    {
                        float t=(float)k/(arcs[i].positionCount-1);
                        float phase=Mathf.Floor((float)session.Time*p.Get("cutter.frequency"))*78.233f+k*12.9898f;
                        var offset=(side*Mathf.Sin(phase)+up*Mathf.Cos(phase))*p.Get("cutter.jitter")*Mathf.Sin(t*Mathf.PI);
                        float distance=t*total;
                        var center=distance<=first?Vector3.Lerp(start,junction,first>0?distance/first:1):Vector3.Lerp(junction,state.Endpoint,total>first?(distance-first)/(total-first):1);
                        arcs[i].SetPosition(k,center+offset);
                    }
                    amber.Disc(start,side,up,muzzleRadius,1);
                    if(state.Wall)Impact(amber,state.Endpoint,axis,1,false,participants[i]);
                    for(int target=0;target<contacts.GetLength(1);target++)
                    {
                        var c=contacts[participants[i],target];double age=session.Time-c.Time;
                        if(!c.Valid||age<0||age>contactSeconds||session.Life(target).Life!=c.Life)continue;
                        float strength=1-(float)(age/contactSeconds);
                        Impact(c.Shield?shield:amber,c.Point,c.Direction,strength,c.Shield,target);
                    }
                }
                // Batch vertices are authored in world coordinates; compensate for owner motion.
                amber.Object.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);amber.Object.transform.localScale=Vector3.one;
                shield.Object.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);shield.Object.transform.localScale=Vector3.one;
                amber.End();shield.End();
            }
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;session.Damaged-=Observe;session.StateRestored-=Clear;
            foreach(var b in amberBatches)Object.Destroy(b.Mesh);foreach(var b in shieldBatches)Object.Destroy(b.Mesh);
            Object.Destroy(root);Object.Destroy(material);Object.Destroy(coreMaterial);Object.Destroy(coronaMaterial);Object.Destroy(contactMaterial);Object.Destroy(shieldMaterial);
        }
    }
}
