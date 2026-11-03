using System.Collections;
using System.Linq;
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
        IEnumerator PointerClick(Button button)
        {
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
            var canvas=button.GetComponentInParent<Canvas>();
            var center=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(qaMouse,new MouseState{position=center});yield return null;yield return null;
            InputSystem.QueueStateEvent(qaMouse,new MouseState{position=center}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(qaMouse,new MouseState{position=center});yield return null;yield return null;
        }
        [UnityTest] public IEnumerator MouseRemovesCardWithGamepadLobbyOwner()
        {
            qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>();
            ground.AddBot();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;yield return PointerClick(B("inline-remove-1"));
                Assert.That(ground.SetupBotCount,Is.Zero,"Actual mouse down/up must remove a bot in a gamepad-owned lobby");
                Call("BeginLobbyJoin",second);yield return null;yield return Click("lobby-profile-none");
                Assert.That(ground.LocalSeatCount,Is.EqualTo(2));
                yield return PointerClick(B("inline-remove-1"));
                Assert.That(ground.LocalSeatCount,Is.EqualTo(1));
                Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad));
                yield return PointerClick(B("roster-card-0"));
                Assert.That(ground.GetComponentsInChildren<Button>().Any(b=>b.name.StartsWith("inline-identity-")),Is.True,"Mouse can edit a gamepad participant");
            }
        }
        [UnityTest] public IEnumerator LeavingLobbyReleasesMenuDevicesAndMouseStaysAvailable()
        {
            qaKeyboard=InputSystem.AddDevice<Keyboard>();qaMouse=InputSystem.AddDevice<Mouse>();yield return null;
            Call("ToMainMenu");yield return null;
            EventSystem.current.SetSelectedGameObject(B("main-action-4").gameObject);
            yield return Press(GamepadButton.South,second);
            Assert.That(B("settings-back"),Is.Not.Null,"A different gamepad can use the menu after leaving the lobby");
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return PointerClick(B("settings-back"));
                Assert.That(B("main-action-0"),Is.Not.Null);
                yield return PointerClick(B("main-action-3"));
                Assert.That(B("profiles-back"),Is.Not.Null,"Mouse remains available after gamepad navigation");
            }
            Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(pad),"Lobby assignment is retained only for the lobby");
        }
        [UnityTest] public IEnumerator UiCorrections_MapBackHasGap()
        {
            yield return Click("lobby-map");
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return null;Canvas.ForceUpdateCanvases();
                var back=B("lobby-dialog-back");var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(back.transform.parent,(RectTransform)back.transform);
                foreach(var card in ground.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("lobby-map-")))
                {var r=RectTransformUtility.CalculateRelativeRectTransformBounds(back.transform.parent,(RectTransform)card.transform);Assert.That(r.min.y-bounds.max.y,Is.GreaterThan(12));}
                yield return Capture("ui-map-gap");
            }
        }
        [UnityTest] public IEnumerator UiCorrections_HintsSurviveYBRepeatedly()
        {
            ground.AddBot();yield return null;
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                for(int i=0;i<3;i++)
                {
                    yield return Press(GamepadButton.North);Assert.That(B(InlineControl("identity")),Is.Not.Null);
                    var back=B("setup-previous").GetComponent<MenuActionHint>();back.Refresh();Assert.That(back.GlyphLabel.enabled,Is.True,"B must remain visible while editing");
                    yield return Press(GamepadButton.East);
                    foreach(var button in new[]{B("setup-previous"),B("roster-add-bot"),B("roster-add-human"),Field<Button>("start")})
                    {var id=button.name;var h=button.GetComponent<MenuActionHint>();h.Refresh();Assert.That(h.GlyphLabel.enabled,Is.True,id+" after Y/B "+i+" device="+h.Device?.Invoke()+" active="+h.Active?.Invoke()+" shortcut="+h.ShortcutActive?.Invoke());}
                }
                yield return Capture("ui-hints-after-y-b");
            }
        }
        [UnityTest] public IEnumerator UiCorrections_InlineKeysHaveRenderedLetters()
        {
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                yield return Press(GamepadButton.North);yield return Click(InlineControl("identity"));yield return Click(InlineControl("create"));
                yield return null;Canvas.ForceUpdateCanvases();
                foreach(var key in ground.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("inline-key-")))
                {var label=key.transform.Find("label").GetComponent<Text>();Assert.That(label.rectTransform.rect.width,Is.GreaterThan(8),key.name);Assert.That(label.cachedTextGenerator.vertexCount,Is.GreaterThan(0),key.name);}
                yield return Capture("ui-inline-keyboard");yield return Capture("ui-inline-keyboard-720",1280,720);
                yield return Click("inline-key-0-0");Assert.That(ground.GetComponentsInChildren<Text>().Single(t=>t.name=="inline-name-0").text,Is.EqualTo("А"));
            }
        }
        [UnityTest] public IEnumerator UiCorrections_TeamsStayOnTheirSidesUntilOverflow()
        {
            ground.SetMatchMode(NativeMatchMode.Teams);for(int i=0;i<7;i++)ground.AddBot();
            using(EditorUiCapture.FixedViewport(ground,1920,1080))
            {
                foreach(int blue in new[]{3,4,5,2,1,6})
                {
                    ground.SetTeam(0,NativeTeam.TeamA);for(int i=0;i<7;i++)ground.SetBotTeam(i,i<blue-1?NativeTeam.TeamA:NativeTeam.TeamB);
                    yield return null;Canvas.ForceUpdateCanvases();
                    var cards=Field<Button[]>("rosterCards");
                    for(int p=0;p<8;p++)
                    {int col=Mathf.RoundToInt(((RectTransform)cards[p].transform).anchorMin.x*4);if(p<blue&&blue<=4)Assert.That(col,Is.LessThan(2),"blue="+blue+" p="+p);if(p>=blue&&8-blue<=4)Assert.That(col,Is.GreaterThanOrEqualTo(2));}
                    yield return Capture("ui-teams-"+blue+"-"+(8-blue));
                }
            }
        }
        [UnityTest] public IEnumerator UiCorrections_ProfileXOpensNameBeforeSaving()
        {
            Call("ToMainMenu");yield return null;yield return Click("main-action-3");
            var catalog=Field<PlayerProfileCatalog>("playerProfiles");int count=catalog.Profiles.Count;
            yield return Press(GamepadButton.West);
            Assert.That(Field<GameObject>("textEntryPanel"),Is.Not.Null);Assert.That(catalog.Profiles.Count,Is.EqualTo(count));
            yield return Capture("ui-profile-create-name");
            yield return Click("text-key-0-0");yield return Click("text-apply");
            Assert.That(catalog.Profiles.Count,Is.EqualTo(count+1));Assert.That(catalog.Profiles.Last().Name,Is.EqualTo("1"));
            yield return Press(GamepadButton.West);yield return Press(GamepadButton.East);Assert.That(catalog.Profiles.Count,Is.EqualTo(count+1),"Cancel must not create");
        }
        [UnityTest] public IEnumerator UiCorrections_ProfileSettingsOnlyPersistedControls()
        {
            Call("ToMainMenu");yield return null;yield return Click("main-action-3");
            float globalMouse=MouseSensitivityPreference.Resolve(ground.Profile);var catalog=Field<PlayerProfileCatalog>("playerProfiles");catalog.Create("UI QA",.1f,false);Call("RefreshProfilesUi");
            yield return Click("profile-settings");
            foreach(var id in new[]{"settings-section-0","settings-section-3","settings-section-4","settings-ui-scale","settings-high-contrast","settings-ammo-hud","settings-bot-reaction-text","settings-reset-all"})
            {var control=ground.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==id);Assert.That(control,Is.Not.Null,id);Assert.That(control.gameObject.activeInHierarchy,Is.False,id);}
            Assert.That(B("settings-section-1").gameObject.activeInHierarchy,Is.True);yield return Capture("ui-personal-control");
            var mouse=ground.GetComponentsInChildren<Slider>().Single(t=>t.name=="settings-mouse-sensitivity");mouse.value=.2f;
            yield return Click("settings-section-2");var fps=ground.GetComponentsInChildren<Toggle>().Single(t=>t.name=="settings-fps");fps.isOn=true;yield return Capture("ui-personal-fps");
            var stored=new PlayerProfileCatalog(ownedProfilePath).Profiles.Single();Assert.That(stored.MouseDegreesPerPixel,Is.EqualTo(mouse.value));Assert.That(stored.ShowFps,Is.True);
            Assert.That(MouseSensitivityPreference.Resolve(ground.Profile),Is.EqualTo(globalMouse),"Profile edit must not change global mouse preference");
            // Selecting a section previews its page before Right enters its controls.
            yield return Press(GamepadButton.DpadLeft);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("settings-section-2"));
            yield return Press(GamepadButton.DpadUp);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("settings-section-1"));
            yield return Press(GamepadButton.DpadRight);Assert.That(EventSystem.current.currentSelectedGameObject.name,Is.EqualTo("settings-mouse-sensitivity"));Assert.That(EventSystem.current.currentSelectedGameObject.activeInHierarchy,Is.True);
            yield return Click("settings-back");yield return Click("profiles-back");yield return Click("main-action-4");
            Assert.That(B("settings-section-0").gameObject.activeInHierarchy,Is.True);Assert.That(B("settings-section-3").gameObject.activeInHierarchy,Is.True);Assert.That(B("settings-reset-all").gameObject.activeInHierarchy,Is.True);
        }
    }
}
