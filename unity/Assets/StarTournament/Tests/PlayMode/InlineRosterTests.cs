using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed partial class SingleScreenLobbyTests
    {
        object CurrentInlineEditor()=>((System.Collections.IDictionary)Field<object>("inlineEditors")).Values.Cast<object>().Single(e=>e.GetType().GetField("Page").GetValue(e).ToString()!="Closed");
        int CurrentInlineParticipant()=>(int)Call("InlineParticipant",CurrentInlineEditor());
        string InlineControl(string kind)=>"inline-"+kind+"-"+CurrentInlineParticipant();
        IEnumerator CloseCurrentInline()
        {var e=CurrentInlineEditor();while(e.GetType().GetField("Page").GetValue(e).ToString()!="Closed"){Call("BackInlineEditor",e);yield return null;}}
        object EditorFor(Gamepad device)=>Call("InlineFor",device);
        T EditorField<T>(Gamepad device,string field)=>(T)EditorFor(device).GetType().GetField(field).GetValue(EditorFor(device));
        void EditorFocus(Gamepad device,string button)=>EditorFor(device).GetType().GetField("Focus").SetValue(EditorFor(device),B(button));
        IEnumerator OpenInline(Gamepad device,int participant)
        {
            // Keep the per-device cursor independent of the shared EventSystem, then use real submit.
            if((bool)Call("IsPrimaryMenuDevice",device))
            {
                EditorFor(device).GetType().GetField("LastSharedSelection").SetValue(EditorFor(device),EventSystem.current.currentSelectedGameObject);
                EditorFocus(device,"roster-card-"+participant);yield return Press(GamepadButton.South,device);
            }
            else {Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(participant),Is.SameAs(device));yield return Press(GamepadButton.North,device);}
            Assert.That(EditorField<string>(device,"Target"),Is.EqualTo(Call("LobbyColorIdentity",participant)));
            Assert.That(EditorField<object>(device,"Page").ToString(),Is.EqualTo("Fields"));
        }
        [UnityTest] public IEnumerator InterruptedShortXDoesNotOpenBotFlowAfterFocusOrDisconnect()
        {
            EditorFor(pad).GetType().GetField("LastSharedSelection").SetValue(EditorFor(pad),EventSystem.current.currentSelectedGameObject);EditorFocus(pad,"roster-card-0");
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));yield return null;yield return null;
            Assert.That(EditorField<bool>(pad,"WestPending"),Is.True);typeof(ProvingGround).GetField("menuWindowFocused",Private).SetValue(ground,false);yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;typeof(ProvingGround).GetField("menuWindowFocused",Private).SetValue(ground,true);yield return null;yield return null;
            Assert.That(Field<GameObject>("lobbyDialog")==null||!Field<GameObject>("lobbyDialog").activeSelf,Is.True);Assert.That(EditorField<bool>(pad,"WestPending"),Is.False);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));yield return null;yield return null;InputSystem.DisableDevice(pad);yield return null;
            Assert.That(EditorField<bool>(pad,"WestPending"),Is.False);InputSystem.EnableDevice(pad);InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
            Assert.That(Field<GameObject>("lobbyDialog")==null||!Field<GameObject>("lobbyDialog").activeSelf,Is.True);
        }
        [UnityTest] public IEnumerator PrimaryKeepsMainMenuAndSecondaryJoinUsesItsOwnDevice()
        {
            Call("ToMainMenu");yield return null;EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);
            yield return Press(GamepadButton.South,second);Assert.That(Field<object>("phase").ToString(),Is.EqualTo("MainMenu"));Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(B("main-action-0").gameObject));
            yield return Press(GamepadButton.South,pad);Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Setup"));
            yield return Press(GamepadButton.North,second);yield return Press(GamepadButton.South,second);
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(1),Is.SameAs(second));
        }
        [UnityTest] public IEnumerator ShortXOnParticipantAddsBotAndSecondaryCannotControlSharedLobby()
        {
            yield return Press(GamepadButton.North,second);yield return Click("lobby-profile-none");
            var primary=Field<SeatInputCoordinator>("input").DeviceAt(0);Assert.That(primary,Is.SameAs(pad));
            var selected=EventSystem.current.currentSelectedGameObject;var mode=ground.SetupMode;int humans=ground.LocalSeatCount;int bots=ground.SetupBotCount;
            EditorFocus(second,"lobby-mode");
            foreach(var button in new[]{GamepadButton.DpadRight,GamepadButton.South,GamepadButton.East,GamepadButton.West,GamepadButton.Start})yield return Press(button,second);
            Assert.That(ground.SetupMode,Is.EqualTo(mode));Assert.That(ground.LocalSeatCount,Is.EqualTo(humans));Assert.That(ground.SetupBotCount,Is.EqualTo(bots));Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(selected));
            Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Setup"));Assert.That(Field<GameObject>("lobbyDialog").activeSelf,Is.False);
            EditorFor(pad).GetType().GetField("LastSharedSelection").SetValue(EditorFor(pad),EventSystem.current.currentSelectedGameObject);EditorFocus(pad,"roster-card-0");
            yield return Press(GamepadButton.West,pad);
            Assert.That(Field<GameObject>("lobbyDialog").activeSelf,Is.True,"Short X on a participant must open bot creation");Assert.That(ground.LocalSeatCount,Is.EqualTo(humans));
            yield return Capture("ownership-short-x-bot-dialog");
        }
        [UnityTest] public IEnumerator YOpensOnlyOwnEditorAndPreservesOtherPlayersDraftAndTeam()
        {
            yield return Press(GamepadButton.North,second);yield return Click("lobby-profile-none");
            ground.SetMatchMode(NativeMatchMode.Teams);yield return null;var original=Field<NativeTeam[]>("teamAssignments")[1];
            yield return Press(GamepadButton.North,pad);Assert.That(EditorField<object>(pad,"Page").ToString(),Is.EqualTo("Fields"));
            EditorFocus(pad,"inline-identity-0");yield return Press(GamepadButton.South,pad);var first=EditorField<Button>(pad,"Focus").name;
            yield return Press(GamepadButton.North,second);
            Assert.That(EditorField<string>(second,"Target"),Is.EqualTo(Call("LobbyColorIdentity",1)));Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));Assert.That(Field<NativeTeam[]>("teamAssignments")[1],Is.EqualTo(original),"Y must not switch teams");
            Assert.That(EditorField<object>(pad,"Page").ToString(),Is.EqualTo("Identity"));Assert.That(EditorField<Button>(pad,"Focus").name,Is.EqualTo(first));
            yield return Capture("ownership-two-y-editors");
        }
        [UnityTest] public IEnumerator PrimaryKeyboardKeepsPadHintsHiddenDespiteSecondaryActivity()
        {
            qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>();var seats=Field<SeatInputCoordinator>("input");Assert.That(seats.Replace(0,qaKeyboard),Is.True);
            Field<LocalIdentitySession>("identities").ChooseGuest(0,qaKeyboard.deviceId,.1f,true);Call("RefreshInterface");yield return null;
            yield return Press(GamepadButton.DpadRight,second);yield return Press(GamepadButton.South,second);
            Assert.That(Field<bool>("menuGamepadCursor"),Is.False);Assert.That(Field<InputDevice>("menuDevice"),Is.SameAs(qaKeyboard));
            foreach(var hint in ground.GetComponentsInChildren<MenuActionHint>()){hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False,hint.name);}
            yield return Capture("ownership-keyboard-no-pad-hints");
        }
        [UnityTest] public IEnumerator InlineEditorsUseIndependentFocusDraftAndBack()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return Press(GamepadButton.North,second);yield return Click("lobby-profile-none");
                ground.AddBot();ground.AddBot();
                yield return OpenInline(pad,0);yield return OpenInline(second,1);
                EditorFocus(pad,"inline-identity-0");yield return Press(GamepadButton.South,pad);
                EditorFocus(second,"inline-screen-1");yield return Press(GamepadButton.South,second);
                var first=EditorField<Button>(pad,"Focus");var other=EditorField<Button>(second,"Focus");
                InputSystem.QueueStateEvent(second,new GamepadState().WithButton(GamepadButton.DpadRight));yield return null;yield return null;
                InputSystem.QueueStateEvent(second,new GamepadState());yield return null;
                Assert.That(EditorField<Button>(pad,"Focus"),Is.SameAs(first));Assert.That(EditorField<Button>(second,"Focus"),Is.Not.SameAs(other));
                yield return Capture("01-two-independent-editors");
                yield return Press(GamepadButton.East,second);
                Assert.That(EditorField<object>(pad,"Page").ToString(),Is.EqualTo("Identity"));Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));
                yield return Click("inline-create-0");yield return Click("inline-key-0-0");
                Assert.That(EditorField<string>(pad,"Draft"),Is.EqualTo("А"));
                EditorFocus(second,"inline-identity-1");yield return Press(GamepadButton.South,second);yield return Click("inline-create-1");yield return Click("inline-key-1-1");
                Assert.That(EditorField<string>(second,"Draft"),Is.EqualTo("Б"));Assert.That(EditorField<string>(pad,"Draft"),Is.EqualTo("А"));
                yield return Press(GamepadButton.East,second);Assert.That(EditorField<string>(pad,"Draft"),Is.EqualTo("А"));Assert.That(EditorField<object>(pad,"Page").ToString(),Is.EqualTo("Create"));
                // Creation from one device must not save/cancel the other's draft.
                yield return Click("inline-save-0");Assert.That(EditorField<object>(pad,"Page").ToString(),Is.EqualTo("Identity"));
                Assert.That(EditorField<string>(second,"Draft"),Is.EqualTo("Б"));
            }
        }
        IEnumerator KeyboardPress(UnityEngine.InputSystem.Key key)
        {InputSystem.QueueStateEvent(qaKeyboard,new KeyboardState(key));yield return null;yield return null;InputSystem.QueueStateEvent(qaKeyboard,new KeyboardState());yield return null;}
        [UnityTest] public IEnumerator InlineKeyboardMouseAndLongProfilesRemainIndependent()
        {
            string path=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"inline-profiles-"+Guid.NewGuid()+".json");
            try
            {
                using(EditorUiCapture.FixedViewport(ground,1920,1080))
                {
                    var catalog=new PlayerProfileCatalog(path);typeof(ProvingGround).GetField("playerProfiles",Private).SetValue(ground,catalog);
                    var profiles=Enumerable.Range(0,12).Select(i=>catalog.Create("Профиль "+(i+1),.1f,true)).ToArray();
                    qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>();
                    var input=Field<SeatInputCoordinator>("input");Assert.That(input.Replace(0,qaKeyboard),Is.True);
                    Field<LocalIdentitySession>("identities").ChooseGuest(0,qaKeyboard.deviceId,.1f,true);Call("RefreshInterface");yield return null;
                    yield return Press(GamepadButton.North,second);yield return Click("lobby-profile-none");
                    EventSystem.current.SetSelectedGameObject(B("lobby-map").gameObject);yield return null;
                    // End to end keyboard route: map -> participant -> profile list.
                    yield return KeyboardPress(UnityEngine.InputSystem.Key.DownArrow);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-0"));
                    yield return KeyboardPress(UnityEngine.InputSystem.Key.Enter);Assert.That(CurrentInlineParticipant(),Is.Zero);
                    yield return KeyboardPress(UnityEngine.InputSystem.Key.Enter);
                    var keyboardEditor=Call("InlineFor",qaKeyboard);var info=keyboardEditor.GetType();
                    Assert.That(info.GetField("Page").GetValue(keyboardEditor).ToString(),Is.EqualTo("Identity"));
                    var scroll=(ScrollRect)info.GetField("Scroll").GetValue(keyboardEditor);var first=ground.GetComponentsInChildren<Button>().Single(b=>b.name=="inline-profile-0-0");
                    Assert.That(scroll.viewport.rect.height/((RectTransform)first.transform).rect.height,Is.InRange(5f,5.5f),"Five visible profile rows");
                    // Secondary joins its own editor through Y without navigating the shared cursor.
                    yield return Press(GamepadButton.North,second);
                    Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));Assert.That(EditorField<string>(second,"Target"),Is.EqualTo(Call("LobbyColorIdentity",1)));
                    for(int i=0;i<12;i++)yield return KeyboardPress(UnityEngine.InputSystem.Key.DownArrow);
                    var last=(Button)info.GetField("Focus").GetValue(keyboardEditor);Assert.That(last.name,Is.EqualTo("inline-profile-0-12"));Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.1f));
                    MenuSelectorOverlayTests.AssertPointerTarget(last);yield return Capture("06-five-profile-rows-and-scroll");
                    string expectedProfile=catalog.ByUsage().Single(profile=>profile.Name==last.GetComponentInChildren<Text>().text).Id;string padFocus=EditorField<Button>(second,"Focus").name;yield return KeyboardPress(UnityEngine.InputSystem.Key.Enter);
                    Assert.That(Field<LocalIdentitySession>("identities").ProfileId(0),Is.EqualTo(expectedProfile));Assert.That(EditorField<Button>(second,"Focus").name,Is.EqualTo(padFocus),"Profile commit preserves the other cursor");
                    yield return KeyboardPress(UnityEngine.InputSystem.Key.Escape);Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));
                    yield return KeyboardPress(UnityEngine.InputSystem.Key.Escape);Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Setup"),"A device without its own editor cannot back out of other editors");Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));
                    yield return Press(GamepadButton.DpadDown,second);
                    // A pointer card click owns the keyboard/mouse context even after pad activity.
                    var card=B("roster-card-0");var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                    ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.pointerDownHandler);ExecuteEvents.Execute(card.gameObject,data,ExecuteEvents.pointerClickHandler);yield return null;
                    Assert.That(info.GetField("Page").GetValue(keyboardEditor).ToString(),Is.EqualTo("Fields"));Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));
                }
            }
            finally{if(System.IO.File.Exists(path))System.IO.File.Delete(path);}
        }
        [UnityTest] public IEnumerator InlineProfileFocusSurvivesReorderAndConcurrentClaim()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                var catalog=Field<PlayerProfileCatalog>("playerProfiles");var profiles=Enumerable.Range(0,12).Select(i=>catalog.Create("A"+i.ToString("00"),.1f,true)).ToArray();
                yield return Press(GamepadButton.North,second);yield return Click("lobby-profile-none");
                yield return OpenInline(pad,0);yield return OpenInline(second,1);
                EditorFocus(pad,"inline-identity-0");yield return Press(GamepadButton.South,pad);
                EditorFocus(second,"inline-identity-1");yield return Press(GamepadButton.South,second);
                Func<int,string,Button> choice=(participant,name)=>ground.GetComponentsInChildren<Button>().Single(button=>button.name.StartsWith("inline-profile-"+participant+"-")&&button.GetComponentInChildren<Text>().text==name);
                EditorFocus(second,choice(1,profiles[6].Name).name);EditorField<ScrollRect>(second,"Scroll").verticalNormalizedPosition=.45f;
                EditorFocus(pad,choice(0,profiles[11].Name).name);yield return Press(GamepadButton.South,pad);
                Assert.That(EditorField<string>(second,"FocusKey"),Is.EqualTo("profile:"+profiles[6].Id),"Usage reorder preserves the other unconfirmed identity");
                Assert.That(EditorField<Button>(second,"Focus").GetComponentInChildren<Text>().text,Is.EqualTo(profiles[6].Name));Assert.That(EditorField<ScrollRect>(second,"Scroll").verticalNormalizedPosition,Is.EqualTo(.45f).Within(.01f));
                EditorFocus(pad,"inline-identity-0");yield return Press(GamepadButton.South,pad);
                EditorFocus(pad,choice(0,profiles[8].Name).name);EditorFocus(second,choice(1,profiles[8].Name).name);
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.QueueStateEvent(second,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
                InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.QueueStateEvent(second,new GamepadState());yield return null;
                var identities=Field<LocalIdentitySession>("identities");Assert.That(identities.ProfileId(0),Is.EqualTo(profiles[8].Id));Assert.That(identities.ProfileId(1),Is.Null,"A stale busy selection cannot confirm a different profile");
                Assert.That(EditorField<string>(second,"FocusKey"),Is.EqualTo("profile:"+profiles[8].Id));Assert.That(EditorField<Button>(second,"Focus").interactable,Is.False);
                EditorFocus(pad,"inline-identity-0");yield return Press(GamepadButton.South,pad);yield return Click("inline-create-0");yield return Click("inline-key-0-0");yield return Click("inline-save-0");
                Assert.That(EditorField<string>(second,"FocusKey"),Is.EqualTo("profile:"+profiles[8].Id),"Creation reconciles other lists without replacing their disabled selection");Assert.That(identities.ProfileId(1),Is.Null);
            }
        }
        [UnityTest] public IEnumerator InlineRemovalKeepsNearestFocusAndLatchesUntilRelease()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                ground.AddBot();ground.AddBot();yield return null;
                string nearestIdentity=(string)Call("LobbyColorIdentity",2);int count=ground.SetupBotCount;
                EventSystem.current.SetSelectedGameObject(B("roster-card-1").gameObject);yield return null;yield return null;
                float threshold=ground.LobbyProfile.Get("lobby.removeHoldSeconds");
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));yield return new WaitForSecondsRealtime(threshold+.1f);
                Assert.That(ground.SetupBotCount,Is.EqualTo(count-1));Assert.That(EditorField<Button>(pad,"Focus"),Is.SameAs(B("roster-card-1")));
                Assert.That((string)Call("LobbyColorIdentity",1),Is.EqualTo(nearestIdentity),"Focus follows the nearest surviving identity");
                Assert.That(EditorField<bool>(pad,"RemovalLatched"),Is.True);
                yield return new WaitForSecondsRealtime(threshold+.2f);
                Assert.That(ground.SetupBotCount,Is.EqualTo(count-1),"Continued hold must not remove the next card");Assert.That(EditorField<float>(pad,"Hold"),Is.Zero);
                yield return Capture("08-removal-latched-nearest");
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
                Assert.That(EditorField<bool>(pad,"RemovalLatched"),Is.False);Assert.That(EditorField<Button>(pad,"Focus"),Is.SameAs(B("roster-card-1")));
                InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));yield return new WaitForSecondsRealtime(threshold+.1f);
                TestContext.WriteLine($"second-hold: hold={EditorField<float>(pad,"Hold")} latched={EditorField<bool>(pad,"RemovalLatched")} pending={EditorField<bool>(pad,"WestPending")} focus={EditorField<Button>(pad,"Focus")?.name} windowFocused={Field<bool>("menuWindowFocused")} pressed={pad.buttonWest.isPressed}");
                Assert.That(ground.SetupBotCount,Is.EqualTo(count-2),"A new hold after release may remove one more participant");Assert.That(EditorField<Button>(pad,"Focus"),Is.SameAs(B("roster-card-0")));
                InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;Assert.That(EditorField<bool>(pad,"RemovalLatched"),Is.False);
            }
        }
        [UnityTest] public IEnumerator InlineFourDevicesColorsHoldCancelAndMapGrid()
        {
            var third=(Gamepad)InputSystem.AddDevice("XInputController");var fourth=(Gamepad)InputSystem.AddDevice("DualShockGamepad");
            try
            {
                using(EditorUiCapture.FixedViewport(ground,1920,1080))
                {
                    foreach(var device in new[]{second,third,fourth}){yield return Press(GamepadButton.North,device);yield return Click("lobby-profile-none");}
                    ground.AddBot();ground.AddBot();ground.AddBot();ground.AddBot();
                    var devices=new[]{pad,second,third,fourth};for(int i=0;i<4;i++)yield return OpenInline(devices[i],i);
                    Assert.That(devices.Select(d=>EditorField<GameObject>(d,"Root")).Distinct().Count(),Is.EqualTo(4));
                    var first=EditorField<Button>(pad,"Focus");EditorFocus(fourth,"inline-screen-3");yield return Press(GamepadButton.South,fourth);
                    Assert.That(EditorField<Button>(pad,"Focus"),Is.SameAs(first));yield return Capture("02-four-independent-editors");
                    var colors=Field<Dictionary<string,int>>("lobbyColors");string a=(string)Call("LobbyColorIdentity",0),b=(string)Call("LobbyColorIdentity",1);int ca=colors[a],cb=colors[b];
                    yield return Click("inline-color-0-"+cb);Assert.That(colors[a],Is.EqualTo(cb));Assert.That(colors[b],Is.EqualTo(ca));Assert.That(colors.Values.Distinct().Count(),Is.EqualTo(8));
                    // Both submits occur in one input frame against occupied colors.
                    EditorFocus(pad,"inline-color-0-"+ca);EditorFocus(second,"inline-color-1-"+cb);
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));InputSystem.QueueStateEvent(second,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
                    InputSystem.QueueStateEvent(pad,new GamepadState());InputSystem.QueueStateEvent(second,new GamepadState());yield return null;
                    Assert.That(colors.Values.Distinct().Count(),Is.EqualTo(8));
                    for(int i=0;i<4;i++){while(EditorField<object>(devices[i],"Page").ToString()!="Closed")yield return Press(GamepadButton.East,devices[i]);}
                    // A short release cancels removal; a full roster cannot start add flow.
                    // Seed the closed cursors through real shared selection. A raw
                    // Focus field write can be superseded by the final editor close.
                    EventSystem.current.SetSelectedGameObject(B("roster-card-7").gameObject);yield return null;yield return null;
                    Assert.That(EditorField<Button>(pad,"Focus"),Is.SameAs(B("roster-card-7")));int count=ground.SetupBotCount;
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));yield return new WaitForSecondsRealtime(.2f);
                    Assert.That(EditorField<float>(pad,"Hold"),Is.GreaterThan(0));yield return Capture("03-removal-hold");
                    InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;yield return null;
                    Assert.That(ground.SetupBotCount,Is.EqualTo(count));Assert.That(EditorField<float>(pad,"Hold"),Is.Zero);Assert.That(Field<GameObject>("lobbyDialog")?.activeSelf??false,Is.False);
                    yield return Capture("04-removal-cancelled");
                    InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.West));yield return new WaitForSecondsRealtime(ground.LobbyProfile.Get("lobby.removeHoldSeconds")+.1f);
                    InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;Assert.That(ground.SetupBotCount,Is.EqualTo(count-1));
                    Assert.That(ground.LocalSeatCount,Is.EqualTo(4),"The selected bot is removed; all four humans remain");
                    yield return Click("lobby-map");var mapButtons=ground.GetComponentsInChildren<Button>().Where(x=>x.name.StartsWith("lobby-map-")).ToArray();Assert.That(mapButtons.Length,Is.EqualTo(5));Assert.That(mapButtons.Select(b=>b.name.Substring("lobby-map-".Length)),Is.EquivalentTo(new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id,FiringRangeCatalog.Id}));
                    Assert.That(mapButtons.Select(x=>x.GetComponent<RectTransform>().anchorMin.x).Distinct().Count(),Is.EqualTo(2));Assert.That(mapButtons.Select(x=>x.GetComponent<RectTransform>().anchorMin.y).Distinct().Count(),Is.EqualTo(3));
                    foreach(var map in mapButtons)Assert.That(map.GetComponent<MenuActionHint>().HideFocusedConfirm,Is.True);
                    yield return Capture("05-map-grid");yield return Click("lobby-map-"+WellArenaCatalog.Id);Assert.That(ground.SelectedMapId,Is.EqualTo(WellArenaCatalog.Id));
                }
            }
            finally{InputSystem.RemoveDevice(third);InputSystem.RemoveDevice(fourth);}
        }
    }
}
