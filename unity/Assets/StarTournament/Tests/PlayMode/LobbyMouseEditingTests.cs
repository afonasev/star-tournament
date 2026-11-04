using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed partial class SingleScreenLobbyTests
    {
        void AddMouseDevices()
        { qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>(); }

        [UnityTest] public IEnumerator MouseRemovesFirstAndCompactedParticipants()
        {
            AddMouseDevices();ground.AddBot();ground.AddBot();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;
                yield return PointerClick(B("inline-remove-0"));
                Assert.That(ground.LocalSeatCount+ground.SetupBotCount,Is.EqualTo(2),"First card must be removed with one click, preserving one view");
                yield return PointerClick(B("inline-remove-0"));
                Assert.That(ground.LocalSeatCount+ground.SetupBotCount,Is.EqualTo(1),"Compacted first card remains removable");
                Assert.That(B("inline-remove-0").interactable,Is.False,"Last participant/view remains protected");
            }
        }
        [UnityTest] public IEnumerator MouseRemovesFirstCardWhileEditing()
        {
            AddMouseDevices();ground.AddBot();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("roster-card-0"));
                Assert.That(ground.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="inline-identity-0"),Is.True);
                yield return PointerClick(B("inline-remove-0"));
                Assert.That(ground.LocalSeatCount+ground.SetupBotCount,Is.EqualTo(1),"Editing must not swallow the remove click");
            }
        }
        [UnityTest] public IEnumerator MouseSwitchesInlineCardAndAddsBotDirectly()
        {
            AddMouseDevices();ground.AddBot();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("roster-card-0"));
                yield return PointerClick(B("roster-card-1"));
                Assert.That(B("inline-identity-1"),Is.Not.Null,"A mouse editor can switch directly to another card");
                yield return PointerClick(B("roster-add-bot"));
                yield return PointerClick(B("lobby-add-difficulty-1"));
                Assert.That(ground.SetupBotCount,Is.EqualTo(2),"Adding a bot exits the pointer editor without a keyboard");
                yield return PointerClick(B("roster-card-0"));
                yield return PointerClick(B("roster-empty-0"));
                yield return PointerClick(B("lobby-add-difficulty-2"));
                Assert.That(ground.SetupBotCount,Is.EqualTo(3),"Empty-slot plus also exits the pointer editor");
            }
        }
        [UnityTest] public IEnumerator MouseRemovesFirstCardInKeyboardLobby()
        {
            AddMouseDevices();Call("ToMainMenu");typeof(ProvingGround).GetField("lobbyInitialized",Private).SetValue(ground,false);yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("main-action-0"));
                Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(qaKeyboard));
                ground.AddBot();yield return null;
                yield return PointerClick(B("inline-remove-0"));
                Assert.That(ground.LocalSeatCount+ground.SetupBotCount,Is.EqualTo(1));
            }
        }
        [UnityTest] public IEnumerator MouseRosterActionsPreserveOtherPlayersEditor()
        {
            AddMouseDevices();Call("BeginLobbyJoin",second);yield return null;yield return Click("lobby-profile-none");
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("roster-card-0"));
                yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.North,second);
                Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"));
                yield return PointerClick(B("roster-add-bot"));
                Assert.That(EditorField<object>(second,"Page").ToString(),Is.EqualTo("Fields"),"Mouse cannot close another player's gamepad editor");
                Assert.That(ground.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="lobby-add-difficulty-1"),Is.False);
                yield return Press(UnityEngine.InputSystem.LowLevel.GamepadButton.East,second);
                yield return PointerClick(B("roster-card-0"));
                yield return PointerClick(B("roster-add-human"));
                yield return PointerClick(B("lobby-device-"+qaKeyboard.deviceId));
                yield return PointerClick(B("lobby-profile-none"));
                Assert.That(ground.LocalSeatCount,Is.EqualTo(3),"Mouse join action closes its own inline editor");
                Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
                Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(1),Is.SameAs(second));
                Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(2),Is.SameAs(qaKeyboard));
            }
        }
        [UnityTest] public IEnumerator MouseLeavesInlineSwitchesCardAndAddsBot()
        {
            AddMouseDevices();ground.AddBot();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("roster-card-0"));
                yield return Capture("mouse-inline-human-before-back");
                yield return Capture("mouse-inline-human-720",1280,720);
                yield return PointerClick(B("inline-back-0"));
                Assert.That(ground.GetComponentsInChildren<UnityEngine.UI.Button>().Any(b=>b.name=="inline-identity-0"),Is.False);
                yield return PointerClick(B("roster-card-1"));
                yield return PointerClick(B("inline-identity-1"));
                yield return Capture("mouse-inline-bot-difficulty");
                yield return PointerClick(B("inline-back-1"));
                Assert.That(B("inline-identity-1"),Is.Not.Null,"Back from a subpage returns to fields");
                yield return PointerClick(B("inline-back-1"));
                yield return PointerClick(B("roster-add-bot"));
                yield return PointerClick(B("lobby-add-difficulty-1"));
                Assert.That(ground.SetupBotCount,Is.EqualTo(2),"Mouse can add a bot after closing inline settings");
                yield return Capture("mouse-roster-after-add");
            }
        }
    }
}
