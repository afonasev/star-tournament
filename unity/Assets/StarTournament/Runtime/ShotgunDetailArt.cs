using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround
{
    /// <summary>Authored normalized mesh/texture motifs. All world sizes, counts and durations use the existing named profile.</summary>
    internal sealed class ShotgunDetailArt : IDisposable
    {
        public readonly Mesh Pellet,Chip;
        public readonly Material Metal,Spark,Dust,Shell,ContactFlash;
        readonly Texture2D smoke,sparkTexture,contactTexture;
        public ShotgunDetailArt(ProvingProfile profile)
        {
            Pellet=MakePellet();Chip=MakeChip();smoke=SmokeTexture(true);sparkTexture=SparkTexture();
            var lit=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            var grey=new Color(profile.Get("presentation.shotTracerRed"),profile.Get("presentation.shotTracerGreen"),profile.Get("presentation.shotTracerBlue"),1);
            Metal=new Material(lit){name="Vector grey machined pellet",color=grey};Metal.SetFloat("_Metallic",1);Metal.SetFloat("_Smoothness",.65f);
            Shell=new Material(lit){name="Vector surface chip",color=grey};
            Spark=new Material(Shader.Find("StarTournament/VectorParticle")){name="Vector contact sparks",mainTexture=sparkTexture};
            Spark.SetFloat("_Radiance",profile.Get(ProvingProfile.VectorPrecisionPrefix+"contactRadiance"));
            contactTexture=FlashTexture();ContactFlash=new Material(Spark){name="Vector contact impulse",mainTexture=contactTexture};
            Dust=new Material(Shader.Find("StarTournament/VectorParticle")){name="Vector contact dust",mainTexture=smoke};
        }
        public GameObject Core(Transform parent,int layer)
        {
            var o=new GameObject("pellet-metal-core");o.layer=layer;o.transform.SetParent(parent,false);
            o.AddComponent<MeshFilter>().sharedMesh=Pellet;var r=o.AddComponent<MeshRenderer>();r.sharedMaterial=Metal;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;r.enabled=false;return o;
        }
        public ParticleSystem[] Impact(Transform parent,Vector3 point,Vector3 normal,PelletContact contact,ProvingProfile profile,uint seed)
        {
            var root=new GameObject("shot-impact");root.transform.SetParent(parent,false);root.transform.position=point;
            root.transform.rotation=Quaternion.LookRotation(normal.sqrMagnitude>0?normal:Vector3.up);
            float P(string key)=>profile.Get(ProvingProfile.VectorPrecisionPrefix+key);
            float life=profile.Get("presentation.shotImpactSeconds");int count=(int)profile.Get("presentation.vector.sparkCount");
            var spark=System(root.transform,"vector-impact-sparks",life,profile.Get("presentation.vector.sparkSize")*P("contactSparkScale"),profile.Get("presentation.vector.sparkSpeed")*P("contactSparkSpeedScale"),count,new Color(1,.78f,.4f),profile,Spark,null,seed);
            var chip=System(root.transform,"vector-impact-chips",life*P("chipLifetimeScale"),P("chipSize"),P("chipSpeed"),count,contact==PelletContact.Participant?Color.gray:Color.white,profile,contact==PelletContact.Participant?Metal:Shell,Chip,seed);
            var dust=System(root.transform,"vector-impact-dust",life*P("dustLifetimeScale"),profile.Get("presentation.shotImpactSize"),profile.Get("presentation.vector.smokeSpeed"),1,new Color(.65f,.64f,.6f,contact==PelletContact.Participant?P("dustOpacity")*.25f:P("dustOpacity")),profile,Dust,null,seed);
            var pulse=System(root.transform,"vector-impact-pulse",P("contactFlashSeconds"),P("contactFlashSize"),0,1,Color.white,profile,ContactFlash,null,seed);
            // Numerical separation from the surface avoids coplanar alpha/depth flicker; authoritative root stays exact.
            pulse.transform.localPosition=Vector3.forward*.001f;
            foreach(var ps in new[]{spark,chip,dust,pulse}){ps.Simulate(0,true,true,false);ps.Play();ps.Emit(ps==dust||ps==pulse?1:count);ps.Pause();}
            return new[]{spark,chip,dust,pulse};
        }
        static ParticleSystem System(Transform parent,string name,float life,float size,float speed,int count,Color color,ProvingProfile profile,Material mat,Mesh mesh,uint seed)
        {
            var o=new GameObject(name);o.transform.SetParent(parent,false);var ps=o.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=life;main.startLifetime=life;main.startSize=size;main.startSpeed=speed;main.startColor=color;main.maxParticles=Mathf.Max(1,count);main.simulationSpace=ParticleSystemSimulationSpace.World;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.enabled=speed>0;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=profile.Get("presentation.vector.sparkConeDegrees");shape.radius=0;
            var fade=ps.colorOverLifetime;fade.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=g;
            var shrink=ps.sizeOverLifetime;shrink.enabled=true;shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            var r=ps.GetComponent<ParticleSystemRenderer>();r.sharedMaterial=mat;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;
            if(mesh){r.renderMode=ParticleSystemRenderMode.Mesh;r.mesh=mesh;main.startRotation3D=true;main.startRotationX=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotationY=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);main.startRotationZ=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);}
            else if(name=="vector-impact-sparks"){r.renderMode=ParticleSystemRenderMode.Stretch;r.lengthScale=profile.Get(ProvingProfile.VectorPrecisionPrefix+"sparkLength");r.velocityScale=0;}
            if(name=="vector-impact-dust")
            {
                main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                shrink.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.35f,1,profile.Get(ProvingProfile.VectorPrecisionPrefix+"dustGrowth")));
                g.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(.5f,0),new GradientAlphaKey(1,.15f),new GradientAlphaKey(.35f,.65f),new GradientAlphaKey(0,1)});fade.color=g;
            }
            else
            {
                main.startSize=new ParticleSystem.MinMaxCurve(size*.5f,size);
                main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.45f,speed);
                main.startLifetime=new ParticleSystem.MinMaxCurve(life*.65f,life);
                main.gravityModifier=profile.Get(ProvingProfile.VectorPrecisionPrefix+"debrisGravity");
                var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.speedModifier=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,.25f));
                if(mesh){var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.separateAxes=true;rotation.x=new ParticleSystem.MinMaxCurve(-5,5);rotation.y=new ParticleSystem.MinMaxCurve(-5,5);rotation.z=new ParticleSystem.MinMaxCurve(-5,5);}
            }
            ps.useAutoRandomSeed=false;ps.randomSeed=seed==0?1:seed;return ps;
        }
        public static Mesh FlameMesh()
        {
            // Three crossed, tapered flame sheets. Normalized editable artwork, no gameplay geometry.
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            for(int plane=0;plane<3;plane++)
            {
                var rotation=Quaternion.AngleAxis(plane*60,Vector3.forward);int start=vertices.Count;
                foreach(var point in new[]{new Vector3(-.12f,0,0),new Vector3(.12f,0,0),new Vector3(-.5f,0,.28f),new Vector3(.5f,0,.28f),new Vector3(-.025f,0,1),new Vector3(.025f,0,1)})vertices.Add(rotation*point);
                uv.AddRange(new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(0,.28f),new Vector2(1,.28f),new Vector2(0,1),new Vector2(1,1)});
                foreach(int i in new[]{0,2,1,1,2,3,2,4,3,3,4,5})triangles.Add(start+i);
            }
            var mesh=new Mesh{name="Vector crossed tapered flame petals"};mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public static Texture2D PlumeTexture()
        {
            const int n=128;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Vector axial ivory flame",wrapMode=TextureWrapMode.Clamp};var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n;
                float noise=.65f+.35f*Mathf.PerlinNoise(19+u*9,7+v*16);
                float edge=Mathf.Clamp01((1-Mathf.Abs(u))*3);float alpha=edge*Mathf.Pow(1-v,.6f)*noise;
                var color=Color.Lerp(new Color(1,1,.9f),new Color(1,.42f,.08f),Mathf.Clamp01(v*.8f+Mathf.Abs(u)*.4f));color.a=alpha;pixels[y*n+x]=color;
            }
            texture.SetPixels(pixels);texture.Apply();return texture;
        }
        public static Texture2D SparkTexture()
        {
            const int n=64;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Vector tapered incandescent spark",wrapMode=TextureWrapMode.Clamp};var p=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1;
                float a=Mathf.Pow(Mathf.Clamp01(1-u*u-v*v),2);
                p[y*n+x]=new Color(1,.7f+.3f*a,.25f+.65f*a,a);
            }
            t.SetPixels(p);t.Apply();return t;
        }
        public static Texture2D SmokeTexture(bool precision=false)
        {
            // Fixed texel lattice and normalized noise are authored artwork, not world-size or simulation knobs.
            const int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Vector authored billowing smoke",wrapMode=TextureWrapMode.Clamp};
            var p=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1,r=Mathf.Sqrt(u*u+v*v);
                float noise=Mathf.PerlinNoise(7+u*3,11+v*3)*.65f+Mathf.PerlinNoise(19+u*9,3+v*9)*.35f;
                float curl=Mathf.PerlinNoise(31+u*5+noise,17+v*5-noise);
                float alpha=Mathf.SmoothStep(0,1,Mathf.Clamp01((1-r)*2))*(precision?Mathf.Clamp01(Mathf.SmoothStep(.18f,.75f,noise)*Mathf.Lerp(.35f,1,curl)*2):noise);
                p[y*n+x]=new Color(.65f+noise*.35f,.65f+noise*.35f,.65f+noise*.35f,alpha);
            }
            t.SetPixels(p);t.Apply();return t;
        }
        public static Texture2D FlashTexture()
        {
            const int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false){name="Vector authored incandescent muzzle",wrapMode=TextureWrapMode.Clamp};var p=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1,r=Mathf.Sqrt(u*u+v*v),a=Mathf.Atan2(v,u);
                float lobe=.53f+.20f*Mathf.Cos(a*3+.6f)+.13f*Mathf.Cos(a*5-1)+.07f*Mathf.Cos(a*9+.4f);
                float noise=.7f+.3f*Mathf.PerlinNoise(u*12+20,v*12+30);
                float core=Mathf.Exp(-r*r*95);
                float alpha=Mathf.Clamp01(core+Mathf.Clamp01((lobe-r)*5)*noise);
                p[y*n+x]=Color.Lerp(new Color(1,.38f,.055f,alpha),new Color(1,1,.88f,alpha),Mathf.Clamp01(1-r*1.6f));
            }
            t.SetPixels(p);t.Apply();return t;
        }
        static Mesh MakePellet()
        {
            // Authored steel shot: round silhouette, shallow machining seam and micro-pitting.
            const int longitude=48,latitude=24;var v=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var indices=new List<int>();
            for(int y=0;y<=latitude;y++)for(int x=0;x<=longitude;x++)
            {
                float theta=y*Mathf.PI/latitude,phi=x*2*Mathf.PI/longitude;
                float seam=Mathf.Exp(-Mathf.Pow((theta-Mathf.PI/2)*35,2))*.012f;
                float radius=.5f-seam+.0015f*Mathf.Sin(phi*13)*Mathf.Sin(theta*17);
                var radial=new Vector3(Mathf.Sin(theta)*Mathf.Cos(phi),Mathf.Cos(theta),Mathf.Sin(theta)*Mathf.Sin(phi));
                var tangent=new Vector3(Mathf.Cos(theta)*Mathf.Cos(phi),-Mathf.Sin(theta),Mathf.Cos(theta)*Mathf.Sin(phi));
                float derivative=2*35*35*(theta-Mathf.PI/2)*seam+.0015f*17*Mathf.Sin(phi*13)*Mathf.Cos(theta*17);
                float azimuth=.0015f*13*Mathf.Cos(phi*13)*Mathf.Sin(theta*17);
                var normal=radial-tangent*(derivative/radius);
                if(y>0&&y<latitude)normal-=new Vector3(-Mathf.Sin(phi),0,Mathf.Cos(phi))*(azimuth/(radius*Mathf.Sin(theta)));
                // Shared analytic normals avoid a split UV seam and pinched duplicate pole vertices.
                normals.Add(y==0||y==latitude?radial:normal.normalized);
                v.Add(radial*radius);uv.Add(new Vector2((float)x/longitude,(float)y/latitude));
                if(y<latitude&&x<longitude){int a=y*(longitude+1)+x,b=a+longitude+1;indices.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            }
            var m=new Mesh{name="Vector detailed steel shot"};m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(indices,0);m.SetNormals(normals);m.RecalculateBounds();return m;
        }
        static Mesh MakeChip()
        {
            var m=new Mesh{name="Vector fractured surface wedge"};m.vertices=new[]{new Vector3(-.5f,-.12f,-.4f),new Vector3(.5f,-.12f,-.25f),new Vector3(.22f,-.12f,.5f),new Vector3(-.3f,-.12f,.2f),new Vector3(-.35f,.12f,-.3f),new Vector3(.35f,.12f,-.15f),new Vector3(.12f,.12f,.35f),new Vector3(-.2f,.12f,.1f)};
            m.triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};var triangles=m.triangles;for(int i=0;i<triangles.Length;i+=3){int t=triangles[i+1];triangles[i+1]=triangles[i+2];triangles[i+2]=t;}m.triangles=triangles;m.RecalculateNormals();m.RecalculateBounds();return m;
        }
        public void Dispose(){foreach(var o in new Object[]{Pellet,Chip,Metal,Spark,Dust,Shell,ContactFlash,smoke,sparkTexture,contactTexture})if(o)Object.Destroy(o);}
    }
}
