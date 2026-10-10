using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class BotReactionSettingsTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;Keyboard keyboard;
        float priorVolume;
        InputSettings.EditorInputBehaviorInPlayMode priorEditorInput;InputSettings.BackgroundBehavior priorBackground;
        readonly Dictionary<string,int?> saved=new Dictionary<string,int?>();
        [SetUp] public void IsolatePreferences()
        {
            priorEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach(string k in new[]{NativeBotReactionPreferences.TextKey,NativeBotReactionPreferences.VoiceKey,"StarTournament.Audio.EffectsPercent"})
                saved[k]=PlayerPrefs.HasKey(k)?PlayerPrefs.GetInt(k):(int?)null;
            PlayerPrefs.DeleteKey(NativeBotReactionPreferences.TextKey);PlayerPrefs.DeleteKey(NativeBotReactionPreferences.VoiceKey);
            NativeAudioPreferences.SetEffects(100);priorVolume=AudioListener.volume;AudioListener.volume=0;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(scene.IsValid()&&scene.isLoaded)yield return SceneManager.UnloadSceneAsync(scene);
            foreach(var item in saved)if(item.Value.HasValue)PlayerPrefs.SetInt(item.Key,item.Value.Value);else PlayerPrefs.DeleteKey(item.Key);
            PlayerPrefs.Save();AudioListener.volume=priorVolume;
            InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;InputSystem.settings.backgroundBehavior=priorBackground;
        }
        IEnumerator Load()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();yield return null;
        }
        Button Button(string n)=>ground.GetComponentsInChildren<Button>(true).Single(x=>x.name==n);
        Toggle Toggle(string n)=>ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name==n);
        static object Field(object o,string n)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);
        static void Call(object o,string n)=>o.GetType().GetMethod(n,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(o,null);
        IEnumerator Press(GamepadButton b)
        {InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(b));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;}
        [UnityTest] public IEnumerator SettingsDefaultsNavigationAndPersistence()
        {
            yield return Load();pad=InputSystem.AddDevice<Gamepad>();keyboard=InputSystem.AddDevice<Keyboard>();
            Button("main-action-4").onClick.Invoke();yield return null;
            Button("settings-section-2").onClick.Invoke();yield return null;
            var text=Toggle("settings-bot-reaction-text");var voice=Toggle("settings-bot-reaction-voice");
            Assert.That(text.isOn,Is.True);Assert.That(voice.isOn,Is.False);
            Assert.That(text.transform.Find("label").GetComponent<Text>().text,Is.EqualTo("Реакции ботов: текст"));
            Assert.That(voice.transform.Find("label").GetComponent<Text>().text,Is.EqualTo("Реакции ботов: голос"));
            EventSystem.current.SetSelectedGameObject(Toggle("settings-fps").gameObject);
            yield return Press(GamepadButton.DpadDown);Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(text.gameObject));
            yield return Press(GamepadButton.South);Assert.That(text.isOn,Is.False);Assert.That(voice.isOn,Is.False);
            yield return Press(GamepadButton.DpadDown);Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(voice.gameObject));
            yield return Press(GamepadButton.South);Assert.That(voice.isOn,Is.True);Assert.That(text.isOn,Is.False);
            // EventSystem pointer route is shared by mouse; keyboard Submit is the standard selectable route.
            text.OnPointerClick(new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left});Assert.That(text.isOn,Is.True);
            EventSystem.current.SetSelectedGameObject(voice.gameObject);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Assert.That(voice.isOn,Is.False);Assert.That(text.isOn,Is.True);
            foreach(bool t in new[]{false,true})foreach(bool v in new[]{false,true})
            {text.isOn=t;voice.isOn=v;Assert.That(NativeBotReactionPreferences.TextEnabled,Is.EqualTo(t));Assert.That(NativeBotReactionPreferences.VoiceEnabled,Is.EqualTo(v));}
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_BANTER_SETTINGS_EVIDENCE");
            if(!string.IsNullOrEmpty(output)&&SystemInfo.graphicsDeviceType!=GraphicsDeviceType.Null)
            {Directory.CreateDirectory(output);yield return EditorUiCapture.Capture(ground,Path.Combine(output,"bot-reaction-settings.png"));}
            NativeBotReactionPreferences.SetText(false);NativeBotReactionPreferences.SetVoice(true);
            yield return SceneManager.UnloadSceneAsync(scene);yield return Load();
            Button("main-action-4").onClick.Invoke();yield return null;Button("settings-section-2").onClick.Invoke();yield return null;
            Assert.That(Toggle("settings-bot-reaction-text").isOn,Is.False);Assert.That(Toggle("settings-bot-reaction-voice").isOn,Is.True);
        }
        static NativeMatchComposition Composition(int locals)
        {
            var roster=NativeMatchRoster.Ffa(locals+1);
            return new NativeMatchComposition(roster,Enumerable.Range(0,locals+1).Select(p=>new NativeParticipantInfo(
                p==0?NativeParticipantKind.Bot:NativeParticipantKind.LocalHuman,p==0?"Бот Борис":"Игрок "+p,
                NativeStandingsView.Identity(roster.Read(),p,false),p==0?1:-1)).ToArray(),Enumerable.Range(1,locals).ToArray());
        }
        NativeGameAudio Audio=>(NativeGameAudio)Field(ground,"gameAudio");
        AudioSource Voice=>ground.transform.Find("native-game-audio/bot-reaction").GetComponent<AudioSource>();
        IEnumerator Start(int locals)
        {
            ground.GetType().GetField("FullHealReviewManualTick",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,true);
            ground.StartBotReview(Composition(locals),1);yield return NativeLoadingTestScene.Wait(ground);yield return null;
            Audio.SetDiagnosticMute(false);
        }
        void EmitVoicedLine()
        {
            var old=(NativeBotBanter)Field(ground,"botBanter");old.Dispose();
            var observer=new NativeBotBanter(ground.Session,ground.Composition,scene.GetPhysicsScene(),ground.Profile.Get("camera.eyeHeight"),100,1,
                new BotBanterPolicy{InitialSilence=0,GlobalMinimum=0,GlobalMaximum=0,BotMinimum=0,BotMaximum=0,Chance=1});
            var m=typeof(ProvingGround).GetMethod("OnBotBanterLineSelected",BindingFlags.Instance|BindingFlags.NonPublic);
            observer.Banter.LineSelected+=(Action<BotBanterLine>)Delegate.CreateDelegate(typeof(Action<BotBanterLine>),ground,m);
            ground.GetType().GetField("botBanter",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,observer);
            // Controlled attempted-fire fact; actual combat event attribution is covered by BotBanterIntegrationTests.
            observer.Banter.Attack(1,ground.Session.Life(1).Life,0,ground.Session.Life(0).Life,ground.Session.Time);
            ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,100,0,ground.Session.Life(0).Life);observer.Flush();
            Assert.That(observer.Banter.Current.Value.Text,Is.EqualTo("Даже не поцарапал!"));
        }
        [UnityTest] public IEnumerator FourChannelCombinationsHaveOneVoiceAcrossOneAndFourViews()
        {
            yield return Load();
            foreach(int locals in new[]{1,4})foreach(bool text in new[]{false,true})foreach(bool voice in new[]{false,true})
            {
                NativeBotReactionPreferences.SetText(text);NativeBotReactionPreferences.SetVoice(voice);yield return Start(locals);
                int count=Audio.BotReactionPlayCount;EmitVoicedLine();yield return null;
                var labels=ground.GetComponentsInChildren<Text>(true).Where(t=>t.name.StartsWith("bot-banter-notice-")&&t.gameObject.activeInHierarchy).ToArray();
                Assert.That(labels.Length,Is.EqualTo(locals));foreach(var label in labels)Assert.That(string.IsNullOrEmpty(label.text),Is.EqualTo(!text));
                Assert.That(Audio.BotReactionPlayCount-count,Is.EqualTo(voice?1:0));Assert.That(Voice.isPlaying,Is.EqualTo(voice));
                Assert.That(Voice.spatialBlend,Is.Zero);Assert.That(Voice.loop,Is.False);
                if(voice){Assert.That(Voice.clip,Is.SameAs(Resources.Load<AudioClip>(NativeBotBanterVoice.Clips["Даже не поцарапал!"])));Assert.That(Voice.volume,Is.GreaterThan(0));}
            }
        }
        [UnityTest] public IEnumerator VoiceLifecycleAndEffectsMixNeverReplayOldLines()
        {
            yield return Load();NativeBotReactionPreferences.SetVoice(false);yield return Start(1);EmitVoicedLine();
            int count=Audio.BotReactionPlayCount;NativeBotReactionPreferences.SetVoice(true);yield return null;
            Assert.That(Audio.BotReactionPlayCount,Is.EqualTo(count));Assert.That(Voice.isPlaying,Is.False);
            Assert.That(Audio.PlayBotReaction("Неизвестная фраза",0),Is.False);
            Assert.That(Audio.PlayBotReaction("Ты хоть попади сначала!",0),Is.True);
            Assert.That(Voice.clip,Is.SameAs(Resources.Load<AudioClip>(NativeBotBanterVoice.Clips["Ты хоть попади сначала!"])));
            Assert.That(Audio.PlayBotReaction("Даже не поцарапал!",0),Is.True);Assert.That(Voice.isPlaying,Is.True);
            NativeAudioPreferences.SetEffects(50);Audio.MenuConfirm();Audio.UpdateVolume();
            Assert.That(Voice.volume,Is.InRange(.001f,.5f));
            var ui=ground.transform.Find("native-game-audio/menu-ui").GetComponent<AudioSource>();
            Assert.That(Voice.volume+ui.volume,Is.LessThanOrEqualTo(.5001f),"Voice participates in the shared SFX budget");
            NativeBotReactionPreferences.SetVoice(false);Audio.UpdateVolume();Assert.That(Voice.isPlaying,Is.False);Assert.That(ui.isPlaying,Is.True,"Voice OFF stops only reactions");
            NativeBotReactionPreferences.SetVoice(true);Audio.UpdateVolume();Assert.That(Voice.isPlaying,Is.False);
            Audio.PlayBotReaction("Даже не поцарапал!",0);NativeAudioPreferences.SetEffects(0);Audio.UpdateVolume();Assert.That(Voice.isPlaying,Is.False);
            NativeAudioPreferences.SetEffects(100);Audio.UpdateVolume();Assert.That(Voice.isPlaying,Is.False);
            Audio.PlayBotReaction("Даже не поцарапал!",0);Audio.SetDiagnosticMute(true);Assert.That(Voice.isPlaying,Is.False);Audio.SetDiagnosticMute(false);Assert.That(Voice.isPlaying,Is.False);
            Audio.PlayBotReaction("Даже не поцарапал!",0);count=Audio.BotReactionPlayCount;Call(ground,"FullHealReviewPause");Assert.That(Voice.isPlaying,Is.False);
            Call(ground,"FullHealReviewResume");yield return null;Assert.That(Audio.BotReactionPlayCount,Is.EqualTo(count));Assert.That(Voice.isPlaying,Is.False);
            Audio.PlayBotReaction("Даже не поцарапал!",0);ground.Session.Restore(ground.Session.Capture());Assert.That(Voice.isPlaying,Is.False);
            Audio.PlayBotReaction("Даже не поцарапал!",0);Audio.Bind(ground.Session,ground.Composition);Assert.That(Voice.isPlaying,Is.False);
            Audio.PlayBotReaction("Даже не поцарапал!",0);ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,100,1,ground.Session.Life(1).Life);Assert.That(Voice.isPlaying,Is.False);
        }
        [UnityTest] public IEnumerator EveryPhrasePlaysItsOwnClipFromOneSource()
        {
            yield return Load();NativeBotReactionPreferences.SetVoice(true);yield return Start(1);
            int count=Audio.BotReactionPlayCount;
            foreach(var entry in NativeBotBanterVoice.Clips)
            {
                Assert.That(Audio.PlayBotReaction(entry.Key,0),Is.True,entry.Key);
                Assert.That(Voice.clip,Is.SameAs(Resources.Load<AudioClip>(entry.Value)),entry.Key);
                Assert.That(Voice.isPlaying,Is.True,entry.Key);
            }
            Assert.That(Audio.BotReactionPlayCount-count,Is.EqualTo(20));
            Assert.That(ground.transform.Find("native-game-audio").GetComponentsInChildren<AudioSource>().Count(x=>x.name=="bot-reaction"),Is.EqualTo(1));
            var current=Voice.clip;
            Assert.That(Audio.PlayBotReaction(null,0),Is.False);Assert.That(Audio.PlayBotReaction("Слабак",0),Is.False,"No punctuation-insensitive fallback");
            Assert.That(Audio.PlayBotReaction("Слабак!",-1),Is.False);Assert.That(Audio.PlayBotReaction("Слабак!",ground.Session.ParticipantCount),Is.False);
            Assert.That(Voice.clip,Is.SameAs(current));
        }
        [UnityTest] public IEnumerator DeadComplaintsReachAudioAndRespawnNeverReplaysThem()
        {
            yield return Load();NativeBotReactionPreferences.SetVoice(true);yield return Start(1);
            Assert.That(Audio.PlayBotReaction("Кто следующий?",0),Is.True);
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,100,1,ground.Session.Life(1).Life);
            Assert.That(ground.Session.Life(0).Dead,Is.True);Assert.That(Voice.isPlaying,Is.False);
            Assert.That(Audio.PlayBotReaction("Кто следующий?",0),Is.False,"Dead live boast rejected");
            var selected=typeof(ProvingGround).GetMethod("OnBotBanterLineSelected",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var reason in new[]{BotBanterReason.SeriesEnded,BotBanterReason.RepeatedKiller,BotBanterReason.LosingStreak,BotBanterReason.SelfExplosion,BotBanterReason.SelfKill})
            foreach(var text in BotBanter.Lines(reason))
            {
                int before=Audio.BotReactionPlayCount;
                selected.Invoke(ground,new object[]{new BotBanterLine(0,1,reason,text,ground.Session.Time+4,true)});
                Assert.That(Audio.BotReactionPlayCount,Is.EqualTo(before+1),text);
                Assert.That(Voice.clip,Is.SameAs(Resources.Load<AudioClip>(NativeBotBanterVoice.Clips[text])),text);
                Assert.That(Voice.isPlaying,Is.True,text);
            }
            int count=Audio.BotReactionPlayCount,ticks=0;
            while(ground.Session.Life(0).Dead)
            {
                Assert.That(++ticks,Is.LessThan(400));
                ground.Session.Tick(new LocalAction[ground.Composition.ParticipantCount],.02f);
            }
            yield return null;
            Assert.That(Audio.BotReactionPlayCount,Is.EqualTo(count));Assert.That(Voice.isPlaying,Is.True,"Already started complaint can finish after respawn");
            Audio.StopBotReaction();
            selected.Invoke(ground,new object[]{new BotBanterLine(0,1,BotBanterReason.KillStreak,"Кто следующий?",ground.Session.Time+4,false)});
            Assert.That(Voice.isPlaying,Is.True);
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,100,1,ground.Session.Life(1).Life);
            Assert.That(Voice.isPlaying,Is.False,"Respawned live boast retains death stop");
        }
    }
}
