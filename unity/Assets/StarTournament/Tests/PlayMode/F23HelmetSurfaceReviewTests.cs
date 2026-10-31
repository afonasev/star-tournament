#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class F23HelmetSurfaceReviewTests
    {
        [UnityTest,Explicit("F02 actual native close-ups and texture A/B; requires STAR_F23_HELMET_EVIDENCE")]
        public IEnumerator HelmetAtlasSurvivesAssignedTint()
        {
            string output=Environment.GetEnvironmentVariable("STAR_F23_HELMET_EVIDENCE");
            Assert.That(Path.IsPathFullyQualified(output??""),Is.True);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            yield return NativeLoadingTestScene.Load();var scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            var root=Object.Instantiate(ground.TrooperBodyPrefab,new Vector3(3000,3000,3000),Quaternion.identity);
            TrooperIdentityPresentation.ApplyBody(root,new Color(.04f,.65f,.72f),ground.ParticipantPaletteProfile);
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var visual=root.AddComponent<TrooperVisual>();visual.EquippedWeapon=WeaponId.Shotgun;
            visual.Initialize(ground.TrooperClips,ProvingProfile.CreateTrooperDefault(),false);visual.Render(0,Vector3.zero,100,1);
            var cameraObject=new GameObject("F02 helmet evidence camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.11f,.12f,.13f);camera.orthographic=true;camera.orthographicSize=.165f;
            camera.nearClipPlane=.01f;camera.farClipPlane=10;
            var target=new RenderTexture(1000,1000,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(1000,1000,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var lightObject=new GameObject("F02 neutral key");var key=lightObject.AddComponent<Light>();
            key.type=LightType.Directional;key.intensity=1.3f;key.cullingMask=1<<30;key.transform.rotation=Quaternion.Euler(35,150,0);
            var oldSun=RenderSettings.sun;var oldMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;
            RenderSettings.sun=key;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.18f,.18f,.18f);
            var flat=new Texture2D(2,2,TextureFormat.RGBA32,false,true);
            flat.SetPixels(Enumerable.Repeat(new Color(.14f,.14f,.14f,1),4).ToArray());flat.Apply();
            Color32[] Capture(string name)
            {
                if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1000,1000),0,0);pixels.Apply();
                File.WriteAllBytes(Path.Combine(output,name+".png"),pixels.EncodeToPNG());return pixels.GetPixels32();
            }
            try
            {
                var center=root.transform.position+Vector3.up*1.65f;
                // glTF import mirrors the authored X axis; show the same side
                // as review_f23_helmet.py rather than the opposite cheek.
                foreach(var view in new[]{("front",new Vector3(0,.06f,1)),("side",new Vector3(-1,.06f,0)),("back",new Vector3(-.3f,.14f,-1)),("three-quarter",new Vector3(-.55f,.17f,1))})
                {
                    camera.transform.position=center+view.Item2;camera.transform.LookAt(center);yield return null;Capture(view.Item1);
                }
                var painted=root.GetComponentsInChildren<Renderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m.name=="vector-fighter-identity-f23-helmet").Distinct().ToArray();
                Assert.That(painted,Is.Not.Empty);
                foreach(var m in painted)
                {
                    Assert.That(m.GetTexture("baseColorTexture"),Is.Not.Null);
                    Assert.That(m.GetTexture("normalTexture"),Is.Not.Null);
                    Assert.That(m.GetTexture("metallicRoughnessTexture"),Is.Not.Null);
                    Assert.That(m.GetVector("_IdentitySurface").x,Is.EqualTo(ground.ParticipantPaletteProfile.Get("participant.surface.detailGain")));
                }
                var textured=Capture("tint-textured");
                // Change only base-color variation. The same geometry, normals,
                // roughness, assigned tint, camera and light remain in this A/B.
                foreach(var m in painted)m.SetTexture("baseColorTexture",flat);
                yield return null;var uniform=Capture("tint-flat-albedo");
                double delta=0;int changed=0;
                for(int i=0;i<textured.Length;i++)
                {
                    int d=Math.Abs(textured[i].r-uniform[i].r)+Math.Abs(textured[i].g-uniform[i].g)+Math.Abs(textured[i].b-uniform[i].b);
                    delta+=d;if(d>=3)changed++;
                }
                double mean=delta/(textured.Length*3.0);
                File.WriteAllText(Path.Combine(output,"texture-ab.json"),"{\"meanAbsoluteByteDifference\":"+mean.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"changedPixels\":"+changed+",\"test\":\"only helmet paint albedo replaced by constant linear 0.14\"}");
                Assert.That(changed,Is.GreaterThan(1000),"Authored pigment variation must affect actual assigned-color pixels");
                Assert.That(mean,Is.GreaterThan(.1),"A/B must produce visible nonzero image response");
            }
            finally
            {
                RenderSettings.sun=oldSun;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;
                RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(pixels);Object.Destroy(flat);
                visual.Release();Object.Destroy(root);Object.Destroy(cameraObject);Object.Destroy(lightObject);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
