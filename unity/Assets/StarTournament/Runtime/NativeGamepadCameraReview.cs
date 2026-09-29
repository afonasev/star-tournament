#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace StarTournament.ProvingGround
{
    public sealed class NativeGamepadCameraReview : MonoBehaviour
    {
        ProvingGround ground;string directory;Gamepad[] pads;
        Button Button(string name)=>ground.GetComponentsInChildren<Button>(true).Single(x=>x.name==name);
        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot(Path.Combine(directory,name+".png"));
            File.WriteAllText(Path.Combine(directory,name+".json"),JsonUtility.ToJson(ground.Session.Capture(),true));
            yield return new WaitForSecondsRealtime(.3f);
        }
        IEnumerator Press(Gamepad pad,GamepadButton button)
        {InputSystem.QueueStateEvent(pad,new GamepadState().WithButton(button));yield return null;yield return null;InputSystem.QueueStateEvent(pad,new GamepadState());yield return null;}
        IEnumerator Start()
        {
            Application.runInBackground=true;ground=GetComponent<ProvingGround>();
            var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-gamepadCameraReview");
            if(at<0||at+1>=args.Length||!Path.IsPathFullyQualified(args[at+1]))throw new ArgumentException("Absolute camera review path required");
            directory=args[at+1];Directory.CreateDirectory(directory);yield return new WaitForSecondsRealtime(1);
            Button("main-action-4").onClick.Invoke();yield return Capture("01-image-checkbox");
            Button("settings-section-1").onClick.Invoke();yield return Capture("02-camera-sliders");
            Button("settings-controls-help").onClick.Invoke();yield return Capture("03-controls-help");
            Button("settings-help-back").onClick.Invoke();Button("settings-section-2").onClick.Invoke();yield return Capture("04-interface-checkbox");
            Button("settings-back").onClick.Invoke();
            int profilesAt=Array.IndexOf(args,"-profilesPath");
            if(profilesAt<0)throw new ArgumentException("Isolated profilesPath required for camera review");
            var general=GamepadLookSettings.General(ground.Profile);
            Button("main-action-3").onClick.Invoke();
            ground.GetComponentsInChildren<InputField>(true).Single(x=>x.name=="profile-name-input").text="QA Новый";
            Button("profile-create").onClick.Invoke();yield return null;
            var created=new PlayerProfileCatalog(args[profilesAt+1]).Profiles.Single(x=>x.Name=="QA Новый");
            if(created.GamepadLookVersion!=1||created.GamepadHorizontal!=general.Horizontal||created.GamepadVertical!=general.Vertical||created.GamepadAutoLevel!=general.AutoLevel)throw new InvalidOperationException("New profile did not inherit general gamepad settings");
            Button("profile-settings").onClick.Invoke();yield return Capture("04a-new-profile-inherited");Button("settings-back").onClick.Invoke();yield return null;
            Button("profile-qa-camera-profile").onClick.Invoke();Button("profile-settings").onClick.Invoke();
            var horizontal=ground.GetComponentsInChildren<Slider>(true).Single(x=>x.name=="settings-gamepad-horizontal");horizontal.value=123;
            if(Button("settings-section-0").interactable||Button("settings-back").navigation.selectOnDown!=Button("settings-section-1"))throw new InvalidOperationException("Personal profile settings navigation is invalid");
            yield return Capture("04b-named-profile-sliders");Button("settings-back").onClick.Invoke();
            if(new PlayerProfileCatalog(args[profilesAt+1]).Find("qa-camera-profile").GamepadHorizontal!=123||GamepadLookSettings.General(ground.Profile).Horizontal!=general.Horizontal)throw new InvalidOperationException("Named profile settings did not persist in isolation");
            Button("profiles-back").onClick.Invoke();Button("main-action-0").onClick.Invoke();
            while(ground.LocalSeatCount>2)Button("seats-minus").onClick.Invoke();
            pads=new Gamepad[2];for(int i=0;i<2;i++){pads[i]=InputSystem.AddDevice<Gamepad>();yield return Press(pads[i],GamepadButton.North);Button("identity-guest").onClick.Invoke();}
            Button("Начать — четыре игрока").onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
            if(!Application.isFocused||!ground.Running)throw new InvalidOperationException("NATIVE_CAMERA_QA_REQUIRES_FOCUSED_PLAYER");
            yield return Press(pads[0],GamepadButton.Start);Button("pause-action-0-1").onClick.Invoke();
            // This fixture exercises the enabled return even when the user's general preference is off.
            ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="pause-setting-0-3").isOn=true;
            yield return Capture("05-personal-sliders-split-screen");
            Button("pause-setting-0-5").onClick.Invoke();yield return Capture("06-personal-help");
            Button("pause-help-back-0").onClick.Invoke();Button("pause-setting-0-6").onClick.Invoke();Button("pause-action-0-0").onClick.Invoke();
            var arena=ground.CameraReviewArena;var transition=arena.ReadNavigationTransitions().First(t=>t.Id.Contains("outer-rise"));
            var feet=transition.OrderedFeet;var delta=feet[feet.Length-1]-feet[0];float yaw=Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg;
            var midpoint=feet[feet.Length/2];ground.PlaceCombatReviewSeat(0,midpoint,30,yaw);
            yield return Capture("07-stairs-manual-pitch");yield return new WaitForSecondsRealtime(3);
            if(!ground.Running)throw new InvalidOperationException("Player lost focus during camera review");
            if(ground.Session.Pose(0).Pitch>=-1)throw new InvalidOperationException("Stair pitch did not return upward");
            yield return Capture("08-stairs-return-up");
            ground.PlaceCombatReviewSeat(0,midpoint,-30,yaw+180);yield return new WaitForSecondsRealtime(3);yield return Capture("09-stairs-return-down");
            if(ground.Session.Pose(0).Pitch<=1)throw new InvalidOperationException("Stair pitch did not return downward");
            ground.PlaceCombatReviewSeat(0,midpoint,30,yaw+90);yield return new WaitForSecondsRealtime(3);yield return Capture("10-stairs-return-across");
            if(Mathf.Abs(ground.Session.Pose(0).Pitch)>1)throw new InvalidOperationException("Across-stair target is not horizontal");
            var flat=arena.Spawns[0];ground.PlaceCombatReviewSeat(0,flat,30,0);yield return new WaitForSecondsRealtime(3);yield return Capture("11-flat-return");
            ground.PlaceCombatReviewSeat(0,flat,30,0);yield return Press(pads[0],GamepadButton.South);
            if(ground.Session.Pose(0).Grounded||Mathf.Abs(ground.Session.Pose(0).Pitch-30)>.1f)throw new InvalidOperationException("Airborne assistance changed pitch");
            yield return Capture("11a-airborne");
            yield return new WaitForSecondsRealtime(3);yield return Capture("11b-landing-return");
            if(!ground.Session.Pose(0).Grounded||Mathf.Abs(ground.Session.Pose(0).Pitch)>1)throw new InvalidOperationException("Landing return failed");
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.name="QA continuous ramp fixture";ramp.layer=ProvingArena.WorldLayer;
            ramp.transform.position=new Vector3(80,10,80);ramp.transform.localScale=new Vector3(20,1,30);ramp.transform.rotation=Quaternion.Euler(-15,0,0);Physics.SyncTransforms();
            Physics.Raycast(ramp.transform.position+Vector3.up*5,Vector3.down,out var rampHit,10,1<<ProvingArena.WorldLayer);
            ground.PlaceCombatReviewSeat(0,rampHit.point+Vector3.up*.02f,30,0);yield return new WaitForSecondsRealtime(3);yield return Capture("11c-continuous-ramp");
            if(Mathf.Abs(ground.Session.Pose(0).Pitch-CharacterMotor.SurfacePitch(rampHit.normal,Vector3.forward))>1)throw new InvalidOperationException("Continuous ramp pitch failed");
            Destroy(ramp);ground.PlaceCombatReviewSeat(0,flat,0,0);
            InputSystem.QueueStateEvent(pads[0],new GamepadState{rightStick=new Vector2(.5f,.5f)});yield return new WaitForSecondsRealtime(.3f);yield return Capture("12-manual-stick-priority");
            if(ground.Session.Pose(0).LookNeutralSeconds!=0||ground.Session.Pose(0).Pitch>=-1)throw new InvalidOperationException("Manual stick priority failed");
            InputSystem.QueueStateEvent(pads[0],new GamepadState());yield return Press(pads[0],GamepadButton.Start);
            Button("pause-action-0-1").onClick.Invoke();var toggle=ground.GetComponentsInChildren<Toggle>(true).Single(x=>x.name=="pause-setting-0-3");toggle.isOn=false;
            Button("pause-setting-0-6").onClick.Invoke();Button("pause-action-0-0").onClick.Invoke();
            ground.PlaceCombatReviewSeat(0,flat,25,0);yield return new WaitForSecondsRealtime(2);yield return Capture("13-return-disabled");
            if(Mathf.Abs(ground.Session.Pose(0).Pitch-25)>.1f)throw new InvalidOperationException("Disabled assistance changed pitch");
            InputSystem.DisableDevice(pads[0]);yield return null;yield return null;var paused=ground.Session.Time;
            yield return new WaitForSecondsRealtime(.5f);yield return Capture("14-disconnected-pause");
            if(ground.Running||ground.Session.Time!=paused)throw new InvalidOperationException("Disconnect did not freeze gameplay");
            InputSystem.EnableDevice(pads[0]);yield return null;Button("pause-action-0-0").onClick.Invoke();yield return new WaitForSecondsRealtime(.5f);
            if(!ground.Running||Mathf.Abs(ground.Session.Pose(0).Pitch-25)>.1f)throw new InvalidOperationException("Reconnect lost personal assistance preference");
            yield return Capture("15-reconnected");
            File.WriteAllText(Path.Combine(directory,"complete.json"),"{\"classification\":\"SYNTHETIC_NATIVE_PLAYER_NOT_PHYSICAL_ACCEPTANCE\",\"muted\":true}");
            Debug.Log("NATIVE_GAMEPAD_CAMERA_REVIEW_COMPLETE");
            foreach(var pad in pads)if(pad.added)InputSystem.RemoveDevice(pad);
            Application.Quit();
        }
    }
}
#endif
