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
        string Focus=>EventSystem.current.currentSelectedGameObject?.name;
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
            Assert.That(names.Any(n=>n.StartsWith("setup-step-")||n=="setup-next"||n=="setup-previous"||n=="Начать — четыре игрока"),Is.False,"Tabs and footer must not be navigable");
            return names;
        }
        [UnityTest] public IEnumerator ArrowsChooseMapAEditsRulesStartAdvancesAndBPreservesDraft()
        {
            yield return Load();Assert.That(Focus,Is.EqualTo("arena-choice-0"));
            Assert.That(Reachable(),Is.EquivalentTo(new[]{"arena-choice-0","arena-choice-1","arena-choice-2"}));
            yield return Press(GamepadButton.DpadDown);
            Assert.That(ground.SelectedMapId,Is.EqualTo(IndustrialTunnelsCatalog.Id));Assert.That(Focus,Is.EqualTo("arena-choice-1"));
            Assert.That(B("arena-choice-1").GetComponent<Image>().color,Is.Not.EqualTo(B("arena-choice-0").GetComponent<Image>().color));
            yield return CaptureIfRequested("01-map-immediate-selection");
            yield return Press(GamepadButton.South);Assert.That(Step,Is.EqualTo(1));Assert.That(Focus,Is.EqualTo("mode-choice-0"));
            Assert.That(Reachable(),Is.EquivalentTo(new[]{"mode-choice-0","mode-choice-1","duration-minus","duration-plus","Цель −","Цель +"}));
            yield return CaptureIfRequested("02-rules-first-setting");
            yield return Press(GamepadButton.DpadRight);yield return Press(GamepadButton.South);
            Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Teams));Assert.That(Step,Is.EqualTo(1),"A changes the setting without advancing");
            yield return Press(GamepadButton.DpadLeft);yield return Press(GamepadButton.South);
            Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Ffa));
            yield return Press(GamepadButton.DpadDown);Assert.That(Focus,Is.EqualTo("duration-minus"));
            yield return Press(GamepadButton.South); // Establish room below the duration maximum.
            yield return Press(GamepadButton.DpadRight);float before=ground.Configuration.DurationMinutes;
            yield return Press(GamepadButton.South);Assert.That(ground.Configuration.DurationMinutes,Is.GreaterThan(before));
            float duration=ground.Configuration.DurationMinutes;
            yield return HoldStart(2);Assert.That(Focus,Is.EqualTo("roster-card-0"));
            Assert.That(Reachable(),Does.Contain("roster-card-1"));
            yield return CaptureIfRequested("03-players-card-focus");
            yield return Press(GamepadButton.East);Assert.That(Step,Is.EqualTo(1));Assert.That(Focus,Is.EqualTo("mode-choice-0"));
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(duration));
            yield return Press(GamepadButton.East);Assert.That(Step,Is.EqualTo(0));Assert.That(Focus,Is.EqualTo("arena-choice-1"));
            Assert.That(ground.SelectedMapId,Is.EqualTo(IndustrialTunnelsCatalog.Id));
            yield return Press(GamepadButton.East);Assert.That(B("main-action-0").gameObject.activeInHierarchy,Is.True);
        }
        [UnityTest] public IEnumerator StartHoldAndSimultaneousSubmitCannotSkipSteps()
        {
            yield return Load();
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.Start).WithButton(GamepadButton.South));
            for(int i=0;i<8;i++){yield return null;if(i>1)Assert.That(Step,Is.EqualTo(1));}
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            yield return HoldStart(2);Assert.That(ground.Running,Is.False);
            B("roster-card-0").onClick.Invoke();yield return Press(GamepadButton.Start);Assert.That(ground.Running,Is.False,"Start does not bypass participant editing");
            yield return Press(GamepadButton.East);ground.RemoveBot(0);yield return Press(GamepadButton.Start);
            Assert.That(ground.Running,Is.False,"A single participant cannot start");
        }
        [UnityTest] public IEnumerator LastPlayedMapSurvivesReentryReloadAndCancelledDraft()
        {
            yield return Load();yield return Press(GamepadButton.DpadDown);
            yield return Press(GamepadButton.Start);yield return Press(GamepadButton.Start);yield return Press(GamepadButton.Start);
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(ground.Running,Is.True);Assert.That(PlayerPrefs.GetString(ProvingGround.LastPlayedMapPreferenceKey),Is.EqualTo(IndustrialTunnelsCatalog.Id));
            typeof(ProvingGround).GetMethod("ToMainMenu",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,null);
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("arena-choice-1"));
            yield return Press(GamepadButton.DpadDown);Assert.That(ground.SelectedMapId,Is.EqualTo(LunarLaboratoryCatalog.Id));
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("arena-choice-1"));
            yield return SceneManager.UnloadSceneAsync(scene);yield return Load();
            Assert.That(ground.SelectedMapId,Is.EqualTo(IndustrialTunnelsCatalog.Id));Assert.That(Focus,Is.EqualTo("arena-choice-1"));
        }
        [UnityTest] public IEnumerator MissingSavedMapFallsBackAndMouseChoiceStaysOnMap()
        {
            PlayerPrefs.SetString(ProvingGround.LastPlayedMapPreferenceKey,"removed-map");yield return Load();
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id));Assert.That(Focus,Is.EqualTo("arena-choice-0"));
            B("arena-choice-2").onClick.Invoke();yield return null;
            Assert.That(Step,Is.EqualTo(0));Assert.That(ground.SelectedMapId,Is.EqualTo(LunarLaboratoryCatalog.Id));
            EventSystem.current.SetSelectedGameObject(B("setup-step-0").gameObject);
            yield return Press(GamepadButton.DpadUp);
            Assert.That(Focus,Does.StartWith("arena-choice-"),"Gamepad must leave a tab selected by a mouse click");Reachable();
        }
        [UnityTest] public IEnumerator KeyboardKeepsEnterTransitionsAndGamepadTakesContentFocusWithoutRebinding()
        {
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);
            for(int step=0;step<3;step++)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.That(Step,Is.EqualTo(step));Assert.That(Focus,Is.EqualTo(step==2?"Начать — четыре игрока":"setup-next"));
            }
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadUp));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Reachable();Assert.That(Focus,Does.StartWith("roster-"));
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
