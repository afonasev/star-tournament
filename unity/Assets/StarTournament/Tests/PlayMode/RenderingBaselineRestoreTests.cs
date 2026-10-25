using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class RenderingBaselineRestoreTests
    {
        Scene scene;GameObject owner;
        [UnityTearDown] public IEnumerator Cleanup(){if(owner)Object.Destroy(owner);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [Test] public void AssetObserverIncludesOwnerOnlyMeshAndDoesNotReleaseIt()
        {
            owner=new GameObject("owned-observer-test");var mesh=new Mesh{name="rendered"};var unused=new Mesh{name="owner-only"};
            try
            {
                mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.triangles=new[]{0,1,2};
                unused.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};unused.triangles=new[]{0,1,2};
                owner.AddComponent<MeshFilter>().sharedMesh=mesh;var source=owner.AddComponent<OrbitalLeaguePresentation>();
                var owned=(System.Collections.Generic.List<Object>)typeof(OrbitalLeaguePresentation).GetField("owned",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(source);owned.Add(mesh);owned.Add(unused);
                var before=NativeRenderingBaseline.ReadAssetScope(owner.transform);var after=NativeRenderingBaseline.ReadAssetScope(owner.transform);
                Assert.AreEqual(2,before.inclusiveOwnerMeshCount);Assert.AreEqual(1,before.unreferencedOwnerMeshCount);
                Assert.AreEqual(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh)+UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(unused),before.inclusiveOwnerMeshBytes);
                Assert.AreEqual(UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(unused),before.unreferencedOwnerMeshBytes);
                Assert.AreEqual(before.inclusiveOwnerMeshBytes,after.inclusiveOwnerMeshBytes);Assert.IsTrue(unused);Assert.Contains(unused,owned);
                owned.Clear();
            }
            finally{Object.DestroyImmediate(mesh);Object.DestroyImmediate(unused);}
        }
        [UnityTest] public IEnumerator ReferenceRestoresExactMatchSeedPosesAndPhysicsTransforms()
        {
            scene=SceneManager.CreateScene("baseline-restore-"+Guid.NewGuid(),new CreateSceneParameters(LocalPhysicsMode.Physics3D));owner=new GameObject("owned-restore-test");SceneManager.MoveGameObjectToScene(owner,scene);
            var movement=ProvingProfile.CreateDefault();var life=ProvingProfile.CreateCombatDefault();var combat=ProvingProfile.CreateNativeCombatDefault();var tuning=ProvingProfile.CreateMatchDefault();
            var root=new GameObject("arena");root.transform.SetParent(owner.transform);var arena=root.AddComponent<ProvingArena>();arena.Build(AuthoredPhysicsFixture.Freeze(),movement);
            var motors=new CharacterMotor[2];for(int n=0;n<2;n++){var go=new GameObject("actor-"+n);go.transform.SetParent(owner.transform);motors[n]=go.AddComponent<CharacterMotor>();motors[n].Initialize(movement,new Vector3(n*3,0,0),arena);}
            var match=new NativeMatchState(2,NativeRenderingBaseline.Configuration(tuning),tuning,50);var session=new NativeCombatSession(motors,arena,scene.GetPhysicsScene(),movement,life,combat,match);
            var snapshot=session.Capture();string json=JsonUtility.ToJson(snapshot);int awardSeed=snapshot.Match.AwardSeed;
            session.Tick(new[]{new LocalAction{Move=Vector2.up},default(LocalAction)},.02f);Assert.AreNotEqual(json,JsonUtility.ToJson(session.Capture()));
            NativeRenderingBaseline.RestoreReference(session,json);Assert.AreEqual(json,JsonUtility.ToJson(session.Capture()));Assert.AreEqual(awardSeed,session.Capture().Match.AwardSeed);
            for(int n=0;n<2;n++)Assert.AreEqual(snapshot.Poses[n].Position,motors[n].transform.position);
            snapshot.ArenaIdentity="invalid-map@1";Assert.Throws<ArgumentException>(()=>NativeRenderingBaseline.RestoreReference(session,JsonUtility.ToJson(snapshot)));
            yield return null;
        }
    }
}
