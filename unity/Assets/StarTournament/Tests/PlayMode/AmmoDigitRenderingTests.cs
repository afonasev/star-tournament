using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class AmmoDigitRenderingTests
    {
        [TestCase("Shotgun")]
        [TestCase("Pulse")]
        public void RenderedDigitsFollowAmmo(string kind)
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            var root=new GameObject("ammo-render-test");
            var cameraObject=new GameObject("ammo-render-camera");
            var target=new RenderTexture(512,512,24);
            var image=new Texture2D(512,512,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            bool asyncShaders=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            try
            {
                var display=new GameObject("display");display.transform.SetParent(root.transform);
                Action<int> set;
                var profile=ProvingProfile.CreateDefault();
                if(kind=="Shotgun") {var d=display.AddComponent<ShotgunAmmoDisplay>();d.Initialize(root.transform,profile,30);set=d.SetAmmo;}
                else {var d=display.AddComponent<PulseAmmoDisplay>();d.Initialize(root.transform,profile,30);set=n=>d.SetAmmo(n,20);}
                display.transform.localPosition=Vector3.zero;display.transform.localRotation=Quaternion.identity;display.transform.localScale=Vector3.one;
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=.6f;
                camera.transform.position=new Vector3(0,0,-2);camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.targetTexture=target;
                Color32[] last=null;
                foreach(int ammo in new[]{18,17,12,11,8,7,1,0,100,2147483647})
                {
                    set(ammo);camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,512,512),0,0);image.Apply();
                    var pixels=image.GetPixels32();
                    var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_DIGIT_CAPTURE");
                    if(!string.IsNullOrEmpty(output)){Directory.CreateDirectory(output);File.WriteAllBytes(Path.Combine(output,kind+"-"+ammo+".png"),image.EncodeToPNG());}
                    if(last!=null)
                    {
                        int changed=0;
                        // Only the central numeric region; excludes reserve arcs and the empty-state icon.
                        for(int y=200;y<335;y++)for(int x=150;x<362;x++)
                        {int i=y*512+x;if(Math.Abs(pixels[i].r-last[i].r)+Math.Abs(pixels[i].g-last[i].g)+Math.Abs(pixels[i].b-last[i].b)>45)changed++;}
                        Assert.That(changed,Is.GreaterThan(100),kind+" rendered digits did not change at ammo "+ammo);
                    }
                    last=pixels;
                }
            }
            finally {UnityEditor.ShaderUtil.allowAsyncCompilation=asyncShaders;RenderTexture.active=previous;Object.DestroyImmediate(root);Object.DestroyImmediate(cameraObject);Object.DestroyImmediate(image);target.Release();Object.DestroyImmediate(target);}
        }
    }
}
