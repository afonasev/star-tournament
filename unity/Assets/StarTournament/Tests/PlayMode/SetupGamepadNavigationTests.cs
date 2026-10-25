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
    public sealed class SetupGamepadNavigationTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;Keyboard keyboard;Mouse mouse;
        bool hadLastMap;string priorLastMap;float priorVolume;
        InputSettings.EditorInputBehaviorInPlayMode priorInput;
        InputSettings.BackgroundBehavior priorBackground;
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        string Focus
        {
            get
            {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                if(pad!=null&&(bool)typeof(ProvingGround).GetField("inlineRouting",flags).GetValue(ground)&&(bool)typeof(ProvingGround).GetField("menuGamepadCursor",flags).GetValue(ground))
                {
                    var editor=typeof(ProvingGround).GetMethod("InlineFor",flags).Invoke(ground,new object[]{pad});
                    return ((Button)editor.GetType().GetField("Focus").GetValue(editor))?.name;
                }
                return EventSystem.current.currentSelectedGameObject?.name;
            }
        }
        int Step=>(int)typeof(ProvingGround).GetField("setupStep",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
        [SetUp] public void Setup()
        {
            hadLastMap=PlayerPrefs.HasKey(ProvingGround.LastPlayedMapPreferenceKey);priorLastMap=PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey);
            PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
            priorVolume=AudioListener.volume;AudioListener.volume=0;
            priorInput=InputSystem.settings.editorInputBehaviorInPlayMode;priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            UnityEngine.Random.InitState(20261008);
        }
        IEnumerator Load()
        {
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            if(pad==null||!pad.added)pad=InputSystem.AddDevice<Gamepad>();
            EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);yield return Press(GamepadButton.South);
        }
        IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        IEnumerator HoldStart(int expectedStep)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));
            for(int i=0;i<8;i++){yield return null;if(i>1)Assert.That(Step,Is.EqualTo(expectedStep),"Held Start must advance only once");}
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        HashSet<string> Reachable()
        {
            var visited=new HashSet<Selectable>();var pending=new Stack<Selectable>();pending.Push(EventSystem.current.currentSelectedGameObject.GetComponent<Selectable>());
            while(pending.Count>0)
            {
                var item=pending.Pop();if(item==null||!visited.Add(item))continue;
                var n=item.navigation;
                foreach(var next in new[]{n.selectOnUp,n.selectOnDown,n.selectOnLeft,n.selectOnRight})if(next!=null)pending.Push(next);
            }
            var names=new HashSet<string>(visited.Select(s=>s.name));
            Assert.That(names.Any(n=>n.StartsWith("setup-step-")||n=="setup-next"),Is.False,"Removed tabs and next-step control must not be navigable");
            return names;
        }
        IEnumerator Click(string name){EventSystem.current.SetSelectedGameObject(B(name).gameObject);B(name).onClick.Invoke();yield return null;yield return null;}
        [UnityTest] public IEnumerator SetupShortcutHintsFollowDeviceAndEditorAvailability()
        {
            yield return Load();ground.AddBot();yield return null;
            var start=B("Начать — четыре игрока");
            Assert.That(start.GetComponentInChildren<Text>().text,Is.EqualTo("Начать матч"));
            Assert.That(start.GetComponent<MenuActionHint>().GlyphLabel.enabled,Is.True);
            Assert.That(start.GetComponent<MenuActionHint>().GlyphLabel.text,Is.EqualTo("Start"));
            keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(start.GetComponent<MenuActionHint>().GlyphLabel.enabled,Is.False,"Keyboard hides pad shortcuts");
            yield return Press(GamepadButton.DpadRight);
            Assert.That(start.GetComponent<MenuActionHint>().GlyphLabel.enabled,Is.True);
            Assert.That(B("roster-add-human").GetComponentInChildren<Text>().text,Is.EqualTo("Присоединиться"));
            Assert.That(B("roster-add-bot").GetComponentInChildren<Text>().text,Is.EqualTo("Добавить бота"));
            yield return CaptureIfRequested("05-roster-start-hint");
            yield return Click("roster-card-0");
            Assert.That(start.GetComponent<MenuActionHint>().GlyphLabel.enabled,Is.False,"Participant editor owns input");
            yield return Press(GamepadButton.Start);Assert.That(ground.Running,Is.False);
            yield return Press(GamepadButton.East);ground.RemoveBot(0);yield return null;yield return null;
            Assert.That(start.IsInteractable(),Is.False);Assert.That(start.GetComponent<MenuActionHint>().GlyphLabel.enabled,Is.False);
            yield return Press(GamepadButton.Start);Assert.That(ground.Running,Is.False);
            EventSystem.current.SetSelectedGameObject(B("roster-add-bot").gameObject);yield return Press(GamepadButton.South);
            Assert.That(B("lobby-add-difficulty-1").gameObject.activeInHierarchy,Is.True);
            Assert.That(ground.SetupBotCount,Is.Zero,"Opening the flow does not commit a bot");
        }
        [UnityTest] public IEnumerator ArrowsPreviewMapAConfirmsAndNumericBPreservesDraft()
        {
            yield return Load();Assert.That(Focus,Is.EqualTo("lobby-map"));
            Assert.That(Reachable(),Is.SupersetOf(new[]{"lobby-map","lobby-mode","lobby-time","lobby-target","roster-card-0","setup-previous","roster-add-bot","roster-add-human"}));
            yield return Press(GamepadButton.South);yield return Press(GamepadButton.DpadRight);
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id),"Focus previews, confirmation commits");
            Assert.That(Focus,Is.EqualTo("lobby-map-"+IndustrialTunnelsCatalog.Id));
            yield return CaptureIfRequested("01-map-preview");yield return Press(GamepadButton.South);
            Assert.That(ground.SelectedMapId,Is.EqualTo(IndustrialTunnelsCatalog.Id));Assert.That(Focus,Is.EqualTo("lobby-map"));
            yield return Press(GamepadButton.DpadRight);yield return Press(GamepadButton.South);
            Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Teams));Assert.That(Focus,Is.EqualTo("lobby-mode"));
            float before=ground.Configuration.DurationMinutes;yield return Click("lobby-time");
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(before+1));Assert.That(Focus,Is.EqualTo("lobby-time"));
            yield return Press(GamepadButton.East);Assert.That(B("main-action-0").gameObject.activeInHierarchy,Is.True);
            yield return Press(GamepadButton.South);Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(before+1));
        }
        [UnityTest] public IEnumerator StartHoldAndSimultaneousSubmitCannotBypassModal()
        {
            yield return Load();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start).WithButton(GamepadButton.South));
            for(int i=0;i<8;i++){yield return null;if(i>1){Assert.That(ground.Running,Is.False);Assert.That(B("lobby-map-"+CombatBowlCatalog.Id).gameObject.activeInHierarchy,Is.True);}}
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return Press(GamepadButton.East);
            yield return Click("roster-card-0");yield return Press(GamepadButton.Start);Assert.That(ground.Running,Is.False);
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.Start);Assert.That(ground.Running,Is.False,"One participant cannot start");
            ground.AddBot();InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start));
            yield return null;yield return NativeLoadingTestScene.Wait(ground);
            for(int i=0;i<8;i++){yield return null;Assert.That(ground.Running,Is.True,"Held Start cannot pause the new match");}
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        [UnityTest] public IEnumerator MapDraftSurvivesReentryButColdReloadUsesDefaults()
        {
            yield return Load();yield return Click("lobby-map");yield return Click("lobby-map-"+IndustrialTunnelsCatalog.Id);
            ground.AddBot();yield return Press(GamepadButton.Start);yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.Running,Is.True);Assert.That(PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey),Is.EqualTo(IndustrialTunnelsCatalog.Id));
            typeof(ProvingGround).GetMethod("ToMainMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,null);
            yield return Press(GamepadButton.South);Assert.That(ground.SelectedMapId,Is.EqualTo(IndustrialTunnelsCatalog.Id));
            yield return Click("lobby-map");yield return Click("lobby-map-"+LunarLaboratoryCatalog.Id);
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.South);
            Assert.That(ground.SelectedMapId,Is.EqualTo(LunarLaboratoryCatalog.Id));
            yield return SceneManager.UnloadSceneAsync(scene);yield return Load();
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id));Assert.That(ground.SetupBotCount,Is.Zero);Assert.That(Focus,Is.EqualTo("lobby-map"));
        }
        [UnityTest] public IEnumerator MissingSavedMapFallsBackAndMouseChoiceReturnsToLobby()
        {
            PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,"removed-map");yield return Load();
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id));Assert.That(Focus,Is.EqualTo("lobby-map"));
            yield return Click("lobby-map");yield return Click("lobby-map-"+LunarLaboratoryCatalog.Id);
            Assert.That(ground.SelectedMapId,Is.EqualTo(LunarLaboratoryCatalog.Id));Assert.That(Focus,Is.EqualTo("lobby-map"));
            yield return Press(GamepadButton.DpadRight);Assert.That(Focus,Is.EqualTo("lobby-mode"));Reachable();
        }
        [UnityTest] public IEnumerator KeyboardEnterAndGamepadNavigationDoNotRebind()
        {
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(Focus,Is.EqualTo("lobby-map"));
            yield return Click("lobby-time");InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.That(Focus,Is.EqualTo("lobby-time"));Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(5));
            yield return Press(GamepadButton.DpadDown);Reachable();Assert.That(Focus,Does.StartWith("roster-"));
            var input=(SeatInputCoordinator)typeof(ProvingGround).GetField("input",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
            Assert.That(input.DeviceAt(0),Is.SameAs(keyboard),"Menu navigation must not rebind the human device");
        }
        // Optional offscreen Editor rendering is evidence of UI state, not native Player/device acceptance.
        IEnumerator CaptureIfRequested(string name)
        {
            string directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SETUP_QA_DIR");if(string.IsNullOrEmpty(directory))yield break;
            Directory.CreateDirectory(directory);
            yield return EditorUiCapture.Capture(ground,Path.Combine(directory,name+".png"));
File.WriteAllText(Path.Combine(directory,name+".txt"),"EDITOR_OFFSCREEN_SYNTHETIC_NOT_PLAYER_ACCEPTANCE\nseed=20261008\nsize=1920x1080\nstep="+Step+"\nfocus="+Focus+"\nmap="+ground.SelectedMapId+"\nrevision="+Environment.GetEnvironmentVariable("STAR_TOURNAMENT_QA_SOURCE_REVISION")+"\nmuted=true\n");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            if(hadLastMap)PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,priorLastMap);else PlayerPrefs.DeleteKey(ProvingGround.LastPlayedMapPreferenceKey);
            PlayerPrefs.Save();InputSystem.settings.editorInputBehaviorInPlayMode=priorInput;InputSystem.settings.backgroundBehavior=priorBackground;AudioListener.volume=priorVolume;
        }
    }
}
