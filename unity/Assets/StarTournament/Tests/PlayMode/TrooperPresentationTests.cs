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
#if UNITY_EDITOR
        bool previousAsyncCompilation;
#endif
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(b=>b.name==name);
        IEnumerator Load(int count)
        {
#if UNITY_EDITOR
            previousAsyncCompilation=UnityEditor.ShaderUtil.allowAsyncCompilation;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
#endif
            yield return NativeLoadingTestScene.Load(); scene=SceneManager.GetSceneByName("ProvingGround"); yield return null;
            ground=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ProvingGround>()).Single();
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Assert.That(ground.GetComponentsInChildren<LineRenderer>(true)
                .Where(r=>r.transform.parent && r.transform.parent.name=="Cutter beams")
                .All(r=>!r.enabled),Is.True,"Setup must never expose uninitialized SEAM beam segments");
            for(int i=0;i<4;i++)pads[i]=InputSystem.AddDevice<Gamepad>();
            ground.StartCombatReview(pads.Take(count).ToArray(),ensureOpponent:count==1); yield return null;
            Assert.That(ground.Running,Is.True,ground.CombatReviewDiagnostic());
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation=previousAsyncCompilation;
#endif
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
            if(System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_GRIP_FACTORY")=="1")
                foreach(var d in ProvingProfile.CreateTrooperDefault().Descriptors)first.Tuning.Set(d.Path,d.DefaultValue);
            var reviewOverrides=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_GRIP_OVERRIDES");
            if(!string.IsNullOrEmpty(reviewOverrides))
                foreach(var line in System.IO.File.ReadAllLines(reviewOverrides))
                {
                    var parts=line.Split('=');var value=float.Parse(parts[1],System.Globalization.CultureInfo.InvariantCulture);
                    // Explicit offline art studies may preview a new joint range before
                    // committing factory/history metadata. Ordinary fixtures are unchanged.
                    if(System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_GRIP_CANDIDATE_RANGES")=="1" &&
                       parts[0].StartsWith("grip.")&&(parts[0].Contains(".left.")||parts[0].Contains(".right.")))
                    {
                        var descriptor=first.Tuning.Descriptors.Single(d=>d.Path==parts[0]);
                        descriptor.Minimum=Mathf.Min(descriptor.Minimum,value);descriptor.Maximum=Mathf.Max(descriptor.Maximum,value);
                    }
                    first.Tuning.Set(parts[0],value);
                    Assert.That(first.Tuning.Get(parts[0]),Is.EqualTo(value).Within(.000001f),"Capture override must not be silently clamped: "+parts[0]);
                }
            var snapshot=ground.Session.Capture();
            Assert.That(new Vector2(snapshot.Poses[1].Velocity.x,snapshot.Poses[1].Velocity.z).magnitude,Is.LessThan(second.Tuning.Get("animation.walkThreshold")),"Stationary fixture must have settled");
            double start=snapshot.Time;first.ResetPose(start);second.ResetPose(start);
            var rest=first.transform.localPosition;var stationary=second.transform.localPosition;
            string directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WALK_EVIDENCE");
            bool sequence=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WALK_SEQUENCE")=="1";
            var mount=first.GetComponentsInChildren<Transform>().Single(t=>t.name=="trooper:rig:weapon-mount");
            Vector3 previous=Vector3.zero;float maximumStep=0;
            var samples=new System.Collections.Generic.List<string>{"weapon,time,offset_x,offset_y,mount_x,mount_y,mount_z"};
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                // Crimson has an accepted shoulder-specific placement in addition to
                // the common ground-step offset; it is not a locomotion displacement.
                var placement=weapon==WeaponId.RocketLauncher&&first.Tuning.Version>=31&&first.Tuning.Get("grip.crimson.enabled")>0?
                    new Vector3(first.Tuning.Get("grip.crimson.viewX"),first.Tuning.Get("grip.crimson.viewY"),first.Tuning.Get("grip.crimson.viewZ")):Vector3.zero;
                var life=snapshot.Lives[0];life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;
                life.ShotgunOwned=life.RocketOwned=life.CutterOwned=true;
                if(weapon==WeaponId.Plasma){life.PlasmaOwned=true;life.PlasmaAmmo=100;}
                // Derive the selected total after filling the owned slot, as Restore requires.
                life.Ammo=CombatLife.AmmoFor(life,weapon);snapshot.Lives[0]=life;
                snapshot.Time=start;ground.Session.Restore(snapshot);first.ResetPose(start);previous=Vector3.zero;float maximumOffset=0;
                for(int tick=0;tick<=65;tick++)
                {
                    if(tick>0)ground.Session.Tick(new[]{new LocalAction{Move=Vector2.up},new LocalAction()},.02f);
                    ground.SendMessage("LateUpdate");maximumOffset=Mathf.Max(maximumOffset,first.WalkOffset.magnitude);
                    Assert.That(first.State,Is.EqualTo("aim"));Assert.That(second.WalkOffset,Is.EqualTo(Vector3.zero));
                    Assert.That(second.transform.localPosition,Is.EqualTo(stationary));
                    Assert.That(Vector3.Distance(first.transform.localPosition,rest+placement+first.WalkOffset),Is.LessThan(.000001f));
                    var point=camera.transform.InverseTransformPoint(mount.position);
                    samples.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F3},{2:F6},{3:F6},{4:F6},{5:F6},{6:F6}",weapon,ground.Session.Time,first.WalkOffset.x,first.WalkOffset.y,point.x,point.y,point.z));
                    if(tick>0)maximumStep=Mathf.Max(maximumStep,Vector3.Distance(previous,point));previous=point;
                    if(!string.IsNullOrEmpty(directory)&&(sequence||tick==0||tick==16||tick==32||tick==49||tick==65))
                        { // Let the normal rendered-frame skinning consume the current bones before capture.
                          yield return null;
                          CaptureWalkFrame(directory,weapon+"-"+tick.ToString("D2"));
                          if(tick==0)
                          {
                              CaptureGrip(directory,weapon.ToString(),first,camera);
                              if(System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WORLD_MODEL_EVIDENCE")=="1")
                                  CaptureWorldModel(directory,weapon.ToString());
                          }
                          if(tick==32)CaptureSingleView(directory,weapon.ToString(),camera); }
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
        [UnityTest] public IEnumerator CrimsonHeavyRecoilNativeReview()
        {
            yield return Load(1);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            ground.PlaceCombatReviewSeat(0,new Vector3(-10,0,14),0,90);
            for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
            foreach(var v in ground.GetComponentsInChildren<TrooperVisual>(true))foreach(var d in ProvingProfile.CreateTrooperDefault().Descriptors)v.Tuning.Set(d.Path,d.DefaultValue);
            var snapshot=ground.Session.Capture();snapshot.Lives[0].SelectedWeapon=WeaponId.RocketLauncher;snapshot.Lives[0].RocketOwned=true;snapshot.Lives[0].RocketAmmo=8;snapshot.Lives[0].Ammo=8;snapshot.Lives[0].SwitchRemaining=0;
            ground.Session.Restore(snapshot);ground.SendMessage("LateUpdate");yield return null;
            var view=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var body=ground.transform.Find("player-1/trooper-presentation").GetComponent<TrooperVisual>();
            var output=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_RECOIL_EVIDENCE");
            var go=new GameObject("Crimson recoil world camera");var review=go.AddComponent<Camera>();review.enabled=false;review.fieldOfView=42;review.nearClipPlane=.05f;review.farClipPlane=150;review.cullingMask=~((1<<15)|(1<<16)|(1<<17)|(1<<18));review.clearFlags=CameraClearFlags.Skybox;
            var target=body.transform.position+Vector3.up*1.40f;go.transform.position=target+body.transform.right*2.2f+Vector3.up*.15f;go.transform.LookAt(target);
            var visuals=new[]{view,body};
            var mounts=visuals.Select(v=>v.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="trooper:rig:weapon-mount")).ToArray();
            var hands=visuals.Select(v=>new[]{"LeftHand","RightHand"}.Select(n=>v.GetComponentsInChildren<Transform>(true).Single(t=>t.name==n)).ToArray()).ToArray();
            var quiet=ground.Session.Capture();var actions=new LocalAction[ground.Session.ParticipantCount];bool checkedPeak=false,checkedRest=false;
            var lines=new System.Collections.Generic.List<string>{"frame,time,fp_pitch,world_pitch,fp_back,world_back"};
            if(!string.IsNullOrEmpty(output))System.IO.Directory.CreateDirectory(output);
            for(int frame=0;frame<90;frame++)
            {
                actions[0].Fire=frame==15;ground.Session.Tick(actions,1f/60);ground.SendMessage("LateUpdate");
                if(frame==15){Assert.That(ground.Session.Rockets.Count(),Is.EqualTo(1));Assert.That(view.GetComponent<WeaponModelPresentation>().PulseDisplay.DisplayText,Is.EqualTo("07"));}
                if(frame==18||frame==65)
                {
                    var positions=mounts.Select(m=>m.position).ToArray();var rotations=mounts.Select(m=>m.rotation).ToArray();
                    var contacts=hands.Select((pair,index)=>pair.Select(h=>mounts[index].InverseTransformPoint(h.position)).ToArray()).ToArray();
                    string state=JsonUtility.ToJson(ground.Session.Capture());ground.SendMessage("LateUpdate");ground.SendMessage("LateUpdate");
                    Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(state),"Presentation cannot advance paused simulation");
                    for(int v=0;v<2;v++){Assert.That(Vector3.Distance(mounts[v].position,positions[v]),Is.LessThan(.0001f));Assert.That(Quaternion.Angle(mounts[v].rotation,rotations[v]),Is.LessThan(.02f));visuals[v].Tuning.Set("grip.crimson.recoil.enabled",0);}
                    ground.SendMessage("LateUpdate");
                    for(int v=0;v<2;v++)
                    {
                        float angle=Quaternion.Angle(mounts[v].rotation,rotations[v]);float travel=Vector3.Distance(mounts[v].position,positions[v]);
                        if(frame==18){Assert.That(angle,Is.GreaterThan(5),"Peak muzzle rise");Assert.That(travel,Is.GreaterThan(.03f),"Heavy impulse moves actual model");}
                        else {Assert.That(angle,Is.LessThan(.02f));Assert.That(travel,Is.LessThan(.0001f));}
                        for(int h=0;h<2;h++)Assert.That(Vector3.Distance(mounts[v].InverseTransformPoint(hands[v][h].position),contacts[v][h]),Is.LessThan(.025f),"Hands follow the same rigid recoil without losing grip");
                        visuals[v].Tuning.Set("grip.crimson.recoil.enabled",1);
                    }
                    ground.SendMessage("LateUpdate");if(frame==18)checkedPeak=true;else checkedRest=true;
                }
                if(!string.IsNullOrEmpty(output))
                {
                    CaptureSingleView(output,"recoil-fp-"+frame.ToString("D3"),camera,960,540);
                    CaptureSingleView(output,"recoil-world-"+frame.ToString("D3"),review,960,540);
                    lines.Add(frame+","+ground.Session.Time.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+","+mounts[0].localEulerAngles.x+","+mounts[1].localEulerAngles.x+","+mounts[0].localPosition.z+","+mounts[1].localPosition.z);
                }
                yield return null;
            }
            Assert.That(checkedPeak&&checkedRest,Is.True);
            // A restore at the same active recoil time must clear the transient kick.
            foreach(var visual in visuals)visual.Fire(ground.Session.Time,WeaponId.RocketLauncher);
            actions[0].Fire=false;ground.Session.Tick(actions,.05f);ground.SendMessage("LateUpdate");
            var activeSnapshot=ground.Session.Capture();ground.Session.Restore(activeSnapshot);ground.SendMessage("LateUpdate");
            var resetPositions=mounts.Select(m=>m.position).ToArray();var resetRotations=mounts.Select(m=>m.rotation).ToArray();
            foreach(var visual in visuals)visual.Tuning.Set("grip.crimson.recoil.enabled",0);ground.SendMessage("LateUpdate");
            for(int v=0;v<2;v++){Assert.That(Vector3.Distance(mounts[v].position,resetPositions[v]),Is.LessThan(.0001f));Assert.That(Quaternion.Angle(mounts[v].rotation,resetRotations[v]),Is.LessThan(.02f));visuals[v].Tuning.Set("grip.crimson.recoil.enabled",1);}
            ground.Session.Restore(quiet);ground.SendMessage("LateUpdate");
            Assert.That(view.GetComponent<WeaponModelPresentation>().PulseDisplay.DisplayText,Is.EqualTo("08"));
            if(!string.IsNullOrEmpty(output))System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"recoil.csv"),lines);
            Object.Destroy(go);
        }
        [UnityTest] public IEnumerator CrimsonShoulderNativeReview()
        {
            yield return Load(1);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            ground.PlaceCombatReviewSeat(0,new Vector3(-10,0,14),0,90);
            for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
            foreach(var v in ground.GetComponentsInChildren<TrooperVisual>(true))
                foreach(var d in ProvingProfile.CreateTrooperDefault().Descriptors)v.Tuning.Set(d.Path,d.DefaultValue);
            var snapshot=ground.Session.Capture();snapshot.Lives[0].SelectedWeapon=WeaponId.RocketLauncher;snapshot.Lives[0].RocketOwned=true;snapshot.Lives[0].RocketAmmo=8;snapshot.Lives[0].Ammo=8;snapshot.Lives[0].SwitchRemaining=0;
            ground.Session.Restore(snapshot);ground.SendMessage("LateUpdate");yield return null;
            var view=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var body=ground.transform.Find("player-1/trooper-presentation").GetComponent<TrooperVisual>();
            var output=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_CRIMSON_EVIDENCE");
            Assert.That(view.EquippedWeapon,Is.EqualTo(WeaponId.RocketLauncher));
            Assert.That(view.GetComponent<WeaponModelPresentation>().PulseDisplay.DisplayText,Is.EqualTo("08"));
            var bones=body.GetComponentsInChildren<Transform>(true);var shoulder=bones.Single(t=>t.name=="RightUpperArm");var saddle=bones.Single(t=>t.name=="crimson-shoulder");
            Assert.That(Vector3.Distance(saddle.position,shoulder.position),Is.LessThan(.2f),"World saddle is located at shoulder, not chest or outstretched hands");
            var model=bones.Single(t=>t.name=="pulse-launcher");
            var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            Assert.That(bounds.size.magnitude,Is.GreaterThan(.70f),"Launcher has increased world mass silhouette");
            string before=JsonUtility.ToJson(ground.Session.Capture());
            if(!string.IsNullOrEmpty(output))
            {
                System.IO.Directory.CreateDirectory(output);
                for(int n=0;n<4;n++){camera.Render();yield return null;ground.SendMessage("LateUpdate");}
                CaptureSingleView(output,"Crimson-first-person",camera,1600,900);
                var lines=new System.Collections.Generic.List<string>();
                foreach(var t in bones.Where(t=>t.name=="RightUpperArm"||t.name=="RightHand"||t.name=="LeftHand"||t.name=="crimson-shoulder"||t.name=="pulse-muzzle"||t.name=="trooper:rig:weapon-mount"))lines.Add(t.name+":"+body.transform.InverseTransformPoint(t.position).ToString("F4"));
                foreach(var t in view.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="LeftLowerArm"||t.name=="RightLowerArm"||t.name=="LeftHand"||t.name=="RightHand"))lines.Add("FP "+t.name+":"+camera.WorldToViewportPoint(t.position).ToString("F4"));
                System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"pose.txt"),lines);
                var go=new GameObject("Crimson scene review camera");var review=go.AddComponent<Camera>();review.enabled=false;review.fieldOfView=42;review.nearClipPlane=.05f;review.farClipPlane=150;review.cullingMask=~((1<<15)|(1<<16)|(1<<17)|(1<<18));
                review.clearFlags=CameraClearFlags.Skybox;
                var target=body.transform.position+Vector3.up*1.30f;
                foreach(var angle in new[]{45,90,270})
                {
                    go.transform.position=target+Quaternion.AngleAxis(angle,Vector3.up)*body.transform.forward*2.8f+Vector3.up*.15f;go.transform.LookAt(target);
                    for(int n=0;n<3;n++){review.Render();yield return null;}
                    CaptureSingleView(output,"Crimson-world-"+angle,review,1600,1100);
                }
                Object.Destroy(go);
            }
            Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(before));
            var actions=new LocalAction[ground.Session.ParticipantCount];ground.Session.Tick(actions,.02f);actions[0].Fire=true;ground.Session.Tick(actions,.02f);ground.SendMessage("LateUpdate");
            Assert.That(ground.Session.Rockets.Count(),Is.EqualTo(1));
            Assert.That(view.GetComponent<WeaponModelPresentation>().PulseDisplay.DisplayText,Is.EqualTo("07"));
            if(!string.IsNullOrEmpty(output))CaptureSingleView(output,"Crimson-launch",camera,1600,900);
        }
        [UnityTest] public IEnumerator PulseMk2StaticGripReview()
        {
            yield return Load(2);
            // The async Lab validation uses JsonUtility; finish it before this short scene witness can tear down the Editor.
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            Assert.That(ground.CombatReviewDiagnostic(),Does.Contain("phase=Running"),"Native review must enter the match before selecting a weapon");
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            foreach(var descriptor in ProvingProfile.CreateTrooperDefault().Descriptors)visual.Tuning.Set(descriptor.Path,descriptor.DefaultValue);
            ground.PlaceCombatReviewSeat(0,new Vector3(-10,0,14),12,90);
            for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[2],.02f);
            var snapshot=ground.Session.Capture();snapshot.Lives[0].SelectedWeapon=WeaponId.RocketLauncher;snapshot.Lives[0].RocketOwned=true;snapshot.Lives[0].RocketAmmo=20;snapshot.Lives[0].Ammo=20;snapshot.Lives[0].SwitchRemaining=0;
            ground.Session.Restore(snapshot);visual.ResetPose(snapshot.Time);ground.SendMessage("LateUpdate");yield return null;
            Assert.That(visual.EquippedWeapon,Is.EqualTo(WeaponId.RocketLauncher));
            string before=JsonUtility.ToJson(ground.Session.Capture());
            string directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_PULSE_EVIDENCE");
            if(!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);CaptureSingleView(directory,"PulseMk2-initial-upload",camera);CaptureGrip(directory,"RocketLauncher",visual,camera);
                // Allow queued texture uploads and temporary review objects to finish before the final seat frame.
                yield return null;ground.SendMessage("LateUpdate");CaptureSingleView(directory,"PulseMk2",camera);
            }
            Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(before));
            var actions=new LocalAction[2];actions[0].Fire=true;ground.Session.Tick(actions,.02f);ground.Session.ClearInput();ground.SendMessage("LateUpdate");
            Assert.That(ground.Session.Rockets.Count(),Is.EqualTo(1));
            var ignition=ground.GetComponentsInChildren<LineRenderer>().Single(r=>r.name=="Muzzle ignition");
            Assert.That(ignition.gameObject.activeInHierarchy,Is.True);
            if(!string.IsNullOrEmpty(directory))CaptureSingleView(directory,"PulseMk2-launch",camera);
            if(!string.IsNullOrEmpty(directory))
            {
                // Native arena witness: actual session rocket and collision, no replacement backdrop.
                var shot=ground.Session.Rockets.Single();var side=Vector3.Cross(Vector3.up,shot.Direction).normalized;
                var reviewGo=new GameObject("Pulse arena projectile review");reviewGo.transform.SetParent(ground.transform);
                var review=reviewGo.AddComponent<Camera>();review.CopyFrom(camera);review.enabled=false;review.rect=new Rect(0,0,1,1);review.nearClipPlane=.01f;review.fieldOfView=45;
                // Match the world camera while omitting all first-person-only layers.
                review.cullingMask&=~((1<<15)|(1<<16)|(1<<17)|(1<<18));
                Vector3 contact=Vector3.zero;bool contacted=false;
                System.Action<RocketExplosion> onContact=e=>{contact=e.Position;contacted=true;};
                ground.Session.RocketExploded+=onContact;
                try
                {
                    for(int tick=0;tick<8;tick++){ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");}
                    Assert.That(ground.Session.Rockets.Count(),Is.EqualTo(1),"Review needs a real unobstructed flight before contact");
                    shot=ground.Session.Rockets.Single();
                    review.transform.position=shot.Position+side*.70f+Vector3.up*.12f-shot.Direction*.08f;
                    review.transform.LookAt(shot.Position-shot.Direction*.10f);
                    CaptureSingleView(directory,"PulseMk2-projectile-arena",review,1800,1100);
                    review.transform.position=shot.Position+side*3+Vector3.up*1.1f-shot.Direction*1.2f;review.transform.LookAt(shot.Position);
                    CaptureSingleView(directory,"PulseMk2-flight-arena",review,1800,1100);
                    for(int tick=0;tick<250&&!contacted;tick++){ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");}
                    Assert.That(contacted,Is.True,"One fired rocket must hit the authored arena");
                    yield return null; // Destroyed flight renderers must leave the actual impact frame.
                    review.transform.position=contact-shot.Direction*5+side*2.2f+Vector3.up*1.8f;review.transform.LookAt(contact+Vector3.up*.35f);
                    for(int tick=0;tick<6;tick++){ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");}
                    CaptureSingleView(directory,"PulseMk2-impact-arena",review,1800,1100);
                    for(int tick=0;tick<18;tick++){ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");}
                    CaptureSingleView(directory,"PulseMk2-smoke-arena",review,1800,1100);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(directory,"arena-witness.txt"),"Map="+ground.SelectedMapId+". " +"Actual native fired rocket; collision event at "+contact+". No arena renderer hidden. No replacement plane. Session ticks fixed .02 s. Visual witness, not performance/device acceptance.");
                }
                finally{ground.Session.RocketExploded-=onContact;Object.Destroy(reviewGo);}
            }
        }
        [UnityTest] public IEnumerator CobaltRifleRetainsAuthoredSurfacesAndNativeGrip()
        {
            yield return Load(1);
            // Complete the existing JsonUtility background reader before this short
            // visual witness can unload its scene or the batch Editor can exit.
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);
            var actions=new LocalAction[ground.Session.ParticipantCount];
            for(int i=0;i<100;i++)ground.Session.Tick(actions,.02f);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            foreach(var descriptor in ProvingProfile.CreateTrooperDefault().Descriptors.Where(d=>d.Path.StartsWith("grip.Rifle.")))
                Assert.That(visual.Tuning.Get(descriptor.Path),Is.EqualTo(descriptor.DefaultValue),"Ordinary scene startup must adopt COBALT defaults: "+descriptor.Path);
            visual.ResetPose(ground.Session.Time);
            ground.Session.Tick(actions,.02f);
            ground.SendMessage("LateUpdate");yield return null;
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var rifle=visual.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="automatic-rifle");
            var authored=ground.RiflePrefab.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            var display=visual.GetComponent<WeaponModelPresentation>().RifleDisplay;
            Assert.That(display.Screen.transform.IsChildOf(rifle),Is.True);
            Assert.That(display.Screen.sharedMaterial.shader,Is.EqualTo(Resources.Load<Shader>("RifleDisplay")));
            // The later integrated ammo display is a separate runtime attachment.
            // Preserve exact equality for every authored rifle surface.
            var actual=rifle.GetComponentsInChildren<Renderer>(true).Where(r=>r!=display.Screen).SelectMany(r=>r.sharedMaterials).Distinct().ToArray();
            CollectionAssert.AreEquivalent(authored,actual,"Mounted rifle must retain the imported PBR surfaces");
            Assert.That(actual.All(m=>m && m.name.StartsWith("cobalt") && m.mainTexture),Is.True,"All authored finishes include their texture");
            foreach(var material in actual)
            {
                Assert.That(material.GetTexture("normalTexture"),Is.Not.Null,material.name+" tangent relief must reach the runtime shader");
                Assert.That(material.GetTexture("metallicRoughnessTexture"),Is.Not.Null,material.name+" surface response must reach the runtime shader");
            }
            Assert.That(rifle.GetComponentsInChildren<Collider>(true),Is.Empty,"Visual rifle cannot affect physical combat");
            var muzzle=rifle.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="rifle-muzzle");
            Assert.That(camera.transform.InverseTransformPoint(muzzle.position).z,Is.GreaterThan(camera.nearClipPlane));
            var directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WALK_EVIDENCE");
            if(!string.IsNullOrEmpty(directory))CaptureSingleView(directory,"COBALT-before-coating-check",camera,1600,900);
            AssertCobaltRenderedCoating(camera);
            if(!string.IsNullOrEmpty(directory))
            {
                CaptureSingleView(directory,"COBALT-rest",camera,1600,900);
                CaptureGrip(directory,"COBALT",visual,camera);CaptureWorldModel(directory,"COBALT");
            }
            var snapshot=ground.Session.Capture();var life=snapshot.Lives[0];life.ShotgunOwned=true;life.ShotgunAmmo=20;snapshot.Lives[0]=life;ground.Session.Restore(snapshot);
            foreach(var selection in new[]{WeaponSelection.Shotgun,WeaponSelection.Rifle})
            {
                actions[0]=new LocalAction{SelectWeapon=selection};ground.Session.Tick(actions,.02f);actions[0]=default;
                for(int i=0;i<70;i++)
                {
                    ground.Session.Tick(actions,.02f);ground.SendMessage("LateUpdate");
                    if(i==12 || i==25 || i==69)
                    {
                        yield return null;
                        if(!string.IsNullOrEmpty(directory))CaptureSingleView(directory,"switch-"+selection+"-"+i,camera,1600,900);
                    }
                }
            }
            Assert.That(visual.EquippedWeapon,Is.EqualTo(WeaponId.Rifle));
        }
        static void RenderNative(Camera camera,RenderTexture target)
        {
            if(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline)
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.RenderPipeline.StandardRequest{destination=target});
            else camera.Render();
        }
        static void AssertCobaltRenderedCoating(Camera camera)
        {
            var target=new RenderTexture(800,450,24);target.Create();
            var pixels=new Texture2D(800,450,TextureFormat.RGB24,false);
            var active=RenderTexture.active;var oldTarget=camera.targetTexture;var rect=camera.rect;
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;
                // The first manual offscreen submission can show a uniform imported
                // surface. A controlled pair at identical pose/lighting proved the
                // second submission resolves the textures; yielding frames or disabling
                // SRP batching did not. Check rendered pixels after that first submission.
                RenderNative(camera,target);RenderNative(camera,target);
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,800,450),0,0);pixels.Apply();
                var diagnostic=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WALK_EVIDENCE");
                if(!string.IsNullOrEmpty(diagnostic)){System.IO.Directory.CreateDirectory(diagnostic);System.IO.File.WriteAllBytes(System.IO.Path.Combine(diagnostic,"coating-check.png"),pixels.EncodeToPNG());}
                // Blue upper-stock strip in the approved closer/lower weapon pose.
                // Previous x575..615 ROI now samples the dark grip insert.
                // Keep the original blue-fraction threshold; only move the sampled surface.
                int blue=0,total=0;
                for(int y=90;y<105;y++)for(int x=520;x<540;x++)
                {var c=pixels.GetPixel(x,y);total++;if(c.b>c.r*1.3f&&c.b>c.g*1.15f)blue++;}
                Assert.That((float)blue/total,Is.GreaterThan(.35f),"Native camera must render COBALT blue coating, not a transient flat material");
            }
            finally{camera.targetTexture=oldTarget;camera.rect=rect;RenderTexture.active=active;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
        [UnityTest] public IEnumerator RifleGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.Rifle);
        static int AssertCobaltBoreAlignment(ProvingGround ground,System.Collections.Generic.List<string> samples)
        {
            int checkedCount=0;var particle=new ParticleSystem.Particle[1];
            foreach(var flash in ground.GetComponentsInChildren<ParticleSystem>().Where(p=>p.name.StartsWith("COBALT muzzle ")&&!p.name.StartsWith("COBALT muzzle smoke ")&&p.particleCount>0))
            {
                var mount=ground.GetComponentsInChildren<WeaponModelPresentation>().Single(m=>m.gameObject.layer==flash.gameObject.layer);
                var rifle=mount.transform.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="automatic-rifle");
                var bore=rifle.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="rifle-muzzle");
                // Authored bore center/axis from the model, independently of RifleDirection.
                Assert.That(Vector3.Distance(rifle.InverseTransformPoint(bore.position),new Vector3(0,.062f,.54f)),Is.LessThan(.0001f));
                var axis=rifle.TransformVector(Vector3.forward).normalized;flash.GetParticles(particle);
                var offset=particle[0].position-bore.position;float along=Vector3.Dot(offset,axis),side=Vector3.Cross(offset,axis).magnitude;
                Assert.That(side,Is.LessThan(.0001f),"Flash must stay on the bore axis through recoil");
                Assert.That(along,Is.EqualTo(ground.Profile.Get("presentation.cobalt.flashOffset")).Within(.0001f),"Flash must be in front of the bore");
                samples.Add(System.FormattableString.Invariant($"{ground.Session.Time},{flash.gameObject.layer},{along},{side},{Vector3.Angle(bore.forward,axis)}"));checkedCount++;
            }
            return checkedCount;
        }
        static void CaptureCobaltMuzzleClose(string output,Camera playerCamera,WeaponModelPresentation mount)
        {
            if(string.IsNullOrEmpty(output))return;
            var rifle=mount.transform.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="automatic-rifle");
            var go=new GameObject("COBALT independent bore inspection");
            try
            {
                var camera=go.AddComponent<Camera>();camera.CopyFrom(playerCamera);camera.enabled=false;camera.fieldOfView=35;camera.nearClipPlane=.01f;
                camera.transform.position=mount.RifleMuzzle+rifle.forward*.45f+rifle.right*.3f+rifle.up*.15f;
                camera.transform.LookAt(mount.RifleMuzzle,rifle.up);
                CaptureSingleView(output,"COBALT-muzzle-close",camera,1200,900);
            }
            finally{Object.Destroy(go);}
        }
        [UnityTest] public IEnumerator CobaltShotImpulsesFollowRealBulletsAndClearOnRestore()
        {
            yield return Load(1);ground.enabled=false;
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);
            var actions=new LocalAction[ground.Session.ParticipantCount];
            for(int i=0;i<100;i++)ground.Session.Tick(actions,.02f);
            ground.SendMessage("LateUpdate");yield return null;
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var presentation=(CombatPresentation)typeof(ProvingGround).GetField("presentation",flags).GetValue(ground);
            var fx=presentation.CobaltShotForReview;Assert.That(fx.Enabled,Is.True);Assert.That(fx.LiveParticles,Is.Zero);Assert.That(fx.ActiveLights,Is.Zero);
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            var output=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WALK_EVIDENCE");
            void Capture(string name,int width,int height){if(!string.IsNullOrEmpty(output))CaptureSingleView(output,name,camera,width,height);}
            Capture("COBALT-idle",1600,900);
            var alignment=new System.Collections.Generic.List<string>{"sessionTime,layer,alongBarrelMeters,offAxisMeters,legacyMarkerAngleDegrees"};int aligned=0;
            var saved=ground.Session.Capture();bool contact=false;RifleBulletContact hit=default;
            System.Action<RifleBulletContact> onContact=e=>{if(e.Bullet.Owner==0){contact=true;hit=e;}};
            ground.Session.RifleBulletHit+=onContact;
            try
            {
                actions[0].Fire=true;ground.Session.Tick(actions,.02f);actions[0].Fire=false;ground.SendMessage("LateUpdate");yield return null;
                Assert.That(fx.LiveParticles,Is.GreaterThan(0));Assert.That(fx.ActiveLights,Is.GreaterThan(0));
                aligned+=AssertCobaltBoreAlignment(ground,alignment);Assert.That(aligned,Is.EqualTo(2),"Local and world muzzle must both be checked");
                Capture("COBALT-fire",1600,900);
                CaptureCobaltMuzzleClose(output,camera,ground.transform.Find("seat-camera-1/trooper-view").GetComponent<WeaponModelPresentation>());
                var lights=ground.GetComponentsInChildren<Light>().Where(l=>l.name.StartsWith("COBALT muzzle light")&&l.enabled).ToArray();
                foreach(var light in lights)light.enabled=false;Capture("COBALT-fire-without-light-control",1600,900);foreach(var light in lights)light.enabled=true;
                double clock=ground.Session.Time;int particles=fx.LiveParticles;yield return new WaitForSecondsRealtime(.05f);ground.SendMessage("LateUpdate");
                Assert.That(ground.Session.Time,Is.EqualTo(clock));Assert.That(fx.LiveParticles,Is.EqualTo(particles),"Paused session must not advance impulses");
                for(int tick=0;tick<90&&!contact;tick++)
                {
                    ground.Session.Tick(actions,.02f);ground.SendMessage("LateUpdate");
                    foreach(var line in ground.GetComponentsInChildren<LineRenderer>().Where(l=>l.name.StartsWith("Rifle flight")))
                        Assert.That(Vector3.Distance(line.GetPosition(0),line.GetPosition(1)),Is.LessThanOrEqualTo(ground.Profile.Get("presentation.rifleTracerLength")+.05f),"Never a continuous beam to contact");
                    if(tick==1){yield return null;Capture("COBALT-bullet",1600,900);}
                }
                Assert.That(contact,Is.True,"Actual scene geometry must receive the physical bullet");
                Assert.That(hit.Contact.Contact,Is.EqualTo(PelletContact.World));
                ground.Session.Tick(actions,.04f);ground.SendMessage("LateUpdate");yield return null;Capture("COBALT-impact",1600,900);
                Assert.That(fx.ActiveContacts,Is.GreaterThan(0));
                // A real arena close-up of the same authoritative contact, no replacement backdrop.
                var position=camera.transform.position;var rotation=camera.transform.rotation;
                camera.transform.position=hit.Contact.Endpoint+hit.Contact.Normal*1.2f+Vector3.up*.3f;camera.transform.LookAt(hit.Contact.Endpoint);
                int mask=camera.cullingMask;camera.cullingMask&=~(1<<15);Capture("COBALT-impact-close",1200,900);camera.cullingMask=mask;camera.transform.SetPositionAndRotation(position,rotation);
                for(int i=0;i<35;i++)ground.Session.Tick(actions,.02f);ground.SendMessage("LateUpdate");Assert.That(fx.ActiveLights,Is.Zero);Assert.That(fx.LiveParticles,Is.Zero);
                ground.Session.Restore(saved);ground.SendMessage("LateUpdate");Assert.That(fx.ActiveContacts,Is.Zero);Assert.That(fx.ActiveLights,Is.Zero);Assert.That(fx.LiveParticles,Is.Zero);
                var directions=new System.Collections.Generic.Dictionary<uint,Vector3>();
                var samples=new System.Collections.Generic.List<string>{"frame,id,directionX,directionY,directionZ,segmentLength"};
                string sequence=string.IsNullOrEmpty(output)?null:System.IO.Path.Combine(output,"burst");if(sequence!=null)System.IO.Directory.CreateDirectory(sequence);
                for(int tick=0;tick<55;tick++)
                {
                    actions[0].Fire=tick<35;ground.Session.Tick(actions,.02f);ground.SendMessage("LateUpdate");
                    aligned+=AssertCobaltBoreAlignment(ground,alignment);
                    foreach(var b in ground.Session.RifleBullets.Where(b=>b.Owner==0))
                    {
                        directions[b.Id]=b.Direction;
                        var line=ground.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Rifle flight "+b.Id);
                        float length=Vector3.Distance(line.GetPosition(0),line.GetPosition(1));
                        Assert.That(length,Is.LessThanOrEqualTo(ground.Profile.Get("presentation.rifleTracerLength")+.05f));
                        samples.Add(System.FormattableString.Invariant($"{tick},{b.Id},{b.Direction.x},{b.Direction.y},{b.Direction.z},{length}"));
                    }
                    yield return null;if(sequence!=null)CaptureSingleView(sequence,"frame-"+tick.ToString("D3"),camera,960,540);
                }
                Assert.That(aligned,Is.GreaterThanOrEqualTo(16),"Check multiple shots/recoil phases in both representations");
                if(!string.IsNullOrEmpty(output))System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"muzzle-alignment.csv"),alignment);
                Assert.That(directions.Count,Is.GreaterThanOrEqualTo(4));
                Assert.That(directions.Values.Any(d=>Vector3.Angle(d,directions.Values.First())>.1f),Is.True,"Burst must retain physical spread");
                if(!string.IsNullOrEmpty(output))System.IO.File.WriteAllLines(System.IO.Path.Combine(output,"burst-directions.csv"),samples);
                if(!string.IsNullOrEmpty(output))System.IO.File.WriteAllText(System.IO.Path.Combine(output,"cobalt-shot-witness.txt"),"Running="+ground.Running+"; contact="+hit.Contact.Endpoint+"; retainedContacts="+fx.RetainedContacts+"; pause/restore=PASS; finiteTrace=PASS");
            }
            finally{ground.Session.RifleBulletHit-=onContact;}
        }
        [UnityTest] public IEnumerator PulseMk2MuzzleTracksBatchedMotionAndRestore()
        {
            yield return Load(2);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            var camera=ground.transform.Find("seat-camera-1");var view=camera.Find("trooper-view");
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);
            for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[2],.02f);
            var baseline=ground.Session.Capture();baseline.Lives[0].SelectedWeapon=WeaponId.RocketLauncher;baseline.Lives[0].RocketOwned=true;baseline.Lives[0].RocketAmmo=20;baseline.Lives[0].Ammo=20;baseline.Lives[0].SwitchRemaining=0;
            ShotNotice notice=default;ground.Session.ShotResolved+=n=>notice=n;
            foreach(int side in new[]{0,-1,1})
            {
                ground.Session.Restore(baseline);ground.SendMessage("LateUpdate");yield return null;
                var oldEye=camera.position;var actions=new LocalAction[2];actions[0].Move=new Vector2(side,0);
                for(int tick=0;tick<5;tick++)ground.Session.Tick(actions,.02f);
                actions[0].LookDegrees=side==0?Vector2.zero:new Vector2(side*12,4);actions[0].Fire=true;ground.Session.Tick(actions,.02f);ground.Session.ClearInput();
                var eye=ground.Session.Pose(0).Position+Vector3.up*ground.Profile.Get("camera.eyeHeight");
                Assert.That(Vector3.Distance(camera.position,eye),Is.LessThan(.0001f),"Shot must sample this tick's camera");
                if(side!=0)Assert.That(Vector3.Distance(oldEye,eye),Is.GreaterThan(.01f));
                var muzzle=view.GetComponent<WeaponModelPresentation>().PulseMuzzle;
                var rocket=ground.Session.Rockets.Single();var offset=muzzle-notice.Origin;
                string before=JsonUtility.ToJson(ground.Session.Capture());ground.SendMessage("LateUpdate");Assert.That(JsonUtility.ToJson(ground.Session.Capture()),Is.EqualTo(before));
                var visual=ground.GetComponentsInChildren<Transform>().Single(t=>t.name=="Pulse rocket "+rocket.Id);
                var expected=rocket.Position+offset*(1-Mathf.Clamp01(rocket.Distance/ground.Profile.Get("presentation.rocketMuzzleBlendDistance")));
                Assert.That(Vector3.Distance(visual.position,expected),Is.LessThan(.0001f));
                var ignition=ground.GetComponentsInChildren<LineRenderer>().Single(r=>r.name=="Muzzle ignition");
                Assert.That(Vector3.Distance(ignition.GetPosition(0),muzzle),Is.LessThan(.0001f));
                Assert.That(Vector3.Dot((ignition.GetPosition(2)-muzzle).normalized,rocket.Direction),Is.GreaterThan(.999f));
                var frozen=visual.position;ground.SendMessage("LateUpdate");Assert.That(visual.position,Is.EqualTo(frozen));
                for(int tick=0;tick<10;tick++)ground.Session.Tick(new LocalAction[2],.02f);ground.SendMessage("LateUpdate");
                if(ground.Session.Rockets.Length>0)Assert.That(Vector3.Distance(visual.position,ground.Session.Rockets[0].Position),Is.LessThan(.0001f));
                ground.Session.Restore(ground.Session.Capture());yield return null;ground.SendMessage("LateUpdate");
                if(ground.Session.Rockets.Length>0)Assert.That(Vector3.Distance(ground.GetComponentsInChildren<Transform>().Single(t=>t.name=="Pulse rocket "+rocket.Id).position,ground.Session.Rockets[0].Position),Is.LessThan(.0001f));
            }
        }
        [UnityTest] public IEnumerator ShotgunGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.Shotgun);
        [UnityTest] public IEnumerator RocketGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.RocketLauncher);
        [UnityTest] public IEnumerator CutterGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.Cutter);
        [UnityTest] public IEnumerator PlasmaGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.Plasma);
        IEnumerator NativeGripPhases(WeaponId selectedWeapon)
        {
            yield return Load(2);
            yield return NativeLoadingTestScene.WaitLabHistory(ground);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var camera=ground.transform.Find("seat-camera-1").GetComponent<Camera>();
            foreach(var descriptor in ProvingProfile.CreateTrooperDefault().Descriptors)visual.Tuning.Set(descriptor.Path,descriptor.DefaultValue);
            ground.PlaceCombatReviewSeat(1,new Vector3(8,0,30),0,180);
            string directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WALK_EVIDENCE");
            // One weapon per case keeps the original fixture timeout unchanged,
            // including when optional native mesh/image evidence is enabled.
            foreach(WeaponId weapon in new[]{selectedWeapon})
            {
                ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);
                for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[2],.02f);
                var snapshot=ground.Session.Capture();var life=snapshot.Lives[0];
                life.SelectedWeapon=weapon;life.PendingWeapon=default;life.SwitchRemaining=0;
                life.ShotgunOwned=life.RocketOwned=life.CutterOwned=true;
                if(weapon==WeaponId.Plasma){life.PlasmaOwned=true;life.PlasmaAmmo=100;}
                life.ShotgunAmmo=life.RocketAmmo=20;life.CutterEnergy=30;life.Ammo=CombatLife.AmmoFor(life,weapon);
                snapshot.Lives[0]=life;ground.Session.Restore(snapshot);visual.ResetPose(snapshot.Time);
                var captured=new System.Collections.Generic.HashSet<string>();
                var states=new System.Collections.Generic.List<string>{"tick,state,airborne,weapon"};
                for(int tick=0;tick<160;tick++)
                {
                    ground.Session.Tick(new[]{new LocalAction{Fire=tick==5,FireHeld=tick>=5&&tick<10,Jump=tick==25},new LocalAction()},.02f);
                    ground.SendMessage("LateUpdate");
                    string phase=tick>=8&&visual.State=="fire"?"fire":visual.AirborneState;
                    if(phase!="fire"&&phase!="rising"&&phase!="falling"&&phase!="landing")continue;
                    if(!captured.Add(phase))continue;
                    yield return null;
                    if(!string.IsNullOrEmpty(directory))
                    {
                        string output=System.IO.Path.Combine(directory,"poses",weapon+"-"+phase);System.IO.Directory.CreateDirectory(output);
                        CaptureSingleView(output,weapon.ToString(),camera);CaptureGrip(output,weapon.ToString(),visual,camera);
                    }
                    states.Add(tick+","+visual.State+","+visual.AirborneState+","+visual.EquippedWeapon);
                }
                if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,weapon+"-pose-states.csv"),states);
                CollectionAssert.AreEquivalent(new[]{"fire","rising","falling","landing"},captured,"Native grip evidence must reach each requested phase for "+weapon);
            }
        }
        [UnityTest] public IEnumerator NativeShortStepsStrafeAndJumpKeepWorldContact()
        {
            yield return Load(2);
            typeof(ProvingGround).GetField("FullHealReviewManualTick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(ground,true);
            ground.PlaceCombatReviewSeat(0,new Vector3(-8,0,26),0,90);ground.PlaceCombatReviewSeat(1,new Vector3(8,0,30),0,180);
            for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[2],.02f);
            ground.SendMessage("LateUpdate");
            var body=ground.transform.Find("player-1/trooper-presentation").GetComponent<TrooperVisual>();
            body.ResetPose(ground.Session.Time);
            var feet=body.GetComponentsInChildren<Transform>().Where(t=>t.name=="LeftFoot"||t.name=="RightFoot").OrderBy(t=>t.name).ToArray();
            var gait=typeof(TrooperVisual).GetField("gait",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(body);
            var swing=gait.GetType().GetProperty("SwingFoot");var anchor=gait.GetType().GetMethod("Anchor");
            string directory=System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_WORLD_STEPS_EVIDENCE");
            var rows=new System.Collections.Generic.List<string>{"frame,time,stage,root_x,root_y,root_z,speed,grounded,swing,left_x,left_y,left_z,right_x,right_y,right_z,stance_error"};
            // Diagnostic view of the actual native actor. Arena rails occlude its
            // feet here, so show a reference floor at the measured ground height.
            var go=new GameObject("world-step-review-camera");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=39;camera.nearClipPlane=.05f;camera.farClipPlane=100;camera.cullingMask=(1<<11)|(1<<31);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="capture-reference-floor";floor.layer=31;floor.GetComponent<Collider>().enabled=false;floor.transform.position=new Vector3(0,-.001f,0);floor.transform.localScale=Vector3.one*10;
            var grid=new Texture2D(32,32);for(int y=0;y<32;y++)for(int x=0;x<32;x++)grid.SetPixel(x,y,x==0||y==0?new Color(.16f,.19f,.22f):new Color(.3f,.34f,.38f));grid.Apply();grid.wrapMode=TextureWrapMode.Repeat;
            var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));floorMaterial.mainTexture=grid;floorMaterial.mainTextureScale=Vector2.one*100;floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;
            var fill=new GameObject("capture-fill-light");var light=fill.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.8f;light.cullingMask=1<<11;fill.transform.rotation=Quaternion.Euler(35,-30,0);
            bool rose=false,fell=false,landed=false;float maximumSlip=0,shortTravel=0,shortSpeed=0;var start=ground.Session.Pose(0).Position;
            try
            {
                for(int tick=0;tick<240;tick++)
                {
                    string stage=tick<60?"short":tick<110?"walk":tick<145?"strafe":tick<205?"jump":"settle";
                    var action=new LocalAction{Move=tick<60?new Vector2(0,.015f):tick<110?Vector2.up:tick<145?Vector2.right:tick<175?Vector2.up:Vector2.zero,Jump=tick==145};
                    ground.Session.Tick(new[]{action,new LocalAction()},.02f);ground.SendMessage("LateUpdate");
                    var pose=ground.Session.Pose(0);int moving=(int)swing.GetValue(gait);float slip=0;
                    if(pose.Grounded)for(int foot=0;foot<2;foot++)if(foot!=moving)slip=Mathf.Max(slip,Vector3.Distance(feet[foot].position,(Vector3)anchor.Invoke(gait,new object[]{foot})));
                    maximumSlip=Mathf.Max(maximumSlip,slip);
                    rose|=body.AirborneState=="rising";fell|=body.AirborneState=="falling";landed|=rose&&pose.Grounded;
                    if(tick<60)shortSpeed=Mathf.Max(shortSpeed,new Vector2(pose.Velocity.x,pose.Velocity.z).magnitude);
                    if(tick==59)shortTravel=Vector3.ProjectOnPlane(pose.Position-start,Vector3.up).magnitude;
                    var l=feet[0].position;var r=feet[1].position;
                    rows.Add(System.FormattableString.Invariant($"{tick},{ground.Session.Time:R},{stage},{pose.Position.x:R},{pose.Position.y:R},{pose.Position.z:R},{pose.Velocity.magnitude:R},{pose.Grounded},{moving},{l.x:R},{l.y:R},{l.z:R},{r.x:R},{r.y:R},{r.z:R},{slip:R}"));
                    if(!string.IsNullOrEmpty(directory))
                    {
                        var center=body.transform.position+Vector3.up*.85f;
                        go.transform.position=center+body.transform.forward*3.3f+body.transform.right*2.1f+Vector3.up*.6f;go.transform.LookAt(center);
                        yield return null;CaptureSingleView(directory,"world-"+tick.ToString("D3"),camera);
                    }
                }
                if(!string.IsNullOrEmpty(directory))System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,"world-steps.csv"),rows);
                Assert.That(shortTravel,Is.GreaterThan(.005f),"Small analog input produces actual movement");
                Assert.That(shortSpeed,Is.LessThan(body.Tuning.Get("animation.walkThreshold")),"Short-step witness must exercise the old idle dead zone");
                Assert.That(maximumSlip,Is.LessThan(.003f),"Actual native motor sequence retains planted support contact");
                Assert.That(rose&&fell&&landed,Is.True,"Native jump, flight and landing actually occurred");
            }
            finally{Object.Destroy(go);Object.Destroy(floor);Object.Destroy(floorMaterial);Object.Destroy(grid);Object.Destroy(fill);}
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
                    try{camera.targetTexture=target;RenderNative(camera,target);}finally{camera.targetTexture=old;}
                }
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
        static void CaptureSingleView(string directory,string weapon,Camera camera,int width=1200,int height=1350)
        {
            System.IO.Directory.CreateDirectory(directory);
            // Same seat projection, rendered alone at higher resolution for visual review.
            var target=new RenderTexture(width,height,24);target.Create();
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            var old=RenderTexture.active;var oldTarget=camera.targetTexture;var rect=camera.rect;
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;RenderNative(camera,target);
                if(System.Environment.GetEnvironmentVariable("STAR_CAPTURE_FIRST_SUBMISSION")=="1")
                {
                    RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,weapon+"-first-submission.png"),pixels.EncodeToPNG());
                }
                // Match the native coating check: the first offscreen submission
                // can leave imported PBR textures unresolved. Keep pose/light fixed.
                RenderNative(camera,target);
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,weapon+"-fps.png"),pixels.EncodeToPNG());
            }
            finally{camera.targetTexture=oldTarget;camera.rect=rect;RenderTexture.active=old;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
        void CaptureWorldModel(string directory,string weapon)
        {
            var body=ground.transform.Find("player-1/trooper-presentation").GetComponent<TrooperVisual>();
            var go=new GameObject("world-model-review-camera");var camera=go.AddComponent<Camera>();
            camera.enabled=false;camera.fieldOfView=39;camera.nearClipPlane=.05f;camera.farClipPlane=7;
            camera.cullingMask=1<<11;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.15f,.17f);
            var fill=new GameObject("world-model-review-light");var light=fill.AddComponent<Light>();
            light.type=LightType.Directional;light.intensity=.8f;light.cullingMask=1<<11;fill.transform.rotation=Quaternion.Euler(35,-30,0);
            try
            {
                var center=body.transform.position+Vector3.up*.95f;
                foreach(var angle in new[]{0,90,180,270})
                {
                    var direction=Quaternion.AngleAxis(angle,Vector3.up)*body.transform.forward;
                    go.transform.position=center+direction*3.4f+Vector3.up*.15f;go.transform.LookAt(center);
                    CaptureSingleView(System.IO.Path.Combine(directory,"world"),weapon+"-"+angle,camera);
                }
            }
            finally{Object.Destroy(go);Object.Destroy(fill);}
        }
        void CaptureGrip(string directory,string weapon,TrooperVisual visual,Camera source)
        {
            var bones=visual.GetComponentsInChildren<Transform>(true);
            var wrist=bones.Single(t=>t.name=="RightHand");
            var mount=bones.Single(t=>t.name=="trooper:rig:weapon-mount");
            var lines=new System.Collections.Generic.List<string>{"name,x,y,z"};
            foreach(var t in bones.Where(t=>t.name.StartsWith("Right")||t.name.StartsWith("Left")||t.name=="rifle-grip"||t.name=="pistol grip"))
            {var p=mount.InverseTransformPoint(t.position);lines.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1:F6},{2:F6},{3:F6}",t.name,p.x,p.y,p.z));}
            foreach(var mesh in visual.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<Renderer>()?.enabled==true))
            {
                var points=mesh.sharedMesh.vertices.Select(v=>mount.InverseTransformPoint(mesh.transform.TransformPoint(v))).ToArray();
                if(points.Length==0)continue;
                var bounds=new Bounds(points[0],Vector3.zero);foreach(var point in points)bounds.Encapsulate(point);
                lines.Add(mesh.name+" bounds "+bounds.ToString("F5"));
            }
            System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,weapon+"-grip.csv"),lines);
            var grip=typeof(TrooperVisual).GetField("equipmentGrip",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(visual);
            var axes=(System.Collections.IDictionary)grip.GetType().GetField("spreadAxes",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(grip);
            var dump=new System.Collections.Generic.List<string>{"name,px,py,pz,qx,qy,qz,qw,cx,cy,cz,sx,sy,sz"};
            foreach(string field in new[]{"digits","primaryDigits"})
            foreach(var item in (System.Collections.IEnumerable)grip.GetType().GetField(field,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(grip))
            {
                var type=item.GetType();var bone=(Transform)type.GetField("Item1").GetValue(item);var curl=(Vector3)type.GetField("Item2").GetValue(item);var spread=(Vector3)axes[bone];
                var p=mount.InverseTransformPoint(bone.position);var q=Quaternion.Inverse(mount.rotation)*bone.rotation;
                dump.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10},{11},{12},{13}",bone.name,p.x,p.y,p.z,q.x,q.y,q.z,q.w,curl.x,curl.y,curl.z,spread.x,spread.y,spread.z));
            }
            System.IO.File.WriteAllLines(System.IO.Path.Combine(directory,weapon+"-axes.csv"),dump);
            // Actual rendered triangles in weapon coordinates, for contact inspection.
            if(System.Environment.GetEnvironmentVariable("STAR_TOURNAMENT_GRIP_MESH")=="1")
            {
                var obj=new System.Text.StringBuilder();int offset=1;
                void AppendMesh(string name,Mesh mesh,Transform transform)
                {
                    obj.AppendLine("o "+name.Replace(' ','_'));
                    foreach(var v in mesh.vertices)
                    {var q=mount.InverseTransformPoint(transform.TransformPoint(v));obj.AppendLine(System.FormattableString.Invariant($"v {q.x:R} {q.y:R} {q.z:R}"));}
                    var ts=mesh.triangles;for(int i=0;i<ts.Length;i+=3)obj.AppendLine($"f {offset+ts[i]} {offset+ts[i+1]} {offset+ts[i+2]}");
                    offset+=mesh.vertexCount;
                }
                foreach(var renderer in visual.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.enabled))
                {
                    var mesh=new Mesh();renderer.BakeMesh(mesh);AppendMesh(renderer.name,mesh,renderer.transform);
                    if(renderer.sharedMesh.boneWeights.Length==mesh.vertexCount)
                    {
                        var skin=new System.Text.StringBuilder();var verts=mesh.vertices;var ws=renderer.sharedMesh.boneWeights;
                        foreach(var b in renderer.bones)
                        {var p=mount.InverseTransformPoint(b.position);var q=Quaternion.Inverse(mount.rotation)*b.rotation;skin.AppendLine(System.FormattableString.Invariant($"b {b.name} {p.x:R} {p.y:R} {p.z:R} {q.x:R} {q.y:R} {q.z:R} {q.w:R}"));}
                        for(int i=0;i<verts.Length;i++)
                        {
                            var w=ws[i];var ids=new[]{w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};var weights=new[]{w.weight0,w.weight1,w.weight2,w.weight3};
                            if(!ids.Any(id=>new[]{"Left","Right"}.Any(side=>new[]{"Hand","Thumb","Index","Middle","Ring","Little"}.Any(digit=>renderer.bones[id].name.StartsWith(side+digit)))))continue;
                            var p=mount.InverseTransformPoint(renderer.transform.TransformPoint(verts[i]));skin.Append(System.FormattableString.Invariant($"v {p.x:R} {p.y:R} {p.z:R}"));
                            for(int j=0;j<4;j++)if(weights[j]>0)skin.Append(System.FormattableString.Invariant($" {renderer.bones[ids[j]].name}:{weights[j]:R}"));skin.AppendLine();
                        }
                        System.IO.File.WriteAllText(System.IO.Path.Combine(directory,weapon+"-"+renderer.name.Replace(' ','_')+"-skin.txt"),skin.ToString());
                    }
                    Object.Destroy(mesh);
                }
                foreach(var mesh in visual.GetComponentsInChildren<MeshFilter>().Where(m=>m.GetComponent<Renderer>()?.enabled==true))AppendMesh(mesh.name,mesh.sharedMesh,mesh.transform);
                System.IO.File.WriteAllText(System.IO.Path.Combine(directory,weapon+"-actual-mesh.obj"),obj.ToString());
            }
            var go=new GameObject("grip-review-camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(source);
            // Diagnostic fill reveals finger geometry on the dark glove; FPS light is unchanged.
            var reviewLight=go.AddComponent<Light>();reviewLight.type=LightType.Point;reviewLight.range=3;reviewLight.intensity=.15f;reviewLight.cullingMask=source.cullingMask;reviewLight.shadows=LightShadows.None;
            camera.rect=new Rect(0,0,1,1);camera.orthographic=true;camera.orthographicSize=.12f;camera.nearClipPlane=.01f;
            var target=new RenderTexture(1000,1000,24);target.Create();camera.targetTexture=target;
            var pixels=new Texture2D(1000,1000,TextureFormat.RGB24,false);var old=RenderTexture.active;
            try
            {
                foreach(string hand in new[]{"Right","Left"})
                foreach(int sign in new[]{-1,1,2})
                {
                    var center=bones.Single(t=>t.name==hand+"MiddleProximal").position;
                    camera.transform.position=center+mount.TransformDirection(sign==2?new Vector3(.15f,-.35f,.6f):new Vector3(sign*.5f,.05f,-.2f));
                    camera.transform.LookAt(center,mount.up);RenderNative(camera,target);RenderNative(camera,target);RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,1000,1000),0,0);pixels.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,weapon+"-"+hand+"-grip-"+sign+".png"),pixels.EncodeToPNG());
                }
            }
            finally{RenderTexture.active=old;target.Release();Object.Destroy(target);Object.Destroy(pixels);Object.Destroy(go);}
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
        static ProvingProfile BeforeRefinedHandAnatomy(ProvingProfile profile)
        {
            return (ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeRefinedHandAnatomy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(profile,null);
        }
        static ProvingProfile BeforeArticulatedFingerGrips(ProvingProfile profile)
        {
            return (ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeArticulatedFingerGrips",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(profile,null);
        }
        [UnityTest] public IEnumerator RefinedHandGripPreservesArmAndWeaponPlacement()
        {
            yield return Load(2);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var current=BeforeArticulatedFingerGrips(ProvingProfile.CreateTrooperDefault());var previous=BeforeRefinedHandAnatomy(current);
            var fixedBones=visual.GetComponentsInChildren<Transform>().Where(t=>
                t.name=="trooper:rig:weapon-mount"||((t.name.StartsWith("Left")||t.name.StartsWith("Right"))&&!t.name.StartsWith("RightThumb"))).ToArray();
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                visual.EquippedWeapon=weapon;
                foreach(var d in previous.Descriptors)visual.Tuning.Set(d.Path,d.DefaultValue);
                visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
                var positions=fixedBones.Select(t=>t.position).ToArray();var rotations=fixedBones.Select(t=>t.rotation).ToArray();
                foreach(var d in current.Descriptors)visual.Tuning.Set(d.Path,d.DefaultValue);
                visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
                for(int i=0;i<fixedBones.Length;i++)
                {
                    Assert.That(Vector3.Distance(positions[i],fixedBones[i].position),Is.LessThan(.0001f),weapon+" "+fixedBones[i].name);
                    Assert.That(Quaternion.Angle(rotations[i],fixedBones[i].rotation),Is.LessThan(.05f),weapon+" "+fixedBones[i].name);
                }
            }
        }
        [UnityTest] public IEnumerator ArticulatedFingerGripPreservesArmAndWeaponPlacement()
        {
            yield return Load(2);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var current=ProvingProfile.CreateTrooperDefault();var previous=BeforeArticulatedFingerGrips(current);
            var fixedBones=visual.GetComponentsInChildren<Transform>().Where(t=>
                t.name=="trooper:rig:weapon-mount"||new[]{"LeftUpperArm","LeftLowerArm","LeftHand","RightUpperArm","RightLowerArm","RightHand"}.Contains(t.name)).ToArray();
            Assert.That(fixedBones.Length,Is.EqualTo(7),"Both complete arm chains and weapon mount must be checked");
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                visual.EquippedWeapon=weapon;
                foreach(var d in previous.Descriptors)visual.Tuning.Set(d.Path,d.DefaultValue);
                visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
                var positions=fixedBones.Select(t=>t.position).ToArray();var rotations=fixedBones.Select(t=>t.rotation).ToArray();
                foreach(var d in current.Descriptors)visual.Tuning.Set(d.Path,d.DefaultValue);
                visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
                for(int i=0;i<fixedBones.Length;i++)
                {
                    Assert.That(Vector3.Distance(positions[i],fixedBones[i].position),Is.LessThan(.0001f),weapon+" "+fixedBones[i].name);
                    Assert.That(Quaternion.Angle(rotations[i],fixedBones[i].rotation),Is.LessThan(.05f),weapon+" "+fixedBones[i].name);
                }
            }
        }
        static ProvingProfile BeforeStableSupportGrip(ProvingProfile profile)
        {
            return (ProvingProfile)typeof(ProvingProfile).GetMethod("BeforeStableSupportGrip",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(profile,null);
        }
        [UnityTest] public IEnumerator FactorySupportGripsKeepForearmDirectionAndNaturalWrist()
        {
            yield return Load(2);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            var current=BeforeRefinedHandAnatomy(ProvingProfile.CreateTrooperDefault());
            var previous=BeforeStableSupportGrip(current);
            var bones=visual.GetComponentsInChildren<Transform>();
            var upper=bones.Single(t=>t.name=="LeftUpperArm");var lower=bones.Single(t=>t.name=="LeftLowerArm");
            var wrist=bones.Single(t=>t.name=="LeftHand");var palm=bones.Single(t=>t.name=="LeftMiddleProximal");
            var independent=bones.Where(t=>t.name.StartsWith("Right")||t.name=="trooper:rig:weapon-mount").ToArray();
            var directions=new System.Collections.Generic.List<Vector3>();
            foreach(WeaponId weapon in System.Enum.GetValues(typeof(WeaponId)))
            {
                visual.EquippedWeapon=weapon;
                foreach(var d in previous.Descriptors)visual.Tuning.Set(d.Path,d.DefaultValue);
                visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
                var positions=independent.Select(t=>t.position).ToArray();var rotations=independent.Select(t=>t.rotation).ToArray();
                var lengths=new[]{Vector3.Distance(upper.position,lower.position),Vector3.Distance(lower.position,wrist.position)};
                foreach(var d in current.Descriptors)visual.Tuning.Set(d.Path,d.DefaultValue);
                visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
                Assert.That(Vector3.Angle(wrist.position-lower.position,palm.position-wrist.position),Is.LessThan(12),weapon.ToString());
                var direction=(wrist.position-lower.position).normalized;
                foreach(var other in directions)Assert.That(Vector3.Angle(other,direction),Is.LessThan(15),weapon+" support forearm direction");
                directions.Add(direction);
                Assert.That(Vector3.Distance(upper.position,lower.position),Is.EqualTo(lengths[0]).Within(.00001f));
                Assert.That(Vector3.Distance(lower.position,wrist.position),Is.EqualTo(lengths[1]).Within(.00001f));
                for(int i=0;i<independent.Length;i++)
                {
                    Assert.That(Vector3.Distance(positions[i],independent[i].position),Is.LessThan(.0001f),independent[i].name);
                    Assert.That(Quaternion.Angle(rotations[i],independent[i].rotation),Is.LessThan(.05f),independent[i].name);
                }
            }
        }
        [UnityTest] public IEnumerator SupportWristAlignmentPreservesPalmDigitsAndBoneLengths()
        {
            yield return Load(2);
            var visual=ground.transform.Find("seat-camera-1/trooper-view").GetComponent<TrooperVisual>();
            foreach(var d in BeforeStableSupportGrip(ProvingProfile.CreateTrooperDefault()).Descriptors)visual.Tuning.Set(d.Path,d.DefaultValue);
            var bones=visual.GetComponentsInChildren<Transform>();
            var upper=bones.Single(t=>t.name=="LeftUpperArm");var lower=bones.Single(t=>t.name=="LeftLowerArm");
            var wrist=bones.Single(t=>t.name=="LeftHand");var palm=bones.Single(t=>t.name=="LeftMiddleProximal");
            var contacts=bones.Where(t=>t.name.StartsWith("Left")&&(t.name.Contains("Proximal")||t.name.Contains("Intermediate")||t.name.Contains("Distal")||t.name=="LeftHand")||t.name=="trooper:rig:weapon-mount").ToArray();
            foreach(var weapon in new[]{WeaponId.RocketLauncher,WeaponId.Cutter})
            {
                visual.EquippedWeapon=weapon;var path="grip."+weapon+".wristAlignment";
                visual.Tuning.Set(path,0);visual.ResetPose(0);visual.Render(.25,Vector3.zero,100,1);
                var positions=contacts.Select(t=>t.position).ToArray();var rotations=contacts.Select(t=>t.rotation).ToArray();
                var lengths=new[]{Vector3.Distance(upper.position,lower.position),Vector3.Distance(lower.position,wrist.position)};
                float before=Vector3.Angle(wrist.position-lower.position,palm.position-wrist.position);
                visual.Tuning.Set(path,1);visual.Render(.25,Vector3.zero,100,1);
                Assert.That(Vector3.Angle(wrist.position-lower.position,palm.position-wrist.position),Is.LessThan(before-5),weapon.ToString());
                Assert.That(Vector3.Distance(upper.position,lower.position),Is.EqualTo(lengths[0]).Within(.00001f));
                Assert.That(Vector3.Distance(lower.position,wrist.position),Is.EqualTo(lengths[1]).Within(.00001f));
                for(int i=0;i<contacts.Length;i++)
                {
                    Assert.That(Vector3.Distance(positions[i],contacts[i].position),Is.LessThan(.0001f),contacts[i].name);
                    Assert.That(Quaternion.Angle(rotations[i],contacts[i].rotation),Is.LessThan(.05f),contacts[i].name);
                }
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
            foreach(int count in new[]{1,2,3,4})
            {
                ground.SendMessage("Menu");yield return null;
                NativeSetupFixture.UnboundHumans(ground,count);
                ground.StartCombatReview(pads.Take(count).ToArray(),ensureOpponent:count==1);yield return NativeLoadingTestScene.Wait(ground);yield return null;
                Assert.That(ground.GetComponentsInChildren<TrooperVisual>().Count(v=>v.gameObject.activeInHierarchy),Is.EqualTo(ground.Composition.ParticipantCount+count));
                var cams=ground.GetComponentsInChildren<Camera>().Where(c=>c.enabled).ToArray();
                var fills=ground.GetComponentsInChildren<Light>().Where(l=>l.name=="equipment-fill").ToArray();
                Assert.That(fills.Length,Is.EqualTo(count));
                for(int i=0;i<count;i++)
                {
                    Assert.That(fills[i].cullingMask,Is.EqualTo(1<<(15+i)));
                    Assert.That(fills[i].intensity,Is.EqualTo(ProvingProfile.CreateTrooperDefault().Get("view.fillIntensity")));
                    Assert.That(fills[i].shadows,Is.EqualTo(LightShadows.None));
                    Assert.That(fills[i].type,Is.EqualTo(LightType.Point));
                    Assert.That(fills[i].transform.localPosition,Is.EqualTo(Vector3.back*ProvingProfile.CreateTrooperDefault().Get("view.fillBackMeters")));
                }
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
                var original=proxy.transform.parent.GetComponent<SkinnedMeshRenderer>();
                Assert.That(original.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.Off));
                Assert.That(proxy.sharedMesh,Is.SameAs(original.sharedMesh));
                Assert.That(proxy.bones,Is.EqualTo(original.bones));
                // Identity materials are owned per renderer; equivalent shadow inputs
                // need not have the same Unity object identity after palette updates.
                Assert.That(proxy.sharedMaterials.Select(m=>m.shader),Is.EqualTo(original.sharedMaterials.Select(m=>m.shader)));
                Assert.That(proxy.sharedMaterials.Select(m=>m.renderQueue),Is.EqualTo(original.sharedMaterials.Select(m=>m.renderQueue)));
                Assert.That(proxy.gameObject.layer,Is.Zero);
                Assert.That(proxy.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
                Assert.That(proxy.GetComponents<Collider>(),Is.Empty);
                Assert.That(proxy.GetComponents<Animator>(),Is.Empty);
                Assert.That(proxy.bones.Length,Is.GreaterThan(0));
            }
        }
    }
}
