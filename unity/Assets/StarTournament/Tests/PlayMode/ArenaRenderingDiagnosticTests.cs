using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class ArenaRenderingDiagnosticTests
    {
        [UnityTest, Explicit("Exclusive graphical Editor diagnostic, not gameplay/device FPS")]
        public IEnumerator LunarControl() { yield return Measure(new[]{LunarLaboratoryCatalog.Id}); }
        [UnityTest, Explicit("Exclusive graphical Editor diagnostic, not gameplay/device FPS")]
        public IEnumerator AllMapControls() { yield return Measure(new[]{CombatBowlCatalog.Id,IndustrialTunnelsCatalog.Id,LunarLaboratoryCatalog.Id,WellArenaCatalog.Id}); }
        [UnityTest, Explicit("Exclusive Well metal-detail partition comparison; Editor diagnostics, not Player FPS")]
        public IEnumerator WellControl() { yield return Measure(new[]{WellArenaCatalog.Id},true); }

        IEnumerator Measure(string[] maps,bool wellDetailPartitionControl=false)
        {
            Assert.That(SystemInfo.graphicsDeviceType, Is.Not.EqualTo(GraphicsDeviceType.Null));
            string directory = Environment.GetEnvironmentVariable("STAR_TOURNAMENT_PERF_EVIDENCE");
            Assert.That(directory, Is.Not.Null.And.Not.Empty); Directory.CreateDirectory(directory);
            var lines = new List<string>{"map,pose,variant,sample,synchronized_render_ms,frustum_triangles,unity_triangles,unity_drawcalls,renderers,source_batches,chunks"};
            File.WriteAllText(Path.Combine(directory,"host.txt"), $"EDITOR_DIAGNOSTIC_NOT_PLAYER_DEVICE_OR_ACCEPTANCE\nUnity={Application.unityVersion}\nGPU={SystemInfo.graphicsDeviceName}\nCPU={SystemInfo.processorType}\nresolution=1280x720\nquality={QualitySettings.GetQualityLevel()}\npipeline={GraphicsSettings.currentRenderPipeline?.name}\nRender timing includes SubmitRenderRequest and synchronous 1px ReadPixels, CPU/GPU not separable.\n");
            float oldTimeScale=Time.timeScale;Time.timeScale=0;
            try
            {
                foreach(string map in maps)
                {
                    var root=new GameObject("Arena controlled render");var arena=root.AddComponent<ProvingArena>();
                    var profile=ProvingProfile.CreateDefault();var preparation=Stopwatch.StartNew();arena.Build(AuthoredArenaCatalog.Freeze(map,profile),profile);preparation.Stop();
                    File.AppendAllText(Path.Combine(directory,"preparation.txt"),$"{map}: total arena build with nav+art+optimization {preparation.Elapsed.TotalMilliseconds}ms; chunks preparation {arena.GetComponent<ArenaRenderChunks>().PreparationMilliseconds}ms\n");
                    var cameraGo=new GameObject("Frozen camera");cameraGo.transform.SetParent(root.transform,false);
                    var camera=cameraGo.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.farClipPlane=300;
                    camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.8f,.87f,.94f);
                    cameraGo.AddComponent<UniversalAdditionalCameraData>();
                    var lightGo=new GameObject("Control directional light");lightGo.transform.SetParent(root.transform,false);
                    var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;light.intensity=map==WellArenaCatalog.Id?ProvingProfile.CreateWellDefault().Get("well.daylight"):1;
                    lightGo.transform.rotation=Quaternion.Euler(45,-30,0);
                    var target=new RenderTexture(1280,720,24);target.Create();var pixel=new Texture2D(1,1,TextureFormat.RGB24,false);
                    var previous=RenderTexture.active;
                    try
                    {
                        foreach(var behavior in root.GetComponentsInChildren<MonoBehaviour>())behavior.enabled=false;
                        var optimizer=root.GetComponent<ArenaRenderChunks>();
                        File.AppendAllText(Path.Combine(directory,"preparation.txt"),$"{map}: owned native mesh count={optimizer.ReadOwnedMeshes().Length}, bytes={optimizer.ReadOwnedMeshes().Sum(mesh=>UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh))}\n");
                        if(map==LunarLaboratoryCatalog.Id)
                        {
                            var presentation=root.GetComponentInChildren<LunarLaboratoryPresentation>();
                            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                            var field=typeof(LunarLaboratoryPresentation).GetField("starMesh",flags)??typeof(LunarLaboratoryPresentation).GetField("sphere",flags);
                            var template=(Mesh)field.GetValue(presentation);
                            var ownedField=typeof(LunarLaboratoryPresentation).GetField("owned",flags);
                            var presentationMeshes=((System.Collections.IEnumerable)ownedField.GetValue(presentation)).Cast<UnityEngine.Object>().OfType<Mesh>().Where(m=>m).ToArray();
                            var optimizerMeshes=optimizer.ReadOwnedMeshes().Where(m=>m).ToArray();
                            var filterMeshes=root.GetComponentsInChildren<MeshFilter>(true).Select(f=>f.sharedMesh).Where(m=>m).ToArray();
                            var allMeshes=filterMeshes.Concat(optimizerMeshes).Concat(presentationMeshes).Distinct().ToArray();
                            long Bytes(IEnumerable<Mesh> meshes)=>meshes.Distinct().Sum(m=>(long)UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(m));
                            File.WriteAllLines(Path.Combine(directory,"unique-mesh-memory.txt"),new[]
                            {
                                $"meshfilter_unique_count={filterMeshes.Distinct().Count()}",$"meshfilter_unique_bytes={Bytes(filterMeshes)}",
                                $"optimizer_owned_count={optimizerMeshes.Distinct().Count()}",$"optimizer_owned_bytes={Bytes(optimizerMeshes)}",
                                $"presentation_owned_count={presentationMeshes.Distinct().Count()}",$"presentation_owned_bytes={Bytes(presentationMeshes)}",
                                $"all_unique_mesh_count={allMeshes.Length}",$"all_unique_mesh_bytes={Bytes(allMeshes)}"
                            });
                            var whiteBatches=root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial&&r.sharedMaterial.name=="Neutral light diffuser").ToArray();
                            long templateTriangles=Enumerable.Range(0,template.subMeshCount).Sum(i=>(long)template.GetIndexCount(i)/3);
                            long batchTriangles=whiteBatches.Sum(r=>{var m=r.GetComponent<MeshFilter>()?.sharedMesh;return m?Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3):0;});
                            File.WriteAllLines(Path.Combine(directory,"lunar-star-geometry.txt"),new[]
                            {
                                "instances=450", "seed=42026", $"template={field.Name}",
                                $"template_vertices={template.vertexCount}", $"template_triangles={templateTriangles}",
                                $"template_bounds={template.bounds.extents}", $"template_runtime_bytes={UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(template)}",
                                $"star_expanded_vertices={template.vertexCount*450L}", $"star_expanded_triangles={templateTriangles*450L}",
                                $"star_expanded_indices={templateTriangles*3*450L}",
                                $"white_batch_renderer_count={whiteBatches.Length}", $"white_batch_triangles_including_other_white_geometry={batchTriangles}",
                                "material=Neutral light diffuser", "shadows=Off", "textures=none", "lod=none"
                            });
                        }
                        Assert.That(optimizer.SourceCount,Is.GreaterThan(0),map+" has no candidate source");
                        var visuals=root.AddComponent<NativeVisualPresentation>();visuals.Bind(profile,root.transform,light,new[]{camera});
                        File.WriteAllLines(Path.Combine(directory,map+"-settings.txt"),new[]{"profile="+profile.Id+"@"+profile.Version,"AA="+NativeVisualPreferences.AntialiasingLevel,"exposure="+NativeVisualPreferences.Adjustment(profile,NativeVisualPreferences.ExposurePath),"saturation="+NativeVisualPreferences.Adjustment(profile,NativeVisualPreferences.SaturationPath)}.Concat(System.Enum.GetValues(typeof(VisualEffect)).Cast<VisualEffect>().Select(effect=>effect+"="+NativeVisualPreferences.Effective(profile,effect))));
                        yield return null;
                        var all=root.GetComponentsInChildren<MeshRenderer>();
                        File.WriteAllLines(Path.Combine(directory,map+"-inventory.csv"),new[]{"name,vertices,triangles,extent,materials,enabled"}.Concat(all.Where(r=>r.GetComponent<MeshFilter>()).Select(r=>{var m=r.GetComponent<MeshFilter>().sharedMesh;return $"{r.name.Replace(',',' ')},{m.vertexCount},{(r.isPartOfStaticBatch?(long)m.GetIndexCount(r.subMeshStartIndex)/3:Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3))},{r.bounds.size.ToString().Replace(',',';')},{r.sharedMaterials.Length},{r.enabled}";})));
                        if(wellDetailPartitionControl)WriteWellDetailPartitionInventory(root,directory);
                        Vector3[] eyes,looks;
                        if(wellDetailPartitionControl){eyes=new[]{new Vector3(7,1.7f,-7),new Vector3(-21.65f,2.25f,-14.15f),new Vector3(-14.15f,WellArenaCatalog.Storey+2.25f,-21.65f),new Vector3(20,1.7f,4.8f)};looks=new[]{new Vector3(-5,12,5),new Vector3(-24.65f,2.25f,-14.15f),new Vector3(-14.15f,WellArenaCatalog.Storey+2.25f,-24.65f),new Vector3(23.9f,1,6.4f)};}
                        else if(map==WellArenaCatalog.Id){eyes=new[]{new Vector3(7,1.7f,-7),new Vector3(-21,9.7f,-13)};looks=new[]{new Vector3(-5,12,5),new Vector3(-21,9.7f,13)};}
                        else if(map==LunarLaboratoryCatalog.Id){eyes=new[]{new Vector3(0,1.7f,-9),new Vector3(0,1.7f,-25),new Vector3(0,1.7f,-25)};looks=new[]{new Vector3(0,1.7f,9),new Vector3(0,1.7f,0),new Vector3(0,100,-25)};}
                        else if(map==IndustrialTunnelsCatalog.Id){eyes=new[]{arena.Spawns[0]+Vector3.up*1.7f,new Vector3(-20,1.1f,16)};looks=new[]{new Vector3(-16,1.1f,13),new Vector3(-14,1.1f,16)};}
                        else {eyes=new[]{new Vector3(0,1.7f,-8),new Vector3(-20,1.7f,16)};looks=new[]{new Vector3(0,1.7f,8),Vector3.up*1.7f};}
                        for(int pose=0;pose<eyes.Length;pose++)
                        {
                            cameraGo.transform.position=eyes[pose];cameraGo.transform.LookAt(looks[pose]);camera.targetTexture=target;
                            foreach(string variant in new[]{"baseline","candidate","candidate-repeat","baseline-repeat"})
                            {
                                if(wellDetailPartitionControl)UseWellMetalDetailChunks(root,variant.StartsWith("baseline"));
                                else optimizer.UseChunks(variant.StartsWith("candidate"));
                                var planes=GeometryUtility.CalculateFrustumPlanes(camera);
                                long submitted=all.Where(r=>r.enabled&&GeometryUtility.TestPlanesAABB(planes,r.bounds)).Sum(r=>{var filter=r.GetComponent<MeshFilter>();var m=filter?filter.sharedMesh:null;return m?(r.isPartOfStaticBatch?(long)m.GetIndexCount(r.subMeshStartIndex)/3:Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3)):0;});
                                for(int sample=0;sample<50;sample++)
                                {
                                    var watch=Stopwatch.StartNew();
                                    RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});
                                    RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();watch.Stop();
                                    if(sample>=10)
                                    {
                                        int actualTriangles=-1,actualDraws=-1;
#if UNITY_EDITOR
                                        actualTriangles=UnityEditor.UnityStats.triangles;actualDraws=UnityEditor.UnityStats.drawCalls;
#endif
                                        lines.Add($"{map},{pose},{variant},{sample},{watch.Elapsed.TotalMilliseconds.ToString(CultureInfo.InvariantCulture)},{submitted},{actualTriangles},{actualDraws},{all.Count(r=>r.enabled)},{optimizer.SourceCount},{optimizer.ChunkCount}");
                                    }
                                    yield return null;
                                }
                                RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
                                File.WriteAllBytes(Path.Combine(directory,$"{map}-{pose}-{variant}.png"),image.EncodeToPNG());Object.Destroy(image);
                                File.WriteAllLines(Path.Combine(directory,"render.csv"),lines);
                            }
                        }
                    }
                    finally {var visual=root.GetComponent<NativeVisualPresentation>();if(visual)visual.ResetWorld();camera.targetTexture=null;RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(pixel);Object.Destroy(root);}
                    yield return null;
                }
            }
            finally {Time.timeScale=oldTimeScale;}
        }

        static void UseWellMetalDetailChunks(GameObject root,bool useChunks)
        {
            var sourceNames=new HashSet<string>{"Well details / Well satin aluminium","Well details / Well brushed metal"};
            foreach(var source in root.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>sourceNames.Contains(renderer.gameObject.name)))
            {
                source.enabled=!useChunks;
                foreach(var chunk in source.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>renderer!=source&&renderer.gameObject.name.StartsWith("render-cell ")))
                    chunk.enabled=useChunks;
            }
        }

        static string Csv(string value)=>"\""+(value??string.Empty).Replace("\"","\"\"")+"\"";
        static void WriteWellDetailPartitionInventory(GameObject root,string directory)
        {
            var sourceNames=new HashSet<string>{"Well details / Well satin aluminium","Well details / Well brushed metal"};
            var lines=new List<string>{"owner,renderer,enabled,static_batch,mesh_instance_id,vertices,triangles,mesh_bytes,material,metallic,smoothness,texture_count,has_normals,has_tangents,uv_channels"};
            foreach(var owner in root.GetComponentsInChildren<MeshRenderer>(true).Where(renderer=>sourceNames.Contains(renderer.gameObject.name)))
            foreach(var renderer in owner.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter=renderer.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var mesh=filter.sharedMesh;var material=renderer.sharedMaterial;if(!material)continue;
                long triangles=renderer.isPartOfStaticBatch?(long)mesh.GetIndexCount(renderer.subMeshStartIndex)/3:Enumerable.Range(0,mesh.subMeshCount).Sum(index=>(long)mesh.GetIndexCount(index)/3);
                int textureCount=new[]{"_BaseMap","_BumpMap","_MetallicGlossMap","_OcclusionMap","_EmissionMap"}.Select(property=>material.HasProperty(property)?material.GetTexture(property):null).Where(texture=>texture).Distinct().Count();
                var uv=Enumerable.Range(0,8).Where(channel=>mesh.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0+channel))).Select(channel=>channel.ToString(CultureInfo.InvariantCulture));
                var metallic=material.HasProperty("_Metallic")?material.GetFloat("_Metallic").ToString(CultureInfo.InvariantCulture):"NA";
                var smoothness=material.HasProperty("_Smoothness")?material.GetFloat("_Smoothness").ToString(CultureInfo.InvariantCulture):"NA";
                lines.Add(string.Join(",",Csv(owner.gameObject.name),Csv(renderer.gameObject.name),renderer.enabled,renderer.isPartOfStaticBatch,mesh.GetInstanceID(),mesh.vertexCount,triangles,UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh),Csv(material.name),metallic,smoothness,textureCount,mesh.HasVertexAttribute(VertexAttribute.Normal),mesh.HasVertexAttribute(VertexAttribute.Tangent),Csv(string.Join("|",uv))));
            }
            File.WriteAllLines(Path.Combine(directory,"well-detail-partition-inventory.csv"),lines);
        }
    }
}
