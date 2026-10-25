using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ArenaMeshResidencyVisualTests
    {
        [UnityTest, Explicit("Focused graphical Editor diagnostic; requires a GPU and STAR_TOURNAMENT_RESIDENCY_EVIDENCE output directory; excluded from default headless runs")]
        public IEnumerator BulkReleasePairedControlsReportNoiseAndPreserveLiveMeshState()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RESIDENCY_EVIDENCE");
            Assert.That(directory,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(directory);
            var samplesPath=Path.Combine(directory,"paired-render-samples.csv");
            File.WriteAllText(samplesPath,"map,phase,mode,sample,pixels_changed_vs_previous_same_mode,pixels_changed_vs_first_same_mode,sha256\n");
            var statePath=Path.Combine(directory,"live-state.csv");
            File.WriteAllText(statePath,"map,phase,live_mesh_count,nonreadable_mesh_count,live_mesh_bytes,geometry_and_reference_sha256\n");
            var boundaryPath=Path.Combine(directory,"phase-boundary.csv");
            File.WriteAllText(boundaryPath,"map,mode,control_last_sha256,post_first_sha256,pixels_changed_control_to_post,exact_hash_match\n");
            const int sampleCount=6;
            foreach(var map in new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id})
            {
                var root=new GameObject("Mesh residency paired control "+map);var arena=root.AddComponent<ProvingArena>();
                var cameraObject=new GameObject("Mesh residency paired frozen camera");Camera camera=null;
                var target=new RenderTexture(640,360,24);target.Create();var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                var lightObject=new GameObject("Mesh residency paired directional light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;
                light.intensity=map==WellArenaCatalog.Id?ProvingProfile.CreateWellDefault().Get("well.daylight"):1;lightObject.transform.rotation=Quaternion.Euler(45,-30,0);
                try
                {
                    camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.farClipPlane=300;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);cameraObject.AddComponent<UniversalAdditionalCameraData>().antialiasing=AntialiasingMode.None;
                    var profile=ProvingProfile.CreateDefault();ArenaMeshResidency.PruningEnabled=false;
                    arena.Build(AuthoredArenaCatalog.Freeze(map,profile),profile);
                    foreach(var behavior in root.GetComponentsInChildren<MonoBehaviour>(true))behavior.enabled=false;
                    var optimizer=root.GetComponent<ArenaRenderChunks>();Assert.That(optimizer,Is.Not.Null,map);
                    camera.transform.position=Eye(map,AuthoredArenaCatalog.Freeze(map,profile).Definition.Spawns);camera.transform.LookAt(Look(map));camera.targetTexture=target;
                    yield return null;
                    var candidates=ArenaMeshResidency.ReadUnreferencedOwnedMeshes(root.transform);
                    var candidateIds=new HashSet<int>(candidates.Select(mesh=>mesh.GetInstanceID()));
                    var previousByMode=new Dictionary<string,Color32[]>();var firstByMode=new Dictionary<string,Color32[]>();
                    for(var sample=0;sample<sampleCount;sample++)
                    {
                        optimizer.UseChunks(false);yield return null;
                        RecordSample("control","source",sample,Capture(camera,target,pixels,previous),previousByMode,firstByMode,samplesPath,map);
                        optimizer.UseChunks(true);yield return null;
                        RecordSample("control","chunks",sample,Capture(camera,target,pixels,previous),previousByMode,firstByMode,samplesPath,map);
                    }
                    var beforeState=WriteLiveState(statePath,map,"before_bulk_release",root.transform,candidateIds);
                    var stableControlLast=new Dictionary<string,Color32[]>(previousByMode);
                    foreach(var mode in new[]{"source","chunks"})
                        SaveImage(pixels,stableControlLast[mode],Path.Combine(directory,map+"-control-last-"+mode+".png"));
                    ArenaMeshResidency.PruningEnabled=true;
                    var released=ArenaMeshResidency.ReleaseUnreferencedOwnedMeshes(root.transform);
                    Assert.That(released,Is.EqualTo(candidates.Length),map+" production release candidate count changed");
                    yield return null;
                    previousByMode.Clear();firstByMode.Clear();
                    var firstPostByMode=new Dictionary<string,Color32[]>();
                    for(var sample=0;sample<sampleCount;sample++)
                    {
                        optimizer.UseChunks(false);yield return null;
                        var sourceSample=Capture(camera,target,pixels,previous);RecordSample("after_bulk_release","source",sample,sourceSample,previousByMode,firstByMode,samplesPath,map);
                        if(!firstPostByMode.ContainsKey("source")){firstPostByMode["source"]=sourceSample;SaveImage(pixels,sourceSample,Path.Combine(directory,map+"-post-first-source.png"));}
                        optimizer.UseChunks(true);yield return null;
                        var chunkSample=Capture(camera,target,pixels,previous);RecordSample("after_bulk_release","chunks",sample,chunkSample,previousByMode,firstByMode,samplesPath,map);
                        if(!firstPostByMode.ContainsKey("chunks")){firstPostByMode["chunks"]=chunkSample;SaveImage(pixels,chunkSample,Path.Combine(directory,map+"-post-first-chunks.png"));}
                    }
                    var afterState=WriteLiveState(statePath,map,"after",root.transform,candidateIds);
                    Assert.That(afterState.Hash,Is.EqualTo(beforeState.Hash),map+" live mesh geometry, transforms, materials, renderer/filter/collider references must remain identical after pruning");
                    Assert.That(afterState.MeshCount,Is.EqualTo(beforeState.MeshCount),map+" live mesh count must remain unchanged");
                    Assert.That(afterState.Bytes,Is.EqualTo(beforeState.Bytes),map+" live mesh bytes must remain unchanged");
                    foreach(var mode in new[]{"source","chunks"})
                    {
                        var controlHash=ImageHash(stableControlLast[mode]);var postHash=ImageHash(firstPostByMode[mode]);
                        File.AppendAllText(boundaryPath,string.Join(",",Csv(map),Csv(mode),controlHash,postHash,ChangedPixels(stableControlLast[mode],firstPostByMode[mode]),controlHash==postHash)+"\n");
                    }
                    UnityEngine.Debug.Log($"ARENA_MESH_RESIDENCY_PAIRED|map={map}|samples_per_mode_phase={sampleCount}|release_candidates={released}|live_mesh_bytes_before_after={beforeState.Bytes}/{afterState.Bytes}|nonreadable_live_meshes={afterState.NonReadableCount}|live_state_sha256={afterState.Hash}");
                }
                finally
                {
                    ArenaMeshResidency.PruningEnabled=true;RenderTexture.active=previous;if(camera)camera.targetTexture=null;
                    target.Release();Object.Destroy(pixels);Object.Destroy(target);Object.Destroy(cameraObject);Object.Destroy(lightObject);Object.Destroy(root);
                }
                yield return null;
            }
        }

        static (int MeshCount,int NonReadableCount,long Bytes,string Hash) WriteLiveState(string path,string map,string phase,Transform arena,HashSet<int> excludedIds)
        {
            var state=LiveState(arena,excludedIds);
            File.AppendAllText(path,string.Join(",",Csv(map),Csv(phase),state.MeshCount,state.NonReadableCount,state.Bytes,state.Hash)+"\n");
            return state;
        }

        static (int MeshCount,int NonReadableCount,long Bytes,string Hash) LiveState(Transform arena,HashSet<int> excludedIds)
        {
            var live=ArenaMeshResidency.ReadInclusiveOwnedMeshes(arena).Where(mesh=>!excludedIds.Contains(mesh.GetInstanceID())).OrderBy(mesh=>mesh.GetInstanceID()).ToArray();var nonreadable=live.Count(mesh=>!mesh.isReadable);
            using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                foreach(var mesh in live)
                {
                    writer.Write(mesh.GetInstanceID());writer.Write(mesh.isReadable);writer.Write(mesh.vertexCount);writer.Write(mesh.subMeshCount);
                    writer.Write(mesh.bounds.center.x);writer.Write(mesh.bounds.center.y);writer.Write(mesh.bounds.center.z);writer.Write(mesh.bounds.extents.x);writer.Write(mesh.bounds.extents.y);writer.Write(mesh.bounds.extents.z);
                    if(mesh.isReadable)
                    {
                        WriteVectors(writer,mesh.vertices);WriteVectors(writer,mesh.normals);WriteVectors(writer,mesh.tangents);WriteVectors(writer,mesh.colors);
                        for(var channel=0;channel<8;channel++){var uv=new List<Vector4>();mesh.GetUVs(channel,uv);WriteVectors(writer,uv);}
                        for(var submesh=0;submesh<mesh.subMeshCount;submesh++){writer.Write((int)mesh.GetTopology(submesh));var indices=mesh.GetIndices(submesh);writer.Write(indices.Length);foreach(var index in indices)writer.Write(index);}
                    }
                }
                foreach(var filter in arena.GetComponentsInChildren<MeshFilter>(true).OrderBy(item=>item.GetInstanceID()))
                    if(filter.sharedMesh&&!excludedIds.Contains(filter.sharedMesh.GetInstanceID())){writer.Write(filter.GetInstanceID());writer.Write(filter.sharedMesh.GetInstanceID());WriteMatrix(writer,filter.transform.localToWorldMatrix);}
                foreach(var renderer in arena.GetComponentsInChildren<MeshRenderer>(true).OrderBy(item=>item.GetInstanceID()))
                {
                    if(renderer.additionalVertexStreams&&!excludedIds.Contains(renderer.additionalVertexStreams.GetInstanceID())){writer.Write(renderer.GetInstanceID());writer.Write(renderer.additionalVertexStreams.GetInstanceID());}
                    writer.Write(renderer.GetInstanceID());foreach(var material in renderer.sharedMaterials)writer.Write(material?material.GetInstanceID():0);WriteMatrix(writer,renderer.transform.localToWorldMatrix);
                }
                foreach(var collider in arena.GetComponentsInChildren<MeshCollider>(true).OrderBy(item=>item.GetInstanceID()))
                    if(collider.sharedMesh&&!excludedIds.Contains(collider.sharedMesh.GetInstanceID())){writer.Write(collider.GetInstanceID());writer.Write(collider.sharedMesh.GetInstanceID());writer.Write(collider.enabled);}
                foreach(var renderer in arena.GetComponentsInChildren<SkinnedMeshRenderer>(true).OrderBy(item=>item.GetInstanceID()))
                    if(renderer.sharedMesh&&!excludedIds.Contains(renderer.sharedMesh.GetInstanceID())){writer.Write(renderer.GetInstanceID());writer.Write(renderer.sharedMesh.GetInstanceID());}
                writer.Flush();return (live.Length,nonreadable,live.Sum(mesh=>(long)UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh)),Sha256(stream.ToArray()));
            }
        }

        static void WriteVectors<T>(BinaryWriter writer,IList<T> values) where T:struct
        {
            writer.Write(values.Count);
            foreach(var value in values)
            {
                if(value is Vector2 v2){writer.Write(v2.x);writer.Write(v2.y);}
                else if(value is Vector3 v3){writer.Write(v3.x);writer.Write(v3.y);writer.Write(v3.z);}
                else if(value is Vector4 v4){writer.Write(v4.x);writer.Write(v4.y);writer.Write(v4.z);writer.Write(v4.w);}
                else if(value is Color color){writer.Write(color.r);writer.Write(color.g);writer.Write(color.b);writer.Write(color.a);}
            }
        }
        static void WriteMatrix(BinaryWriter writer,Matrix4x4 matrix){for(var i=0;i<16;i++)writer.Write(matrix[i]);}
        static void SaveImage(Texture2D pixels,Color32[] image,string path){pixels.SetPixels32(image);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());}
        static void RecordSample(string phase,string mode,int sample,Color32[] image,Dictionary<string,Color32[]> previousByMode,Dictionary<string,Color32[]> firstByMode,string path,string map)
        {
            var previous=previousByMode.TryGetValue(mode,out var prior)?prior:null;var first=firstByMode.TryGetValue(mode,out var initial)?initial:image;
            var hash=ImageHash(image);
            File.AppendAllText(path,string.Join(",",Csv(map),Csv(phase),Csv(mode),sample,previous==null?0:ChangedPixels(previous,image),ChangedPixels(first,image),hash)+"\n");
            previousByMode[mode]=image;firstByMode[mode]=first;
        }
        static string ImageHash(Color32[] image){var bytes=new byte[image.Length*4];for(var i=0;i<image.Length;i++){var offset=i*4;bytes[offset]=image[i].r;bytes[offset+1]=image[i].g;bytes[offset+2]=image[i].b;bytes[offset+3]=image[i].a;}return Sha256(bytes);}
        static string Sha256(byte[] bytes){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(bytes).Select(value=>value.ToString("x2",CultureInfo.InvariantCulture)));}

        [UnityTest, Explicit("Focused graphical Editor diagnostic; requires a GPU and STAR_TOURNAMENT_RESIDENCY_EVIDENCE output directory; excluded from default headless runs")]
        public IEnumerator PruningPreservesSourceFallbackAndChunkPixelsForAllArenas()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RESIDENCY_EVIDENCE");
            Assert.That(directory,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(directory);
            var inventory=Path.Combine(directory,"inclusive-mesh-inventory.csv");
            File.WriteAllText(inventory,"map,phase,mesh_instance_id,name,mesh_bytes,vertices,readable,filter_refs,mesh_collider_refs,skinned_refs,additional_stream_refs\n");
            var memory=Path.Combine(directory,"memory-phases.csv");
            File.WriteAllText(memory,"map,phase,unique_mesh_count,inclusive_mesh_bytes,unity_allocated,unity_reserved,unity_unused_reserved,system_memory_capacity_mb,os_available_page_estimate_bytes,os_free_percent\n");
            var candidates=Path.Combine(directory,"released-intermediate-meshes.csv");
            File.WriteAllText(candidates,"map,mesh_instance_id,name,mesh_bytes,vertices,readable\n");
            var sensitivity=Path.Combine(directory,"release-pixel-sensitivity.csv");
            File.WriteAllText(sensitivity,"map,control_repeat_changed_pixels,bulk_release_changed_pixels\n");
            var maps=new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id};
            foreach(var map in maps)
            {
                var root=new GameObject("Mesh residency visual "+map);var arena=root.AddComponent<ProvingArena>();
                Camera camera=null;var cameraObject=new GameObject("Mesh residency frozen camera");
                var target=new RenderTexture(640,360,24);target.Create();var pixels=new Texture2D(640,360,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                var lightObject=new GameObject("Mesh residency directional light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;
                light.intensity=map==WellArenaCatalog.Id?ProvingProfile.CreateWellDefault().Get("well.daylight"):1;lightObject.transform.rotation=Quaternion.Euler(45,-30,0);
                try
                {
                    camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.farClipPlane=300;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);cameraObject.AddComponent<UniversalAdditionalCameraData>().antialiasing=AntialiasingMode.None;
                    camera.transform.position=Eye(map,AuthoredArenaCatalog.Freeze(map,ProvingProfile.CreateDefault()).Definition.Spawns);camera.transform.LookAt(Look(map));camera.targetTexture=target;
                    var profile=ProvingProfile.CreateDefault();ArenaMeshResidency.PruningEnabled=false;
                    arena.Build(AuthoredArenaCatalog.Freeze(map,profile),profile);
                    foreach(var behavior in root.GetComponentsInChildren<MonoBehaviour>(true))behavior.enabled=false;
                    yield return null;
                    var optimizer=root.GetComponent<ArenaRenderChunks>();Assert.That(optimizer,Is.Not.Null,map);
                    optimizer.UseChunks(false);yield return null;var sourceBefore=Capture(camera,target,pixels,previous);
                    File.WriteAllBytes(Path.Combine(directory,map+"-before-source-fallback.png"),pixels.EncodeToPNG());
                    optimizer.UseChunks(true);yield return null;var chunksBefore=Capture(camera,target,pixels,previous);
                    File.WriteAllBytes(Path.Combine(directory,map+"-before-chunks.png"),pixels.EncodeToPNG());
                    optimizer.UseChunks(false);yield return null;var sourceBeforeRepeat=Capture(camera,target,pixels,previous);
                    File.WriteAllBytes(Path.Combine(directory,map+"-before-source-repeat.png"),pixels.EncodeToPNG());
                    optimizer.UseChunks(true);yield return null;
                    var before=Snapshot(root.transform,map,"before-prune",inventory,memory);

                    var released=ArenaMeshResidency.ReadUnreferencedOwnedMeshes(root.transform);
                    foreach(var mesh in released)
                        File.AppendAllText(candidates,string.Join(",",Csv(map),mesh.GetInstanceID(),Csv(mesh.name),UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh),mesh.vertexCount,mesh.isReadable)+"\n");
                    ArenaMeshResidency.PruningEnabled=true;
                    var releasedCount=ArenaMeshResidency.ReleaseUnreferencedOwnedMeshes(root.transform);
                    Assert.That(ArenaMeshResidency.ReadUnreferencedOwnedMeshes(root.transform),Is.Empty,map+" must not retain unreferenced owned meshes");
                    yield return null;
                    optimizer.UseChunks(false);yield return null;
                    var sourceAfter=Capture(camera,target,pixels,previous);File.WriteAllBytes(Path.Combine(directory,map+"-after-source-fallback.png"),pixels.EncodeToPNG());
                    optimizer.UseChunks(true);yield return null;var chunksAfter=Capture(camera,target,pixels,previous);File.WriteAllBytes(Path.Combine(directory,map+"-after-chunks.png"),pixels.EncodeToPNG());
                    var after=Snapshot(root.transform,map,"after-prune",inventory,memory);
                    File.AppendAllText(sensitivity,string.Join(",",Csv(map),ChangedPixels(sourceBefore,sourceBeforeRepeat),ChangedPixels(sourceBeforeRepeat,sourceAfter))+"\n");
                    Assert.That(sourceAfter,Is.EqualTo(sourceBeforeRepeat),map+" pruning must preserve source fallback pixels");
                    Assert.That(chunksAfter,Is.EqualTo(chunksBefore),map+" pruning must preserve chunked pixels");
                    UnityEngine.Debug.Log($"ARENA_MESH_RESIDENCY_VISUAL|map={map}|resolution=640x360|pruned_meshes={releasedCount}|mesh_bytes_before={before.Bytes}|mesh_bytes_after={after.Bytes}|source_repeat_changed_pixels={ChangedPixels(sourceBefore,sourceBeforeRepeat)}|source_prune_changed_pixels={ChangedPixels(sourceBeforeRepeat,sourceAfter)}|chunk_prune_changed_pixels={ChangedPixels(chunksBefore,chunksAfter)}|baseline_source_vs_chunk_changed_pixels={ChangedPixels(sourceBefore,chunksBefore)}");
                }
                finally
                {
                    ArenaMeshResidency.PruningEnabled=true;RenderTexture.active=previous;if(camera)camera.targetTexture=null;target.Release();Object.Destroy(pixels);Object.Destroy(target);Object.Destroy(cameraObject);Object.Destroy(lightObject);Object.Destroy(root);
                }
                yield return null;
            }
        }

        sealed class MeshSnapshot { public long Bytes; }

        static MeshSnapshot Snapshot(Transform arena,string map,string phase,string inventoryPath,string memoryPath)
        {
            var meshes=ArenaMeshResidency.ReadInclusiveOwnedMeshes(arena);
            var filters=arena.GetComponentsInChildren<MeshFilter>(true).Where(filter=>filter.sharedMesh).GroupBy(filter=>filter.sharedMesh.GetInstanceID()).ToDictionary(group=>group.Key,group=>group.Count());
            var colliders=arena.GetComponentsInChildren<MeshCollider>(true).Where(collider=>collider.sharedMesh).GroupBy(collider=>collider.sharedMesh.GetInstanceID()).ToDictionary(group=>group.Key,group=>group.Count());
            var skinned=arena.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(renderer=>renderer.sharedMesh).GroupBy(renderer=>renderer.sharedMesh.GetInstanceID()).ToDictionary(group=>group.Key,group=>group.Count());
            var streams=arena.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer.additionalVertexStreams).GroupBy(renderer=>renderer.additionalVertexStreams.GetInstanceID()).ToDictionary(group=>group.Key,group=>group.Count());
            var bytes=meshes.Sum(mesh=>(long)UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh));
            foreach(var mesh in meshes)
            {
                int id=mesh.GetInstanceID();
                File.AppendAllText(inventoryPath,string.Join(",",Csv(map),Csv(phase),id,Csv(mesh.name),UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh),mesh.vertexCount,mesh.isReadable,Get(filters,id),Get(colliders,id),Get(skinned,id),Get(streams,id))+"\n");
            }
            var os=ReadOsMemory();
            File.AppendAllText(memoryPath,string.Join(",",Csv(map),Csv(phase),meshes.Length,bytes,UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong(),UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong(),UnityEngine.Profiling.Profiler.GetTotalUnusedReservedMemoryLong(),SystemInfo.systemMemorySize,Csv(os.AvailableBytes),Csv(os.FreePercent))+"\n");
            return new MeshSnapshot{Bytes=bytes};
        }

        static int Get(Dictionary<int,int> values,int id)=>values.TryGetValue(id,out var count)?count:0;

        static (string AvailableBytes,string FreePercent) ReadOsMemory()
        {
            if(Application.platform!=RuntimePlatform.OSXEditor)return ("unavailable","unavailable");
            try
            {
                string vm=Run("/usr/bin/vm_stat");
                long pageSize=long.Parse(Regex.Match(vm,@"page size of ([0-9]+) bytes").Groups[1].Value,CultureInfo.InvariantCulture);
                long Count(string key)=>long.Parse(Regex.Match(vm,@"Pages "+key+@":\s+([0-9]+)").Groups[1].Value,CultureInfo.InvariantCulture);
                long available=(Count("free")+Count("inactive")+Count("speculative"))*pageSize;
                string pressure=Run("/usr/bin/memory_pressure","-Q");
                string percent=Regex.Match(pressure,@"free percentage:\s*([0-9]+)%").Groups[1].Value;
                return (available.ToString(CultureInfo.InvariantCulture),percent);
            }
            catch{return ("unavailable","unavailable");}
        }

        static string Run(string path,string args="")
        {
            using(var process=Process.Start(new ProcessStartInfo(path,args){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}))
            {var output=process.StandardOutput.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new InvalidOperationException(path+" exited "+process.ExitCode);return output;}
        }

        static Color32[] Capture(Camera camera,RenderTexture target,Texture2D pixels,RenderTexture previous)
        {
            RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
            RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            var result=pixels.GetPixels32();RenderTexture.active=previous;return result;
        }

        static int ChangedPixels(Color32[] before,Color32[] after)=>before.Where((pixel,index)=>!pixel.Equals(after[index])).Count();
        static string Csv(string value)=>"\""+(value??string.Empty).Replace("\"","\"\"")+"\"";

        static Vector3 Eye(string map,Vector3[] spawns)
        {
            if(map==CombatBowlCatalog.Id)return new Vector3(0,1.7f,-8);
            if(map==IndustrialTunnelsCatalog.Id)return spawns[0]+Vector3.up*1.7f;
            if(map==LunarLaboratoryCatalog.Id)return new Vector3(0,1.7f,-9);
            return new Vector3(7,1.7f,-7);
        }

        static Vector3 Look(string map)
        {
            if(map==CombatBowlCatalog.Id)return new Vector3(0,1.7f,8);
            if(map==IndustrialTunnelsCatalog.Id)return new Vector3(-16,1.1f,13);
            if(map==LunarLaboratoryCatalog.Id)return new Vector3(0,1.7f,9);
            return new Vector3(-5,12,5);
        }
    }
}
