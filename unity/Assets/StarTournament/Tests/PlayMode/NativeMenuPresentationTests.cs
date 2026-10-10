using System.Collections;
using System.Linq;
using System.Reflection;
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
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);InputSystem.settings.editorInputBehaviorInPlayMode=priorEditorInput;InputSystem.settings.backgroundBehavior=priorBackground;}
        [UnityTest] public IEnumerator CreditFooterFollowsEveryMenuAndStaysBelowControls()
        {
            var footer=ground.GetComponentsInChildren<Text>(true).Single(t=>t.name=="menu-credit-footer");
            void Check(){Canvas.ForceUpdateCanvases();Assert.That(footer.gameObject.activeInHierarchy,Is.True);Assert.That(footer.text,Is.EqualTo(NativeLoadingScreen.Credit));Assert.That(footer.raycastTarget,Is.False);Assert.That(footer.rectTransform.rect.height,Is.GreaterThan(footer.preferredHeight));}
            var ui=ground.transform.Find("native-ui").gameObject;Check();
            yield return NativeLoadingScreenTests.Capture("menu-main",ui);
            B("main-action-0").onClick.Invoke();Check();yield return NativeLoadingScreenTests.Capture("menu-map",ui);
            B("seat-identity-0").onClick.Invoke();Check();yield return NativeLoadingScreenTests.Capture("menu-identity",ui);B("identity-cancel").onClick.Invoke();
            B("setup-next").onClick.Invoke();Check();B("setup-next").onClick.Invoke();Check();
            yield return NativeLoadingScreenTests.Capture("menu-roster",ui);
            B("roster-card-0").onClick.Invoke();Check();yield return NativeLoadingScreenTests.Capture("menu-roster-editor",ui);B("roster-done").onClick.Invoke();
            B("Назад к главному меню").onClick.Invoke();B("main-action-3").onClick.Invoke();Check();
            yield return NativeLoadingScreenTests.Capture("menu-profiles",ui);B("profiles-back").onClick.Invoke();
            B("main-action-4").onClick.Invoke();
            for(int section=0;section<4;section++){B("settings-section-"+section).onClick.Invoke();Check();}
            yield return NativeLoadingScreenTests.Capture("menu-settings",ui);B("settings-back").onClick.Invoke();
            B("main-action-2").onClick.Invoke();Check();yield return NativeLoadingScreenTests.Capture("menu-lab",ui);B("lab-back").onClick.Invoke();
            B("main-action-0").onClick.Invoke();B("Диагностика четырёх камер · без управления").onClick.Invoke();
            yield return NativeLoadingTestScene.Wait(ground);Assert.That(footer.gameObject.activeInHierarchy,Is.False);
            ground.SendMessage("Pause","Footer review");Check();yield return NativeLoadingScreenTests.Capture("menu-pause",ui);
            B("Повторить матч").onClick.Invoke();yield return NativeLoadingTestScene.Wait(ground);
            ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,10000,0,ground.Session.Life(0).Life);
            while(ground.Session.Match.Phase==NativeMatchPhase.Running){ground.Session.Match.BeginTick();ground.Session.Match.EndTick();}
            yield return new WaitForFixedUpdate();yield return null;Check();yield return NativeLoadingScreenTests.Capture("menu-results",ui);
        }
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
        [UnityTest] public IEnumerator NavigationFocusHasGoldGraphicAndHoverCannotCreateASecondFocus()
        {
            var primary=B("main-action-0");var secondary=B("main-action-2");
            var primaryFill=primary.GetComponent<Image>().color;
            EventSystem.current.SetSelectedGameObject(primary.gameObject);yield return null;
            Assert.That(primary.GetComponent<MenuPresentation>().FocusVisible,Is.True);
            var graphic=primary.GetComponentInChildren<MenuFocusGraphic>();
            Assert.That(graphic,Is.Not.Null);Assert.That(graphic.raycastTarget,Is.False);
            Assert.That(primary.GetComponent<Image>().color,Is.EqualTo(primaryFill),"Focus preserves action fill");
            Assert.That(MenuFocusGraphic.StrokeWidth(95),Is.EqualTo(7));
            Assert.That(MenuFocusGraphic.GapWidth(95),Is.GreaterThan(5));
            Assert.That(MenuFocusGraphic.StrokeWidth(46)+MenuFocusGraphic.GapWidth(46),Is.LessThan(7));
            EventSystem.current.SetSelectedGameObject(secondary.gameObject);yield return null;
            ExecuteEvents.Execute(primary.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
            Assert.That(primary.GetComponent<MenuPresentation>().FocusVisible,Is.False);
            Assert.That(secondary.GetComponent<MenuPresentation>().FocusVisible,Is.True);
            secondary.interactable=false;yield return null;
            Assert.That(secondary.GetComponent<MenuPresentation>().FocusVisible,Is.False,"Disabled controls never show navigation focus");
        }
        [UnityTest] public IEnumerator GoldStartAndSettingsButtonsUseTheSameFocusFrame()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
            try
            {
                B("main-action-0").onClick.Invoke();B("setup-next").onClick.Invoke();B("setup-next").onClick.Invoke();yield return null;
                var start=(Button)typeof(ProvingGround).GetField("start",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(ground);
                var fill=start.GetComponent<Image>().color;
                start.GetComponent<MenuPresentation>().SetFocused(true);yield return null;
                Assert.That(start.GetComponent<MenuPresentation>().FocusVisible,Is.True);
                Assert.That(start.GetComponentInChildren<MenuFocusGraphic>(),Is.Not.Null);
                Assert.That(start.GetComponent<Image>().color,Is.EqualTo(fill));
                B("Назад к главному меню").onClick.Invoke();B("main-action-4").onClick.Invoke();yield return null;
                for(int section=0;section<4;section++)
                {
                    B("settings-section-"+section).onClick.Invoke();yield return null;
                    var buttons=ground.GetComponentsInChildren<Button>().Where(b=>b.name.StartsWith("settings-")&&!b.name.StartsWith("settings-section-")).ToArray();
                    foreach(var button in buttons)
                    {
                        if(!button.gameObject.activeInHierarchy||!button.interactable)continue;
                        EventSystem.current.SetSelectedGameObject(button.gameObject);yield return null;
                        Assert.That(button.GetComponent<MenuPresentation>().FocusVisible,Is.True,button.name);
                        Assert.That(button.GetComponentInChildren<MenuFocusGraphic>(),Is.Not.Null,button.name);
                        Assert.That(button.transform.Find("controller-focus"),Is.Null,"Button must not retain a competing legacy frame");
                    }
                }
            }
            finally{InputSystem.RemoveDevice(keyboard);InputSystem.RemoveDevice(mouse);}
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
        [UnityTest] public IEnumerator RosterUsesActionButtonHintsInsteadOfATopShortcutLegend()
        {
            B("main-action-0").onClick.Invoke();B("setup-next").onClick.Invoke();B("setup-next").onClick.Invoke();yield return null;
            Assert.That(B("roster-add-human").GetComponentInChildren<Text>().text,Is.EqualTo("+ ИГРОК · Y"));
            Assert.That(B("roster-add-bot").GetComponentInChildren<Text>().text,Is.EqualTo("+ БОТ · X"));
            StringAssert.Contains("Start",B("Начать — четыре игрока").GetComponentInChildren<Text>().text);
            StringAssert.Contains("B",B("setup-previous").GetComponentInChildren<Text>().text);
            var description=ground.GetComponentsInChildren<Text>().Single(t=>t.name=="setup-description").text;
            Assert.That(description,Is.EqualTo("Соберите участников матча."));
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
