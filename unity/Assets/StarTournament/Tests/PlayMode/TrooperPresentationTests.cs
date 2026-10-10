using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace StarTournament.ProvingGround.Tests.PlayMode
{
    public sealed class TrooperPresentationTests
    {
        Scene scene; ProvingGround ground; readonly Gamepad[] pads=new Gamepad[4];
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Load(int count)
        {
            yield return NativeLoadingTestScene.Load(); scene=SceneManager.GetSceneByName("ProvingGround"); yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
            ground.StartCombatReview(pads.Take(count).ToArray()); yield return null;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            foreach(var p in pads)if(p!=null&&p.added)InputSystem.RemoveDevice(p);
            if(scene.IsValid())yield return SceneManager.UnloadSceneAsync(scene);
        }
        [UnityTest] public IEnumerator FirstPersonStepsStaySmoothAndIndependentInTheRenderedScene()
        {
            yield return Load(2);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            var first=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var second=ground.transform.Find("seat-camera-2/trooper-view").GetComponent<TrooperVisual>();
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);ground.PlaceCombatReviewSeat(1,new Vector3(8,0,30),0,180);
            // Let the native capsules settle after placement before testing a stationary seat.
            // Penetration resolution is real movement and must not be mistaken for cross-seat sway.
            for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[2],.02f);
            var snapshot=ground.Session.Capture();
            Assert.That(new Vector2(snapshot.Poses[1].Velocity.x,snapshot.Poses[1].Velocity.z).magnitude,Is.LessThan(second.Tuning.Get("animation.walkThreshold")),"Stationary fixture must have settled");
            double start=snapshot.Time;first.ResetPose(start);second.ResetPose(start);
            var rest=first.transform.localPosition;var stationary=second.transform.localPosition;
            string directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WALK_EVIDENCE");
            var mount=first.GetComponentsInChildren<Transform>().Single(t=>t.name=="trooper:rig:weapon-mount");
            Vector3 previous=Vector3.zero;float maximumStep=0;
            var samples=new System.Collections.Generic.List<string>{"weapon,time,offset_x,offset_y,mount_x,mount_y,mount_z"};
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                var life=snapshot.Lives[0];life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;life.Ammo=CombatLife.AmmoFor(life,weapon);
                life.ShotgunOwned=life.RocketOwned=life.CutterOwned=true;snapshot.Lives[0]=life;
                snapshot.Time=start;ground.Session.Restore(snapshot);first.ResetPose(start);previous=Vector3.zero;float maximumOffset=0;
                for(int tick=0;tick<=65;tick++)
                {
                    if(tick>0)ground.Session.Tick(new[]{new LocalAction{Move=Vector2.up},new LocalAction()},.02f);
                    ground.SendMessage("LateUpdate");maximumOffset=Mathf.Max(maximumOffset,first.WalkOffset.magnitude);
                    Assert.That(first.State,Is.EqualTo("aim"));Assert.That(second.WalkOffset,Is.EqualTo(Vector3.zero));
                    Assert.That(second.transform.localPosition,Is.EqualTo(stationary));
                    Assert.That(Vector3.Distance(first.transform.localPosition,rest+first.WalkOffset),Is.LessThan(.000001f));
                    var point=camera.transform.InverseTransformPoint(mount.position);
                    samples.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F3},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6}",weapon,ground.Session.Time,first.WalkOffset.x,first.WalkOffset.y,point.x,point.y,point.z));
                    if(tick>0)maximumStep=Mathf.Max(maximumStep,Vector3.Distance(previous,point));previous=point;
                    if(!string.IsNullOrEmpty(directory)&&(tick==0||tick==16||tick==32||tick==49||tick==65))
                        CaptureWalkFrame(directory,weapon+"-"+tick.ToString("D2"));
                }
                if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,"motion.csv"),samples);
                Assert.That(maximumOffset,Is.GreaterThan(.003f),"Ground movement must produce visible small steps");
                start=ground.Session.Time;
            }
            if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,"motion.csv"),samples);
            Assert.That(maximumStep,Is.LessThan(.006f),"Actual weapon mount has no rapid walk/run jolts");
            var frozen=first.transform.localPosition;ground.SendMessage("LateUpdate");Assert.That(first.transform.localPosition,Is.EqualTo(frozen));
            for(int tick=0;tick<100;tick++){ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");}
            Assert.That(Vector3.Distance(first.transform.localPosition,rest),Is.LessThan(.00001f));
        }
        void CaptureWalkFrame(string directory,string name)
        {
            System.IO.Directory.CreateDirectory(directory);
            var target=new RenderTexture(1600,900,24);var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;target.Create();
            try
            {
                foreach(var camera in ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled))
                {
                    var old=camera.targetTexture;
                    try{camera.targetTexture=target;camera.Render();}finally{camera.targetTexture=old;}
                }
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
        [UnityTest] public IEnumerator VectorMuzzlesAndParticlesRespectSeatClockAndLifetime()
        {
            yield return Load(2);
            var view=ground.transform.Find("seat-camera-1/trooper-view");
            var effect=view.GetComponent<VectorShotPresentation>();
            Assert.That(effect.MuzzleCount,Is.EqualTo(2));
            Assert.That(Vector3.Distance(effect.Muzzle(0),effect.Muzzle(1)),Is.GreaterThan(.01f));
            Assert.That(view.GetComponentsInChildren<Transform>().Count(t=>t.name=="vector-armored-hands"),Is.EqualTo(1));
            effect.Render(0);effect.Render(1);Assert.That(effect.LiveParticles,Is.Zero,"No accepted fire means no burst");
            var before=ground.Session.Pose(0);
            effect.Fire(2);effect.Render(2.02);Assert.That(effect.LiveParticles,Is.GreaterThan(0));
            int frozen=effect.LiveParticles;effect.Render(2.02);Assert.That(effect.LiveParticles,Is.EqualTo(frozen));
            Assert.That(view.GetComponentsInChildren<ParticleSystem>().All(p=>p.gameObject.layer==15),Is.True);
            effect.Render(4);Assert.That(effect.LiveParticles,Is.Zero);
            Assert.That(ground.Session.Pose(0).Position,Is.EqualTo(before.Position));
        }
        [UnityTest] public IEnumerator ActualBonesMoveWithoutMovingGameplayAndFreezeAtSameClock()
        {
            yield return Load(2);
            var visual=ground.transform.Find("player-1/trooper-presentation").GetComponent<TrooperVisual>();
            var bone=visual.GetComponentsInChildren<Transform>().Single(t=>t.name=="LeftLowerLeg");
            var pose=ground.Session.Pose(0); var before=bone.localRotation;
            visual.ResetPose(0);visual.Render(.2,new Vector3(0,0,8),100,1);visual.Render(.4,new Vector3(0,0,8),100,1);
            Assert.That(Quaternion.Angle(before,bone.localRotation),Is.GreaterThan(.1f));
            Assert.That(ground.Session.Pose(0).Position,Is.EqualTo(pose.Position));
            var frozen=bone.localRotation;double time=visual.SampleTime;
            visual.Render(.4,new Vector3(0,0,8),100,1);
            Assert.That(bone.localRotation,Is.EqualTo(frozen));Assert.That(visual.SampleTime,Is.EqualTo(time));
            visual.Fire(.4);visual.Render(.5,Vector3.zero,100,1);Assert.That(visual.State,Is.EqualTo("fire"));
            visual.Fire(.52);visual.Render(.54,Vector3.zero,100,1);Assert.That(visual.SampleTime,Is.EqualTo(.02).Within(.0001));
            visual.Hit(new DamageNotice(0,1,10,0,.6,new FatalImpact(WeaponId.Rifle,1,Vector3.forward,visual.transform.position)));
            visual.Render(.63,Vector3.zero,90,1);Assert.That(visual.HitWeight,Is.GreaterThan(.9f));
            visual.Render(.7,Vector3.zero,80,1);Assert.That(visual.HitWeight,Is.GreaterThan(0));
            visual.BeginDeath(.7);visual.Render(2.4,Vector3.zero,0,1);Assert.That(visual.State,Is.EqualTo("death"));
            Assert.That(ground.Session.Pose(0).Position,Is.EqualTo(pose.Position));
        }
        [UnityTest] public IEnumerator ImmediateResetAndDeathKeepActualBonesAtTheSameClock()
        {
            yield return Load(2);
            foreach(var visual in ground.GetComponentsInChildren<TrooperVisual>())
            {
                var bones=visual.GetComponentsInChildren<Transform>().Where(t=>t.name=="RightHand" || t.name=="LeftUpperArm" || t.name=="Chest").ToArray();
                visual.ResetPose(4);
                var reset=bones.Select(t=>t.localRotation).ToArray();
                visual.Render(4,Vector3.zero,100,1);
                for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(reset[i],bones[i].localRotation),Is.LessThan(.01f));
                visual.BeginDeath(5);
                var death=bones.Select(t=>t.localRotation).ToArray();
                visual.Render(5,Vector3.zero,0,1);
                for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(death[i],bones[i].localRotation),Is.LessThan(.01f));
            }
        }
        [UnityTest] public IEnumerator OrbitalGripDoesNotAccumulateAcrossFrozenSamples()
        {
            yield return Load(2);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var bones=visual.GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftHand"||t.name=="LeftIndexProximal"||t.name=="RightHand"||t.name=="RightIndexProximal"||t.name=="trooper:rig:weapon-mount").ToArray();
            visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
            var rotations=bones.Select(t=>t.localRotation).ToArray();var positions=bones.Select(t=>t.localPosition).ToArray();
            for(int sample=0;sample<5;sample++)visual.Render(.25,Vector3.zero,100,1);
            for(int i=0;i<bones.Length;i++)
            {
                Assert.That(Quaternion.Angle(rotations[i],bones[i].localRotation),Is.LessThan(.01f),bones[i].name);
                Assert.That(Vector3.Distance(positions[i],bones[i].localPosition),Is.LessThan(.0001f),bones[i].name);
            }
        }
        [UnityTest] public IEnumerator DeathRespawnAndRepeatClearViewAndCorpseState()
        {
            yield return Load(3);
            var view=ground.transform.Find("seat-camera-1/trooper-view");
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,100);
            yield return null;
            Assert.That(view.gameObject.activeSelf,Is.False);
            var corpse=ground.GetComponentsInChildren<TrooperVisual>().Single(v=>v.name.StartsWith("corpse-"));
            Assert.That(corpse.State,Is.EqualTo("ragdoll"));Assert.That(corpse.Ragdoll,Is.Not.Null);Assert.That(corpse.GetComponentsInChildren<Collider>(),Is.Empty,"Physics proxies must stay outside the gameplay scene");
            const float dt=.02f;
            // Follow the frozen life timer instead of assuming a particular release's delay.
            int beforeRespawnTicks=Mathf.Max(0,Mathf.CeilToInt((float)(ground.Session.Life(0).RespawnRemaining/dt))-2);
            for(int i=0;i<beforeRespawnTicks;i++)ground.Session.Tick(new LocalAction[3],dt);
            Assert.That(ground.Session.Life(0).Dead,Is.True,"No respawn before the configured delay");
            Assert.That(ground.Session.Life(0).RespawnRemaining,Is.GreaterThan(0));
            for(int i=0;i<3;i++)ground.Session.Tick(new LocalAction[3],dt);
            yield return null;
            Assert.That(view.gameObject.activeSelf,Is.True);Assert.That(view.GetComponent<TrooperVisual>().State,Is.EqualTo("aim"));
            ground.SendMessage("Pause","test");double sessionTime=ground.Session.Time;
            // Allow LateUpdate to present the final already-completed simulation tick before sampling.
            // Capturing before that boundary compared a stale rendered pose with the correct frozen pose.
            yield return null;double time=view.GetComponent<TrooperVisual>().SampleTime;
            yield return new WaitForSecondsRealtime(.1f);Assert.That(view.GetComponent<TrooperVisual>().SampleTime,Is.EqualTo(time));
            Assert.That(ground.Session.Time,Is.EqualTo(sessionTime));
            Button("Повторить матч").onClick.Invoke();yield return null;yield return NativeLoadingTestScene.Wait(ground);
            Assert.That(ground.GetComponentsInChildren<TrooperVisual>().Count(v=>v.name.StartsWith("corpse-")),Is.Zero);
            Assert.That(ground.Session.Life(0).Life,Is.EqualTo(1));
        }
        [UnityTest] public IEnumerator SeatsShareAssetsButNeverRenderAnotherFirstPersonView()
        {
            yield return Load(4);
            foreach(int count in new[]{2,3,4})
            {
                ground.StartCombatReview(pads.Take(count).ToArray());yield return null;
                Assert.That(ground.GetComponentsInChildren<TrooperVisual>().Count(v=>v.gameObject.activeInHierarchy),Is.EqualTo(count*2));
                var cams=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).ToArray();
                var fills=ground.GetComponentsInChildren<Light>().Where(l=>l.name=="equipment-fill").ToArray();
                Assert.That(fills.Length,Is.EqualTo(count));
                for(int i=0;i<count;i++)Assert.That(fills[i].cullingMask,Is.EqualTo(1<<(15+i)));
                for(int i=0;i<count;i++)for(int j=0;j<count;j++)
                {
                    Assert.That((cams[i].cullingMask&(1<<(15+j)))!=0,Is.EqualTo(i==j));
                    Assert.That((cams[i].cullingMask&(1<<(11+j)))!=0,Is.EqualTo(i!=j));
                }
                var skins=ground.GetComponentsInChildren<SkinnedMeshRenderer>();
                Assert.That(skins.GroupBy(s=>s.sharedMesh.name).All(g=>g.Select(s=>s.sharedMesh).Distinct().Count()==1),Is.True);
                Assert.That(ground.GetComponentsInChildren<Animator>().All(a=>!a.applyRootMotion),Is.True);
            }
        }
        [UnityTest] public IEnumerator LocalBodyCastsAnAnimatedShadowWithoutRenderingInItsOwnCamera()
        {
            yield return Load(2);
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var body=ground.transform.Find("player-1/trooper-presentation");
            var proxies=body.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r=>r.name=="own-shadow-proxy").ToArray();
            var source=body.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r=>r.name!="own-shadow-proxy").ToArray();
            Assert.That(proxies.Length,Is.EqualTo(source.Length));
            Assert.That(proxies.Length,Is.GreaterThan(0));
            Assert.That((camera.cullingMask&(1<<11)),Is.Zero);
            Assert.That((camera.cullingMask&1),Is.Not.Zero);
            foreach(var proxy in proxies)
            {
                Assert.That(proxy.gameObject.layer,Is.Zero);
                Assert.That(proxy.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
                Assert.That(proxy.GetComponents<Collider>(),Is.Empty);
                Assert.That(proxy.GetComponents<Animator>(),Is.Empty);
                Assert.That(proxy.bones.Length,Is.GreaterThan(0));
            }
        }
    }
}
