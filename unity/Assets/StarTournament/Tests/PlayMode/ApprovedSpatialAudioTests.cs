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
        const string Key="StarTournament.Audio.EffectsPercent",MusicKey="StarTournament.Audio.MusicPercent";
        Scene scene;Gamepad[] pads;ProvingGround ground;NativeGameAudio audio;Transform root;
        bool had,hadMusic;int prior,priorMusic;float listener;
        static object Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,args);
        AudioSource[] Voices=>root.GetComponentsInChildren<AudioSource>().Where(s=>s.name.StartsWith("voice-")).ToArray();
        AudioSource[] MusicSources=>root.GetComponentsInChildren<AudioSource>().Where(s=>s.name.Contains("-music-")).ToArray();
        ProvingProfile EffectsProfile=>(ProvingProfile)typeof(NativeGameAudio).GetField("profile",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio);
        ProvingProfile MusicProfile=>(ProvingProfile)typeof(NativeGameAudio).GetField("musicProfile",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio);
        void Clear(){audio.StopEffects();foreach(var s in Voices)s.clip=null;root.Find("damage-bonus-alert").GetComponent<AudioSource>().clip=null;}
        [UnitySetUp] public IEnumerator Setup()
        {
            hadMusic=PlayerPrefs.HasKey(MusicKey);priorMusic=PlayerPrefs.GetInt(MusicKey);NativeAudioPreferences.SetMusic(100);
            had=PlayerPrefs.HasKey(Key);prior=PlayerPrefs.GetInt(Key);listener=AudioListener.volume;AudioListener.volume=0;NativeAudioPreferences.SetEffects(80);
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
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
            if(hadMusic)PlayerPrefs.SetInt(MusicKey,priorMusic);else PlayerPrefs.DeleteKey(MusicKey);
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
        [UnityTest] public IEnumerator DeathGainBoostsOwnAndNearbyDeathsButKeepsSpatialRangeAndMute()
        {
            var profile=(ProvingProfile)typeof(NativeGameAudio).GetField("profile",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio);
            foreach(int seat in new[]{0,1})
            {
                profile.Set("audio.deathGain",1);
                Invoke(audio,"Death",new DeathNotice(seat,ground.Session.Life(seat),ground.Session.Pose(seat)));
                float previous=Voices.Single(s=>s.clip).volume;Clear();
                profile.Set("audio.deathGain",2);
                Invoke(audio,"Death",new DeathNotice(seat,ground.Session.Life(seat),ground.Session.Pose(seat)));
                var cue=Voices.Single(s=>s.clip);
                Assert.That(cue.volume,Is.GreaterThan(previous*1.4f));
                if(seat==0)Assert.That(cue.panStereo,Is.EqualTo(0));else Assert.That(cue.panStereo,Is.GreaterThan(0));Clear();
            }
            ground.PlaceCombatReviewSeat(1,new Vector3(-8+profile.Get("audio.maxDistanceMeters")+1,0,2),0);
            Invoke(audio,"Death",new DeathNotice(1,ground.Session.Life(1),ground.Session.Pose(1)));
            Assert.That(Voices.Any(s=>s.clip),Is.False,"Death stays within the existing hearing range");
            ground.PlaceCombatReviewSeat(1,new Vector3(8,0,2),0);
            profile.Set("audio.deathGain",0);
            Invoke(audio,"Death",new DeathNotice(1,ground.Session.Life(1),ground.Session.Pose(1)));
            Assert.That(Voices.Any(s=>s.clip),Is.False);
            profile.Set("audio.deathGain",2);audio.SetDiagnosticMute(true);
            Invoke(audio,"Death",new DeathNotice(0,ground.Session.Life(0),ground.Session.Pose(0)));
            Assert.That(Voices.Any(s=>s.clip),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator DeathSurvivesSaturatedShotPoolAndMixRemainsBounded()
        {
            for(int i=0;i<Voices.Length;i++)Invoke(audio,"Play","rifle",1f,0);
            Assert.That(Voices.All(s=>s.isPlaying),Is.True);
            Invoke(audio,"Death",new DeathNotice(1,ground.Session.Life(1),ground.Session.Pose(1)));
            var cue=Voices.Single(s=>s.clip.name.StartsWith("death-"));
            for(int i=0;i<Voices.Length*2;i++)Invoke(audio,"Play","shotgun",1f,0);
            Assert.That(cue.isPlaying,Is.True);Assert.That(cue.clip.name,Does.StartWith("death-"));
            Assert.That(Voices.Sum(s=>s.volume),Is.LessThanOrEqualTo(1.0001f));
            audio.StopEffects();Assert.That(cue.isPlaying,Is.False);
            yield return null;
        }
        void RequestWeapon(int participant,WeaponSelection selection)
        {
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[participant].SelectWeapon=selection;
            ground.Session.Tick(actions,.02f);
        }
        void CompleteWeaponSwitch()
        {
            var actions=new LocalAction[ground.Session.ParticipantCount];
            for(int i=0;i<200&&Enumerable.Range(0,ground.Session.ParticipantCount).Any(p=>ground.Session.Life(p).SwitchRemaining>0||ground.Session.Life(p).CooldownRemaining>0);i++)
                ground.Session.Tick(actions,.02f);
            Assert.That(Enumerable.Range(0,ground.Session.ParticipantCount).All(p=>ground.Session.Life(p).SwitchRemaining==0&&ground.Session.Life(p).CooldownRemaining==0),Is.True);
        }
        [UnityTest] public IEnumerator EveryWeaponGetsOneLocalReadyCueOnlyAtTheEndOfItsSwitch()
        {
            EquippedCombatFixture.Equip(ground.Session);Clear();
            var clip=Resources.Load<AudioClip>("Audio/weapon-ready");Assert.That(clip,Is.Not.Null);
            Assert.That(clip.length,Is.EqualTo(.16f).Within(.001f));Assert.That(clip.preloadAudioData,Is.True);
            foreach(var pair in new[]{(WeaponSelection.Shotgun,WeaponId.Shotgun),(WeaponSelection.RocketLauncher,WeaponId.RocketLauncher),(WeaponSelection.Cutter,WeaponId.Cutter),(WeaponSelection.Rifle,WeaponId.Rifle)})
            {
                RequestWeapon(0,pair.Item1);Assert.That(Voices.Any(v=>v.clip),Is.False,"Request is not readiness");
                while(ground.Session.Life(0).SwitchRemaining>0)
                {
                    ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
                    if(ground.Session.Life(0).SwitchRemaining>0)Assert.That(Voices.Any(v=>v.clip),Is.False);
                }
                Assert.That(ground.Session.Life(0).SelectedWeapon,Is.EqualTo(pair.Item2));
                var ready=Voices.Single(v=>v.clip);Assert.That(ready.clip.name,Is.EqualTo("weapon-ready"));
                Assert.That(ready.volume,Is.GreaterThan(.5f));Assert.That(ready.panStereo,Is.EqualTo(0));Assert.That(ready.pitch,Is.EqualTo(1));Clear();
                for(int t=0;t<5;t++)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
                RequestWeapon(0,pair.Item1);Assert.That(Voices.Any(v=>v.clip),Is.False,"Idle/same-slot input must not repeat readiness");
            }
            RequestWeapon(1,WeaponSelection.Shotgun);CompleteWeaponSwitch();
            Assert.That(Voices.Any(v=>v.clip),Is.False,"Opponent/bot readiness is private");
            yield return null;
        }
        [UnityTest] public IEnumerator WeaponReadyWaitsForExistingTargetCooldownAndPrecedesFirstHeldRifleShot()
        {
            EquippedCombatFixture.Equip(ground.Session);var snap=ground.Session.Capture();
            snap.Lives[0].ShotgunCooldownRemaining=ground.Session.WeaponSwitchSeconds+.2;
            ground.Session.Restore(snap);Clear();RequestWeapon(0,WeaponSelection.Shotgun);
            while(ground.Session.Life(0).SwitchRemaining>0)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
            Assert.That(ground.Session.Life(0).CooldownRemaining,Is.GreaterThan(0));Assert.That(Voices.Any(v=>v.clip),Is.False,"Animation completion is not readiness while cooldown remains");
            CompleteWeaponSwitch();Assert.That(Voices.Single(v=>v.clip).clip.name,Is.EqualTo("weapon-ready"));Clear();
            for(int i=0;i<Voices.Length;i++)Invoke(audio,"Play","rifle",1f,0);
            bool observed=false;ground.Session.ShotResolved+=notice=>
            {
                if(notice.Shooter!=0||notice.Weapon!=WeaponId.Rifle)return;
                observed=true;Assert.That(Voices.Count(v=>v.clip&&v.clip.name=="weapon-ready"),Is.EqualTo(1),"Ready feedback must precede cooldown/ammo consumption on the first eligible tick");
            };
            RequestWeapon(0,WeaponSelection.Rifle);var held=new LocalAction[ground.Session.ParticipantCount];held[0].FireHeld=true;
            while(ground.Session.Life(0).SwitchRemaining>0)ground.Session.Tick(held,.02f);
            Assert.That(observed,Is.True);var cue=Voices.Single(v=>v.clip&&v.clip.name=="weapon-ready");
            for(int i=0;i<Voices.Length*2;i++)Invoke(audio,"Play","shotgun",1f,0);
            Assert.That(cue.isPlaying,Is.True);Assert.That(cue.clip.name,Is.EqualTo("weapon-ready"));
            Assert.That(Voices.Sum(v=>v.volume),Is.LessThanOrEqualTo(1.0001f));
            yield return null;
        }
        [UnityTest] public IEnumerator WeaponReadyHasNoGhostsFromRestoreDeathEmptyAmmoOrMute()
        {
            EquippedCombatFixture.Equip(ground.Session);var original=ground.Session.Capture();Clear();
            var changed=ground.Session.Capture();changed.Lives[0].SelectedWeapon=WeaponId.Shotgun;changed.Lives[0].Ammo=changed.Lives[0].ShotgunAmmo;
            ground.Session.Restore(changed);ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
            Assert.That(Voices.Any(v=>v.clip),Is.False,"Restored selected weapon is not a newly completed switch");
            ground.Session.Restore(original);RequestWeapon(0,WeaponSelection.Shotgun);
            var dead=ground.Session.Capture();dead.Lives[0].Dead=true;dead.Lives[0].Health=0;dead.Lives[0].RespawnRemaining=2;
            ground.Session.Restore(dead);ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
            Assert.That(Voices.Any(v=>v.clip),Is.False,"Death cancels readiness");
            var empty=original;empty.Lives[0].ShotgunAmmo=0;ground.Session.Restore(empty);RequestWeapon(0,WeaponSelection.Shotgun);CompleteWeaponSwitch();
            Assert.That(Voices.Any(v=>v.clip),Is.False,"Empty weapon cannot promise a shot");
            var restocked=ground.Session.Capture();restocked.Lives[0].ShotgunAmmo=20;restocked.Lives[0].Ammo=20;
            ground.Session.Restore(restocked);ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
            Assert.That(Voices.Any(v=>v.clip),Is.False,"Restock must not replay a suppressed empty-weapon cue");
            RequestWeapon(0,WeaponSelection.Rifle);CompleteWeaponSwitch();Clear();
            var equipped=ground.Session.Capture();audio.SetDiagnosticMute(true);RequestWeapon(0,WeaponSelection.Shotgun);CompleteWeaponSwitch();
            Assert.That(Voices.Any(v=>v.clip),Is.False);audio.SetDiagnosticMute(false);
            ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);Assert.That(Voices.Any(v=>v.clip),Is.False,"Unmute must not replay readiness");
            ground.Session.Restore(equipped);var profile=(ProvingProfile)typeof(NativeGameAudio).GetField("profile",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(audio);
            profile.Set("audio.weaponReadyGain",0);RequestWeapon(0,WeaponSelection.Shotgun);CompleteWeaponSwitch();Assert.That(Voices.Any(v=>v.clip),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator SplitScreenWeaponReadyIsEmittedOnceForTheSwitchingSeat()
        {
            foreach(var p in pads)InputSystem.RemoveDevice(p);pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();ground.StartCombatReview(pads);
            EquippedCombatFixture.Equip(ground.Session);Clear();RequestWeapon(2,WeaponSelection.Cutter);CompleteWeaponSwitch();
            Assert.That(Voices.Count(v=>v.clip&&v.clip.name=="weapon-ready"),Is.EqualTo(1));
            yield return null;
        }
        [UnityTest] public IEnumerator HistoricalCombatMixDefaultsWorkWithoutRewritingTheSavedProfile()
        {
            var old=(ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeCombatAudioMix",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(ProvingProfile.CreateDefault(),null);
            Assert.That(old.Descriptor("audio.weaponGain"),Is.Null);Assert.That(old.Descriptor("audio.music.combatDuckGain"),Is.Null);string serialized=JsonUtility.ToJson(old);
            audio.SetDiagnosticMute(true);var owner=new GameObject("legacy-combat-audio");owner.transform.SetParent(ground.transform,false);
            var legacy=new NativeGameAudio(owner.transform,old,false);
            try
            {
                legacy.Bind(ground.Session,ground.Composition);legacy.TickMusic(false,false,4);
                var sources=owner.GetComponentsInChildren<AudioSource>();var music=sources.Where(v=>v.name.Contains("-music-")).ToArray();float idle=music.Sum(v=>v.volume);
                Assert.That(idle,Is.GreaterThan(0));
                Invoke(legacy,"Shot",new ShotNotice(1,0,1,0,ground.Session.Pose(0).Position,Array.Empty<PelletNotice>(),WeaponId.Rifle));
                var shot=sources.Single(v=>v.name.StartsWith("voice-")&&v.clip);Assert.That(shot.volume,Is.EqualTo(.8f).Within(.0001f));
                Assert.That(music.Sum(v=>v.volume),Is.EqualTo(idle*.25f).Within(.0001f));shot.Stop();legacy.TickMusic(false,false,.3f);
                Assert.That(music.Sum(v=>v.volume),Is.EqualTo(idle).Within(.0001f));Assert.That(JsonUtility.ToJson(old),Is.EqualTo(serialized));
            }
            finally{legacy.Dispose();UnityEngine.Object.Destroy(owner);}
            yield return null;
        }
        [UnityTest] public IEnumerator OwnAndNearbyGunshotsGainForegroundOverFullVolumeMusic()
        {
            audio.TickMusic(false,false,4);Assert.That(audio.Music.HasOutput,Is.True);
            foreach(int seat in new[]{0,1})
            {
                if(seat==1)ground.PlaceCombatReviewSeat(1,new Vector3(-6,0,2),0);
                foreach(var weapon in new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher})
                {
                    Clear();EffectsProfile.Set("audio.weaponGain",1);MusicProfile.Set("audio.music.combatDuckGain",1);audio.TickMusic(false,false,0);
                    var shot=new ShotNotice(1,seat,1,0,ground.Session.Pose(seat).Position,Array.Empty<PelletNotice>(),weapon);
                    Invoke(audio,"Shot",shot);float oldShot=Voices.Single(v=>v.clip).volume,oldMusic=MusicSources.Sum(v=>v.volume);
                    Assert.That(oldMusic,Is.GreaterThan(0));Clear();
                    EffectsProfile.Set("audio.weaponGain",2);MusicProfile.Set("audio.music.combatDuckGain",.25f);audio.TickMusic(false,false,0);
                    Invoke(audio,"Shot",shot);var foreground=Voices.Single(v=>v.clip);float quietMusic=MusicSources.Sum(v=>v.volume);
                    Assert.That(foreground.volume,Is.GreaterThan(oldShot*1.4f),weapon+" seat "+seat);
                    Assert.That(quietMusic,Is.GreaterThan(0));Assert.That(quietMusic,Is.LessThan(oldMusic*.4f));
                    Assert.That(foreground.volume/quietMusic,Is.GreaterThan(oldShot/oldMusic*4));
                    if(seat==0)Assert.That(foreground.panStereo,Is.EqualTo(0).Within(.0001f));else Assert.That(foreground.panStereo,Is.GreaterThan(0));
                    Assert.That(Voices.Count(v=>v.isPlaying),Is.EqualTo(1),"One event is mixed once; stopped pool slots do not contribute audio");
                    Assert.That(Voices.Where(v=>v.isPlaying).Sum(v=>v.volume)+MusicSources.Where(v=>v.isPlaying).Sum(v=>v.volume),Is.LessThanOrEqualTo(1.0001f));
                }
            }
            Assert.That(NativeAudioPreferences.Music(MusicProfile),Is.EqualTo(100));Assert.That(NativeAudioPreferences.Effects(EffectsProfile),Is.EqualTo(80));
            yield return null;
        }
        [UnityTest] public IEnumerator DistantGunshotsDuckLessAndInaudibleShotsLeaveMusicUnchanged()
        {
            audio.TickMusic(false,false,4);float idle=MusicSources.Sum(v=>v.volume);Assert.That(idle,Is.GreaterThan(0));
            float FireAt(float distance)
            {
                Clear();audio.TickMusic(false,false,0);var point=ground.Session.Pose(0).Position+Vector3.right*distance;
                Invoke(audio,"Shot",new ShotNotice(1,1,1,0,point,Array.Empty<PelletNotice>(),WeaponId.Rifle));return MusicSources.Sum(v=>v.volume);
            }
            float near=FireAt(2),far=FireAt(20);Assert.That(near,Is.LessThan(far));Assert.That(far,Is.LessThan(idle));
            Assert.That(FireAt(EffectsProfile.Get("audio.maxDistanceMeters")+1),Is.EqualTo(idle).Within(.0001f));
            Assert.That(Voices.Any(v=>v.clip),Is.False);
            yield return null;
        }
        [UnityTest] public IEnumerator CombatDuckingReleasesOnMusicClockAndEffectsZeroRestoreLeavesNoGhost()
        {
            audio.TickMusic(false,false,4);float idle=MusicSources.Sum(v=>v.volume);string track=audio.Music.TrackId;
            Invoke(audio,"Shot",new ShotNotice(1,0,1,0,ground.Session.Pose(0).Position,Array.Empty<PelletNotice>(),WeaponId.Rifle));
            float ducked=MusicSources.Sum(v=>v.volume);Assert.That(ducked,Is.LessThan(idle*.4f));
            foreach(var voice in Voices)voice.Stop();
            for(int i=0;i<10;i++)audio.UpdateVolume();Assert.That(MusicSources.Sum(v=>v.volume),Is.EqualTo(ducked).Within(.0001f),"Refresh does not advance the release clock");
            audio.TickMusic(false,true,10);Assert.That(MusicSources.Sum(v=>v.volume),Is.Zero);
            audio.TickMusic(false,false,0);Assert.That(MusicSources.Sum(v=>v.volume),Is.EqualTo(ducked).Within(.0001f));
            float half=MusicProfile.Get("audio.music.combatDuckReleaseSeconds")*.5f;
            audio.TickMusic(false,false,half);float recovering=MusicSources.Sum(v=>v.volume);
            Assert.That(recovering,Is.GreaterThan(ducked));Assert.That(recovering,Is.LessThan(idle));
            audio.TickMusic(false,false,half);Assert.That(MusicSources.Sum(v=>v.volume),Is.EqualTo(idle).Within(.0001f));Assert.That(audio.Music.TrackId,Is.EqualTo(track));
            Invoke(audio,"Shot",new ShotNotice(1,0,1,0,ground.Session.Pose(0).Position,Array.Empty<PelletNotice>(),WeaponId.Rifle));
            NativeAudioPreferences.SetEffects(0);audio.UpdateVolume();Assert.That(Voices.All(v=>!v.isPlaying),Is.True);
            Assert.That(MusicSources.Sum(v=>v.volume),Is.EqualTo(idle).Within(.0001f));
            NativeAudioPreferences.SetEffects(80);Invoke(audio,"Shot",new ShotNotice(1,0,1,0,ground.Session.Pose(0).Position,Array.Empty<PelletNotice>(),WeaponId.Rifle));
            ground.Session.Restore(ground.Session.Capture());audio.TickMusic(false,false,0);Assert.That(MusicSources.Sum(v=>v.volume),Is.EqualTo(idle).Within(.0001f));
            yield return null;
        }
        [UnityTest] public IEnumerator NearbyCutterDucksMusicForSustainAndSharedCombatMixStaysBounded()
        {
            audio.TickMusic(false,false,4);float idle=MusicSources.Sum(v=>v.volume);
            var snap=ground.Session.Capture();snap.Beams[1].Active=true;snap.Beams[1].EmissionSeconds=1;
            snap.Beams[1].Origin=new Vector3(10,0,2);snap.Beams[1].Endpoint=ground.Session.Pose(0).Position;
            ground.Session.Restore(snap);audio.TickMusic(false,false,0);for(int i=0;i<5;i++)audio.Tick(true);
            var cutter=root.Find("cutter-1").GetComponent<AudioSource>();Assert.That(cutter.isPlaying,Is.True);Assert.That(cutter.volume,Is.GreaterThan(.3f));
            Assert.That(MusicSources.Sum(v=>v.volume),Is.LessThan(idle*.4f));
            for(int i=0;i<Voices.Length;i++)Invoke(audio,"Shot",new ShotNotice(1,0,1,0,ground.Session.Pose(0).Position,Array.Empty<PelletNotice>(),WeaponId.Rifle));
            Assert.That(Voices.Sum(v=>v.volume)+cutter.volume+MusicSources.Sum(v=>v.volume),Is.LessThanOrEqualTo(1.0001f));
            Assert.That(MusicSources.Sum(v=>v.volume),Is.GreaterThan(0));
            NativeAudioPreferences.SetMusic(0);audio.UpdateVolume();float effectsOnly=Voices.Sum(v=>v.volume)+cutter.volume;
            Assert.That(MusicSources.Sum(v=>v.volume),Is.Zero);Assert.That(effectsOnly,Is.EqualTo(.8f).Within(.0001f));
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
