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
        [UnityTest] public IEnumerator RifleGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.Rifle);
        [UnityTest] public IEnumerator ShotgunGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.Shotgun);
        [UnityTest] public IEnumerator RocketGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.RocketLauncher);
        [UnityTest] public IEnumerator CutterGripHasNativeFireAndAirbornePhases()=>NativeGripPhases(WeaponId.Cutter);
        IEnumerator NativeGripPhases(WeaponId selectedWeapon)
        {
            yield return Load(2);
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
                    try{camera.targetTexture=target;camera.Render();}finally{camera.targetTexture=old;}
                }
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1600,900),0,0);pixels.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;target.Release();Object.Destroy(target);Object.Destroy(pixels);}
        }
        static void CaptureSingleView(string directory,string weapon,Camera camera)
        {
            System.IO.Directory.CreateDirectory(directory);
            // Same seat projection, rendered alone at higher resolution for visual review.
            var target=new RenderTexture(1200,1350,24);target.Create();
            var pixels=new Texture2D(1200,1350,TextureFormat.RGB24,false);
            var old=RenderTexture.active;var oldTarget=camera.targetTexture;var rect=camera.rect;
            try
            {
                camera.rect=new Rect(0,0,1,1);camera.targetTexture=target;camera.Render();
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1200,1350),0,0);pixels.Apply();
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
                    camera.transform.LookAt(center,mount.up);camera.Render();RenderTexture.active=target;
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
                Assert.That(proxy.gameObject.layer,Is.Zero);
                Assert.That(proxy.shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
                Assert.That(proxy.GetComponents<Collider>(),Is.Empty);
                Assert.That(proxy.GetComponents<Animator>(),Is.Empty);
                Assert.That(proxy.bones.Length,Is.GreaterThan(0));
            }
        }
    }
}
