#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class WeaponRenderingOptimizationTests
    {
        const string Art="Assets/StarTournament/Art/";
        Scene scene; GameObject studio, lightRoot, subject;
        readonly Dictionary<Mesh,Mesh> original=new Dictionary<Mesh,Mesh>();
        [UnityTearDown] public IEnumerator Cleanup()
        { if(subject)Object.Destroy(subject);if(studio)Object.Destroy(studio);if(lightRoot)Object.Destroy(lightRoot);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene); }
        static byte[] Capture(Camera camera,string path)
        {
            var target=new RenderTexture(960,720,24);target.Create();var texture=new Texture2D(960,720,TextureFormat.RGB24,false);
            var previous=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;
                if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
                RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,960,720),0,0);texture.Apply();
                File.WriteAllBytes(path,texture.EncodeToPNG());return texture.GetRawTextureData().ToArray();
            }
            finally{camera.rect=rect;camera.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(texture);}
        }
        static int CacheMisses(Mesh mesh,int size)
        {
            int count=0;var cache=new List<int>();
            for(int sub=0;sub<mesh.subMeshCount;sub++)
            {
                cache.Clear();foreach(int vertex in mesh.GetIndices(sub))
                {if(!cache.Remove(vertex))count++;cache.Insert(0,vertex);if(cache.Count>size)cache.RemoveAt(size);}
            }
            return count;
        }
        void Swap(GameObject root,bool baseline)
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(baseline&&original.TryGetValue(filter.sharedMesh,out var old)){filter.sharedMesh=old;continue;}
                if(!baseline){var pair=original.FirstOrDefault(p=>p.Value==filter.sharedMesh);if(pair.Key)filter.sharedMesh=pair.Key;}
            }
            foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(baseline&&original.TryGetValue(skin.sharedMesh,out var old)){skin.sharedMesh=old;continue;}
                if(!baseline){var pair=original.FirstOrDefault(p=>p.Value==skin.sharedMesh);if(pair.Key)skin.sharedMesh=pair.Key;}
            }
        }
        void Compare(GameObject root,Camera camera,string stem,List<string> rows)
        {
            // Prime the render path before comparing; the first SRP request can
            // initialize lighting/material resources independently of mesh choice.
            Swap(root,true);Capture(camera,stem+"-warmup.png");var before=Capture(camera,stem+"-before.png");Swap(root,false);var after=Capture(camera,stem+"-after.png");
            Swap(root,true);var repeat=Capture(camera,stem+"-repeat.png");Swap(root,false);
            int unstable=0,repeatMax=0;long repeatSum=0;for(int i=0;i<before.Length;i++){int delta=Math.Abs(before[i]-repeat[i]);if(delta>0)unstable++;repeatMax=Math.Max(repeatMax,delta);repeatSum+=delta;}
            rows.Add($"{Path.GetFileName(stem)}-baseline-repeat,{unstable},{repeatMax},{(double)repeatSum/before.Length}");
            int changed=0,max=0;long sum=0;for(int i=0;i<before.Length;i++){int delta=Math.Abs(before[i]-after[i]);if(delta>0)changed++;max=Math.Max(max,delta);sum+=delta;}
            rows.Add($"{Path.GetFileName(stem)},{changed},{max},{(double)sum/before.Length}");
            // Reordering may select the other surface at authored coplanar overlaps.
            // Keep real differences visible for review; exact geometry is audited offline.
        }
        [UnityTest,Explicit("Graphical before/after weapon review with baseline GLBs; no build or gameplay benchmark")]
        public IEnumerator CompareAllWeapons()
        {
            var output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WEAPON_OPT_EVIDENCE");Assert.That(output,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(output);
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            string[] files={"automatic-rifle-lod0.glb","vector-shotgun-lod0.glb","pulse-launcher-lod0.glb","cutter-lod0.glb","forge-plasma-lod0.glb","trooper-body-animated.glb","trooper-arms-animated.glb"};
            var imported=new List<string>{"file,vertices_before,vertices_after,triangles_before,triangles_after,submeshes,cache16_before,cache16_after,cache32_before,cache32_after"};
            foreach(var file in files)
            {
                var before=AssetDatabase.LoadAllAssetsAtPath(Art+"WeaponRenderBaseline/"+file).OfType<Mesh>().ToDictionary(m=>m.name);
                var after=AssetDatabase.LoadAllAssetsAtPath(Art+file).OfType<Mesh>().ToArray();Assert.That(after,Is.Not.Empty);
                foreach(var mesh in after){Assert.That(before.ContainsKey(mesh.name),Is.True,file+" "+mesh.name);original[mesh]=before[mesh.name];Assert.That(mesh.triangles.Length,Is.EqualTo(before[mesh.name].triangles.Length));Assert.That(mesh.subMeshCount,Is.EqualTo(before[mesh.name].subMeshCount));}
                imported.Add($"{file},{before.Values.Sum(m=>m.vertexCount)},{after.Sum(m=>m.vertexCount)},{before.Values.Sum(m=>m.triangles.Length/3)},{after.Sum(m=>m.triangles.Length/3)},{after.Sum(m=>m.subMeshCount)},{before.Values.Sum(m=>CacheMisses(m,16))},{after.Sum(m=>CacheMisses(m,16))},{before.Values.Sum(m=>CacheMisses(m,32))},{after.Sum(m=>CacheMisses(m,32))}");
            }
            var rows=new List<string>{"capture,changed_channels,max_channel_delta,mean_channel_delta"};
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            var roster=NativeMatchRoster.Ffa(4);var info=Enumerable.Range(0,4).Select(i=>new NativeParticipantInfo(i==0?NativeParticipantKind.LocalHuman:NativeParticipantKind.Bot,"Review "+i,NativeParticipantColors.For(i),i==0?-1:0)).ToArray();
            ground.StartBotReview(new NativeMatchComposition(roster,info,new[]{0}),4193);ground.enabled=false;yield return NativeLoadingTestScene.Wait(ground);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0,0);
            var seat=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            foreach(WeaponId weapon in Enum.GetValues(typeof(WeaponId)))
            {
                var state=ground.Session.Capture();var life=state.Lives[0];life.ShotgunOwned=life.RocketOwned=life.CutterOwned=life.PlasmaOwned=true;life.PlasmaAmmo=100;life.CutterEnergy=30;
                life.SelectedWeapon=weapon;life.SwitchRemaining=0;life.PendingWeapon=default;life.Ammo=CombatLife.AmmoFor(life,weapon);state.Lives[0]=life;ground.Session.Restore(state);ground.SendMessage("LateUpdate");yield return null;
                var frozen=JsonUtility.ToJson(ground.Session.Capture());Compare(ground.gameObject,seat,Path.Combine(output,"view-"+weapon),rows);Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(frozen));
            }
            studio=new GameObject("weapon optimization camera");var camera=studio.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.17f);camera.fieldOfView=35;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            lightRoot=new GameObject("weapon optimization fill");var light=lightRoot.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.cullingMask=1<<30;light.transform.rotation=Quaternion.Euler(30,-40,0);
            foreach(var file in files.Take(5))
            {
                subject=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+file));foreach(var t in subject.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                var renderers=subject.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                foreach(int angle in new[]{0,90,180})
                {
                    var direction=Quaternion.Euler(15,angle+30,0)*Vector3.forward;camera.transform.position=bounds.center+direction*bounds.size.magnitude*1.8f;camera.transform.LookAt(bounds.center);
                    Compare(subject,camera,Path.Combine(output,file+"-"+angle),rows);
                }
                Object.Destroy(subject);yield return null;
            }
            File.WriteAllLines(Path.Combine(output,"pixels.csv"),rows);File.WriteAllLines(Path.Combine(output,"imported.csv"),imported);
        }
    }
}
#endif
