using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ArenaRenderingLifecycleTests
    {
        [UnityTest] public IEnumerator EveryArenaRebuildReleasesOwnedMeshesAndPreservesAuthoredSolids()
        {
            foreach(string map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                var root=new GameObject("render lifecycle "+map);var arena=root.AddComponent<ProvingArena>();var profile=ProvingProfile.CreateDefault();var frozen=AuthoredArenaCatalog.Freeze(map,profile);
                try
                {
                    for(int rebuild=0;rebuild<2;rebuild++)
                    {
                        arena.Build(frozen,profile);yield return null;
                        Assert.That(ArenaMeshResidency.ReadUnreferencedOwnedMeshes(root.transform),Is.Empty,"all retained owned meshes must be renderer-, collider- or skin-referenced after build: "+map);
                        var optimizer=root.GetComponent<ArenaRenderChunks>();Assert.That(optimizer,Is.Not.Null);Assert.That(optimizer.SourceCount,Is.GreaterThan(0));
                        var meshes=optimizer.ReadOwnedMeshes();Assert.That(meshes.Length,Is.GreaterThan(0));
                        ReportResidency(root,optimizer,map,rebuild,"chunks-active");
                        Assert.That(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.isPartOfStaticBatch),Is.True);
                        frozen.RequireDefinition(arena.Definition);
                        foreach(var solid in arena.Definition.Solids)
                        {
                            var projected=root.transform.Find(solid.Id);Assert.That(projected,Is.Not.Null,solid.Id);
                            Assert.That(projected.position,Is.EqualTo(solid.Position));Assert.That(Quaternion.Angle(projected.rotation,solid.Rotation),Is.LessThan(.001f));Assert.That(projected.localScale,Is.EqualTo(solid.Size));
                            Assert.That(projected.GetComponent<Collider>(),Is.Not.Null,solid.Id);
                        }
                        foreach(var lift in arena.Definition.Lifts??System.Array.Empty<ArenaLiftDefinition>())
                            Assert.That(root.transform.Find("lift-"+lift.Id).GetComponent<MeshRenderer>().enabled,Is.True,"moving lift must retain its own renderer");
                        optimizer.UseChunks(false);yield return null;
                        Assert.That(root.GetComponentsInChildren<MeshRenderer>().Any(r=>r.enabled&&!r.isPartOfStaticBatch),Is.True,"source-renderer fallback must remain available");
                        ReportResidency(root,optimizer,map,rebuild,"source-fallback");
                        optimizer.UseChunks(true);yield return null;
                        arena.ClearProjection();yield return null;
                        Assert.That(root.transform.childCount,Is.Zero);Assert.That(meshes.All(mesh=>!mesh),Is.True,"native mesh leak after rebuild "+map);
                        Debug.Log($"ARENA_MESH_RESIDENCY|phase=clear|map={map}|rebuild={rebuild}|unity_allocated={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()}|unity_reserved={UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()}|unity_unused_reserved={UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong()}");
                    }
                }
                finally{Object.Destroy(root);}yield return null;
            }
        }

        static void ReportResidency(GameObject root,ArenaRenderChunks optimizer,string map,int rebuild,string phase)
        {
            var unique=new Dictionary<int,Mesh>();
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
                if(filter.sharedMesh)unique[filter.sharedMesh.GetInstanceID()]=filter.sharedMesh;
            // Presentation ownership lists retain source and intermediate meshes even after
            // a renderer has been disabled or its original child object has been replaced.
            foreach(var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var field=component.GetType().GetField("owned",BindingFlags.Instance|BindingFlags.NonPublic);
                if(field?.GetValue(component) is System.Collections.IEnumerable values)
                    foreach(var value in values)if(value is Mesh mesh&&mesh)unique[mesh.GetInstanceID()]=mesh;
            }
            foreach(var mesh in optimizer.ReadOwnedMeshes())if(mesh)unique[mesh.GetInstanceID()]=mesh;
            var bytes=unique.Values.Sum(mesh=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh));
            var filters=root.GetComponentsInChildren<MeshFilter>(true);
            var filterIds=new HashSet<int>(filters.Where(filter=>filter.sharedMesh).Select(filter=>filter.sharedMesh.GetInstanceID()));
            var ownedOnly=unique.Values.Where(mesh=>!filterIds.Contains(mesh.GetInstanceID())).ToArray();
            var readable=unique.Values.Count(mesh=>mesh.isReadable);
            Debug.Log($"ARENA_MESH_RESIDENCY|phase={phase}|map={map}|rebuild={rebuild}|unique_meshes={unique.Count}|inclusive_mesh_bytes={bytes}|filter_meshes={filterIds.Count}|owner_only_meshes={ownedOnly.Length}|readable_meshes={readable}|unity_allocated={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()}|unity_reserved={UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong()}|unity_unused_reserved={UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong()}|system_memory_mb={SystemInfo.systemMemorySize}|preparation_ms={optimizer.PreparationMilliseconds}");
            foreach(var mesh in unique.Values.OrderBy(mesh=>mesh.name,StringComparer.Ordinal))
                Debug.Log($"ARENA_MESH_OBJECT|phase={phase}|map={map}|rebuild={rebuild}|id={mesh.GetInstanceID()}|name={mesh.name}|bytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh)}|vertices={mesh.vertexCount}|readable={mesh.isReadable}|filter_referenced={filterIds.Contains(mesh.GetInstanceID())}");
        }
    }
}
