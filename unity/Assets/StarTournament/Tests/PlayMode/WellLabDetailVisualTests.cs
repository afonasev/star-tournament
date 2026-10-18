using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellLabDetailVisualTests
    {
        [UnityTest] public IEnumerator PreserveAuthoringAndSaveDetailViews()
        {
            var root=new GameObject("Well visual review");var movement=ProvingProfile.CreateDefault();var arenaObject=new GameObject("Well visual arena");arenaObject.transform.SetParent(root.transform);var arena=arenaObject.AddComponent<ProvingArena>();
            // Match the HDR colour buffer of the runtime camera; an LDR target clips before tone mapping.
            var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.DefaultHDR);var cameraGo=new GameObject("Well review camera");cameraGo.transform.SetParent(root.transform);var camera=cameraGo.AddComponent<Camera>();camera.targetTexture=rt;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.backgroundColor=new Color(.8f,.87f,.94f);camera.clearFlags=CameraClearFlags.SolidColor;
            var lightGo=new GameObject("Review daylight");lightGo.transform.SetParent(root.transform);var light=lightGo.AddComponent<Light>();WellArenaPresentation.ConfigureDaylight(light,ProvingProfile.CreateWellDefault());
            NativeVisualPresentation visual=null;
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement);yield return null;
                visual=root.AddComponent<NativeVisualPresentation>();visual.Bind(movement,arena.transform,light,new[]{camera});
                yield return null;yield return null;
                var skin=arenaObject.GetComponentInChildren<WellArenaPresentation>();
                Assert.That(light.shadows,Is.EqualTo(LightShadows.Soft));
                Assert.That(light.shadowStrength,Is.EqualTo(ProvingProfile.CreateWellDefault().Get("well.daylightShadow")));
                Assert.That(skin.GetComponentsInChildren<Light>().Length,Is.EqualTo(24),"No growth in realtime light count");
                Assert.That(skin.GetComponentsInChildren<MeshRenderer>().Length,Is.LessThan(64),"Bounded material batches and labels");
                Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())),"Presentation cannot change authoring");
                int vertices=0;foreach(var filter in skin.GetComponentsInChildren<MeshFilter>())vertices+=filter.sharedMesh.vertexCount;
                Assert.That(vertices,Is.LessThan(650000),"Bounded presentation geometry");
                foreach(var renderer in skin.GetComponentsInChildren<Renderer>())foreach(var material in renderer.sharedMaterials)Assert.That(material.shader.isSupported,Is.True,material.name);
                foreach(var renderer in skin.GetComponentsInChildren<Renderer>())
                foreach(var material in renderer.sharedMaterials)
                {
                    if(material.name=="Armoured glass")
                    {Assert.That(material.GetFloat("_Metallic"),Is.Zero);Assert.That(material.GetFloat("_Smoothness"),Is.EqualTo(ProvingProfile.CreateWellDefault().Get("well.glassSmoothness")));}
                    if(material.name=="Well ceramic white"||material.name=="Well pale floor"||material.name=="Garden soil"||material.name=="Well technical ceiling")
                    {
                        Assert.That(material.GetTexture("_BumpMap"),Is.Not.Null,material.name);
                        Assert.That(material.IsKeywordEnabled("_NORMALMAP"),Is.True,material.name);
                    }
                }
                foreach(var renderer in skin.GetComponentsInChildren<MeshRenderer>())
                {
                    if(renderer.gameObject.name!="Well ceramic white"&&renderer.gameObject.name!="Well pale floor")continue;
                    Assert.That(renderer.sharedMaterials.Length,Is.EqualTo(3));
                    Assert.That(renderer.sharedMaterials[1].name,Is.EqualTo("Well ceramic white"));
                    Assert.That(renderer.sharedMaterials[2].name,Is.EqualTo("Well technical ceiling"));
                    Assert.That(renderer.sharedMaterials[0].GetTexture("_BaseMap"),Is.Not.SameAs(renderer.sharedMaterials[2].GetTexture("_BaseMap")));
                    var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.subMeshCount,Is.EqualTo(3));
                    var normals=mesh.normals;foreach(var index in mesh.GetTriangles(2))Assert.That(normals[index].y,Is.LessThan(-.5f));
                }
                var directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WELL_FULL_VISUAL_OUTPUT")??Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-storeys/full-visual"));Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,"arena.json"),JsonUtility.ToJson(arena.Definition,true));
                var settings=new System.Text.StringBuilder();settings.AppendLine("Color space: "+QualitySettings.activeColorSpace);settings.AppendLine("Pipeline: "+UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name);
                foreach(VisualEffect effect in System.Enum.GetValues(typeof(VisualEffect)))settings.AppendLine(effect+": "+NativeVisualPreferences.Enabled(effect)+" / "+NativeVisualPreferences.Strength(movement,effect));
                settings.AppendLine("Daylight: "+light.intensity+" shadow="+light.shadowStrength);settings.AppendLine("Probe: "+(visual.EnvironmentProbe?visual.EnvironmentProbe.transform.position.ToString():"disabled"));settings.AppendLine("Ambient: "+RenderSettings.ambientLight);File.WriteAllText(Path.Combine(directory,"render-state.txt"),settings.ToString());
                Assert.That(arenaObject.GetComponentInChildren<WellArenaPresentation>().GetComponentsInChildren<Collider>(),Is.Empty,"Presentation must not add gameplay blockers");
                float middle=WellArenaCatalog.Storey,top=2*middle;
                var eyes=new System.Collections.Generic.List<Vector3>();var targets=new System.Collections.Generic.List<Vector3>();var names=new System.Collections.Generic.List<string>();
                for(int f=0;f<3;f++)foreach(int side in new[]{-1,1})
                {
                    float y=f*WellArenaCatalog.Storey;
                    eyes.Add(new Vector3(2.8f,y+1.7f,side*28));targets.Add(new Vector3(-3.5f,y+3.2f,side*24.8f));names.Add($"lift-{side}-floor-{f}-entry");
                    eyes.Add(new Vector3(0,y+1.7f,side*26));targets.Add(new Vector3(4.8f,y+.3f,side*32.6f));names.Add($"lift-{side}-floor-{f}-back");
                }
                eyes.Add(new Vector3(20,1.7f,21));targets.Add(new Vector3(9.6f,3,16));names.Add("corner-room");
                eyes.Add(new Vector3(-21,middle+1.7f,-13));targets.Add(new Vector3(-16,middle+2,3.2f));names.Add("stair-room");
                eyes.Add(new Vector3(7,1.7f,-7));targets.Add(new Vector3(9.6f,4,9.6f));names.Add("atrium-corner");
                eyes.Add(new Vector3(28,middle+1.7f,0));targets.Add(new Vector3(24.8f,middle+3,3.7f));names.Add("stair-entry");
                eyes.Add(new Vector3(-7,1.7f,-7));targets.Add(new Vector3(-6,.65f,-6));names.Add("garden-detail");
                eyes.Add(new Vector3(7,middle+1.7f,-7));targets.Add(new Vector3(9.6f,middle+4,9.6f));names.Add("physics-gallery");
                eyes.Add(new Vector3(7,top+1.7f,-7));targets.Add(new Vector3(9.6f,top+4,9.6f));names.Add("electronics-gallery");
                eyes.Add(new Vector3(20,top+1.7f,21));targets.Add(new Vector3(14,top+2.7f,16));names.Add("electronics-room");
                for(int f=0;f<3;f++)
                {
                    var module=f==0?new Vector3(-24.65f,2.25f,-14.15f):f==1?new Vector3(-14.15f,middle+2.25f,-24.65f):new Vector3(-14.15f,top+2.25f,24.65f);
                    var inward=f==0?Vector3.right:f==1?Vector3.forward:Vector3.back;
                    eyes.Add(module+inward*3);targets.Add(module);names.Add("department-module-"+f);
                }
                for(int f=0;f<3;f++)
                {
                    float y=f*WellArenaCatalog.Storey;
                    eyes.Add(new Vector3(18,y+1.7f,20));targets.Add(new Vector3(21,y+5,23));names.Add("ceiling-"+f);
                    eyes.Add(new Vector3(15.5f,y+1.7f,20.5f));targets.Add(new Vector3(21,y+1.15f,23));names.Add("workstation-"+f);
                    eyes.Add(new Vector3(17,y+1.7f,11.3f));targets.Add(new Vector3(20,y+.9f,14.65f));names.Add("gallery-cover-"+f);
                }
                eyes.Add(new Vector3(20,1.7f,4.8f));targets.Add(new Vector3(23.9f,1,6.4f));names.Add("service-workbench");
                File.WriteAllText(Path.Combine(directory,"resources.json"),"{\"lights\":24,\"vertices\":"+vertices+",\"renderers\":"+skin.GetComponentsInChildren<MeshRenderer>().Length+"}");
                for(int i=0;i<eyes.Count;i++)
                {
                    cameraGo.transform.position=eyes[i];cameraGo.transform.LookAt(targets[i]);camera.Render();yield return null;
                    var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,names[i]+".png"),image.EncodeToPNG());}finally{RenderTexture.active=previous;Object.Destroy(image);}
                }
                Assert.That(Directory.GetFiles(directory,"*.png").Length,Is.EqualTo(33));
            }
            finally{if(visual)visual.ResetWorld();if(camera)camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);}yield return null;
        }
    }
}
