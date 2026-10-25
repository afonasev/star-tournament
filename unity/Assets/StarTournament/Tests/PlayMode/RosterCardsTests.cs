using System;
using System.Collections;
using System.Collections.Generic;
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
        InputSettings.EditorInputBehaviorInPlayMode priorInput;InputSettings.BackgroundBehavior priorBackground;
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
        Button B(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name==name);
        IEnumerator Click(string name){Assert.That(B(name).interactable,Is.True,name);EventSystem.current.SetSelectedGameObject(B(name).gameObject);B(name).onClick.Invoke();yield return null;yield return null;}
        object Inline=>typeof(ProvingGround).GetMethod("InlineFor",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{pad});
        T Editor<T>(string name)=>(T)Inline.GetType().GetField(name).GetValue(Inline);
        string Focus=>Editor<Button>("Focus")?.name;
        IEnumerator CloseInline(){while(Editor<object>("Page").ToString()!="Closed")yield return Press(GamepadButton.East);}
        Button ProfileChoice(string name)=>ground.GetComponentsInChildren<Button>().Single(b=>b.name.StartsWith("inline-profile-0-")&&b.GetComponentInChildren<Text>().text.StartsWith(name));
        [UnitySetUp] public IEnumerator Load()
        {
            priorInput=InputSystem.settings.editorInputBehaviorInPlayMode;priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();pad=InputSystem.AddDevice<Gamepad>();
            EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);yield return Press(GamepadButton.South);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(ground)typeof(ProvingGround).GetMethod("CancelHistoryLoading",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(ground,new object[]{true});
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(secondPad!=null&&secondPad.added)InputSystem.RemoveDevice(secondPad);if(unsupported!=null&&unsupported.added)InputSystem.RemoveDevice(unsupported);
            if(profilesPath!=null&&File.Exists(profilesPath))File.Delete(profilesPath);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            InputSystem.settings.editorInputBehaviorInPlayMode=priorInput;InputSystem.settings.backgroundBehavior=priorBackground;
        }
        IEnumerator Press(GamepadButton b,Gamepad device=null)
        {device=device??pad;InputSystem.QueueStateEvent(device,new GamepadState().WithButton(b));yield return null;yield return null;InputSystem.QueueStateEvent(device,new GamepadState());yield return null;}
        IEnumerator AddBot(int difficulty=1,NativeTeam team=NativeTeam.TeamA)
        {yield return Click("roster-add-bot");yield return Click("lobby-add-difficulty-"+difficulty);if(ground.SetupMode==NativeMatchMode.Teams)yield return Click("lobby-add-team-"+(int)team);}
        PlayerProfileCatalog Profiles(int count)
        {
            profilesPath=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+"-roster-profiles.json");var catalog=new PlayerProfileCatalog(profilesPath);
            for(int i=0;i<count;i++)catalog.Create("Профиль "+i.ToString("00"),.2f,false);
            typeof(ProvingGround).GetField("playerProfiles",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(ground,catalog);return catalog;
        }
        [UnityTest] public IEnumerator RosterYJoinsThenSwitchesOwnTeamAndXUsesLastCommittedDifficulty()
        {
            ground.SetMatchMode(NativeMatchMode.Teams);secondPad=InputSystem.AddDevice<Gamepad>();
            yield return Press(GamepadButton.North,secondPad);Assert.That(ground.LocalSeatCount,Is.EqualTo(1));yield return Click("lobby-profile-none");yield return Click("lobby-add-team-"+(int)NativeTeam.TeamB);
            Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(1),Is.SameAs(secondPad));
            yield return Press(GamepadButton.North,secondPad);Assert.That(Field<NativeTeam[]>("teamAssignments")[1],Is.EqualTo(NativeTeam.TeamA));
            yield return AddBot(0,NativeTeam.TeamB);ground.SetBotDifficulty(0,2);int before=ground.SetupComposition().ParticipantCount;
            EventSystem.current.SetSelectedGameObject(B("roster-add-bot").gameObject);yield return null;yield return null;yield return Press(GamepadButton.West);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-difficulty-0"));
            Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(before));yield return Click("lobby-add-difficulty-0");yield return Click("lobby-add-team-"+(int)NativeTeam.TeamB);
            Assert.That(ground.SetupComposition().Participant(before).Difficulty,Is.Zero);Assert.That(Field<int>("rosterEditing"),Is.EqualTo(-1));
        }
        [UnityTest] public IEnumerator NewHumanAndBotsOfferActualSmallerTeamAfterManualMoves()
        {
            NativeSetupFixture.UnboundHumans(ground,3);ground.SetMatchMode(NativeMatchMode.Teams);for(int i=0;i<3;i++)ground.SetTeam(i,NativeTeam.TeamA);
            secondPad=InputSystem.AddDevice<Gamepad>();yield return Click("roster-add-human");yield return Click("lobby-device-"+secondPad.deviceId);yield return Click("lobby-profile-none");
            Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-team-"+(int)NativeTeam.TeamB));yield return Click("lobby-add-team-"+(int)NativeTeam.TeamB);
            ground.AddBot();Assert.That(ground.SetupComposition().Roster.Read().Teams[4],Is.EqualTo(NativeTeam.TeamB));
            ground.SetBotTeam(0,NativeTeam.TeamA);ground.SetSeatAi(1,true);ground.AddBot();Assert.That(ground.SetupComposition().Roster.Read().Teams[5],Is.EqualTo(NativeTeam.TeamB));
            ground.SetBotTeam(0,NativeTeam.TeamB);ground.AddBot();Assert.That(ground.SetupComposition().Roster.Read().Teams[6],Is.EqualTo(NativeTeam.TeamA));
            ground.RemoveBot(0);ground.AddBot();Assert.That(ground.SetupComposition().Roster.Read().Teams[6],Is.EqualTo(NativeTeam.TeamB));
        }
        [UnityTest] public IEnumerator NewYJoinAndReconnectPreserveExplicitTeamAndIdentity()
        {
            ground.SetMatchMode(NativeMatchMode.Teams);ground.AddBot();ground.SetBotTeam(0,NativeTeam.TeamA);ground.AddBot();ground.SetBotTeam(1,NativeTeam.TeamB);
            secondPad=InputSystem.AddDevice<Gamepad>();yield return Press(GamepadButton.North,secondPad);yield return Click("lobby-profile-none");
            Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("lobby-add-team-"+(int)NativeTeam.TeamB));yield return Click("lobby-add-team-"+(int)NativeTeam.TeamB);
            var id=Field<LocalIdentitySession>("identities").GuestAt(1).Id;yield return Press(GamepadButton.North,secondPad);
            Assert.That(Field<NativeTeam[]>("teamAssignments")[1],Is.EqualTo(NativeTeam.TeamA));
            InputSystem.RemoveDevice(secondPad);secondPad=InputSystem.AddDevice<Gamepad>();yield return Press(GamepadButton.North,secondPad);
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(1),Is.SameAs(secondPad));
            Assert.That(Field<NativeTeam[]>("teamAssignments")[1],Is.EqualTo(NativeTeam.TeamA));Assert.That(Field<LocalIdentitySession>("identities").GuestAt(1).Id,Is.EqualTo(id));
        }
        [UnityTest] public IEnumerator CompactProfilesShowFiveRowsAndScrollWithoutChoosingBusyProfile()
        {
            var profiles=Profiles(10);Field<LocalIdentitySession>("identities").ChooseProfile(1,-101,profiles.Profiles[0].Id,profiles);
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return Click("roster-card-0");yield return Click("inline-identity-0");
                Canvas.ForceUpdateCanvases();Assert.That(ProfileChoice(profiles.Profiles[0].Name).interactable,Is.False);
                var scroll=Editor<ScrollRect>("Scroll");var first=Editor<List<Button>>("Choices")[0];
                Assert.That(scroll.viewport.rect.height/((RectTransform)first.transform).rect.height,Is.InRange(5f,5.5f));
                var fifth=Editor<List<Button>>("Choices")[4];
                var corners=new Vector3[4];((RectTransform)fifth.transform).GetWorldCorners(corners);
                var bottom=scroll.viewport.InverseTransformPoint(corners[0]);
                Assert.That(bottom.y,Is.GreaterThanOrEqualTo(scroll.viewport.rect.yMin),"Five complete visible profile buttons; decorative focus can extend outside its row");
                // Real pad navigation reveals the final non-busy profile without committing along the way.
                for(int i=0;i<Editor<List<Button>>("Choices").Count&&Editor<Button>("Focus").GetComponentInChildren<Text>().text!=profiles.Profiles[9].Name;i++)yield return Press(GamepadButton.DpadDown);
                Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(1));
                var last=Editor<Button>("Focus");Assert.That(last.GetComponentInChildren<Text>().text,Is.EqualTo(profiles.Profiles[9].Name));
                yield return Press(GamepadButton.South);
            }
            Assert.That(Field<LocalIdentitySession>("identities").ProfileId(0),Is.EqualTo(profiles.Profiles[9].Id));
            yield return Click("inline-identity-0");yield return Click("inline-profile-0-0");Assert.That(Field<LocalIdentitySession>("identities").GuestAt(0),Is.Not.Null);
        }
        [UnityTest] public IEnumerator BotViewRoundtripPreservesRosterDifficultyTeamAndFocus()
        {
            yield return AddBot(2);yield return Click("roster-card-1");yield return Click("inline-screen-1");yield return Click("inline-screen-choice-1-1");yield return CloseInline();
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(ground.SetupBotCount,Is.Zero);Assert.That(ground.SetupComposition().Participant(1).Difficulty,Is.EqualTo(2));
            Assert.That(Focus,Is.EqualTo("roster-card-1"));Assert.That(Field<GameObject[]>("rosterScreenIcons")[1].activeSelf,Is.True);
            yield return Click("roster-card-1");yield return Click("inline-screen-1");yield return Click("inline-screen-off-1");yield return CloseInline();
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(2));Assert.That(ground.SetupComposition().Participant(1).Difficulty,Is.EqualTo(2));
            Assert.That(Field<GameObject[]>("rosterScreenIcons")[1].activeSelf,Is.False);
            yield return Click("setup-previous");yield return Click("main-action-0");Assert.That(ground.SetupComposition().Participant(1).Difficulty,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator UnsupportedJoystickCannotBeOfferedOrAssigned()
        {
            unsupported=InputSystem.AddDevice<Joystick>();yield return Click("roster-add-human");
            Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name=="lobby-device-"+unsupported.deviceId),Is.False,"Only available supported devices belong in this picker");
            Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.Not.SameAs(unsupported));Assert.That(GamepadCompatibility.CanAssign(unsupported),Is.False);
        }
        [UnityTest] public IEnumerator GamepadCanEditDifficultyCancelAndReturnToCard()
        {
            yield return AddBot();Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("roster-card-1"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("inline-identity-1"));
            yield return Press(GamepadButton.South);Assert.That(Focus,Is.EqualTo("inline-difficulty-1-0"));
            yield return Press(GamepadButton.DpadDown);yield return Press(GamepadButton.DpadDown);yield return Press(GamepadButton.South);
            Assert.That(ground.SetupComposition().Participant(1).Difficulty,Is.EqualTo(2));yield return Press(GamepadButton.East);
            Assert.That(Focus,Is.EqualTo("roster-card-1"));Assert.That(Editor<object>("Page").ToString(),Is.EqualTo("Closed"));
            Assert.That(B("roster-card-1").GetComponent<MenuPresentation>().FocusVisible,Is.True);
        }
        [UnityTest] public IEnumerator YCannotJoinThroughBotEditorAndNewJoinRequiresExplicitProfile()
        {
            var profiles=Profiles(1);secondPad=InputSystem.AddDevice<Gamepad>();yield return AddBot(2);yield return Click("roster-card-1");yield return Click("inline-identity-1");
            yield return Press(GamepadButton.North,secondPad);Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(Editor<object>("Page").ToString(),Is.EqualTo("Identity"));
            Assert.That(Editor<GameObject>("Root").activeSelf,Is.True);Assert.That(Editor<Button>("Focus").transform.IsChildOf(Editor<GameObject>("Root").transform),Is.True);
            yield return Press(GamepadButton.East);yield return Press(GamepadButton.East);yield return Press(GamepadButton.North,secondPad);
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));yield return Click("lobby-profile-"+profiles.Profiles[0].Id);
            Assert.That(ground.LocalSeatCount,Is.EqualTo(2));Assert.That(Field<LocalIdentitySession>("identities").ProfileId(1),Is.EqualTo(profiles.Profiles[0].Id));Assert.That(ground.SetupComposition().Participant(2).Difficulty,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator FullUnequalTeamsAllowExplicitTransferAndBlockFurtherAdds()
        {
            ground.SetMatchMode(NativeMatchMode.Teams);for(int i=0;i<7;i++)yield return AddBot(1,NativeTeam.TeamA);ground.SetTeam(0,NativeTeam.TeamB);
            Assert.That(B("roster-add-human").interactable,Is.False);Assert.That(B("roster-add-bot").interactable,Is.False);Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(8));
            yield return Click("roster-card-7");yield return Click("inline-team-7");yield return Click("inline-team-choice-7-1");yield return CloseInline();
            Assert.That(ground.SetupComposition().Roster.Read().Teams[7],Is.EqualTo(NativeTeam.TeamB));Assert.That(Focus,Is.EqualTo("roster-card-7"));
        }
    }
}
