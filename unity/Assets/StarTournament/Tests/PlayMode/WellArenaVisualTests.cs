using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace StarTournament.ProvingGround.Tests
{
    public sealed class WellArenaVisualTests
    {
        [UnityTest] public IEnumerator SaveEditorViewsForVisualReview()
        {
            var root=new GameObject("Well visual review");var movement=ProvingProfile.CreateDefault();var arenaObject=new GameObject("Well visual arena");arenaObject.transform.SetParent(root.transform);var arena=arenaObject.AddComponent<ProvingArena>();
            var rt=new RenderTexture(1280,720,24);var cameraGo=new GameObject("Well review camera");cameraGo.transform.SetParent(root.transform);var camera=cameraGo.AddComponent<Camera>();camera.targetTexture=rt;camera.fieldOfView=75;camera.nearClipPlane=.05f;camera.backgroundColor=new Color(.8f,.87f,.94f);camera.clearFlags=CameraClearFlags.SolidColor;
            var lightGo=new GameObject("Review daylight");lightGo.transform.SetParent(root.transform);var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=ProvingProfile.CreateWellDefault().Get("well.daylight");lightGo.transform.rotation=Quaternion.Euler(45,-30,0);
            try
            {
                arena.Build(AuthoredArenaCatalog.Freeze(WellArenaCatalog.Id,movement),movement);yield return null;
                var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/evidence/well-structure/editor-views"));Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,"arena.json"),JsonUtility.ToJson(arena.Definition,true));
                float middle=WellArenaCatalog.Storey,top=2*middle;
                var eyes=new[]{new Vector3(7,1.7f,-7),new Vector3(-7,1.7f,18),new Vector3(0,middle+1.7f,18),new Vector3(0,1.7f,-23),new Vector3(-21,middle+1.7f,-13),new Vector3(20,top+1.7f,22),new Vector3(0,1.7f,-7),new Vector3(25,middle+1.7f,0)};
                var targets=new[]{new Vector3(-5,12,5),new Vector3(18,1.7f,18),new Vector3(0,middle+.5f,0),new Vector3(0,1.7f,-29),new Vector3(-21,middle+1.7f,13),new Vector3(12,top+1.7f,17),new Vector3(0,4,7),new Vector3(35,middle+2,0)};
                var names=new[]{"atrium","bio-gallery","physics-bridge","south-lift","floor-2-stair-corridor","corner-room","garden-centre","stairs"};
                for(int i=0;i<eyes.Length;i++)
                {
                    cameraGo.transform.position=eyes[i];cameraGo.transform.LookAt(targets[i]);camera.Render();yield return null;
                    var previous=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    try{image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Path.Combine(directory,names[i]+".png"),image.EncodeToPNG());}finally{RenderTexture.active=previous;Object.Destroy(image);}
                }
                Assert.That(Directory.GetFiles(directory,"*.png").Length,Is.EqualTo(8));
            }
            finally{if(camera)camera.targetTexture=null;Object.Destroy(rt);Object.Destroy(root);}yield return null;
        }
    }
}
