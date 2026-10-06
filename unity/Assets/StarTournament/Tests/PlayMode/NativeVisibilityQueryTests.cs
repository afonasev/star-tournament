using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public class NativeVisibilityQueryTests
    {
        [UnityTest] public IEnumerator GlassWallAndSaturationInOwnedPhysicsScene()
        {
            var scene=SceneManager.CreateScene("optical-glass",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            try
            {
                var physics=scene.GetPhysicsScene();var q=new NativeVisibilityQuery(physics,4);
                for(int i=1;i<=5;i++){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(go,scene);go.layer=ProvingArena.WorldLayer;go.transform.position=new Vector3(0,1,i*2);go.transform.localScale=new Vector3(4,4,.1f);go.AddComponent<NativeSightTransparent>();}
                physics.Simulate(.02f);
                Assert.That(q.Query(Vector3.up,Vector3.forward*3),Is.EqualTo(NativeVisibilityResult.Clear));
                Assert.That(q.Query(Vector3.up,Vector3.forward*7),Is.EqualTo(NativeVisibilityResult.Clear));
                Assert.That(q.Query(Vector3.up,Vector3.forward*12),Is.EqualTo(NativeVisibilityResult.Indeterminate));
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(wall,scene);wall.layer=ProvingArena.WorldLayer;wall.transform.position=new Vector3(0,1,3);wall.transform.localScale=new Vector3(4,4,.1f);physics.Simulate(.02f);
                Assert.That(q.Query(Vector3.up,Vector3.forward*5),Is.EqualTo(NativeVisibilityResult.Occluded));
                Assert.That(physics.Raycast(Vector3.up,Vector3.forward,out var hit,3,ProvingArena.ShotMask),Is.True);Assert.That(hit.collider.GetComponent<NativeSightTransparent>(),Is.Not.Null,"glass remains first physical shot hit");
            }
            finally {SceneManager.UnloadSceneAsync(scene);}yield return null;
        }
    }
}
