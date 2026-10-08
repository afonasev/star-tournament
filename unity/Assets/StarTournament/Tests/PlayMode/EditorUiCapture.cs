using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    // Batch Editor screenshots do not imply native Player/device acceptance.
    public static class EditorUiCapture
    {
        public static IEnumerator Capture(ProvingGround ground,string path)
        {
            var canvas=ground.GetComponentsInChildren<Canvas>(true).Single(c=>c.name=="native-ui");
            var mode=canvas.renderMode;var worldCamera=canvas.worldCamera;float plane=canvas.planeDistance;
            var go=new GameObject("setup-qa-camera");SceneManager.MoveGameObjectToScene(go,ground.gameObject.scene);
            var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=new Vector3(0,0,-1000);
            camera.orthographic=true;camera.orthographicSize=540;camera.aspect=1920f/1080;camera.nearClipPlane=.1f;camera.farClipPlane=10;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color32(10,18,30,255);
            var target=new RenderTexture(1920,1080,24);target.Create();camera.targetTexture=target;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
            yield return null;Canvas.ForceUpdateCanvases();yield return null;
            var previous=RenderTexture.active;
            try
            {
                if(GraphicsSettings.currentRenderPipeline!=null)
                {
                    var request=new RenderPipeline.StandardRequest{destination=target};Assert.That(RenderPipeline.SupportsRenderRequest(camera,request),Is.True);
                    RenderPipeline.SubmitRenderRequest(camera,request);
                }
                else camera.Render();
                RenderTexture.active=target;var pixels=new Texture2D(1920,1080,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,1920,1080),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());UnityEngine.Object.Destroy(pixels);
            }
            finally
            {
                RenderTexture.active=previous;canvas.renderMode=mode;canvas.worldCamera=worldCamera;canvas.planeDistance=plane;
                camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(go);
            }
        }
    }
}
