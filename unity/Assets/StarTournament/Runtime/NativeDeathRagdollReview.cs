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
    public sealed class NativeDeathRagdollReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;Camera reviewCamera;
        readonly List<double> tickMs=new List<double>();
        bool measureTicks;
        readonly List<Measurement> measurements=new List<Measurement>();
        void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        TrooperVisual Body()=>ground.GetComponentsInChildren<TrooperVisual>().First(v=>v.name.StartsWith("corpse-"));
        void Step(int n){for(int i=0;i<n;i++){var timer=System.Diagnostics.Stopwatch.StartNew();ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);Render();if(measureTicks)tickMs.Add(timer.Elapsed.TotalMilliseconds);}}
        void Equip(WeaponId weapon)
        {
            var snap=ground.Session.Capture();
            for(int i=0;i<snap.Lives.Length;i++)
            {snap.Lives[i].ShotgunOwned=snap.Lives[i].RocketOwned=snap.Lives[i].CutterOwned=true;snap.Lives[i].ShotgunAmmo=snap.Lives[i].RocketAmmo=20;snap.Lives[i].CutterEnergy=30;}
            snap.Lives[1].Health=1;ground.Session.Restore(snap);
            var a=new LocalAction[ground.Session.ParticipantCount];a[0].SelectWeapon=weapon==WeaponId.Shotgun?WeaponSelection.Shotgun:weapon==WeaponId.RocketLauncher?WeaponSelection.RocketLauncher:weapon==WeaponId.Cutter?WeaponSelection.Cutter:WeaponSelection.Rifle;
            ground.Session.Tick(a,.02f);Step(60);
        }
        void Spectate(Vector3 center)
        {
            if(!reviewCamera){var go=new GameObject("ragdoll-review-camera");go.transform.SetParent(transform);reviewCamera=go.AddComponent<Camera>();reviewCamera.depth=100;reviewCamera.fieldOfView=55;reviewCamera.nearClipPlane=.05f;
                // First-person layers 15–18 are the existing viewport routing invariant.
                reviewCamera.cullingMask=~((1<<15)|(1<<16)|(1<<17)|(1<<18));}
            var aim=center+Vector3.up*.3f;var best=Vector3.zero;float clearance=-1;
            // Diagnostic camera candidates avoid authored supports; these are QA framing constants.
            foreach(var offset in new[]{new Vector3(4,2.5f,-4),new Vector3(-4,2.5f,-4),new Vector3(4,2.5f,4),new Vector3(-4,2.5f,4),new Vector3(0,6,-1)})
            {
                float distance=offset.magnitude;
                if(Physics.SphereCast(aim,.1f,offset.normalized,out var hit,distance,(1<<ProvingArena.WorldLayer)|(1<<ProvingArena.MovementOnlyLayer),QueryTriggerInteraction.Ignore))distance=Mathf.Max(.2f,hit.distance-.2f);
                if(distance>clearance){clearance=distance;best=offset.normalized*distance;}
            }
            reviewCamera.transform.position=aim+best;reviewCamera.transform.LookAt(aim);
        }
        IEnumerator Capture(string path)
        {Render();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(path);yield return new WaitForSecondsRealtime(.03f);}
        IEnumerator Start()
        {
            directory=Environment.GetCommandLineArgs()[Array.IndexOf(Environment.GetCommandLineArgs(),"-deathRagdollReview")+1];Directory.CreateDirectory(directory);
            AudioListener.volume=0;Application.runInBackground=true;yield return new WaitForSecondsRealtime(1);
            ground=GetComponent<ProvingGround>();ground.enabled=false;pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            foreach(var weapon in new[]{WeaponId.Rifle,WeaponId.Shotgun,WeaponId.RocketLauncher,WeaponId.Cutter})
            {
                ground.StartCombatReview(new[]{pads[0]},backgroundDiagnostic:true,ensureOpponent:true);Check(ground.Running,"QA did not start a match");Equip(weapon);
                ground.PlaceCombatReviewSeat(0,new Vector3(-20,0,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(-20,0,0),0,180);
                // Author the lethal fixture after settling/placement; map pickups cannot add armor first.
                var lethal=ground.Session.Capture();lethal.Lives[1].Health=1;lethal.Lives[1].Armor=0;ground.Session.Restore(lethal);Render();
                Check(ground.Session.Life(0).SelectedWeapon==weapon,"Weapon selection fixture failed: "+weapon);
                DeathNotice fatal=default;bool died=false;Action<DeathNotice> onDeath=n=>{if(n.Seat==1){fatal=n;died=true;}};ground.Session.Died+=onDeath;
                var a=new LocalAction[ground.Session.ParticipantCount];a[0].Fire=true;a[0].FireHeld=true;
                for(int i=0;i<60&&!died;i++){ground.Session.Tick(a,.02f);Render();a[0].Fire=false;}
                ground.Session.Died-=onDeath;Check(died&&fatal.Impact.Valid&&fatal.Impact.Weapon==weapon,"Missing real fatal context: "+weapon);
                var body=Body();var start=body.Ragdoll.Center;Spectate(start);var frames=Path.Combine(directory,weapon.ToString());Directory.CreateDirectory(frames);
                for(int frame=0;frame<=30;frame++){yield return Capture(Path.Combine(frames,frame.ToString("D3")+".png"));if(frame<30)Step(5);}
                Step(150);Spectate(body.Ragdoll.Center);yield return Capture(Path.Combine(frames,"settled.png"));
                var resting=body.Ragdoll.Center;Step(25);Check(Vector3.Distance(resting,body.Ragdoll.Center)<.3f,"Final corpse pose did not settle: "+weapon);
                var hand=body.GetComponentsInChildren<Transform>().Single(t=>t.name=="LeftHand");var frozen=hand.position;double clock=ground.Session.Time;
                yield return new WaitForSecondsRealtime(.2f);Render();Check(hand.position==frozen&&ground.Session.Time==clock,"Pause drift");
                Check(body.GetComponentsInChildren<Collider>(),"Corpse collider leaked into gameplay");
                measurements.Add(new Measurement{weapon=weapon.ToString(),direction=fatal.Impact.Direction,impulse=fatal.Impact.Velocity(ground.DeathProfile),travel=body.Ragdoll.Center-start,finalSegmentSpeed=body.Ragdoll.MaximumSegmentSpeed,handSeparation=Vector3.Distance(hand.position,body.GetComponentsInChildren<Transform>().Single(t=>t.name=="RightHand").position),sleeping=body.Ragdoll.Sleeping});
            }
            // Airborne death: initial pose must be retained instead of being projected onto support.
            ground.StartCombatReview(new[]{pads[0]},backgroundDiagnostic:true,ensureOpponent:true);Check(ground.Running,"QA did not start a match");ground.PlaceCombatReviewSeat(1,new Vector3(-20,2,0),0,180);
            var moving=new LocalAction[ground.Session.ParticipantCount];moving[1].Move=Vector2.right;
            for(int i=0;i<6;i++)ground.Session.Tick(moving,.02f);Render();Check(ground.Session.Pose(1).Velocity.sqrMagnitude>.1f,"Airborne movement fixture has no momentum");
            ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,500);var airborne=Body();var airStart=airborne.Ragdoll.Center;Spectate(new Vector3(-20,.5f,0));
            for(int frame=0;frame<=25;frame++){yield return Capture(Path.Combine(directory,"air-"+frame.ToString("D3")+".png"));if(frame<25)Step(5);}
            Check(airborne.Ragdoll.Center.y<airStart.y-1,"Airborne body did not fall");Check(airborne.Ragdoll.Center.y>-1.5f,"Body fell through support");
            // Contact review on actual authored steps and beside an actual wall.
            foreach(string contour in new[]{"steps","wall","edge"})
            {
                ground.StartCombatReview(new[]{pads[0]},backgroundDiagnostic:true,ensureOpponent:true);Check(ground.Running,"QA did not start a match");
                var supports=ground.GetComponentsInChildren<BoxCollider>().Where(c=>c.gameObject.layer==ProvingArena.WorldLayer||c.gameObject.layer==ProvingArena.MovementOnlyLayer).ToArray();
                var support=contour=="steps"?supports.First(c=>c.name.Contains("-step-")):contour=="edge"?supports.First(c=>c.name=="balcony-west"):supports.First(c=>c.bounds.size.y>3&&c.bounds.size.x<2);
                var feet=contour=="steps"?new Vector3(support.bounds.center.x,support.bounds.max.y+.2f,support.bounds.center.z):contour=="edge"?new Vector3(support.bounds.max.x+.5f,support.bounds.max.y+.2f,0):new Vector3(support.bounds.max.x+.6f,0,support.bounds.center.z);
                ground.PlaceCombatReviewSeat(1,feet,0);Render();ground.Session.ApplyDamage(1,ground.Session.Life(1).Life,500);var contact=Body();Spectate(feet);
                for(int frame=0;frame<=20;frame++){Spectate(contact.Ragdoll.Center);yield return Capture(Path.Combine(directory,contour+"-"+frame.ToString("D3")+".png"));if(frame<20)Step(5);}
                Check(contact.Ragdoll.MaximumSegmentSpeed<ground.DeathProfile.Get("corpse.maximumSpeed"),"Unstable "+contour+" contact");
                if(contour=="edge")Check(contact.Ragdoll.Center.y<feet.y-1,"Body did not fall from platform edge");
            }
            // Four simultaneous independent corpse identities and their existing per-seat killcams.
            reviewCamera.enabled=false;ground.StartCombatReview(pads,backgroundDiagnostic:true);
            for(int i=0;i<4;i++){ground.PlaceCombatReviewSeat(i,new Vector3(-20,0,-5+i*2),0,180);ground.Session.ApplyDamage(i,ground.Session.Life(i).Life,500);}
            measureTicks=true;Step(60);measureTicks=false;yield return Capture(Path.Combine(directory,"four-views.png"));Check(ground.GetComponentsInChildren<TrooperVisual>().Count(v=>v.Ragdoll!=null)==4,"Missing simultaneous corpse");
            Step(130);Check(Enumerable.Range(0,4).All(i=>!ground.Session.Life(i).Dead),"Respawn failed");
            ground.StartCombatReview(pads,backgroundDiagnostic:true);yield return null;Check(!ground.GetComponentsInChildren<TrooperVisual>().Any(v=>v.Ragdoll!=null),"Repeat leaked ragdolls");
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{muted=AudioListener.volume==0,arena=ground.Session.ArenaIdentity,profile=ground.Session.DesignProfile.Hash,measurements=measurements.ToArray(),diagnosticTickMeanMs=tickMs.Average(),diagnosticTickMaxMs=tickMs.Max()},true));
            Debug.Log("DEATH_RAGDOLL_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void Check(Collider[] colliders,string message)=>Check(colliders.Length==0,message);
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
        [Serializable]sealed class Measurement{public string weapon;public Vector3 direction,impulse,travel;public float finalSegmentSpeed,handSeparation;public bool sleeping;}
        [Serializable]sealed class Result{public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",arena,profile;public double diagnosticTickMeanMs,diagnosticTickMaxMs;public bool muted;public Measurement[] measurements;}
    }
}
#endif

#endif
