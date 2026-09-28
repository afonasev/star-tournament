using System.Collections;
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
    public sealed class DefaultRosterTests
    {
        Scene scene;ProvingGround ground;Gamepad pad;Keyboard keyboard;Mouse mouse;
        InputSettings.EditorInputBehaviorInPlayMode priorEditorInput;InputSettings.BackgroundBehavior priorBackground;
        T Field<T>(string name)=>(T)typeof(ProvingGround).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
        Button B(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Load()
        {
            priorEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();pad=InputSystem.AddDevice<Gamepad>();
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
        }
        void AssertDefault(InputDevice device,int profiles)
        {
            Assert.That(ground.LocalSeatCount,Is.EqualTo(1));Assert.That(ground.SetupBotCount,Is.EqualTo(1));
            Assert.That(ground.SetupComposition().ParticipantCount,Is.EqualTo(2));Assert.That(Field<SeatInputCoordinator>("input").DeviceAt(0),Is.SameAs(device));
            Assert.That(Field<SeatInputCoordinator>("input").Ready,Is.True);Assert.That(Field<LocalIdentitySession>("identities").GuestAt(0),Is.Not.Null);
            Assert.That(Field<PlayerProfileCatalog>("playerProfiles").Profiles.Count,Is.EqualTo(profiles));Assert.That(Field<int>("pendingSeat"),Is.EqualTo(-1));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
            foreach(var d in new InputDevice[]{pad,keyboard,mouse})if(d!=null&&d.added)InputSystem.RemoveDevice(d);
            InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;InputSystem.settings.backgroundBehavior=priorBackground;
        }
        [UnityTest] public IEnumerator GamepadEntryUsesSubmittingPadAndBackKeepsDraftButNewMatchResets()
        {
            yield return Load();int profiles=Field<PlayerProfileCatalog>("playerProfiles").Profiles.Count;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;AssertDefault(pad,profiles);
            B("setup-next").onClick.Invoke();B("setup-next").onClick.Invoke();ground.AddBot();
            B("setup-previous").onClick.Invoke();B("setup-next").onClick.Invoke();Assert.That(ground.SetupBotCount,Is.EqualTo(2));
            B("setup-previous").onClick.Invoke();B("setup-previous").onClick.Invoke();B("setup-map-exit").onClick.Invoke();
            InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(20,20)});yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.South));yield return null;yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;AssertDefault(pad,profiles);
        }
        [UnityTest] public IEnumerator KeyboardEntryCreatesOnlyOneGuestAndOneBot()
        {
            yield return Load();int profiles=Field<PlayerProfileCatalog>("playerProfiles").Profiles.Count;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;AssertDefault(keyboard,profiles);
        }
        [UnityTest] public IEnumerator MouseEntryAssignsKeyboardMouseInsteadOfCurrentGamepad()
        {
            yield return Load();int profiles=Field<PlayerProfileCatalog>("playerProfiles").Profiles.Count;
            InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(GamepadButton.DpadDown));yield return null;
            InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)B("main-action-0").transform;
            var center=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center}.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=center});yield return null;yield return null;AssertDefault(keyboard,profiles);
        }
    }
}
