#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    /// <summary>Native rendering of exact integrated models. Browser assets retain
    /// their authored rest pose: they never had Trooper's animation skeleton.</summary>
    public sealed class B01HistoryReviewTests
    {
        [Serializable] public sealed class Entry { public string id,path,kind,label; }
        [Serializable] public sealed class Catalog { public Entry[] items; }
        static GameObject Procedural()
        {
            // Historical firstPersonRenderer.volumeMesh + combatSliceScenarioV1
            // at 14d2ad9f^, re-rendered natively; analytical dimensions are exact.
            var root=new GameObject("historical procedural target");
            void Part(PrimitiveType type,Vector3 position,Vector3 scale)
            {var p=GameObject.CreatePrimitive(type);p.transform.SetParent(root.transform,false);p.transform.localPosition=position;p.transform.localScale=scale;Object.Destroy(p.GetComponent<Collider>());}
            Part(PrimitiveType.Sphere,new Vector3(0,1.78f,0),Vector3.one*.48f);
            Part(PrimitiveType.Cube,new Vector3(0,1.18f,0),new Vector3(.68f,.96f,.44f));
            foreach(float side in new[]{-1f,1f})
            {
                Part(PrimitiveType.Capsule,new Vector3(side*.48f,1.16f,0),new Vector3(.24f,.46f,.24f));
                Part(PrimitiveType.Capsule,new Vector3(side*.2f,.43f,0),new Vector3(.28f,.43f,.28f));
            }
            foreach(var r in root.GetComponentsInChildren<Renderer>())
            {r.material.color=new Color(.04f,.65f,.72f);r.material.SetFloat("_Metallic",.68f);r.material.SetFloat("_Smoothness",.68f);}
            return root;
        }
        [UnityTest,Explicit("Exact historical GLB gallery and B01 optimization pair; needs STAR_B01_GALLERY and STAR_B01_HISTORY_EVIDENCE")]
        public IEnumerator IntegratedBodiesInCommonNativeStudio()
        {
            string catalog=Environment.GetEnvironmentVariable("STAR_B01_GALLERY"),output=Environment.GetEnvironmentVariable("STAR_B01_HISTORY_EVIDENCE");
            Assert.That(File.Exists(catalog),Is.True);Assert.That(Path.IsPathFullyQualified(output??""),Is.True);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            var entries=JsonUtility.FromJson<Catalog>(File.ReadAllText(catalog)).items;
            Assert.That(entries.Length,Is.GreaterThan(10));
            string selected=Environment.GetEnvironmentVariable("STAR_B01_GALLERY_IDS");
            if(!string.IsNullOrEmpty(selected))
            {
                var ids=selected.Split(';');entries=entries.Where(e=>ids.Contains(e.id)).ToArray();
                Assert.That(entries.Length,Is.EqualTo(ids.Length),"Every requested gallery revision must exist");
            }
            yield return NativeLoadingTestScene.Load();var scene=SceneManager.GetSceneByName("ProvingGround");
            var ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            var cameraObject=new GameObject("B01 common native studio");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.085f,.095f);
            camera.orthographic=true;camera.orthographicSize=1.08f;camera.nearClipPlane=.01f;camera.farClipPlane=20;
            var target=new RenderTexture(1200,1600,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(1200,1600,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var lightObject=new GameObject("B01 common key");var key=lightObject.AddComponent<Light>();key.type=LightType.Directional;key.intensity=1.3f;key.cullingMask=1<<30;key.transform.rotation=Quaternion.Euler(35,150,0);
            var oldSun=RenderSettings.sun;var oldMode=RenderSettings.ambientMode;var oldAmbient=RenderSettings.ambientLight;
            RenderSettings.sun=key;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.18f,.18f,.18f);
            try
            {
                foreach(var entry in new[]{new Entry{id="procedural-pre-glb",kind="procedural",label="Analytical reconstruction of pre-GLB target"}}.Concat(entries))
                {
                    GameObject root;
                    if(entry.kind=="procedural")root=Procedural();
                    else
                    {var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(entry.path);Assert.That(prefab,Is.Not.Null,entry.path);root=Object.Instantiate(prefab);}
                    var origin=new Vector3(3000,3000,3000);root.transform.position=origin;
                    TrooperVisual visual=null;
                    try
                    {
                        TrooperIdentityPresentation.ApplyBody(root,new Color(.04f,.65f,.72f),ground.ParticipantPaletteProfile);
                        bool native=entry.kind=="native";
                        if(native)
                        {
                            // Raw historical GLBs may lack the persisted prefab Animator.
                            if(!root.GetComponentInChildren<Animator>())
                            {var rig=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Root").parent;rig.gameObject.AddComponent<Animator>();}
                            visual=root.AddComponent<TrooperVisual>();visual.EquippedWeapon=WeaponId.Shotgun;
                            visual.Initialize(ground.TrooperClips,ProvingProfile.CreateTrooperDefault(),false);
                        }
                        else
                        {
                            var renderers=root.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                            foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                            root.transform.localScale*=1.77f/bounds.size.y;
                            bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                            root.transform.position+=origin-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
                        }
                        foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                        var folder=Path.Combine(output,entry.id);Directory.CreateDirectory(folder);
                        File.WriteAllText(Path.Combine(folder,"provenance.json"),JsonUtility.ToJson(entry,true));
                        foreach(string state in native?new[]{"idle","run"}:new[]{"authored-rest"})
                        {
                            if(native)
                            {
                                float speed=state=="run"?ProvingProfile.CreateTrooperDefault().Get("animation.runThreshold")+1:0;
                                for(int i=0;i<=35;i++){root.transform.position=origin+Vector3.forward*speed*i*.01f;visual.Render(i*.01,new Vector3(0,0,speed),100,1);}
                                root.transform.position=origin;Assert.That(visual.State,Is.EqualTo(state));
                            }
                            foreach(var r in root.GetComponentsInChildren<Renderer>(true))
                            {if(r.name=="weapon:joined"||r.name.StartsWith("weapon:"))r.enabled=false;}
                            foreach(var view in new[]{("front",new Vector3(2,1.2f,4)),("side",new Vector3(4,.3f,0)),("back",new Vector3(-2,1,-4))})
                            {
                                var center=origin+Vector3.up*.9f;camera.transform.position=center+view.Item2;camera.transform.LookAt(center);yield return null;
                                if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
                                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1200,1600),0,0);pixels.Apply();
                                File.WriteAllBytes(Path.Combine(folder,state+"-"+view.Item1+".png"),pixels.EncodeToPNG());
                            }
                        }
                    }
                    finally{if(visual)visual.Release();Object.Destroy(root);}
                    yield return null;
                }
            }
            finally
            {
                RenderSettings.sun=oldSun;RenderSettings.ambientMode=oldMode;RenderSettings.ambientLight=oldAmbient;RenderTexture.active=previous;
                target.Release();Object.Destroy(target);Object.Destroy(pixels);Object.Destroy(cameraObject);Object.Destroy(lightObject);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
