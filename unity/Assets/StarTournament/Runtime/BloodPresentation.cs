using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround
{
    /// <summary>Bounded world VFX driven by actual health loss; owns no gameplay objects/state.</summary>
    public sealed class BloodPresentation:IDisposable
    {
        sealed class Drop { public GameObject Root;public Vector3 Origin,Velocity;public double Born,Sampled;public float Scale,Size;public uint Seed; }
        sealed class Mark { public GameObject Root;public Mesh Mesh;public Renderer Renderer;public double Born; }
        readonly NativeCombatSession session;readonly ProvingProfile profile;readonly Transform owner;readonly PhysicsScene physics;
        readonly List<Drop> drops=new List<Drop>();readonly List<Mark> marks=new List<Mark>();
        readonly double[] lastCutter;readonly MaterialPropertyBlock properties=new MaterialPropertyBlock();
        Material material;Mesh dropMesh;bool disposed;uint ordinal;
        public int ActiveDrops=>drops.Count;public int ActiveMarks=>marks.Count;public int Bursts {get;private set;}
        // Geometry tessellation/query tolerances are technical invariants, not artistic tuning.
        const int CurveSteps=4,SatelliteSegments=12;const float SurfaceOffset=.002f,ProjectionDepth=.04f,FlightStep=.02f;
        public BloodPresentation(NativeCombatSession session,ProvingProfile profile,Transform owner,PhysicsScene physics)
        {
            this.session=session;this.profile=JsonUtility.FromJson<ProvingProfile>(JsonUtility.ToJson(profile));this.owner=owner;this.physics=physics;
            lastCutter=new double[session.ParticipantCount];for(int i=0;i<lastCutter.Length;i++)lastCutter[i]=double.NegativeInfinity;
            session.Damaged+=Observe;session.StateRestored+=Clear;
        }
        public void Observe(DamageNotice notice)
        {
            if(disposed||profile.Get("blood.enabled")==0||notice.HealthLost<=0||!notice.Impact.Valid)return;
            if(notice.Impact.Weapon==WeaponId.Cutter)
            { if(notice.Time-lastCutter[notice.Participant]<profile.Get("blood.cutterInterval"))return;lastCutter[notice.Participant]=notice.Time; }
            EnsureMaterial();Bursts++;
            var impact=notice.Impact;float scale=Scale(impact.Weapon);var direction=impact.Direction.normalized;
            if(direction.sqrMagnitude==0)return;
            var origin=impact.Point;
            var tangent=Vector3.Cross(Mathf.Abs(direction.y)>.9f?Vector3.right:Vector3.up,direction).normalized;
            var bitangent=Vector3.Cross(direction,tangent);uint seed=impact.Sequence*747796405u+(uint)(notice.Participant+1)*2891336453u+(++ordinal);
            int count=(int)profile.Get("blood.drops");
            for(int i=0;i<count;i++)
            {
                while(drops.Count>=(int)profile.Get("blood.maxDrops"))RemoveDrop(0);
                float angle=Next(ref seed)*Mathf.PI*2;float spread=Mathf.Tan(profile.Get("blood.spreadDegrees")*Mathf.Deg2Rad)*Mathf.Sqrt(Next(ref seed));
                var velocity=(direction+(tangent*Mathf.Cos(angle)+bitangent*Mathf.Sin(angle))*spread).normalized*profile.Get("blood.speed")*Mathf.Lerp(1-profile.Get("blood.speedVariation"),1,Next(ref seed))+Vector3.up*profile.Get("blood.lift");
                float size=profile.Get("blood.dropSize")*scale*Mathf.Lerp(1-profile.Get("blood.dropVariation"),1,Next(ref seed));
                var root=new GameObject("blood-drop");root.transform.SetParent(owner,false);root.transform.position=origin;root.transform.localScale=new Vector3(size,size,size*profile.Get("blood.dropElongation"));
                root.AddComponent<MeshFilter>().sharedMesh=DropMesh();var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                drops.Add(new Drop{Root=root,Origin=origin,Velocity=velocity,Born=notice.Time,Sampled=notice.Time,Scale=scale,Size=size,Seed=seed});
            }
        }
        float Scale(WeaponId weapon)=>profile.Get("blood."+(weapon==WeaponId.Shotgun?"shotgunScale":weapon==WeaponId.Rifle?"rifleScale":weapon==WeaponId.Cutter?"cutterScale":"rocketScale"));
        Vector3 Position(Drop drop,double time)=>drop.Origin+drop.Velocity*(float)(time-drop.Born)+Vector3.down*(profile.Get("blood.gravity")*.5f*(float)((time-drop.Born)*(time-drop.Born)));
        public void Render()
        {
            if(disposed)return;double now=session.Time;
            for(int i=drops.Count-1;i>=0;i--)
            {
                var drop=drops[i];bool landed=false;double until=Math.Min(now,drop.Born+profile.Get("blood.flightSeconds"));
                while(drop.Sampled<until)
                {
                    double next=Math.Min(until,drop.Sampled+FlightStep);var start=Position(drop,drop.Sampled);var end=Position(drop,next);var delta=end-start;
                    if(delta.sqrMagnitude>0&&physics.Raycast(start,delta.normalized,out var hit,delta.magnitude,1<<ProvingArena.WorldLayer,QueryTriggerInteraction.Ignore))
                    {
                        float variation=1+profile.Get("blood.markVariation")*(Next(ref drop.Seed)*2-1);
                        AddMark(hit,profile.Get("blood.markSize")*.5f*drop.Scale*variation,next,drop.Seed,drop.Velocity+Vector3.down*profile.Get("blood.gravity")*(float)(next-drop.Born));landed=true;break;
                    }
                    drop.Sampled=next;
                }
                if(landed||now-drop.Born>=profile.Get("blood.flightSeconds")){RemoveDrop(i);continue;}
                drop.Root.transform.position=Position(drop,now);
                var velocity=drop.Velocity+Vector3.down*profile.Get("blood.gravity")*(float)(now-drop.Born);
                if(velocity.sqrMagnitude>0)drop.Root.transform.rotation=Quaternion.LookRotation(velocity);
            }
            for(int i=marks.Count-1;i>=0;i--)
            {
                var mark=marks[i];float age=(float)(now-mark.Born)/profile.Get("blood.markSeconds");
                if(age>=1){RemoveMark(i);continue;}
                float opacity=profile.Get("blood.opacity")*(1-Mathf.Clamp01((age-(1-profile.Get("blood.fadeFraction")))/profile.Get("blood.fadeFraction")));
                properties.Clear();properties.SetColor("_BaseColor",Color(age));properties.SetFloat("_Opacity",opacity);mark.Renderer.SetPropertyBlock(properties);
            }
        }
        Color Color(float age)=>new Color(profile.Get("blood.red"),profile.Get("blood.green"),profile.Get("blood.blue"),1)*(1-Mathf.Clamp01(age)*profile.Get("blood.dryDarkening"));
        void EnsureMaterial()
        {
            if(material)return;var shader=Resources.Load<Shader>("BloodSurface");if(!shader)throw new InvalidOperationException("BloodSurface shader is missing");
            material=new Material(shader){name="stylized-oxblood"};material.SetColor("_BaseColor",Color(0));material.SetFloat("_Opacity",profile.Get("blood.opacity"));
        }
        void AddMark(RaycastHit hit,float radius,double born,uint seed,Vector3 velocity)
        {
            var mesh=BuildSurfaceMark(hit,radius,seed,velocity,profile.Get("blood.markVariation"));if(!mesh)return;
            while(marks.Count>=(int)profile.Get("blood.maxMarks"))RemoveMark(0);
            var root=new GameObject("blood-surface-mark");root.transform.SetParent(owner,false);root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            root.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            marks.Add(new Mark{Root=root,Mesh=mesh,Renderer=renderer,Born=born});
        }
        /// <summary>Each triangle is checked on the hit collider, including edges/centre: no projection across holes or corners.</summary>
        public static Mesh BuildSurfaceMark(RaycastHit hit,float radius,uint seed,Vector3 velocity=default,float variation=0)
        {
            if(!hit.collider||radius<=0)return null;
            // Avalanche nearby event seeds before choosing a shape; LCG first samples otherwise cluster.
            seed^=seed>>16;seed*=0x7feb352d;seed^=seed>>15;seed*=0x846ca68b;seed^=seed>>16;
            var normal=hit.normal;var tangent=Vector3.ProjectOnPlane(velocity,normal).normalized;
            if(tangent.sqrMagnitude==0)
            {
                tangent=Vector3.Cross(Mathf.Abs(normal.y)>.9f?Vector3.right:Vector3.up,normal).normalized;
                tangent=Quaternion.AngleAxis(Next(ref seed)*360,normal)*tangent;
            }
            var bitangent=Vector3.Cross(normal,tangent);var vertices=new List<Vector3>();var triangles=new List<int>();
            // Normalized authored geometry for the selected F1 #2 decal, like vertices of a mesh asset.
            // These coordinates define silhouettes, not adjustable gameplay/readability values;
            // world size, shape variation, colour, lifetime and limits come from the Blood profile.
            var outline=new[]{new Vector2(.78f,.06f),new Vector2(.58f,.39f),new Vector2(.17f,.66f),new Vector2(-.28f,.57f),new Vector2(-.68f,.26f),new Vector2(-.61f,-.14f),new Vector2(-.39f,-.51f),new Vector2(.05f,-.58f),new Vector2(.49f,-.38f),new Vector2(.69f,-.17f)};
            int variant=(int)(Next(ref seed)*3);
            float stretch=1+variation*(Next(ref seed)*2-1);
            for(int i=0;i<outline.Length;i++)outline[i]=new Vector2(outline[i].x*stretch,outline[i].y/stretch)*(1+variation*(Next(ref seed)*2-1));
            Vector3 World(Vector2 p)=>hit.point+(tangent*p.x+bitangent*p.y)*radius;
            bool Project(Vector3 sample,out Vector3 point)
            {
                if(hit.collider.Raycast(new Ray(sample+normal*ProjectionDepth,-normal),out var contact,ProjectionDepth*2)&&Vector3.Dot(contact.normal,normal)>.98f&&Vector3.Distance(contact.point,sample)<ProjectionDepth)
                {point=contact.point+normal*SurfaceOffset;return true;}point=default;return false;
            }
            void Triangle(Vector2 x,Vector2 y,Vector2 z)
            {
                var a=World(x);var b=World(y);var c=World(z);
                if(!Project(a,out var pa)||!Project(b,out var pb)||!Project(c,out var pc)||!Project((a+b+c)/3,out _)||!Project((a+b)/2,out _)||!Project((a+c)/2,out _)||!Project((b+c)/2,out _))return;
                int start=vertices.Count;vertices.Add(pa);vertices.Add(pb);vertices.Add(pc);triangles.Add(start);triangles.Add(start+1);triangles.Add(start+2);
            }
            // Catmull-Rom interpolation rounds the uneven blob; no periodic radial teeth.
            Vector2 Curve(int segment,float t)
            {
                int n=outline.Length;var a=outline[(segment+n-1)%n];var b=outline[segment%n];var c=outline[(segment+1)%n];var d=outline[(segment+2)%n];
                return .5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t);
            }
            for(int i=0;i<outline.Length;i++)for(int j=0;j<CurveSteps;j++)Triangle(Vector2.zero,Curve(i,j/(float)CurveSteps),Curve(i,(j+1)/(float)CurveSteps));
            // Three authored stroke patterns: single tail, fork, or two unequal tails.
            var tips=variant==0?new[]{new Vector2(2.25f,.12f)}:variant==1?new[]{new Vector2(1.8f,.58f),new Vector2(2.6f,-.27f)}:new[]{new Vector2(2.1f,.4f),new Vector2(1.35f,-.53f),new Vector2(1.6f,.02f)};
            foreach(var authored in tips)
            {
                var tip=authored*(1+variation*(Next(ref seed)*2-1));var axis=tip.normalized;var side=new Vector2(-axis.y,axis.x);var basePoint=axis*.5f;var neck=tip*.72f;
                Triangle(basePoint-side*.15f,basePoint+side*.15f,neck+side*.045f);
                Triangle(basePoint-side*.15f,neck+side*.045f,neck-side*.045f);
                Triangle(neck-side*.045f,neck+side*.045f,tip);
            }
            var satellites=variant==0?new[]{new Vector2(2.55f,.18f),new Vector2(1.6f,-.42f),new Vector2(-.5f,.86f)}:variant==1?new[]{new Vector2(2.95f,-.35f),new Vector2(2.12f,.66f),new Vector2(.1f,-.86f),new Vector2(-.86f,.07f)}:new[]{new Vector2(2.46f,.48f),new Vector2(1.67f,-.68f),new Vector2(1.88f,.13f),new Vector2(-.73f,-.59f),new Vector2(.53f,.9f)};
            foreach(var authored in satellites)
            {
                var centre=authored*(1+variation*(Next(ref seed)*2-1));float size=.045f+.065f*Next(ref seed);
                Vector2 Rim(int i){float angle=i*Mathf.PI*2/SatelliteSegments;return centre+new Vector2(Mathf.Cos(angle)*size,Mathf.Sin(angle)*size*.7f);}
                for(int i=0;i<SatelliteSegments;i++)Triangle(centre,Rim(i),Rim(i+1));
            }
            if(triangles.Count==0)return null;
            var mesh=new Mesh{name="blood-surface-splat"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        Mesh DropMesh()
        {
            if(dropMesh)return dropMesh;
            // An octahedron is fixed low-poly VFX geometry; all artistic size comes from the profile.
            dropMesh=new Mesh{name="blood-droplet"};dropMesh.vertices=new[]{Vector3.up*.5f,Vector3.down*.5f,Vector3.left*.5f,Vector3.right*.5f,Vector3.forward*.5f,Vector3.back*.5f};
            dropMesh.triangles=new[]{0,4,3,0,3,5,0,5,2,0,2,4,1,3,4,1,5,3,1,2,5,1,4,2};dropMesh.RecalculateBounds();return dropMesh;
        }
        static float Next(ref uint state){state=state*1664525u+1013904223u;return (state>>8)*(1f/16777216f);}
        void RemoveDrop(int index){var root=drops[index].Root;if(root){root.SetActive(false);Object.Destroy(root);}drops.RemoveAt(index);}
        void RemoveMark(int index){var mark=marks[index];if(mark.Root){mark.Root.SetActive(false);Object.Destroy(mark.Root);}if(mark.Mesh)Object.Destroy(mark.Mesh);marks.RemoveAt(index);}
        public void Clear(){while(drops.Count>0)RemoveDrop(0);while(marks.Count>0)RemoveMark(0);for(int i=0;i<lastCutter.Length;i++)lastCutter[i]=double.NegativeInfinity;}
        public void Dispose(){if(disposed)return;disposed=true;session.Damaged-=Observe;session.StateRestored-=Clear;Clear();if(material)Object.Destroy(material);if(dropMesh)Object.Destroy(dropMesh);}
    }
}
