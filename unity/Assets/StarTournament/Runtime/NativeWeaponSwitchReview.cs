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
    /// <summary>Scripted native Player evidence; synthetic devices are not physical-controller acceptance.</summary>
    public sealed class NativeWeaponSwitchReview:MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;readonly List<string> checks=new List<string>();
        GameObject View(int seat)=>ground.GetComponentsInChildren<Camera>().Single(c=>c.name=="seat-camera-"+(seat+1)).transform.Find("trooper-view").gameObject;
        Transform Bone(int seat,string name)=>View(seat).GetComponentsInChildren<Transform>(true).Single(t=>t.name==name);
        void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        void Render(){ground.SendMessage("RefreshInterface");ground.SendMessage("LateUpdate");}
        void TickTo(double remaining)
        {
            int budget=1000;
            while(ground.Session.Life(0).SwitchRemaining>remaining&&budget-->0)
                ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],(float)Math.Min(.02,ground.Session.Life(0).SwitchRemaining-remaining));
            Require(budget>0,"Switch failed to advance");Render();
        }
        void RequireModel(int seat,WeaponId expected)
        {
            var view=View(seat);var model=view.GetComponent<WeaponModelPresentation>();
            Require(model.ShownWeapon==expected,"Rendered weapon changed outside apex: "+expected+" / "+model.ShownWeapon);
            foreach(var pair in new[]{("automatic-rifle",WeaponId.Rifle),("pulse-launcher",WeaponId.RocketLauncher),("cutter",WeaponId.Cutter)})
                Require(view.GetComponentsInChildren<Transform>(true).Single(t=>t.name==pair.Item1).gameObject.activeSelf==(expected==pair.Item2),"Wrong active model: "+pair.Item1);
            Require(view.GetComponentsInChildren<Renderer>(true).Single(t=>t.name=="weapon:joined").enabled==(expected==WeaponId.Shotgun),"Wrong shotgun visibility");
            var body=ground.GetComponentsInChildren<WeaponModelPresentation>().Single(m=>m.gameObject.name=="trooper-presentation"&&m.transform.parent.name=="player-"+(seat+1));
            Require(body.ShownWeapon==expected,"World/view swap mismatch");
        }
        void Select(WeaponSelection selection)
        {
            // This pose diagnostic authors an equipped fixture; physical acquisition is covered by weaponPickupReview.
            var weapon=selection==WeaponSelection.Shotgun?WeaponId.Shotgun:selection==WeaponSelection.RocketLauncher?WeaponId.RocketLauncher:selection==WeaponSelection.Cutter?WeaponId.Cutter:WeaponId.Rifle;
            if(!CombatLife.Owned(ground.Session.Life(0),weapon))
            {
                var fixture=ground.Session.Capture();var life=fixture.Lives[0];
                if(weapon==WeaponId.Shotgun){life.ShotgunOwned=true;life.ShotgunAmmo=(int)ground.LifeProfile.Get("combat.startingAmmo");}
                else if(weapon==WeaponId.RocketLauncher){life.RocketOwned=true;life.RocketAmmo=(int)ground.LifeProfile.Get("rocket.startingAmmo");}
                else {life.CutterOwned=true;life.CutterEnergy=ground.CutterProfile.Get("cutter.energyCapacity");}
                fixture.Lives[0]=life;ground.Session.Restore(fixture);
            }
            var a=new LocalAction[ground.Session.ParticipantCount];a[0].SelectWeapon=selection;ground.Session.Tick(a,.02f);Render();
        }
        IEnumerator Capture(string name)
        {
            Render();Require(ground.Running,"Capture must show the active match");
            Require(!ground.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="setup-screen"&&t.gameObject.activeInHierarchy),"Setup covers gameplay");
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.15f);
        }
        IEnumerator Start()
        {
            var args=Environment.GetCommandLineArgs();directory=args[Array.IndexOf(args,"-weaponSwitchReview")+1];Directory.CreateDirectory(directory);
            ground=GetComponent<ProvingGround>();AudioListener.volume=0;Application.runInBackground=true;
            yield return new WaitForSecondsRealtime(1);ground.enabled=false;
            pads=Enumerable.Range(0,4).Select(_=>InputSystem.AddDevice<Gamepad>()).ToArray();
            foreach(float seconds in new[]{.1f,1f,3f})
            {
                ground.LifeProfile.Set("weapon.switchSeconds",seconds);ground.StartCombatReview(new[]{pads[0]},backgroundDiagnostic:true,ensureOpponent:true);
                ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,5),0,180);Render();
                var rest=View(0).transform.localPosition;var duration=ground.Session.WeaponSwitchSeconds;
                if(seconds==1)yield return Capture("01-rest-rifle");
                foreach(var target in new[]{WeaponSelection.Shotgun,WeaponSelection.Rifle,WeaponSelection.RocketLauncher,WeaponSelection.Rifle,WeaponSelection.Cutter,WeaponSelection.Shotgun,WeaponSelection.RocketLauncher,WeaponSelection.Cutter,WeaponSelection.RocketLauncher,WeaponSelection.Shotgun,WeaponSelection.Cutter,WeaponSelection.Rifle})
                {
                    var from=ground.Session.Life(0).SelectedWeapon;Select(target);
                    var pending=ground.Session.Life(0).PendingWeapon;int shots=ground.Session.ShotCount;RequireModel(0,from);
                    var shoulderLocal=Bone(0,"RightUpperArm").localPosition;
                    TickTo(duration*.75);Require(View(0).transform.localPosition==rest,"View root detached from body");
                    Require(Bone(0,"RightUpperArm").localPosition==shoulderLocal,"Shoulder joint translated");
                    if(seconds==1&&target==WeaponSelection.Shotgun&&from==WeaponId.Rifle)yield return Capture("02-old-weapon-rising");
                    TickTo(duration*.5001);RequireModel(0,from);
                    if(seconds==1&&target==WeaponSelection.Shotgun&&from==WeaponId.Rifle)yield return Capture("02b-old-model-before-apex");
                    TickTo(duration*.5);RequireModel(0,pending);
                    var camera=View(0).transform.parent;
                    float near=camera.GetComponent<Camera>().nearClipPlane;
                    foreach(var handName in new[]{"LeftHand","RightHand"})
                        Require(camera.InverseTransformPoint(Bone(0,handName).position).z>near,"Hand crossed the camera: "+handName);
                    Require(View(0).transform.localPosition==rest,"Midpoint root moved");
                    if(seconds==1&&target==WeaponSelection.Shotgun&&from==WeaponId.Rifle)yield return Capture("03-model-swap-in-raised-hands");
                    TickTo(duration*.25);RequireModel(0,pending);Require(ground.Session.Life(0).SelectedWeapon==from,"Authoritative slot changed early");
                    var phase=WeaponSwitchPose.Read(ground.Session.Life(0),duration);Require(phase.Weapon==pending,"Pending model missing on descent");
                    if(seconds==1&&target==WeaponSelection.Shotgun&&from==WeaponId.Rifle)
                    {
                        yield return Capture("04-new-weapon-descending");
                        var paused=Bone(0,"RightUpperArm").localRotation;double time=ground.Session.Time;yield return new WaitForSecondsRealtime(.25f);Render();
                        Require(ground.Session.Time==time&&Bone(0,"RightUpperArm").localRotation==paused,"Paused pose drifted");checks.Add("pause freezes simulation and pose");
                    }
                    if(seconds==1&&target==WeaponSelection.Cutter&&from==WeaponId.Rifle)yield return Capture("08-cutter-descending");
                    var fire=new LocalAction[ground.Session.ParticipantCount];fire[0].Fire=true;fire[0].FireHeld=true;
                    ground.Session.Tick(fire,(float)(ground.Session.Life(0).SwitchRemaining*.5));Render();Require(ground.Session.ShotCount==shots&&!ground.Session.Beam(0).Active,"Fire during switch");
                    TickTo(.001);
                    // A fresh fire edge on the completion tick must use the resting viewmodel before LateUpdate.
                    var completionFire=new LocalAction[ground.Session.ParticipantCount];completionFire[0].Fire=true;completionFire[0].FireHeld=true;
                    ground.Session.Tick(completionFire,(float)(ground.Session.Life(0).SwitchRemaining+.0001));
                    if(pending==WeaponId.Cutter)Require(ground.Session.Beam(0).Active,"Cutter failed to start on completion tick");
                    else
                    {
                        Require(ground.Session.ShotCount==shots+1,"New weapon failed to fire on completion tick");
                        Require(View(0).transform.localPosition==rest,"Shot effect sampled a lifted muzzle on completion tick");
                    }
                    Render();Require(ground.Session.Life(0).SelectedWeapon==pending,"Slot failed to change");Require(View(0).transform.localPosition==rest,"Hands not at rest on completion");
                    if(seconds==1&&target==WeaponSelection.Shotgun&&from==WeaponId.Rifle)yield return Capture("05-switch-complete-shotgun");
                    checks.Add(seconds+"s "+from+" -> "+pending+" completed at rest; no early fire; first shot on completion");
                    // Diagnostic settling window leaves every slot cooldown ready for the next isolated pair.
                    for(int tick=0;tick<100;tick++)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);
                }
            }
            // Capture a continuous native sequence with the ordinary one-second fixture profile.
            ground.LifeProfile.Set("weapon.switchSeconds",1);ground.StartCombatReview(new[]{pads[0]},backgroundDiagnostic:true,ensureOpponent:true);
            ground.PlaceCombatReviewSeat(0,new Vector3(0,-1.2f,-5),0);ground.PlaceCombatReviewSeat(1,new Vector3(0,-1.2f,5),0,180);
            var frames=Path.Combine(directory,"frames");Directory.CreateDirectory(frames);Select(WeaponSelection.Shotgun);
            for(int frame=0;frame<=20;frame++)
            {
                Render();yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(frames,frame.ToString("D3")+".png"));
                yield return new WaitForSecondsRealtime(.05f);
                if(frame<20)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.05f);
            }
            ground.LifeProfile.Set("weapon.switchSeconds",1);ground.StartCombatReview(pads,backgroundDiagnostic:true);
            for(int i=0;i<4;i++)ground.PlaceCombatReviewSeat(i,new Vector3(0,-1.2f,-6+i*3),0);
            Render();Select(WeaponSelection.Shotgun);TickTo(.75);
            for(int i=1;i<4;i++)
            {
                // Compare the rendered joint to its clean clip at the SAME clock; idle breathing is legitimate.
                var observed=Bone(i,"RightUpperArm").localRotation;var life=ground.Session.Life(i);
                View(i).GetComponent<TrooperVisual>().Render(ground.Session.Time,ground.Session.Pose(i).Velocity,life.Health,life.Life);
                Require(Quaternion.Angle(observed,Bone(i,"RightUpperArm").localRotation)<.01f,"Cross-seat additive pose leaked");
                Require(WeaponSwitchPose.Read(life,ground.Session.WeaponSwitchSeconds).Lift==0,"Cross-seat switch state leaked");
            }
            yield return Capture("06-four-seat-independent-switch");checks.Add("four independent first-person views");
            ground.Session.ApplyDamage(0,ground.Session.Life(0).Life,100);Render();
            int budgetRespawn=500;while(ground.Session.Life(0).Dead&&budgetRespawn-->0)ground.Session.Tick(new LocalAction[ground.Session.ParticipantCount],.02f);Render();
            Require(!ground.Session.Life(0).Dead&&ground.Session.Life(0).SelectedWeapon==WeaponId.Rifle,"Respawn failed");
            var resting=View(0).GetComponent<TrooperVisual>().Tuning;
            Require(View(0).transform.localPosition==new Vector3(resting.Get("view.x"),resting.Get("view.y"),resting.Get("view.z")),"Respawn retained lift");
            checks.Add("death cancels switch; respawn restores rifle and resting hands");yield return Capture("07-respawn-rest");
            File.WriteAllText(Path.Combine(directory,"complete.json"),JsonUtility.ToJson(new Result{arena=ground.Session.ArenaIdentity,profile=ground.Session.DesignProfile.Hash,muted=AudioListener.volume==0,checks=checks.ToArray(),width=Screen.width,height=Screen.height},true));
            Debug.Log("WEAPON_SWITCH_NATIVE_REVIEW_COMPLETE "+directory);Application.Quit();
        }
        void OnDestroy(){if(pads!=null)foreach(var pad in pads)if(pad!=null&&pad.added)InputSystem.RemoveDevice(pad);}
        [Serializable]sealed class Result{public string classification="AUTOMATED_NATIVE_PLAYER_NOT_HUMAN_ACCEPTANCE",arena,profile;public bool muted;public int width,height;public string[] checks;}
    }
}
#endif

#endif
