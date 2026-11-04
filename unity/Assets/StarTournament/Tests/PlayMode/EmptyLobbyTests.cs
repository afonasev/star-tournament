using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed partial class SingleScreenLobbyTests
    {
        [UnityTest] public IEnumerator EmptyLobbyCanRejoinAndRecoverStartGate()
        {
            AddMouseDevices();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("inline-remove-0"));
                Assert.That(ground.LocalSeatCount,Is.Zero);Assert.That(ground.SetupBotCount,Is.Zero);
                Assert.That(Field<SeatInputCoordinator>("input").Ready,Is.False);
                Assert.That(ground.GetComponentsInChildren<Button>().Count(b=>b.name.StartsWith("roster-empty-")),Is.EqualTo(8));
                Assert.That(Field<Button>("start").interactable,Is.False);
                Assert.That(Call("SetupBlockingReason"),Is.EqualTo("Добавьте игрока или бота"));
                Field<Button>("start").onClick.Invoke();yield return null;
                Assert.That(Field<object>("phase").ToString(),Is.EqualTo("Setup"),"Blocked start cannot construct an invalid match");
                yield return Capture("empty-roster");
                yield return PointerClick(B("roster-add-human"));
                yield return PointerClick(B("lobby-device-"+pad.deviceId));
                yield return PointerClick(B("lobby-profile-none"));
                Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
                Assert.That(Field<Button>("start").interactable,Is.False,"Normal arena still needs two participants");
                yield return PointerClick(B("roster-add-bot"));yield return PointerClick(B("lobby-add-difficulty-1"));
                Assert.That(Field<Button>("start").interactable,Is.True);
            }
        }
        [UnityTest] public IEnumerator LastViewCanBeRemovedWithoutDeletingOrPromotingAnotherBot()
        {
            AddMouseDevices();ground.AddBot();ground.SetBotDifficulty(0,2);yield return null;
            var original=Field<NativeBotSetup>("botSetup").At(0);
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("inline-remove-0"));
                Assert.That(ground.LocalSeatCount,Is.Zero);Assert.That(ground.SetupBotCount,Is.EqualTo(1));
                Assert.That(Field<NativeBotSetup>("botSetup").At(0),Is.EqualTo(original),"Deleting a participant leaves other identities/settings untouched");
                Assert.That(Call("SetupBlockingReason"),Is.EqualTo("Назначьте экран хотя бы одному участнику"));
                Assert.That(Field<Button>("start").interactable,Is.False);
                yield return PointerClick(B("roster-card-0"));yield return PointerClick(B("inline-screen-0"));
                yield return PointerClick(B("inline-screen-choice-0-0"));yield return PointerClick(B("inline-apply-0"));
                Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupBotCount,Is.Zero);
                Assert.That(Field<NativeBotSetup>("botSetup").IsAi(0),Is.True);
                ground.AddBot();yield return null;Assert.That(Field<Button>("start").interactable,Is.True,"Existing AI-only match contract remains usable");
                yield return PointerClick(B("inline-remove-1"));yield return PointerClick(B("inline-remove-0"));
                Assert.That(ground.LocalSeatCount+ground.SetupBotCount,Is.Zero,"Last AI screen can also be deleted");
                ground.AddBot();yield return null;Assert.That(ground.SetupBotCount,Is.EqualTo(1),"A bot can be added to an empty setup");
            }
        }
        [UnityTest] public IEnumerator GreenCheckClosesSubpageWithoutApplyingUnconfirmedFocus()
        {
            AddMouseDevices();ground.AddBot();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("roster-card-1"));yield return PointerClick(B("inline-identity-1"));
                yield return PointerClick(B("inline-apply-1"));
                Assert.That(Field<NativeBotSetup>("botSetup").At(0).Difficulty,Is.EqualTo(1),"Opening a subpage and finishing does not choose its first option");
                yield return PointerClick(B("roster-card-1"));yield return PointerClick(B("inline-identity-1"));
                yield return PointerClick(B("inline-difficulty-1-2"));yield return PointerClick(B("inline-apply-1"));
                Assert.That(Field<NativeBotSetup>("botSetup").At(0).Difficulty,Is.EqualTo(2));
                Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name=="inline-identity-1"),Is.False);
                yield return PointerClick(B("roster-card-0"));
                var apply=B("inline-apply-0");var cross=B("inline-remove-0");
                Assert.That(apply.GetComponentInChildren<Text>().text,Is.EqualTo("✓"));
                Assert.That(apply.GetComponentInChildren<Text>().color.g,Is.GreaterThan(apply.GetComponentInChildren<Text>().color.r));
                // Measure the actual hit rectangles, not recursive bounds that also
                // include inactive generic menu hint/decor children.
                var checkCorners=new Vector3[4];var crossCorners=new Vector3[4];
                ((RectTransform)apply.transform).GetWorldCorners(checkCorners);((RectTransform)cross.transform).GetWorldCorners(crossCorners);
                Assert.That(checkCorners[2].x,Is.LessThan(crossCorners[0].x),"Check sits left of the delete action without overlap");
                MenuSelectorOverlayTests.AssertPointerTarget(apply);MenuSelectorOverlayTests.AssertPointerTarget(cross);
                yield return Capture("green-confirm-1080");yield return Capture("green-confirm-720",1280,720);
            }
        }
        [UnityTest] public IEnumerator GreenCheckValidatesAndAssignsNewProfileDraft()
        {
            AddMouseDevices();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("roster-card-0"));yield return PointerClick(B("inline-identity-0"));yield return PointerClick(B("inline-create-0"));
                int before=Field<PlayerProfileCatalog>("playerProfiles").Profiles.Count;
                yield return PointerClick(B("inline-apply-0"));
                Assert.That(B("inline-key-0-0"),Is.Not.Null,"Empty names stay in the editor");
                Assert.That(Field<PlayerProfileCatalog>("playerProfiles").Profiles.Count,Is.EqualTo(before));
                yield return PointerClick(B("inline-key-0-0"));yield return PointerClick(B("inline-apply-0"));
                Assert.That(Field<PlayerProfileCatalog>("playerProfiles").Profiles.Count,Is.EqualTo(before+1));
                Assert.That(Field<LocalIdentitySession>("identities").ProfileId(0),Is.Not.Null,"Confirm assigns the explicitly named new profile");
                Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name=="inline-create-0"),Is.False);
            }
        }
        [UnityTest] public IEnumerator GreenCheckPreservesOtherDeviceEditor()
        {
            AddMouseDevices();Call("BeginLobbyJoin",second);yield return null;yield return Click("lobby-profile-none");
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("roster-card-0"));yield return Press(GamepadButton.North,second);
                string target=EditorField<string>(second,"Target");string focus=EditorField<Button>(second,"Focus").name;
                EditorFor(second).GetType().GetField("Draft").SetValue(EditorFor(second),"Other draft");
                yield return PointerClick(B("inline-apply-0"));
                Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));
                Assert.That(EditorField<string>(second,"Target"),Is.EqualTo(target));
                Assert.That(EditorField<string>(second,"Draft"),Is.EqualTo("Other draft"));
                Assert.That(EditorField<Button>(second,"Focus").name,Is.EqualTo(focus));
            }
        }
        [UnityTest] public IEnumerator EmptyLobbyRemainsEditableAcrossModesAndTraining()
        {
            AddMouseDevices();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("inline-remove-0"));
                ground.SetMatchMode(NativeMatchMode.Teams);yield return null;
                Assert.That(Field<Button>("start").interactable,Is.False);Assert.That(B("roster-add-bot").interactable,Is.True);
                ground.SelectAuthoredMap(FiringRangeCatalog.Id);yield return null;
                Assert.That(Field<Button>("start").interactable,Is.False);Assert.That(B("roster-add-human").interactable,Is.True);
                Assert.That(B("roster-add-bot").interactable,Is.False,"Training's independent no-bot rule remains intact");
            }
        }
    }
}
