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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class SingleScreenLobbyTests
    {
        ProvingGround ground;Scene scene;Gamepad pad,second;
        InputSettings.EditorInputBehaviorInPlayMode oldEditor;InputSettings.BackgroundBehavior oldBackground;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,Private).GetValue(ground);
        object Call(string name,params object[] args)=>typeof(ProvingGround).GetMethod(name,Private).Invoke(ground,args);
        Button B(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        IEnumerator Click(string name){B(name).onClick.Invoke();yield return null;yield return null;}
        IEnumerator Press(GamepadButton button,Gamepad device=null)
        {device=device??pad;InputSystem.QueueStateEvent(device,new GamepadState().WithButton(button));yield return null;yield return null;InputSystem.QueueStateEvent(device,new GamepadState());yield return null;}
        [UnitySetUp] public IEnumerator Load()
        {
            oldEditor=InputSystem.settings.editorInputBehaviorInPlayMode;oldBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            yield return null;pad=InputSystem.AddDevice<Gamepad>();second=InputSystem.AddDevice<Gamepad>();yield return null;
            EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);yield return Press(GamepadButton.South);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(second);
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;
        }
        IEnumerator Capture(string name,int width=1920,int height=1080)
        {string dir=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_LOBBY_QA_DIR");if(string.IsNullOrEmpty(dir))yield break;Directory.CreateDirectory(dir);yield return new WaitForSecondsRealtime(B("setup-previous").colors.fadeDuration);yield return EditorUiCapture.Capture(ground,Path.Combine(dir,name+".png"),width,height);}
        HashSet<string> Reachable()
        {
            var seen=new HashSet<Selectable>();var stack=new Stack<Selectable>();stack.Push(B("lobby-map"));
            while(stack.Count>0){var b=stack.Pop();if(b==null||!seen.Add(b))continue;var n=b.navigation;foreach(var next in new[]{n.selectOnUp,n.selectOnDown,n.selectOnLeft,n.selectOnRight})if(next!=null)stack.Push(next);}
            return new HashSet<string>(seen.Select(b=>b.name));
        }
        [UnityTest] public IEnumerator DefaultsFooterAndSessionDraftSurviveMenuReturn()
        {
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(3));Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2000));Assert.That(ground.SetupBotCount,Is.Zero);
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-map"));
            Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
            Assert.That(Reachable(),Is.SupersetOf(new[]{"lobby-map","lobby-mode","lobby-time","lobby-target","roster-card-0","setup-previous","roster-add-bot","roster-add-human"}));
            yield return Capture("01-cold-lobby");
            yield return Click("lobby-time");yield return Press(GamepadButton.DpadRight);yield return Press(GamepadButton.East);
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(4));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-time"));
            yield return Click("setup-previous");EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);yield return Press(GamepadButton.South,second);
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(4));Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
            yield return Click("lobby-map");EventSystem.current.SetSelectedGameObject(B("lobby-map-"+WellArenaCatalog.Id).gameObject);yield return null;yield return Capture("02-map-picker");yield return Press(GamepadButton.East);
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id));
        }
        [UnityTest] public IEnumerator BotTransactionsRememberOnlySuccessfulAddAndBlockModalShortcuts()
        {
            yield return Click("lobby-mode");yield return Click("lobby-teams");
            yield return Press(GamepadButton.West);yield return Click("lobby-add-difficulty-2");
            Assert.That(ground.SetupBotCount,Is.Zero);yield return Press(GamepadButton.Start);yield return Press(GamepadButton.North,second);
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.Running,Is.False);
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);Assert.That(ground.SetupBotCount,Is.Zero);
            yield return Press(GamepadButton.West);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-difficulty-1"));
            yield return Click("lobby-add-difficulty-2");Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-team-"+(int)NativeTeam.TeamB));
            yield return Click("lobby-add-team-"+(int)NativeTeam.TeamB);Assert.That(ground.SetupBotCount,Is.EqualTo(1));
            yield return Press(GamepadButton.West);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-difficulty-2"));yield return Press(GamepadButton.East);
            Assert.That(Reachable(),Does.Contain("Начать — четыре игрока"));
            yield return Click("roster-card-1");yield return Capture("03-bot-editor");yield return Click("roster-done");
            while(ground.SetupBotCount<7)ground.AddBot();yield return null;yield return Capture("04-full-teams");
            ground.SetMatchMode(NativeMatchMode.Ffa);yield return null;yield return Capture("05-full-ffa");yield return Capture("06-full-ffa-720",1280,720);
        }
        [UnityTest] public IEnumerator JoiningIsAtomicProfilePickerAndDeviceBindingsArePreserved()
        {
            yield return Press(GamepadButton.North,second);Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-profile-none"));
            yield return Capture("07-profiles");yield return Press(GamepadButton.East);Assert.That(ground.LocalSeatCount,Is.EqualTo(1));
            yield return Click("roster-add-human");Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name=="lobby-device-"+pad.deviceId),Is.False);
            yield return Click("lobby-device-"+second.deviceId);yield return Click("lobby-create-profile");
            Assert.That(Field<InputField>("textEntryTarget").text,Is.Empty);yield return Capture("08-empty-name-keyboard");yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);yield return Click("lobby-profile-none");
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));var input=Field<SeatInputCoordinator>("input");var ids=Field<LocalIdentitySession>("identities");
            Assert.That(input.DeviceAt(0),Is.SameAs(pad));Assert.That(input.DeviceAt(1),Is.SameAs(second));
            string id0=ids.GuestAt(0).Id,id1=ids.GuestAt(1).Id;
            yield return Click("roster-card-0");yield return Capture("09-human-editor");yield return Click("roster-device");yield return Capture("10-two-screens");yield return Click("lobby-screen-1");
            Assert.That(input.DeviceAt(0),Is.SameAs(pad));Assert.That(ids.GuestAt(0).Id,Is.EqualTo(id0));Assert.That(ids.GuestAt(1).Id,Is.EqualTo(id1));Assert.That(Field<int[]>("lobbyPositions").Take(2),Is.EqualTo(new[]{1,0}));
            yield return Click("roster-done");ground.AddBot();yield return null;yield return Click("roster-card-2");yield return Click("roster-device");yield return Capture("11-three-screens");yield return Click("lobby-screen-0");
            Assert.That(ground.LocalSeatCount,Is.EqualTo(3));Assert.That(input.DeviceAt(1),Is.SameAs(second));Assert.That(Field<int[]>("lobbyPositions").Take(3).OrderBy(x=>x),Is.EqualTo(new[]{0,1,2}));
            Assert.That(Field<int[]>("lobbyPositions")[1],Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator BotIdentitySurvivesTransfersAndFullScreensProtectHumans()
        {
            var bots=Field<NativeBotSetup>("botSetup");var input=Field<SeatInputCoordinator>("input");
            var human=Field<LocalIdentitySession>("identities").GuestAt(0).Id;
            for(int i=0;i<3;i++)
            {
                ground.AddBot();var bot=bots.At(0);
                yield return Click("roster-card-"+ground.LocalSeatCount);yield return Click("roster-device");
                yield return Capture("screens-"+(i+2));yield return Click("lobby-screen-"+(i+1));
                Assert.That(bots.SeatBot(i+1,Field<NativeTeam[]>("teamAssignments")[i+1]),Is.EqualTo(bot));
                Assert.That(input.DeviceAt(i+1),Is.Null,"AI views own no input device");yield return Click("roster-done");
            }
            ground.AddBot();var incoming=bots.At(0);var outgoing=bots.SeatBot(1,Field<NativeTeam[]>("teamAssignments")[1]);
            yield return Click("roster-card-4");yield return Click("roster-device");
            Assert.That(B("lobby-screen-0").interactable,Is.False);yield return Capture("12-full-screen-block");
            Call("AssignLobbyScreen",4,0);Assert.That(ground.LocalSeatCount,Is.EqualTo(4));Assert.That(bots.At(0),Is.EqualTo(incoming));
            Assert.That(B("lobby-screen-1").navigation.selectOnDown,Is.Not.Null);
            yield return Click("lobby-screen-1");Assert.That(bots.At(0),Is.EqualTo(outgoing));
            Assert.That(bots.SeatBot(1,Field<NativeTeam[]>("teamAssignments")[1]),Is.EqualTo(incoming));
            yield return Click("roster-device");yield return Click("lobby-screen-off");
            Assert.That(bots.At(bots.Count-1),Is.EqualTo(incoming));Assert.That(ground.LocalSeatCount,Is.EqualTo(3));
            Assert.That(input.DeviceAt(0),Is.SameAs(pad));Assert.That(Field<LocalIdentitySession>("identities").GuestAt(0).Id,Is.EqualTo(human));
            yield return Click("roster-done");
            yield return Click("roster-card-0");yield return Click("roster-remove");
            while(ground.LocalSeatCount>1){yield return Click("roster-card-0");yield return Click("roster-device");yield return Click("lobby-screen-off");yield return Click("roster-done");}
            yield return Click("roster-card-0");yield return Click("roster-device");Assert.That(B("lobby-screen-off").interactable,Is.False);
        }
        [UnityTest] public IEnumerator LongProfilesCountOnlyCommittedChoicesAndReconnectRetainsSeat()
        {
            string path=Path.Combine(Path.GetTempPath(),"lobby-profiles-"+Guid.NewGuid()+".json");
            try
            {
                var catalog=new PlayerProfileCatalog(path);typeof(ProvingGround).GetField("playerProfiles",Private).SetValue(ground,catalog);
                var profiles=Enumerable.Range(0,12).Select(i=>catalog.Create(new string('Ж',22)+i.ToString("00"),.1f,true)).ToArray();
                catalog.RecordSelection(profiles[10].Id);catalog.RecordSelection(profiles[10].Id);
                yield return Press(GamepadButton.North,second);
                var choices=Field<List<Button>>("lobbyChoices");Assert.That(choices[0].name,Is.EqualTo("lobby-profile-none"));Assert.That(choices[1].name,Is.EqualTo("lobby-profile-"+profiles[10].Id));
                using(EditorUiCapture.FixedViewport(ground,1920,1080))
                {
                    Canvas.ForceUpdateCanvases();Assert.That(Field<ScrollRect>("lobbyScroll").viewport.rect.height,Is.GreaterThanOrEqualTo(8*56));
                    EventSystem.current.SetSelectedGameObject(choices.Last().gameObject);yield return null;MenuSelectorOverlayTests.AssertPointerTarget(choices.Last());
                    yield return Capture("13-long-profiles");
                }
                yield return Press(GamepadButton.East);Assert.That(profiles[10].SelectionCount,Is.EqualTo(2));
                yield return Press(GamepadButton.North,second);yield return Click("lobby-profile-"+profiles[10].Id);
                Assert.That(profiles[10].SelectionCount,Is.EqualTo(3));Assert.That(ground.LocalSeatCount,Is.EqualTo(2));
                yield return Click("roster-card-0");yield return Click("roster-identity");Assert.That(B("lobby-profile-"+profiles[10].Id).interactable,Is.False);
                yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);
                InputSystem.DisableDevice(second);yield return null;yield return null;
                Assert.That(B("Начать — четыре игрока").interactable,Is.False);Assert.That(ground.LocalSeatCount,Is.EqualTo(2));
                InputSystem.EnableDevice(second);yield return null;yield return null;
                Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(1),Is.SameAs(second));Assert.That(B("Начать — четыре игрока").interactable,Is.True);
                Assert.That(Field<LocalIdentitySession>("identities").ProfileId(1),Is.EqualTo(profiles[10].Id));
            }
            finally{if(File.Exists(path))File.Delete(path);}
        }
        [UnityTest] public IEnumerator NumericConfirmHoldUnevenTeamsAndResolutionEvidence()
        {
            yield return Click("lobby-target");yield return Press(GamepadButton.DpadRight);
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                MenuSelectorOverlayTests.AssertPointerTarget(B("lobby-minus"),true);yield return null;Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2000));
                MenuSelectorOverlayTests.AssertPointerTarget(B("lobby-plus"),true);yield return null;Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2500));
                yield return Capture("14-numeric-target");
            }
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));
            for(int i=0;i<8;i++)yield return null;
            Assert.That(Field<GameObject>("lobbyDialog").activeSelf,Is.False);Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2500));
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            yield return Click("lobby-target");
            for(int i=0;i<4;i++)yield return Press(GamepadButton.DpadLeft);
            Assert.That(ground.Configuration.TargetEnabled,Is.False,"Arrow editing can select the existing optional target state");
            yield return Press(GamepadButton.South);Assert.That(ground.Configuration.TargetEnabled,Is.False);
            yield return Click("lobby-target");Assert.That(Field<Text>("lobbyNumber").text,Is.EqualTo("Без цели"));
            yield return Press(GamepadButton.DpadRight);Assert.That(ground.Configuration.TargetEnabled,Is.True);
            Field<Slider>("lobbySlider").value=2500;yield return null;yield return Click("lobby-number-done");
            yield return Click("lobby-mode");yield return Click("lobby-teams");Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2500));
            for(int i=0;i<7;i++){ground.AddBot();ground.SetBotTeam(i,i==0?NativeTeam.TeamB:NativeTeam.TeamA);}yield return null;
            Assert.That(Reachable(),Is.SupersetOf(Enumerable.Range(0,8).Select(i=>"roster-card-"+i)));
            foreach(int i in Enumerable.Range(0,8)){EventSystem.current.SetSelectedGameObject(B("roster-card-"+i).gameObject);yield return null;}
            yield return Capture("15-uneven-teams");yield return Capture("16-uneven-teams-4k",3840,2160);
            var identity=Field<LocalIdentitySession>("identities").GuestAt(0).Id;yield return Press(GamepadButton.North);
            Assert.That(Field<NativeTeam[]>("teamAssignments")[0],Is.EqualTo(NativeTeam.TeamB));Assert.That(Field<LocalIdentitySession>("identities").GuestAt(0).Id,Is.EqualTo(identity));
        }
        [UnityTest] public IEnumerator ControllerFamilyGlyphsStayInsideActionsWithoutRebinding()
        {
            string[] layouts={"XInputController","DualShockGamepad","SwitchProControllerHID"};string[] confirms={"A","×","B"};
            for(int i=0;i<layouts.Length;i++)
            {
                var device=(Gamepad)InputSystem.AddDevice(layouts[i]);
                try
                {
                    EventSystem.current.SetSelectedGameObject(B("lobby-map").gameObject);
                    using(StateEvent.From(device,out var press)){device.dpad.WriteValueIntoEvent(Vector2.right,press);InputSystem.QueueEvent(press);}yield return null;yield return null;
                    using(StateEvent.From(device,out var release)){device.dpad.WriteValueIntoEvent(Vector2.zero,release);InputSystem.QueueEvent(release);}yield return null;
                    Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
                    var focused=EventSystem.current.currentSelectedGameObject.GetComponent<Button>();var hint=focused.GetComponent<MenuActionHint>();
                    hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo(confirms[i]));
                    yield return Capture("glyph-"+layouts[i]);
                }
                finally{InputSystem.RemoveDevice(device);}
            }
        }
    }
}
