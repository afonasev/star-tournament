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
        Scene scene;GameObject owner;NativeCombatSession session;RocketPresentation presentation;ProvingProfile profile;
        [UnitySetUp]public IEnumerator Setup()
        {
            scene=SceneManager.CreateScene("pulse-vfx-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            owner=new GameObject("Pulse presentation fixture");SceneManager.MoveGameObjectToScene(owner,scene);
            profile=ProvingProfile.CreateDefault();var arena=owner.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),profile);
            var motors=new CharacterMotor[2];
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
        [UnityTearDown]public IEnumerator Cleanup(){presentation?.Dispose();if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
