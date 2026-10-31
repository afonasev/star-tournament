using System;
using System.Collections;
using System.Collections.Generic;
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
    /// <summary>Actual session shots, actual cameras and contacts. Captures do not add lights, targets or effects.</summary>
    public sealed class VectorPrecisionCycleTests
    {
        Scene scene;Gamepad pad;ProvingGround ground;bool previousAsync;
        RenderTexture target;Texture2D image;
        readonly List<string> metrics=new List<string>();
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        void Capture(Camera camera,string path)
        {
            if(path==null)return;
            if(!target){target=new RenderTexture(1600,900,24);image=new Texture2D(1600,900,TextureFormat.RGB24,false);}
            var old=camera.targetTexture;var active=RenderTexture.active;var rect=camera.rect;
            try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());}
            finally{camera.targetTexture=old;camera.rect=rect;RenderTexture.active=active;}
        }
        [UnityTest] public IEnumerator ActualPrecisionCycleIsGreyBoundedAndClockOwned()
        {
            previousAsync=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
            string output=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_VECTOR_CYCLE_CAPTURE");
            if(output!=null){Directory.CreateDirectory(output);Assert.That(SystemInfo.graphicsDeviceType,Is.Not.EqualTo(UnityEngine.Rendering.GraphicsDeviceType.Null));}
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();ground.enabled=false;
            pad=InputSystem.AddDevice<Gamepad>();ground.StartCombatReview(new[]{pad},ensureOpponent:true);yield return null;
            var snap=ground.Session.Capture();snap.Lives[0].ShotgunOwned=true;snap.Lives[0].ShotgunAmmo=20;ground.Session.Restore(snap);
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].SelectWeapon=WeaponSelection.Shotgun;ground.Session.Tick(actions,.02f);actions[0]=default;
            for(int i=0;i<60;i++)ground.Session.Tick(actions,.02f);
            var camera=ground.GetComponentsInChildren<Camera>().Single(c=>c.name=="seat-camera-1");var view=camera.transform.Find("trooper-view");
            var muzzle=view.GetComponent<VectorShotPresentation>();
            var ready=ground.Session.Capture();
            metrics.Add("scenario,frame,clock,core_count,streak_count,impact_count,particles,renderers,triangles");
            foreach(string scenario in new[]{"corridor","wall","robot"})
            {
                ground.Session.Restore(ready);
                ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0,scenario=="wall"?90:0);
                ground.PlaceCombatReviewSeat(1,scenario=="robot"?new Vector3(0,-1.2f,-1):new Vector3(5,-1.2f,5),0,180);
                Render();yield return null;
                string dir=output==null?null:Path.Combine(output,scenario);if(dir!=null)Directory.CreateDirectory(dir);
                ShotNotice shot=default;Action<ShotNotice> handler=n=>{if(n.Shooter==0)shot=n;};ground.Session.ShotResolved+=handler;
                bool sawContact=false,sawTail=false;
                for(int frame=0;frame<65;frame++)
                {
                    if(frame==10)actions[0].Fire=true;
                    ground.Session.Tick(actions,.02f);actions[0]=default;Render();yield return null;
                    var traces=ground.GetComponentsInChildren<Transform>().Where(t=>t.name=="pellet-streak").ToArray();
                    var cores=ground.GetComponentsInChildren<MeshFilter>().Where(t=>t.name=="pellet-metal-core").ToArray();
                    var contacts=ground.GetComponentsInChildren<Transform>().Where(t=>t.name=="shot-impact").ToArray();
                    var particles=muzzle.GetComponentsInChildren<ParticleSystem>().Concat(contacts.SelectMany(t=>t.GetComponentsInChildren<ParticleSystem>())).ToArray();
                    foreach(var trace in traces)
                    {
                        Assert.That(trace.localScale.y*2,Is.LessThanOrEqualTo(ground.Profile.Get("presentation.vectorPrecision.traceLength")+.0001f));
                        var mat=trace.GetComponent<Renderer>().sharedMaterial;var color=mat.color;
                        Assert.That(color.r,Is.EqualTo(color.g).Within(.00001f));Assert.That(color.g,Is.EqualTo(color.b).Within(.00001f));Assert.That(mat.IsKeywordEnabled("_EMISSION"),Is.False);
                        Assert.That(trace.GetComponents<Collider>().All(c=>!c.enabled),Is.True);
                    }
                    if(frame==10)
                    {
                        Assert.That(shot.Pellets.Length,Is.EqualTo(18));Assert.That(cores.Length,Is.EqualTo(18));
                        var mounts=view.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("vector-muzzle-")).ToArray();
                        if(dir!=null)File.WriteAllText(Path.Combine(dir,"muzzles.txt"),string.Join("\n",mounts.Select(t=>$"{t.name}: world={t.position:R}, forward={t.forward:R}, screen={camera.WorldToViewportPoint(t.position):R}")));
                        for(int i=0;i<traces.Length;i++)
                        {
                            var trace=traces[i];var tail=trace.position-trace.up*trace.localScale.y;var origin=muzzle.Muzzle(i);var end=shot.Pellets[i].Endpoint;
                            var expected=origin+(end-origin).normalized*Mathf.Min(ground.Profile.Get("presentation.shotMuzzleClearance"),Vector3.Distance(origin,end)*.5f);
                            Assert.That(Vector3.Distance(tail,expected),Is.LessThan(.001f),"first visible tail at its actual muzzle");
                        }
                    }
                    foreach(var contact in contacts)
                    {
                        sawContact=true;Assert.That(shot.Pellets.Any(p=>Vector3.Distance(p.Endpoint,contact.position)<.00001f),Is.True);
                        Assert.That(contact.GetComponentsInChildren<Collider>(),Is.Empty);
                        var layers=contact.GetComponentsInChildren<ParticleSystem>();
                        Assert.That(layers.Length,Is.EqualTo(4));
                        if(frame==19)
                        {
                            var moving=layers.Single(p=>p.name=="vector-impact-chips");var data=new ParticleSystem.Particle[moving.main.maxParticles];int count=moving.GetParticles(data);
                            Assert.That(data.Take(count).Any(p=>Vector3.Distance(p.position,contact.position)>.01f),Is.True,"native fragments must actually leave the hit position");
                        }
                        if(layers.Single(p=>p.name=="vector-impact-sparks").particleCount==0&&layers.Single(p=>p.name=="vector-impact-dust").particleCount>0)sawTail=true;
                    }
                    var state=JsonUtility.ToJson(ground.Session.Capture());var times=particles.Select(p=>p.time).ToArray();Render();Render();
                    Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(state));CollectionAssert.AreEqual(times,particles.Select(p=>p.time).ToArray());
                    if(dir!=null && (frame==16||frame==19))
                    {
                        var debug=new List<string>();
                        foreach(var ps in particles)
                        {
                            var batch=new ParticleSystem.Particle[ps.main.maxParticles];int n=ps.GetParticles(batch);
                            var r=ps.GetComponent<ParticleSystemRenderer>();
                            debug.Add($"{ps.name} time={ps.time} layer={ps.gameObject.layer} scale={ps.transform.lossyScale} material={r.sharedMaterial.name} radiance={(r.sharedMaterial.HasProperty("_Radiance")?r.sharedMaterial.GetFloat("_Radiance"):0)} visible={r.isVisible} cameraMask={camera.cullingMask}");
                            for(int i=0;i<n;i++)debug.Add($" {i} position={batch[i].position:R} velocity={batch[i].velocity:R} size={batch[i].GetCurrentSize(ps):R} color={batch[i].GetCurrentColor(ps)} life={batch[i].remainingLifetime}");
                        }
                        File.WriteAllLines(Path.Combine(dir,$"particles-{frame}.txt"),debug);
                    }
                    Capture(camera,dir==null?null:Path.Combine(dir,$"frame-{frame:D3}.png"));
                    int triangleCount=cores.Sum(m=>m.sharedMesh.triangles.Length/3)+traces.Sum(t=>t.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3);
                    metrics.Add(FormattableString.Invariant($"{scenario},{frame},{ground.Session.Time},{cores.Length},{traces.Length},{contacts.Length},{particles.Sum(p=>p.particleCount)},{cores.Length+traces.Length+particles.Length},{triangleCount}"));
                    if(frame==64){Assert.That(cores,Is.Empty);Assert.That(traces,Is.Empty);Assert.That(contacts,Is.Empty);Assert.That(muzzle.LiveParticles,Is.Zero);}
                }
                ground.Session.ShotResolved-=handler;
                Assert.That(sawContact,Is.True,scenario);Assert.That(sawTail,Is.True,"separate contact dust tail "+scenario);
                if(scenario=="robot")Assert.That(shot.Pellets.Any(p=>p.Contact==PelletContact.Participant),Is.True,"actual participant contact, not synthetic VFX");
            }
            // Focused shotgun witness remains independent of the legacy multi-weapon fixture's rifle branch.
            foreach(int side in new[]{0,-1,1})
            {
                ground.Session.Restore(ready);ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);Render();yield return null;
                var move=new LocalAction[ground.Session.ParticipantCount];move[0].Move=new Vector2(side,0);
                for(int tick=0;tick<5;tick++)ground.Session.Tick(move,.02f);
                move[0].LookDegrees=new Vector2(side*12,side==0?0:4);move[0].Fire=true;
                ShotNotice shot=default;Action<ShotNotice> resolved=n=>{if(n.Shooter==0)shot=n;};ground.Session.ShotResolved+=resolved;
                ground.Session.Tick(move,.02f);move[0].Fire=false;move[0].LookDegrees=Vector2.zero;
                ground.Session.Tick(move,.02f); // Batched ticks before the next render.
                var state=JsonUtility.ToJson(ground.Session.Capture());Render();
                var traces=ground.GetComponentsInChildren<Transform>().Where(t=>t.name=="pellet-streak").ToArray();Assert.That(traces.Length,Is.EqualTo(shot.Pellets.Length));
                for(int pellet=0;pellet<traces.Length;pellet++)
                {
                    var origin=muzzle.Muzzle(pellet);var end=shot.Pellets[pellet].Endpoint;
                    var expected=origin+(end-origin).normalized*Mathf.Min(ground.Profile.Get("presentation.shotMuzzleClearance"),Vector3.Distance(origin,end)*.5f);
                    var tail=traces[pellet].position-traces[pellet].up*traces[pellet].localScale.y;
                    Assert.That(Vector3.Distance(tail,expected),Is.LessThan(.001f),"moving VECTOR muzzle "+side);
                }
                Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(state));ground.Session.ShotResolved-=resolved;
            }
            if(output!=null){File.WriteAllLines(Path.Combine(output,"metrics.csv"),metrics);File.WriteAllText(Path.Combine(output,"profile.json"),JsonUtility.ToJson(ground.Profile,true));}
        }
        [UnityTearDown]public IEnumerator Cleanup()
        {
            UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsync;
            if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(target){target.Release();Object.Destroy(target);}if(image)Object.Destroy(image);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
