using System.Linq;
using UnityEngine;

namespace StarTournament.ProvingGround
{
    /// <summary>Reusable twin muzzle systems, advanced exclusively by the session clock.</summary>
    public sealed class VectorShotPresentation : MonoBehaviour
    {
        Transform[] muzzles;
        ParticleSystem[] effects;
        int[] emissionCounts;
        Material material, flashMaterial;
        Texture2D texture, flashTexture;
        double lastTime;
        bool fired;
        public int MuzzleCount => muzzles?.Length ?? 0;
        public int LiveParticles => effects?.Sum(p=>p?p.particleCount:0) ?? 0;
        public Vector3 Muzzle(int index) => muzzles[index%2].position;
        public void Initialize(ProvingProfile profile)
        {
            muzzles=Enumerable.Range(0,2).Select(i=>GetComponentsInChildren<Transform>(true).Single(t=>t.name=="vector-muzzle-"+i)).ToArray();
            texture=ShotgunDetailArt.SmokeTexture();flashTexture=ShotgunDetailArt.FlashTexture();
            material=new Material(Shader.Find("StarTournament/VectorParticle")){mainTexture=texture};
            flashMaterial=new Material(material){mainTexture=flashTexture};
            effects=new ParticleSystem[6];
            emissionCounts=Enumerable.Range(0,6).Select(i=>i%3==1?(int)profile.Get("presentation.vector.sparkCount"):1).ToArray();
            for(int i=0;i<2;i++)
            {
                effects[i*3]=Create("vector-flash",muzzles[i],profile.Get("presentation.vector.muzzleSeconds"),profile.Get("presentation.vector.muzzleSize"),0,1,Color.white,0);
                effects[i*3+1]=Create("vector-sparks",muzzles[i],profile.Get("presentation.vector.sparkSeconds"),profile.Get("presentation.vector.sparkSize"),profile.Get("presentation.vector.sparkSpeed"),(int)profile.Get("presentation.vector.sparkCount"),new Color(1,.78f,.4f),profile.Get("presentation.vector.sparkConeDegrees"));
                effects[i*3+2]=Create("vector-smoke",muzzles[i],profile.Get("presentation.vector.smokeSeconds"),profile.Get("presentation.vector.smokeSize"),profile.Get("presentation.vector.smokeSpeed"),1,new Color(.65f,.7f,.74f,.3f),0);
            }
        }
        ParticleSystem Create(string label,Transform muzzle,float life,float size,float speed,int count,Color color,float angle)
        {
            var obj=new GameObject(label);obj.layer=gameObject.layer;obj.transform.SetParent(muzzle,false);
            // GLB front is +Z. Ignore the imported mount's authored scale for particle sizing.
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.duration=life;main.startLifetime=life;main.startSize=size;main.startSpeed=speed;main.startColor=color;main.maxParticles=Mathf.Max(1,count);main.simulationSpace=ParticleSystemSimulationSpace.World;main.scalingMode=ParticleSystemScalingMode.Shape;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.enabled=speed>0;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=angle;shape.radius=0;
            var fade=ps.colorOverLifetime;fade.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});fade.color=gradient;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=label=="vector-flash"?flashMaterial:material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            ps.useAutoRandomSeed=false;ps.randomSeed=1;return ps;
        }
        public void Clear(double clock)
        {
            lastTime=clock;fired=false;
            if(effects!=null)foreach(var ps in effects)if(ps)ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        public void Fire(double clock)
        {
            if(effects==null)return;
            lastTime=clock;fired=true;
            for(int i=0;i<effects.Length;i++){var ps=effects[i];ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);ps.Simulate(0,true,true);ps.Play();ps.Emit(emissionCounts[i]);ps.Pause();}
        }
        public void Render(double clock)
        {
            if(effects==null || !fired)return;
            if(clock<lastTime){Clear(clock);return;}
            float delta=(float)System.Math.Max(0,clock-lastTime);lastTime=clock;
            if(delta<=0)return;
            foreach(var ps in effects){ps.Simulate(delta,true,false);ps.Pause();}
        }
        void OnDestroy(){if(material)Destroy(material);if(texture)Destroy(texture);if(flashMaterial)Destroy(flashMaterial);if(flashTexture)Destroy(flashTexture);}
    }
}
