using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class HitFeedbackPresentationTests
    {
        Scene scene;ProvingGround ground;readonly Gamepad[] pads=new Gamepad[4];GameObject body;TrooperVisual visual;
        IEnumerator Load(int seats=1)
        {
            AudioListener.volume=0;
            yield return NativeLoadingTestScene.Load();scene=SceneManager.GetSceneByName("ProvingGround");yield return null;
            ground=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ProvingGround>()).Single();
            for(int i=0;i<seats;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
            ground.StartCombatReview(pads.Take(seats).ToArray(),backgroundDiagnostic:true,ensureOpponent:true);
            ground.enabled=false;yield return null;
            body=ground.transform.Find("player-2/trooper-presentation").gameObject;visual=body.GetComponent<TrooperVisual>();
            visual.Render(ground.Session.Time,Vector3.zero,ground.Session.Life(1).Health,ground.Session.Life(1).Life);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);}
        Transform Bone(string name)=>body.GetComponentsInChildren<Transform>().Single(t=>t.name==name);
        DamageNotice Notice(float health,float armor,Vector3 direction=default)
        {
            var chest=Bone("Chest");return new DamageNotice(1,ground.Session.Life(1).Life,health,armor,ground.Session.Time,
                new FatalImpact(WeaponId.Rifle,1,direction==Vector3.zero?-body.transform.forward:direction,chest.position+body.transform.right*.27f+Vector3.up*.12f));
        }
        void Advance(float dt){ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],dt);ground.SendMessage("LateUpdate");}
        [UnityTest] public IEnumerator ActualWeaponDamageDrivesShieldThenHealthEffects()
        {
            yield return Load();ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,2),0,0);ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,5),0,180);
            ground.SendMessage("LateUpdate");var saved=ground.Session.Capture();saved.Lives[1].Armor=100;ground.Session.Restore(saved);Advance(.02f);
            DamageNotice received=default;ground.Session.Damaged+=n=>received=n;
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;ground.Session.Tick(actions,.02f);
            for(int i=0;i<40&&!received.Impact.Valid;i++)Advance(.01f);
            Assert.That(received.Impact.Valid,Is.True);Assert.That(received.ArmorLost,Is.GreaterThan(0));Assert.That(received.HealthLost,Is.Zero);
            Assert.That(ground.HitFeedbackForReview.ShieldFlashes,Is.GreaterThan(0));Assert.That(ground.HitFeedbackForReview.BloodMarks,Is.Zero);
            Assert.That(ground.GetComponentsInChildren<Transform>().Any(t=>t.name=="blood-drop"),Is.False);
            saved=ground.Session.Capture();saved.Lives[1].Armor=0;ground.Session.Restore(saved);Advance(.6f);received=default;
            ground.Session.Tick(actions,.02f);for(int i=0;i<40&&!received.Impact.Valid;i++)Advance(.01f);
            Assert.That(received.HealthLost,Is.GreaterThan(0));Assert.That(ground.HitFeedbackForReview.BloodMarks,Is.GreaterThan(0));
            Assert.That(ground.HitFeedbackForReview.ShieldFlashes,Is.Zero);
        }
        [UnityTest] public IEnumerator ActualShotgunPreservesIndividualTargetContactsAndSingleDamageNotice()
        {
            yield return Load();ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,2),0,0);ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,5),0,180);
            ground.SendMessage("LateUpdate");var saved=ground.Session.Capture();
            saved.Lives[0].ShotgunOwned=true;saved.Lives[0].ShotgunAmmo=20;saved.Lives[0].SelectedWeapon=WeaponId.Shotgun;saved.Lives[0].Ammo=20;
            saved.Lives[1].Armor=100;ground.Session.Restore(saved);Advance(.02f);
            DamageNotice received=default;ShotNotice shot=default;int count=0;
            ground.Session.Damaged+=n=>{if(n.Participant==1){received=n;count++;}};ground.Session.ShotResolved+=n=>shot=n;
            var actions=new LocalAction[ground.Session.ParticipantCount];actions[0].Fire=true;ground.Session.Tick(actions,.02f);
            var hits=shot.Pellets.Where(p=>p.Contact==PelletContact.Participant&&p.TargetSeat==1&&p.TargetLife==received.Life).ToArray();
            Assert.That(hits.Length,Is.GreaterThan(1));Assert.That(count,Is.EqualTo(1));Assert.That(received.HealthLost,Is.Zero);
            Assert.That(received.Contacts.Count,Is.EqualTo(hits.Length));
            for(int i=0;i<hits.Length;i++)
            {Assert.That(received.Contacts[i].Point,Is.EqualTo(hits[i].Endpoint));Assert.That(Vector3.Distance(received.Contacts[i].Direction,hits[i].Direction.normalized),Is.LessThan(.0001f));}
            Assert.That(received.Contacts.Select(c=>c.Point).Distinct().Count(),Is.GreaterThan(1));
            Assert.That(ground.HitFeedbackForReview.ShieldFlashes,Is.GreaterThan(1));Assert.That(ground.HitFeedbackForReview.BloodMarks,Is.Zero);
            Assert.That(100-ground.Session.Life(1).Armor,Is.EqualTo(received.ArmorLost).Within(.001f));
        }
        DamageNotice ShotgunNotice(float health,float armor,int count=3)
        {
            var chest=Bone("Chest");var points=new[]{chest.position+body.transform.right*.27f+Vector3.up*.12f,
                chest.position-body.transform.right*.27f+Vector3.up*.12f,chest.position-Vector3.up*.25f};
            var contacts=Enumerable.Range(0,count).Select(i=>new FatalImpact(WeaponId.Shotgun,2,-body.transform.forward,points[i%points.Length])).ToArray();
            return new DamageNotice(1,ground.Session.Life(1).Life,health,armor,ground.Session.Time,contacts[0],Array.AsReadOnly(contacts));
        }
        [UnityTest] public IEnumerator ShotgunPatchesUseSeparatePositionsAndShotLevelRateLimitsWithinBudget()
        {
            yield return Load();var effects=ground.HitFeedbackForReview;string before=JsonUtility.ToJson(ground.Session.Capture());
            var notice=ShotgunNotice(0,10);effects.Observe(notice);
            Assert.That(effects.ShieldFlashes,Is.EqualTo(3));Assert.That(effects.BloodMarks,Is.Zero);
            var patches=body.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name=="hit-contact-shield").ToArray();
            Assert.That(patches.Select(r=>r.transform.parent.name+string.Join(";",r.sharedMesh.uv.Select(v=>v.ToString("F4")))).Distinct().Count(),Is.EqualTo(3),"Each patch has its own contact projection");
            effects.Observe(notice);Assert.That(effects.ShieldFlashes,Is.EqualTo(3),"Repeated notice is limited as a shot, while all its pellets were admitted together");
            effects.Clear();effects.Observe(ShotgunNotice(10,0));Assert.That(effects.BloodMarks,Is.EqualTo(3));Assert.That(effects.ShieldFlashes,Is.Zero);
            effects.Observe(ShotgunNotice(10,0));Assert.That(effects.BloodMarks,Is.EqualTo(3));
            effects.Clear();effects.Observe(ShotgunNotice(5,5,40));
            Assert.That(effects.BloodMarks,Is.EqualTo((int)ground.HitFeedbackProfile.Get("hit.maxBloodMarksPerParticipant")));
            Assert.That(effects.ShieldFlashes,Is.EqualTo((int)ground.HitFeedbackProfile.Get("hit.maxShieldFlashes")));
            Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(before));
            effects.Clear();Assert.That(effects.BloodMarks+effects.ShieldFlashes,Is.Zero);
        }
        [UnityTest] public IEnumerator CaptureShotgunSeparateContactPositions()
        {
            string directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_SHOTGUN_CAPTURE");
            if(string.IsNullOrEmpty(directory)){Assert.Ignore("Visual capture runs separately with graphics enabled");yield break;}
            yield return Load();Directory.CreateDirectory(directory);var effects=ground.HitFeedbackForReview;
            effects.Observe(ShotgunNotice(8,0));Advance(.03f);yield return Capture(Path.Combine(directory,"shotgun-health.png"));
            effects.Clear();Advance(.3f);effects.Observe(ShotgunNotice(0,8));Advance(.03f);yield return Capture(Path.Combine(directory,"shotgun-shield.png"));
        }
        [UnityTest] public IEnumerator ArmorAndHealthUseSeparateSkinEffectsAndPreserveAuthoritativeState()
        {
            yield return Load();var effects=ground.HitFeedbackForReview;string before=JsonUtility.ToJson(ground.Session.Capture());
            effects.Observe(Notice(0,10));Assert.That(effects.ShieldFlashes,Is.EqualTo(1));Assert.That(effects.BloodMarks,Is.Zero);
            Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(before));
            effects.Clear();effects.Observe(Notice(10,0));Assert.That(effects.BloodMarks,Is.EqualTo(1));Assert.That(effects.ShieldFlashes,Is.Zero);
            var patch=body.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="hit-contact-blood");
            Assert.That(patch.bones.All(b=>b),Is.True);Assert.That(patch.sharedMesh.boneWeights.Length,Is.EqualTo(patch.sharedMesh.vertexCount));
            Assert.That(patch.GetComponents<Collider>(),Is.Empty);
            var source=patch.transform.parent.GetComponent<SkinnedMeshRenderer>();CollectionAssert.AreEqual(source.bones,patch.bones);
            var baked=new Mesh();patch.BakeMesh(baked);var first=baked.vertices;
            visual.Render(.2f,new Vector3(0,0,8),100,ground.Session.Life(1).Life);patch.BakeMesh(baked);
            Assert.That(baked.vertices.Where((v,i)=>Vector3.Distance(v,first[i])>.0001f).Any(),Is.True,"Patch follows the animated skin");Object.Destroy(baked);
            effects.Clear();effects.Observe(Notice(5,5));Assert.That(effects.BloodMarks,Is.EqualTo(1));Assert.That(effects.ShieldFlashes,Is.EqualTo(1));
            effects.Clear();effects.Observe(Notice(0,0));Assert.That(effects.BloodMarks+effects.ShieldFlashes,Is.Zero);
            ground.Session.Restore(ground.Session.Capture());Assert.That(effects.BloodMarks+effects.ShieldFlashes,Is.Zero);
        }
        [UnityTest] public IEnumerator TorsoReactsDirectionallyWithoutChangingFeetRootOrFrozenPose()
        {
            yield return Load();var chest=Bone("Chest");var feet=new[]{Bone("LeftFoot"),Bone("RightFoot"),Bone("Hips")};int life=ground.Session.Life(1).Life;
            double now=ground.Session.Time;visual.ClearHit();visual.Render(now+.03,Vector3.zero,100,life);var baseChest=chest.localRotation;var baseFeet=feet.Select(t=>t.position).ToArray();var root=body.transform.position;
            visual.Hit(Notice(0,10));visual.Render(now+.03,Vector3.zero,100,life);
            Assert.That(Quaternion.Angle(baseChest,chest.localRotation),Is.GreaterThan(5));Assert.That(body.transform.position,Is.EqualTo(root));
            for(int i=0;i<feet.Length;i++)Assert.That(Vector3.Distance(baseFeet[i],feet[i].position),Is.LessThan(.0001f));
            var frozen=chest.localRotation;visual.Render(now+.03,Vector3.zero,100,life);Assert.That(Quaternion.Angle(frozen,chest.localRotation),Is.LessThan(.01f));
            visual.Render(now+.16,Vector3.zero,100,life);var midReturn=chest.localRotation;
            visual.Hit(new DamageNotice(1,life,10,0,now+.16,new FatalImpact(WeaponId.Rifle,2,body.transform.right,Bone("Chest").position)));
            visual.Render(now+.16,Vector3.zero,100,life);Assert.That(Quaternion.Angle(midReturn,chest.localRotation),Is.LessThan(.01f),"A new hit starts from the current recoil rather than snapping to neutral");
            visual.Render(now+1,Vector3.zero,100,life);Assert.That(visual.HitWeight,Is.Zero);
            visual.ClearHit();visual.Hit(Notice(10,0,body.transform.right));visual.Render(now+.03,Vector3.zero,100,life);var side=chest.localRotation;
            Assert.That(Quaternion.Angle(frozen,side),Is.GreaterThan(5));
            visual.ClearHit();visual.Hit(Notice(10,0,Vector3.up));visual.Render(now+.03,Vector3.zero,100,life);
            Assert.That(visual.HitWeight,Is.GreaterThan(.9f),"A vertical blast uses a stable facing-derived recoil");
            visual.BeginDeath(.03);Assert.That(visual.HitWeight,Is.Zero);
        }
        [UnityTest] public IEnumerator ContactBudgetPauseExpiryAndDeathCloneDoNotLeak()
        {
            yield return Load();var effects=ground.HitFeedbackForReview;
            for(int i=0;i<40;i++){effects.Observe(Notice(1,1));Advance(.21f);}
            Assert.That(effects.BloodMarks,Is.LessThanOrEqualTo((int)ground.HitFeedbackProfile.Get("hit.maxBloodMarks")));
            int count=effects.BloodMarks;effects.Render();yield return new WaitForSecondsRealtime(.1f);effects.Render();Assert.That(effects.BloodMarks,Is.EqualTo(count));
            for(int i=0;i<45;i++)Advance(.21f);Assert.That(effects.BloodMarks+effects.ShieldFlashes,Is.Zero);
            effects.Observe(Notice(1,1));Assert.That(effects.BloodMarks,Is.GreaterThan(0));effects.ClearParticipant(1);
            var clone=Object.Instantiate(body);Assert.That(clone.GetComponentsInChildren<Renderer>(true).Any(r=>r.name.StartsWith("hit-contact-")),Is.False);Object.Destroy(clone);
            ground.Session.Restore(ground.Session.Capture());Assert.That(effects.BloodMarks+effects.ShieldFlashes,Is.Zero);
        }
        [UnityTest] public IEnumerator ShieldWorksWithBloodDisabledAndFourSeatsShareOneBudget()
        {
            yield return Load(4);var disabled=ProvingProfile.CreateBloodDefault();disabled.Set("blood.enabled",0);
            ground.HitFeedbackForReview.Clear();
            using(var effects=new HitFeedbackPresentation(ground.Session,new[]{ground.transform.Find("player-1/trooper-presentation").gameObject,body,
                ground.transform.Find("player-3/trooper-presentation").gameObject,ground.transform.Find("player-4/trooper-presentation").gameObject},ground.HitFeedbackProfile,disabled))
            {
                effects.Observe(Notice(5,5));Assert.That(effects.BloodMarks,Is.Zero);Assert.That(effects.ShieldFlashes,Is.EqualTo(1));
                for(int i=0;i<4;i++)effects.Render();Assert.That(effects.ShieldFlashes,Is.EqualTo(1),"Cameras do not duplicate effects");
                effects.Clear();Assert.That(effects.ShieldFlashes,Is.Zero);
            }
        }
        [UnityTest] public IEnumerator CaptureEditorHealthShieldAndDirectionalReaction()
        {
            string directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_HIT_CAPTURE");
            if(string.IsNullOrEmpty(directory)){Assert.Ignore("Visual capture runs separately with graphics enabled");yield break;}
            yield return Load();Directory.CreateDirectory(directory);var effects=ground.HitFeedbackForReview;
            yield return Capture(Path.Combine(directory,"01-before.png"));
            effects.Observe(Notice(8,0));Advance(.03f);yield return Capture(Path.Combine(directory,"02-health-hit.png"));
            Advance(.7f);yield return Capture(Path.Combine(directory,"03-blood-on-body.png"));
            effects.Clear();effects.Observe(Notice(0,8));Advance(.03f);yield return Capture(Path.Combine(directory,"04-shield-hit.png"));
            Advance(.4f);yield return Capture(Path.Combine(directory,"05-shield-expired.png"));
            effects.Observe(Notice(8,0,body.transform.right));Advance(.03f);yield return Capture(Path.Combine(directory,"06-side-hit.png"));
            string frames=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_HIT_FRAMES");
            if(!string.IsNullOrEmpty(frames))
            {
                Directory.CreateDirectory(frames);effects.Clear();Advance(.5f);
                for(int i=0;i<36;i++)
                {
                    if(i==6)effects.Observe(Notice(8,0));
                    yield return Capture(Path.Combine(frames,"frame-"+i.ToString("D3")+".png"));Advance(1f/30);
                }
            }
        }
        [UnityTest] public IEnumerator CaptureFourSeatContactViews()
        {
            string directory=Environment.GetEnvironmentVariable("STAR_TOURNAMENT_HIT_CAPTURE");
            if(string.IsNullOrEmpty(directory)){Assert.Ignore("Visual capture runs separately with graphics enabled");yield break;}
            yield return Load(4);Directory.CreateDirectory(directory);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,2),0,0);ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,5),0,180);
            ground.PlaceCombatReviewSeat(2,new Vector3(-1,-1.2f,2),0,18);ground.PlaceCombatReviewSeat(3,new Vector3(1,-1.2f,2),0,-18);ground.SendMessage("LateUpdate");
            ground.HitFeedbackForReview.Observe(Notice(8,0));Advance(.03f);
            for(int i=0;i<4;i++)yield return CaptureCamera(ground.transform.Find("seat-camera-"+(i+1)).GetComponent<Camera>(),Path.Combine(directory,"07-health-seat-"+(i+1)+".png"));
            ground.HitFeedbackForReview.Clear();Advance(.3f);ground.HitFeedbackForReview.Observe(Notice(0,8));Advance(.03f);
            for(int i=0;i<4;i++)yield return CaptureCamera(ground.transform.Find("seat-camera-"+(i+1)).GetComponent<Camera>(),Path.Combine(directory,"08-shield-seat-"+(i+1)+".png"));
        }
        IEnumerator Capture(string path)
        {
            var go=new GameObject("hit-preview-camera");SceneManager.MoveGameObjectToScene(go,scene);var camera=go.AddComponent<Camera>();camera.enabled=false;
            var chest=Bone("Chest");camera.transform.position=chest.position+body.transform.forward*2.3f+body.transform.right*.25f+Vector3.up*.25f;
            camera.transform.LookAt(chest.position-Vector3.up*.12f);camera.fieldOfView=40;camera.nearClipPlane=.05f;
            camera.cullingMask=~((1<<5)|(1<<15)|(1<<16)|(1<<17)|(1<<18));
            yield return CaptureCamera(camera,path);Object.Destroy(go);
        }
        IEnumerator CaptureCamera(Camera camera,string path)
        {
            var oldRect=camera.rect;var oldTarget=camera.targetTexture;camera.rect=new Rect(0,0,1,1);
            var target=new RenderTexture(1200,900,24);target.Create();camera.targetTexture=target;yield return null;
            var previous=RenderTexture.active;
            if(GraphicsSettings.currentRenderPipeline!=null)RenderPipeline.SubmitRenderRequest(camera,new RenderPipeline.StandardRequest{destination=target});else camera.Render();
            RenderTexture.active=target;var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1200,900),0,0);pixels.Apply();File.WriteAllBytes(path,pixels.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=oldTarget;camera.rect=oldRect;target.Release();Object.Destroy(pixels);Object.Destroy(target);
        }
    }
}
