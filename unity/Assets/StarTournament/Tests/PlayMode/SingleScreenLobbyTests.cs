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
    public sealed partial class SingleScreenLobbyTests
    {
        ProvingGround ground;Scene scene;Gamepad pad,second;Keyboard qaKeyboard;Mouse qaMouse;
        string ownedProfilePath;
        InputSettings.EditorInputBehaviorInPlayMode oldEditor;InputSettings.BackgroundBehavior oldBackground;
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,Private).GetValue(ground);
        object Call(string name,params object[] args)=>typeof(ProvingGround).GetMethod(name,Private).Invoke(ground,args);
        Button B(string name)=>ground.GetComponentsInChildren<Button>().SingleOrDefault(b=>b.name==name)??throw new InvalidOperationException("Missing active button: "+name+"; available: "+string.Join(",",ground.GetComponentsInChildren<Button>().Select(b=>b.name)));
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
            ownedProfilePath=Path.Combine(Path.GetTempPath(),"lobby-fixture-profiles-"+Guid.NewGuid()+".json");typeof(ProvingGround).GetField("playerProfiles",Private).SetValue(ground,new PlayerProfileCatalog(ownedProfilePath));
            yield return null;pad=(Gamepad)InputSystem.AddDevice("XInputController");second=(Gamepad)InputSystem.AddDevice("DualShockGamepad");yield return null;
            EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);yield return Press(GamepadButton.South);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(ground)typeof(ProvingGround).GetMethod("CancelHistoryLoading",Private).Invoke(ground,new object[]{true});
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            if(ownedProfilePath!=null&&File.Exists(ownedProfilePath))File.Delete(ownedProfilePath);
            if(qaKeyboard!=null&&qaKeyboard.added)InputSystem.RemoveDevice(qaKeyboard);
            if(qaMouse!=null&&qaMouse.added)InputSystem.RemoveDevice(qaMouse);
            InputSystem.RemoveDevice(pad);InputSystem.RemoveDevice(second);
            InputSystem.settings.editorInputBehaviorInPlayMode=oldEditor;InputSystem.settings.backgroundBehavior=oldBackground;
        }
        IEnumerator Capture(string name,int width=1920,int height=1080)
        {string dir=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_LOBBY_QA_DIR");if(string.IsNullOrEmpty(dir))yield break;Directory.CreateDirectory(dir);yield return new WaitForSecondsRealtime(ground.GetComponentsInChildren<Button>(true).Single(b=>b.name=="setup-previous").colors.fadeDuration);yield return EditorUiCapture.Capture(ground,Path.Combine(dir,name+".png"),width,height);}
        HashSet<string> Reachable()
        {
            var seen=new HashSet<Selectable>();var stack=new Stack<Selectable>();stack.Push(B("lobby-map"));
            while(stack.Count>0){var b=stack.Pop();if(b==null||!seen.Add(b))continue;var n=b.navigation;foreach(var next in new[]{n.selectOnUp,n.selectOnDown,n.selectOnLeft,n.selectOnRight})if(next!=null)stack.Push(next);}
            return new HashSet<string>(seen.Select(b=>b.name));
        }
        [UnityTest] public IEnumerator F05DisconnectedDeviceRemainsExplicit()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                InputSystem.DisableDevice(pad);yield return null;yield return null;
                try
                {
                    var card=B("roster-card-0");Assert.That(card.transform.Find("participant-detail").GetComponent<Text>().text,Is.EqualTo("Устройство отключено"));
                    Assert.That(card.transform.Find("participant-device-glyph").gameObject.activeSelf,Is.True);
                    Assert.That(Field<SeatInputCoordinator>("input").IsConnected(0),Is.False);
                    yield return Capture("74-disconnected-device");
                }
                finally{InputSystem.EnableDevice(pad);}
            }
        }
        [UnityTest] public IEnumerator EmptySlotsUseAtomicBotFlowAndRemainReachable()
        {
            Assert.That(ground.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("roster-empty-")),Is.EqualTo(7));
            Assert.That(Reachable(),Does.Contain("roster-empty-6"));
            var empty=B("roster-empty-0");EventSystem.current.SetSelectedGameObject(empty.gameObject);
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {MenuSelectorOverlayTests.AssertPointerTarget(empty,true);yield return null;yield return null;}
            Assert.That(ground.SetupBotCount,Is.Zero);yield return Press(GamepadButton.East);
            Assert.That(EventSystem.current.currentSelectedGameObject,Is.SameAs(empty.gameObject));
            yield return Press(GamepadButton.South);yield return Click("lobby-add-difficulty-1");Assert.That(ground.SetupBotCount,Is.EqualTo(1));
            yield return Click("lobby-mode");yield return Capture("17-empty-team-slots");
            EventSystem.current.SetSelectedGameObject(B("roster-empty-0").gameObject);yield return Press(GamepadButton.South);yield return Click("lobby-add-difficulty-2");
            Assert.That(ground.SetupBotCount,Is.EqualTo(1));yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);
            Assert.That(ground.SetupBotCount,Is.EqualTo(1));
            while(ground.SetupBotCount<7)ground.AddBot();yield return null;
            Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("roster-empty-")),Is.False);
        }
        [UnityTest] public IEnumerator DefaultsFooterAndSessionDraftSurviveMenuReturn()
        {
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(3));Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2000));Assert.That(ground.SetupBotCount,Is.Zero);
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-map"));
            Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
            Assert.That(Reachable(),Is.SupersetOf(new[]{"lobby-map","lobby-mode","lobby-time","lobby-target","roster-card-0","setup-previous","roster-add-bot","roster-add-human"}));
            yield return Capture("01-cold-lobby");
            EventSystem.current.SetSelectedGameObject(B("lobby-time").gameObject);yield return Press(GamepadButton.South);
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(4));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-time"));
            yield return Click("setup-previous");EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);
            yield return Press(GamepadButton.South,second);
            Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(4));Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
            yield return Click("lobby-map");EventSystem.current.SetSelectedGameObject(B("lobby-map-"+WellArenaCatalog.Id).gameObject);yield return null;yield return Capture("02-map-picker");yield return Press(GamepadButton.East);
            Assert.That(ground.SelectedMapId,Is.EqualTo(CombatBowlCatalog.Id));
        }
        [UnityTest] public IEnumerator CardsUseSharedPortraitsAndPictogramRowsWithoutTextDeviceOrScreenLabels()
        {
            var human=B("roster-card-0");var humanPortrait=human.transform.Find("portrait").GetComponent<Image>();
            Assert.That(humanPortrait.sprite,Is.Not.Null);Assert.That(humanPortrait.sprite.texture,Is.SameAs(Resources.Load<Texture2D>("LobbyPortraits/Human")));
            Assert.That(human.transform.Find("participant-device-glyph").gameObject.activeSelf,Is.True);
            Assert.That(human.transform.Find("participant-device-glyph").GetComponent<LobbyRosterGlyph>().mainTexture,Is.SameAs(Resources.Load<Texture2D>("LobbyIcons/GamepadSilhouette")),"Gamepad uses the exact approved SVG raster resource");
            Assert.That(human.transform.Find("participant-screen-slot").gameObject.activeSelf,Is.True);
            var slots=human.transform.Find("participant-screen-slot").GetComponentsInChildren<Image>(true);
            Assert.That(slots.Length,Is.EqualTo(4));Assert.That(slots[0].sprite,Is.SameAs(LobbyVisualAssets.ScreenCell(true)));
            Assert.That(slots.All(i=>((Color32)i.color).Equals(new Color32(93,220,225,255))),Is.True);
            Assert.That(slots.Skip(1).All(i=>i.sprite==LobbyVisualAssets.ScreenCell(false)),Is.True);
            var hollow=LobbyVisualAssets.ScreenCell(false).texture;var filled=LobbyVisualAssets.ScreenCell(true).texture;
            Assert.That(hollow.GetPixel(32,32).a,Is.Zero,"Unassigned cells have a hollow center");Assert.That(filled.GetPixel(32,32).a,Is.EqualTo(1),"Assigned cell is filled");
            Assert.That(hollow.GetPixel(2,32).a,Is.GreaterThan(.9f),"Hollow cell retains a visible stroke");
            var slotRects=slots.Select(i=>i.rectTransform).ToArray();Assert.That(slotRects.All(r=>Mathf.Abs(r.rect.width-r.rect.height)<.1f),Is.True,"Screen glyphs use square cells");
            Assert.That(Vector2.Distance(slotRects[0].anchoredPosition,slotRects[1].anchoredPosition),Is.GreaterThan(slotRects[0].rect.width),"Screen columns have a visible gap");
            Assert.That(Vector2.Distance(slotRects[0].anchoredPosition,slotRects[2].anchoredPosition),Is.GreaterThan(slotRects[0].rect.height),"Screen rows have a visible gap");
            Assert.That(human.transform.Find("participant-view"),Is.Null);Assert.That(human.GetComponentsInChildren<Text>(true).All(t=>!t.text.Contains("Экран")&&!t.text.Contains("Геймпад")&&!t.text.Contains("Клавиатура")),Is.True);
            yield return Click("roster-empty-0");yield return Click("lobby-add-difficulty-1");
            var bot=B("roster-card-1");var botPortrait=bot.transform.Find("portrait").GetComponent<Image>();
            Assert.That(botPortrait.sprite,Is.Not.SameAs(humanPortrait.sprite));Assert.That(botPortrait.sprite.texture,Is.SameAs(Resources.Load<Texture2D>("LobbyPortraits/Bot")));
            Assert.That(bot.transform.Find("participant-device-glyph").gameObject.activeSelf,Is.False);
            Assert.That(bot.transform.Find("participant-screen-slot").gameObject.activeSelf,Is.False);
            Assert.That(bot.GetComponentsInChildren<Text>(true).All(t=>!t.text.Contains("Экран")&&!t.text.Contains("Геймпад")&&!t.text.Contains("Клавиатура")),Is.True);
            typeof(ProvingGround).GetMethod("OpenRosterEditor",Private).Invoke(ground,new object[]{1});
            yield return Click(InlineControl("screen"));yield return Click(InlineControl("screen-choice")+"-1");yield return CloseCurrentInline();
            var promoted=B("roster-card-1").transform.Find("participant-screen-slot");Assert.That(promoted.gameObject.activeSelf,Is.True);
            var promotedSlots=promoted.GetComponentsInChildren<Image>(true);Assert.That(promotedSlots[0].sprite,Is.SameAs(LobbyVisualAssets.ScreenCell(false)));
            Assert.That(promotedSlots[1].sprite,Is.SameAs(LobbyVisualAssets.ScreenCell(true)),"Promoted AI seat shows its assigned screen position");
            yield return Click("roster-card-1");yield return Click(InlineControl("screen"));yield return Click(InlineControl("screen-off"));yield return CloseCurrentInline();
            Assert.That(B("roster-card-1").transform.Find("participant-screen-slot").gameObject.activeSelf,Is.False,"Demoted bot returns to no-screen roster state");
        }
        [UnityTest] public IEnumerator KeyboardMouseDeviceGetsTheSameSingleLinePictogramRow()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>();
                var input=Field<SeatInputCoordinator>("input");Assert.That(input.Replace(0,qaKeyboard),Is.True);yield return null;
                var glyph=B("roster-card-0").transform.Find("participant-device-glyph").GetComponent<LobbyRosterGlyph>();
                Assert.That(glyph.Kind,Is.EqualTo(LobbyRosterGlyph.DeviceKind.KeyboardMouse));
                yield return Capture("01-cold-lobby-keyboard-mouse");
            }
        }
        [UnityTest] public IEnumerator CaptureLobbyVisualReviewStatesAtFixedFullHd()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return Capture("01-cold-lobby-xinput");
                yield return Click("lobby-mode");
                EventSystem.current.SetSelectedGameObject(B("roster-card-0").gameObject);yield return null;yield return Capture("02-teams");
                while(ground.SetupBotCount<7)ground.AddBot();yield return null;
                Assert.That(B("roster-add-bot").interactable,Is.False);
                EventSystem.current.SetSelectedGameObject(B("roster-card-0").gameObject);yield return null;yield return Capture("03-full-teams-disabled");
                ground.SetMatchMode(NativeMatchMode.Ffa);yield return null;
                EventSystem.current.SetSelectedGameObject(B("roster-card-1").gameObject);yield return null;Assert.That(B("roster-card-1").GetComponent<MenuPresentation>().FocusVisible,Is.True);yield return Capture("04-full-ffa-disabled");
            }
        }
        [UnityTest] public IEnumerator SelectedMapArtworkFollowsPreviewAndConfirmation()
        {
            foreach(var id in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                var original=ground.SelectedMapId;
                yield return Click("lobby-map");
                EventSystem.current.SetSelectedGameObject(B("lobby-map-"+id).gameObject);yield return null;yield return null;
                var preview=ground.GetComponentsInChildren<ArenaPreviewGraphic>().Single(p=>p.name=="map-art"&&p.transform.parent.name=="lobby-map-"+id);
                Assert.That(ground.SelectedMapId,Is.EqualTo(original),"Focus previews without committing the map");
                Assert.That(preview.texture,Is.SameAs(Resources.Load<Texture2D>("MapMenuArt/"+id)),id);
                Assert.That(preview.texture,Is.Not.Null,id);Assert.That(preview.raycastTarget,Is.False);
                var rect=preview.rectTransform.rect;
                Assert.That(preview.texture.width*preview.uvRect.width/(preview.texture.height*preview.uvRect.height),Is.EqualTo(rect.width/rect.height).Within(.01f),"Artwork must not stretch");
                yield return Capture("map-picker-"+id);
                yield return Click("lobby-map-"+id);
                Assert.That(ground.SelectedMapId,Is.EqualTo(id));
                var compact=ground.GetComponentsInChildren<ArenaPreviewGraphic>().Single(p=>p.name=="lobby-map-preview");
                Assert.That(compact.texture,Is.SameAs(preview.texture),"Confirmed artwork also updates the compact card");
                yield return Capture("lobby-"+id);
            }
        }
        [UnityTest] public IEnumerator BotTransactionsRememberOnlySuccessfulAddAndBlockModalShortcuts()
        {
            yield return Click("lobby-mode");
            yield return Press(GamepadButton.West);yield return Click("lobby-add-difficulty-2");
            Assert.That(ground.SetupBotCount,Is.Zero);yield return Press(GamepadButton.Start);yield return Press(GamepadButton.North,second);
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.Running,Is.False);
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);Assert.That(ground.SetupBotCount,Is.Zero);
            yield return Press(GamepadButton.West);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-difficulty-1"));
            yield return Click("lobby-add-difficulty-2");Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-team-"+(int)NativeTeam.TeamB));
            yield return Click("lobby-add-team-"+(int)NativeTeam.TeamB);Assert.That(ground.SetupBotCount,Is.EqualTo(1));
            yield return Press(GamepadButton.DpadDown);yield return Press(GamepadButton.West);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-difficulty-2"));yield return Press(GamepadButton.East);
            Assert.That(Reachable(),Does.Contain("Начать — четыре игрока"));
            yield return Click("roster-card-1");yield return Capture("03-bot-editor");yield return CloseCurrentInline();
            while(ground.SetupBotCount<7)ground.AddBot();yield return null;
            Assert.That(B("roster-add-bot").interactable,Is.False);Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("roster-empty-")),Is.False);
            yield return Capture("04-full-teams-disabled");
            ground.SetMatchMode(NativeMatchMode.Ffa);yield return null;yield return Capture("05-full-ffa-disabled");yield return Capture("06-full-ffa-720",1280,720);
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
            yield return Click("roster-card-0");yield return Capture("09-human-editor");yield return Click(InlineControl("screen"));yield return Capture("10-two-screens");yield return Click(InlineControl("screen-choice")+"-1");
            Assert.That(input.DeviceAt(0),Is.SameAs(pad));Assert.That(ids.GuestAt(0).Id,Is.EqualTo(id0));Assert.That(ids.GuestAt(1).Id,Is.EqualTo(id1));Assert.That(Field<int[]>("lobbyPositions").Take(2),Is.EqualTo(new[]{1,0}));
            yield return CloseCurrentInline();ground.AddBot();yield return null;yield return Click("roster-card-2");yield return Click(InlineControl("screen"));yield return Capture("11-three-screens");yield return Click(InlineControl("screen-choice")+"-0");
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
                yield return Click("roster-card-"+ground.LocalSeatCount);yield return Click(InlineControl("screen"));
                yield return Capture("screens-"+(i+2));yield return Click(InlineControl("screen-choice")+"-"+(i+1));
                Assert.That(bots.SeatBot(i+1,Field<NativeTeam[]>("teamAssignments")[i+1]),Is.EqualTo(bot));
                Assert.That(input.DeviceAt(i+1),Is.Null,"AI views own no input device");
                Assert.That(B("roster-card-"+(i+1)).transform.Find("participant-device-glyph").gameObject.activeSelf,Is.False);
                yield return CloseCurrentInline();
                Assert.That(B("roster-card-"+(i+1)).transform.Find("participant-screen-slot").gameObject.activeSelf,Is.True);
            }
            ground.AddBot();var incoming=bots.At(0);var outgoing=bots.SeatBot(1,Field<NativeTeam[]>("teamAssignments")[1]);
            yield return Click("roster-card-4");yield return Click(InlineControl("screen"));
            Assert.That(B(InlineControl("screen-choice")+"-0").interactable,Is.False);yield return Capture("12-full-screen-block");
            Call("AssignLobbyScreen",4,0);Assert.That(ground.LocalSeatCount,Is.EqualTo(4));Assert.That(bots.At(0),Is.EqualTo(incoming));
            Assert.That(ground.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith(InlineControl("screen-choice"))),Is.EqualTo(4));
            var owner=(Gamepad)CurrentInlineEditor().GetType().GetField("Device").GetValue(CurrentInlineEditor());
            foreach(var step in new[]{new[]{(int)GamepadButton.DpadDown,3},new[]{(int)GamepadButton.DpadLeft,2},new[]{(int)GamepadButton.DpadUp,1}})
            {yield return Press((GamepadButton)step[0],owner);Assert.That(EditorField<Button>(owner,"Focus").name,Is.EqualTo(InlineControl("screen-choice")+"-"+step[1]),"All available screens are reachable through real Dpad input; human screen remains blocked");}
            yield return Press(GamepadButton.South,owner);Assert.That(bots.At(0),Is.EqualTo(outgoing));
            Assert.That(bots.SeatBot(1,Field<NativeTeam[]>("teamAssignments")[1]),Is.EqualTo(incoming));
            yield return Click(InlineControl("screen"));yield return Click(InlineControl("screen-off"));
            Assert.That(bots.At(bots.Count-1),Is.EqualTo(incoming));Assert.That(ground.LocalSeatCount,Is.EqualTo(3));
            Assert.That(input.DeviceAt(0),Is.SameAs(pad));Assert.That(Field<LocalIdentitySession>("identities").GuestAt(0).Id,Is.EqualTo(human));
            yield return CloseCurrentInline();
            yield return Click("roster-card-0");yield return Click("inline-remove-"+CurrentInlineParticipant());
            while(ground.LocalSeatCount>1){yield return Click("roster-card-0");yield return Click(InlineControl("screen"));yield return Click(InlineControl("screen-off"));yield return CloseCurrentInline();}
            yield return Click("roster-card-0");yield return Click(InlineControl("screen"));Assert.That(B(InlineControl("screen-off")).interactable,Is.False);
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
                yield return Click("roster-card-0");yield return Click(InlineControl("identity"));var busyButton=ground.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith(InlineControl("profile")+"-")&&b.GetComponentInChildren<Text>().text==profiles[10].Name);Assert.That(busyButton.interactable,Is.False);
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
            EventSystem.current.SetSelectedGameObject(B("lobby-target").gameObject);
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));
            for(int i=0;i<8;i++)yield return null;
            Assert.That(Field<GameObject>("lobbyDialog")==null||!Field<GameObject>("lobbyDialog").activeSelf,Is.True);
            Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2500),"Holding confirm advances once");
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080)){yield return Capture("14-inline-target");}
            yield return Click("lobby-mode");Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2500));
            for(int i=0;i<7;i++){ground.AddBot();ground.SetBotTeam(i,i==0?NativeTeam.TeamB:NativeTeam.TeamA);}yield return null;
            Assert.That(Reachable(),Is.SupersetOf(Enumerable.Range(0,8).Select(i=>"roster-card-"+i)));
            foreach(int i in Enumerable.Range(0,8)){EventSystem.current.SetSelectedGameObject(B("roster-card-"+i).gameObject);yield return null;}
            yield return Capture("15-uneven-teams");yield return Capture("16-uneven-teams-4k",3840,2160);
            var identity=Field<LocalIdentitySession>("identities").GuestAt(0).Id;yield return Press(GamepadButton.North);
            Assert.That(Field<NativeTeam[]>("teamAssignments")[0],Is.EqualTo(NativeTeam.TeamB));Assert.That(Field<LocalIdentitySession>("identities").GuestAt(0).Id,Is.EqualTo(identity));
        }
        [UnityTest] public IEnumerator GenericGamepadFallbackKeepsTextHintReadable()
        {
            var generic=InputSystem.AddDevice<Gamepad>();
            try
            {
                typeof(ProvingGround).GetField("menuDevice",Private).SetValue(ground,generic);
                EventSystem.current.SetSelectedGameObject(B("lobby-map").gameObject);yield return null;
                var hint=B("lobby-map").GetComponent<MenuActionHint>();hint.Refresh();
                Assert.That(hint.HideFocusedConfirm,Is.True);Assert.That(hint.GlyphLabel.enabled,Is.False,"Lobby hides redundant confirm even for generic pads");
                var shortcut=B("roster-add-bot").GetComponent<MenuActionHint>();shortcut.Refresh();
                Assert.That(shortcut.GlyphLabel.enabled,Is.True);Assert.That(shortcut.GlyphLabel.text,Is.EqualTo("Левая"));
                yield return Capture("01-cold-lobby-generic");
            }
            finally{InputSystem.RemoveDevice(generic);}
        }
        [UnityTest] public IEnumerator CaptureLobbyConceptV3RosterComposition()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>();yield return null;
                var input=Field<SeatInputCoordinator>("input");input.SetActiveSeatCount(2);
                Assert.That(input.Replace(0,qaKeyboard),Is.True);Assert.That(input.Assign(1,second),Is.True);
                var identities=Field<LocalIdentitySession>("identities");
                identities.ChooseGuest(0,qaKeyboard.deviceId,MouseSensitivityPreference.Resolve(ground.Profile),Field<FpsDisplay>("fps").Visible);
                identities.ChooseGuest(1,second.deviceId,MouseSensitivityPreference.Resolve(ground.Profile),Field<FpsDisplay>("fps").Visible);
                typeof(ProvingGround).GetMethod("ApplyLayout",Private).Invoke(ground,new object[]{2});
                ground.AddBot();ground.AddBot();yield return null;
                Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(ground.SetupBotCount,Is.EqualTo(2));
                Assert.That(Reachable(),Does.Contain("roster-empty-3"));
                EventSystem.current.SetSelectedGameObject(B("roster-card-2").gameObject);yield return null;
                Assert.That(B("roster-card-2").GetComponent<MenuPresentation>().FocusVisible,Is.True,"Focused bot card retains the gold frame");
                Assert.That(B("lobby-map").transform.Find("label").GetComponent<Text>().text,Is.EqualTo(AuthoredArenaCatalog.Name(ground.SelectedMapId)),"The real map title survives artwork/hint layer ordering");
                yield return Capture("concept-v3-keyboard-gamepad-two-bots");
                var generic=InputSystem.AddDevice<Gamepad>();
                try
                {
                    typeof(ProvingGround).GetField("menuDevice",Private).SetValue(ground,generic);typeof(ProvingGround).GetField("menuGamepadCursor",Private).SetValue(ground,true);
                    yield return null;Canvas.ForceUpdateCanvases();
                    var botCard=B("roster-card-2");var editBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(botCard.transform,botCard.transform.Find("participant-name").GetComponent<Text>().rectTransform);
                    var hintBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(botCard.transform,botCard.GetComponent<MenuActionHint>().GlyphLabel.rectTransform);
                    Assert.That(botCard.GetComponent<MenuActionHint>().GlyphLabel.enabled,Is.False,"The approved inline slice omits confirm hints even for generic gamepads");
                    var deviceRect=botCard.transform.Find("participant-device-glyph");Assert.That(deviceRect,Is.Not.Null,"The portrait/name/device structure remains available");
                    yield return Capture("concept-v3-generic-gamepad-two-bots");
                }
                finally{InputSystem.RemoveDevice(generic);}
            }
        }
        void PointerArrow(string rule,string arrow)
        {
            var graphic=B(rule).transform.Find(arrow).GetComponent<Text>();
            Canvas.ForceUpdateCanvases();var canvas=graphic.GetComponentInParent<Canvas>();
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(canvas.worldCamera,graphic.rectTransform.TransformPoint(graphic.rectTransform.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject,Is.SameAs(graphic.gameObject),"Arrow receives the actual topmost pointer hit");
            // ExecuteHierarchy uses the same first-handler boundary as the InputSystem UI module.
            var target=ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,data,ExecuteEvents.pointerClickHandler);
            Assert.That(target,Is.EqualTo(graphic.gameObject));Assert.That(graphic.GetComponent<Selectable>(),Is.Null);
        }
        [UnityTest] public IEnumerator F06InlineRuleBoundariesAndSingleActivation()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(3));Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2000));yield return Capture("01-defaults-3-2000");
                EventSystem.current.SetSelectedGameObject(B("lobby-time").gameObject);
                yield return Press(GamepadButton.South);Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(4));
                qaKeyboard=InputSystem.AddDevice<Keyboard>();yield return null;
                InputSystem.QueueStateEvent(qaKeyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
                InputSystem.QueueStateEvent(qaKeyboard,new KeyboardState());yield return null;
                Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(5),"Enter advances once");
                MenuSelectorOverlayTests.AssertPointerTarget(B("lobby-time"),true);yield return null;Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(6),"Body pointer advances once");
                for(int i=0;i<4;i++)yield return Click("lobby-time");
                Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(10));yield return Capture("02-time-10");
                yield return Click("lobby-time");Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(1));yield return Capture("03-time-1");
                PointerArrow("lobby-time","rule-left");Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(10));
                PointerArrow("lobby-time","rule-right");Assert.That(ground.Configuration.DurationMinutes,Is.EqualTo(1));
                for(int i=0;i<6;i++)yield return Click("lobby-target");
                Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(5000));yield return Capture("04-target-5000");
                yield return Click("lobby-target");Assert.That(ground.Configuration.TargetEnabled,Is.False);yield return Capture("05-target-off");
                PointerArrow("lobby-target","rule-right");Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(1000));Assert.That(ground.Configuration.TargetEnabled,Is.True);yield return Capture("06-target-1000");
                PointerArrow("lobby-target","rule-left");Assert.That(ground.Configuration.TargetEnabled,Is.False);
                PointerArrow("lobby-target","rule-left");Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(5000));Assert.That(ground.Configuration.TargetEnabled,Is.True);
                PointerArrow("lobby-target","rule-right");Assert.That(ground.Configuration.TargetEnabled,Is.False);
                yield return Click("lobby-target");Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(1000));
                EventSystem.current.SetSelectedGameObject(B("lobby-mode").gameObject);yield return Press(GamepadButton.South);Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Teams));
                PointerArrow("lobby-mode","rule-left");Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Ffa));
                PointerArrow("lobby-mode","rule-right");Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Teams));
                Assert.That(Field<GameObject>("lobbyDialog")==null||!Field<GameObject>("lobbyDialog").activeSelf,Is.True);
            }
        }
        [UnityTest] public IEnumerator F06ColorsFollowIdentityAndFreezeWithoutNewAddStep()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return Click("roster-add-bot");Assert.That(ground.SetupBotCount,Is.Zero);
                Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("inline-color-")),Is.False,"Initial add has no color step");yield return Capture("12-initial-add-difficulty");
                yield return Click("lobby-add-difficulty-1");Assert.That(ground.SetupBotCount,Is.EqualTo(1));
                var before=(Color)Call("LobbyColor",0);yield return Click("roster-card-1");
                var palette=NativeParticipantColors.Read(ground.ParticipantPaletteProfile);var used=Field<Dictionary<string,int>>("lobbyColors").Values.ToArray();var available=Enumerable.Range(0,8).First(n=>!used.Contains(n));
                var selected=palette[available];yield return Click(InlineControl("color")+"-"+available);
                Assert.That((Color)Call("LobbyColor",1),Is.EqualTo(selected));Assert.That((Color)Call("LobbyColor",0),Is.EqualTo(before));
                Assert.That(Enumerable.Range(0,8).Count(n=>B(InlineControl("color")+"-"+n).interactable),Is.EqualTo(8),"Occupied colors remain selectable for swaps");
                foreach(int n in Enumerable.Range(0,8)){var r=(RectTransform)B(InlineControl("color")+"-"+n).transform;Assert.That(Mathf.Abs(r.rect.width-r.rect.height),Is.LessThan(.1f),"Color choices are squares");}
                Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name=="roster-done"),Is.False,"Inline editors have no Done action");
                yield return Capture("09-existing-bot-color-swatches");
                yield return Click(InlineControl("screen"));yield return Click(InlineControl("screen-choice")+"-1");Assert.That((Color)Call("LobbyColor",1),Is.EqualTo(selected));
                yield return Click(InlineControl("screen"));yield return Click(InlineControl("screen-off"));Assert.That((Color)Call("LobbyColor",1),Is.EqualTo(selected));
                yield return CloseCurrentInline();
                var frozen=(NativeMatchComposition)Call("FreezeLobbyColors",ground.SetupComposition());Assert.That(frozen.Participant(1).Color,Is.EqualTo(selected));
                Assert.That(frozen.Read().Participants.Select(p=>p.Color).Distinct().Count(),Is.EqualTo(2));
                yield return Click("roster-card-0");Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("inline-color-")),Is.True);yield return Capture("10-existing-human-color-swatches");yield return CloseCurrentInline();
                yield return Click("lobby-mode");yield return Click("roster-card-1");Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("inline-color-")),Is.False);yield return CloseCurrentInline();
                Assert.That(frozen.Participant(1).Color,Is.EqualTo(selected),"Frozen snapshot detached from later draft/mode");
            }
        }
        void AssertAdjacentHintGeometry()
        {
            Canvas.ForceUpdateCanvases();
            foreach(var hint in ground.GetComponentsInChildren<MenuActionHint>().Where(h=>h.ColoredLobbyGlyph&&h.GlyphLabel&&h.GlyphLabel.enabled))
            {
                var caption=hint.CaptionOverride?hint.CaptionOverride:hint.transform.Find("label").GetComponent<Text>();
                var textBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(hint.transform,caption.rectTransform);
                var glyphBounds=RectTransformUtility.CalculateRelativeRectTransformBounds(hint.transform,hint.GlyphLabel.rectTransform);
                Debug.Log("F06 layout "+hint.name+" caption="+textBounds+" hint="+glyphBounds+" preferred="+caption.preferredWidth+" scale="+caption.pixelsPerUnit);
                Assert.That(glyphBounds.min.x-textBounds.max.x,Is.GreaterThanOrEqualTo(7),hint.name+" caption/hint gap");
                Assert.That(Mathf.Abs(glyphBounds.center.y-textBounds.center.y),Is.LessThan(1),hint.name+" caption/hint baseline region");
            }
        }
        [UnityTest] public IEnumerator F06CaptureChangedRosterStates()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>();yield return null;
                var input=Field<SeatInputCoordinator>("input");input.SetActiveSeatCount(2);Assert.That(input.Replace(0,qaKeyboard),Is.True);Assert.That(input.Assign(1,second),Is.True);
                var identities=Field<LocalIdentitySession>("identities");
                identities.ChooseGuest(0,qaKeyboard.deviceId,MouseSensitivityPreference.Resolve(ground.Profile),Field<FpsDisplay>("fps").Visible);identities.ChooseGuest(1,second.deviceId,MouseSensitivityPreference.Resolve(ground.Profile),Field<FpsDisplay>("fps").Visible);
                Call("ApplyLayout",2);ground.AddBot();ground.AddBot();yield return null;
                EventSystem.current.SetSelectedGameObject(B("roster-card-2").gameObject);yield return null;
                var colors=Enumerable.Range(0,4).Select(p=>(Color)Call("LobbyColor",p)).ToArray();Assert.That(colors.Distinct().Count(),Is.EqualTo(4));
                foreach(int p in Enumerable.Range(0,4))
                {
                    var card=B("roster-card-"+p);Assert.That(card.transform.Find("stripe"),Is.Null);Assert.That(card.transform.Find("open"),Is.Null);Assert.That(card.transform.Find("edit-context"),Is.Null);
                    Assert.That(((RectTransform)card.transform).rect.height,Is.GreaterThan(200));Assert.That(card.GetComponent<MenuPresentation>().SurfaceTint,Is.EqualTo(colors[p]));
                }
                AssertAdjacentHintGeometry();yield return Capture("07-ffa-keyboard-gamepad-two-bots");
                yield return Click("lobby-mode");EventSystem.current.SetSelectedGameObject(B("roster-card-2").gameObject);yield return null;Assert.That(B("roster-card-2").GetComponent<MenuPresentation>().FocusVisible,Is.True,"Focus survives reparenting into team grids");yield return Capture("08-teams-continuous");
                var generic=InputSystem.AddDevice<Gamepad>();
                try
                {
                    typeof(ProvingGround).GetField("menuDevice",Private).SetValue(ground,generic);typeof(ProvingGround).GetField("menuGamepadCursor",Private).SetValue(ground,true);
                    yield return Click("lobby-mode");yield return null;AssertAdjacentHintGeometry();yield return Capture("11-generic-ffa");
                    foreach(var hint in ground.GetComponentsInChildren<MenuActionHint>().Where(h=>h.GlyphLabel&&h.GlyphLabel.enabled))
                    {
                        var rect=(RectTransform)hint.transform;var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(hint.transform,hint.GlyphLabel.rectTransform);
                        Assert.That(bounds.min.x,Is.GreaterThanOrEqualTo(rect.rect.xMin));Assert.That(bounds.max.x,Is.LessThanOrEqualTo(rect.rect.xMax));
                    }
                }
                finally{InputSystem.RemoveDevice(generic);}
            }
        }
        [UnityTest] public IEnumerator F09FocusedLobbyHasOnlyShortcutsAndKeepsConfirmInput()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                ground.AddBot();yield return null;
                EventSystem.current.SetSelectedGameObject(B("lobby-mode").gameObject);yield return Press(GamepadButton.South);Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Teams));
                yield return Press(GamepadButton.South);Assert.That(ground.SetupMode,Is.EqualTo(NativeMatchMode.Ffa),"Confirm input remains bound while its glyph is hidden");
                foreach(string name in new[]{"lobby-map","lobby-mode","lobby-time","lobby-target","roster-card-0","roster-empty-0"})
                {
                    var button=B(name);EventSystem.current.SetSelectedGameObject(button.gameObject);yield return null;yield return null;
                    var hint=button.GetComponent<MenuActionHint>();hint.Refresh();Assert.That(hint.HideFocusedConfirm,Is.True);Assert.That(hint.GlyphLabel.enabled,Is.False);
                    Assert.That(button.GetComponent<MenuPresentation>().FocusVisible,Is.True,"Focus remains visible: "+button.name);
                    Assert.That(button.transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.False);
                }
                EventSystem.current.SetSelectedGameObject(B("lobby-mode").gameObject);yield return null;yield return Capture("01-upper-mode-no-A");
                foreach(var pair in new[]{new[]{"setup-previous","B"},new[]{"roster-add-bot","X"},new[]{"roster-add-human","Y"},new[]{"Начать — четыре игрока","≡"}})
                {
                    var button=B(pair[0]);Assert.That(button.interactable,Is.True);EventSystem.current.SetSelectedGameObject(button.gameObject);yield return null;yield return null;
                    var hint=button.GetComponent<MenuActionHint>();hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo(pair[1]));
                    Assert.That(button.transform.Find("gamepad-shortcut-hint").GetComponent<Text>().enabled,Is.False);
                    Assert.That(button.GetComponent<MenuPresentation>().FocusVisible,Is.True,"Focus remains visible: "+button.name);
                    if(pair[0]=="roster-add-bot")yield return Capture("02-footer-add-bot-X");
                    if(pair[0]=="Начать — четыре игрока")yield return Capture("03-footer-start-Menu");
                }
                yield return Click("roster-card-0");var editor=B(InlineControl("identity")).GetComponent<MenuActionHint>();
                Assert.That(editor.HideFocusedConfirm,Is.True,"Inline editors intentionally omit confirm hints");
                EventSystem.current.SetSelectedGameObject(editor.gameObject);yield return null;editor.Refresh();Assert.That(editor.GlyphLabel.enabled,Is.False);
            }
        }
        [UnityTest] public IEnumerator ControllerFamilyGlyphsStayInsideActionsWithoutRebinding()
        {
            string[] layouts={"XInputController","DualShockGamepad","SwitchProControllerHID"};
            var actions=new[]{MenuPadButton.East,MenuPadButton.West,MenuPadButton.North,MenuPadButton.Start};
            var expected=new[]{new[]{"B","X","Y","≡"},new[]{"○","□","△","≡"},new[]{"A","Y","X","≡"}};
            var colors=new[]{new Color32(216,61,66,255),new Color32(47,133,226,255),new Color32(238,190,45,255),new Color32(9,20,29,255)};
            for(int i=0;i<layouts.Length;i++)
            {
                var device=(Gamepad)InputSystem.AddDevice(layouts[i]);
                try
                {
                    EventSystem.current.SetSelectedGameObject(B("lobby-map").gameObject);
                    using(StateEvent.From(device,out var press)){device.dpad.WriteValueIntoEvent(Vector2.right,press);InputSystem.QueueEvent(press);}yield return null;yield return null;
                    using(StateEvent.From(device,out var release)){device.dpad.WriteValueIntoEvent(Vector2.zero,release);InputSystem.QueueEvent(release);}yield return null;
                    Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
                    var hint=EventSystem.current.currentSelectedGameObject.GetComponent<MenuActionHint>();
                    Assert.That(hint.HideFocusedConfirm,Is.True);hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);
                    Assert.That(hint.transform.Find("gamepad-action-button").GetComponent<Image>().enabled,Is.False);
                    hint.ShortcutActive=()=>true;
                    for(int n=0;n<actions.Length;n++)
                    {
                        hint.Shortcut=actions[n];hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.True);Assert.That(hint.GlyphLabel.text,Is.EqualTo(expected[i][n]));
                        Assert.That(hint.transform.Find("gamepad-shortcut-hint").GetComponent<Text>().enabled,Is.False);
                        var backing=hint.transform.Find("gamepad-action-button").GetComponent<Image>();Assert.That(backing.enabled,Is.True);Assert.That((Color32)backing.color,Is.EqualTo(colors[n]));
                    }
                    hint.Shortcut=MenuPadButton.South;hint.Refresh();Assert.That(hint.GlyphLabel.enabled,Is.False);
                    hint.Shortcut=null;hint.Refresh();yield return Capture("family-no-confirm-"+layouts[i]);
                }
                finally{InputSystem.RemoveDevice(device);}
            }
        }
    }
}
