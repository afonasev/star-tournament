using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    // Opt-in diagnostic: timings describe this host, never a target-device acceptance gate.
    public sealed class VectorPerformanceDiagnosticTests
    {
        Scene scene; Gamepad pad;
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest, Explicit("Run exclusively with an explicit diagnostic filter")]
        public IEnumerator FrozenBodyContactCost()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);
            ground.enabled=false;yield return null;
            var body=ground.transform.Find("player-2/trooper-presentation").gameObject;
            var chest=body.GetComponentsInChildren<Transform>().Single(t=>t.name=="Chest");
            var point=chest.position+body.transform.right*.27f+Vector3.up*.12f;
            var lines=new System.Collections.Generic.List<string>{"operation,index,milliseconds"};
            for(int i=0;i<8;i++)
            {
                var watch=Stopwatch.StartNew();var contact=SkinnedContactPatch.Find(body,point,-body.transform.forward);watch.Stop();
                Assert.That(contact,Is.Not.Null);lines.Add($"find,{i},{watch.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                watch.Restart();var patch=SkinnedContactPatch.Create(contact,.1f,null,"diagnostic-contact",out var mesh);watch.Stop();
                Assert.That(patch,Is.Not.Null);lines.Add($"create,{i},{watch.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                UnityEngine.Object.Destroy(patch);UnityEngine.Object.Destroy(mesh);yield return null;
            }
            // Compare an eight-contact burst against the preserved exhaustive algorithm
            // in the same pose/process. No cross-frame pose cache is involved.
            foreach(bool optimized in new[]{false,true})
                for(int sample=0;sample<3;sample++)
                {
                    var watch=Stopwatch.StartNew();
                    var projection=optimized?new SkinnedContactPatch.Projection(body):null;
                    for(int pellet=0;pellet<8;pellet++)
                    {
                        var hit=point+body.transform.right*((pellet-4)*.008f);
                        GameObject patch;Mesh mesh;
                        if(optimized)
                        {
                            var contact=projection.Find(hit,-body.transform.forward);Assert.That(contact,Is.Not.Null);
                            patch=SkinnedContactPatch.Create(contact,.1f,null,"volley",out mesh);
                        }
                        else
                        {
                            var contact=ReferenceSkinnedContactPatch.Find(body,hit,-body.transform.forward);Assert.That(contact,Is.Not.Null);
                            patch=ReferenceSkinnedContactPatch.Create(contact,.1f,null,"volley",out mesh);
                        }
                        Assert.That(patch,Is.Not.Null);UnityEngine.Object.DestroyImmediate(patch);UnityEngine.Object.DestroyImmediate(mesh);
                    }
                    watch.Stop();lines.Add($"{(optimized?"volley-optimized":"volley-reference")},{sample},{watch.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    yield return null;
                }
            var path=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_PERF_EVIDENCE");
            Assert.That(path,Is.Not.Null.And.Not.Empty);Directory.CreateDirectory(path);File.WriteAllLines(Path.Combine(path,"contacts.csv"),lines);
            UnityEngine.Debug.Log(string.Join("\n",lines));
        }
        [UnityTest] public IEnumerator TinySkinnedFacesKeepUnitNormalsAndVisibleContactPatches()
        {
            var owner=new GameObject("tiny-contact-body");var bone=new GameObject("tiny-contact-bone");bone.transform.SetParent(owner.transform,false);
            var skin=owner.AddComponent<SkinnedMeshRenderer>();var source=new Mesh();
            source.vertices=new[]{new Vector3(-.001f,-.001f,0),new Vector3(.001f,-.001f,0),new Vector3(-.001f,.001f,0),new Vector3(.001f,.001f,0)};
            source.normals=Enumerable.Repeat(Vector3.forward,4).ToArray();source.triangles=new[]{0,1,2,2,1,3};
            source.boneWeights=Enumerable.Repeat(new BoneWeight{boneIndex0=0,weight0=1},4).ToArray();source.bindposes=new[]{Matrix4x4.identity};source.RecalculateBounds();
            skin.sharedMesh=source;skin.bones=new[]{bone.transform};skin.rootBone=bone.transform;skin.updateWhenOffscreen=true;skin.localBounds=new Bounds(Vector3.zero,Vector3.one*.01f);
            try
            {
                foreach(bool cached in new[]{false,true})
                {
                    var contact=new SkinnedContactPatch.Projection(owner,immutableTopology:cached).Find(new Vector3(0,0,.001f),Vector3.back);
                    Assert.That(contact,Is.Not.Null);Assert.That(contact.Normal.sqrMagnitude,Is.EqualTo(1).Within(.000001f));Assert.That(Vector3.Dot(contact.Normal,Vector3.forward),Is.GreaterThan(.99999f));
                    var patch=SkinnedContactPatch.Create(contact,.01f,null,"hit-contact-tiny-test",out var mesh);
                    try{Assert.That(patch,Is.Not.Null);Assert.That(mesh.triangles.Length,Is.EqualTo(6));Assert.That(mesh.vertexCount,Is.EqualTo(4));}
                    finally{if(patch)UnityEngine.Object.Destroy(patch);if(mesh)UnityEngine.Object.Destroy(mesh);}
                }
            }
            finally{UnityEngine.Object.Destroy(owner);UnityEngine.Object.Destroy(source);}
            yield return null;
        }
        [UnityTest] public IEnumerator ContactProjectionMatchesExhaustiveGeometry()
        {
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);
            ground.enabled=false;yield return null;
            var body=ground.transform.Find("player-2/trooper-presentation").gameObject;
            var chest=body.GetComponentsInChildren<Transform>().Single(t=>t.name=="Chest");
            foreach(var scale in new[]{Vector3.one,new Vector3(.8f,1.2f,1.1f)})
            {
                body.transform.localScale=scale;body.transform.localRotation=Quaternion.Euler(0,37,0);
                // Exercise the product cache across contacts and changed transform/pose.
                var projection=new SkinnedContactPatch.Projection(body,immutableTopology:true);
                for(int i=0;i<12;i++)
                {
                    var point=chest.position+body.transform.right*(i%3-1)*.27f+Vector3.up*(i/3-1)*.15f;
                    var direction=i%2==0?-body.transform.forward:body.transform.right;
                    var expected=ReferenceSkinnedContactPatch.Find(body,point,direction,preciseNormals:true);
                    var actual=projection.Find(point,direction);
                    Assert.That(actual==null,Is.EqualTo(expected==null));if(expected==null)continue;
                    Assert.That(actual.Skin,Is.SameAs(expected.Skin));
                    Assert.That(Vector3.Distance(actual.Point,expected.Point),Is.LessThan(.00001f));
                    Assert.That(Vector3.Distance(actual.Normal,expected.Normal),Is.LessThan(.00001f));
                    var oldPatch=ReferenceSkinnedContactPatch.Create(expected,.1f,null,"reference",out var oldMesh,preciseNormals:true);
                    var newPatch=SkinnedContactPatch.Create(actual,.1f,null,"candidate",out var newMesh);
                    try
                    {
                        Assert.That(newPatch==null,Is.EqualTo(oldPatch==null));if(!oldMesh)continue;
                        CollectionAssert.AreEqual(oldMesh.triangles,newMesh.triangles);
                        CollectionAssert.AreEqual(oldMesh.vertices,newMesh.vertices);
                        CollectionAssert.AreEqual(oldMesh.normals,newMesh.normals);
                        CollectionAssert.AreEqual(oldMesh.boneWeights,newMesh.boneWeights);
                        CollectionAssert.AreEqual(oldMesh.bindposes,newMesh.bindposes);
                        var a=oldMesh.uv;var b=newMesh.uv;Assert.That(b.Length,Is.EqualTo(a.Length));
                        for(int v=0;v<a.Length;v++)Assert.That(Vector2.Distance(a[v],b[v]),Is.LessThan(.0001f));
                    }
                    finally
                    {
                        if(oldPatch)UnityEngine.Object.DestroyImmediate(oldPatch);if(newPatch)UnityEngine.Object.DestroyImmediate(newPatch);
                        if(oldMesh)UnityEngine.Object.DestroyImmediate(oldMesh);if(newMesh)UnityEngine.Object.DestroyImmediate(newMesh);
                    }
                }
            }
        }
        [UnityTest, Explicit("Graphical exclusive diagnostic; synchronized render cost, not gameplay FPS")]
        public IEnumerator FrozenRenderAblation()
        {
            Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(GraphicsDeviceType.Null));
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            var ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},backgroundDiagnostic:true,ensureOpponent:true);
            ground.enabled=false;ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,-3),0,0);ground.PlaceCombatReviewSeat(1,new Vector3(-8,0,1),0,180);
            ground.SendMessage("LateUpdate");yield return null;
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var world=ground.GetComponentsInChildren<TrooperVisual>().Where(v=>v.name=="trooper-presentation").SelectMany(v=>v.GetComponentsInChildren<Renderer>()).ToArray();
            var hands=ground.transform.Find("seat-camera-1/trooper-view").GetComponentsInChildren<Renderer>();
            var all=world.Concat(hands).ToArray();var enabled=all.Select(r=>r.enabled).ToArray();
            var target=new RenderTexture(1920,1080,24);target.Create();var pixel=new Texture2D(1,1,TextureFormat.RGB24,false);
            var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;
            var directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_PERF_EVIDENCE");Directory.CreateDirectory(directory);
            var lines=new System.Collections.Generic.List<string>{"state,index,synchronized_render_ms"};
            try
            {
                camera.targetTexture=target;
                foreach(var state in new[]{"all","world-hidden","hands-hidden","all-hidden","all-repeat"})
                {
                    for(int r=0;r<all.Length;r++)all[r].enabled=enabled[r];
                    if(state=="world-hidden"||state=="all-hidden")foreach(var r in world)r.enabled=false;
                    if(state=="hands-hidden"||state=="all-hidden")foreach(var r in hands)r.enabled=false;
                    for(int i=0;i<25;i++)
                    {
                        var watch=Stopwatch.StartNew();
                        if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
                        RenderTexture.active=target;pixel.ReadPixels(new Rect(0,0,1,1),0,0);pixel.Apply();watch.Stop();
                        if(i>=5)lines.Add($"{state},{i},{watch.Elapsed.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                        yield return null;
                    }
                    RenderTexture.active=target;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();
                    File.WriteAllBytes(Path.Combine(directory,state+".png"),image.EncodeToPNG());UnityEngine.Object.Destroy(image);
                }
                File.WriteAllLines(Path.Combine(directory,"render.csv"),lines);
            }
            finally
            {
                for(int r=0;r<all.Length;r++)all[r].enabled=enabled[r];camera.targetTexture=oldTarget;RenderTexture.active=oldActive;
                target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixel);
            }
        }
    }
}
