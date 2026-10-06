using System;
using System.Collections;
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
        [UnityTearDown]public IEnumerator Cleanup(){presentation?.Dispose();if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
