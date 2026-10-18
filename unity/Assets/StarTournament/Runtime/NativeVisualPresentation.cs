using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace StarTournament.ProvingGround
{
    public sealed class NativeVisualPresentation : MonoBehaviour
    {
        struct Surface {public Material Material;public VisualMaterialRole Role;public float Metallic,Smoothness;}
        struct LocalLight {public Light Light;public float Intensity;}
        readonly List<LocalLight> localLights=new List<LocalLight>();
        string localLightPath;ProvingProfile localLightProfile,wellSettings;Tonemapping tone;
        RenderPipelineAsset priorQualityPipeline;UniversalRenderPipelineAsset wellPipeline;
        readonly List<Surface> surfaces=new List<Surface>();
        readonly Dictionary<GameObject,int> layers=new Dictionary<GameObject,int>();
        ProvingProfile profile;Light key;float keyIntensity;Color ambient;AmbientMode ambientMode;
        Volume volume;VolumeProfile volumeProfile;Bloom bloom;ColorCurves curves;ColorAdjustments color;ReflectionProbe probe;Transform arena;
        Camera[] cameras;bool bound;
        public int SurfaceCount=>surfaces.Count;
        public ReflectionProbe EnvironmentProbe=>probe;
        public void Bind(ProvingProfile settings,Transform world,Light studio,Camera[] views)
        {
            ResetWorld();profile=settings;arena=world;key=studio;cameras=views;
            var well=world.GetComponentInChildren<WellArenaPresentation>();wellSettings=well&&well.Settings.Version>=5?well.Settings:null;
            // Neutral tonemapping needs an HDR grading LUT: the LDR LUT clips bright wall detail
            // before tonemapping. Own only a transient map-local copy; never mutate the project asset.
            if(wellSettings!=null&&GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline&&pipeline.colorGradingMode!=ColorGradingMode.HighDynamicRange)
            {
                priorQualityPipeline=QualitySettings.renderPipeline;wellPipeline=Instantiate(pipeline);wellPipeline.name="Well daylight HDR grading (owned)";
                wellPipeline.colorGradingMode=ColorGradingMode.HighDynamicRange;QualitySettings.renderPipeline=wellPipeline;
            }
            ambient=RenderSettings.ambientLight;ambientMode=RenderSettings.ambientMode;keyIntensity=key.intensity;bound=true;
            foreach(var registry in world.GetComponentsInChildren<NativeVisualMaterials>(true))
                foreach(var pair in registry.Roles)
                    if(pair.Key&&pair.Key.HasProperty("_Metallic")&&pair.Key.HasProperty("_Smoothness"))
                        surfaces.Add(new Surface{Material=pair.Key,Role=pair.Value,Metallic=pair.Key.GetFloat("_Metallic"),Smoothness=pair.Key.GetFloat("_Smoothness")});
            // Only collider-free presentation renderers join the existing world rendering layer.
            // This permits a world-only cubemap capture without participants or first-person arms.
            foreach(var renderer in world.GetComponentsInChildren<Renderer>(true))
                if(renderer.gameObject.layer==0&&!renderer.GetComponent<Collider>())
                {layers[renderer.gameObject]=0;renderer.gameObject.layer=ProvingArena.WorldLayer;}
            localLightPath=world.GetComponentInChildren<IndustrialTunnelsPresentation>()?"visual.tunnelsLocalLightScale":
                world.GetComponentInChildren<LunarLaboratoryPresentation>()?"visual.lunarLocalLightScale":
                world.GetComponentInChildren<OrbitalLeaguePresentation>()?"visual.bowlLocalLightScale":wellSettings!=null?"well.localLightScale":null;
            localLightProfile=wellSettings??profile;
            // Arena-owned lamps only. Camera-local equipment fill and participant materials stay outside this registry.
            if(localLightPath!=null)foreach(var lamp in world.GetComponentsInChildren<Light>(true))
                localLights.Add(new LocalLight{Light=lamp,Intensity=lamp.intensity});
            Apply();
        }
        public void BindCameras(Camera[] views){cameras=views;if(bound)ApplyCameras();}
        public void Apply()
        {
            if(!bound)return;
            float light=NativeVisualPreferences.Effective(profile,VisualEffect.Lighting);
            // Preserve each arena's authored tint while establishing a bounded diffuse floor.
            Color tint=ambient.maxColorComponent>0?ambient/ambient.maxColorComponent:Color.white;
            RenderSettings.ambientLight=ambient*Mathf.Lerp(1,profile.Get("visual.fillScale"),light)+tint*(profile.Get("visual.ambientLift")*light);
            key.intensity=keyIntensity*Mathf.Lerp(1,profile.Get("visual.keyScale"),light);
            foreach(var lamp in localLights)if(lamp.Light)
                lamp.Light.intensity=lamp.Intensity*Mathf.Lerp(1,localLightProfile.Get(localLightPath),light);
            float materials=NativeVisualPreferences.Effective(profile,VisualEffect.Materials);
            foreach(var s in surfaces)if(s.Material)
            {
                string role=s.Role==VisualMaterialRole.Wall?"wall":s.Role==VisualMaterialRole.Floor?"floor":"metal";
                s.Material.SetFloat("_Metallic",Mathf.Lerp(s.Metallic,profile.Get("visual."+role+"Metallic"),materials));
                s.Material.SetFloat("_Smoothness",Mathf.Lerp(s.Smoothness,profile.Get("visual."+role+"Smoothness"),materials));
            }
            float reflection=NativeVisualPreferences.Effective(profile,VisualEffect.Reflections);
            if(reflection>0&&!probe&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {
                var go=new GameObject("visual-environment-reflection");go.transform.SetParent(transform,false);probe=go.AddComponent<ReflectionProbe>();
                var bounds=new Bounds(arena.position,Vector3.zero);foreach(var collider in arena.GetComponentsInChildren<Collider>())bounds.Encapsulate(collider.bounds);
                var projection=arena.GetComponent<ProvingArena>();
                // An existing walkable spawn is outside walls and slabs; capture from eye height.
                probe.transform.position=projection.Spawns[0]+Vector3.up*profile.Get("camera.eyeHeight");probe.center=bounds.center-probe.transform.position;probe.size=bounds.size;probe.boxProjection=true;
                // Fixed 128/256px captures are bounded technical resource tiers, not artistic tuning.
                // The Well's multi-storey atrium supplies a representative environment; a corner-room spawn does not.
                bool well=arena.GetComponentInChildren<WellArenaPresentation>();
                if(well){probe.transform.position=arena.position+new Vector3(0,3.2f,0);probe.center=bounds.center-probe.transform.position;}
                probe.resolution=well?256:128;probe.mode=ReflectionProbeMode.Realtime;probe.refreshMode=ReflectionProbeRefreshMode.ViaScripting;
                probe.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;probe.cullingMask=(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer);
                probe.nearClipPlane=.1f;probe.farClipPlane=bounds.size.magnitude;probe.clearFlags=ReflectionProbeClearFlags.Skybox;
                if(well){probe.clearFlags=ReflectionProbeClearFlags.SolidColor;probe.backgroundColor=new Color(.8f,.87f,.94f);}
                probe.RenderProbe();
            }
            if(probe){probe.intensity=reflection*profile.Get("visual.reflectionIntensity");probe.enabled=reflection>0;}
            float glow=NativeVisualPreferences.Effective(profile,VisualEffect.Bloom);
            float grading=NativeVisualPreferences.Effective(profile,VisualEffect.ColorGrading);
            if((glow>0||grading>0)&&!volume)
            {
                var source=Resources.Load<VolumeProfile>("VisualPolishVolume");
                if(!source)throw new System.InvalidOperationException("Missing visual polish VolumeProfile");
                volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();foreach(var component in source.components)volumeProfile.components.Add(Instantiate(component));
                volume=gameObject.AddComponent<Volume>();volume.isGlobal=true;volume.priority=100;volume.sharedProfile=volumeProfile;volumeProfile.TryGet(out bloom);volumeProfile.TryGet(out curves);volumeProfile.TryGet(out color);
                if(wellSettings!=null){tone=volumeProfile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.Neutral);}
            }
            if(volume)
            {
                volume.enabled=glow>0||grading>0;bloom.active=glow>0;
                bloom.intensity.Override(glow*profile.Get("visual.bloomIntensity")*(wellSettings!=null?wellSettings.Get("well.bloomScale"):1));bloom.threshold.Override(profile.Get("visual.bloomThreshold"));bloom.scatter.Override(profile.Get("visual.bloomScatter"));
                curves.active=color.active=grading>0;
                curves.master.value.Release();curves.master.Override(NeutralCurve(grading,profile.Get("visual.ColorGradingShadowLift"),profile.Get("visual.ColorGradingHighlightCompression")));
                if(tone)tone.active=grading>0;
                color.postExposure.Override(grading*(NativeVisualPreferences.Adjustment(profile,NativeVisualPreferences.ExposurePath)+(wellSettings!=null?wellSettings.Get("well.gradingExposure"):0)));
                color.saturation.Override(grading*(NativeVisualPreferences.Adjustment(profile,NativeVisualPreferences.SaturationPath)-100));
            }
            ApplyCameras();
        }
        public static TextureCurve NeutralCurve(float strength,float shadowLift,float highlightCompression)
        {
            // Five uniform samples represent one shared RGB curve. The analytic tangents
            // keep it smooth and monotone within the descriptor limits; black stays black.
            var keys=new Keyframe[5];
            for(int i=0;i<keys.Length;i++)
            {
                float x=i/(float)(keys.Length-1),lift=strength*shadowLift,compression=strength*highlightCompression;
                float y=x+4*lift*x*(1-x)*(1-x)-compression*x*x*x*x;
                float tangent=1+4*lift*(1-4*x+3*x*x)-4*compression*x*x*x;
                keys[i]=new Keyframe(x,y,tangent,tangent);
            }
            return new TextureCurve(keys,0,false,new Vector2(0,1));
        }
        void ApplyCameras()
        {
            if(cameras==null)return;
            int renderer=Mathf.RoundToInt(NativeVisualPreferences.Effective(profile,VisualEffect.AmbientOcclusion)*4);
            bool antialiasing=NativeVisualPreferences.AntialiasingLevel>NativeVisualPreferences.AntialiasingOff;
            bool post=antialiasing||NativeVisualPreferences.Effective(profile,VisualEffect.Bloom)>0||NativeVisualPreferences.Effective(profile,VisualEffect.ColorGrading)>0;
            foreach(var camera in cameras)if(camera)
            {
                var data=camera.GetUniversalAdditionalCameraData();data.SetRenderer(renderer);data.renderPostProcessing=post;camera.allowHDR=true;
                data.antialiasing=antialiasing?AntialiasingMode.SubpixelMorphologicalAntiAliasing:AntialiasingMode.None;
                data.antialiasingQuality=(AntialiasingQuality)Mathf.Clamp(NativeVisualPreferences.AntialiasingLevel-1,0,2);
            }
        }
        public void ResetWorld()
        {
            if(bound){RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=ambientMode;if(key)key.intensity=keyIntensity;}
            foreach(var lamp in localLights)if(lamp.Light)lamp.Light.intensity=lamp.Intensity;localLights.Clear();
            foreach(var s in surfaces)if(s.Material){s.Material.SetFloat("_Metallic",s.Metallic);s.Material.SetFloat("_Smoothness",s.Smoothness);}surfaces.Clear();
            foreach(var p in layers)if(p.Key)p.Key.layer=p.Value;layers.Clear();
            if(probe){probe.enabled=false;DestroyImmediate(probe.gameObject);}probe=null;
            if(volume){volume.enabled=false;DestroyImmediate(volume);}volume=null;
            if(curves)
            {
                foreach(var curve in new[]{curves.master,curves.red,curves.green,curves.blue,curves.hueVsHue,curves.hueVsSat,curves.satVsSat,curves.lumVsSat})curve.value.Release();
            }
            if(volumeProfile){foreach(var c in volumeProfile.components)if(c)DestroyImmediate(c);DestroyImmediate(volumeProfile);}volumeProfile=null;bloom=null;curves=null;color=null;tone=null;wellSettings=null;localLightProfile=null;localLightPath=null;bound=false;
            if(wellPipeline)
            {
                if(QualitySettings.renderPipeline==wellPipeline)QualitySettings.renderPipeline=priorQualityPipeline;
                DestroyImmediate(wellPipeline);wellPipeline=null;priorQualityPipeline=null;
            }
        }
        void OnDestroy()=>ResetWorld();
    }
}
