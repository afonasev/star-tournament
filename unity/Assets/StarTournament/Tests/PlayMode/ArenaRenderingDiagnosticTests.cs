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

        IEnumerator Measure(string[] maps)
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
                        Assert.That(optimizer.SourceCount,Is.GreaterThan(0),map+" has no candidate source");
                        var visuals=root.AddComponent<NativeVisualPresentation>();visuals.Bind(profile,root.transform,light,new[]{camera});
                        File.WriteAllLines(Path.Combine(directory,map+"-settings.txt"),new[]{"profile="+profile.Id+"@"+profile.Version,"AA="+NativeVisualPreferences.AntialiasingLevel,"exposure="+NativeVisualPreferences.Adjustment(profile,NativeVisualPreferences.ExposurePath),"saturation="+NativeVisualPreferences.Adjustment(profile,NativeVisualPreferences.SaturationPath)}.Concat(System.Enum.GetValues(typeof(VisualEffect)).Cast<VisualEffect>().Select(effect=>effect+"="+NativeVisualPreferences.Effective(profile,effect))));
                        yield return null;
                        var all=root.GetComponentsInChildren<MeshRenderer>();
                        File.WriteAllLines(Path.Combine(directory,map+"-inventory.csv"),new[]{"name,vertices,triangles,extent,materials,enabled"}.Concat(all.Where(r=>r.GetComponent<MeshFilter>()).Select(r=>{var m=r.GetComponent<MeshFilter>().sharedMesh;return $"{r.name.Replace(',',' ')},{m.vertexCount},{(r.isPartOfStaticBatch?(long)m.GetIndexCount(r.subMeshStartIndex)/3:Enumerable.Range(0,m.subMeshCount).Sum(i=>(long)m.GetIndexCount(i)/3))},{r.bounds.size.ToString().Replace(',',';')},{r.sharedMaterials.Length},{r.enabled}";})));
                        Vector3[] eyes,looks;
                        if(map==WellArenaCatalog.Id){eyes=new[]{new Vector3(7,1.7f,-7),new Vector3(-21,9.7f,-13)};looks=new[]{new Vector3(-5,12,5),new Vector3(-21,9.7f,13)};}
                        else if(map==LunarLaboratoryCatalog.Id){eyes=new[]{new Vector3(0,1.7f,-9),new Vector3(0,1.7f,-25)};looks=new[]{new Vector3(0,1.7f,9),new Vector3(0,1.7f,0)};}
                        else if(map==IndustrialTunnelsCatalog.Id){eyes=new[]{arena.Spawns[0]+Vector3.up*1.7f,new Vector3(-20,1.1f,16)};looks=new[]{new Vector3(-16,1.1f,13),new Vector3(-14,1.1f,16)};}
                        else {eyes=new[]{new Vector3(0,1.7f,-8),new Vector3(-20,1.7f,16)};looks=new[]{new Vector3(0,1.7f,8),Vector3.up*1.7f};}
                        for(int pose=0;pose<eyes.Length;pose++)
                        {
                            cameraGo.transform.position=eyes[pose];cameraGo.transform.LookAt(looks[pose]);camera.targetTexture=target;
                            foreach(string variant in new[]{"baseline","candidate","candidate-repeat","baseline-repeat"})
                            {
                                optimizer.UseChunks(variant.StartsWith("candidate"));
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
    }
}
