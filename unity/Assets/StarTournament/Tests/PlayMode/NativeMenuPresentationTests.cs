using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeMenuPresentationTests
    {
        Scene scene;ProvingGround ground;
        InputSettings.EditorInputBehaviorInPlayMode priorEditorInput;InputSettings.BackgroundBehavior priorBackground;
        Button B(string n)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==n);
        [UnitySetUp] public IEnumerator Setup()
        {
            priorEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;priorBackground=InputSystem.settings.backgroundBehavior;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;InputSystem.settings.backgroundBehavior=priorBackground;}
        [UnityTest] public IEnumerator ApprovedMainMenuHasReadableArtworkAndSixPreservedActions()
        {
            var root=ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="main-menu");
            var art=root.GetComponentInChildren<RawImage>();Assert.That(art.texture,Is.Not.Null);Assert.That(art.raycastTarget,Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("КОМБАТ БОУЛ")||t.text.Contains("ОРБИТАЛЬНАЯ ЛИГА")||t.name=="route"),Is.False);
            for(int i=0;i<6;i++)Assert.That(B("main-action-"+i).interactable,Is.EqualTo(i!=1));
            Canvas.ForceUpdateCanvases();yield return null;
            var battle=(RectTransform)B("main-action-0").transform;
            Assert.That(battle.rect.height,Is.InRange(50,72));
            Assert.That(B("main-action-0").GetComponentInChildren<Text>().color,Is.Not.EqualTo(Color.white));
        }
        [UnityTest] public IEnumerator CompactControlsDoNotCollapseRosterCardsOrLoseSettingsNavigation()
        {
            B("main-action-0").onClick.Invoke();B("setup-next").onClick.Invoke();B("setup-next").onClick.Invoke();yield return null;
            Canvas.ForceUpdateCanvases();
            var card=B("roster-card-0");Assert.That(card.GetComponent<MenuPresentation>().Compact,Is.False);
            Assert.That(((RectTransform)card.transform).rect.height,Is.GreaterThan(MenuPresentation.ButtonHeight));
            B("Назад к главному меню").onClick.Invoke();B("main-action-4").onClick.Invoke();yield return null;
            for(int i=0;i<4;i++)
            {
                B("settings-section-"+i).onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
                foreach(var b in ground.GetComponentsInChildren<Button>().Where(x=>x.name.StartsWith("settings-")))
                {
                    Assert.That(((RectTransform)b.transform).rect.height,Is.GreaterThan(0),b.name);
                    Assert.That(b.GetComponent<MenuPresentation>(),Is.Not.Null,b.name);
                }
            }
            B("settings-back").onClick.Invoke();Assert.That(B("main-action-0").gameObject.activeInHierarchy,Is.True);
        }
        [UnityTest] public IEnumerator KeyboardNavigatesMainMenuAndSubmitsAfterReturningFromSettings()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                B("main-action-4").onClick.Invoke();B("settings-back").onClick.Invoke();
                EventSystem.current.SetSelectedGameObject(B("main-action-0").gameObject);yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.DownArrow));yield return null;yield return null;
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                Assert.That(EventSystem.current.currentSelectedGameObject,Is.EqualTo(B("main-action-2").gameObject));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Enter));yield return null;yield return null;
                Assert.That(B("lab-back").gameObject.activeInHierarchy,Is.True);
            }
            finally{InputSystem.RemoveDevice(keyboard);}
        }
        [UnityTest] public IEnumerator SetupUsesMandatoryModeTargetsAndInheritedBotDifficulty()
        {
            B("main-action-0").onClick.Invoke();yield return null;
            Assert.That(ground.Configuration.TargetEnabled,Is.True);Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2000));
            ground.SetMatchMode(NativeMatchMode.Teams);Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(3500));
            ground.SetBotDifficulty(0,0);ground.AddBot();Assert.That(ground.SetupComposition().Participant(2).Difficulty,Is.EqualTo(0));
            ground.SetBotDifficulty(0,2);ground.AddBot();Assert.That(ground.SetupComposition().Participant(3).Difficulty,Is.EqualTo(2));
            ground.SetMatchMode(NativeMatchMode.Ffa);Assert.That(ground.Configuration.TargetPoints,Is.EqualTo(2000));
        }
        [UnityTest] public IEnumerator FeedbackUsesTopBackSquareChecksAndFullResolutionList()
        {
            B("main-action-3").onClick.Invoke();yield return null;
            Assert.That(((RectTransform)B("profiles-back").transform).anchorMin.y,Is.GreaterThan(.9f));
            B("profiles-back").onClick.Invoke();B("main-action-0").onClick.Invoke();yield return null;
            Assert.That(ground.GetComponentsInChildren<ArenaPreviewGraphic>().Length,Is.EqualTo(1));
            Assert.That(((RectTransform)B("setup-previous").transform).anchorMin.y,Is.GreaterThan(.9f));
            B("Назад к главному меню").onClick.Invoke();B("main-action-4").onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
            foreach(var t in ground.GetComponentsInChildren<Toggle>())
            {var box=(RectTransform)t.transform.Find("box");Assert.That(box.rect.width,Is.EqualTo(box.rect.height).Within(.1));}
            B("settings-resolution").onClick.Invoke();yield return null;
            Assert.That(ground.GetComponentsInChildren<Button>(true).Any(b=>b.name=="resolution-page-next"),Is.False);
            var choices=ground.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("resolution-choice-")).ToArray();
            Assert.That(choices.Length,Is.GreaterThan(0));Assert.That(choices.Last().navigation.selectOnDown,Is.EqualTo(B("resolution-cancel")));
            B("resolution-cancel").onClick.Invoke();B("settings-section-1").onClick.Invoke();B("settings-controls-help").onClick.Invoke();yield return null;
            var illustration=ground.GetComponentsInChildren<RawImage>().Single(x=>x.name=="help-illustration");Assert.That(illustration.texture,Is.Not.Null);Assert.That(illustration.texture.width,Is.GreaterThanOrEqualTo(1024));
            Assert.That(((RectTransform)B("settings-help-back").transform).anchorMin,Is.EqualTo(((RectTransform)B("settings-back").transform).anchorMin));
        }
    }
}
