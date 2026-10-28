using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Reusable twin muzzle systems, advanced exclusively by the session clock.</summary>
    public sealed class VectorShotPresentation : MonoBehaviour
    {
        Transform[] muzzles;
        ParticleSystem[] effects;
        ParticleSystem[] plumes;
        Mesh plumeMesh;
        Material plumeMaterial;
        Texture2D plumeTexture;
        int[] emissionCounts;
        Material material, flashMaterial, sparkMaterial;
        ProvingProfile profile;
        Texture2D texture, flashTexture, sparkTexture;
        double lastTime;
        bool fired;
        public int MuzzleCount => muzzles?.Length ?? 0;
        public int LiveParticles => (effects?.Sum(p=>p?p.particleCount:0) ?? 0)+(plumes?.Sum(p=>p?p.particleCount:0) ?? 0);
        public Vector3 Muzzle(int index) => muzzles[index%2].position;
        public void Initialize(ProvingProfile profile)
        {
            this.profile=profile;
            muzzles=Enumerable.Range(0,2).Select(i=>GetComponentsInChildren<Transform>(true).Single(t=>t.name=="vector-muzzle-"+i)).ToArray();
            texture=ShotgunDetailArt.SmokeTexture(true);flashTexture=ShotgunDetailArt.FlashTexture();
            material=new Material(Shader.Find("StarTournament/VectorParticle")){mainTexture=texture};
            flashMaterial=new Material(material){mainTexture=flashTexture};
            flashMaterial.SetFloat("_Radiance",P("flashRadiance"));
            sparkTexture=ShotgunDetailArt.SparkTexture();sparkMaterial=new Material(material){mainTexture=sparkTexture};
            plumeMesh=ShotgunDetailArt.FlameMesh();plumeTexture=ShotgunDetailArt.PlumeTexture();
            plumeMaterial=new Material(flashMaterial){mainTexture=plumeTexture};
            effects=new ParticleSystem[6];plumes=new ParticleSystem[2];
            emissionCounts=Enumerable.Range(0,6).Select(i=>i%3==1?(int)profile.Get("presentation.vector.sparkCount"):i%3==2?(int)P("smokeCount"):1).ToArray();
            for(int i=0;i<2;i++)
            {
                effects[i*3]=Create("vector-flash",muzzles[i],profile.Get("presentation.vector.muzzleSeconds"),profile.Get("presentation.vector.muzzleSize")*P("flashScale"),0,1,Color.white,0);
                effects[i*3+1]=Create("vector-sparks",muzzles[i],profile.Get("presentation.vector.sparkSeconds"),profile.Get("presentation.vector.sparkSize"),profile.Get("presentation.vector.sparkSpeed"),(int)profile.Get("presentation.vector.sparkCount"),new Color(1,.78f,.4f),profile.Get("presentation.vector.sparkConeDegrees"));
                effects[i*3+2]=Create("vector-smoke",muzzles[i],profile.Get("presentation.vector.smokeSeconds"),profile.Get("presentation.vector.smokeSize"),profile.Get("presentation.vector.smokeSpeed"),(int)P("smokeCount"),new Color(.68f,.68f,.66f,P("smokeOpacity")),0);
                var plume=Create("vector-plume",muzzles[i],profile.Get("presentation.vector.muzzleSeconds"),1,0,1,Color.white,0);plumes[i]=plume;
                var pm=plume.main;pm.startSize3D=true;pm.startSizeX=pm.startSizeY=P("plumeWidth");pm.startSizeZ=P("plumeLength");pm.startRotation3D=true;
                var pr=plume.GetComponent<ParticleSystemRenderer>();pr.renderMode=ParticleSystemRenderMode.Mesh;pr.mesh=plumeMesh;pr.alignment=ParticleSystemRenderSpace.World;pr.sharedMaterial=plumeMaterial;
            }
        }
        float P(string key)=>profile.Get(ProvingProfile.VectorPrecisionPrefix+key);
        ParticleSystem Create(string label,Transform muzzle,float life,float size,float speed,int count,Color color,float angle)
        {
            var obj=new GameObject(label);obj.layer=gameObject.layer;obj.transform.SetParent(transform,false);obj.transform.SetPositionAndRotation(muzzle.position,muzzle.rotation);
            // The flash extends from the actual muzzle mouth; place its centre half a
            // diameter forward so the detailed bore collar does not swallow the billboard.
            if(label=="vector-flash")obj.transform.position=muzzle.position+muzzle.forward*(size*.5f);
            // GLB front is +Z. Ignore the imported mount's authored scale for particle sizing.
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=life;main.startLifetime=life;main.startSize=size;main.startSpeed=speed;main.startColor=color;main.maxParticles=Mathf.Max(1,count);main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.enabled=speed>0;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=angle;shape.radius=0;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=label=="vector-flash"?flashMaterial:label=="vector-sparks"?sparkMaterial:material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            // Normalized curves are authored shape/timing, like the texture motifs; world knobs live in the profile.
            var sizeOverLife=ps.sizeOverLifetime;sizeOverLife.enabled=true;
            if(label=="vector-smoke")
            {
                main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                main.startSize=new ParticleSystem.MinMaxCurve(size*.65f,size);
                main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.5f,speed);
                main.startLifetime=new ParticleSystem.MinMaxCurve(life*.7f,life);
                sizeOverLife.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.4f,1,P("smokeGrowth")));
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.15f),new GradientAlphaKey(.45f,.5f),new GradientAlphaKey(0,1)});fade.color=gradient;
                var rotation=ps.rotationOverLifetime;rotation.enabled=true;rotation.z=new ParticleSystem.MinMaxCurve(-1,1);
            }
            else if(label=="vector-sparks")
            {
                renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.lengthScale=P("sparkLength");renderer.velocityScale=0;
                main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.55f,speed);
                main.startLifetime=new ParticleSystem.MinMaxCurve(life*.6f,life);
                main.gravityModifier=P("debrisGravity");
                sizeOverLife.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,1,1,0));
            }
            else
            {
                main.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
                sizeOverLife.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.7f),new Keyframe(.18f,1),new Keyframe(1,.25f)));
                gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(1,.8f,.55f),1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.8f,.25f),new GradientAlphaKey(0,1)});fade.color=gradient;
            }
            ps.useAutoRandomSeed=false;ps.randomSeed=(uint)(muzzle.name.EndsWith("0")?11:29);return ps;
        }
        public void Clear(double clock)
        {
            lastTime=clock;fired=false;
            if(effects!=null)foreach(var ps in effects)if(ps)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            if(plumes!=null)foreach(var ps in plumes)if(ps)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void Fire(double clock)
        {
            if(effects==null)return;
            lastTime=clock;fired=true;
            // Emission uses the actual sampled muzzle pose, without the imported weapon's mesh scale.
            for(int barrel=0;barrel<2;barrel++)
            {
                for(int layer=0;layer<3;layer++)
                {
                    var ps=effects[barrel*3+layer];var offset=layer==0?profile.Get("presentation.vector.muzzleSize")*P("flashScale")*.5f:0;
                    ps.transform.SetPositionAndRotation(muzzles[barrel].position+muzzles[barrel].forward*offset,muzzles[barrel].rotation);
                }
                plumes[barrel].transform.SetPositionAndRotation(muzzles[barrel].position,muzzles[barrel].rotation);
            }
            for(int i=0;i<plumes.Length;i++)
            {
                var ps=plumes[i];var main=ps.main;var angles=muzzles[i].rotation.eulerAngles*Mathf.Deg2Rad;
                main.startRotationX=angles.x;main.startRotationY=angles.y;main.startRotationZ=angles.z;
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Simulate(0,true,true,false);ps.Play();ps.Emit(1);ps.Pause();
            }
            for(int i=0;i<effects.Length;i++){var ps=effects[i];ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Simulate(0,true,true);ps.Play();ps.Emit(emissionCounts[i]);ps.Pause();}
        }
        public void Render(double clock)
        {
            if(effects==null || !fired)return;
            if(clock<lastTime){Clear(clock);return;}
            float delta=(float)System.Math.Max(0,clock-lastTime);lastTime=clock;
            if(delta<=0)return;
            foreach(var ps in effects){ps.Simulate(delta,true,false,false);ps.Pause();}
            foreach(var ps in plumes){ps.Simulate(delta,true,false,false);ps.Pause();}
            if(LiveParticles==0)fired=false; // All emissions are immediate; no delayed work remains.
        }
        void OnDestroy(){if(material)Destroy(material);if(texture)Destroy(texture);if(flashMaterial)Destroy(flashMaterial);if(flashTexture)Destroy(flashTexture);if(sparkMaterial)Destroy(sparkMaterial);if(sparkTexture)Destroy(sparkTexture);if(plumeMesh)Destroy(plumeMesh);if(plumeMaterial)Destroy(plumeMaterial);if(plumeTexture)Destroy(plumeTexture);}
    }
}
