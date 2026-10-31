using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class FirstMapLoadingTests
    {
        [UnityTest] public IEnumerator FreshProjectionMeasurements()
        {
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            for(int sample=0;sample<3;sample++)
            {
                var root=new GameObject("fresh-load-"+map);var arena=root.AddComponent<ProvingArena>();var movement=ProvingProfile.CreateDefault();
                try
                {
                    var watch=System.Diagnostics.Stopwatch.StartNew();var frozen=AuthoredArenaCatalog.Freeze(map,movement);double freeze=watch.Elapsed.TotalMilliseconds;
                    watch.Restart();int start=Time.frameCount;
                    yield return arena.BuildSteps(frozen,movement);
                    Debug.Log($"FRESH_MAP_LOAD map={map} sample={sample} freeze_ms={freeze:F3} build_ms={watch.Elapsed.TotalMilliseconds:F3} frames={Time.frameCount-start} solids={arena.Definition.Solids.Length} renderers={root.GetComponentsInChildren<Renderer>().Length}");
                    Assert.That(arena.Surface.navMeshData,Is.Not.Null);frozen.RequireDefinition(arena.Definition);
                }
                finally{Object.Destroy(root);}yield return null;
            }
        }
    }
}
