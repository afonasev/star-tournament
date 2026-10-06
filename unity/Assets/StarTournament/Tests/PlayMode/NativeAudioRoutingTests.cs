using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeAudioRoutingTests
    {
        Scene scene;
        Gamepad pad;
        [UnityTest] public IEnumerator MovementBanksVaryWithoutRepeatingAndWeaponReuseResetsPitch()
        {
            const string key="StarTournament.Audio.EffectsPercent";
            bool had=PlayerPrefs.HasKey(key);int prior=PlayerPrefs.GetInt(key);
            float listener=AudioListener.volume;AudioListener.volume=0;NativeAudioPreferences.SetEffects(80);
            try
            {
                yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
                var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
                pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);
                var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                audio.SetDiagnosticMute(false);
                var method=typeof(NativeGameAudio).GetMethod("PlayMovement",BindingFlags.Instance|BindingFlags.NonPublic);
                var indices=(int[,])typeof(NativeGameAudio).GetField("lastMovement",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(audio);
                var voices=ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Where(s=>s.name.StartsWith("voice-")).ToArray();
                var randomState=Random.state;
                foreach(var name in new[]{"step"})
                {
                    int kind=name=="step"?0:name=="jump"?1:2,last=-1;
                    var seen=new System.Collections.Generic.HashSet<int>();
                    for(int i=0;i<50;i++)
                    {
                        foreach(var v in voices){v.Stop();v.clip=null;}
                        method.Invoke(audio,new object[]{name,.5f,0});
                        int selected=indices[0,kind]-1;Assert.That(selected,Is.Not.EqualTo(last));last=selected;seen.Add(selected);
                        var played=voices.Single(v=>v.clip!=null);
                        Assert.That(played.clip.name,Is.EqualTo(name+"-"+selected));
                        Assert.That(played.pitch,Is.InRange(.982f,1.018f));
                        Assert.That(played.volume,Is.InRange(.5f*.65f*.8f*.95f,.5f*.65f*.8f*1.05f));
                    }
                    Assert.That(seen.Count,Is.GreaterThan(1));
                    for(int i=0;i<5;i++)Assert.That(Resources.Load<AudioClip>("Audio/Movement/"+name+"-"+i),Is.Not.Null);
                }
                foreach(var name in new[]{"jump","land"})
                {
                    foreach(var v in voices){v.Stop();v.clip=null;}
                    method.Invoke(audio,new object[]{name,.5f,0});
                    var selected=voices.Single(v=>v.clip!=null);
                    Assert.That(selected.clip.name,Is.EqualTo(name));Assert.That(selected.pitch,Is.EqualTo(1),"Approved jump/land timbres remain unchanged");
                }
                Assert.That(JsonUtility.ToJson(Random.state),Is.EqualTo(JsonUtility.ToJson(randomState)),"Presentation must not advance Unity's shared random stream");
                foreach(var v in voices){v.Stop();v.clip=null;v.pitch=.98f;}
                typeof(NativeGameAudio).GetMethod("Play",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(audio,new object[]{"rifle",1f,0});
                Assert.That(voices.Single(v=>v.clip!=null).pitch,Is.EqualTo(1));
            }
            finally
            {
                AudioListener.volume=listener;
                if(had)PlayerPrefs.SetInt(key,prior);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();
            }
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator ConfirmedRifleShotSelectsTheChosenRuntimeClip()
        {
            const string effectsKey="StarTournament.Audio.EffectsPercent";
            bool hadEffects=PlayerPrefs.HasKey(effectsKey);int previousEffects=PlayerPrefs.GetInt(effectsKey);
            NativeAudioPreferences.SetEffects(80);
            try
            {
                yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
                var ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
                ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="main-action-0").onClick.Invoke();
                pad=InputSystem.AddDevice<Gamepad>();
                ground.StartCombatReview(new[]{pad},ensureOpponent:true);
                // Keep the Editor run inaudible at the AudioListener while exercising routing.
                var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                audio.SetDiagnosticMute(false);
                var sourceRoot=ground.transform.Find("native-game-audio");
                Assert.That(sourceRoot,Is.Not.Null);
                Assert.That(Resources.Load<AudioClip>("Audio/rifle"),Is.Not.Null);
                yield return new WaitForSeconds(.4f);
                int before=ground.Session.ShotCount;
                bool fired=false;ground.Session.ShotResolved+=notice=>fired|=notice.Shooter==0&&notice.Weapon==WeaponId.Rifle;
                var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;actions[0].FireHeld=true;
                ground.Session.Tick(actions,Time.fixedDeltaTime);
                Assert.That(ground.Session.ShotCount,Is.GreaterThan(before));
                Assert.That(fired,Is.True);
                Assert.That(sourceRoot.GetComponentsInChildren<AudioSource>().Any(s=>s.name.StartsWith("voice-")&&s.clip!=null&&s.clip.name.StartsWith("rifle-")),Is.True);
            }
            finally
            {
                if(hadEffects)PlayerPrefs.SetInt(effectsKey,previousEffects);else PlayerPrefs.DeleteKey(effectsKey);
                PlayerPrefs.Save();
            }
        }
        [UnityTest] public IEnumerator MouseHoverIsSilentButGamepadNavigationStillSounds()
        {
            const string effectsKey="StarTournament.Audio.EffectsPercent";
            bool hadEffects=PlayerPrefs.HasKey(effectsKey);int previousEffects=PlayerPrefs.GetInt(effectsKey);
            float previousListenerVolume=AudioListener.volume;
            NativeAudioPreferences.SetEffects(80);
            AudioListener.volume=0;
            try
            {
                yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
                scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
                var ground=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<ProvingGround>()).Single();
                var audio=(NativeGameAudio)typeof(ProvingGround).GetField("gameAudio",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                audio.SetDiagnosticMute(false);
                var source=ground.transform.Find("native-game-audio/menu-ui").GetComponent<AudioSource>();
                source.Stop();source.clip=null;
                var battle=ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="main-action-0");
                ExecuteEvents.Execute(battle.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                yield return null;
                Assert.That(source.clip,Is.Null,"Hovering a menu button must not trigger navigation audio");
                pad=InputSystem.AddDevice<Gamepad>();
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));
                yield return null;yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
                Assert.That(source.clip?.name,Is.EqualTo("menu_move"));
            }
            finally
            {
                AudioListener.volume=previousListenerVolume;
                if(hadEffects)PlayerPrefs.SetInt(effectsKey,previousEffects);else PlayerPrefs.DeleteKey(effectsKey);
                PlayerPrefs.Save();
            }
        }
    }
}
