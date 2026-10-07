using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class NativeMenuPresentationTests
    {
        Scene scene;ProvingGround ground;
        Button B(string n)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==n);
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
        }
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator ApprovedMainMenuHasReadableArtworkAndSixPreservedActions()
        {
            var root=ground.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="main-menu");
            var art=root.GetComponentInChildren<RawImage>();Assert.That(art.texture,Is.Not.Null);Assert.That(art.raycastTarget,Is.False);
            Assert.That(root.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("КОМБАТ БОУЛ")||t.text.Contains("ОРБИТАЛЬНАЯ ЛИГА")||t.name=="route"),Is.False);
            for(int i=0;i<6;i++)Assert.That(B("main-action-"+i).interactable,Is.EqualTo(i!=1));
            Canvas.ForceUpdateCanvases();yield return null;
            var battle=(RectTransform)B("main-action-0").transform;
            Assert.That(battle.rect.height,Is.InRange(20,MenuPresentation.ButtonHeight+.1f));
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
    }
}
