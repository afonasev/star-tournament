using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class VectorShotgunDetailTests
    {
        Scene scene;Gamepad pad;bool previousAsync;
        ProvingGround ground;string output;
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        IEnumerator Capture(Camera camera,string name)
        {
            if(string.IsNullOrEmpty(output))yield break;
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));
            Directory.CreateDirectory(output);var target=new RenderTexture(1600,1000,24);var prior=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            var image=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();yield return null;camera.Render();yield return null;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),image.EncodeToPNG());}
            finally{if(camera){camera.rect=rect;camera.targetTexture=prior;}RenderTexture.active=active;Object.Destroy(image);target.Release();Object.Destroy(target);}
        }
        [UnityTest]public IEnumerator DetailedModelAndKineticEffectsFollowActualSession()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_VECTOR_SHOTGUN_CAPTURE");
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);
            yield return null; // Retire the inactive menu cameras queued by RebuildActors before selecting the live camera.
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
            var snap=ground.Session.Capture();snap.Lives[0].ShotgunOwned=true;snap.Lives[0].ShotgunAmmo=20;ground.Session.Restore(snap);
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].SelectWeapon=WeaponSelection.Shotgun;ground.Session.Tick(actions,.02f);actions[0]=default;
            for(int i=0;i<60;i++)ground.Session.Tick(actions,.02f);Render();
            var camera=ground.GetComponentsInChildren<Camera>().Single(c=>c.name=="seat-camera-1");var view=camera.transform.Find("trooper-view");
            var gun=view.GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="weapon:joined");
            Assert.That(gun.sharedMaterials.Any(m=>m.name=="vector-petrol-enamel"),Is.True);
            var muzzle=view.GetComponent<VectorShotPresentation>();
            var authoredParticleMaterials=muzzle.GetComponentsInChildren<ParticleSystemRenderer>().ToDictionary(r=>r,r=>r.sharedMaterial);
            foreach(bool active in new[]{false,true})
            {
                view.gameObject.SetActive(active);TrooperIdentityPresentation.ApplyBody(view.gameObject,Color.cyan,ground.ParticipantPaletteProfile);
                foreach(var pair in authoredParticleMaterials){Assert.That(pair.Key.sharedMaterial,Is.SameAs(pair.Value));Assert.That(pair.Value.shader.name,Is.EqualTo("StarTournament/VectorParticle"));}
            }
            Assert.That(muzzle.MuzzleCount,Is.EqualTo(2));Assert.That(muzzle.LiveParticles,Is.Zero);
            yield return Capture(camera,"01-fps-idle");
            if(!string.IsNullOrEmpty(output))typeof(TrooperPresentationTests).GetMethod("CaptureGrip",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(new TrooperPresentationTests(),new object[]{output,"Shotgun",view.GetComponent<TrooperVisual>(),camera});
            // Same native sampled pose, isolated camera layer: inspect both sides without changing bones.
            var inspect=new GameObject("vector-detail-review-camera").AddComponent<Camera>();inspect.transform.SetParent(ground.transform,false);inspect.cullingMask=1<<15;inspect.clearFlags=CameraClearFlags.SolidColor;inspect.backgroundColor=new Color(.12f,.15f,.19f);inspect.nearClipPlane=.01f;inspect.farClipPlane=10;inspect.orthographic=true;inspect.orthographicSize=.32f;
            var center=gun.bounds.center;
            foreach(var row in new[]{("02-grip-left",new Vector3(-.7f,.22f,.15f)),("03-grip-right",new Vector3(.7f,.22f,.15f)),("04-grip-under",new Vector3(.25f,-.55f,-.15f))})
            {
                inspect.transform.position=center+camera.transform.TransformDirection(row.Item2);inspect.transform.LookAt(center,camera.transform.up);yield return Capture(inspect,row.Item1);
            }
            Object.Destroy(inspect.gameObject);
            ShotNotice shot=default;ground.Session.ShotResolved+=n=>shot=n;actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0]=default;Render();
            Assert.That(shot.Pellets.Length,Is.EqualTo(18));Assert.That(muzzle.LiveParticles,Is.GreaterThan(0));
            var cores=ground.GetComponentsInChildren<MeshFilter>().Where(m=>m.name=="pellet-metal-core").ToArray();Assert.That(cores.Length,Is.EqualTo(18));
            foreach(var core in cores){Assert.That(core.sharedMesh.triangles.Length,Is.GreaterThan(1000));Assert.That(core.GetComponent<Collider>(),Is.Null);var mesh=core.sharedMesh;int mid=mesh.vertexCount/2;Assert.That(Vector3.Dot(mesh.vertices[mid],mesh.normals[mid]),Is.GreaterThan(0));}
            var state=JsonUtility.ToJson(ground.Session.Capture());var positions=cores.Select(c=>c.transform.position).ToArray();var particles=muzzle.GetComponentsInChildren<ParticleSystem>().Select(p=>p.time).ToArray();Render();Render();
            Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(state));CollectionAssert.AreEqual(positions,cores.Select(c=>c.transform.position).ToArray());CollectionAssert.AreEqual(particles,muzzle.GetComponentsInChildren<ParticleSystem>().Select(p=>p.time).ToArray());
            yield return Capture(camera,"05-shot-birth");
            var muzzleCamera=new GameObject("muzzle-review-camera").AddComponent<Camera>();muzzleCamera.transform.SetParent(ground.transform,false);muzzleCamera.cullingMask=1<<15;muzzleCamera.clearFlags=CameraClearFlags.SolidColor;muzzleCamera.backgroundColor=new Color(.08f,.1f,.13f);muzzleCamera.nearClipPlane=.001f;muzzleCamera.farClipPlane=2;
            var muzzleCenter=(muzzle.Muzzle(0)+muzzle.Muzzle(1))*.5f;muzzleCamera.transform.position=muzzleCenter+camera.transform.TransformDirection(new Vector3(.2f,.1f,.22f));muzzleCamera.transform.LookAt(muzzleCenter,camera.transform.up);
            yield return Capture(muzzleCamera,"11-muzzle-detail");Object.Destroy(muzzleCamera.gameObject);
            // Magnify the actual emitted core without scaling or replacing its mesh.
            var pelletCamera=new GameObject("pellet-review-camera").AddComponent<Camera>();pelletCamera.transform.SetParent(ground.transform,false);pelletCamera.nearClipPlane=.0001f;pelletCamera.farClipPlane=.05f;pelletCamera.orthographic=true;pelletCamera.orthographicSize=cores[0].transform.lossyScale.x*.8f;pelletCamera.clearFlags=CameraClearFlags.SolidColor;pelletCamera.backgroundColor=new Color(.08f,.10f,.13f);
            pelletCamera.transform.position=cores[0].transform.position+camera.transform.TransformDirection(new Vector3(.012f,.008f,-.018f));pelletCamera.transform.LookAt(cores[0].transform.position);
            var pelletLight=new GameObject("pellet-review-fill").AddComponent<Light>();pelletLight.transform.SetParent(pelletCamera.transform,false);pelletLight.transform.localPosition=new Vector3(-.012f,.015f,0);pelletLight.type=LightType.Point;pelletLight.range=.08f;pelletLight.intensity=.00015f;
            yield return Capture(pelletCamera,"10-pellet-detail");Object.Destroy(pelletCamera.gameObject);

            ground.Session.Tick(actions,.02f);Render();yield return Capture(camera,"06-muzzle-and-pellets");
            for(int i=0;i<5;i++)ground.Session.Tick(actions,.02f);Render();yield return null;
            var impacts=ground.GetComponentsInChildren<Transform>().Where(t=>t.name=="shot-impact").ToArray();
            Assert.That(shot.Pellets.Any(p=>p.Contact!=PelletContact.Miss),Is.True,"fixture must hit real architecture");Assert.That(impacts,Is.Not.Empty);
            Assert.That(impacts.All(t=>t.GetComponentsInChildren<Collider>().Length==0),Is.True);
            Assert.That(impacts.All(t=>t.GetComponentsInChildren<ParticleSystem>().Length==4),Is.True);
            Assert.That(impacts.SelectMany(t=>t.GetComponentsInChildren<ParticleSystem>()).All(p=>p.particleCount>0),Is.True);
            foreach(var r in impacts.SelectMany(t=>t.GetComponentsInChildren<ParticleSystemRenderer>()).Where(r=>r.renderMode==ParticleSystemRenderMode.Mesh))
            {
                var m=r.mesh;var vertices=m.vertices;var triangles=m.triangles;
                for(int i=0;i<triangles.Length;i+=3){var a=vertices[triangles[i]];var b=vertices[triangles[i+1]];var c=vertices[triangles[i+2]];Assert.That(Vector3.Dot(Vector3.Cross(b-a,c-a),(a+b+c)/3-m.bounds.center),Is.GreaterThan(0),"chip faces must point outwards");}
            }
            Assert.That(impacts.All(t=>shot.Pellets.Any(p=>Vector3.Distance(p.Endpoint,t.position)<.00001f)),Is.True);
            yield return Capture(camera,"07-world-contact");
            // Close-up of a real contact; no synthetic event or fabricated endpoint.
            var hit=impacts[0];var normal=hit.forward;var close=new GameObject("contact-review-camera").AddComponent<Camera>();close.transform.SetParent(ground.transform,false);close.clearFlags=CameraClearFlags.SolidColor;close.backgroundColor=new Color(.07f,.08f,.1f);close.nearClipPlane=.001f;close.farClipPlane=20;
            close.transform.position=hit.position+normal*.23f+Vector3.up*.045f;close.transform.LookAt(hit.position);yield return Capture(close,"08-contact-detail");Object.Destroy(close.gameObject);
            var frozen=ground.Session.Capture();ground.Session.Restore(frozen);Render();yield return null;
            Assert.That(ground.GetComponentsInChildren<Transform>().Any(t=>t.name=="pellet-metal-core"||t.name=="shot-impact"),Is.False);Assert.That(muzzle.LiveParticles,Is.Zero);
            var empty=ground.Session.Capture();empty.Lives[0].ShotgunAmmo=0;empty.Lives[0].Ammo=0;ground.Session.Restore(empty);actions[0].Fire=true;int count=ground.Session.ShotCount;ground.Session.Tick(actions,.02f);Render();Assert.That(ground.Session.ShotCount,Is.EqualTo(count));Assert.That(muzzle.LiveParticles,Is.Zero);
            if(!string.IsNullOrEmpty(output))File.WriteAllText(Path.Combine(output,"result.json"),"{\"actualSession\":true,\"pellets\":18,\"pauseRestoreDryFire\":true,\"humanAcceptance\":false}");
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
