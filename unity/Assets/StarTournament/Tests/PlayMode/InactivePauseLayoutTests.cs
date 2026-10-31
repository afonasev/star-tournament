using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class PauseLayoutDimensionsProbe : UIBehaviour
    {
        public int Changes;
        protected override void OnRectTransformDimensionsChange(){Changes++;}
    }
    public sealed class InactivePauseLayoutTests
    {
        Scene scene;
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator InactivePauseCardsDoNotInvalidateLayoutDuringRefresh()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            const BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
            var cards=(GameObject[])typeof(ProvingGround).GetField("seatPauseCards",flags).GetValue(ground);
            var roots=(GameObject[])typeof(ProvingGround).GetField("seatPauseRoots",flags).GetValue(ground);
            var refresh=typeof(ProvingGround).GetMethod("RefreshSeatPauseUi",flags);
            Assert.AreEqual(4,cards.Length);
            var probes=new PauseLayoutDimensionsProbe[4];
            for(int seat=0;seat<4;seat++)
            {
                Assert.IsNotNull(cards[seat]);Assert.IsFalse(roots[seat].activeSelf);
                probes[seat]=cards[seat].AddComponent<PauseLayoutDimensionsProbe>();
            }
            // Warm once to reach the already-authored compact layout, then observe
            // repeated inactive refreshes. No frame/time budget or gameplay is mocked.
            refresh.Invoke(ground,null);
            for(int seat=0;seat<4;seat++){probes[seat].Changes=0;cards[seat].transform.hasChanged=false;}
            for(int n=0;n<3;n++)refresh.Invoke(ground,null);
            for(int seat=0;seat<4;seat++)
            {
                TestContext.WriteLine("INACTIVE_PAUSE_LAYOUT seat="+seat+" dimensions="+probes[seat].Changes+" changed="+cards[seat].transform.hasChanged);
                Assert.IsFalse(roots[seat].activeSelf);
            }
            Assert.That(probes.Select(p=>p.Changes).ToArray(),Is.All.EqualTo(0),"Hidden pause refresh must not reflow any card");
            Assert.That(cards.Select(c=>c.transform.hasChanged).ToArray(),Is.All.False,"Hidden cards must remain geometrically untouched");
        }
    }
}
