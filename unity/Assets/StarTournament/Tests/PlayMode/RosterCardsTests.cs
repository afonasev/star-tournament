using System.Collections;
using System.Linq;
using System.IO;
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
    public sealed class RosterCardsTests
    {
        Scene scene;ProvingGround ground;Gamepad pad,secondPad;Joystick unsupported;string profilesPath;
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        void Click(string name){Assert.That(B(name).interactable,Is.True,name);B(name).onClick.Invoke();}
        IEnumerator Load()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();pad=InputSystem.AddDevice<Gamepad>();
            Click("main-action-0");NativeSetupFixture.UnboundHumans(ground);Click("setup-next");Click("setup-next");
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(secondPad!=null&&secondPad.added)InputSystem.RemoveDevice(secondPad);if(unsupported!=null&&unsupported.added)InputSystem.RemoveDevice(unsupported);if(profilesPath!=null&&File.Exists(profilesPath))File.Delete(profilesPath);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        IEnumerator Press(GamepadButton b)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(b));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        [UnityTest] public IEnumerator RosterYJoinsThenSwitchesOwnTeamAndXAddsInheritedBot()
        {
            yield return Load();ground.SetMatchMode(NativeMatchMode.Teams);
            var input=Field<SeatInputCoordinator>("input");input.Reset();
            yield return Press(GamepadButton.North);
            int seat=Enumerable.Range(0,ground.LocalSeatCount).Single(i=>input.DeviceAt(i)==pad);
            Assert.That(Field<int>("rosterEditing"),Is.EqualTo(-1));
            var team=ground.SetupComposition().Roster.Read().Teams[seat];
            yield return Press(GamepadButton.North);
            Assert.That(ground.SetupComposition().Roster.Read().Teams[seat],Is.Not.EqualTo(team));
            ground.AddBot();ground.SetBotDifficulty(0,0);int before=ground.SetupComposition().ParticipantCount;
            yield return Press(GamepadButton.West);Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(before+1));
            Assert.That(ground.SetupComposition().Participant(before).Difficulty,Is.EqualTo(0));
            ground.SetBotDifficulty(0,2);before=ground.SetupComposition().ParticipantCount;
            yield return Press(GamepadButton.West);Assert.That(ground.SetupComposition().Participant(before).Difficulty,Is.EqualTo(2));
            Assert.That(Field<int>("rosterEditing"),Is.EqualTo(-1));
        }
        [UnityTest] public IEnumerator NewHumanAndBotsUseActualSmallerTeamAfterManualMoves()
        {
            yield return Load();NativeSetupFixture.UnboundHumans(ground,3);ground.SetMatchMode(NativeMatchMode.Teams);
            for(int i=0;i<3;i++)ground.SetTeam(i,NativeTeam.TeamA);
            Click("roster-add-human");
            Assert.That(ground.SetupComposition().Roster.Read().Teams[3],Is.EqualTo(NativeTeam.TeamB));
            Click("roster-done");ground.AddBot();
            Assert.That(ground.SetupComposition().Roster.Read().Teams[4],Is.EqualTo(NativeTeam.TeamB));
            ground.SetBotTeam(0,NativeTeam.TeamA);ground.SetSeatAi(1,true);ground.AddBot();
            Assert.That(ground.SetupComposition().Roster.Read().Teams[5],Is.EqualTo(NativeTeam.TeamB),"Bots with a view count toward their team");
            ground.SetBotTeam(0,NativeTeam.TeamB);ground.AddBot();
            Assert.That(ground.SetupComposition().Roster.Read().Teams[6],Is.EqualTo(NativeTeam.TeamA),"A tie chooses TeamA");
            ground.RemoveBot(0);ground.AddBot();
            Assert.That(ground.SetupComposition().Roster.Read().Teams[6],Is.EqualTo(NativeTeam.TeamB));
        }
        [UnityTest] public IEnumerator NewYJoinBalancesAndRepeatedYOrReconnectPreservesExplicitChoice()
        {
            yield return Load();NativeSetupFixture.UnboundHumans(ground,1);ground.SetMatchMode(NativeMatchMode.Teams);
            var input=Field<SeatInputCoordinator>("input");Assert.That(input.Assign(0,pad),Is.True);
            ground.AddBot();ground.SetBotTeam(0,NativeTeam.TeamA);ground.AddBot();ground.SetBotTeam(1,NativeTeam.TeamB);
            secondPad=InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(secondPad,new GamepadState().WithButton(GamepadButton.North));yield return null;yield return null;
            InputSystem.QueueStateEvent(secondPad,new GamepadState());yield return null;
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(input.DeviceAt(1),Is.SameAs(secondPad));
            Assert.That(ground.SetupComposition().Roster.Read().Teams[1],Is.EqualTo(NativeTeam.TeamB));
            InputSystem.QueueStateEvent(secondPad,new GamepadState().WithButton(GamepadButton.North));yield return null;yield return null;
            InputSystem.QueueStateEvent(secondPad,new GamepadState());yield return null;
            Assert.That(ground.SetupComposition().Roster.Read().Teams[1],Is.EqualTo(NativeTeam.TeamA));
            InputSystem.RemoveDevice(secondPad);secondPad=InputSystem.AddDevice<Gamepad>();
            InputSystem.QueueStateEvent(secondPad,new GamepadState().WithButton(GamepadButton.North));yield return null;yield return null;
            InputSystem.QueueStateEvent(secondPad,new GamepadState());yield return null;
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(input.DeviceAt(1),Is.SameAs(secondPad));
            Assert.That(ground.SetupComposition().Roster.Read().Teams[1],Is.EqualTo(NativeTeam.TeamA));
        }
        [UnityTest] public IEnumerator CompactProfilesShowEightRowsAndScrollToGuestWithoutChoosingBusyProfile()
        {
            yield return Load();profilesPath=Path.Combine(Application.temporaryCachePath,"compact-roster-profiles-"+System.Guid.NewGuid()+".json");
            var profiles=new PlayerProfileCatalog(profilesPath);for(int i=0;i<10;i++)profiles.Create("Профиль "+(i+1),.2f,false);
            typeof(ProvingGround).GetField("playerProfiles",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,profiles);
            Field<LocalIdentitySession>("identities").ChooseProfile(1,-101,profiles.Profiles[0].Id,profiles);
            Click("roster-card-0");Click("roster-identity");yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(B("roster-choice-profile-"+profiles.Profiles[0].Id).interactable,Is.False);
            var scroll=Field<ScrollRect>("rosterChoiceScroll");var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport,(RectTransform)B("roster-choice-profile-"+profiles.Profiles[7].Id).transform);
            Assert.That(scroll.viewport.rect.Contains(new Vector2(bounds.center.x,bounds.min.y)),Is.True,"Eighth profile fits in the viewport");
            string directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SETUP_QA_DIR");
            if(!string.IsNullOrEmpty(directory)){Directory.CreateDirectory(directory);yield return EditorUiCapture.Capture(ground,Path.Combine(directory,"compact-profiles.png"));}
            EventSystem.current.SetSelectedGameObject(B("roster-choice-guest").gameObject);yield return null;
            Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(1));
            Click("roster-choice-guest");Assert.That(Field<LocalIdentitySession>("identities").GuestAt(0),Is.Not.Null);
            Click("roster-identity");Click("roster-choice-profile-"+profiles.Profiles[9].Id);
            Assert.That(Field<LocalIdentitySession>("identities").ProfileId(0),Is.EqualTo(profiles.Profiles[9].Id));
        }
        [UnityTest] public IEnumerator BotViewRoundtripPreservesRosterDifficultyTeamAndFocus()
        {
            yield return Load();Click("roster-card-3");Click("roster-remove");
            Click("roster-add-bot");Click("roster-identity");Click("roster-choice-difficulty-2");ground.GetComponentsInChildren<Toggle>().Single(t=>t.name=="roster-bot-view").isOn=true;Click("roster-done");
            Assert.That(ground.LocalSeatCount,Is.EqualTo(4));Assert.That(ground.SetupBotCount,Is.EqualTo(0));
            Assert.That(ground.SetupComposition().Participant(3).Difficulty,Is.EqualTo(2));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-3"));
            Click("roster-card-3");ground.GetComponentsInChildren<Toggle>().Single(t=>t.name=="roster-bot-view").isOn=false;Click("roster-done");
            Assert.That(ground.LocalSeatCount,Is.EqualTo(3));Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(4));Assert.That(ground.SetupComposition().Participant(3).Difficulty,Is.EqualTo(2));
            var view=B("roster-card-3").GetComponentsInChildren<Text>(true).Single(t=>t.name=="participant-view");Assert.That(view.gameObject.activeSelf,Is.False);
            Click("setup-previous");Click("setup-previous");Click("setup-step-2");Assert.That(ground.SetupComposition().Participant(3).Difficulty,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator GenericJoystickAppearsInPickerButCannotBeAssigned()
        {
            yield return Load();unsupported=InputSystem.AddDevice<Joystick>();
            Click("roster-card-0");Click("roster-device");
            var choice=B("roster-choice-device-"+unsupported.deviceId);
            Assert.That(choice.interactable,Is.False);
            StringAssert.Contains("XInput",choice.GetComponentInChildren<Text>().text);
            Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.Not.SameAs(unsupported));
        }
        [UnityTest] public IEnumerator GamepadCanEditDifficultyCancelAndReturnToCard()
        {
            yield return Load();Click("roster-add-bot");Click("roster-done");yield return null;
            Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-4"));
            yield return Press(GamepadButton.South);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-identity"));
            yield return Press(GamepadButton.South);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-choice-difficulty-0"));
            yield return Press(GamepadButton.DpadDown);yield return Press(GamepadButton.DpadDown);yield return Press(GamepadButton.South);
            Assert.That(ground.SetupComposition().Participant(4).Difficulty,Is.EqualTo(2));
            yield return Press(GamepadButton.East);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-4"));
            var frame=B("roster-card-4").transform.Find("gold-focus");Assert.That(frame.gameObject.activeSelf,Is.True);
        }
        [UnityTest] public IEnumerator YRestoresProfileWhileEditorRemainsOnSameBot()
        {
            yield return Load();
            profilesPath=Path.Combine(Path.GetTempPath(),System.Guid.NewGuid()+"-roster-profiles.json");
            var catalog=new PlayerProfileCatalog(profilesPath);var profile=catalog.Create("Тест",.3f,true);
            typeof(ProvingGround).GetField("playerProfiles",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,catalog);
            Click("roster-card-3");Click("roster-remove");Click("roster-card-2");Click("roster-remove");
            Click("roster-card-0");Click("roster-device");Click("roster-choice-device-"+pad.deviceId);
            Click("roster-identity");Click("roster-choice-profile-"+profile.Id);Click("roster-done");
            secondPad=InputSystem.AddDevice<Gamepad>();
            Click("roster-card-1");Click("roster-device");Click("roster-choice-device-"+secondPad.deviceId);
            Click("roster-identity");Click("roster-choice-guest");Click("roster-done");
            Click("roster-card-0");Click("roster-remove");
            Click("roster-add-bot");Click("roster-done");Click("roster-add-bot");
            Click("roster-identity");Click("roster-choice-difficulty-2");Click("roster-done");
            Click("roster-card-2");Click("roster-identity");yield return Press(GamepadButton.North);
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(4));
            Assert.That(Field<int>("rosterEditing"),Is.EqualTo(3));Assert.That(Field<GameObject>("rosterPicker").activeSelf,Is.False);
            Assert.That(EventSystem.current.currentSelectedGameObject.transform.IsChildOf(Field<GameObject>("rosterEditor").transform),Is.True,"Joining must not steal focus from the open editor");
            Assert.That(Field<LocalIdentitySession>("identities").ProfileId(1),Is.EqualTo(profile.Id));
            Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(1),Is.SameAs(pad));
            Assert.That(ground.SetupComposition().Participant(3).Difficulty,Is.EqualTo(2));
            Click("roster-done");Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-3"));
        }
        [UnityTest] public IEnumerator FullUnequalTeamsAllowExplicitTransferAndBlockFurtherAdds()
        {
            yield return Load();ground.SetMatchMode(NativeMatchMode.Teams);
            for(int i=0;i<4;i++){Click("roster-add-bot");Click("roster-done");}
            Assert.That(B("roster-add-human").interactable,Is.False);Assert.That(B("roster-add-bot").interactable,Is.False);
            for(int p=0;p<4;p++)ground.SetTeam(p,p==0?NativeTeam.TeamB:NativeTeam.TeamA);
            for(int p=0;p<4;p++)ground.SetBotTeam(p,NativeTeam.TeamA);
            Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(8));
            Click("roster-card-7");Click("roster-team");Click("roster-choice-team-"+(int)NativeTeam.TeamB);Click("roster-done");
            Assert.That(ground.SetupComposition().Roster.Read().Teams[7],Is.EqualTo(NativeTeam.TeamB));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-7"));
        }
    }
}
