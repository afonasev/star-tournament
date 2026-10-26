#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class SeamCutterVisualTests
    {
        Scene scene;ProvingGround ground;GameObject studio,lightObject,model;
        static void Capture(Camera camera,string path)
        {
            var rt=new RenderTexture(1600,1000,24);rt.Create();var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;
                if(GraphicsSettings.currentRenderPipeline)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=rt});else camera.Render();
                RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());
            }
            finally{camera.rect=rect;camera.targetTexture=previous;RenderTexture.active=active;rt.Release();Object.Destroy(rt);Object.Destroy(texture);}
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {if(studio)Object.Destroy(studio);if(lightObject)Object.Destroy(lightObject);if(model)Object.Destroy(model);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        [UnityTest] public IEnumerator ImportedModelGripsBeamContactsAndRelease()
        {
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SEAM_EVIDENCE");
            Assert.That(output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null),"This is a graphical review; headless is NotRun");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StarTournament/Art/cutter-lod0.glb");Assert.That(prefab,Is.Not.Null);
            Assert.That(prefab.GetComponentsInChildren<Collider>(true),Is.Empty);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Count(t=>t.name=="cutter-muzzle"),Is.EqualTo(1));
            Assert.That(prefab.GetComponentsInChildren<MeshRenderer>(true).Length,Is.EqualTo(7));
            Assert.That(prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).All(m=>m&&m.mainTexture),Is.True,"F1 authored surface maps must survive the GLB import");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");
            ground=scene.GetRootGameObjects().SelectMany(o=>o.GetComponentsInChildren<ProvingGround>()).Single();
            var roster=NativeMatchRoster.Ffa(4);
            var info=Enumerable.Range(0,4).Select(i=>new NativeParticipantInfo(i==0?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,"SEAM "+i,NativeParticipantColors.For(i),i==0?-1:0)).ToArray();
            ground.StartBotReview(new NativeMatchComposition(roster,info,new[]{0}),4193);ground.enabled=false;yield return NativeLoadingTestScene.Wait(ground);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0,0);
            ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,3),0,180);
            ground.PlaceCombatReviewSeat(2,new Vector3(5,-1.2f,3),0,180);
            ground.PlaceCombatReviewSeat(3,new Vector3(-5,-1.2f,3),0,180);
            var state=ground.Session.Capture();
            for(int i=0;i<4;i++){state.Lives[i].CutterOwned=true;state.Lives[i].CutterEnergy=30;state.Lives[i].SelectedWeapon=WeaponId.Cutter;state.Lives[i].SwitchRemaining=0;state.Lives[i].PendingWeapon=default;state.Lives[i].Ammo=CombatLife.AmmoFor(state.Lives[i],WeaponId.Cutter);state.Lives[i].CooldownRemaining=0;state.Lives[i].Health=100;}
            ground.Session.Restore(state);ground.SendMessage("LateUpdate");yield return null;
            var seat=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            Capture(seat,Path.Combine(output,"fps-idle.png"));
            studio=new GameObject("SEAM studio camera");var camera=studio.AddComponent<Camera>();camera.enabled=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.13f,.15f);camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.fieldOfView=36;camera.cullingMask=1<<30;
            lightObject=new GameObject("SEAM studio light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;lightObject.transform.rotation=Quaternion.Euler(35,-40,0);
            model=Object.Instantiate(prefab);model.transform.position=new Vector3(30,30,30);foreach(var t in model.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
            var center=model.transform.position+new Vector3(0,.015f,.18f);
            foreach(int side in new[]{-1,1}){camera.transform.position=center+new Vector3(side*.95f,.35f,.6f);camera.transform.LookAt(center);Capture(camera,Path.Combine(output,"model-side-"+side+".png"));}
            model.SetActive(false);
            var view=ground.transform.Find("seat-camera-1/trooper-view");var gun=view.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="cutter");
            camera.cullingMask=1<<view.gameObject.layer;
            foreach(var name in new[]{"cutter-grip","cutter-support-grip"})
            {
                var anchor=gun.GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
                foreach(int side in new[]{-1,1})
                {
                    camera.transform.position=anchor.position+gun.right*(side*.39f)+gun.up*.08f-gun.forward*.05f;
                    camera.transform.LookAt(anchor.position);Capture(camera,Path.Combine(output,name+"-"+side+".png"));
                }
            }
            lightObject.SetActive(false);
            var hitPoints=new List<Vector3>();int notices=0;ground.Session.Damaged+=n=>{if(n.Impact.Weapon==WeaponId.Cutter){notices++;hitPoints.Add(n.Impact.Point);}};
            ground.Session.Tick(new LocalAction[4],.02f);
            var actions=new LocalAction[4];actions[0].FireHeld=true;
            for(int i=0;i<10;i++)ground.Session.Tick(actions,.02f);
            ground.SendMessage("LateUpdate");yield return null;
            Assert.That(ground.Session.Beam(0).Active,Is.True);Assert.That(notices,Is.GreaterThan(0),"fixture must produce real body contact");
            Capture(seat,Path.Combine(output,"fps-body-contact.png"));
            camera.cullingMask=~((1<<15)|(1<<16)|(1<<17)|(1<<18)|(1<<30));
            var contactCenter=ground.Session.Pose(1).Position+Vector3.up*1.45f;
            camera.transform.position=contactCenter+new Vector3(1.5f,.2f,-1.8f);camera.transform.LookAt(contactCenter);
            Capture(camera,Path.Combine(output,"body-contact-detail.png"));
            var beams=ground.GetComponentsInChildren<LineRenderer>(true).Where(x=>x.transform.parent.name=="Cutter beams").ToArray();
            Assert.That(beams.Count(x=>x.enabled),Is.GreaterThanOrEqualTo(3));
            foreach(var core in beams.Where(x=>x.enabled&&x.name.StartsWith("Core ")))
            {
                Assert.That(core.positionCount,Is.EqualTo(2),"Straight authoritative section has no billboard miter");
                var axis=(core.GetPosition(1)-core.GetPosition(0)).normalized;
                foreach(var point in hitPoints)Assert.That(Vector3.Cross(point-core.GetPosition(0),axis).magnitude,Is.LessThan(.001f),"Visible beam must pass through real contact after muzzle compensation");
            }
            var meshes=ground.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("SEAM contacts")).ToArray();
            Assert.That(meshes.Any(m=>m.sharedMesh.vertexCount>0),Is.True);
            // Frozen session render is deterministic even when real time advances.
            var frozen=meshes.Select(m=>m.sharedMesh.vertices).ToArray();yield return null;ground.SendMessage("LateUpdate");
            for(int i=0;i<meshes.Length;i++)Assert.That(meshes[i].sharedMesh.vertices,Is.EqualTo(frozen[i]));
            ground.Session.Tick(new LocalAction[4],.02f);ground.SendMessage("LateUpdate");
            Assert.That(beams.All(x=>!x.enabled),Is.True);Assert.That(meshes.All(m=>m.sharedMesh.vertexCount==0),Is.True);
            var shieldState=ground.Session.Capture();shieldState.Lives[1].Armor=100;ground.Session.Restore(shieldState);
            for(int i=0;i<10;i++)ground.Session.Tick(actions,.02f);ground.SendMessage("LateUpdate");yield return null;
            Assert.That(ground.GetComponentsInChildren<MeshFilter>(true).Any(m=>m.name.StartsWith("SEAM shield")&&m.sharedMesh.vertexCount>0),Is.True);
            Capture(seat,Path.Combine(output,"fps-shield-contact.png"));Capture(camera,Path.Combine(output,"shield-contact-detail.png"));
            ground.PlaceCombatReviewSeat(1,new Vector3(4,-1.2f,3),0,180);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0,90);
            ground.Session.Tick(actions,.02f);ground.SendMessage("LateUpdate");yield return null;
            Assert.That(ground.Session.Beam(0).Wall,Is.True);Capture(seat,Path.Combine(output,"fps-wall-contact.png"));
            File.WriteAllText(Path.Combine(output,"review.json"),"{\"classification\":\"Editor Play Mode, not human/device acceptance\",\"seed\":4193,\"damageNotices\":"+notices+"}");
        }
    }
}
#endif
