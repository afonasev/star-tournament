using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class LoadingPerformanceTests
    {
        Scene scene;
        [UnityTearDown] public IEnumerator Cleanup(){if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator StartupAndMapPreparationProbe()
        {
            var watch=System.Diagnostics.Stopwatch.StartNew();int frame=Time.frameCount;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(x=>x.GetComponentsInChildren<ProvingGround>()).Single();
            Debug.Log($"LOADING_PROBE startup ms={watch.Elapsed.TotalMilliseconds:F1} frames={Time.frameCount-frame}");
            var build=typeof(ProvingGround).GetMethod("BuildSelectedArenaSteps",BindingFlags.Instance|BindingFlags.NonPublic);
            var selected=typeof(ProvingGround).GetProperty("SelectedMapId");
            foreach(var id in new[]{CombatBowlCatalog.Id,LunarLaboratoryCatalog.Id,IndustrialTunnelsCatalog.Id,WellArenaCatalog.Id})
            {
                selected.SetValue(ground,id);
                for(int repeat=0;repeat<2;repeat++)
                {
                    watch.Restart();frame=Time.frameCount;
                    yield return (IEnumerator)build.Invoke(ground,new object[]{true});
                    var arena=ground.GetComponentInChildren<ProvingArena>();
                    Assert.That(arena.Surface.navMeshData,Is.Not.Null);Assert.That(arena.Definition.MapId,Is.EqualTo(id));
                    Debug.Log($"LOADING_PROBE map={id} repeat={repeat} ms={watch.Elapsed.TotalMilliseconds:F1} frames={Time.frameCount-frame} solids={arena.Definition.Solids.Length} renderers={arena.GetComponentsInChildren<Renderer>().Length}");
                }
            }
        }
    }
}
