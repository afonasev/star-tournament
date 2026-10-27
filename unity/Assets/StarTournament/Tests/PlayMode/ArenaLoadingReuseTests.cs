using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ArenaLoadingReuseTests
    {
        Scene scene;
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator ReusePreservesGeometryNavigationAndResetsLiftsWhileInvalidatingChanges()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            var build=typeof(ProvingGround).GetMethod("BuildSelectedArenaSteps",BindingFlags.Instance|BindingFlags.NonPublic);
            var selected=typeof(ProvingGround).GetProperty("SelectedMapId");
            foreach(var id in new[]{CombatBowlCatalog.Id,LunarLaboratoryCatalog.Id,IndustrialTunnelsCatalog.Id,WellArenaCatalog.Id})
            {
                selected.SetValue(ground,id);yield return (IEnumerator)build.Invoke(ground,new object[]{true});
                var arena=ground.GetComponentInChildren<ProvingArena>();var nav=arena.Surface.navMeshData;
                var geometry=JsonUtility.ToJson(arena.Definition);
                var meshes=arena.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh.GetInstanceID()).ToArray();
                var spawn=arena.Spawns[0];arena.Spawns[0]=Vector3.one*999;
                string initialLifts=JsonUtility.ToJson(new LiftSnapshot{States=arena.Lifts?.Capture()});
                if(arena.Lifts!=null){var states=arena.Lifts.Capture();states[0].IdleSeconds=1;arena.Lifts.Restore(states);}
                yield return (IEnumerator)build.Invoke(ground,new object[]{true});
                Assert.That(arena.Surface.navMeshData,Is.SameAs(nav));
                Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(geometry));
                Assert.That(arena.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh.GetInstanceID()),Is.EqualTo(meshes));
                Assert.That(arena.Spawns[0],Is.EqualTo(spawn));
                Assert.That(JsonUtility.ToJson(new LiftSnapshot{States=arena.Lifts?.Capture()}),Is.EqualTo(initialLifts));
            }
            selected.SetValue(ground,CombatBowlCatalog.Id);yield return (IEnumerator)build.Invoke(ground,new object[]{true});
            var bowl=ground.GetComponentInChildren<ProvingArena>();var before=bowl.Surface.navMeshData;
            ground.OrbitalLeagueProfile.Set("surface.wallValue",ground.OrbitalLeagueProfile.Get("surface.wallValue")-.01f);
            yield return (IEnumerator)build.Invoke(ground,new object[]{true});Assert.That(bowl.Surface.navMeshData,Is.Not.SameAs(before));
            before=bowl.Surface.navMeshData;
            ground.Profile.Set("player.movement.slopeLimitDegrees",ground.Profile.Get("player.movement.slopeLimitDegrees")-1);
            yield return (IEnumerator)build.Invoke(ground,new object[]{true});Assert.That(bowl.Surface.navMeshData,Is.Not.SameAs(before));
            before=bowl.Surface.navMeshData;bowl.ClearProjection();
            yield return (IEnumerator)build.Invoke(ground,new object[]{true});Assert.That(bowl.Surface.navMeshData,Is.Not.SameAs(before));
            bowl.Definition.Solids[0].Id="tampered";before=bowl.Surface.navMeshData;
            yield return (IEnumerator)build.Invoke(ground,new object[]{true});Assert.That(bowl.Surface.navMeshData,Is.Not.SameAs(before));
            Assert.That(bowl.Definition.Solids[0].Id,Is.Not.EqualTo("tampered"));
        }
        [System.Serializable] sealed class LiftSnapshot {public WellLiftState[] States;}
    }
}
