#if UNITY_EDITOR || DEVELOPMENT_BUILD || STAR_TOURNAMENT_DEVELOPMENT_QA
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StarTournament.ProvingGround
{
    /// <summary>Bounded regression fixture shared by PlayMode and muted native Player QA.</summary>
    public sealed class NativeShotOriginReview:MonoBehaviour
    {
        Gamepad[] pads;readonly List<string> checks=new List<string>();readonly List<float> riflePixelErrors=new List<float>();
        ProvingGround ground;string directory;int capture;
        Transform View(int seat)=>ground.transform.Find("seat-camera-"+(seat+1)+"/trooper-view");
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        Vector3 Muzzle(int seat,WeaponId weapon,int index=0)=>weapon==WeaponId.Shotgun?View(seat).GetComponent<VectorShotPresentation>().Muzzle(index):
            weapon==WeaponId.Rifle?View(seat).GetComponent<WeaponModelPresentation>().RifleMuzzle:View(seat).GetComponent<WeaponModelPresentation>().PulseMuzzle;
        void RequireRifleFlight(int seat,uint sequence)
        {
            var bullet=ground.Session.RifleBullets.Single(b=>b.Id==sequence);
            var line=ground.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Rifle flight "+sequence);
            Require(line.GetComponents<Collider>().All(c=>!c.enabled),"Rifle presentation has a physics collider");
            if(bullet.Distance==0)
            {
                var bore=View(seat).GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="dark muzzle bore");
                var muzzle=bore.transform.TransformPoint(bore.sharedMesh.bounds.center);
                float error=Vector3.Distance(line.GetPosition(0),muzzle);riflePixelErrors.Add(error);
                Require(error<.0001f,"New rifle flight did not originate at the final rendered muzzle");
            }
            else if(bullet.Distance>=ground.Profile.Get("presentation.rifleTracerLength"))
                Require(Vector3.Distance(line.GetPosition(0),bullet.Position)<.0001f,"Rifle visual head disagrees with authoritative bullet");
        }
        void Awake()
        {
            // Direct autonomous Player launches must progress before their first focused frame.
            if(Environment.GetCommandLineArgs().Contains("-shotOriginReview"))Application.runInBackground=true;
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();int flag=Array.IndexOf(args,"-shotOriginReview");if(flag<0)yield break;
            yield return new WaitForSecondsRealtime(1);
            yield return Run(GetComponent<ProvingGround>(),args[flag+1]);
            Debug.Log("SHOT_ORIGIN_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        public IEnumerator Run(ProvingGround target,string output=null)
        {
            ground=target;directory=output;if(directory!=null)Directory.CreateDirectory(directory);
            AudioListener.volume=0;Application.runInBackground=true;ground.enabled=false;
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            foreach(var weapon in new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher})
                foreach(int side in new[]{0,-1,1})
                {
                    ground.StartCombatReview(new[]{pads[0],pads[1]},backgroundDiagnostic:true);
                    ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);
                    ground.PlaceCombatReviewSeat(1,new Vector3(5,-1.2f,5),0,180);
                    var equipped=ground.Session.Capture();
                    for(int i=0;i<equipped.Lives.Length;i++){equipped.Lives[i].ShotgunOwned=equipped.Lives[i].RocketOwned=true;equipped.Lives[i].ShotgunAmmo=equipped.Lives[i].RocketAmmo=20;}
                    ground.Session.Restore(equipped);
                    var select=new LocalAction[2];select[0].SelectWeapon=weapon==WeaponId.Shotgun?WeaponSelection.Shotgun:weapon==WeaponId.RocketLauncher?WeaponSelection.RocketLauncher:WeaponSelection.Rifle;
                    ground.Session.Tick(select,.02f);for(int tick=0;tick<60;tick++)ground.Session.Tick(new LocalAction[2],.02f);Render();
                    yield return null; // Flush disposed effects from the preceding fixture.
                    var oldEye=View(0).parent.position;
                    var a=new LocalAction[2];a[0].Move=new Vector2(side,0);
                    // Deliberately withhold rendering for multiple fixed ticks and add yaw/pitch on the fire tick.
                    for(int tick=0;tick<5;tick++)ground.Session.Tick(a,.02f);
                    a[0].LookDegrees=side==0?Vector2.zero:new Vector2(side*12,4);a[0].Fire=true;
                    ShotNotice notice=default;ground.Session.ShotResolved+=n=>notice=n;
                    ground.Session.Tick(a,.02f);
                    var eye=ground.Session.Pose(0).Position+Vector3.up*ground.Profile.Get("camera.eyeHeight");
                    Require(Vector3.Distance(View(0).parent.position,eye)<.0001f,"Shot sampled stale camera: "+weapon+" "+side);
                    if(side!=0)Require(Vector3.Distance(oldEye,eye)>.01f,"Strafe fixture did not move");
                    if(weapon==WeaponId.Shotgun)
                    {
                        Require(notice.Pellets!=null&&notice.Pellets.Length>0,"No authoritative pellets");
                        var traces=ground.GetComponentsInChildren<Transform>().Where(t=>t.name=="pellet-streak").OrderBy(t=>t.parent.GetSiblingIndex()).ToArray();
                        Require(traces.Length==notice.Pellets.Length,"Missing traces");
                        for(int i=0;i<traces.Length;i++)
                        {
                            Require(traces[i].GetComponents<Collider>().All(c=>!c.enabled),"Presentation tracer has an active physics collider before frame end");
                            var muzzle=Muzzle(0,weapon,i);var endpoint=notice.Pellets[i].Endpoint;
                            var expected=muzzle+(endpoint-muzzle).normalized*Mathf.Min(ground.Profile.Get("presentation.shotMuzzleClearance"),Vector3.Distance(muzzle,endpoint)*.5f);
                            Require(Vector3.Distance(traces[i].position,expected)<.0001f,"Wrong muzzle/clearance: "+weapon+" "+side+" pellet "+i);
                        }
                        // Another tick before rendering must not drag earlier world-space effects with the shooter.
                        var frozen=traces[0].position;var move=new LocalAction[2];move[0].Move=new Vector2(-side,0);ground.Session.Tick(move,.02f);
                        Require(traces[0].position==frozen,"Emitted trace dragged with shooter");
                    }
                    var stateBeforeRender=JsonUtility.ToJson(ground.Session.Capture());
                    Render();
                    Require(JsonUtility.ToJson(ground.Session.Capture())==stateBeforeRender,"Render changed combat snapshot");
                    if(weapon==WeaponId.Rifle)
                    {
                        RequireRifleFlight(0,notice.Sequence);
                        // The zero-length birth frame is invisible. Strafe again before the
                        // first visible segment and check its extrapolated origin independently.
                        var firstFlight=new LocalAction[2];firstFlight[0].Move=new Vector2(side,0);
                        ground.Session.Tick(firstFlight,.02f);Render();
                        var bullet=ground.Session.RifleBullets.Single(b=>b.Id==notice.Sequence);
                        var flight=ground.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Rifle flight "+notice.Sequence);
                        float tail=Mathf.Min(bullet.Distance,Mathf.Max(ground.Profile.Get("presentation.shotMuzzleClearance"),bullet.Distance-ground.Profile.Get("presentation.rifleTracerLength")));
                        var launch=flight.GetPosition(1)-(flight.GetPosition(0)-flight.GetPosition(1))*tail/(bullet.Distance-tail);
                        var bore=View(0).GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="dark muzzle bore");
                        Require(Vector3.Distance(launch,bore.transform.TransformPoint(bore.sharedMesh.bounds.center))<.0001f,
                            "First visible rifle segment missed the current muzzle during strafe "+side);
                    }
                    if(weapon==WeaponId.RocketLauncher)
                    {
                        var rocket=ground.Session.Rockets.Single();var visual=ground.GetComponentsInChildren<Transform>().Single(t=>t.name=="Pulse rocket "+rocket.Id);
                        var offset=Muzzle(0,weapon)-notice.Origin;
                        var expected=rocket.Position+offset*(1-Mathf.Clamp01(rocket.Distance/ground.Profile.Get("presentation.rocketMuzzleBlendDistance")));
                        Require(Vector3.Distance(visual.position,expected)<.0001f,"Pulse did not leave its muzzle");
                        Require(ground.Session.Rockets.Single().Position==rocket.Position,"Presentation moved authoritative rocket");
                        if(directory!=null)
                        {
                            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,(++capture).ToString("D2")+"-Pulse-"+side+".png"));
                            yield return new WaitForSecondsRealtime(.1f);
                        }
                        for(int i=0;i<10;i++)ground.Session.Tick(new LocalAction[2],.02f);Render();
                        if(ground.Session.Rockets.Length>0)Require(Vector3.Distance(visual.position,ground.Session.Rockets[0].Position)<.0001f,"Pulse offset never converged");
                        ground.Session.Restore(ground.Session.Capture());yield return null;Render();
                        if(ground.Session.Rockets.Length>0)Require(Vector3.Distance(ground.GetComponentsInChildren<Transform>().Single(t=>t.name=="Pulse rocket "+rocket.Id).position,ground.Session.Rockets[0].Position)<.0001f,"Restore retained a transient launch offset");
                    }
                    // Session pause means identical clock: neither traces nor rockets drift on repeated renders.
                    var effectPositions=ground.GetComponentsInChildren<Transform>().Where(t=>t.name=="pellet-streak"||t.name.StartsWith("Pulse rocket ")).ToArray();
                    var frozenPositions=effectPositions.Select(t=>t.position).ToArray();Render();
                    for(int i=0;i<effectPositions.Length;i++)Require(Vector3.Distance(effectPositions[i].position,frozenPositions[i])<.0001f,"Paused effect drifted");
                    checks.Add(weapon+" "+(side==0?"stationary":side<0?"left + yaw/pitch":"right + yaw/pitch")+": current muzzle, authoritative flight unchanged");
                    if(directory!=null&&weapon!=WeaponId.RocketLauncher)
                    {
                        if(weapon==WeaponId.Rifle)
                        {
                            var flight=ground.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Rifle flight "+notice.Sequence);
                            Require(Vector3.Distance(flight.GetPosition(0),flight.GetPosition(1))>.01f,"Rifle screenshot has no visible flight segment");
                        }
                        yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,(++capture).ToString("D2")+"-"+weapon+"-"+side+".png"));
                        yield return new WaitForSecondsRealtime(.1f);
                    }
                    if(weapon==WeaponId.Rifle)
                    {
                        var previousBullet=ground.Session.RifleBullets.Single(b=>b.Id==notice.Sequence);
                        var previousHead=ground.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Rifle flight "+notice.Sequence).GetPosition(0);
                        var blend=1-Mathf.Clamp01(previousBullet.Distance/ground.Profile.Get("presentation.rifleTracerLength"));
                        var offset=blend>0?(previousHead-previousBullet.Position)/blend:Vector3.zero;
                        var later=new LocalAction[2];later[0].Move=Vector2.right;later[0].LookDegrees=new Vector2(10,0);
                        ground.Session.Tick(later,.02f);Render();
                        var bullet=ground.Session.RifleBullets.Single(b=>b.Id==notice.Sequence);
                        var line=ground.GetComponentsInChildren<LineRenderer>().Single(l=>l.name=="Rifle flight "+notice.Sequence);
                        var expected=bullet.Position+offset*(1-Mathf.Clamp01(bullet.Distance/ground.Profile.Get("presentation.rifleTracerLength")));
                        Require(Vector3.Distance(line.GetPosition(0),expected)<.0001f,"Visible rifle flight dragged with later camera pose");
                    }
                }
            ground.StartCombatReview(pads,backgroundDiagnostic:true);
            for(int seat=0;seat<4;seat++)ground.PlaceCombatReviewSeat(seat,new Vector3(-6+seat*3,-1.2f,-5),0);
            ground.Session.Tick(new LocalAction[4],.02f); // Release the new session's focus/input latch.
            yield return null; // Unity destroys the preceding fixture's same-named cameras at frame end.
            Render();var previous=Enumerable.Range(0,4).Select(i=>View(i).parent.position).ToArray();
            var all=new LocalAction[4];all[0]=new LocalAction{Move=Vector2.right,LookDegrees=new Vector2(15,5),Fire=true};all[2]=new LocalAction{Move=Vector2.left,LookDegrees=new Vector2(-15,-5),Fire=true};
            ground.Session.Tick(all,.02f);
            Require(ground.Session.ShotCount==2,"Four-seat fixture failed to create two confirmed shots");
            foreach(int i in new[]{0,2})Require(Vector3.Distance(View(i).parent.position,ground.Session.Pose(i).Position+Vector3.up*ground.Profile.Get("camera.eyeHeight"))<.0001f,"Wrong local seat muzzle");
            foreach(int i in new[]{1,3})Require(View(i).parent.position==previous[i],"Shot leaked into another camera");
            checks.Add("four seats: simultaneous independent muzzle sampling");Render();
            ShotNotice automatic=default;ground.Session.ShotResolved+=n=>automatic=n;
            int followUps=0;var held=new LocalAction[4];held[0]=new LocalAction{Move=Vector2.right,FireHeld=true,LookDegrees=new Vector2(1,0)};
            for(int tick=0;tick<24;tick++)
            {
                int previousShots=ground.Session.ShotCount;ground.Session.Tick(held,.02f);Render();
                if(ground.Session.ShotCount==previousShots)continue;
                RequireRifleFlight(0,automatic.Sequence);followUps++;
            }
            Require(followUps>=2,"Automatic fixture did not produce consecutive rifle shots");
            checks.Add("automatic rifle: consecutive shots agree with the rendered recoil pose");
            if(directory!=null)
            {
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,"10-four-seats.png"));yield return new WaitForSecondsRealtime(.1f);
                File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{checks=checks.ToArray(),arena=ground.Session.ArenaIdentity,profile=ground.Session.DesignProfile.Hash,muted=AudioListener.volume==0,width=Screen.width,height=Screen.height,rifleLaunchErrorsMeters=riflePixelErrors.ToArray()},true));
            }
        }
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
        [Serializable]sealed class Result{public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",arena,profile;public bool muted;public int width,height;public string[] checks;public float[] rifleLaunchErrorsMeters;}
    }
}
#endif

#endif
