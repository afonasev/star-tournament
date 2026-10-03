using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ApprovedSpatialAudioTests
    {
        const string Key="StarTournament.Audio.EffectsPercent";
        Scene scene;Gamepad[] pads;ProvingGround ground;NativeGameAudio audio;Transform root;
        bool had;int prior;float listener;
        static object Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
        AudioSource[] Voices=>root.GetComponentsInChildren<AudioSource>().Where(s=>s.name.StartsWith("voice-")).ToArray();
        void Clear(){audio.StopEffects();foreach(var s in Voices)s.clip=null;root.Find("damage-bonus-alert").GetComponent<AudioSource>().clip=null;}
        [UnitySetUp] public IEnumerator Setup()
        {
            had=PlayerPrefs.HasKey(Key);prior=PlayerPrefs.GetInt(Key);listener=AudioListener.volume;AudioListener.volume=0;NativeAudioPreferences.SetEffects(80);
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            pads=new[]{InputSystem.AddDevice<Gamepad>()};ground.StartCombatReview(pads,ensureOpponent:true);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ground,true);
            audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ground);
            audio.SetDiagnosticMute(false);root=ground.transform.Find("native-game-audio");
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,2),0);ground.PlaceCombatReviewSeat(1,new Vector3(8,0,2),0);Clear();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pads!=null)foreach(var p in pads)if(p.added)InputSystem.RemoveDevice(p);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            AudioListener.volume=listener;if(had)PlayerPrefs.SetInt(Key,prior);else PlayerPrefs.DeleteKey(Key);PlayerPrefs.Save();
        }
        [UnityTest] public IEnumerator HitsDeathAndPickupUseApprovedBanksAndAudience()
        {
            Invoke(audio,"Damage",new DamageNotice(1,1,0,5,1));
            Assert.That(Voices.Single(s=>s.clip).clip.name,Is.EqualTo("shield-hit"));Clear();
            Invoke(audio,"Damage",new DamageNotice(1,1,5,0,2));
            Assert.That(Voices.Single(s=>s.clip).clip.name,Does.StartWith("body-hit-"));Clear();
            var random=JsonUtility.ToJson(UnityEngine.Random.state);var seen=new System.Collections.Generic.HashSet<string>();
            for(int i=0;i<60;i++){Invoke(audio,"Death",new DeathNotice(1,ground.Session.Life(1),ground.Session.Pose(1)));seen.Add(Voices.Single(s=>s.clip).clip.name);Clear();}
            Assert.That(seen,Is.EquivalentTo(new[]{"death-0","death-1","death-2"}));Assert.That(JsonUtility.ToJson(UnityEngine.Random.state),Is.EqualTo(random));
            foreach(var pair in new[]{(NativeBotPickupKind.Weapon,"weapon-pickup"),(NativeBotPickupKind.Armor,"shield-pickup"),(NativeBotPickupKind.Speed,"speed-pickup"),(NativeBotPickupKind.Heal,"heal-pickup")})
            {Invoke(audio,"Pickup",0,"fixture",pair.Item1);Assert.That(Voices.Single(s=>s.clip).clip.name,Is.EqualTo(pair.Item2));Clear();}
            Invoke(audio,"Pickup",0,"fixture",NativeBotPickupKind.Damage);
            Assert.That(Voices.Single(s=>s.clip).clip.name,Is.EqualTo("damage-pickup"));Assert.That(root.Find("damage-bonus-alert").GetComponent<AudioSource>().clip,Is.Null);Clear();
            Invoke(audio,"Pickup",1,"fixture",NativeBotPickupKind.Damage);
            Assert.That(root.Find("damage-bonus-alert").GetComponent<AudioSource>().clip.name,Is.EqualTo("damage-bonus-pickup"));Assert.That(Voices.Any(s=>s.clip),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator MovingRocketLoopUsesPositionAndStopsOnPauseRestoreMute()
        {
            var snap=ground.Session.Capture();snap.ShotSequence=1;
            snap.Rockets=new[]{new RocketState{Id=1,Owner=1,OwnerLife=snap.Lives[1].Life,Position=new Vector3(-6,1,2),Direction=Vector3.forward,DamageMultiplier=1}};
            ground.Session.Restore(snap);audio.Tick(true);
            var rocket=root.GetComponentsInChildren<AudioSource>().Single(s=>s.name.StartsWith("rocket-flight-")&&s.isPlaying);
            Assert.That(rocket.loop,Is.True);Assert.That(rocket.clip.name,Is.EqualTo("rocket-flight"));Assert.That(rocket.panStereo,Is.GreaterThan(0));float near=rocket.volume;
            // Restoring stops old loops; the live restored projectile starts at its actual position on the next tick.
            snap.Rockets[0].Position=new Vector3(-30,1,2);ground.Session.Restore(snap);Assert.That(rocket.isPlaying,Is.False);audio.Tick(true);
            Assert.That(rocket.panStereo,Is.LessThan(0));Assert.That(rocket.volume,Is.LessThan(near));
            audio.Tick(false);Assert.That(rocket.isPlaying,Is.False);audio.Tick(true);Assert.That(rocket.isPlaying,Is.True);
            audio.SetDiagnosticMute(true);Assert.That(rocket.isPlaying,Is.False);audio.SetDiagnosticMute(false);audio.Tick(true);Assert.That(rocket.isPlaying,Is.True);
            snap.Rockets=Array.Empty<RocketState>();ground.Session.Restore(snap);audio.Tick(true);Assert.That(rocket.isPlaying,Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator CutterAttackSustainAndReleaseFollowLiveBeamWithoutRestart()
        {
            var snap=ground.Session.Capture();snap.Beams[1].Active=true;snap.Beams[1].EmissionSeconds=1;
            snap.Beams[1].Origin=new Vector3(10,0,2);snap.Beams[1].Endpoint=new Vector3(-8,0,2);
            ground.Session.Restore(snap);audio.Tick(true);
            var beam=root.Find("cutter-1").GetComponent<AudioSource>();Assert.That(beam.isPlaying,Is.True);Assert.That(beam.loop,Is.True);Assert.That(beam.clip.name,Is.EqualTo("cutter-loop"));
            float attack=beam.volume;audio.Tick(true);audio.Tick(true);Assert.That(beam.volume,Is.GreaterThan(attack));
            yield return new WaitForSeconds(.08f);int cursor=beam.timeSamples;audio.Tick(true);Assert.That(beam.timeSamples,Is.GreaterThanOrEqualTo(cursor),"Active beam must not restart the loop every tick");
            // Stop the authoritative beam without restoring: release keeps the last spatial position.
            var beams=(CutterBeam[])typeof(NativeCombatSession).GetField("beams",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(ground.Session);
            beams[1].Stop();float held=beam.volume;audio.Tick(true);Assert.That(beam.isPlaying,Is.True);Assert.That(beam.volume,Is.LessThan(held));Assert.That(beam.volume,Is.GreaterThan(0));
            for(int i=0;i<8;i++)audio.Tick(true);Assert.That(beam.isPlaying,Is.False);
        }
        [UnityTest] public IEnumerator SharedOutputDoesNotMultiplyShotsAndBeamUsesEndpoint()
        {
            var one=(AudioSpatialMix)Invoke(audio,"Spatial",ground.Session.Pose(0).Position,1,null);
            foreach(var p in pads)InputSystem.RemoveDevice(p);pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();ground.StartCombatReview(pads);
            for(int i=0;i<4;i++)ground.PlaceCombatReviewSeat(i,new Vector3(-8,0,2),0);
            var four=(AudioSpatialMix)Invoke(audio,"Spatial",ground.Session.Pose(0).Position,7,null);Assert.That(four.Gain,Is.EqualTo(one.Gain).Within(.0001));Clear();
            Invoke(audio,"Shot",new ShotNotice(1,1,1,0,ground.Session.Pose(1).Position,Array.Empty<PelletNotice>(),WeaponId.Shotgun));
            Assert.That(Voices.Count(s=>s.clip),Is.EqualTo(1));Assert.That(Voices.Single(s=>s.clip).clip.name,Does.StartWith("shotgun-"));
            var nearBeam=(AudioSpatialMix)Invoke(audio,"Spatial",new Vector3(10,0,2),7,(Vector3?)new Vector3(-8,0,2));
            var weapon=(AudioSpatialMix)Invoke(audio,"Spatial",new Vector3(10,0,2),7,null);Assert.That(nearBeam.Gain,Is.GreaterThan(weapon.Gain*2));
            yield return null;
        }
    }
}
