using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround
{
    /// <summary>Derived COBALT impulses. No simulation writes, wall clock or gameplay RNG.</summary>
    public sealed class CobaltShotPresentation : IDisposable
    {
        sealed class Muzzle
        {
            public int Participant; public bool View; public GameObject Source; public WeaponModelPresentation Mount;
            public ParticleSystem Flash,Smoke; public Light Light; public double Began=double.NegativeInfinity;
        }
        sealed class ContactFx
        {
            public ParticleSystem Sparks,Dust;public double Began;public Vector3 Point,Normal;public uint Seed;public bool Active;
        }
        readonly NativeCombatSession session;readonly ProvingProfile profile;readonly GameObject root;
        readonly List<Muzzle> muzzles=new List<Muzzle>();readonly List<ContactFx> contacts=new List<ContactFx>();
        readonly Material flashMaterial,sparkMaterial,smokeMaterial;
        readonly Texture2D flashTexture,sparkTexture,smokeTexture;
        readonly ParticleSystem.Particle[] particles=new ParticleSystem.Particle[32];
        int nextContact;bool disposed;
        public bool Enabled {get;}
        public int ActiveContacts=>contacts.Count(c=>c.Active);
        public int ActiveLights=>muzzles.Count(m=>m.Light.enabled);
        public int LiveParticles=>muzzles.Sum(m=>m.Flash.particleCount+m.Smoke.particleCount)+contacts.Sum(c=>c.Sparks.particleCount+c.Dust.particleCount);
        public int RetainedContacts=>contacts.Count;
        float F(string key)=>profile.Get(ProvingProfile.CobaltFxPrefix+key);
        Color Tint=>new Color(F("red"),F("green"),F("blue"));
        public CobaltShotPresentation(NativeCombatSession session,ProvingProfile profile,Transform owner,GameObject[] bodies,GameObject[] views,int[] local)
        {
            this.session=session;this.profile=profile;Enabled=profile.Descriptor(ProvingProfile.CobaltFxPrefix+"enabled")!=null&&F("enabled")>0;
            root=new GameObject("COBALT shot presentation");root.transform.SetParent(owner,false);
            if(!Enabled)return;
            flashTexture=Envelope(true);sparkTexture=Envelope(false);smokeTexture=ShotgunDetailArt.SmokeTexture();
            flashMaterial=new Material(Shader.Find("StarTournament/CobaltShot")){name="COBALT white-hot petals",mainTexture=flashTexture};flashMaterial.SetFloat("_Radiance",F("flashRadiance"));
            sparkMaterial=new Material(flashMaterial){name="COBALT contact sparks",mainTexture=sparkTexture};
            smokeMaterial=new Material(Shader.Find("StarTournament/VectorParticle")){name="COBALT soft smoke",mainTexture=smokeTexture};
            int worldMask=(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer);
            foreach(var body in bodies)worldMask|=1<<body.layer;
            // View impulses get the light budget first. Culling masks select cameras;
            // Forward+ without rendering layers can also light nearby view geometry.
            for(int i=0;i<views.Length;i++)AddMuzzle(views[i],local[i],1<<views[i].layer,true);
            for(int i=0;i<bodies.Length;i++)AddMuzzle(bodies[i],i,worldMask);
        }
        Texture2D Envelope(bool flame)
        {
            // Fixed texel grid and petal frequencies are authored art sampling, not gameplay tuning.
            const int n=128;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false){name=flame?"COBALT asymmetric muzzle petals":"COBALT spark envelope",wrapMode=TextureWrapMode.Clamp};
            var data=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1,r=Mathf.Sqrt(u*u+v*v),a=Mathf.Atan2(v,u);
                float petals=flame?.43f+.16f*Mathf.Cos(a*5+.6f)+.10f*Mathf.Cos(a*3-1):.8f;
                float rim=Mathf.Clamp01((petals-r)*8);
                float core=Mathf.Exp(-r*r*(flame?48:12));
                float grain=flame?.7f+.3f*Mathf.PerlinNoise(u*12+17,v*12+23):1;
                var c=Color.Lerp(Tint,Color.white,Mathf.Clamp01(core*1.8f));c.a=Mathf.Clamp01(rim*grain+core)*Mathf.Clamp01((1-r)*8);data[y*n+x]=c;
            }
            texture.SetPixels(data);texture.Apply(false,true);return texture;
        }
        ParticleSystem System(string name,int layer,int capacity,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.layer=layer;
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.playOnAwake=false;main.loop=false;main.maxParticles=capacity;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.enabled=false;var shape=ps.shape;shape.enabled=false;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            ps.useAutoRandomSeed=false;ps.randomSeed=1;return ps;
        }
        void AddMuzzle(GameObject source,int participant,int lightMask,bool view=false)
        {
            var mount=source.GetComponent<WeaponModelPresentation>();if(!mount)return;
            var m=new Muzzle{Source=source,Participant=participant,View=view,Mount=mount,
                Flash=System("COBALT muzzle "+source.name,source.layer,1,flashMaterial),Smoke=System("COBALT muzzle smoke "+source.name,source.layer,1,smokeMaterial)};
            var lightObject=new GameObject("COBALT muzzle light "+source.name);lightObject.transform.SetParent(root.transform,false);
            m.Light=lightObject.AddComponent<Light>();m.Light.type=LightType.Point;m.Light.enabled=false;m.Light.range=F("lightRange");m.Light.color=Color.Lerp(Tint,Color.white,.25f);m.Light.cullingMask=lightMask;m.Light.shadows=LightShadows.None;
            muzzles.Add(m);
        }
        void Particle(int index,Vector3 position,float size,Color color,float rotation=0)
        {
            particles[index]=new ParticleSystem.Particle{position=position,startSize=size,startColor=color,startLifetime=1,remainingLifetime=1,rotation=rotation,randomSeed=(uint)(index+1)};
        }
        void Set(ParticleSystem ps,int count){ps.SetParticles(particles,count);ps.Pause();}
        public void Fire(int shooter)
        {
            if(!Enabled||disposed)return;
            foreach(var m in muzzles)if(m.Participant==shooter)m.Began=session.Time;
        }
        public void Contact(RifleBulletContact contact)
        {
            if(!Enabled||disposed||contact.Contact.Contact!=PelletContact.World)return;
            ContactFx fx;
            if(contacts.Count<(int)F("maxContacts"))
            {
                fx=new ContactFx{Sparks=System("COBALT metal contact "+contacts.Count,ProvingArena.WorldLayer,32,sparkMaterial),Dust=System("COBALT contact dust "+contacts.Count,ProvingArena.WorldLayer,3,smokeMaterial)};contacts.Add(fx);
            }
            else{fx=contacts[nextContact];nextContact=(nextContact+1)%contacts.Count;}
            fx.Began=contact.Time;fx.Point=contact.Contact.Endpoint;fx.Normal=contact.Contact.Normal.sqrMagnitude>.01f?contact.Contact.Normal.normalized:-contact.Bullet.Direction;fx.Seed=contact.Bullet.Id;fx.Active=true;
        }
        public void Render()
        {
            if(!Enabled||disposed)return;int lights=0;
            foreach(var m in muzzles)
            {
                float age=(float)(session.Time-m.Began);bool valid=age>=0&&m.Source.activeInHierarchy&&m.Mount.ShownWeapon==WeaponId.Rifle&&!session.Life(m.Participant).Dead;
                var point=m.Mount.RifleMuzzle;var direction=m.Mount.RifleDirection;
                float phase=age/F("flashSeconds");
                if(valid&&phase<1){Particle(0,point+direction*F("flashOffset"),F("flashSize")*(1-.35f*phase),new Color(1,1,1,(1-phase)*(1-phase)),(float)(m.Began*997)%360);Set(m.Flash,1);}else Set(m.Flash,0);
                float smoke=age/F("smokeSeconds");
                if(valid&&smoke<1){Particle(0,point+direction*(F("flashOffset")+F("smokeSize")*smoke)+Vector3.up*F("smokeSize")*smoke,F("smokeSize")*(.4f+.6f*smoke),new Color(.75f,.78f,.8f,F("smokeOpacity")*Mathf.Sin(smoke*Mathf.PI)));Set(m.Smoke,1);}else Set(m.Smoke,0);
                float light=age/F("lightSeconds");m.Light.enabled=valid&&light<1&&lights<(int)F("maxLights")&&F(m.View?"viewLightIntensity":"lightIntensity")>0;
                if(m.Light.enabled){lights++;m.Light.transform.position=point-direction*F("lightBack");m.Light.intensity=F(m.View?"viewLightIntensity":"lightIntensity")*(1-light)*(1-light);}
            }
            foreach(var fx in contacts)
            {
                if(!fx.Active)continue;
                float age=(float)(session.Time-fx.Began),t=age/F("impactSeconds");
                if(t>=1||t<0){fx.Active=false;Set(fx.Sparks,0);Set(fx.Dust,0);continue;}
                var n=fx.Normal;var side=Vector3.Cross(n,Mathf.Abs(n.y)>.9f?Vector3.right:Vector3.up).normalized;var up=Vector3.Cross(n,side);
                int count=(int)F("sparkCount");
                // Golden-angle sample pattern is deterministic authored decoration; never consumes session RNG.
                for(int i=0;i<count;i++)
                {
                    float a=i*2.399963f+fx.Seed*.71f;float variation=.5f+.5f*Mathf.Repeat((i+1)*.618034f+fx.Seed*.13f,1);
                    var velocity=(n*.5f+side*Mathf.Cos(a)+up*Mathf.Sin(a)).normalized*F("sparkSpeed")*variation;
                    var point=fx.Point+n*.006f+velocity*age-Vector3.up*(F("sparkGravity")*age*age*.5f);
                    var color=Color.Lerp(Color.white,Tint,t);color.a=(1-t)*(1-t);
                    Particle(i,point,F("sparkSize")*(1-t*.7f),color);
                }
                if(t<.25f){Particle(count,fx.Point+n*.008f,F("impactSize")*(1-t*4),new Color(1,1,1,1-t*4));count++;}
                Set(fx.Sparks,count);
                for(int i=0;i<3;i++){var point=fx.Point+n*(.015f+F("dustSize")*t*.3f)+(side*Mathf.Sin(i*2.1f)+up*Mathf.Cos(i*2.1f))*F("dustSize")*t*.2f;Particle(i,point,F("dustSize")*(.25f+t*.75f),new Color(.55f,.57f,.6f,F("dustOpacity")*Mathf.Sin(Mathf.PI*t)));}Set(fx.Dust,3);
            }
        }
        public void Clear()
        {
            if(!Enabled)return;
            foreach(var m in muzzles){m.Began=double.NegativeInfinity;m.Light.enabled=false;Set(m.Flash,0);Set(m.Smoke,0);}
            foreach(var fx in contacts){fx.Active=false;Set(fx.Sparks,0);Set(fx.Dust,0);}
        }
        public void Dispose()
        {
            if(disposed)return;Clear();disposed=true;Object.Destroy(root);
            foreach(var m in new[]{flashMaterial,sparkMaterial,smokeMaterial})if(m)Object.Destroy(m);
            foreach(var t in new[]{flashTexture,sparkTexture,smokeTexture})if(t)Object.Destroy(t);
        }
    }
}
