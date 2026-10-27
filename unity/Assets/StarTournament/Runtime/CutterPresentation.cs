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
        readonly LineRenderer[] cores,arcs,filaments,coronas,muzzleLinks;
        readonly WeaponModelPresentation[] mounts;
        readonly GameObject[] sources;
        readonly int[] participants;
        readonly Material material,coreMaterial,coronaMaterial,contactMaterial,shieldMaterial;
        readonly Contact[,] contacts;
        readonly Batch[] amberBatches,shieldBatches;
        readonly float coronaScale,contactSeconds,sparkSeconds,sparkReach,shieldRadius,muzzleRadius,shieldRingWidth,muzzleBlend,sparkWidth,contactGlow;
        readonly int sparkCount,arcSubdivisions;
        readonly float sparkSpread,shieldSpread,arcRadiusScale,arcWidthScale;
        readonly float shieldOpacity,sparkGravity,sparkVariance,coreFraction;
        readonly int rippleCount;
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
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,float alpha,bool radial=false,float white=0,bool solid=false)
            {
                int n=vertices.Count;vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                uv.Add(new Vector2(0,0));uv.Add(new Vector2(1,0));uv.Add(new Vector2(1,1));uv.Add(new Vector2(0,1));
                var color=new Color(radial?1:0,white,solid?0:1,alpha);for(int i=0;i<4;i++)colors.Add(color);
                triangles.Add(n);triangles.Add(n+1);triangles.Add(n+2);triangles.Add(n);triangles.Add(n+2);triangles.Add(n+3);
            }
            public void Disc(Vector3 center,Vector3 side,Vector3 up,float radius,float alpha,float white=0)
                =>Quad(center-side*radius-up*radius,center+side*radius-up*radius,center+side*radius+up*radius,center-side*radius+up*radius,alpha,true,white);
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
            shieldOpacity=Read("surface.shieldOpacity");rippleCount=(int)Read("surface.rippleCount");sparkGravity=Read("surface.sparkGravity");sparkVariance=Read("surface.sparkVariance");coreFraction=Read("surface.coreFraction");
            coronaScale=Read("coronaScale");contactSeconds=Read("contactSeconds");sparkSeconds=Read("sparkSeconds");sparkReach=Read("sparkReach");
            arcRadiusScale=Read("detail.arcRadiusScale");arcWidthScale=Read("detail.arcWidthScale");
            sparkCount=(int)(Read("sparkCount")*Read("detail.sparkLayers")*Read("surface.sparkDensity"));arcSubdivisions=(int)Read("detail.arcSubdivisions");sparkSpread=Read("detail.sparkSpread");shieldSpread=Read("detail.shieldSpread");shieldRadius=Read("shieldRadius");muzzleRadius=Read("muzzleRadius");shieldRingWidth=Read("shieldRingWidth");muzzleBlend=Read("muzzleBlendDistance");sparkWidth=Read("sparkWidth");contactGlow=Read("contactGlow");
            root=new GameObject("Cutter beams");root.transform.SetParent(owner,false);
            var shader=Resources.Load<Shader>("CutterBeam");
            var color=new Color(p.Get("cutter.red"),p.Get("cutter.green"),p.Get("cutter.blue"));
            Material Make(Color tint,bool radial=false){var m=new Material(shader);m.SetColor("_Tint",tint);m.SetFloat("_NearFade",p.Get("cutter.nearFade"));m.SetFloat("_Radial",radial?1:0);return m;}
            var glow=Color.Lerp(color,color.linear,Read("detail.warmth"))*p.Get("cutter.glow");glow.a=1;material=Make(glow);
            var core=Color.Lerp(color,Color.white,p.Get("cutter.coreWhiten"))*p.Get("cutter.glow");core.a=1;coreMaterial=Make(core);
            glow.a=Read("coronaAlpha");coronaMaterial=Make(glow);glow.a=1;contactMaterial=Make(glow,true);
            shieldMaterial=Make(new Color(Read("shieldRed"),Read("shieldGreen"),Read("shieldBlue"),1),true);
            sources=bodies.Concat(views).ToArray();participants=Enumerable.Range(0,bodies.Length).Concat(local).ToArray();
            mounts=sources.Select(s=>s.GetComponent<WeaponModelPresentation>()).ToArray();
            contacts=new Contact[bodies.Length,bodies.Length];
            muzzleLinks=new LineRenderer[sources.Length];cores=new LineRenderer[sources.Length];arcs=new LineRenderer[sources.Length];filaments=new LineRenderer[sources.Length];coronas=new LineRenderer[sources.Length];
            amberBatches=new Batch[sources.Length];shieldBatches=new Batch[sources.Length];
            for(int i=0;i<sources.Length;i++)
            {
                int layer=sources[i].layer;
                cores[i]=Line("Core "+i,2,p.Get("cutter.width"),coreMaterial,layer);
                coronas[i]=Line("Corona "+i,2,p.Get("cutter.width")*coronaScale,coronaMaterial,layer);
                muzzleLinks[i]=Line("Muzzle link "+i,2,p.Get("cutter.width"),coreMaterial,layer);
                arcs[i]=Line("Arc "+i,(int)p.Get("cutter.segments")*arcSubdivisions+1,p.Get("cutter.arcWidth")*arcWidthScale,material,layer);
                filaments[i]=Line("Filament "+i,arcs[i].positionCount,p.Get("cutter.arcWidth")*arcWidthScale,material,layer);
                amberBatches[i]=new Batch("SEAM contacts "+i,root.transform,layer,contactMaterial);
                shieldBatches[i]=new Batch("SEAM shield "+i,root.transform,layer,shieldMaterial);
            }
            session.Damaged+=Observe;session.StateRestored+=Clear;
        }
        LineRenderer Line(string name,int count,float width,Material mat,int layer)
        {
            var obj=new GameObject(name);obj.transform.SetParent(root.transform,false);obj.layer=layer;var line=obj.AddComponent<LineRenderer>();
            line.sharedMaterial=mat;line.useWorldSpace=true;line.positionCount=count;line.startWidth=line.endWidth=width;
            line.numCornerVertices=4;line.numCapVertices=4;
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
            float glowRadius=p.Get("cutter.impactSize")*.5f;
            batch.Disc(point-direction*.008f,side,up,glowRadius,strength*contactGlow);
            batch.Disc(point-direction*.009f,side,up,glowRadius*coreFraction,strength*contactGlow,1);
            if(shield)
            {
                float radius=shieldRadius*shieldSpread;
                // Curved local surface, tessellated radially. The fragment envelope
                // fades into the undisturbed shield; it is never a gameplay volume.
                const int facets=48;
                const int bands=8;
                Vector3 Surface(float r,float a)=>point+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*r+direction*(r*r/(radius*3));
                for(int band=0;band<bands;band++)for(int k=0;k<facets;k++)
                {
                    float inner=radius*band/bands,outer=radius*(band+1)/bands;
                    float a=k*Mathf.PI*2/facets,b=(k+1)*Mathf.PI*2/facets;
                    float fade=1-(band+.5f)/bands;
                    batch.Quad(Surface(inner,a),Surface(inner,b),Surface(outer,b),Surface(outer,a),strength*shieldOpacity*fade,solid:true);
                }
                for(int wave=0;wave<rippleCount;wave++)
                {
                    float life=Mathf.Repeat(phase*.5f+(float)wave/rippleCount,1);
                    float r=radius*life;
                    for(int k=0;k<facets;k++)
                    {
                        float a=k*Mathf.PI*2/facets,b=(k+1)*Mathf.PI*2/facets;
                        float wa=1+Mathf.Sin(a*7+phase)*shieldRingWidth/radius,wb=1+Mathf.Sin(b*7+phase)*shieldRingWidth/radius;
                        batch.Quad(Surface(r*wa,a),Surface(r*wb,b),Surface(r*wb+shieldRingWidth,b),Surface(r*wa+shieldRingWidth,a),strength*(1-life));
                    }
                }
                float cell=radius/7;
                for(int row=-5;row<=5;row++)for(int col=-5;col<=5;col++)
                {
                    float x=col*cell*1.5f,y=(row+(col%2==0?0:.5f))*cell*1.732f;
                    var center=point+side*x+up*y+direction*((x*x+y*y)/(radius*3));
                    float fade=Mathf.Clamp01(1-Mathf.Sqrt(x*x+y*y)/radius);
                    if(fade<=0)continue;
                    for(int edge=0;edge<6;edge++)
                    {
                        float a=edge*Mathf.PI/3,b=(edge+1)*Mathf.PI/3;
                        var first=center+(side*Mathf.Cos(a)+up*Mathf.Sin(a))*cell;
                        var last=center+(side*Mathf.Cos(b)+up*Mathf.Sin(b))*cell;
                        var width=Vector3.Cross(direction,last-first).normalized*shieldRingWidth*.12f;
                        batch.Quad(first-width,last-width,last+width,first+width,strength*fade*.5f);
                    }
                }
                for(int k=0;k<sparkCount/3;k++)
                {
                    float t=Mathf.Repeat(phase+k*.618034f,1),angle=k*2.399963f;
                    var radial=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                    var center=point+radial*radius*t-direction*shieldRingWidth;
                    var width=Vector3.Cross(direction,radial)*shieldRingWidth*.15f;
                    batch.Quad(center-width,center+radial*shieldRingWidth*3-width,center+radial*shieldRingWidth*3+width,center+width,strength*(1-t),white:.5f);
                }
                return;
            }
            for(int k=0;k<sparkCount;k++)
            {
                float Random(float salt)=>Mathf.Repeat(Mathf.Sin(k*127.1f+seed*311.7f+salt)*43758.5453f,1);
                float t=Mathf.Repeat(phase+Random(19),1);
                float angle=Random(53)*Mathf.PI*2;
                var radial=side*Mathf.Cos(angle)+up*Mathf.Sin(angle);
                float speed=Mathf.Lerp(1,.2f+Random(73),sparkVariance);
                var velocity=radial*speed-direction*(.1f+Random(11)*.45f);
                // Dense short fragments coexist with a few long, tapered hot streaks.
                float travel=t*t;
                float tail=Mathf.Lerp(.18f,.02f+Random(37)*.18f,sparkVariance);
                float previous=Mathf.Max(0,travel-tail*speed);
                Vector3 At(float time)=>point+velocity*sparkReach*sparkSpread*time-Vector3.up*sparkGravity*time*time;
                var end=At(travel);var start=At(previous);
                var width=Vector3.Cross(direction,velocity).normalized*sparkWidth*Mathf.Lerp(1,.12f+Random(91)*.7f,sparkVariance);
                float alpha=strength*(1-t)*contactGlow;
                batch.Quad(start-width,end-width*.15f,end+width*.15f,start+width,alpha,false,1-t);
                batch.Disc(end,side,up,width.magnitude*1.5f,alpha,1-t);
                if(k%3==0)batch.Disc(At(travel*.2f),side,up,glowRadius*(.04f+Random(151)*.08f),alpha,1);
            }
        }
        public void Render()
        {
            if(disposed)return;
            for(int i=0;i<cores.Length;i++)
            {
                var state=session.Beam(participants[i]);var amber=amberBatches[i];var shield=shieldBatches[i];amber.Begin();shield.Begin();
                bool visible=state.Active&&sources[i]&&sources[i].activeInHierarchy;
                muzzleLinks[i].enabled=cores[i].enabled=arcs[i].enabled=filaments[i].enabled=coronas[i].enabled=visible;
                if(visible)
                {
                    var start=mounts[i]?mounts[i].CutterMuzzle:state.Origin;
                    var axis=(state.Endpoint-state.Origin).normalized;Basis(axis,out var side,out var up);
                    float length=Vector3.Distance(state.Origin,state.Endpoint);
                    // Viewmodel parallax joins the real ray near the muzzle, then remains collinear
                    // with authoritative contacts. No hit query or simulation endpoint is changed.
                    float merge=Mathf.Min(length,Mathf.Max(muzzleBlend,Vector3.Dot(start-state.Origin,axis)+muzzleBlend));
                    var junction=state.Origin+axis*merge;
                    // Separate straight strips avoid a camera-facing miter spike at the parallax bend.
                    muzzleLinks[i].SetPosition(0,start);muzzleLinks[i].SetPosition(1,junction);
                    cores[i].SetPosition(0,junction);cores[i].SetPosition(1,state.Endpoint);
                    coronas[i].SetPosition(0,junction);coronas[i].SetPosition(1,state.Endpoint);
                    float first=Vector3.Distance(start,junction),total=first+Vector3.Distance(junction,state.Endpoint);
                    // The saved segment budget is for full range. Short beams need fewer
                    // samples, otherwise constant radius with hundreds of points becomes a coil.
                    int samples=Mathf.Max(3,Mathf.CeilToInt(p.Get("cutter.segments")*arcSubdivisions*Mathf.Clamp01(total/p.Get("cutter.maxRangeMeters")))+1);
                    arcs[i].positionCount=filaments[i].positionCount=samples;
                    for(int k=0;k<samples;k++)
                    {
                        float t=(float)k/(arcs[i].positionCount-1);
                        float phase=Mathf.Floor((float)session.Time*p.Get("cutter.frequency"))*311.7f+k*127.1f;
                        float Noise(float seed)=>Mathf.Repeat(Mathf.Sin(seed)*43758.5453f,1)*2-1;
                        var offset=(side*Noise(phase)+up*Noise(phase+73.1f))*p.Get("cutter.jitter")*arcRadiusScale*Mathf.Sin(t*Mathf.PI);
                        float distance=t*total;
                        var center=distance<=first?Vector3.Lerp(start,junction,first>0?distance/first:1):Vector3.Lerp(junction,state.Endpoint,total>first?(distance-first)/(total-first):1);
                        arcs[i].SetPosition(k,center+offset);
                        var second=(side*Noise(phase+19.4f)+up*Noise(phase+51.8f))*p.Get("cutter.jitter")*arcRadiusScale*Mathf.Sin(t*Mathf.PI);
                        filaments[i].SetPosition(k,center+second);
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
