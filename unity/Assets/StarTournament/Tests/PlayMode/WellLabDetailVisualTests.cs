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
            var rt=new RenderTexture(1280,720,24);var cameraGo=new GameObject("Well review camera");cameraGo.transform.SetParent(root.transform);var camera=cameraGo.AddComponent<Camera>();camera.targetTexture=rt;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.backgroundColor=new Color(.8f,.87f,.94f);camera.clearFlags=CameraClearFlags.SolidColor;
            var lightGo=new GameObject("Review daylight");lightGo.transform.SetParent(root.transform);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=ProvingProfile.CreateWellDefault().Get("well.daylight");lightGo.transform.rotation=Quaternion.Euler(45,-30,0);
            NativeVisualPresentation visual=null;
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement);yield return null;
                visual=root.AddComponent<NativeVisualPresentation>();visual.Bind(movement,arena.transform,light,new[]{camera});
                yield return null;
                var skin=arenaObject.GetComponentInChildren<WellArenaPresentation>();
                Assert.That(skin.GetComponentsInChildren<Light>().Length,Is.EqualTo(24),"No growth in realtime light count");
                Assert.That(skin.GetComponentsInChildren<MeshRenderer>().Length,Is.LessThan(64),"Bounded material batches and labels");
                Assert.That(JsonUtility.ToJson(arena.Definition),Is.EqualTo(JsonUtility.ToJson(WellArenaCatalog.Build())),"Presentation cannot change authoring");
                int vertices=0;foreach(var filter in skin.GetComponentsInChildren<MeshFilter>())vertices+=filter.sharedMesh.vertexCount;
                Assert.That(vertices,Is.LessThan(250000),"Bounded presentation geometry");
                foreach(var renderer in skin.GetComponentsInChildren<Renderer>())Assert.That(renderer.sharedMaterial.shader.isSupported,Is.True,renderer.sharedMaterial.name);
                var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-lab-detail/views"));Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,"arena.json"),JsonUtility.ToJson(arena.Definition,true));
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
                File.WriteAllText(Path.Combine(directory,"resources.json"),"{\"lights\":24,\"vertices\":"+vertices+",\"renderers\":"+skin.GetComponentsInChildren<MeshRenderer>().Length+"}");
                for(int i=0;i<eyes.Count;i++)
                {
                    cameraGo.transform.position=eyes[i];cameraGo.transform.LookAt(targets[i]);camera.Render();yield return null;
                    var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,names[i]+".png"),image.EncodeToPNG());}finally{RenderTexture.active=previous;Object.Destroy(image);}
                }
                Assert.That(Directory.GetFiles(directory,"*.png").Length,Is.EqualTo(20));
            }
            finally{if(visual)visual.ResetWorld();if(camera)camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);}yield return null;
        }
    }
}
