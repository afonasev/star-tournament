using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ShotOriginTests
    {
        Scene scene;
        [UnityTest]public IEnumerator WeaponsUseCurrentMuzzlesAcrossStrafeRotationAndBatchedTicks()
        {
            yield return NativeLoadingTestScene.Load();
            scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            var review=ground.gameObject.AddComponent<NativeShotOriginReview>();
            yield return review.Run(ground);
        }
        [UnityTearDown]public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
    }
}
