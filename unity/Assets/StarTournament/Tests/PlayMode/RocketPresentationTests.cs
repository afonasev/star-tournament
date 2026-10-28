using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class RocketPresentationTests
    {
        Scene scene;GameObject owner;NativeCombatSession session;RocketPresentation presentation;ProvingProfile profile;ProvingArena arena;CharacterMotor[] motors;
        [UnitySetUp]public IEnumerator Setup()
        {
            scene=SceneManager.CreateScene("pulse-vfx-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            owner=new GameObject("Pulse presentation fixture");SceneManager.MoveGameObjectToScene(owner,scene);
            profile=ProvingProfile.CreateDefault();arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),profile);
            motors=new CharacterMotor[2];
            for(int i=0;i<2;i++){var obj=new GameObject("p"+i);obj.transform.SetParent(owner.transform);obj.layer=ProvingArena.ParticipantLayer;motors[i]=obj.AddComponent<CharacterMotor>();motors[i].Initialize(profile,new Vector3(-8+i*16,0,0));}
            session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),profile,ProvingProfile.CreateCombatDefault(),ProvingProfile.CreateNativeCombatDefault());
            EquippedCombatFixture.Equip(session);
            var effects=ProvingProfile.CreateRocketEffectsDefault();effects.Set("pulseFx.maxBursts",1);
            presentation=new RocketPresentation(session,profile,owner.transform,effects:effects);
            var actions=new LocalAction[2];actions[0].SelectWeapon=WeaponSelection.RocketLauncher;session.Tick(actions,.02f);Advance(1.3f);
            yield return null;
        }
        void Advance(float seconds){for(int i=0;i<Mathf.RoundToInt(seconds/.02f);i++)session.Tick(new LocalAction[2],.02f);}
        NativeCombatSessionSnapshot Flight()
        {
            var actions=new LocalAction[2];actions[0].Fire=true;session.Tick(actions,.02f);session.ClearInput();
            var state=session.Capture();Assert.That(state.Rockets.Length,Is.EqualTo(1));return state;
        }
        void Contact(NativeCombatSessionSnapshot state)
        {
            // Actual authoritative floor sweep, safely away from participants.
            state.Rockets[0].Position=new Vector3(0,.1f,0);state.Rockets[0].Direction=Vector3.down;session.Restore(state);
            session.Tick(new LocalAction[2],.02f);session.Tick(new LocalAction[2],.08f);presentation.Render();
        }
        [UnityTest,Explicit("P5 actual Pulse VFX inventory and matched Editor render capture; diagnostic, not Player FPS or acceptance")]
        public IEnumerator P5BaselineCapturesPulseEffectsCostAndFrame()
        {
            var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RENDER_COST_OUTPUT");Assert.That(directory,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(directory);
            var flight=Flight();presentation.Render();
            // Preserve construction-owned flight materials in the inventory after contact removes the rocket renderers.
            var flightMaterials=owner.GetComponentsInChildren<Renderer>(true).Where(r=>r is LineRenderer||r is ParticleSystemRenderer).SelectMany(r=>r.sharedMaterials).ToArray();
            Contact(flight);presentation.Render();
            var systems=owner.GetComponentsInChildren<ParticleSystem>(true);var particleRenderers=systems.Select(s=>s.GetComponent<ParticleSystemRenderer>()).Where(r=>r).Cast<Renderer>().Concat(owner.GetComponentsInChildren<LineRenderer>(true)).ToArray();
            SecondaryRenderingCostDiagnostics.Write(directory,"pulse-vfx",particleRenderers,flightMaterials);SecondaryRenderingCostDiagnostics.WriteParticles(directory,"pulse-vfx",systems);
            File.WriteAllLines(Path.Combine(directory,"pulse-vfx-settings.txt"),new[]{"profile="+effectsIdentity(),"active_bursts="+presentation.ActiveBursts,"pooled_bursts="+presentation.PooledBursts,"active_lights="+presentation.ActiveLights,"live_particles="+presentation.LiveParticles,"editor="+Application.unityVersion,"gpu="+SystemInfo.graphicsDeviceName,"graphics="+SystemInfo.graphicsDeviceType,"quality="+QualitySettings.names[QualitySettings.GetQualityLevel()],"resolution=1280x720 DefaultHDR","timing=NotRun; this test inventories native objects and renders one frame only"});
            var cameraGo=new GameObject("P5 pulse VFX baseline camera");cameraGo.transform.position=new Vector3(0,2.3f,-7);cameraGo.transform.LookAt(new Vector3(0,.8f,0));var camera=cameraGo.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=55;camera.nearClipPlane=.05f;camera.farClipPlane=100;
            SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"pulse-vfx.png"));UnityEngine.Object.Destroy(cameraGo);yield return null;
            string effectsIdentity()=>ProvingProfile.CreateRocketEffectsDefault().Id+"@"+ProvingProfile.CreateRocketEffectsDefault().Version;
        }
        [UnityTest]public IEnumerator PauseFreezesParticlesRestoreClearsBurstAndRecreatesFlightWithoutChangingState()
        {
            var flight=Flight();presentation.Render();Assert.That(presentation.ActiveFlights,Is.EqualTo(1));
            var exhaust=owner.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Hot exhaust");Assert.That(exhaust.widthMultiplier,Is.EqualTo(ProvingProfile.CreateRocketEffectsDefault().Get("pulseFx.exhaustWidth")).Within(.0001f));
            string before=JsonUtility.ToJson(session.Capture());presentation.Render();Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(before));
            Contact(flight);Assert.That(presentation.ActiveBursts,Is.EqualTo(1));Assert.That(presentation.LiveParticles,Is.GreaterThan(0));
            var ps=owner.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Fire lobes");
            var first=new ParticleSystem.Particle[32];int count=ps.GetParticles(first);
            yield return new WaitForSecondsRealtime(.05f);presentation.Render();
            var frozen=new ParticleSystem.Particle[32];Assert.That(ps.GetParticles(frozen),Is.EqualTo(count));
            for(int i=0;i<count;i++){Assert.That(frozen[i].position,Is.EqualTo(first[i].position));Assert.That(frozen[i].startColor,Is.EqualTo(first[i].startColor));Assert.That(frozen[i].startSize,Is.EqualTo(first[i].startSize));}
            session.Restore(flight);presentation.Render();Assert.That(presentation.ActiveBursts,Is.Zero);Assert.That(presentation.ActiveFlights,Is.EqualTo(1));
            yield return null;Assert.That(owner.GetComponentsInChildren<ParticleSystem>().Count(p=>p.name=="Light flight smoke"),Is.EqualTo(1));
        }
        [UnityTest]public IEnumerator BurstExpiresBySessionClockAndReusesItsBoundedObjects()
        {
            var flight=Flight();Contact(flight);Assert.That(presentation.PooledBursts,Is.EqualTo(1));
            var root=owner.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Pulse hot impulse").gameObject;
            Advance(1.2f);presentation.Render();Assert.That(presentation.ActiveBursts,Is.Zero);Assert.That(root.activeSelf,Is.False);
            // A restored confirmed flight may legitimately create a new event; retain the reusable slot.
            Contact(flight);Assert.That(presentation.PooledBursts,Is.EqualTo(1));Assert.That(presentation.ActiveBursts,Is.EqualTo(1));Assert.That(root.activeSelf,Is.True);
            Assert.That(owner.GetComponentsInChildren<LineRenderer>(true).Any(r=>r.name.Contains("Impulse plane")),Is.False);
            presentation.Dispose();presentation=null;yield return null;Assert.That(owner.GetComponentsInChildren<ParticleSystem>(true),Is.Empty);
        }
        [UnityTest]public IEnumerator CoreLifetimeOutlivesOtherLayersWhenProfileRequestsIt()
        {
            presentation.Dispose();yield return null;
            profile.Set("presentation.rocketExplosionSeconds",.05f);
            var effects=ProvingProfile.CreateRocketEffectsDefault();effects.Set("pulseFx.coreSeconds",.5f);effects.Set("pulseFx.sparkSeconds",.05f);effects.Set("pulseFx.smokeDelay",0);effects.Set("pulseFx.smokeSeconds",.1f);
            presentation=new RocketPresentation(session,profile,owner.transform,effects:effects);
            Contact(Flight());Advance(.16f);presentation.Render();
            Assert.That(presentation.ActiveBursts,Is.EqualTo(1));
            Assert.That(owner.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Hot core").particleCount,Is.EqualTo(1));
            Advance(.4f);presentation.Render();Assert.That(presentation.ActiveBursts,Is.Zero);
        }
        [UnityTest]public IEnumerator FlameEnvelopeFollowsFrozenRadiusAndIgnoresLegacySizeAtExtremeShapeSettings()
        {
            foreach(float radius in new[]{2f,8f})foreach(float legacy in new[]{.1f,8f})foreach(float variation in new[]{0f,.8f})foreach(float visualScale in new[]{.1f,.8f,1f})
            {
                presentation.Dispose();yield return null;
                profile.Set("presentation.rocketExplosionSize",legacy);
                var combat=ProvingProfile.CreateNativeCombatDefault();combat.Set("rocket.radius",radius);
                session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),profile,ProvingProfile.CreateCombatDefault(),combat);EquippedCombatFixture.Equip(session);
                var a=new LocalAction[2];a[0].SelectWeapon=WeaponSelection.RocketLauncher;session.Tick(a,.02f);Advance(1.3f);
                var fx=ProvingProfile.CreateRocketEffectsDefault();fx.Set("pulseFx.blastRadiusScale",visualScale);fx.Set("pulseFx.fireSizeVariation",variation);fx.Set("pulseFx.fireSpreadScale",variation==0?.1f:1.5f);fx.Set("pulseFx.fireLobeScale",variation==0?2:.1f);
                presentation=new RocketPresentation(session,profile,owner.transform,effects:fx);Contact(Flight());
                Assert.That(presentation.FlameEnvelope,Is.LessThanOrEqualTo(radius*visualScale+.001f));
                if(variation==0)Assert.That(presentation.FlameEnvelope,Is.EqualTo(radius*visualScale).Within(.001f));
                var fire=owner.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Fire lobes");var particles=new ParticleSystem.Particle[24];int count=fire.GetParticles(particles);
                Assert.That(particles.Take(count).All(p=>p.startSize>0&&!float.IsNaN(p.startSize)),Is.True);
            }
        }
        [UnityTest]public IEnumerator LightAndAtlasFreezeWithSessionTimeAndRestoreDisablesThePooledLight()
        {
            var flight=Flight();Contact(flight);var light=owner.GetComponentsInChildren<Light>().Single();
            Assert.That(presentation.ActiveLights,Is.EqualTo(1));Assert.That(light.shadows,Is.EqualTo(LightShadows.None));
            Assert.That(light.range,Is.EqualTo(session.RocketBlastRadius*1.25f));float intensity=light.intensity;
            var fire=owner.GetComponentsInChildren<ParticleSystem>().Single(p=>p.name=="Fire lobes");float frame=fire.textureSheetAnimation.frameOverTime.constant;
            var cameraRoot=new GameObject("Atlas UV camera");cameraRoot.transform.SetParent(owner.transform);var camera=cameraRoot.AddComponent<Camera>();camera.enabled=false;
            var mesh=new Mesh();var renderer=fire.GetComponent<ParticleSystemRenderer>();renderer.BakeMesh(mesh,camera,false);var uv=mesh.uv;Assert.That(uv,Is.Not.Empty);
            yield return new WaitForSecondsRealtime(.1f);presentation.Render();Assert.That(light.intensity,Is.EqualTo(intensity));Assert.That(fire.textureSheetAnimation.frameOverTime.constant,Is.EqualTo(frame));renderer.BakeMesh(mesh,camera,false);Assert.That(mesh.uv,Is.EqualTo(uv));
            Advance(.04f);presentation.Render();Assert.That(light.intensity,Is.LessThan(intensity));Assert.That(fire.textureSheetAnimation.frameOverTime.constant,Is.Not.EqualTo(frame));renderer.BakeMesh(mesh,camera,false);Assert.That(mesh.uv,Is.Not.EqualTo(uv));UnityEngine.Object.Destroy(mesh);
            session.Restore(flight);presentation.Render();Assert.That(presentation.ActiveLights,Is.Zero);Assert.That(light.enabled,Is.False);
            Contact(flight);Assert.That(owner.GetComponentsInChildren<Light>(true).Length,Is.EqualTo(1));
            presentation.Dispose();presentation=null;yield return null;Assert.That(owner.GetComponentsInChildren<Light>(true),Is.Empty);
        }
        [UnityTest]public IEnumerator ConcurrentBurstsRespectAnIndependentLightBudget()
        {
            presentation.Dispose();yield return null;
            var fx=ProvingProfile.CreateRocketEffectsDefault();fx.Set("pulseFx.maxBursts",4);fx.Set("pulseFx.maxLights",1);
            presentation=new RocketPresentation(session,profile,owner.transform,effects:fx);
            var actions=new LocalAction[2];actions[1].SelectWeapon=WeaponSelection.RocketLauncher;session.Tick(actions,.02f);Advance(1.3f);
            actions=new LocalAction[2];actions[0].Fire=actions[1].Fire=true;session.Tick(actions,.02f);var state=session.Capture();Assert.That(state.Rockets.Length,Is.EqualTo(2));
            for(int i=0;i<2;i++){state.Rockets[i].Position=new Vector3(i,.1f,0);state.Rockets[i].Direction=Vector3.down;}session.Restore(state);Advance(.1f);presentation.Render();
            Assert.That(presentation.ActiveBursts,Is.EqualTo(2));Assert.That(presentation.ActiveLights,Is.EqualTo(1));
            Advance(.3f);presentation.Render();Assert.That(presentation.ActiveLights,Is.Zero);
        }
        [UnityTest,Explicit("Editor art review with STAR_TOURNAMENT_PULSE_EVIDENCE; capture diagnostic, not gameplay or device acceptance")]public IEnumerator Mk2ActualEffectsReview()
        {
            string directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_PULSE_EVIDENCE");
            Assert.That(directory,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(directory);
            presentation.Dispose();
            #if UNITY_EDITOR
            var prefab=UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Art/pulse-projectile.glb");
            #else
            GameObject prefab=null;
            #endif
            Assert.That(prefab,Is.Not.Null);
            // Isolate art from arena occlusion; physics/contact fixture remains intact.
            foreach(var renderer in arena.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Pulse review backdrop";floor.transform.SetParent(owner.transform);floor.transform.position=new Vector3(0,-.02f,0);floor.transform.localScale=Vector3.one*3;
            floor.GetComponent<Collider>().enabled=false;UnityEngine.Object.Destroy(floor.GetComponent<Collider>());
            var backdrop=new Material(Shader.Find("Universal Render Pipeline/Lit"));backdrop.SetColor("_BaseColor",new Color(.09f,.095f,.10f));backdrop.SetFloat("_Smoothness",.1f);floor.GetComponent<Renderer>().sharedMaterial=backdrop;
            presentation=new RocketPresentation(session,profile,owner.transform,prefab);
            var go=new GameObject("Pulse Mk II review camera");go.transform.SetParent(owner.transform);var camera=go.AddComponent<Camera>();camera.enabled=false;
            camera.transform.position=new Vector3(1,2,-4);camera.transform.LookAt(new Vector3(0,.9f,0));camera.fieldOfView=50;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.025f,.03f,.04f);
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.intensity=3;light.range=12;
            var flight=Flight();flight.Rockets[0].Position=new Vector3(0,1,0);flight.Rockets[0].Direction=Vector3.right;flight.Rockets[0].Distance=1;session.Restore(flight);presentation.Render();yield return null;
            SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"fx-01-flight.png"));
            var widePosition=camera.transform.position;var wideRotation=camera.transform.rotation;
            camera.transform.position=new Vector3(.1f,1.14f,-.85f);camera.transform.LookAt(new Vector3(-.08f,1,0));
            SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"fx-01-projectile-closeup.png"));
            camera.transform.SetPositionAndRotation(widePosition,wideRotation);
            presentation.Launch(flight.Rockets[0].Id,Vector3.zero,new Vector3(-.6f,1,0),Vector3.right);presentation.Render();
            SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"fx-02-launch.png"));
            camera.transform.position=new Vector3(1,3,-9);camera.transform.LookAt(new Vector3(0,.4f,0));
            Contact(flight);yield return null;SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"fx-03-impact-early.png"));
            Advance(.12f);presentation.Render();SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"fx-04-impact-fire.png"));
            floor.GetComponent<Renderer>().enabled=false;
            SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"fx-04-fire-without-floor.png"));
            floor.GetComponent<Renderer>().enabled=true;
            Advance(.38f);presentation.Render();SecondaryRenderingCostDiagnostics.Capture(camera,Path.Combine(directory,"fx-05-impact-smoke.png"));
            UnityEngine.Object.Destroy(go);UnityEngine.Object.Destroy(backdrop);yield return null;
        }
        [UnityTest]public IEnumerator Mk2IgnitionFollowsSuppliedDirectionFreezesAndClearsOnRestore()
        {
            var flight=Flight();var point=new Vector3(2,3,4);var direction=Vector3.left;
            string before=JsonUtility.ToJson(session.Capture());
            presentation.Launch(flight.Rockets[0].Id,Vector3.zero,point,direction);presentation.Render();
            Assert.That(presentation.ActiveIgnitions,Is.EqualTo(1));
            var flame=owner.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Muzzle ignition");
            Assert.That(flame.GetPosition(0),Is.EqualTo(point));
            Assert.That(Vector3.Dot((flame.GetPosition(2)-point).normalized,direction),Is.GreaterThan(.999f));
            var end=flame.GetPosition(2);float width=flame.widthMultiplier;
            yield return new WaitForSecondsRealtime(.05f);presentation.Render();
            Assert.That(flame.GetPosition(2),Is.EqualTo(end));Assert.That(flame.widthMultiplier,Is.EqualTo(width));
            Assert.That(JsonUtility.ToJson(session.Capture()),Is.EqualTo(before));
            Advance(.12f);presentation.Render();Assert.That(presentation.ActiveIgnitions,Is.Zero);
            presentation.Launch(flight.Rockets[0].Id,Vector3.zero,point,direction);session.Restore(flight);presentation.Render();
            Assert.That(presentation.ActiveIgnitions,Is.Zero);
        }
        [UnityTest]public IEnumerator Mk2SoftContactRequestsOnlyOwnedCameraDepthAndRestoresOptions()
        {
            var go=new GameObject("Pulse depth camera");go.transform.SetParent(owner.transform);go.AddComponent<Camera>();
            var data=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.requiresDepthOption=UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings;
            var foreign=new GameObject("Unrelated depth camera");foreign.transform.SetParent(owner.transform.parent);
            foreign.AddComponent<Camera>();var other=foreign.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();other.requiresDepthOption=UnityEngine.Rendering.Universal.CameraOverrideOption.Off;
            try
            {
                var flight=Flight();presentation.Render();Assert.That(data.requiresDepthOption,Is.EqualTo(UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings));
                Contact(flight);Assert.That(data.requiresDepthTexture,Is.True);Assert.That(other.requiresDepthOption,Is.EqualTo(UnityEngine.Rendering.Universal.CameraOverrideOption.Off));
                Advance(3);presentation.Render();Assert.That(data.requiresDepthOption,Is.EqualTo(UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings));
                Contact(flight);Assert.That(data.requiresDepthTexture,Is.True);
                session.Restore(flight);presentation.Render();Assert.That(data.requiresDepthOption,Is.EqualTo(UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings));
                Contact(flight);presentation.Dispose();presentation=null;Assert.That(data.requiresDepthOption,Is.EqualTo(UnityEngine.Rendering.Universal.CameraOverrideOption.UsePipelineSettings));
            }
            finally{UnityEngine.Object.Destroy(foreign);}
            yield return null;
        }
        [UnityTearDown]public IEnumerator Cleanup(){presentation?.Dispose();if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
