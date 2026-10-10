using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    // Preserve all scene/runtime assertions; wait for real bootstrap, never a fixed frame count.
    static class NativeLoadingTestScene
    {
        public static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("ProvingGround",LoadSceneMode.Additive);
            var scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            yield return Wait(ground);
        }
        public static IEnumerator Wait(ProvingGround ground)
        {
            float deadline=Time.realtimeSinceStartup+120;
            while(!ground.IsReady||ground.IsLoading)
            {
                Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline),"Loading did not become ready within 120 seconds");
                yield return null;
            }
        }
    }
}
