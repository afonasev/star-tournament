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
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();pad=InputSystem.AddDevice<Gamepad>();
            Click("main-action-0");NativeSetupFixture.UnboundHumans(ground);Click("setup-next");Click("setup-next");
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(secondPad!=null&&secondPad.added)InputSystem.RemoveDevice(secondPad);if(unsupported!=null&&unsupported.added)InputSystem.RemoveDevice(unsupported);if(profilesPath!=null&&File.Exists(profilesPath))File.Delete(profilesPath);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        IEnumerator Press(GamepadButton b)
        {
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(b));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
        }
        [UnityTest] public IEnumerator BotViewRoundtripPreservesRosterDifficultyTeamAndFocus()
        {
            yield return Load();Click("roster-card-3");Click("roster-remove");
            Click("roster-add-bot");Click("roster-identity");Click("roster-choice-difficulty-2");Click("roster-device");Click("roster-choice-view-on");Click("roster-done");
            Assert.That(ground.LocalSeatCount,Is.EqualTo(4));Assert.That(ground.SetupBotCount,Is.EqualTo(0));
            Assert.That(ground.SetupComposition().Participant(3).Difficulty,Is.EqualTo(2));Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-3"));
            Click("roster-card-3");Click("roster-device");Click("roster-choice-view-off");Click("roster-done");
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
