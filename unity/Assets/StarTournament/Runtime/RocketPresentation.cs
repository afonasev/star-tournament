using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace StarTournament.ProvingGround
{
    /// <summary>Derived hot-impulse visuals. No physics, gameplay RNG or wall-clock animation.</summary>
    public sealed class RocketPresentation : IDisposable
    {
        sealed class Flight
        {
            public GameObject Root;
            public LineRenderer Flame,Core;
            public ParticleSystem Smoke;
        }
        sealed class Ignition
        {
            public GameObject Root;public LineRenderer Flame;public double Time;public Vector3 Position,Direction;public uint Id;public bool Active;
        }
        readonly List<Ignition> ignitions=new List<Ignition>();
        public int ActiveIgnitions {get {int n=0;foreach(var i in ignitions)if(i.Active)n++;return n;}}
        sealed class Burst
        {
            public GameObject Root;
            public ParticleSystem Fire,Core,Sparks,Smoke;
            public double Time;
            public uint Id;
            public bool Active;
            public Vector3 Center;
            public float Radius;public Light Light;
        }
        readonly NativeCombatSession session;
        readonly ProvingProfile profile,effects;
        readonly Transform owner;
        readonly GameObject prefab;
        readonly Dictionary<uint,Flight> flights=new Dictionary<uint,Flight>();
        readonly Dictionary<uint,Vector3> launchOffsets=new Dictionary<uint,Vector3>();
        readonly List<Burst> bursts=new List<Burst>();
        readonly List<uint> removed=new List<uint>();
        readonly HashSet<uint> liveIds=new HashSet<uint>();
        readonly Material fireMaterial,exhaustMaterial,smokeMaterial,burstSmokeMaterial,glowMaterial;
        readonly Texture2D fireTexture,exhaustTexture,smokeTexture,glowTexture;
        readonly ParticleSystem.Particle[] particles;
        // Depth is requested only on this presentation's cameras while a burst is alive.
        // Remember the tri-state option, including UsePipelineSettings, for exact restoration.
        readonly Dictionary<UniversalAdditionalCameraData,CameraOverrideOption> depthOptions=new Dictionary<UniversalAdditionalCameraData,CameraOverrideOption>();
        readonly List<UniversalAdditionalCameraData> addedCameraData=new List<UniversalAdditionalCameraData>();
        sealed class SoftDepthInput:ScriptableRenderPass
        {
            // This renderer normally copies depth AFTER transparent objects. Declare
            // Pulse's earlier read so URP schedules the copy before our billboards.
            public SoftDepthInput(){renderPassEvent=RenderPassEvent.BeforeRenderingTransparents;ConfigureInput(ScriptableRenderPassInput.Depth);}
            public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData){}
            public override void Execute(ScriptableRenderContext context,ref RenderingData renderingData){}
        }
        readonly SoftDepthInput softDepthInput=new SoftDepthInput();
        void BeforeCamera(ScriptableRenderContext context,Camera camera)
        {
            if(ActiveBursts==0)return;
            var data=camera.GetComponent<UniversalAdditionalCameraData>();
            if(data&&depthOptions.ContainsKey(data))data.scriptableRenderer.EnqueuePass(softDepthInput);
        }
        public int ActiveFlights=>flights.Count;
        public int ActiveBursts {get {int n=0;foreach(var b in bursts)if(b.Active)n++;return n;}}
        public int PooledBursts=>bursts.Count;
        public int ActiveLights{get{int count=0;foreach(var b in bursts)if(b.Light.enabled)count++;return count;}}
        public float FlameEnvelope{get{float extent=0;foreach(var b in bursts)if(b.Active){int count=b.Fire.GetParticles(particles);for(int i=0;i<count;i++)extent=Mathf.Max(extent,Vector3.Distance(particles[i].position,b.Center)+particles[i].startSize*.5f);}return extent;}}
        public int LiveParticles {get {int n=0;foreach(var b in bursts)if(b.Active)n+=b.Fire.particleCount+b.Core.particleCount+b.Sparks.particleCount+b.Smoke.particleCount;foreach(var f in flights.Values)n+=f.Smoke.particleCount;return n;}}
        public RocketPresentation(NativeCombatSession session,ProvingProfile profile,Transform owner,GameObject prefab=null,ProvingProfile effects=null)
        {
            this.session=session;this.profile=profile;this.owner=owner;this.prefab=prefab;
            this.effects=effects??ProvingProfile.CreateRocketEffectsDefault();
            // Fixed procedural sprite sampling/motif is authored art, independent of visual size/lifetime.
            fireTexture=RequiredArt("FireAtlas");
            exhaustTexture=RequiredArt("Exhaust");
            smokeTexture=RequiredArt("Smoke");
            glowTexture=CloudTexture("Pulse hot core",false,true);
            fireMaterial=Material(fireTexture,true);exhaustMaterial=Material(exhaustTexture);smokeMaterial=Material(smokeTexture);burstSmokeMaterial=Material(smokeTexture,true);glowMaterial=Material(glowTexture);
            particles=new ParticleSystem.Particle[Mathf.Max(1,Mathf.Max((int)F("fireCount"),Mathf.Max((int)F("sparkCount"),Mathf.Max((int)F("smokeCount"),(int)F("trailSmokeCount")))))];
            session.RocketExploded+=Explode;session.StateRestored+=Clear;RenderPipelineManager.beginCameraRendering+=BeforeCamera;
        }
        float P(string key)=>profile.Get("presentation."+key);
        float F(string key)=>effects.Get("pulseFx."+key);
        Color Hot=>new Color(P("rocketRed"),P("rocketGreen"),P("rocketBlue"));
        Color CoreColor(float alpha){var color=Color.Lerp(Color.white,Hot,F("coreWarmth"));color.a=alpha;return color;}
        Material Material(Texture2D texture,bool soft=false)
        {
            var shader=Shader.Find(soft?"StarTournament/PulseSoftParticle":"StarTournament/VectorParticle");
            if(!shader)throw new InvalidOperationException("Pulse particle shader missing from build");
            var material=new Material(shader){name=texture.name,mainTexture=texture};
            if(soft){material.SetFloat("_FadeDistance",F("surfaceFadeMeters"));material.SetFloat("_Radiance",texture==fireTexture?F("fireRadiance"):1);}
            return material;
        }
        void UpdateDepthRequests()
        {
            if(ActiveBursts==0){RestoreDepthRequests();return;}
            foreach(var camera in owner.GetComponentsInChildren<Camera>(true))
            {
                var data=camera.GetComponent<UniversalAdditionalCameraData>();
                if(!data){data=camera.gameObject.AddComponent<UniversalAdditionalCameraData>();addedCameraData.Add(data);}
                if(depthOptions.ContainsKey(data))continue;
                depthOptions.Add(data,data.requiresDepthOption);data.requiresDepthTexture=true;
            }
        }
        void RestoreDepthRequests()
        {
            foreach(var pair in depthOptions)if(pair.Key)pair.Key.requiresDepthOption=pair.Value;
            depthOptions.Clear();
        }
        static Texture2D CloudTexture(string name,bool fire,bool glow)
        {
            // Fixed noise frequencies and palette form the authored texture, not simulation parameters.
            const int resolution=128;
            var texture=new Texture2D(resolution,resolution,TextureFormat.RGBA32,false){name=name,wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<resolution;y++)for(int x=0;x<resolution;x++)
            {
                float u=(x+.5f)/resolution,v=(y+.5f)/resolution;
                float r=new Vector2(u*2-1,v*2-1).magnitude;
                float noise=Mathf.PerlinNoise(u*6.4f+2.7f,v*6.4f+9.3f)*.65f+Mathf.PerlinNoise(u*15.6f+17.1f,v*15.6f+3.8f)*.35f;
                float boundary=.78f+.22f*Mathf.PerlinNoise(u*8+4,v*8+12);
                float alpha=glow?Mathf.Pow(Mathf.Clamp01(1-r),2):Mathf.SmoothStep(0,1,Mathf.Clamp01((boundary-r)*7))*(.35f+.65f*noise);
                float heat=Mathf.Clamp01((1-r)*.7f+noise*.75f-.24f);
                Color color=fire?Color.Lerp(new Color(.6f,.045f,.008f),Color.Lerp(new Color(1,.24f,.018f),new Color(1,.91f,.52f),heat),heat):new Color(.55f+noise*.45f,.55f+noise*.45f,.55f+noise*.45f);
                if(glow)color=Color.white;
                color.a=alpha;texture.SetPixel(x,y,color);
            }
            texture.Apply(false,true);return texture;
        }
        static Texture2D RequiredArt(string name)
        {
            var texture=Resources.Load<Texture2D>("PulseMk2/"+name);
            if(!texture)throw new InvalidOperationException("Pulse Mk II art missing: "+name);
            return texture; // Shared imported art: this presentation does not destroy it.
        }
        LineRenderer Line(string name,Transform parent,Material material,float width)
        {
            var root=new GameObject(name);root.transform.SetParent(parent,false);
            var line=root.AddComponent<LineRenderer>();line.sharedMaterial=material;line.positionCount=3;line.useWorldSpace=true;
            line.startWidth=width;line.endWidth=0;line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            // Authored taper/temperature motif; absolute width/length/tint are profile parameters.
            line.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(.3f,.65f),new Keyframe(1,0));
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Hot,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(.65f,.5f),new GradientAlphaKey(0,1)});line.colorGradient=gradient;line.widthMultiplier=width;
            return line;
        }
        ParticleSystem System(string name,Transform parent,Material material,int count,bool sparks=false)
        {
            var obj=new GameObject(name);obj.transform.SetParent(parent,false);
            var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=false;main.playOnAwake=false;main.maxParticles=Mathf.Max(1,count);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.enabled=false;
            var shape=ps.shape;shape.enabled=false;
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            if(sparks){renderer.renderMode=ParticleSystemRenderMode.Stretch;renderer.velocityScale=F("sparkStretch");renderer.lengthScale=1;}
            ps.Pause();return ps;
        }
        Burst CreateBurst()
        {
            var root=new GameObject("Pulse hot impulse");root.transform.SetParent(owner,false);
            var b=new Burst{Root=root};
            b.Fire=System("Fire lobes",root.transform,fireMaterial,(int)F("fireCount"));
            var sheet=b.Fire.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=4;sheet.numTilesY=4;sheet.startFrame=new ParticleSystem.MinMaxCurve(0,15);
            var lightObject=new GameObject("Explosion light");lightObject.transform.SetParent(root.transform,false);b.Light=lightObject.AddComponent<Light>();b.Light.type=LightType.Point;b.Light.shadows=LightShadows.None;b.Light.renderMode=LightRenderMode.ForcePixel;b.Light.enabled=false;
            b.Core=System("Hot core",root.transform,glowMaterial,1);
            b.Sparks=System("Sparse sparks",root.transform,glowMaterial,(int)F("sparkCount"),true);
            b.Smoke=System("Dissipating smoke",root.transform,burstSmokeMaterial,(int)F("smokeCount"));
            bursts.Add(b);return b;
        }
        void Explode(RocketExplosion e)
        {
            Burst chosen=null;
            foreach(var b in bursts)
            {
                if(b.Active&&b.Id==e.Rocket.Id&&b.Time==e.Time)return;
                if(!b.Active)chosen=b;
            }
            if(chosen==null&&bursts.Count<(int)F("maxBursts"))chosen=CreateBurst();
            if(chosen==null)
            {
                chosen=bursts[0];foreach(var b in bursts)if(b.Time<chosen.Time)chosen=b;
            }
            chosen.Id=e.Rocket.Id;chosen.Time=e.Time;chosen.Center=e.Position;chosen.Radius=session.RocketBlastRadius;chosen.Root.transform.position=e.Position;chosen.Light.transform.localPosition=-e.Rocket.Direction*chosen.Radius*F("lightOffsetScale");chosen.Light.enabled=false;chosen.Active=true;chosen.Root.SetActive(true);
            DrawBurst(chosen);SelectLights();UpdateDepthRequests();
        }
        public void Launch(uint id,Vector3 offset)=>launchOffsets[id]=offset;
        public void Launch(uint id,Vector3 offset,Vector3 muzzle,Vector3 direction)
        {
            Launch(id,offset);
            foreach(var existing in ignitions)if(existing.Id==id&&existing.Active)return;
            Ignition chosen=null;foreach(var item in ignitions)if(!item.Active){chosen=item;break;}
            if(chosen==null&&ignitions.Count<(int)F("maxBursts"))
            {
                var root=new GameObject("Pulse Mk II ignition");root.transform.SetParent(owner,false);
                chosen=new Ignition{Root=root,Flame=Line("Muzzle ignition",root.transform,exhaustMaterial,F("ignitionSize"))};ignitions.Add(chosen);
            }
            if(chosen==null){chosen=ignitions[0];foreach(var item in ignitions)if(item.Time<chosen.Time)chosen=item;}
            chosen.Id=id;chosen.Position=muzzle;chosen.Direction=direction.normalized;chosen.Time=session.Time;chosen.Active=true;chosen.Root.SetActive(true);DrawIgnition(chosen);
        }
        void DrawIgnition(Ignition item)
        {
            float age=Mathf.Max(0,(float)(session.Time-item.Time)),t=Mathf.Clamp01(age/F("ignitionSeconds"));
            item.Active=t<1;item.Root.SetActive(item.Active);if(!item.Active)return;
            item.Flame.widthMultiplier=F("ignitionSize")*(1-t);
            var length=F("ignitionLength")*(1-t*t);
            item.Flame.SetPosition(0,item.Position);item.Flame.SetPosition(1,item.Position+item.Direction*length*.3f);item.Flame.SetPosition(2,item.Position+item.Direction*length);
        }
        Vector3 Offset(uint id,float distance)=>launchOffsets.TryGetValue(id,out var offset)?offset*(1-Mathf.Clamp01(distance/P("rocketMuzzleBlendDistance"))):Vector3.zero;
        static Vector3 Direction(int index,int count,uint seed)
        {
            // Golden-angle sphere is an authored sampling invariant; it does not touch gameplay RNG.
            float y=1-2*(index+.5f)/Mathf.Max(1,count),radius=Mathf.Sqrt(1-y*y),angle=index*2.399963f+(seed%997)*.017f;
            return new Vector3(Mathf.Cos(angle)*radius,y,Mathf.Sin(angle)*radius);
        }
        void Particle(int index,Vector3 position,float size,Color color,Vector3 velocity,uint seed)
        {
            particles[index]=new ParticleSystem.Particle{position=position,startSize=size,startColor=color,velocity=velocity,startLifetime=1,remainingLifetime=1,randomSeed=seed,rotation=(seed%360)};
        }
        void Set(ParticleSystem ps,int count){ps.SetParticles(particles,count);ps.Pause();}
        void DrawBurst(Burst b)
        {
            float age=Mathf.Max(0,(float)(session.Time-b.Time)),life=P("rocketExplosionSeconds"),t=Mathf.Clamp01(age/life),radius=b.Radius*F("blastRadiusScale");
            // Authored eased expansion/fade shape. All absolute scales/counts/times have Lab metadata.
            int fire=t<1?(int)F("fireCount"):0;
            // Frozen values and session-age envelopes are identical for every lobe.
            // Hoist them out of the particle loops without changing their arithmetic.
            var hot=Hot;float fireVariation=F("fireSizeVariation");
            float spreadWeight=Mathf.Sqrt(t)*F("fireSpreadScale"),lobeWeight=F("fireLobeScale")*Mathf.Sin(Mathf.PI*Mathf.Sqrt(t))*.5f;
            float scale=radius*Mathf.Clamp01(Mathf.Sqrt(t)*2.4f)/Mathf.Max(.0001f,spreadWeight+lobeWeight),spread=spreadWeight*scale;
            var fireColor=Color.Lerp(Color.white,hot,t*t);fireColor.a=(1-t)*(1-t);
            for(int i=0;i<fire;i++)
            {
                var direction=Direction(i,fire,b.Id);
                float variety=1-fireVariation+fireVariation*(Mathf.Sin(i*2.1f+b.Id)+1)*.5f;
                // Normalize centers + half-diameter to the real radius; authored fast opening then fade.

                Particle(i,b.Center+direction*spread,lobeWeight*2*scale*variety,fireColor,Vector3.zero,b.Id+(uint)i);
            }
            var sheet=b.Fire.textureSheetAnimation;sheet.frameOverTime=new ParticleSystem.MinMaxCurve((Mathf.Floor(age*F("fireFramesPerSecond"))%16)/16f);
            Set(b.Fire,fire);
            float lightPhase=Mathf.Clamp01(age/F("lightSeconds"));b.Light.range=b.Radius*F("lightRangeScale");
            b.Light.color=Color.Lerp(Color.white,Hot,F("lightWarmth"));b.Light.intensity=F("lightIntensity")*(1-lightPhase)*(1-lightPhase);
            float core=Mathf.Clamp01(age/F("coreSeconds"));
            Particle(0,b.Center,radius*F("coreScale")*(1+core),CoreColor((1-core)*(1-core)),Vector3.zero,b.Id);Set(b.Core,core<1?1:0);
            float sparks=Mathf.Clamp01(age/F("sparkSeconds"));int sparkCount=sparks<1?(int)F("sparkCount"):0;
            float sparkSpeed=F("sparkSpeed"),sparkVariation=F("sparkSpeedVariation"),sparkSize=F("sparkSize")*(1-sparks);
            var sparkColor=Color.Lerp(CoreColor(1),hot,sparks);sparkColor.a=1-sparks;
            for(int i=0;i<sparkCount;i++)
            {
                var direction=Direction(i,sparkCount,b.Id+31);float speed=sparkSpeed*(1-sparkVariation+sparkVariation*(i%3)/2);
                Particle(i,b.Center+direction*(speed*age),sparkSize,sparkColor,direction*speed,b.Id+(uint)i);
            }
            Set(b.Sparks,sparkCount);
            float smokeAge=age-F("smokeDelay"),s=Mathf.Clamp01(smokeAge/F("smokeSeconds"));int smokeCount=smokeAge>0&&s<1?(int)F("smokeCount"):0;
            float shade=F("smokeShade"),smokeSpreadScale=F("smokeSpreadScale");
            var smokeColor=new Color(shade,shade,shade,F("smokeOpacity")*Mathf.Sin(Mathf.PI*s));
            var rise=Vector3.up*F("smokeRise")*smokeAge;float smokeSize=radius*F("smokeScale")*(F("smokeInitialScale")+s);
            for(int i=0;i<smokeCount;i++)
            {
                var direction=Direction(i,smokeCount,b.Id+71);
                Particle(i,b.Center+direction*radius*Mathf.Sqrt(s)*smokeSpreadScale+rise,smokeSize,smokeColor,Vector3.zero,b.Id+(uint)i);
            }
            Set(b.Smoke,smokeCount);
            if(age>=Mathf.Max(F("lightSeconds"),Mathf.Max(F("coreSeconds"),Mathf.Max(life,Mathf.Max(F("sparkSeconds"),F("smokeDelay")+F("smokeSeconds")))))){b.Active=false;b.Light.enabled=false;b.Root.SetActive(false);}
        }
        bool diagnosticLights=true;
        public void SetDiagnosticLights(bool enabled){diagnosticLights=enabled;SelectLights();}
        void SelectLights()
        {
            foreach(var b in bursts)
            {
                int rank=0;foreach(var other in bursts)if(other.Active&&other.Light.intensity>0&&(other.Time>b.Time||other.Time==b.Time&&other.Id>b.Id))rank++;
                b.Light.enabled=diagnosticLights&&b.Active&&b.Light.intensity>0&&rank<(int)F("maxLights");
            }
        }
        static readonly Unity.Profiling.ProfilerMarker renderMarker=new Unity.Profiling.ProfilerMarker("ST.Rocket.Render");
        public void Render() {using(renderMarker.Auto())RenderCore();}
        void RenderCore()
        {
            var rockets=session.Rockets;liveIds.Clear();foreach(var r in rockets)liveIds.Add(r.Id);
            removed.Clear();foreach(var id in flights.Keys)if(!liveIds.Contains(id))removed.Add(id);
            foreach(var id in removed){UnityEngine.Object.Destroy(flights[id].Root);flights.Remove(id);launchOffsets.Remove(id);}
            removed.Clear();foreach(var id in launchOffsets.Keys)if(!liveIds.Contains(id))removed.Add(id);foreach(var id in removed)launchOffsets.Remove(id);
            foreach(var r in rockets)
            {
                if(!flights.TryGetValue(r.Id,out var flight))
                {
                    var root=new GameObject("Pulse rocket "+r.Id);root.transform.SetParent(owner,false);
                    if(prefab)
                    {
                        var model=UnityEngine.Object.Instantiate(prefab,root.transform);model.name="pulse-projectile";
                        // Authored body radius .23m and total length 1m; topology dimensions are asset invariants.
                        model.transform.localScale=new Vector3(P("rocketBodyRadius")/.23f,P("rocketBodyRadius")/.23f,P("rocketBodyLength"));
                        foreach(var renderer in model.GetComponentsInChildren<Renderer>()){renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;}
                    }
                    flights[r.Id]=flight=new Flight{Root=root,Flame=Line("Hot exhaust",root.transform,exhaustMaterial,F("exhaustWidth")),Core=Line("Exhaust core",root.transform,glowMaterial,P("rocketTrailWidth")),Smoke=System("Light flight smoke",root.transform,smokeMaterial,(int)F("trailSmokeCount"))};
                }
                var position=r.Position+Offset(r.Id,r.Distance);flight.Root.transform.SetPositionAndRotation(position,Quaternion.LookRotation(r.Direction));
                var nozzle=position-r.Direction*P("rocketBodyLength")*.49f;
                float length=Mathf.Min(r.Distance,F("exhaustLength"));
                flight.Flame.SetPosition(0,nozzle);flight.Flame.SetPosition(1,nozzle-r.Direction*length*.3f);flight.Flame.SetPosition(2,nozzle-r.Direction*length);
                flight.Core.SetPosition(0,nozzle);flight.Core.SetPosition(1,nozzle-r.Direction*length*.2f);flight.Core.SetPosition(2,nozzle-r.Direction*length*F("exhaustCoreRatio"));
                int count=r.Distance>0?(int)F("trailSmokeCount"):0;float trail=Mathf.Min(r.Distance,P("rocketTrailLength"));
                for(int i=0;i<count;i++)
                {
                    float t=(i+1f)/(count+1f),distance=trail*t;
                    var color=new Color(F("smokeShade"),F("smokeShade"),F("smokeShade"),F("trailSmokeOpacity")*(1-t));
                    Particle(i,r.Position-r.Direction*(distance+P("rocketBodyLength")*.49f)+Offset(r.Id,Mathf.Max(0,r.Distance-distance)),F("trailSmokeSize")*(1+t),color,Vector3.zero,r.Id+(uint)i);
                }
                Set(flight.Smoke,count);
            }
            foreach(var item in ignitions)if(item.Active)DrawIgnition(item);
            foreach(var b in bursts)if(b.Active)DrawBurst(b);SelectLights();UpdateDepthRequests();
        }
        void Clear()
        {
            RestoreDepthRequests();
            foreach(var item in ignitions){item.Active=false;if(item.Root)item.Root.SetActive(false);}
            launchOffsets.Clear();foreach(var f in flights.Values)if(f.Root)UnityEngine.Object.Destroy(f.Root);flights.Clear();
            foreach(var b in bursts){b.Active=false;b.Light.enabled=false;if(b.Root)b.Root.SetActive(false);}
        }
        public void Dispose()
        {
            RenderPipelineManager.beginCameraRendering-=BeforeCamera;
            session.RocketExploded-=Explode;session.StateRestored-=Clear;Clear();
            foreach(var b in bursts)if(b.Root)UnityEngine.Object.Destroy(b.Root);bursts.Clear();
            UnityEngine.Object.Destroy(fireMaterial);UnityEngine.Object.Destroy(exhaustMaterial);UnityEngine.Object.Destroy(smokeMaterial);UnityEngine.Object.Destroy(burstSmokeMaterial);UnityEngine.Object.Destroy(glowMaterial);
            foreach(var data in addedCameraData)if(data)UnityEngine.Object.Destroy(data);addedCameraData.Clear();
            foreach(var item in ignitions)if(item.Root)UnityEngine.Object.Destroy(item.Root);ignitions.Clear();
            UnityEngine.Object.Destroy(glowTexture);
        }
    }
}
